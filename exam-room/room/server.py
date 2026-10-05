from http.cookies import CookieError, SimpleCookie
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import urlparse
import json
import logging
import mimetypes
import secrets
import socket
import threading
import time

from .demo import DEMO_EXAM
from .reports import students_csv, render_report, results_csv
from .validation import RoomError
from .storage import storage_details, backup_path, check_database, open_data_folder
from .key_vault import valid_key


WEB_ROOT = (Path(__file__).resolve().parents[1] / "web").resolve()


def local_addresses():
    try:
        addresses = {entry[4][0] for entry in socket.getaddrinfo(socket.gethostname(), None, socket.AF_INET)}
    except socket.gaierror:
        addresses = set()
    with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as probe:
        try:
            probe.connect(("192.0.2.1", 1))  # Selects the default interface without sending a packet.
            addresses.add(probe.getsockname()[0])
        except OSError:
            logging.info("No default network route; using available local addresses")
    return sorted(address for address in addresses if not address.startswith(("127.", "169.254.")))


class RoomServer(ThreadingHTTPServer):
    daemon_threads = True
    request_queue_size = 256

    def __init__(self, address, room, role):
        self.room = room
        self.role = role
        self.admin_token = secrets.token_urlsafe(32)
        self.addresses = local_addresses()
        self.allowed_hosts = {"localhost", "127.0.0.1"} | set(self.addresses)
        self.student_port = None
        self.slots = threading.BoundedSemaphore(128)
        super().__init__(address, RoomHandler)

    def process_request(self, request, client_address):
        self.slots.acquire()
        try:
            super().process_request(request, client_address)
        except BaseException:
            self.slots.release()
            raise

    def process_request_thread(self, request, client_address):
        try:
            super().process_request_thread(request, client_address)
        finally:
            self.slots.release()


