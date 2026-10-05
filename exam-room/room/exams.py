import hashlib
import json
import secrets
import sqlite3
import time
import uuid

from .database import audit
from .validation import (RoomError, boolean_field, integer_field, normalize_code,
                         normalize_phone, text_field, validate_answers, validate_exam,
                         validate_grade)


def encode(payload):
    return json.dumps(payload, ensure_ascii=False, separators=(",", ":"))


def exam_record(connection, exam_id):
    record = connection.execute("SELECT * FROM exams WHERE id=?", (exam_id,)).fetchone()
    if record is None:
        raise RoomError("الامتحان غير موجود", 404)
    return dict(record) | {"config": json.loads(record["config"])}


def attempt_record(connection, attempt_id):
    record = connection.execute("SELECT * FROM attempts WHERE id=?", (attempt_id,)).fetchone()
    if record is None:
        raise RoomError("محاولة الطالب غير موجودة", 404)
    return dict(record)


def assigned_questions(exam, attempt):
    lookup = {question["id"]: question for question in exam["config"]["questions"]}
    return [lookup[question_id] for question_id in json.loads(attempt["question_ids"])]


def finalize_attempt(connection, attempt, exam, reason):
    if attempt["submitted_at"] is not None:
        return
    answers = json.loads(attempt["answers"])
    grades = {}
    for question in assigned_questions(exam, attempt):
        answer = answers.get(question["id"])
        if question["kind"] == "mcq":
            grades[question["id"]] = {"score": question["points"] if answer == question["correct"] else 0,
                                      "feedback": "تصحيح تلقائي"}
        elif not isinstance(answer, str) or not answer.strip():
            grades[question["id"]] = {"score": 0, "feedback": "لم تُكتب إجابة"}
    connection.execute("UPDATE attempts SET submitted_at=?, submit_reason=?, grades=? WHERE id=?",
                       (time.time(), reason, encode(grades), attempt["id"]))
    audit(connection, "submitted:" + reason, attempt["id"])


def expire_attempt(connection, attempt, exam):
    now = time.time()
    if attempt["submitted_at"] is None and attempt["deadline"] is not None and now >= attempt["deadline"]:
        finalize_attempt(connection, attempt, exam, "timeout")
        return attempt_record(connection, attempt["id"])
    return attempt


def student_view(exam, attempt):
    running = exam["state"] == "running" and attempt["submitted_at"] is None
    return {"id": attempt["id"], "name": attempt["name"], "examId": exam["id"],
            "title": exam["config"]["title"], "instructions": exam["config"]["instructions"],
            "state": "submitted" if attempt["submitted_at"] is not None else exam["state"],
            "serverTime": time.time(), "deadline": attempt["deadline"],
            "revision": attempt["revision"], "submittedAt": attempt["submitted_at"],
            "questionCount": exam["config"]["questionCount"],
            "minutes": exam["config"]["minutes"], "timerMode": exam["config"]["timerMode"],
            "answers": json.loads(attempt["answers"]) if running else {},
            "questions": [{key: value for key, value in question.items()
                           if key in ("id", "kind", "text", "points", "options")}
                          for question in assigned_questions(exam, attempt)] if running else []}


def admin_attempt(exam, attempt):
    questions = assigned_questions(exam, attempt)
    grades = json.loads(attempt["grades"])
    return {key: attempt[key] for key in ("id", "code", "name", "phone", "joined_at", "last_seen",
                                        "deadline", "submitted_at", "submit_reason", "revision")} | {
        "answers": json.loads(attempt["answers"]), "grades": grades,
        "questionIds": json.loads(attempt["question_ids"]),
        "score": sum(grade["score"] for grade in grades.values()),
        "maximum": sum(question["points"] for question in questions),
        "pending": sum(question["id"] not in grades for question in questions)
                   if attempt["submitted_at"] else None}


