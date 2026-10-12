import io
import json
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
import tempfile
import threading
import unittest
from unittest.mock import patch
from urllib import error, request
import zipfile

from exam_downloads import ExamDownloads, XLSX


def workbook_bytes():
    output = io.BytesIO()
    with zipfile.ZipFile(output, 'w') as archive:
        archive.writestr('[Content_Types].xml', '<Types/>')
        archive.writestr('xl/workbook.xml', '<workbook/>')
        archive.writestr('xl/worksheets/sheet1.xml', '<worksheet/>')
    return output.getvalue()


class ExamDownloadTest(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory(prefix='exam-download-test-')
        self.addCleanup(self.directory.cleanup)
        self.destination = Path(self.directory.name) / 'نتائج.xlsx'
        self.workbook = workbook_bytes()
        self.reply = {'status': 200, 'type': XLSX, 'body': self.workbook}
        self.selection = [str(self.destination)]
        self.dialog_count = 0
        self.requests = []
        fixture = self

        class Handler(BaseHTTPRequestHandler):
            def do_GET(self):
                fixture.requests.append((self.command, self.path))
                if self.path == '/api/bootstrap':
                    self.send_response(200)
                    self.send_header('Content-Type', 'application/json')
                    self.end_headers()
                    self.wfile.write(json.dumps({'token': 'current-admin-secret'}).encode())
                    return
                if self.headers.get('X-Admin-Token') != 'current-admin-secret':
                    self.send_response(401)
                    self.send_header('Content-Type', 'application/json')
                    self.end_headers()
                    self.wfile.write(json.dumps({'error': 'حدّث لوحة الإدارة لإعادة الاتصال'}).encode())
                    return
                self.send_response(fixture.reply['status'])
                self.send_header('Content-Type', fixture.reply['type'])
                self.send_header('Content-Length', str(fixture.reply.get('length', len(fixture.reply['body']))))
                if fixture.reply['status'] == 302:
                    self.send_header('Location', '/unexpected')
                self.end_headers()
                self.wfile.write(fixture.reply['body'])

            def do_POST(self):
                self.rfile.read(int(self.headers.get('Content-Length', '0')))
                self.do_GET()

            def log_message(self, *_):
                pass

        self.server = ThreadingHTTPServer(('127.0.0.1', 0), Handler)
        self.thread = threading.Thread(target=self.server.serve_forever)
        self.thread.start()
        self.addCleanup(self.close_server)
        self.downloader = ExamDownloads(self.server.server_port, self.choose_destination)

    def close_server(self):
        self.server.shutdown()
        self.thread.join()
        self.server.server_close()

    def choose_destination(self, filename):
        self.dialog_count += 1
        return self.selection

    def download(self):
        return self.downloader.download_exam_file('/api/exams/exam-1/results.xlsx?groupId=group-2',
                                                  'نتائج.xlsx', XLSX)

    def test_native_export_preserves_workbook_when_authless_refetch_returns_json(self):
        # Regression: macOS's second, unauthenticated request saved a JSON error as XLSX.
        url = f'http://127.0.0.1:{self.server.server_port}/api/exams/exam-1/results.xlsx'
        with self.assertRaises(error.HTTPError) as failure:
            request.urlopen(url)
        self.assertEqual(failure.exception.code, 401)
        failure.exception.close()
        receipt = self.download()
        self.assertEqual(receipt, {'saved': True, 'path': str(self.destination)})
        self.assertEqual(self.destination.read_bytes(), self.workbook)
        self.assertEqual(self.dialog_count, 1)

    def test_errors_and_damaged_workbooks_never_open_save_dialog_or_replace_file(self):
        damaged_crc = self.workbook.replace(b'<workbook/>', b'<workbook!>')
        non_workbook = io.BytesIO()
        with zipfile.ZipFile(non_workbook, 'w') as archive:
            archive.writestr('error.json', '{"error":"not a workbook"}')
        for reply in [
            {'status': 401, 'type': 'application/json', 'body': b'{"error":"expired"}'},
            {'status': 200, 'type': 'application/json', 'body': b'{"error":"expired"}'},
            {'status': 200, 'type': XLSX, 'body': b'{"error":"expired"}'},
            {'status': 200, 'type': XLSX, 'body': self.workbook[:-22]},
            {'status': 200, 'type': XLSX, 'body': damaged_crc},
            {'status': 200, 'type': XLSX, 'body': non_workbook.getvalue()},
            {'status': 200, 'type': XLSX, 'body': self.workbook, 'length': len(self.workbook) + 1},
            {'status': 302, 'type': XLSX, 'body': self.workbook},
        ]:
            with self.subTest(reply={key: value for key, value in reply.items() if key != 'body'}):
                self.reply = reply
                self.destination.write_bytes(b'existing export')
                with self.assertRaises(RuntimeError):
                    self.download()
                self.assertEqual(self.destination.read_bytes(), b'existing export')
                self.assertEqual(self.dialog_count, 0)
                self.assertFalse(list(self.destination.parent.glob('.massar-download-*')))
        self.assertNotIn(('GET', '/unexpected'), self.requests)

    def test_cancelling_native_dialog_preserves_existing_file(self):
        self.destination.write_bytes(b'existing export')
        self.selection = None
        self.assertEqual(self.download(), {'saved': False})
        self.assertEqual(self.destination.read_bytes(), b'existing export')
        self.assertEqual(self.dialog_count, 1)

    def test_failed_atomic_replace_preserves_existing_file_and_removes_staging_file(self):
        self.destination.write_bytes(b'existing export')
        with patch('exam_downloads.os.replace', side_effect=OSError('disk failure')):
            with self.assertRaises(RuntimeError):
                self.download()
        self.assertEqual(self.destination.read_bytes(), b'existing export')
        self.assertFalse(list(self.destination.parent.glob('.massar-download-*')))

    def test_unapproved_routes_and_filenames_never_contact_server(self):
        for path, filename, mime_type in [
            ('https://example.com/export.xlsx', 'result.xlsx', XLSX),
            ('//example.com/export.xlsx', 'result.xlsx', XLSX),
            ('/api/exams/exam-1/results.xlsx#fragment', 'result.xlsx', XLSX),
            ('/api/exams/exam-1/results.xlsx?groupId=a&groupId=b', 'result.xlsx', XLSX),
            ('/api/exams/exam-1/results.xlsx', '../result.xlsx', XLSX),
            ('/api/exams/exam-1/results.xlsx', 'result.pdf', 'application/pdf'),
            ('/api/backups/../exams.sqlite3', 'backup.sqlite3', 'application/octet-stream'),
            ('/api/settings/gemini', 'settings.xlsx', XLSX),
        ]:
            with self.subTest(path=path, filename=filename):
                with self.assertRaises(RuntimeError):
                    self.downloader.download_exam_file(path, filename, mime_type)
        self.assertEqual(self.requests, [])
        self.assertEqual(self.dialog_count, 0)
        self.assertFalse(self.destination.exists())

    def test_pdf_zip_csv_and_backup_exports_use_validated_native_save(self):
        sqlite_bytes = b'SQLite format 3\x00' + b'\x00' * 84
        exports = [
            ('/api/attempts/attempt-1/report.pdf', 'تقرير.pdf', 'application/pdf', b'%PDF-1.4\nbody\n%%EOF\n', 'GET'),
            ('/api/exams/exam-1/reports.zip', 'تقارير.zip', 'application/zip', self.workbook, 'GET'),
            ('/api/exams/exam-1/results.csv', 'نتائج.csv', 'text/csv', 'اسم,درجة\nطالب,10\n'.encode(), 'GET'),
            ('/api/backups/exam-room-123.sqlite3', 'نسخة.sqlite3', 'application/octet-stream', sqlite_bytes, 'GET'),
            ('/api/backup', 'نسخة.sqlite3', 'application/octet-stream', sqlite_bytes, 'POST'),
        ]
        for path, filename, mime_type, contents, method in exports:
            with self.subTest(path=path):
                self.reply = {'status': 200, 'type': mime_type + '; charset=utf-8', 'body': contents}
                receipt = self.downloader.download_exam_file(path, filename, mime_type)
                self.assertTrue(receipt['saved'])
                self.assertEqual(self.destination.read_bytes(), contents)
                self.assertIn((method, path), self.requests)

    def test_corrupt_pdf_csv_and_backups_are_rejected_before_save_dialog(self):
        for path, filename, mime_type, contents in [
            ('/api/attempts/attempt-1/report.pdf', 'تقرير.pdf', 'application/pdf', b'%PDF-1.4\ntruncated'),
            ('/api/exams/exam-1/results.csv', 'نتائج.csv', 'text/csv', b'{"error":"failed"}'),
            ('/api/backups/exam-room-123.sqlite3', 'نسخة.sqlite3', 'application/octet-stream', b'SQLite format 3\x00'),
        ]:
            with self.subTest(path=path):
                self.reply = {'status': 200, 'type': mime_type, 'body': contents}
                with self.assertRaises(RuntimeError):
                    self.downloader.download_exam_file(path, filename, mime_type)
                self.assertFalse(self.destination.exists())
                self.assertEqual(self.dialog_count, 0)


if __name__ == '__main__':
    unittest.main()