class RoomHandler(BaseHTTPRequestHandler):
    server_version = "MassarExamRoom"

    def setup(self):
        super().setup()
        self.connection.settimeout(12)

    def log_message(self, format_string, *args):
        pass  # HTTP paths may contain student identifiers; application errors are logged separately.

    def do_GET(self):
        self._dispatch_request()

    def do_POST(self):
        self._dispatch_request()

    def _dispatch_request(self):
        try:
            self._verify_request()
            if self.path.startswith("/api/") or self.path.startswith("/report/"):
                self._route_api()
            else:
                self._serve_file()
        except RoomError as error:
            self._json({"error": str(error)}, error.status)
        except (BrokenPipeError, ConnectionResetError, TimeoutError):
            return
        except Exception:
            logging.exception("Request failed (%s)", self.command)
            self._json({"error": "تعذر إكمال العملية. لم يتم تأكيد الحفظ؛ حاول مجددًا وراجع سجل التشغيل."}, 500)

    def _verify_request(self):
        host = self.headers.get("Host", "")
        try:
            parsed = urlparse("//" + host)
            port = parsed.port
        except ValueError as error:
            raise RoomError("عنوان الشبكة غير صالح") from error
        if parsed.hostname not in self.server.allowed_hosts or port != self.server.server_port:
            raise RoomError("عنوان الشبكة غير مسموح", 403)
        if self.server.role == "admin" and parsed.hostname not in ("127.0.0.1", "localhost"):
            raise RoomError("الإدارة متاحة على جهاز التشغيل فقط", 403)
        origin = self.headers.get("Origin")
        if origin and origin != "http://" + host:
            raise RoomError("مصدر الطلب غير مسموح", 403)
        if self.command == "POST":
            if self.headers.get("X-Exam-Request") != "1":
                raise RoomError("طلب غير مسموح", 403)
            if self.headers.get("Content-Type", "").split(";")[0] != "application/json":
                raise RoomError("صيغة الطلب غير مدعومة", 415)

    def _cookie(self, name):
        jar = SimpleCookie()
        try:
            jar.load(self.headers.get("Cookie", ""))
        except CookieError:
            return ""
        return jar[name].value if name in jar else ""

    def _payload(self):
        try:
            length = int(self.headers.get("Content-Length", "0"))
        except ValueError as error:
            raise RoomError("حجم الطلب غير صالح") from error
        if not 0 < length <= 1500000 or self.headers.get("Transfer-Encoding"):
            raise RoomError("حجم الطلب غير مسموح", 413)
        try:
            payload = json.loads(self.rfile.read(length))
        except (ValueError, UnicodeDecodeError) as error:
            raise RoomError("تعذر قراءة الطلب") from error
        if not isinstance(payload, dict):
            raise RoomError("صيغة الطلب غير صالحة")
        return payload

    def _route_api(self):
        if self.server.role == "admin":
            self._admin_api()
        else:
            self._student_api()

    def _student_api(self):
        room = self.server.room
        token = self._cookie("massar_student")
        route = (self.command, self.path)
        if route == ("GET", "/api/lounge"):
            self._json(room.lounge())
        elif route == ("GET", "/api/session"):
            self._json(room.session(token))
        elif route == ("POST", "/api/join"):
            response, token = room.join(self._payload(), token)
            self._json(response, cookie=f"massar_student={token}; Path=/; HttpOnly; SameSite=Strict; Max-Age=604800")
        elif route == ("POST", "/api/answers"):
            self._json(room.save_answers(token, self._payload()))
        elif route == ("POST", "/api/leave"):
            self._payload()
            session = room.session(token)["session"]
            if session and session["state"] != "submitted":
                raise RoomError("سلّم المحاولة الحالية قبل الدخول لامتحان آخر", 409)
            self._json({"session": None}, cookie="massar_student=; Path=/; HttpOnly; SameSite=Strict; Max-Age=0")
        else:
            raise RoomError("الصفحة غير موجودة", 404)

    def _admin_api(self):
        if self.path == "/api/bootstrap" and self.command == "GET":
            self._json({"token": self.server.admin_token,
                        "studentUrls": [f"http://{address}:{self.server.student_port}" for address in self.server.addresses],
                        "localStudentUrl": f"http://127.0.0.1:{self.server.student_port}"},
                       cookie=f"massar_admin={self.server.admin_token}; Path=/; HttpOnly; SameSite=Strict")
            return
        presented = self.headers.get("X-Admin-Token", "") if self.command == "POST" else self._cookie("massar_admin")
        if not secrets.compare_digest(presented, self.server.admin_token):
            raise RoomError("حدّث لوحة الإدارة لإعادة الاتصال", 401)
        if self.command == "GET":
            self._admin_query()
        else:
            self._admin_command()

    def _admin_query(self):
        room = self.server.room
        parts = self.path.split("/")
        if self.path == "/api/status":
            self._json({"serverTime": time.time()})
        elif self.path == "/api/storage":
            self._json(storage_details(room.database))
        elif self.path == "/api/settings/gemini":
            vault = self.server.grader.vault
            self._json({'configured': vault.configured(), 'path': str(vault.path)})
        elif len(parts) == 5 and parts[1:3] == ["api", "exams"] and parts[4] == 'essay-batch':
            self._json(self.server.grader.status(parts[3]))
        elif len(parts) == 4 and parts[1:3] == ["api", "backups"]:
            backup = backup_path(room.database, parts[3])
            self._send(backup.read_bytes(), "application/octet-stream", filename=backup.name)
        elif self.path == "/api/exams":
            self._json({"exams": room.list_exams()})
        elif len(parts) == 4 and parts[1:3] == ["api", "exams"]:
            self._json(room.dashboard(parts[3]))
        elif len(parts) == 5 and parts[1:3] == ["api", "exams"]:
            dashboard = room.dashboard(parts[3])
            exports = {"students.csv": students_csv, "results.csv": results_csv}
            if parts[4] not in exports:
                raise RoomError("الصفحة غير موجودة", 404)
            self._send(exports[parts[4]](dashboard), "text/csv; charset=utf-8", filename=parts[4])
        elif len(parts) == 3 and parts[1] == "report":
            self._send(render_report(room.report(parts[2])), "text/html; charset=utf-8")
        else:
            raise RoomError("الصفحة غير موجودة", 404)

    def _admin_command(self):
        room = self.server.room
        payload = self._payload()
        parts = self.path.split("/")
        if self.path == "/api/exams":
            self._json(room.create_exam(payload))
        elif self.path == "/api/demo":
            created = room.create_exam(DEMO_EXAM)
            self._json(created)
        elif self.path == "/api/storage/check":
            self._json(check_database(room.database))
        elif self.path == "/api/storage/open-folder":
            self._json(open_data_folder(room.database))
        elif self.path == "/api/storage/backup":
            backup = room.database.backup()
            self._json({"name": backup.name})
        elif self.path == "/api/settings/gemini":
            key = payload.get('apiKey')
            if not valid_key(key):
                raise RoomError('اكتب مفتاح Gemini API صحيحًا')
            try:
                self.server.grader.vault.save(key)
            except OSError as error:
                raise RoomError('تعذر حفظ المفتاح في ملف الإعدادات', 503) from error
            self._json({'configured': True, 'path': str(self.server.grader.vault.path)})
        elif len(parts) == 5 and parts[1:3] == ["api", "exams"] and parts[4] == 'essay-batch':
            self._json(self.server.grader.start(parts[3], payload))
        elif len(parts) == 6 and parts[1:3] == ["api", "exams"] and parts[4:] == ['essay-batch', 'stop']:
            self._json(self.server.grader.cancel_current(parts[3]))
        elif self.path == "/api/backup":
            backup = room.database.backup()
            self._send(backup.read_bytes(), "application/octet-stream", filename=backup.name)
        elif len(parts) == 5 and parts[1:3] == ["api", "exams"]:
            self._exam_command(parts[3], parts[4], payload)
        elif len(parts) == 5 and parts[1:3] == ["api", "attempts"]:
            if parts[4] == "grade":
                self._json(room.grade_essay(parts[3], payload))
            elif parts[4] == "reset-login":
                self._json(room.reset_login(parts[3]))
            else:
                raise RoomError("إجراء غير موجود", 404)
        else:
            raise RoomError("إجراء غير موجود", 404)

    def _exam_command(self, exam_id, action, payload):
        room = self.server.room
        if action == "save":
            self._json(room.update_exam(exam_id, payload))
        elif action == "duplicate":
            self._json(room.duplicate_exam(exam_id))
        else:
            response = room.change_state(exam_id, action)
            room.database.backup()
            self._json(response)

    def _serve_file(self):
        if self.command != "GET":
            raise RoomError("الصفحة غير موجودة", 404)
        if self.path == "/":
            target = WEB_ROOT / ("admin.html" if self.server.role == "admin" else "student.html")
        elif self.path.startswith("/assets/"):
            target = (WEB_ROOT / self.path.lstrip("/")).resolve()
            if not target.is_relative_to(WEB_ROOT / "assets"):
                raise RoomError("الصفحة غير موجودة", 404)
            if self.server.role == "student" and target.name in ("admin.js", "editor.js", "storage.js", "report.js", "report.css"):
                raise RoomError("الصفحة غير موجودة", 404)
        else:
            raise RoomError("الصفحة غير موجودة", 404)
        if not target.is_file():
            raise RoomError("الصفحة غير موجودة", 404)
        mime = mimetypes.guess_type(target)[0] or "application/octet-stream"
        if target.suffix == ".js":
            mime = "text/javascript; charset=utf-8"
        self._send(target.read_bytes(), mime)

    def _json(self, response, status=200, cookie=None):
        self._send(json.dumps(response, ensure_ascii=False).encode(), "application/json; charset=utf-8", status, cookie=cookie)

    def _send(self, body, content_type, status=200, **headers):
        self.send_response(status)
        self.send_header("Content-Type", content_type)
        self.send_header("Content-Length", str(len(body)))
        self.send_header("Cache-Control", "no-store")
        self.send_header("X-Content-Type-Options", "nosniff")
        self.send_header("Referrer-Policy", "no-referrer")
        self.send_header("Content-Security-Policy", "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; font-src 'self'; connect-src 'self'; frame-ancestors 'none'; base-uri 'none'; form-action 'self'")
        if headers.get("cookie"):
            self.send_header("Set-Cookie", headers["cookie"])
        if headers.get("filename"):
            self.send_header("Content-Disposition", f'attachment; filename="{headers["filename"]}"')
        self.end_headers()
        self.wfile.write(body)
