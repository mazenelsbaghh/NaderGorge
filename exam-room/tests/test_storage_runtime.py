import json
from http.client import HTTPConnection
from pathlib import Path
import sqlite3
import tempfile
import unittest

from room.demo import DEMO_EXAM
from room.runtime import Runtime
from room.storage import backup_path, check_database, storage_details
from room.validation import RoomError


class StorageRuntimeTests(unittest.TestCase):
    def setUp(self):
        folder = tempfile.TemporaryDirectory()
        self.addCleanup(folder.cleanup)
        self.path = Path(folder.name).resolve()
        self.runtime = Runtime(self.path, 0, 0)
        self.addCleanup(lambda: self.runtime.close())

    def request(self, role, path, method='GET', authorized=False, payload=None):
        server = self.runtime.servers[role]
        headers = {'Content-Type': 'application/json', 'X-Exam-Request': '1'}
        if authorized:
            headers.update({'Cookie': 'massar_admin=' + server.admin_token,
                            'X-Admin-Token': server.admin_token})
        client = HTTPConnection('127.0.0.1', server.server_port, timeout=10)
        try:
            client.request(method, path, json.dumps(payload or {}) if method == 'POST' else None, headers)
            response = client.getresponse()
            return response.status, response.read()
        finally:
            client.close()

    def test_storage_backup_download_preserves_student_answers_and_grades(self):
        room = self.runtime.room
        exam = room.create_exam(DEMO_EXAM)['id']
        room.change_state(exam, 'publish')
        _, token = room.join({'name': 'اختبار حفظ', 'code': '001', 'phone': '01012345678'}, '')
        room.change_state(exam, 'start')
        questions = room.dashboard(exam)['exam']['config']['questions']
        answers = {q['id']: q['correct'] if q['kind'] == 'mcq' else 'إجابة مقالية محفوظة' for q in questions}
        room.save_answers(token, {'revision': 1, 'answers': answers, 'submit': True})
        details = storage_details(room.database)
        self.assertEqual(details['databasePath'], str(self.path / 'exams.sqlite3'))
        self.assertEqual(details['counts'], {'exams': 1, 'students': 1, 'submissions': 1})
        self.assertTrue(check_database(room.database)['healthy'])
        self.assertEqual((room.list_exams()[0]['students'], room.list_exams()[0]['submitted']), (1, 1))
        status, body = self.request(1, '/api/storage/backup', 'POST', True)
        self.assertEqual(status, 200)
        filename = json.loads(body)['name']
        status, downloaded = self.request(1, '/api/backups/' + filename, authorized=True)
        self.assertEqual(status, 200)
        recovered = self.path / 'downloaded.sqlite3'
        recovered.write_bytes(downloaded)
        with sqlite3.connect(recovered) as connection:
            saved, submitted, grades = connection.execute('SELECT answers, submitted_at, grades FROM attempts').fetchone()
            self.assertEqual(json.loads(saved), answers)
            self.assertIsNotNone(submitted)
            self.assertEqual(sum(grade['score'] for grade in json.loads(grades).values()), 6)
            self.assertEqual(connection.execute('PRAGMA quick_check').fetchone()[0], 'ok')

    def test_storage_and_backups_are_admin_only(self):
        for path, method in [('/api/storage', 'GET'), ('/api/storage/check', 'POST'),
                             ('/api/storage/backup', 'POST'), ('/api/storage/open-folder', 'POST'),
                             ('/api/backups/exam-room-1.sqlite3', 'GET'),
                             ('/api/exams/anything/essay-batch', 'GET'),
                             ('/api/exams/anything/essay-batch', 'POST'),
                             ('/api/exams/anything/essay-batch/stop', 'POST'),
                             ('/api/settings/gemini', 'GET'), ('/api/settings/gemini', 'POST')]:
            with self.subTest(path=path):
                self.assertEqual(self.request(0, path, method)[0], 404)
                self.assertEqual(self.request(1, path, method)[0], 401)
        self.assertEqual(self.request(0, '/assets/storage.js')[0], 404)
        status, body = self.request(1, '/api/storage', authorized=True)
        self.assertEqual(status, 200)
        self.assertEqual(json.loads(body)['databasePath'], str(self.path / 'exams.sqlite3'))

    def test_admin_saves_gemini_key_in_env_without_echoing_it(self):
        key = 'test-settings-key-123'
        status, body = self.request(1, '/api/settings/gemini', 'POST', True, {'apiKey': key})
        self.assertEqual(status, 200)
        self.assertNotIn(key.encode(), body)
        env_path = self.path / '.env'
        self.assertIn(key, env_path.read_text())
        status, body = self.request(1, '/api/settings/gemini', authorized=True)
        self.assertEqual(status, 200)
        self.assertTrue(json.loads(body)['configured'])
        self.assertNotIn(key.encode(), body)

    def test_backup_lookup_rejects_traversal_and_symlink(self):
        database = self.runtime.room.database
        for name in ['../exams.sqlite3', '%2e%2e%2fexams.sqlite3', 'exams.sqlite3', 'exam-room-0.sqlite3']:
            with self.subTest(name=name), self.assertRaises(RoomError):
                backup_path(database, name)
        link = self.path / 'backups' / 'exam-room-123.sqlite3'
        try:
            link.symlink_to(database.path)
        except OSError:
            self.skipTest('OS does not permit creating symlinks for this user')
        with self.assertRaises(RoomError):
            backup_path(database, link.name)
        self.assertNotIn(link.name, [item['name'] for item in storage_details(database)['backups']])

    def test_second_instance_cannot_write_same_data(self):
        self.runtime.room.create_exam(DEMO_EXAM)
        with self.assertRaisesRegex(RuntimeError, 'نسخة أخرى'):
            Runtime(self.path, 0, 0)
        self.assertEqual(len(self.runtime.room.list_exams()), 1)
        self.assertEqual(self.request(1, '/api/status', authorized=True)[0], 200)

    def test_shutdown_backup_and_restart_keep_exam(self):
        exam = self.runtime.room.create_exam(DEMO_EXAM)['id']
        self.runtime.close()
        self.runtime = Runtime(self.path, 0, 0)
        self.assertEqual(self.runtime.room.list_exams()[0]['id'], exam)
        self.assertGreaterEqual(storage_details(self.runtime.room.database)['backupCount'], 3)
