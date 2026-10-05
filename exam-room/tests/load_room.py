"""Local HTTP burst test. This does not test a Wi-Fi router or physical phones."""
import json
import tempfile
import threading
import time
from concurrent.futures import ThreadPoolExecutor
from http.client import HTTPConnection
from pathlib import Path
from room.database import Database
from room.demo import DEMO_EXAM
from room.exams import ExamRoom
from room.server import RoomServer


def run():
    with tempfile.TemporaryDirectory() as folder:
        room = ExamRoom(Database(Path(folder) / 'room.sqlite3'))
        exam_id = room.create_exam(DEMO_EXAM)['id']
        codes = [f'STU{index:04}' for index in range(100)]
        room.change_state(exam_id, 'publish')
        server = RoomServer(('127.0.0.1', 0), room, 'student')
        threading.Thread(target=server.serve_forever, daemon=True).start()
        def request(path, payload=None, cookie=''):
            client = HTTPConnection('127.0.0.1', server.server_port, timeout=30)
            headers = {'Content-Type': 'application/json', 'X-Exam-Request': '1', 'Cookie': cookie}
            try:
                client.request('POST' if payload is not None else 'GET', path,
                               json.dumps(payload) if payload is not None else None, headers)
                response = client.getresponse()
                body = json.loads(response.read())
                if response.status != 200:
                    raise AssertionError((response.status, body))
                return body, response.getheader('Set-Cookie', '').split(';')[0]
            finally:
                client.close()
        try:
            with ThreadPoolExecutor(max_workers=100) as pool:
                started = time.monotonic()
                joined = list(pool.map(lambda code: request('/api/join', {'name': 'Load test', 'phone': '01012345678', 'code': code}), codes))
                join_seconds = time.monotonic() - started
                room.change_state(exam_id, 'start')
                cookies = [response[1] for response in joined]
                sessions = list(pool.map(lambda cookie: request('/api/session', cookie=cookie)[0]['session'], cookies))
                assert all(len(session['questions']) == 4 for session in sessions)
                questions = room.dashboard(exam_id)['exam']['config']['questions']
                answers = {q['id']: q['correct'] if q['kind'] == 'mcq' else 'Load test answer' for q in questions}
                started = time.monotonic()
                saved = list(pool.map(lambda cookie: request('/api/answers', {'revision': 1, 'submit': False, 'answers': answers}, cookie), cookies))
                save_seconds = time.monotonic() - started
                started = time.monotonic()
                submitted = list(pool.map(lambda cookie: request('/api/answers', {'revision': 2, 'submit': True, 'answers': answers}, cookie), cookies))
                submit_seconds = time.monotonic() - started
            attempts = room.dashboard(exam_id)['attempts']
            assert len(attempts) == 100
            assert all(a['submitted_at'] and a['answers'] == answers and a['score'] == 6 and a['pending'] == 1 for a in attempts)
            assert all(response[0]['session']['state'] == 'submitted' for response in submitted)
            print(json.dumps({'clients': 100, 'failed': 0, 'join_burst_seconds': round(join_seconds, 3), 'save_burst_seconds': round(save_seconds, 3), 'submit_burst_seconds': round(submit_seconds, 3), 'scope': 'loopback HTTP only; Wi-Fi capacity not tested'}))
        finally:
            server.shutdown()
            server.server_close()


if __name__ == '__main__':
    run()
