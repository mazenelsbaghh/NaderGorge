"""Concurrent HTTP clients against a temporary Go exam server."""
import argparse
from concurrent.futures import ThreadPoolExecutor
from http.client import HTTPConnection
from pathlib import Path
import json
import socket
import subprocess
import sys
import tempfile
import threading
import time

ROOT = Path(__file__).resolve().parents[1]


def free_port():
    with socket.socket() as s:
        s.bind(('127.0.0.1', 0))
        return s.getsockname()[1]


def run(clients=100, student_host='127.0.0.1', poll_rounds=0, chaos=False, workers=256):
    with tempfile.TemporaryDirectory() as folder:
        binary = Path(folder) / 'massar-exam-room'
        build_command = ['go', 'build']
        if clients > 1000:
            build_command += ['-tags=loadtest']
        subprocess.run(build_command + ['-o', str(binary), '.'], cwd=ROOT, check=True)
        student_port, admin_port = free_port(), free_port()
        proc = subprocess.Popen([str(binary), '--data-dir', folder, '--port', str(student_port),
                                 '--admin-port', str(admin_port)], stdout=subprocess.DEVNULL,
                                stderr=subprocess.DEVNULL)
        connections = threading.local()
        def request(port, path, payload=None, cookie='', token=''):
            key = 'student' if port == student_port else 'admin'
            client = getattr(connections, key, None)
            if client is None:
                client = HTTPConnection(student_host if port == student_port else '127.0.0.1', port, timeout=30)
                setattr(connections, key, client)
            headers = {'Content-Type': 'application/json', 'X-Exam-Request': '1', 'Cookie': cookie,
                       'X-Admin-Token': token}
            try:
                client.request('POST' if payload is not None else 'GET', path,
                               json.dumps(payload, ensure_ascii=False) if payload is not None else None,
                               headers)
                response = client.getresponse()
                data = json.loads(response.read())
                if response.status != 200:
                    raise AssertionError((response.status, data))
                return data, response.getheader('Set-Cookie', '').split(';')[0]
            except Exception:
                client.close()
                setattr(connections, key, None)
                raise
        try:
            for _ in range(100):
                if proc.poll() is not None:
                    raise RuntimeError('Go server exited')
                try:
                    bootstrap, admin_cookie = request(admin_port, '/api/bootstrap')
                    break
                except OSError:
                    time.sleep(.1)
            else:
                raise RuntimeError('Go server did not start')
            token = bootstrap['token']
            exam_id = request(admin_port, '/api/demo', {}, token=token)[0]['id']
            request(admin_port, f'/api/exams/{exam_id}/publish', {}, token=token)
            codes = [f'STU{index:04}' for index in range(clients)]
            with ThreadPoolExecutor(max_workers=min(clients, workers)) as pool:
                started = time.monotonic()
                joined = list(pool.map(lambda code: request(student_port, '/api/join',
                    {'name': 'Load test', 'phone': '01012345678', 'code': code}), codes))
                join_seconds = time.monotonic() - started
                if clients > 1000:
                    print(f'joined {clients} clients in {join_seconds:.1f}s', file=sys.stderr, flush=True)
                request(admin_port, f'/api/exams/{exam_id}/start', {}, token=token)
                cookies = [response[1] for response in joined]
                sessions = list(pool.map(lambda cookie: request(student_port, '/api/session', cookie=cookie)[0]['session'], cookies))
                assert all(len(session['questions']) == 4 for session in sessions)
                poll_seconds = []
                for round_index in range(poll_rounds):
                    if round_index:
                        time.sleep(3)
                    started = time.monotonic()
                    sessions = list(pool.map(lambda cookie: request(student_port, '/api/session', cookie=cookie)[0]['session'], cookies))
                    assert all(len(session['questions']) == 4 for session in sessions)
                    poll_seconds.append(time.monotonic() - started)
                    if clients > 1000:
                        print(f'poll {round_index + 1}/{poll_rounds}: {poll_seconds[-1]:.1f}s', file=sys.stderr, flush=True)
                questions = request(admin_port, f'/api/exams/{exam_id}', cookie=admin_cookie)[0]['exam']['config']['questions']
                def answers_for(index):
                    return {q['id']: q['correct'] if q['kind'] == 'mcq' else f'Load test answer {index}'
                            for q in questions}
                if chaos:
                    # Each worker has its own cookie and draft. These faults affect only this test client.
                    def unstable_client(item):
                        index, cookie = item
                        answers = answers_for(index)
                        save = {'revision': 1, 'submit': False, 'answers': answers}
                        submit = {'revision': 2, 'submit': True, 'answers': answers}
                        time.sleep(0.04 * (index % 8))
                        if index % 4 == 0:
                            # A disconnected device keeps its draft, then reconnects.
                            unavailable = HTTPConnection('127.0.0.1', offline_port, timeout=1)
                            try:
                                unavailable.request('GET', '/api/session')
                            except OSError:
                                pass
                            else:
                                raise AssertionError('offline simulation unexpectedly connected')
                            finally:
                                unavailable.close()
                            time.sleep(0.5)
                        if index % 4 == 1:
                            # The save reached the server but its acknowledgement was lost.
                            request(student_port, '/api/answers', save, cookie)
                        saved = request(student_port, '/api/answers', save, cookie)[0]['session']
                        assert saved['revision'] == 1 and saved['answers'] == answers
                        if index % 5 == 0:
                            # Retrying a submission after losing the reply must not create another attempt.
                            request(student_port, '/api/answers', submit, cookie)
                        submitted = request(student_port, '/api/answers', submit, cookie)[0]['session']
                        assert submitted['state'] == 'submitted'
                    offline_port = free_port()
                    started = time.monotonic()
                    list(pool.map(unstable_client, enumerate(cookies)))
                    chaos_seconds = time.monotonic() - started
                    if clients > 1000:
                        print(f'chaos save and submit: {chaos_seconds:.1f}s', file=sys.stderr, flush=True)
                    save_seconds = submit_seconds = None
                else:
                    answers = answers_for(0)
                    started = time.monotonic()
                    list(pool.map(lambda cookie: request(student_port, '/api/answers',
                        {'revision': 1, 'submit': False, 'answers': answers}, cookie), cookies))
                    save_seconds = time.monotonic() - started
                    started = time.monotonic()
                    list(pool.map(lambda cookie: request(student_port, '/api/answers',
                        {'revision': 2, 'submit': True, 'answers': answers}, cookie), cookies))
                    submit_seconds = time.monotonic() - started
            attempts = request(admin_port, f'/api/exams/{exam_id}', cookie=admin_cookie)[0]['attempts']
            assert len(attempts) == clients
            assert all(a['submitted_at'] and a['answers'] == answers_for(int(a['code'][3:]) if chaos else 0)
                       and a['score'] == 6 and a['pending'] == 1 for a in attempts)
            print(json.dumps({'clients': clients, 'student_host': student_host, 'failed': 0,
                              'test_only_limit': clients > 1000,
                              'max_parallel_requests': min(clients, workers),
                              'poll_rounds': poll_rounds,
                              'slowest_poll_wave_seconds': round(max(poll_seconds, default=0), 3),
                              'join_seconds': round(join_seconds, 3),
                              'save_seconds': round(save_seconds, 3) if save_seconds is not None else None,
                              'submit_seconds': round(submit_seconds, 3) if submit_seconds is not None else None,
                              'chaos_seconds': round(chaos_seconds, 3) if chaos else None,
                              'simulated_disconnects': sum(index % 4 == 0 for index in range(clients)) if chaos else 0,
                              'lost_save_replies': sum(index % 4 == 1 for index in range(clients)) if chaos else 0,
                              'lost_submit_replies': sum(index % 5 == 0 for index in range(clients)) if chaos else 0}))
        finally:
            proc.terminate()
            proc.wait(timeout=20)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--clients', type=int, default=100)
    parser.add_argument('--student-host', default='127.0.0.1')
    parser.add_argument('--poll-rounds', type=int, default=0,
                        help='repeat the student session check every three seconds before saving')
    parser.add_argument('--chaos', action='store_true',
                        help='simulate disconnected clients, delayed saves, and lost acknowledgements')
    parser.add_argument('--workers', type=int, default=256,
                        help='maximum requests in flight (default: 256)')
    args = parser.parse_args()
    if not 1 <= args.clients <= 10000:
        parser.error('--clients must be between 1 and 10000')
    if not 0 <= args.poll_rounds <= 1200:
        parser.error('--poll-rounds must be between 0 and 1200')
    if not 1 <= args.workers <= 1024:
        parser.error('--workers must be between 1 and 1024')
    run(args.clients, args.student_host, args.poll_rounds, args.chaos, args.workers)
