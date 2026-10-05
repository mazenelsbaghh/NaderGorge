from contextlib import contextmanager
from pathlib import Path
import sqlite3
import time


ATTEMPTS_TABLE = """
CREATE TABLE IF NOT EXISTS attempts (
    id TEXT PRIMARY KEY, exam_id TEXT NOT NULL REFERENCES exams(id),
    code TEXT NOT NULL, name TEXT, phone TEXT,
    token_hash TEXT UNIQUE, joined_at REAL, last_seen REAL, deadline REAL,
    question_ids TEXT NOT NULL DEFAULT '[]', answers TEXT NOT NULL DEFAULT '{}',
    revision INTEGER NOT NULL DEFAULT 0, submitted_at REAL,
    submit_reason TEXT, grades TEXT NOT NULL DEFAULT '{}',
    UNIQUE(exam_id, code)
);
"""

SCHEMA = """
CREATE TABLE IF NOT EXISTS exams (
    id TEXT PRIMARY KEY, config TEXT NOT NULL,
    state TEXT NOT NULL DEFAULT 'draft', created_at REAL NOT NULL,
    started_at REAL, ended_at REAL
);
CREATE UNIQUE INDEX IF NOT EXISTS one_live_exam ON exams ((1))
    WHERE state IN ('waiting', 'running');
""" + ATTEMPTS_TABLE + """
CREATE INDEX IF NOT EXISTS attempts_by_exam ON attempts(exam_id);
CREATE INDEX IF NOT EXISTS attempts_by_deadline ON attempts(deadline)
    WHERE submitted_at IS NULL;
CREATE TABLE IF NOT EXISTS audit (
    id INTEGER PRIMARY KEY, created_at REAL NOT NULL,
    action TEXT NOT NULL, subject_id TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS essay_grade_jobs (
    id TEXT PRIMARY KEY, exam_id TEXT NOT NULL REFERENCES exams(id),
    state TEXT NOT NULL, model TEXT NOT NULL,
    total INTEGER NOT NULL, processed INTEGER NOT NULL DEFAULT 0,
    graded INTEGER NOT NULL DEFAULT 0, skipped INTEGER NOT NULL DEFAULT 0,
    needs_review INTEGER NOT NULL DEFAULT 0, failed INTEGER NOT NULL DEFAULT 0,
    message TEXT NOT NULL DEFAULT '', created_at REAL NOT NULL, updated_at REAL NOT NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS one_running_essay_job ON essay_grade_jobs ((1)) WHERE state='running';
CREATE INDEX IF NOT EXISTS essay_jobs_by_exam ON essay_grade_jobs(exam_id,created_at DESC);
PRAGMA user_version=2;
"""


class Database:
    def __init__(self, path):
        self.path = Path(path)
        self.path.parent.mkdir(parents=True, exist_ok=True)
        with self.connection() as connection:
            connection.execute("PRAGMA journal_mode=WAL")
            version = connection.execute("PRAGMA user_version").fetchone()[0]
            if version > 2:
                raise RuntimeError("This database needs a newer version of Massar Exam Room")
            if version == 1:
                self.backup()
                migrate_student_codes(connection)
            connection.executescript(SCHEMA)
        self.path.chmod(0o600)

    @contextmanager
    def connection(self):
        connection = sqlite3.connect(self.path, timeout=15)
        connection.row_factory = sqlite3.Row
        connection.execute("PRAGMA foreign_keys=ON")
        connection.execute("PRAGMA synchronous=FULL")
        try:
            yield connection
        finally:
            connection.close()

    @contextmanager
    def transaction(self):
        with self.connection() as connection:
            connection.execute("BEGIN IMMEDIATE")
            try:
                yield connection
                connection.commit()
            except BaseException:
                connection.rollback()
                raise

    def backup(self):
        folder = self.path.parent / "backups"
        folder.mkdir(exist_ok=True)
        target = folder / f"exam-room-{time.time_ns()}.sqlite3"
        with self.connection() as source:
            destination = sqlite3.connect(target)
            try:
                source.backup(destination)
            finally:
                destination.close()
        target.chmod(0o600)
        return target


def audit(connection, action, subject_id):
    connection.execute("INSERT INTO audit(created_at, action, subject_id) VALUES(?,?,?)",
                       (time.time(), action, subject_id))


def migrate_student_codes(connection):
    # Scope student identity to each exam while preserving existing answers and sessions.
    connection.execute("BEGIN IMMEDIATE")
    try:
        connection.execute(ATTEMPTS_TABLE.replace("attempts (", "attempts_v2 ("))
        connection.execute("INSERT INTO attempts_v2 SELECT * FROM attempts")
        connection.execute("DROP TABLE attempts")
        connection.execute("ALTER TABLE attempts_v2 RENAME TO attempts")
        connection.execute("PRAGMA user_version=2")
        connection.commit()
    except BaseException:
        connection.rollback()
        raise
