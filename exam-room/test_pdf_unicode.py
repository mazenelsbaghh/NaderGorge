import io
import json
from pathlib import Path
import runpy
import sys
import tempfile
import unittest
from unittest.mock import patch
import zipfile


class ArabicReportEncodingTests(unittest.TestCase):
    def test_utf8_reports_preserve_arabic_with_a_legacy_windows_file_encoding(self):
        # The Go exporter writes UTF-8, even when Windows defaults to a legacy code page.
        report = {
            'exam': {'config': {'title': 'امتحان الشهر الثاني'}},
            'attempt': {
                'name': 'أحمد علي', 'phone': '01000000000', 'code': '00001',
                'score': 1, 'maximum': 2,
                'answers': {'essay': 'إجابة الطالب بالعربية'},
                'grades': {'essay': {'score': 1, 'feedback': 'إجابة جزئية صحيحة'}},
            },
            'questions': [{'id': 'essay', 'kind': 'essay', 'text': 'اشرح الإجابة',
                           'points': 2, 'modelAnswer': 'الإجابة النموذجية'}],
        }
        renderer = Path(__file__).with_name('pdf_report.py')
        real_open = io.open
        with tempfile.TemporaryDirectory(prefix='massar-arabic-pdf-') as temporary:
            folder = Path(temporary) / 'تقارير الطلاب'
            folder.mkdir()
            source = folder / 'تقرير.json'

            def legacy_open(file, mode='r', buffering=-1, encoding=None, *args, **kwargs):
                if file == source and 'b' not in mode and encoding in (None, 'locale'):
                    encoding = 'cp1252'
                return real_open(file, mode, buffering, encoding, *args, **kwargs)

            for archive in (False, True):
                with self.subTest(archive=archive):
                    source.write_bytes(json.dumps([report, report] if archive else report,
                                                  ensure_ascii=False).encode('utf-8'))
                    output = folder / ('تقارير.zip' if archive else 'تقرير.pdf')
                    arguments = [str(renderer), *(['--zip'] if archive else []), str(source), str(output)]
                    # Mock only the OS file-encoding boundary; render actual PDFs with bundled fonts.
                    with patch('io.open', legacy_open), patch.object(sys, 'argv', arguments):
                        runpy.run_path(str(renderer), run_name='__main__')
                    if archive:
                        with zipfile.ZipFile(output) as bundle:
                            self.assertEqual(bundle.namelist(), [
                                'أحمد علي - 1 من 2.pdf', 'أحمد علي - 1 من 2 (2).pdf',
                            ])
                            self.assertIsNone(bundle.testzip())
                            documents = [bundle.read(name) for name in bundle.namelist()]
                    else:
                        documents = [output.read_bytes()]
                    for document in documents:
                        self.assertGreater(len(document), 2000)
                        self.assertTrue(document.startswith(b'%PDF-'))
                        self.assertIn(b'%%EOF', document[-64:])


if __name__ == '__main__':
    unittest.main()
