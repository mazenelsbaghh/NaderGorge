"""Native macOS setup bridge for the offline, directly wired exam network."""

from pathlib import Path
import os
import re
import subprocess
import sys
import time


EXAM_IP = '10.77.0.1'


def command(*args, timeout=10):
    return subprocess.run(args, capture_output=True, text=True, timeout=timeout, check=False)


def bundle_path():
    return Path(getattr(sys, '_MEIPASS', Path(__file__).resolve().parent)) / 'portal-helper'


def service_running(label):
    result = command('launchctl', 'print', f'system/{label}')
    return result.returncode == 0 and 'state = running' in result.stdout


def dhcp_failure_detail():
    report = command('launchctl', 'print', 'system/com.massar.examroom.dhcp')
    if report.returncode:
        return 'خدمة توزيع العناوين غير مسجلة في macOS.'
    for log_name in ('dnsmasq-error.log', 'dnsmasq.log'):
        log_file = Path('/var/db/massar-exam-room') / log_name
        try:
            lines = log_file.read_text(errors='replace').splitlines()[-30:]
        except OSError:
            continue
        errors = [line for line in lines if re.search(r'failed|error|cannot|unable|address already|permission denied',
                                                      line, re.IGNORECASE)]
        if errors:
            return errors[-1][-280:]
    exit_code = re.search(r'last exit code = (\d+)', report.stdout)
    if exit_code:
        return f'خدمة توزيع العناوين أُغلقت برمز {exit_code.group(1)}.'
    return 'خدمة توزيع العناوين لم تبدأ؛ اضغط فحص الاتصال بعد بضع ثوانٍ.'


def wired_service():
    services = command('networksetup', '-listnetworkserviceorder').stdout
    candidates = []
    for match in re.finditer(r'^\(\d+\) ([^\n]+)\n\(Hardware Port: ([^\n]+), Device: (en\d+)\)$',
                             services, re.MULTILINE):
        name, port, device = match.groups()
        if name.startswith('*') or re.search(r'wi-?fi|airport|iphone|bluetooth|bridge',
                                               f'{name} {port}', re.IGNORECASE):
            continue
        active = 'status: active' in command('ifconfig', device).stdout
        candidates.append((active, name, device))
    if not candidates:
        return None
    active, name, device = next((item for item in candidates if item[0]), candidates[0])
    return {'service': name, 'device': device, 'active': active}


class NetworkSetupApi:
    def network_status(self):
        if sys.platform != 'darwin':
            return {'supported': False, 'platform': sys.platform,
                    'message': 'تهيئة الشبكة المباشرة متاحة على الماك حاليًا. تجهيز ويندوز قيد الإعداد.'}
        wired = wired_service()
        info = command('networksetup', '-getinfo', wired['service']).stdout if wired else ''
        match = re.search(r'^IP address: (.+)$', info, re.MULTILINE)
        ethernet_ip = match.group(1) if match else ''
        dhcp_running = service_running('com.massar.examroom.dhcp')
        portal_running = service_running('com.massar.examroom.portal')
        installed_config = Path('/usr/local/etc/massar-exam-dnsmasq.conf')
        config_check = (command(str(bundle_path() / 'dnsmasq'), '--test',
                                f'--conf-file={installed_config}')
                        if installed_config.is_file() and (bundle_path() / 'dnsmasq').is_file() else None)
        return {
            'supported': True,
            'ready': bool(wired) and ethernet_ip == EXAM_IP and dhcp_running and portal_running,
            'ethernetActive': wired['active'] if wired else False,
            'ethernetService': wired['service'] if wired else '',
            'ethernetDevice': wired['device'] if wired else '',
            'ethernetIp': ethernet_ip,
            'dhcpRunning': dhcp_running,
            'dhcpDetail': '' if dhcp_running else dhcp_failure_detail(),
            'configValid': config_check.returncode == 0 if config_check else None,
            'configDetail': (config_check.stderr or config_check.stdout).strip()[-280:] if config_check else '',
            'portalRunning': portal_running,
            'dnsmasqAvailable': (bundle_path() / 'dnsmasq').is_file(),
            'studentUrl': f'http://{EXAM_IP}/',
            'apIp': '10.77.0.2',
        }

    def prepare_network(self):
        if sys.platform != 'darwin':
            return {'ok': False, 'message': 'تهيئة الشبكة المباشرة متاحة على الماك حاليًا.'}
        source = bundle_path()
        script = source / 'mac-direct-setup.sh'
        if not script.is_file():
            return {'ok': False, 'message': 'ملفات تهيئة الشبكة غير موجودة في نسخة البرنامج الحالية.'}
        wired = wired_service()
        if not wired:
            return {'ok': False, 'message': 'وصّل محوّل كابل Ethernet بالماك ثم اضغط تهيئة الماك مرة أخرى.'}
        if not (source / 'dnsmasq').is_file():
            return {'ok': False, 'message': 'مكوّن توزيع العناوين غير موجود داخل نسخة البرنامج. ثبّت النسخة الأحدث.'}
        # osascript shows the normal macOS administrator prompt. The password
        # stays with macOS and never enters the web view or this Python process.
        result = command('osascript',
                         '-e', 'on run argv',
                         '-e', 'do shell script ("/bin/sh " & quoted form of (item 1 of argv) & " " & quoted form of (item 2 of argv) & " " & quoted form of (item 3 of argv)) with administrator privileges',
                         '-e', 'end run', str(script), wired['service'], wired['device'], timeout=180)
        if result.returncode:
            message = (result.stderr or result.stdout).strip()
            if 'User canceled' in message or '(-128)' in message:
                message = 'أُلغيت نافذة تصريح macOS؛ لم تكتمل التهيئة.'
            return {'ok': False, 'message': message[-500:] or 'تعذرت تهيئة الشبكة.'}
        for _ in range(10):
            state = self.network_status()
            if state['ready']:
                break
            time.sleep(1)
        if state['ready']:
            message = 'تم تجهيز الماك لشبكة الامتحان المباشرة.'
        elif state['ethernetIp'] != EXAM_IP:
            message = f'عنوان منفذ الكابل غير جاهز: {state["ethernetIp"] or "لا يوجد عنوان"}.'
        elif not state['dhcpRunning']:
            message = f'توزيع العناوين لم يبدأ: {state["dhcpDetail"]}'
        else:
            message = 'خدمة صفحة الطلاب لم تبدأ بعد. اضغط فحص الاتصال.'
        return {'ok': state['ready'], 'message': message, 'status': state}
