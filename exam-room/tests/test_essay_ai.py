import io
import json
from pathlib import Path
import tempfile
import threading
import unittest
from unittest.mock import patch

from room.demo import DEMO_EXAM
from room.essay_ai import grade_with_gemini
from room.key_vault import KeyVault
from room.runtime import Runtime
from room.validation import RoomError


class EssayAiTests(unittest.TestCase):
    def test_credential_store_is_read_only_when_grading_is_requested(self):
        class Store:
            reads = 0
            def get_password(self, *_):
                self.reads += 1
                return 'saved-test-key'
        store = Store()
        with patch('room.key_vault.keyring', store):
            vault = KeyVault(self.runtime.room.database.path.parent)
            self.assertEqual(store.reads, 0)
            self.assertEqual(vault.load(), 'saved-test-key')
            self.assertEqual(vault.load(), 'saved-test-key')
            self.assertEqual(store.reads, 1)

    def test_env_key_survives_restart_without_entering_sqlite(self):
        key = 'test-local-gemini-key-123'
        first = self.runtime.grader.vault
        first.save(key)
        self.assertEqual(first.path.read_text(), 'GEMINI_API_KEY=' + key + '\n')
        self.assertEqual(first.path.stat().st_mode & 0o777, 0o600)
        self.assertEqual(KeyVault(first.path.parent).load(), key)
        self.assertNotIn(key.encode(), self.runtime.room.database.path.read_bytes())

    def setUp(self):
        folder = tempfile.TemporaryDirectory()
        self.addCleanup(folder.cleanup)
        self.runtime = Runtime(Path(folder.name), 0, 0)
        self.addCleanup(self.runtime.close)
        self.room = self.runtime.room
        self.exam = self.room.create_exam(DEMO_EXAM)['id']
        self.room.change_state(self.exam, 'publish')

    def submit(self, code):
        response, token = self.room.join({'name': 'طالب اختبار', 'code': code, 'phone': '01012345678'}, '')
        student = response['session']
        if self.room.dashboard(self.exam)['exam']['state'] == 'waiting':
            self.room.change_state(self.exam, 'start')
        questions = self.room.dashboard(self.exam)['exam']['config']['questions']
        essay = next(q for q in questions if q['kind'] == 'essay')
        self.room.save_answers(token, {'revision': 1, 'answers': {essay['id']: 'إجابة صحيحة بمعنى آخر'}, 'submit': True})
        return student['id'], essay['id']

    def wait_for_finish(self):
        self.runtime.grader.thread.join(timeout=4)
        self.assertFalse(self.runtime.grader.thread.is_alive())
        return self.runtime.grader.status(self.exam)['job']

    def test_batch_grades_only_ungraded_submitted_answers(self):
        first, essay_id = self.submit('001')
        second, _ = self.submit('002')
        self.room.grade_essay(first, {'questionId': essay_id, 'score': 2.5, 'feedback': 'تقدير المدرس'})
        self.room.change_state(self.exam, 'close')
        self.runtime.grader.grade = lambda *_: {'correct': True, 'needsReview': False, 'feedback': 'المعنى صحيح'}
        self.runtime.grader.delay = 0
        started = self.runtime.grader.start(self.exam, {'apiKey': 'test_key_123456789'})
        self.assertEqual(started['total'], 1)
        job = self.wait_for_finish()
        self.assertEqual((job['state'], job['graded'], job['processed']), ('completed', 1, 1))
        rows = {a['id']: a for a in self.room.dashboard(self.exam)['attempts']}
        self.assertEqual(rows[first]['grades'][essay_id]['score'], 2.5)
        self.assertEqual(rows[second]['grades'][essay_id]['score'], 4)
        self.assertEqual(rows[second]['grades'][essay_id]['source'], 'gemini')
        with self.assertRaises(RoomError):
            self.runtime.grader.start(self.exam, {'apiKey': 'test_key_123456789'})

    def test_manual_grade_during_gemini_request_wins(self):
        attempt, essay_id = self.submit('003')
        self.room.change_state(self.exam, 'close')
        entered, release = threading.Event(), threading.Event()
        def slow_verdict(*_):
            entered.set()
            release.wait(2)
            return {'correct': True, 'needsReview': False, 'feedback': 'صحيح'}
        self.runtime.grader.grade = slow_verdict
        self.runtime.grader.delay = 0
        self.runtime.grader.start(self.exam, {'apiKey': 'test_key_123456789'})
        self.assertTrue(entered.wait(2))
        self.room.grade_essay(attempt, {'questionId': essay_id, 'score': 1, 'feedback': 'مراجعة يدوية'})
        release.set()
        job = self.wait_for_finish()
        self.assertEqual((job['skipped'], job['graded']), (1, 0))
        grade = self.room.dashboard(self.exam)['attempts'][0]['grades'][essay_id]
        self.assertEqual((grade['score'], grade['feedback']), (1, 'مراجعة يدوية'))

    def test_uncertain_verdict_remains_for_teacher_and_can_resume(self):
        attempt, essay_id = self.submit('004')
        self.room.change_state(self.exam, 'close')
        self.runtime.grader.grade = lambda *_: {'correct': False, 'needsReview': True, 'feedback': 'الإجابة ملتبسة'}
        self.runtime.grader.delay = 0
        self.runtime.grader.start(self.exam, {'apiKey': 'test_key_123456789'})
        job = self.wait_for_finish()
        self.assertEqual((job['state'], job['needs_review']), ('partial', 1))
        self.assertEqual(self.room.dashboard(self.exam)['attempts'][0]['pending'], 1)
        self.runtime.grader.grade = lambda *_: {'correct': False, 'needsReview': False, 'feedback': 'ينقصها جزء أساسي'}
        self.runtime.grader.start(self.exam, {'apiKey': 'test_key_123456789'})
        self.assertEqual(self.wait_for_finish()['graded'], 1)
        self.assertEqual(self.room.dashboard(self.exam)['attempts'][0]['grades'][essay_id]['score'], 0)

    def test_gemini_request_sends_only_three_educational_fields_and_validates_result(self):
        captured = []
        response = {'candidates': [{'content': {'parts': [{'text': json.dumps({
            'correct': True, 'needsReview': False, 'feedback': 'إجابة سليمة'}, ensure_ascii=False)}]}}]}
        def send(call, timeout):
            captured.append((call, timeout))
            return io.BytesIO(json.dumps(response).encode())
        with patch('room.essay_ai.request.urlopen', side_effect=send):
            verdict = grade_with_gemini('test_key_123456789', 'سؤال', 'نموذج', 'إجابة')
        self.assertTrue(verdict['correct'])
        call, timeout = captured[0]
        self.assertIn('test_key_123456789', call.headers.values())
        self.assertEqual(timeout, 35)
        body = json.loads(call.data)
        self.assertEqual(body['generationConfig']['responseMimeType'], 'application/json')
        self.assertNotIn('01012345678', str(body))
        self.assertNotIn('طالب اختبار', str(body))
        response['candidates'][0]['content']['parts'][0]['text'] = '{"correct":"yes","needsReview":false,"feedback":"bad"}'
        with patch('room.essay_ai.request.urlopen', side_effect=send), self.assertRaises(ValueError):
            grade_with_gemini('test_key_123456789', 'سؤال', 'نموذج', 'إجابة')

    def test_batch_rejects_open_exam_and_parallel_run(self):
        self.submit('005')
        with self.assertRaises(RoomError):
            self.runtime.grader.start(self.exam, {'apiKey': 'test_key_123456789'})
        self.room.change_state(self.exam, 'close')
        entered, release = threading.Event(), threading.Event()
        def slow_verdict(*_):
            entered.set(); release.wait(2)
            return {'correct': True, 'needsReview': False, 'feedback': 'صح'}
        self.runtime.grader.grade = slow_verdict
        self.runtime.grader.start(self.exam, {'apiKey': 'test_key_123456789'})
        self.assertTrue(entered.wait(2))
        with self.assertRaises(RoomError):
            self.runtime.grader.start(self.exam, {'apiKey': 'test_key_123456789'})
        self.runtime.grader.cancel_current(self.exam)
        release.set()
        self.assertEqual(self.wait_for_finish()['state'], 'interrupted')
        self.assertEqual(self.room.dashboard(self.exam)['attempts'][0]['pending'], 1)


if __name__ == '__main__':
    unittest.main()
