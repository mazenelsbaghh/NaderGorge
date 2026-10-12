"""Execute packaged Windows helpers using synthetic data and read-only commands."""
import argparse
import json
import os
from pathlib import Path
import subprocess
import tempfile
import zipfile


def run(executable, *arguments, request=None, cwd):
    result = subprocess.run(
        [str(executable), *map(str, arguments)],
        input=None if request is None else json.dumps(request).encode('utf-8'),
        stdout=subprocess.PIPE, stderr=subprocess.PIPE, timeout=45, cwd=cwd,
    )
    if result.returncode:
        raise RuntimeError('Packaged runtime execution failed: ' + executable.name)
    return result.stdout


def verify(directory):
    if os.name != 'nt':
        raise RuntimeError('Runtime verification must execute on Windows.')
    directory = directory.resolve(strict=True)
    with tempfile.TemporaryDirectory(prefix='massar-runtime-verification-') as temporary:
        folder = Path(temporary)
        run(directory / 'massar-exam-room.exe', '--help', cwd=folder)
        run(directory / 'massar-exam-portal-proxy.exe', '--help', cwd=folder)
        adapters = json.loads(run(
            directory / 'massar-exam-network.exe',
            request={'action': 'list-adapters'}, cwd=folder,
        ).decode('utf-8-sig'))
        if adapters.get('ok') is not True or not isinstance(adapters.get('adapters'), list):
            raise RuntimeError('Packaged adapter inspection did not return its contract.')
        hotspot = json.loads(run(
            directory / 'massar-exam-network.exe',
            request={'action': 'hotspot-status'}, cwd=folder,
        ).decode('utf-8-sig'))
        if hotspot.get('ok') is not True or not isinstance(hotspot.get('active'), bool):
            raise RuntimeError('Packaged Wi-Fi Direct status helper did not start.')
        report = {
            'exam': {'config': {'title': 'امتحان تجريبي للتحقق'}},
            'attempt': {
                'name': 'طالب اختبار فقط', 'phone': '01000000000', 'code': '00001',
                'score': 1, 'maximum': 2,
                'answers': {'q1': 0, 'q2': 'إجابة اختبار عربية'},
                'grades': {'q1': {'score': 1}, 'q2': {'score': 0, 'feedback': 'ملاحظة اختبار'}},
            },
            'questions': [
                {'id': 'q1', 'kind': 'mcq', 'text': 'سؤال اختيار تجريبي', 'points': 1,
                 'options': ['الإجابة الأولى', 'الإجابة الثانية'], 'correct': 0},
                {'id': 'q2', 'kind': 'essay', 'text': 'سؤال مقالي تجريبي', 'points': 1,
                 'modelAnswer': 'نموذج اختبار عربي'},
            ],
        }
        source, pdf, archive = folder / 'report.json', folder / 'report.pdf', folder / 'reports.zip'
        # Match Go's json.Marshal output: real UTF-8 Arabic, regardless of the Windows locale.
        source.write_text(json.dumps(report, ensure_ascii=False), encoding='utf-8')
        helper = directory / 'report-pdf/massar-report-pdf.exe'
        run(helper, source, pdf, cwd=folder)
        content = pdf.read_bytes()
        if len(content) < 2000 or not content.startswith(b'%PDF-') or b'%%EOF' not in content[-64:]:
            raise RuntimeError('Packaged Arabic PDF export is incomplete.')
        source.write_text(json.dumps([report, report], ensure_ascii=False), encoding='utf-8')
        run(helper, '--zip', source, archive, cwd=folder)
        with zipfile.ZipFile(archive) as bundle:
            names = bundle.namelist()
            expected_names = ['طالب اختبار فقط - 1 من 2.pdf', 'طالب اختبار فقط - 1 من 2 (2).pdf']
            if names != expected_names or bundle.testzip() is not None:
                raise RuntimeError('Packaged PDF ZIP export changed Arabic names or lost duplicate reports.')
            if any(not bundle.read(name).startswith(b'%PDF-') for name in names):
                raise RuntimeError('Packaged PDF ZIP contains invalid reports.')
    return {'windowsCliSmokePassed': True, 'syntheticPdfAndZipPassed': True,
            'networkMutations': False, 'physicalWindowsVerified': False}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('directory', type=Path)
    arguments = parser.parse_args()
    print(json.dumps(verify(arguments.directory)))
