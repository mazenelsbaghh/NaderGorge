"""Download authenticated exam exports without the web view's second HTTP request."""
import http.client
import json
import os
from pathlib import Path
import re
import shutil
import tempfile
from urllib import error, parse, request
import zipfile
import zlib


XLSX = 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'
MAX_EXPORT_BYTES = 512 * 1024 * 1024
EXPORT_ROUTES = (
    (r'/api/exams/[A-Za-z0-9_-]+/results\.xlsx', XLSX, '.xlsx', 'GET'),
    (r'/api/exams/[A-Za-z0-9_-]+/reports\.zip', 'application/zip', '.zip', 'GET'),
    (r'/api/exams/[A-Za-z0-9_-]+/(?:students|results)\.csv', 'text/csv', '.csv', 'GET'),
    (r'/api/attempts/[A-Za-z0-9_-]+/report\.pdf', 'application/pdf', '.pdf', 'GET'),
    (r'/api/backups/exam-room-[0-9]+\.sqlite3', 'application/octet-stream', '.sqlite3', 'GET'),
    (r'/api/backup', 'application/octet-stream', '.sqlite3', 'POST'),
)


class NoRedirect(request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None


def export_method(path, filename, expected_type):
    if not all(isinstance(part, str) for part in (path, filename, expected_type)):
        raise RuntimeError('طلب تنزيل الملف غير صالح.')
    parsed = parse.urlsplit(path)
    if parsed.scheme or parsed.netloc or parsed.fragment:
        raise RuntimeError('التنزيل متاح من برنامج الامتحان المحلي فقط.')
    if not filename or re.search(r'[\\/:\x00-\x1f]', filename):
        raise RuntimeError('اسم الملف غير صالح.')
    for pattern, mime_type, extension, method in EXPORT_ROUTES:
        if not re.fullmatch(pattern, parsed.path):
            continue
        query = parse.parse_qs(parsed.query, keep_blank_values=True)
        valid_query = (not query or mime_type == XLSX and set(query) == {'groupId'}
                       and len(query['groupId']) == 1
                       and re.fullmatch(r'[A-Za-z0-9_-]*', query['groupId'][0]))
        if valid_query and expected_type == mime_type and filename.lower().endswith(extension):
            return method
        break
    raise RuntimeError('نوع الملف أو رابط التنزيل غير مسموح.')


def validate_archive(source, expected_type):
    with zipfile.ZipFile(source) as archive:
        names = set(archive.namelist())
        if not names or sum(entry.file_size for entry in archive.infolist()) > MAX_EXPORT_BYTES:
            raise RuntimeError('ملف التقارير فارغ أو أكبر من الحد المسموح.')
        if any(entry.flag_bits & 1 for entry in archive.infolist()):
            raise RuntimeError('ملف التقارير مشفر بصيغة غير متوقعة؛ أعد التنزيل.')
        if expected_type == XLSX and not {'[Content_Types].xml', 'xl/workbook.xml',
                                          'xl/worksheets/sheet1.xml'} <= names:
            raise RuntimeError('الملف المستلم ليس شيت Excel صالحًا.')
        if archive.testzip() is not None:
            raise RuntimeError('ملف التقارير غير مكتمل؛ أعد التنزيل.')


def validate_export(source, expected_type):
    source.seek(0)
    signature = source.read(100)
    if expected_type in (XLSX, 'application/zip'):
        validate_archive(source, expected_type)
    elif expected_type == 'application/pdf':
        source.seek(0, 2)
        source.seek(max(0, source.tell() - 4096))
        if not signature.startswith(b'%PDF-') or not source.read().rstrip().endswith(b'%%EOF'):
            raise RuntimeError('تقرير PDF غير مكتمل؛ أعد التنزيل.')
    elif expected_type == 'application/octet-stream':
        if len(signature) < 100 or not signature.startswith(b'SQLite format 3\x00'):
            raise RuntimeError('الملف المستلم ليس نسخة بيانات SQLite صالحة.')
    elif not signature.lstrip(b'\xef\xbb\xbf \r\n\t') or signature.lstrip(b'\xef\xbb\xbf \r\n\t')[:1] in (b'{', b'[', b'<'):
        raise RuntimeError('الملف المستلم ليس كشف CSV صالحًا.')


def write_download(source, destination):
    destination = Path(destination)
    staged_path = None
    try:
        with tempfile.NamedTemporaryFile(prefix='.massar-download-', dir=destination.parent,
                                         delete=False) as staged:
            staged_path = Path(staged.name)
            source.seek(0)
            shutil.copyfileobj(source, staged)
            staged.flush()
            os.fsync(staged.fileno())
        os.replace(staged_path, destination)
    finally:
        if staged_path is not None:
            staged_path.unlink(missing_ok=True)


class ExamDownloads:
    def __init__(self, admin_port, choose_destination):
        self._origin = f'http://127.0.0.1:{int(admin_port)}'
        self._choose_destination = choose_destination
        self._opener = request.build_opener(request.ProxyHandler({}), NoRedirect())

    def _admin_token(self):
        with self._opener.open(self._origin + '/api/bootstrap', timeout=12) as response:
            if response.status != 200 or response.headers.get_content_type() != 'application/json':
                raise RuntimeError('تعذر تجديد الاتصال بلوحة الإدارة.')
            bootstrap = json.loads(response.read(16384))
            token = bootstrap.get('token') if isinstance(bootstrap, dict) else None
            if not isinstance(token, str) or not token:
                raise RuntimeError('تعذر تجديد الاتصال بلوحة الإدارة.')
            return token

    def _receive_export(self, path, method, expected_type, source):
        headers = {'X-Admin-Token': self._admin_token(), 'X-Exam-Request': '1'}
        body = None
        if method == 'POST':
            headers['Content-Type'] = 'application/json'
            body = b'{}'
        export_request = request.Request(self._origin + path, data=body, headers=headers, method=method)
        with self._opener.open(export_request, timeout=300) as response:
            if response.status != 200 or response.headers.get_content_type() != expected_type:
                raise RuntimeError('الاستجابة ليست الملف المطلوب؛ لم يتم حفظ ملف غير صالح.')
            length_header = response.headers.get('Content-Length')
            expected_length = int(length_header) if length_header is not None else None
            while chunk := response.read(65536):
                if source.tell() + len(chunk) > MAX_EXPORT_BYTES:
                    raise RuntimeError('حجم الملف أكبر من الحد المسموح للتنزيل.')
                source.write(chunk)
            if expected_length is not None and source.tell() != expected_length:
                raise RuntimeError('لم يصل الملف بالكامل؛ أعد التنزيل.')

    def download_exam_file(self, path, filename, expected_type):
        method = export_method(path, filename, expected_type)
        try:
            with tempfile.TemporaryFile() as source:
                self._receive_export(path, method, expected_type, source)
                validate_export(source, expected_type)
                selection = self._choose_destination(filename)
                if not selection:
                    return {'saved': False}
                destination = selection[0] if isinstance(selection, (list, tuple)) else selection
                write_download(source, destination)
                return {'saved': True, 'path': str(destination)}
        except error.HTTPError as response:
            response.close()
            if response.code == 401:
                raise RuntimeError('تعذر تجديد جلسة الإدارة؛ أعد فتح البرنامج وحاول التنزيل.') from None
            raise RuntimeError('لم يكتمل تجهيز الملف؛ راجع حالة التقرير وحاول مرة أخرى.') from None
        except (OSError, http.client.HTTPException, ValueError, zipfile.BadZipFile,
                zlib.error, NotImplementedError):
            raise RuntimeError('تعذر تنزيل ملف سليم أو حفظه؛ تأكد من تشغيل البرنامج والمساحة المتاحة ثم أعد المحاولة.') from None
