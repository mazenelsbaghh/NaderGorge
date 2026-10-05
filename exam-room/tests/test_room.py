import tempfile
import sqlite3
import threading
import unittest
from concurrent.futures import ThreadPoolExecutor
from http.client import HTTPConnection
from pathlib import Path
from unittest.mock import patch

from room.database import Database, SCHEMA
from room.demo import DEMO_EXAM
from room.exams import ExamRoom
from room.server import RoomServer
from room.validation import RoomError


class RoomTests(unittest.TestCase):
    def setUp(self):
        self.folder = tempfile.TemporaryDirectory()
        self.addCleanup(self.folder.cleanup)
        self.db = Database(Path(self.folder.name) / 'room.sqlite3')
        self.room = ExamRoom(self.db)
        self.exam = self.room.create_exam(DEMO_EXAM)['id']
        self.codes = ['STU001', 'STU002', 'STU003', 'STU004']
        self.room.change_state(self.exam, 'publish')

    def join(self, index=0):
        return self.room.join({'name': 'طالب تجربة', 'phone': '01012345678', 'code': self.codes[index]}, '')

    def start_student(self):
        _, token = self.join()
        self.room.change_state(self.exam, 'start')
        return token, self.room.session(token)['session']

    def correct_answers(self):
        questions = self.room.dashboard(self.exam)['exam']['config']['questions']
        return {q['id']: q['correct'] if q['kind'] == 'mcq' else 'تنظيم الوقت وتقليل التوتر' for q in questions}

    def test_waiting_and_student_privacy(self):
        response, token = self.join()
        self.assertEqual(response['session']['questions'], [])
        with self.assertRaises(RoomError):
            self.room.save_answers(token, {'revision': 1, 'answers': {}, 'submit': False})
        self.room.change_state(self.exam, 'start')
        session = self.room.session(token)['session']
        self.assertEqual(len(session['questions']), 4)
        for question in session['questions']:
            self.assertNotIn('correct', question)
            self.assertNotIn('modelAnswer', question)
        self.assertNotIn('grades', session)
        self.assertNotIn('score', session)

    def test_save_retry_submission_grade_and_report(self):
        token, student = self.start_student()
        answers = self.correct_answers()
        payload = {'revision': 1, 'answers': answers, 'submit': False}
        self.room.save_answers(token, payload)
        self.assertEqual(self.room.save_answers(token, payload)['session']['revision'], 1)
        with self.assertRaises(RoomError):
            self.room.save_answers(token, payload | {'answers': {}})
        self.room.save_answers(token, payload | {'submit': True})
        receipt = self.room.session(token)['session']
        self.assertEqual(receipt['state'], 'submitted')
        self.assertEqual(receipt['answers'], {})
        self.assertEqual(receipt['questions'], [])
        with self.assertRaises(RoomError):
            self.room.report(student['id'])
        attempt = self.room.dashboard(self.exam)['attempts'][0]
        self.assertEqual((attempt['score'], attempt['pending']), (6, 1))
        essay_id = next(q['id'] for q in student['questions'] if q['kind'] == 'essay')
        self.room.grade_essay(student['id'], {'questionId': essay_id, 'score': 3.5, 'feedback': 'جيد'})
        self.assertEqual(self.room.report(student['id'])['attempt']['score'], 9.5)
        self.room.save_answers(token, {'revision': 2, 'answers': {}, 'submit': True})
        self.assertEqual(self.room.report(student['id'])['attempt']['answers'], answers)

    def test_shared_late_deadline_and_expiry(self):
        with patch('room.exams.time.time', return_value=1000):
            _, token = self.join()
            self.room.change_state(self.exam, 'start')
        with patch('room.exams.time.time', return_value=1100):
            response, _ = self.join(1)
            self.assertEqual(response['session']['deadline'], 1900)
        with patch('room.exams.time.time', return_value=1901):
            result = self.room.save_answers(token, {'revision': 1, 'answers': self.correct_answers(), 'submit': True})
            self.assertEqual(result['session']['state'], 'submitted')
            self.assertEqual(self.room.dashboard(self.exam)['attempts'][0]['score'], 0)
            self.room.expire()
            self.assertEqual(self.room.dashboard(self.exam)['exam']['state'], 'closed')

    def test_individual_timer_recovery_and_late_block(self):
        self.room.change_state(self.exam, 'close')
        self.exam = self.room.create_exam(DEMO_EXAM | {'timerMode': 'individual'})['id']
        self.codes = ['STU001']
        self.room.change_state(self.exam, 'publish')
        with patch('room.exams.time.time', return_value=1000):
            self.room.change_state(self.exam, 'start')
        with patch('room.exams.time.time', return_value=1100):
            response, token = self.join()
            self.assertEqual(response['session']['deadline'], 2000)
            attempt_id = response['session']['id']
            self.room.save_answers(token, {'revision': 1, 'answers': self.correct_answers(), 'submit': False})
        self.room.reset_login(attempt_id)
        with patch('room.exams.time.time', return_value=1200):
            response, replacement = self.join()
            self.assertEqual(response['session']['deadline'], 2000)
            self.assertEqual(response['session']['revision'], 1)
            with self.assertRaises(RoomError):
                self.room.session(token)
            self.assertNotEqual(token, replacement)
        with patch('room.exams.time.time', return_value=2001):
            self.room.expire()
            self.assertEqual(self.room.session(replacement)['session']['state'], 'submitted')
            self.assertEqual(self.room.dashboard(self.exam)['exam']['state'], 'running')

    def test_restart_backup_and_close_preserve_answers(self):
        token, student = self.start_student()
        answers = self.correct_answers()
        self.room.save_answers(token, {'revision': 1, 'answers': answers, 'submit': False})
        restarted = ExamRoom(Database(self.db.path))
        self.assertEqual(restarted.session(token)['session']['answers'], answers)
        restored = ExamRoom(Database(self.db.backup()))
        self.assertEqual(restored.session(token)['session']['answers'], answers)
        restarted.change_state(self.exam, 'close')
        self.assertEqual(restarted.session(token)['session']['state'], 'submitted')
        self.assertEqual(restarted.dashboard(self.exam)['attempts'][0]['answers'], answers)

    def test_only_one_device_can_claim_code_concurrently(self):
        def claim(_):
            try:
                return self.join()[0]['session']['id']
            except RoomError as error:
                return error.status
        with ThreadPoolExecutor(max_workers=10) as pool:
            outcomes = list(pool.map(claim, range(10)))
        self.assertEqual(outcomes.count(409), 9)

    def test_invalid_answers_do_not_change_saved_attempt(self):
        token, student = self.start_student()
        mcq = next(q for q in student['questions'] if q['kind'] == 'mcq')
        for answers in ({'unknown': 1}, {mcq['id']: True}, {mcq['id']: 100}):
            with self.assertRaises(RoomError):
                self.room.save_answers(token, {'revision': 1, 'answers': answers, 'submit': False})
        self.assertEqual(self.room.session(token)['session']['revision'], 0)

    def test_single_live_exam_and_locked_questions(self):
        with self.assertRaises(RoomError):
            self.room.update_exam(self.exam, DEMO_EXAM)
        second = self.room.create_exam(DEMO_EXAM)['id']
        with self.assertRaises(RoomError):
            self.room.change_state(second, 'publish')
        self.assertEqual(self.room.dashboard(second)['exam']['state'], 'draft')

    def test_late_entry_disabled(self):
        self.room.change_state(self.exam, 'close')
        other = self.room.create_exam(DEMO_EXAM | {'allowLate': False})['id']
        code = 'STU001'
        self.room.change_state(other, 'publish')
        self.room.change_state(other, 'start')
        with self.assertRaises(RoomError):
            self.room.join({'name': 'طالب جديد', 'phone': '01012345678', 'code': code}, '')

    def test_student_supplied_code_reused_across_exams(self):
        self.codes[0] = '٠٠١٢٣'
        first, token = self.join()
        self.assertEqual(self.room.dashboard(self.exam)['attempts'][0]['code'], '00123')
        with self.assertRaises(RoomError):
            self.room.join({'name': 'اسم مختلف', 'phone': '01099999999', 'code': '00123'}, '')
        with self.assertRaises(RoomError):
            self.room.join({'name': 'اسم مختلف', 'phone': '01099999999', 'code': 'OTHER'}, token)
        self.assertEqual(len(self.room.dashboard(self.exam)['attempts']), 1)
        self.room.change_state(self.exam, 'close')
        self.exam = self.room.create_exam(DEMO_EXAM)['id']
        self.room.change_state(self.exam, 'publish')
        second, _ = self.join()
        self.assertNotEqual(first['session']['id'], second['session']['id'])

    def test_legacy_migration_preserves_sessions_answers_and_backup(self):
        token, student = self.start_student()
        answers = self.correct_answers()
        self.room.save_answers(token, {'revision': 1, 'answers': answers, 'submit': False})
        legacy_path = Path(self.folder.name) / 'legacy.sqlite3'
        legacy_schema = SCHEMA.replace('code TEXT NOT NULL,', 'code TEXT NOT NULL UNIQUE,').replace(",\n    UNIQUE(exam_id, code)", '').replace('user_version=2', 'user_version=1')
        with sqlite3.connect(legacy_path) as connection:
            connection.executescript(legacy_schema)
            connection.execute('ATTACH DATABASE ? AS source', (str(self.db.path),))
            for table in ('exams', 'attempts', 'audit'):
                connection.execute(f'INSERT INTO {table} SELECT * FROM source.{table}')
        migrated = ExamRoom(Database(legacy_path))
        restored = migrated.session(token)['session']
        self.assertEqual((restored['id'], restored['answers'], restored['revision']), (student['id'], answers, 1))
        backups = list((legacy_path.parent / 'backups').glob('*.sqlite3'))
        self.assertEqual(len(backups), 1)
        with sqlite3.connect(backups[0]) as connection:
            self.assertEqual(connection.execute('PRAGMA user_version').fetchone()[0], 1)
        migrated.change_state(self.exam, 'close')
        second = migrated.create_exam(DEMO_EXAM)['id']
        migrated.change_state(second, 'publish')
        migrated.join({'name': 'طالب تجربة', 'phone': '01012345678', 'code': self.codes[0]}, '')
        self.assertEqual(len(migrated.dashboard(second)['attempts']), 1)

    def test_http_boundaries(self):
        student = RoomServer(('127.0.0.1', 0), self.room, 'student')
        admin = RoomServer(('127.0.0.1', 0), self.room, 'admin')
        admin.student_port = student.server_port
        for server in (student, admin):
            thread = threading.Thread(target=server.serve_forever, daemon=True)
            thread.start()
            self.addCleanup(server.server_close)
            self.addCleanup(server.shutdown)
        def request(server, path, method='GET', headers=None, body=None):
            client = HTTPConnection('127.0.0.1', server.server_port)
            try:
                client.request(method, path, body, headers or {})
                response = client.getresponse()
                return response.status, response.read()
            finally:
                client.close()
        self.assertEqual(request(student, '/api/exams')[0], 404)
        self.assertEqual(request(student, '/assets/admin.js')[0], 404)
        self.assertEqual(request(admin, '/api/exams')[0], 401)
        self.assertEqual(request(student, '/api/lounge', headers={'Host': 'attacker.test:1234'})[0], 403)
        self.assertEqual(request(student, '/api/lounge', headers={'Host': '127.0.0.1:bad'})[0], 400)
        self.assertEqual(request(student, '/api/join', 'POST', {'Content-Type': 'application/json'}, '{}')[0], 403)
        self.assertEqual(request(student, '/api/join', 'POST', {'Content-Type': 'application/json', 'X-Exam-Request': '1', 'Origin': 'https://attacker.test'}, '{}')[0], 403)
        self.assertEqual(request(admin, '/api/bootstrap')[0], 200)
        _, token = self.join()
        headers = {'Content-Type': 'application/json', 'X-Exam-Request': '1', 'Cookie': 'massar_student=' + token}
        self.assertEqual(request(student, '/api/leave', 'POST', headers, '{}')[0], 409)
        self.room.change_state(self.exam, 'close')
        self.assertEqual(request(student, '/api/leave', 'POST', headers, '{}')[0], 200)


if __name__ == '__main__':
    unittest.main()
