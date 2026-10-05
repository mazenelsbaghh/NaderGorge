from pathlib import Path
import os
import re
import shutil
import subprocess
import sys

from .validation import RoomError


BACKUP_NAME = re.compile(r'exam-room-[0-9]+\.sqlite3')


def backup_files(database):
    folder = database.path.parent / 'backups'
    return sorted((path for path in folder.glob('exam-room-*.sqlite3')
                   if BACKUP_NAME.fullmatch(path.name) and path.is_file() and not path.is_symlink()),
                  key=lambda path: path.stat().st_mtime, reverse=True)


def backup_path(database, name):
    if not BACKUP_NAME.fullmatch(name):
        raise RoomError('النسخة المطلوبة غير موجودة', 404)
    path = database.path.parent / 'backups' / name
    if not path.is_file() or path.is_symlink():
        raise RoomError('النسخة المطلوبة غير موجودة', 404)
    return path


def storage_details(database):
    path = database.path.resolve()
    backups = backup_files(database)
    with database.connection() as connection:
        counts = {
            'exams': connection.execute('SELECT COUNT(*) FROM exams').fetchone()[0],
            'students': connection.execute('SELECT COUNT(*) FROM attempts WHERE joined_at IS NOT NULL').fetchone()[0],
            'submissions': connection.execute('SELECT COUNT(*) FROM attempts WHERE submitted_at IS NOT NULL').fetchone()[0],
        }
    wal = Path(str(path) + '-wal')
    return {'databasePath': str(path), 'folderPath': str(path.parent),
            'backupFolder': str(path.parent / 'backups'),
            'databaseBytes': path.stat().st_size, 'pendingBytes': wal.stat().st_size if wal.exists() else 0,
            'freeBytes': shutil.disk_usage(path.parent).free, 'counts': counts,
            'backupCount': len(backups), 'backupBytes': sum(file.stat().st_size for file in backups),
            'backups': [{'name': file.name, 'bytes': file.stat().st_size, 'createdAt': file.stat().st_mtime}
                        for file in backups[:30]]}


def check_database(database):
    with database.connection() as connection:
        messages = [row[0] for row in connection.execute('PRAGMA quick_check').fetchall()]
    return {'healthy': messages == ['ok'], 'messages': messages}


def open_data_folder(database):
    folder = str(database.path.resolve().parent)
    try:
        if sys.platform == 'win32':
            os.startfile(folder)
        else:
            command = 'open' if sys.platform == 'darwin' else 'xdg-open'
            subprocess.run([command, folder], check=True, timeout=5, capture_output=True)
    except (OSError, subprocess.SubprocessError) as error:
        raise RoomError('تعذر فتح المجلد. انسخ المسار وافتحه من مدير الملفات', 503) from error
    return {'opened': True}
