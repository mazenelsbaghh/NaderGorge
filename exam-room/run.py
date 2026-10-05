#!/usr/bin/env python3
"""Source launcher: business logic is served entirely by Go."""
import argparse
import os
from pathlib import Path
import subprocess
import sys
import time
from urllib.request import urlopen
import webbrowser


def main():
    root = Path(__file__).resolve().parent
    parser = argparse.ArgumentParser(description='Massar local exam room')
    parser.add_argument('--port', type=int, default=8765)
    parser.add_argument('--admin-port', type=int, default=8766)
    parser.add_argument('--data-dir', type=Path, default=root / 'data')
    parser.add_argument('--no-browser', action='store_true')
    args = parser.parse_args()
    binary_dir = root / 'build'
    binary_dir.mkdir(exist_ok=True)
    binary = binary_dir / ('massar-exam-room.exe' if sys.platform == 'win32' else 'massar-exam-room')
    subprocess.run(['go', 'build', '-trimpath', '-o', str(binary), '.'], cwd=root, check=True)
    command = [str(binary), '--port', str(args.port), '--admin-port', str(args.admin_port),
               '--data-dir', str(args.data_dir.resolve()), '--parent-pid', str(os.getpid())]
    process = subprocess.Popen(command, cwd=root)
    try:
        for _ in range(150):
            if process.poll() is not None:
                return process.returncode or 1
            try:
                with urlopen(f'http://127.0.0.1:{args.admin_port}/api/bootstrap', timeout=0.2) as response:
                    if response.status == 200:
                        break
            except OSError:
                pass
            time.sleep(0.1)
        else:
            print('Go server did not start.', file=sys.stderr)
            return 1
        url = f'http://127.0.0.1:{args.admin_port}'
        print(f'Massar Exam Room (Go)\nAdmin: {url}', flush=True)
        if not args.no_browser:
            webbrowser.open(url)
        return process.wait()
    except KeyboardInterrupt:
        return 0
    finally:
        if process.poll() is None:
            process.terminate()
            try:
                process.wait(timeout=50)
            except subprocess.TimeoutExpired:
                process.kill()
                process.wait()


if __name__ == '__main__':
    raise SystemExit(main())
