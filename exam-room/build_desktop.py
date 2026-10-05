"""Run using the build environment on each target OS. Never package data/."""
from pathlib import Path
import argparse
import os
import plistlib
import subprocess
import sys


root = Path(__file__).resolve().parent
parser = argparse.ArgumentParser()
parser.add_argument('--output-dir', type=Path, default=root / 'dist')
args = parser.parse_args()
frontend = root / 'frontend'
if not (frontend / 'node_modules').is_dir():
    subprocess.run(['npm', 'ci', '--prefix', str(frontend)], cwd=root, check=True)
subprocess.run(['npm', 'run', 'build', '--prefix', str(frontend)], cwd=root, check=True)
binary_dir = root / 'build' / 'go-server'
binary_dir.mkdir(parents=True, exist_ok=True)
binary = binary_dir / ('massar-exam-room.exe' if sys.platform == 'win32' else 'massar-exam-room')
subprocess.run(['go', 'build', '-trimpath', '-o', str(binary), '.'], cwd=root, check=True)
pdf_dist = root / 'build' / 'pdf-dist'
subprocess.run([sys.executable, '-m', 'PyInstaller', '--noconfirm', '--clean', '--onefile',
                '--name', 'massar-report-pdf', '--distpath', str(pdf_dist),
                '--workpath', str(root / 'build' / 'pdf-work'), '--specpath', str(root / 'build'),
                '--add-data', f'{root / "web/assets/Tajawal-Regular.ttf"}{os.pathsep}fonts',
                '--add-data', f'{root / "web/assets/Tajawal-Bold.ttf"}{os.pathsep}fonts',
                str(root / 'pdf_report.py')], cwd=root, check=True)
pdf_binary = pdf_dist / ('massar-report-pdf.exe' if sys.platform == 'win32' else 'massar-report-pdf')
command = [sys.executable, '-m', 'PyInstaller', '--noconfirm', '--clean', '--windowed',
           '--name', 'Massar Exam Room', '--add-binary', f'{binary}{os.pathsep}go-server',
           '--add-binary', f'{pdf_binary}{os.pathsep}report-helper',
           '--distpath', str(args.output_dir.resolve()), '--workpath', str(root / 'build'),
           '--specpath', str(root / 'build'), '--paths', str(root)]
if sys.platform == 'darwin':
    portal_binary = binary_dir / 'massar-exam-portal-proxy'
    dnsmasq_binary = Path('/opt/homebrew/opt/dnsmasq/sbin/dnsmasq')
    if not dnsmasq_binary.is_file():
        raise RuntimeError('Install dnsmasq on the build Mac before packaging.')
    subprocess.run(['go', 'build', '-trimpath', '-o', str(portal_binary), './cmd/portal-proxy'], cwd=root, check=True)
    command += ['--osx-bundle-identifier', 'com.massar.examroom',
                '--icon', str(root / 'packaging' / 'Massar.icns'),
                '--add-binary', f'{portal_binary}{os.pathsep}portal-helper',
                '--add-binary', f'{dnsmasq_binary}{os.pathsep}portal-helper',
                '--add-data', f'{root / "packaging" / "mac-direct-setup.sh"}{os.pathsep}portal-helper',
                '--add-data', f'{root / "packaging" / "omada-direct-dnsmasq.conf"}{os.pathsep}portal-helper',
                '--add-data', f'{root / "packaging" / "vendor" / "dnsmasq-2.93.tar.gz"}{os.pathsep}third-party',
                '--add-data', f'{root / "packaging" / "vendor" / "COPYING"}{os.pathsep}third-party',
                '--add-data', f'{root / "packaging" / "vendor" / "COPYING-v3"}{os.pathsep}third-party']
elif sys.platform == 'win32':
    command += ['--icon', str(root / 'packaging' / 'Massar.ico')]
command.append(str(root / 'desktop.py'))
subprocess.run(command, cwd=root, check=True)
if sys.platform == 'darwin':
    app = args.output_dir.resolve() / 'Massar Exam Room.app'
    info = app / 'Contents' / 'Info.plist'
    with info.open('rb') as source:
        metadata = plistlib.load(source)
    metadata['CFBundleShortVersionString'] = '0.4.4'
    metadata['CFBundleVersion'] = '23'
    with info.open('wb') as target:
        plistlib.dump(metadata, target)
    subprocess.run(['codesign', '--force', '--deep', '--sign', '-', '--timestamp=none', str(app)], check=True)
    subprocess.run(['codesign', '--verify', '--deep', '--strict', str(app)], check=True)
    installer = args.output_dir.resolve() / 'Massar-Exam-Room-macOS-arm64-Installer.pkg'
    subprocess.run(['pkgbuild', '--component', str(app), '--install-location', '/Applications',
                    '--identifier', 'com.massar.examroom.installer', '--version', '0.4.4',
                    str(installer)], check=True)
    print(f'Installable package: {installer}')
