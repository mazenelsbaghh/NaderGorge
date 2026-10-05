"""Thin native window for the Go exam server."""
import argparse
from html import escape
import os
from pathlib import Path
import subprocess
import sys
import time
from urllib.request import urlopen

import webview
from network_setup import NetworkSetupApi


def data_directory():
    if sys.platform == 'darwin':
        return Path.home() / 'Library' / 'Application Support' / 'Massar Exam Room'
    if sys.platform == 'win32':
        return Path(os.environ.get('LOCALAPPDATA', Path.home() / 'AppData' / 'Local')) / 'Massar Exam Room'
    return Path.home() / '.local' / 'share' / 'massar-exam-room'


def backend_binary():
    base = Path(getattr(sys, '_MEIPASS', Path(__file__).resolve().parent))
    name = 'massar-exam-room.exe' if sys.platform == 'win32' else 'massar-exam-room'
    return base / 'go-server' / name


def main():
    parser = argparse.ArgumentParser(description='Massar Exam Room desktop')
    parser.add_argument('--data-dir', type=Path, default=data_directory())
    parser.add_argument('--port', type=int, default=8765)
    parser.add_argument('--admin-port', type=int, default=8766)
    args = parser.parse_args()
    args.data_dir.mkdir(parents=True, exist_ok=True)
    binary = backend_binary()
    if not binary.is_file():
        message = 'ملف خادم Go غير موجود في حزمة البرنامج.'
        process = None
    else:
        output = (args.data_dir / 'room.log').open('ab', buffering=0)
        try:
            process = subprocess.Popen([str(binary), '--data-dir', str(args.data_dir),
                                        '--port', str(args.port), '--admin-port', str(args.admin_port),
                                        '--pdf-helper', str(binary.parent.parent / 'report-helper' / ('massar-report-pdf.exe' if sys.platform == 'win32' else 'massar-report-pdf')),
                                        '--parent-pid', str(os.getpid())],
                                       stdout=output, stderr=subprocess.STDOUT)
        finally:
            output.close()
        message = None
        url = f'http://127.0.0.1:{args.admin_port}/api/bootstrap'
        for _ in range(100):
            if process.poll() is not None:
                message = 'تعذر تشغيل خادم Go. قد تكون نسخة أخرى مفتوحة؛ راجع room.log في مجلد البيانات.'
                break
            try:
                with urlopen(url, timeout=0.2) as response:
                    if response.status == 200:
                        break
            except OSError:
                pass
            time.sleep(0.1)
        else:
            message = 'الخادم لم يبدأ خلال عشر ثوانٍ؛ راجع room.log في مجلد البيانات.'
    if message:
        webview.create_window('مسار · تعذر التشغيل', html=f'<html lang="ar" dir="rtl"><body><h1>تعذر تشغيل مسار</h1><p>{escape(message)}</p><p>{escape(str(args.data_dir))}</p></body></html>', width=680, height=360)
        webview.start()
        if process and process.poll() is None:
            process.terminate()
        return 1
    try:
        webview.settings['ALLOW_DOWNLOADS'] = True
        webview.create_window('مسار · امتحانات السنتر', f'http://127.0.0.1:{args.admin_port}',
                              width=1360, height=900, min_size=(800, 600), confirm_close=True,
                              js_api=NetworkSetupApi(),
                              text_select=True,
                              localization={'global.quit': 'إغلاق البرنامج', 'global.cancel': 'رجوع',
                                            'global.saveFile': 'حفظ ملف',
                                            'global.quitConfirmation': 'إغلاق البرنامج سيوقف اتصال الطلاب. الإجابات المحفوظة ستبقى، والوقت سيستمر في الانقضاء. هل تريد الإغلاق؟'})
        webview.start(private_mode=True, gui='edgechromium' if sys.platform == 'win32' else None)
    finally:
        process.terminate()
        try:
            process.wait(timeout=50)
        except subprocess.TimeoutExpired:
            process.kill()
            process.wait()
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
