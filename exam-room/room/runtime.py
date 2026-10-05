import logging
import os
from pathlib import Path
import sys
import threading
import time

from .database import Database
from .exams import ExamRoom
from .server import RoomServer
from .essay_ai import EssayBatch


def desktop_data_directory():
    if sys.platform == 'darwin':
        return Path.home() / 'Library' / 'Application Support' / 'Massar Exam Room'
    if sys.platform == 'win32':
        return Path(os.environ.get('LOCALAPPDATA', Path.home() / 'AppData' / 'Local')) / 'Massar Exam Room'
    return Path.home() / '.local' / 'share' / 'massar-exam-room'


class DataLock:
    """Hold an OS lock, which is released even if the process crashes."""
    def __init__(self, folder):
        folder.mkdir(parents=True, exist_ok=True)
        self.file = (folder / '.running.lock').open('a+b')
        try:
            if sys.platform == 'win32':
                import msvcrt
                self.file.write(b'0')
                self.file.flush()
                self.file.seek(0)
                msvcrt.locking(self.file.fileno(), msvcrt.LK_NBLCK, 1)
            else:
                import fcntl
                fcntl.flock(self.file, fcntl.LOCK_EX | fcntl.LOCK_NB)
        except OSError as error:
            self.file.close()
            raise RuntimeError('هذه البيانات مفتوحة في نسخة أخرى من البرنامج. أغلق النسخة الأخرى أولًا.') from error

    def close(self):
        self.file.close()


class Runtime:
    def __init__(self, folder, student_port=8765, admin_port=8766):
        self.lock = DataLock(Path(folder))
        self.stop = threading.Event()
        self.servers = []
        try:
            self.room = ExamRoom(Database(Path(folder) / 'exams.sqlite3'))
            self.grader = EssayBatch(self.room, self.stop)
            self.servers.append(RoomServer(('0.0.0.0', student_port), self.room, 'student'))
            self.servers.append(RoomServer(('127.0.0.1', admin_port), self.room, 'admin'))
            self.servers[1].grader = self.grader
            self.servers[1].student_port = self.servers[0].server_port
            self.room.expire()
            self.room.database.backup()
        except BaseException:
            for server in self.servers:
                server.server_close()
            self.lock.close()
            raise
        for server in self.servers:
            threading.Thread(target=server.serve_forever, daemon=True).start()
        self.worker = threading.Thread(target=self._maintain, daemon=True)
        self.worker.start()

    @property
    def admin_url(self):
        return f'http://127.0.0.1:{self.servers[1].server_port}'

    def _maintain(self):
        last_backup = time.monotonic()
        while not self.stop.wait(1):
            try:
                self.room.expire()
                if time.monotonic() - last_backup >= 300:
                    self.room.database.backup()
                    last_backup = time.monotonic()
            except Exception:
                logging.exception('Maintenance failed; exam writes may be unavailable')

    def close(self):
        self.stop.set()
        for server in self.servers:
            server.shutdown()
            server.server_close()
        self.worker.join()
        if self.grader.thread:
            self.grader.thread.join(timeout=45)
        # Let in-flight answer transactions finish before taking the final snapshot.
        for server in self.servers:
            for _ in range(128):
                server.slots.acquire()
        try:
            self.room.database.backup()
        finally:
            self.lock.close()