class ExamRoom:
    def __init__(self, database):
        self.database = database

    def create_exam(self, payload):
        config = validate_exam(payload)
        exam_id = uuid.uuid4().hex
        with self.database.transaction() as connection:
            connection.execute("INSERT INTO exams(id,config,created_at) VALUES(?,?,?)",
                               (exam_id, encode(config), time.time()))
            audit(connection, "exam-created", exam_id)
        return {"id": exam_id}

    def update_exam(self, exam_id, payload):
        config = validate_exam(payload)
        with self.database.transaction() as connection:
            exam = exam_record(connection, exam_id)
            if exam["state"] != "draft":
                raise RoomError("الأسئلة مقفولة بعد فتح قاعة الانتظار. أنشئ نسخة جديدة للتعديل", 409)
            connection.execute("UPDATE exams SET config=? WHERE id=?", (encode(config), exam_id))
            audit(connection, "exam-updated", exam_id)
        return {"id": exam_id}

    def duplicate_exam(self, exam_id):
        with self.database.connection() as connection:
            config = exam_record(connection, exam_id)["config"]
        config["title"] = config["title"][:135] + " · نسخة جديدة"
        return self.create_exam(config)

    def list_exams(self):
        with self.database.connection() as connection:
            rows = connection.execute("""SELECT exams.*, (SELECT COUNT(*) FROM attempts WHERE exam_id=exams.id AND joined_at IS NOT NULL) AS student_count,
                (SELECT COUNT(*) FROM attempts WHERE exam_id=exams.id AND submitted_at IS NOT NULL) AS submitted_count
                FROM exams ORDER BY created_at DESC""").fetchall()
            return [{"id": row["id"], "state": row["state"], "createdAt": row["created_at"],
                     "startedAt": row["started_at"], "students": row["student_count"], "submitted": row["submitted_count"], "title": json.loads(row["config"])["title"]}
                    for row in rows]

    def dashboard(self, exam_id):
        with self.database.connection() as connection:
            exam = exam_record(connection, exam_id)
            attempts = connection.execute("SELECT * FROM attempts WHERE exam_id=? AND joined_at IS NOT NULL ORDER BY joined_at, rowid",
                                          (exam_id,)).fetchall()
            return {"exam": exam, "attempts": [admin_attempt(exam, attempt) for attempt in attempts],
                    "serverTime": time.time()}

    def change_state(self, exam_id, action):
        with self.database.transaction() as connection:
            exam = exam_record(connection, exam_id)
            states = {"publish": ("draft", "waiting"), "start": ("waiting", "running")}
            if action in states:
                expected, target = states[action]
                if exam["state"] != expected:
                    raise RoomError("حالة الامتحان تغيرت. حدّث الصفحة", 409)
                self._advance_exam(connection, exam, target)
            elif action == "close":
                if exam["state"] not in ("waiting", "running"):
                    raise RoomError("الامتحان غير مفتوح", 409)
                self._close_exam(connection, exam)
            else:
                raise RoomError("إجراء غير معروف", 404)
            audit(connection, "exam:" + action, exam_id)
        return {"id": exam_id}

    def _advance_exam(self, connection, exam, target):
        try:
            connection.execute("UPDATE exams SET state=? WHERE id=?", (target, exam["id"]))
        except sqlite3.IntegrityError as error:
            raise RoomError("يوجد امتحان آخر مفتوح. أنهِه أولًا", 409) from error
        if target == "running":
            now = time.time()
            connection.execute("UPDATE exams SET started_at=? WHERE id=?", (now, exam["id"]))
            connection.execute("UPDATE attempts SET deadline=? WHERE exam_id=? AND joined_at IS NOT NULL",
                               (now + exam["config"]["minutes"] * 60, exam["id"]))

    def _close_exam(self, connection, exam):
        attempts = connection.execute("SELECT * FROM attempts WHERE exam_id=? AND joined_at IS NOT NULL AND submitted_at IS NULL",
                                      (exam["id"],)).fetchall()
        for attempt in attempts:
            finalize_attempt(connection, attempt, exam, "closed")
        connection.execute("UPDATE exams SET state='closed', ended_at=? WHERE id=?", (time.time(), exam["id"]))

    def expire(self):
        with self.database.transaction() as connection:
            rows = connection.execute("SELECT * FROM exams WHERE state='running'").fetchall()
            for row in rows:
                exam = dict(row) | {"config": json.loads(row["config"])}
                if exam["config"]["timerMode"] == "shared" and time.time() >= exam["started_at"] + exam["config"]["minutes"] * 60:
                    self._close_expired_exam(connection, exam)
                else:
                    expired = connection.execute("SELECT * FROM attempts WHERE exam_id=? AND submitted_at IS NULL AND deadline<=?",
                                                 (exam["id"], time.time())).fetchall()
                    for attempt in expired:
                        finalize_attempt(connection, attempt, exam, "timeout")

    def _close_expired_exam(self, connection, exam):
        attempts = connection.execute("SELECT * FROM attempts WHERE exam_id=? AND joined_at IS NOT NULL AND submitted_at IS NULL",
                                      (exam["id"],)).fetchall()
        for attempt in attempts:
            finalize_attempt(connection, attempt, exam, "timeout")
        connection.execute("UPDATE exams SET state='closed', ended_at=? WHERE id=?", (time.time(), exam["id"]))
        audit(connection, "exam:timeout", exam["id"])

    def lounge(self):
        with self.database.connection() as connection:
            row = connection.execute("SELECT id FROM exams WHERE state IN ('waiting','running')").fetchone()
            if row is None:
                return {"exam": None, "serverTime": time.time()}
            exam = exam_record(connection, row["id"])
            return {"exam": {"title": exam["config"]["title"], "state": exam["state"],
                             "allowLate": exam["config"]["allowLate"]}, "serverTime": time.time()}

    def join(self, payload, current_token):
        code = normalize_code(payload.get("code"))
        name = text_field(payload, "name", 100, 2)
        phone = normalize_phone(payload.get("phone"))
        with self.database.transaction() as connection:
            active = connection.execute("SELECT id FROM exams WHERE state IN ('waiting','running')").fetchone()
            if active is None:
                raise RoomError("لا يوجد امتحان مفتوح للدخول الآن", 409)
            exam = exam_record(connection, active["id"])
            row = connection.execute("SELECT * FROM attempts WHERE exam_id=? AND code=?",
                                     (exam["id"], code)).fetchone()
            if row is None:
                count = connection.execute("SELECT COUNT(*) FROM attempts WHERE exam_id=? AND joined_at IS NOT NULL",
                                           (exam["id"],)).fetchone()[0]
                if count >= 1000:
                    raise RoomError("اكتمل عدد الطلاب لهذه الجلسة", 409)
                attempt_id = uuid.uuid4().hex
                connection.execute("INSERT INTO attempts(id,exam_id,code) VALUES(?,?,?)", (attempt_id, exam["id"], code))
                attempt = attempt_record(connection, attempt_id)
            else:
                attempt = dict(row)
            if current_token:
                claimed = connection.execute("SELECT id FROM attempts WHERE exam_id=? AND token_hash=?",
                                             (exam["id"], self._token_hash(current_token))).fetchone()
                if claimed and claimed["id"] != attempt["id"]:
                    raise RoomError("هذا المتصفح مسجل بكود آخر في الامتحان. راجع المشرف", 409)
            if attempt["token_hash"]:
                if current_token and secrets.compare_digest(attempt["token_hash"], self._token_hash(current_token)):
                    return {"session": student_view(exam, expire_attempt(connection, attempt, exam))}, current_token
                raise RoomError("الكود مستخدم بالفعل. افتح نفس المتصفح، أو اطلب استعادة الدخول من المشرف", 409)
            self._check_admission(exam, attempt)
            token = secrets.token_urlsafe(32)
            self._admit_attempt(connection, exam, attempt, {"name": name, "phone": phone, "token": token})
            audit(connection, "student-joined", attempt["id"])
            return {"session": student_view(exam, attempt_record(connection, attempt["id"]))}, token

    def _check_admission(self, exam, attempt):
        if exam["state"] not in ("waiting", "running"):
            raise RoomError("الامتحان غير مفتوح للدخول", 409)
        if attempt["submitted_at"] is not None:
            raise RoomError("تم تسليم هذه المحاولة", 409)
        if exam["state"] == "running":
            if not exam["config"]["allowLate"] and attempt["joined_at"] is None:
                raise RoomError("بدأ الامتحان والدخول المتأخر غير متاح", 409)
            if exam["config"]["timerMode"] == "shared" and time.time() >= exam["started_at"] + exam["config"]["minutes"] * 60:
                raise RoomError("انتهى وقت الامتحان", 409)

    def _admit_attempt(self, connection, exam, attempt, registration):
        if attempt["joined_at"] is not None:
            if registration["phone"] != attempt["phone"]:
                raise RoomError("للاستعادة، استخدم نفس رقم الموبايل المسجل")
            connection.execute("UPDATE attempts SET token_hash=?,last_seen=? WHERE id=?",
                               (self._token_hash(registration["token"]), time.time(), attempt["id"]))
            return
        questions = list(exam["config"]["questions"])
        if exam["config"]["shuffle"] or exam["config"]["questionCount"] < len(questions):
            secrets.SystemRandom().shuffle(questions)
        selected_ids = [question["id"] for question in questions[:exam["config"]["questionCount"]]]
        now = time.time()
        deadline = None
        if exam["state"] == "running":
            start = exam["started_at"] if exam["config"]["timerMode"] == "shared" else now
            deadline = start + exam["config"]["minutes"] * 60
        connection.execute("""UPDATE attempts SET name=?,phone=?,token_hash=?,joined_at=?,last_seen=?,
                              deadline=?,question_ids=? WHERE id=?""",
                           (registration["name"], registration["phone"], self._token_hash(registration["token"]),
                            now, now, deadline, encode(selected_ids), attempt["id"]))

    @staticmethod
    def _token_hash(token):
        return hashlib.sha256(token.encode()).hexdigest()

    def _authenticated_attempt(self, connection, token):
        if not token:
            raise RoomError("سجل الدخول أولًا", 401)
        attempt = connection.execute("SELECT * FROM attempts WHERE token_hash=?", (self._token_hash(token),)).fetchone()
        if attempt is None:
            raise RoomError("انتهت جلسة الدخول. استخدم الكود أو راجع المشرف", 401)
        exam = exam_record(connection, attempt["exam_id"])
        return expire_attempt(connection, dict(attempt), exam), exam

    def session(self, token):
        if not token:
            return {"session": None}
        with self.database.transaction() as connection:
            attempt, exam = self._authenticated_attempt(connection, token)
            if time.time() - (attempt["last_seen"] or 0) >= 5:
                connection.execute("UPDATE attempts SET last_seen=? WHERE id=?", (time.time(), attempt["id"]))
            return {"session": student_view(exam, attempt)}

    def save_answers(self, token, payload):
        revision = integer_field(payload, "revision", 1, 2147483647)
        submit = boolean_field(payload, "submit")
        with self.database.transaction() as connection:
            attempt, exam = self._authenticated_attempt(connection, token)
            if attempt["submitted_at"] is not None:
                return {"session": student_view(exam, attempt)}
            if exam["state"] != "running":
                raise RoomError("الامتحان لم يبدأ", 409)
            answers = validate_answers(payload.get("answers"), assigned_questions(exam, attempt))
            if revision == attempt["revision"] and answers == json.loads(attempt["answers"]):
                pass  # The acknowledgement may have been lost; the same write is idempotent.
            elif revision != attempt["revision"] + 1:
                raise RoomError("الإجابات تغيرت في نافذة أخرى. أعد تحميل الصفحة قبل المتابعة", 409)
            else:
                connection.execute("UPDATE attempts SET answers=?,revision=?,last_seen=? WHERE id=?",
                                   (encode(answers), revision, time.time(), attempt["id"]))
                attempt = attempt_record(connection, attempt["id"])
            if submit:
                finalize_attempt(connection, attempt, exam, "student")
                attempt = attempt_record(connection, attempt["id"])
            return {"session": student_view(exam, attempt)}

    def reset_login(self, attempt_id):
        with self.database.transaction() as connection:
            attempt = attempt_record(connection, attempt_id)
            if attempt["submitted_at"] is not None:
                raise RoomError("المحاولة مسلّمة ولا يمكن إعادة فتحها", 409)
            connection.execute("UPDATE attempts SET token_hash=NULL WHERE id=?", (attempt_id,))
            audit(connection, "login-reset", attempt_id)
        return {"id": attempt_id}

    def grade_essay(self, attempt_id, payload):
        with self.database.transaction() as connection:
            attempt = attempt_record(connection, attempt_id)
            exam = exam_record(connection, attempt["exam_id"])
            if attempt["submitted_at"] is None:
                raise RoomError("انتظر تسليم الطالب", 409)
            question = next((question for question in assigned_questions(exam, attempt)
                             if question["id"] == payload.get("questionId") and question["kind"] == "essay"), None)
            if question is None:
                raise RoomError("السؤال المقالي غير موجود", 404)
            grades = json.loads(attempt["grades"])
            grades[question["id"]] = validate_grade(payload, question["points"])
            connection.execute("UPDATE attempts SET grades=? WHERE id=?", (encode(grades), attempt_id))
            audit(connection, "essay-graded", attempt_id)
        return {"id": attempt_id}

    def report(self, attempt_id):
        with self.database.connection() as connection:
            attempt = attempt_record(connection, attempt_id)
            exam = exam_record(connection, attempt["exam_id"])
            summary = admin_attempt(exam, attempt)
            if not attempt["submitted_at"] or summary["pending"]:
                raise RoomError("أكمل تصحيح المحاولة قبل تصدير التقرير", 409)
            return {"exam": exam, "attempt": summary, "questions": assigned_questions(exam, attempt)}
