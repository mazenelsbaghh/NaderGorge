"""Native macOS setup bridge for the offline, directly wired exam network."""

from pathlib import Path
import io
from http.client import HTTPException
import ipaddress
import json
import os
import re
import subprocess
import sys
import time
import uuid
from urllib import error, parse, request

from network_diagnostics import DEFAULT_CONFIG, config_options, read_snapshot, inspect_dhcp_leases, inspect_dhcp_socket, sanitized_dhcp_log
from network_report import app_release, portal_event, read_backend_diagnostics, report_complete
from network_timeline import safe_state, utc_time
from exam_downloads import write_download


EXAM_IP = '10.77.0.1'
PORTAL_DIAGNOSTIC_DIRECTORY = Path('/var/db/massar-exam-room')


def command(*args, timeout=10):
    return subprocess.run(args, capture_output=True, text=True, timeout=timeout, check=False)


def bundle_path():
    return Path(getattr(sys, '_MEIPASS', Path(__file__).resolve().parent)) / 'portal-helper'


def service_running(label):
    result = command('launchctl', 'print', f'system/{label}', timeout=3)
    return result.returncode == 0 and 'state = running' in result.stdout


def dhcp_failure_detail():
    report = command('launchctl', 'print', 'system/com.massar.examroom.dhcp', timeout=3)
    if report.returncode:
        return 'خدمة توزيع العناوين غير مسجلة في macOS.'
    for log_name in ('dnsmasq-error.log', 'dnsmasq.log'):
        log_file = Path('/var/db/massar-exam-room') / log_name
        try:
            lines = diagnostic_tail(log_file, 32768).splitlines()[-30:]
        except OSError:
            continue
        errors = [line for line in lines if re.search(r'failed|error|cannot|unable|address already|permission denied',
                                                      line, re.IGNORECASE)]
        safe_errors = sanitized_dhcp_log('\n'.join(errors)).splitlines()
        if safe_errors:
            return 'آخر خطأ مسجل (قد يكون سابقًا): ' + safe_errors[-1][-280:]
    exit_code = re.search(r'last exit code = (\d+)', report.stdout)
    if exit_code:
        return f'خدمة توزيع العناوين أُغلقت برمز {exit_code.group(1)}.'
    return 'خدمة توزيع العناوين لم تبدأ؛ اضغط فحص الاتصال بعد بضع ثوانٍ.'


def wired_service(config_path=DEFAULT_CONFIG):
    service_order = command('networksetup', '-listnetworkserviceorder', timeout=3)
    if service_order.returncode:
        raise OSError('Ethernet service inspection failed')
    services = service_order.stdout
    try:
        configured = config_options(read_snapshot(config_path)).get('interface', [])
        configured_device = configured[0] if len(configured) == 1 else ''
    except (OSError, ValueError, UnicodeError):
        configured_device = ''
    candidates = []
    for match in re.finditer(r'^\(\d+\) ([^\n]+)\n\(Hardware Port: ([^\n]+), Device: (en\d+)\)$',
                             services, re.MULTILINE):
        name, port, device = match.groups()
        if name.startswith('*') or re.search(r'wi-?fi|airport|iphone|bluetooth|bridge',
                                               f'{name} {port}', re.IGNORECASE):
            continue
        interface = command('ifconfig', device, timeout=3)
        runtime_addresses = []
        for address, mask in re.findall(r'^\s*inet ([0-9.]+) netmask ([0-9a-fA-Fx.]+)', interface.stdout, re.MULTILINE):
            try:
                runtime_addresses.append({'ip': str(ipaddress.IPv4Address(address)),
                                          'mask': str(ipaddress.IPv4Address(int(mask, 16) if mask.lower().startswith('0x') else mask))})
            except ValueError:
                continue
        exam_address_ready = bool(re.search(r'^\s*inet 10\.77\.0\.1 netmask (?:0xfffffc00|255\.255\.252\.0)(?:\s|$)',
                                            interface.stdout, re.MULTILINE | re.IGNORECASE))
        candidates.append({'active': interface.returncode == 0 and 'status: active' in interface.stdout,
                           'service': name, 'device': device,
                           'addresses': re.findall(r'^\s*inet ([0-9.]+) ', interface.stdout, re.MULTILINE),
                           'runtimeAddresses': runtime_addresses,
                           'examAddressReady': interface.returncode == 0 and exam_address_ready,
                           'inspectionFailed': interface.returncode != 0})
    if not candidates:
        return None
    active_candidates = [item for item in candidates if item['active']]
    if len(active_candidates) > 1:
        return {'service': '', 'device': '', 'active': False, 'ambiguous': True,
                'configuredDevice': configured_device, 'candidates': candidates}
    configured_candidate = next((item for item in candidates if item['device'] == configured_device), candidates[0])
    selected = active_candidates[0] if active_candidates else configured_candidate
    if selected['inspectionFailed']:
        raise OSError('Ethernet interface inspection failed')
    return {**selected, 'ambiguous': False, 'configuredDevice': configured_device, 'candidates': candidates}


class NoPortalRedirect(request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None


class NetworkMaintenanceConflict(ValueError):
    """The local exam server cannot safely reserve a network change."""


def student_service_ready(student_url):
    opener = request.build_opener(request.ProxyHandler({}), NoPortalRedirect())
    try:
        with opener.open(parse.urljoin(student_url, '/api/lounge'), timeout=3) as response:
            lounge = json.loads(response.read(262144))
            return (response.status == 200 and isinstance(lounge, dict)
                    and isinstance(lounge.get('rooms'), list)
                    and isinstance(lounge.get('serverTime'), (int, float)))
    except error.HTTPError as response:
        response.close()
        return False
    except (OSError, error.URLError, json.JSONDecodeError, UnicodeDecodeError):
        return False


def check_android_portal(student_url=f'http://{EXAM_IP}/', mode=None):
    """Check the applied HTTP response and exam service, not handset Internet validation."""
    hostname = 'connectivitycheck.gstatic.com'
    ip = parse.urlsplit(student_url).hostname
    try:
        dns = command('/usr/bin/dig', '+short', '+time=2', '+tries=1',
                      f'@{ip}', hostname, 'A', timeout=4)
        addresses = [line.strip() for line in dns.stdout.splitlines()
                     if re.fullmatch(r'(?:[0-9]{1,3}\.){3}[0-9]{1,3}', line.strip())]
        dns_ready = dns.returncode == 0 and ip in addresses
    except (OSError, subprocess.TimeoutExpired):
        dns_ready = False
    redirect_ready = False
    connectivity_ready = False
    opener = request.build_opener(request.ProxyHandler({}), NoPortalRedirect())
    probe = request.Request(parse.urljoin(student_url, '/generate_204'),
                            headers={'Host': hostname})
    try:
        with opener.open(probe, timeout=3) as response:
            connectivity_ready = response.status == 204 and response.read(1) == b''
    except error.HTTPError as response:
        destination = parse.urljoin(student_url, '/welcome') if mode == 'welcome' else student_url
        redirect_ready = response.code == 302 and response.headers.get('Location') == destination
        response.close()
    except (OSError, error.URLError):
        pass
    student_ready = student_service_ready(student_url)
    if not dns_ready:
        detail = 'فحص Android: اسم فحص الدخول لا يصل إلى عنوان الماك عبر DNS.'
    elif mode == 'direct' and not connectivity_ready:
        detail = 'الدخول بدون نافذة مختار، لكن فحص HTTP لا يطابقه. حدّث تجهيز الماك بعد إغلاق القاعات.'
    elif mode == 'welcome' and not (redirect_ready or connectivity_ready):
        detail = 'بوابة الترحيب لا تطابق الإعداد. حدّث تجهيز الماك بعد إغلاق القاعات.'
    elif mode not in ('direct', 'welcome') and not redirect_ready:
        detail = 'فحص Android: خدمة الماك لم تُرجع تحويلًا لصفحة الامتحان.'
    elif not student_ready:
        detail = 'فحص الاتصال يعمل، لكن خدمة الامتحان لا تستجيب. تأكد أن التطبيق مفتوح وراجع سجل التشغيل.'
    elif mode == 'direct':
        detail = 'فحص HTTP بدون تحويل وصفحة الامتحان يستجيبان. هذا فحص محلي؛ علامة الإنترنت وبقاء الموبايل على الشبكة يحتاجان تجربة فعلية.'
    else:
        detail = 'تحويل الدخول وخدمة الامتحان يستجيبان على الماك. ظهور النافذة على الموبايل يحتاج تجربة فعلية.'
    expected_response = (connectivity_ready if mode == 'direct' else
                         redirect_ready or connectivity_ready if mode == 'welcome' else redirect_ready)
    return {'ready': dns_ready and expected_response and student_ready, 'dnsReady': dns_ready,
            'httpRedirectReady': redirect_ready, 'httpConnectivityReady': connectivity_ready,
            'studentServiceReady': student_ready, 'detail': detail}


def diagnostic_tail(path, limit=131072):
    try:
        with path.open('rb') as source:
            source.seek(0, 2)
            size = source.tell()
            source.seek(max(0, size-limit))
            data = source.read(limit)
        if size > limit:
            data = data.split(b'\n', 1)[-1]
        return data.decode('utf-8', errors='replace')
    except FileNotFoundError:
        return ''


def diagnostic_log(path, limit):
    try:
        metadata = path.stat()
        return diagnostic_tail(path, limit), {'name': path.name, 'status': 'ok',
                    'modifiedAt': utc_time(metadata.st_mtime), 'sizeBytes': metadata.st_size,
                    'tailLimited': metadata.st_size > limit}
    except FileNotFoundError:
        return '', {'name': path.name, 'status': 'missing'}
    except OSError:
        return '', {'name': path.name, 'status': 'unavailable'}


class NetworkSetupApi:
    def __init__(self, sleep_protection=None, exam_downloads=None, portal_settings=None, admin_port=8766, timeline=None, omada_monitor=None):
        self._sleep_protection = sleep_protection
        self._exam_downloads = exam_downloads
        self._portal_settings = portal_settings
        self._admin_port = admin_port
        self._timeline = timeline
        self._omada_monitor = omada_monitor

    def configure_omada_monitor(self, host, username, password):
        if self._omada_monitor is None:
            return {'ok': False, 'message': 'افتح الربط داخل نسخة الماك الأحدث.'}
        from omada_monitor import MonitorFailure
        try:
            status = self._omada_monitor.configure(host, username, password)
        except MonitorFailure as failure:
            messages = {'auth_required': 'لم يقبل Omada بيانات الدخول؛ راجع اسم المستخدم وكلمة السر.',
                        'unsupported': 'صفحة الجهاز أو إصدارها غير مدعوم؛ لم يُحفظ الربط.',
                        'certificate_changed': 'تغيرت شهادة الجهاز أثناء الربط؛ افحص الاتصال ثم أعد المحاولة.'}
            return {'ok': False, 'message': messages.get(failure.state, 'تعذر قراءة الجهاز؛ لم يُحفظ الربط.')}
        except (OSError, ValueError, HTTPException):
            return {'ok': False, 'message': 'تعذر الربط؛ راجع العنوان وبيانات الدخول واتصال الكابل. لم تُغيّر إعدادات Omada.'}
        return {'ok': True, 'message': 'تم حفظ الربط على هذا الماك. الإحصائيات تُقرأ في الخلفية كل ٣٠ ثانية.', 'status': status}

    def download_exam_file(self, path, filename, expected_type):
        if self._exam_downloads is None:
            raise RuntimeError('خدمة تنزيل الملفات غير جاهزة؛ أعد فتح البرنامج.')
        return self._exam_downloads.download_exam_file(path, filename, expected_type)

    def select_portal_certificate(self, hostname):
        if self._portal_settings is None:
            raise RuntimeError("افتح هذه الخطوة داخل النسخة الأحدث من برنامج الماك.")
        return self._portal_settings.import_certificate(hostname)

    def select_portal_mode(self, mode):
        if self._portal_settings is None:
            raise RuntimeError("افتح هذه الخطوة داخل النسخة الأحدث من برنامج الماك.")
        return self._portal_settings.select_mode(mode)

    def exam_health(self):
        network = self.network_status()
        network['sleepProtection'] = self._sleep_protection.status() if self._sleep_protection else {'active': False}
        return network

    def network_diagnostics(self):
        directory = PORTAL_DIAGNOSTIC_DIRECTORY
        events, files, discarded = [], [], 0
        for name in ('portal-events.jsonl.1', 'portal-events.jsonl'):
            contents, metadata = diagnostic_log(directory / name, 262144)
            files.append(metadata)
            for line in contents.splitlines():
                try:
                    event = portal_event(json.loads(line))
                except (ValueError, UnicodeError):
                    event = None
                if event is None:
                    discarded += 1
                    continue
                events.append(event)
        dhcp_log, dhcp_meta = diagnostic_log(directory / 'dnsmasq.log', 32768)
        dhcp_errors, error_meta = diagnostic_log(directory / 'dnsmasq-error.log', 32768)
        current = self._timeline.current() if self._timeline else {'ready': False, 'inspectionError': True}
        snapshot = safe_state(current)
        for key in ('sampledAt', 'sampleAgeSeconds'):
            if key in current:
                snapshot[key] = current[key]
        opener = request.build_opener(request.ProxyHandler({}), NoPortalRedirect())
        backend = read_backend_diagnostics(self._admin_port, self._timeline.directory.parent if self._timeline else None, opener)
        report = {'schemaVersion': 2, 'capturedAt': utc_time(), **app_release(),
                'events': events, 'networkStatus': snapshot,
                'loggingInstalled': any(entry['status'] == 'ok' for entry in files),
                'portalLogs': {'files': files, 'discardedRecords': discarded},
                'dhcpLog': sanitized_dhcp_log(dhcp_log), 'dhcpErrors': sanitized_dhcp_log(dhcp_errors),
                'dhcpLogs': {'files': [dhcp_meta, error_meta], 'historical': True},
                'networkTimeline': self._timeline.history() if self._timeline else
                    {'schemaVersion': 1, 'status': 'unavailable', 'recording': {'active': False}, 'events': []},
                'backendDiagnostics': backend}
        report['status'] = 'ok' if report_complete(report) else 'partial'
        return report

    def export_network_diagnostics(self):
        return self._save_network_report(self.network_diagnostics())

    def capture_network_incident(self):
        marker = self.mark_network_incident()
        report = self.network_diagnostics()
        report['incident'] = {'time': marker['capturedAt'], 'markerSaved': marker['ok']}
        if not marker['ok']:
            report['status'] = 'partial'
        saved = self._save_network_report(report)
        saved.update(capturedAt=marker['capturedAt'], markerSaved=marker['ok'])
        if not marker['ok']:
            saved['message'] = 'تعذر حفظ علامة الوقت في السجل؛ ' + saved['message']
        elif not saved['ok']:
            saved['message'] = 'تم تسجيل وقت المشكلة، لكن ' + saved['message']
        return saved

    def _save_network_report(self, report):
        destination = Path.home() / 'Downloads' / ('Massar-Network-' + time.strftime('%Y%m%d-%H%M%S') + '-' + uuid.uuid4().hex[:6] + '.json')
        try:
            destination.parent.mkdir(parents=True, exist_ok=True)
            write_download(io.BytesIO((json.dumps(report, ensure_ascii=False, indent=2) + '\n').encode()), destination)
        except OSError:
            return {'ok': False, 'status': 'failed', 'message': 'تعذر حفظ سجل التشخيص. راجع المساحة المتاحة وصلاحية مجلد التنزيلات؛ الامتحان مستمر.'}
        complete = report.get('status') == 'ok'
        return {'ok': True, 'status': 'ok' if complete else 'partial', 'path': str(destination),
                'message': 'تم حفظ سجل التشخيص في التنزيلات' + ('' if complete else ' مع توضيح الأجزاء غير المتاحة') + ': ' + destination.name}

    def mark_network_incident(self):
        stamp = self._timeline.record('user_marker', state=self._timeline.current()) if self._timeline else None
        return {'ok': bool(stamp), 'capturedAt': stamp or utc_time(),
                'message': 'تم تسجيل وقت المشكلة. صدّر سجل التشخيص بعد ذلك.' if stamp else
                           'تعذر حفظ العلامة؛ راجع مساحة التخزين ثم صدّر السجل المتاح.'}

    def network_status(self):
        return self._timeline.current() if self._timeline else self._collect_network_status()

    def refresh_network_status(self):
        return self._timeline.capture() if self._timeline else self._collect_network_status()

    def _collect_network_status(self):
        try:
            snapshot = self._network_status()
            snapshot['sleepProtection'] = self._sleep_protection.status() if self._sleep_protection else {'active': False}
        except (OSError, subprocess.SubprocessError, UnicodeError):
            snapshot = {'supported': sys.platform == 'darwin', 'ready': False, 'inspectionError': True,
                    'message': 'تعذر إكمال فحص الشبكة على macOS. لم يتم تغيير الإعداد؛ ثبّت الهَب والكابل ثم أعد الفحص.',
                    'dhcpDetail': 'لم يمكن التحقق من حالة منفذ Ethernet وخدمات الشبكة.'}
        snapshot['omada'] = self._omada_monitor.status() if self._omada_monitor else {'state': 'not_configured'}
        return snapshot

    def _network_status(self):
        if sys.platform != 'darwin':
            return {'supported': False, 'platform': sys.platform,
                    'message': 'تهيئة الشبكة المباشرة متاحة على الماك حاليًا. تجهيز ويندوز قيد الإعداد.'}
        wired = wired_service(DEFAULT_CONFIG)
        info = ''
        if wired and not wired['ambiguous']:
            interface_config = command('networksetup', '-getinfo', wired['service'], timeout=3)
            if interface_config.returncode:
                raise OSError('Ethernet configuration inspection failed')
            info = interface_config.stdout
        match = re.search(r'^IP address: (.+)$', info, re.MULTILINE)
        ethernet_ip = match.group(1) if match else ''
        mask = re.search(r'^Subnet mask: (.+)$', info, re.MULTILINE)
        ethernet_mask = mask.group(1) if mask else ''
        dhcp_running = service_running('com.massar.examroom.dhcp')
        portal_running = service_running('com.massar.examroom.portal')
        installed_config = DEFAULT_CONFIG
        config_check = (command(str(bundle_path() / 'dnsmasq'), '--test',
                                f'--conf-file={installed_config}', timeout=3)
                        if installed_config.is_file() and (bundle_path() / 'dnsmasq').is_file() else None)
        leases = inspect_dhcp_leases(installed_config)
        dhcp_configuration_ready = bool(config_check and config_check.returncode == 0
                                        and leases['configVerified'] and wired
                                        and leases.get('interfaces') == [wired['device']]
                                        and ethernet_mask == '255.255.252.0')
        dhcp_socket = inspect_dhcp_socket(command) if dhcp_running else {'ready': False, 'listeners': []}
        ethernet_address_ready = bool(wired and wired.get('examAddressReady'))
        ethernet_changed = bool(wired and wired['active'] and len(leases.get('interfaces', [])) == 1
                                and leases['interfaces'] != [wired['device']])
        ethernet_detail = ''
        if wired and wired['ambiguous']:
            ethernet_detail = 'يوجد أكثر من كابل Ethernet نشط. اترك كابل Omada وحده، واستخدم Wi-Fi للإنترنت، ثم افحص الاتصال.'
        elif not wired or not wired['active']:
            ethernet_detail = 'وصلة Ethernet غير نشطة. ثبّت الهَب وكابل Omada وانتظر إضاءة منفذ الكابل، ثم افحص الاتصال. لا يلزم تغيير الإعداد لمجرد فصل الكابل.'
        elif ethernet_changed:
            ethernet_detail = f'اتغيّر منفذ Ethernet المتصل إلى {wired["device"]}. أغلق القاعات ثم حدّث تجهيز الماك لربط توزيع العناوين بالمحوّل المتصل.'
        elif ethernet_ip != EXAM_IP or not ethernet_address_ready:
            ethernet_detail = 'عنوان شبكة الامتحان 10.77.0.1/22 غير جاهز على منفذ الكابل. ثبّت الهَب وكابل Omada ثم افحص الاتصال؛ إذا استمرت المشكلة أغلق القاعات وحدّث تجهيز الماك.'
        if ethernet_detail:
            dhcp_detail = ethernet_detail
        elif not dhcp_running:
            dhcp_detail = dhcp_failure_detail()
        elif not dhcp_configuration_ready:
            dhcp_detail = 'إعداد DHCP أو منفذ الكابل أو قناع الشبكة لا يطابق 10.77.0.0/22. راجع سجل الشبكة وحدّث تجهيز الماك بعد إغلاق القاعات.'
        else:
            dhcp_detail = dhcp_socket['detail']
        local_ready = bool(wired and wired['active']) and ethernet_address_ready and ethernet_ip == EXAM_IP and dhcp_running and portal_running
        portal_status = self._portal_settings.status() if self._portal_settings else {'state': 'unconfigured'}
        android_probe = check_android_portal(mode=portal_status.get('appliedMode')) if local_ready else None
        return {
            'supported': True,
            'ready': local_ready and dhcp_configuration_ready and dhcp_socket['ready'] is True
                     and bool(android_probe and android_probe['ready']),
            'androidProbe': android_probe,
            'radioSettingsVerified': False,
            'dhcpLeases': leases,
            'capport': portal_status,
            'ethernetActive': wired['active'] if wired else False,
            'ethernetAmbiguous': wired['ambiguous'] if wired else False,
            'ethernetService': wired['service'] if wired else '',
            'ethernetDevice': wired['device'] if wired else '',
            'configuredEthernetDevice': leases.get('interfaces', []),
            'ethernetCandidates': wired.get('candidates', []) if wired else [],
            'ethernetRuntimeAddresses': wired.get('runtimeAddresses', []) if wired else [],
            'ethernetAddressReady': ethernet_address_ready,
            'ethernetDeviceChanged': ethernet_changed,
            'ethernetDetail': ethernet_detail,
            'ethernetIp': ethernet_ip,
            'ethernetMask': ethernet_mask,
            'dhcpRunning': dhcp_running,
            'dhcpConfigurationReady': dhcp_configuration_ready,
            'dhcpSocketReady': dhcp_socket['ready'],
            'dhcpListeners': dhcp_socket['listeners'],
            'dhcpDetail': dhcp_detail,
            'configValid': config_check.returncode == 0 if config_check else None,
            'configDetail': (config_check.stderr or config_check.stdout).strip()[-280:] if config_check else '',
            'portalRunning': portal_running,
            'dnsmasqAvailable': (bundle_path() / 'dnsmasq').is_file(),
            'studentUrl': f'http://{EXAM_IP}/',
            'apIp': '10.77.0.2',
        }

    def _network_maintenance(self, action, admin_token='', lease_token=''):
        opener = request.build_opener(request.ProxyHandler({}), NoPortalRedirect())
        origin = f'http://127.0.0.1:{self._admin_port}'
        try:
            if not admin_token:
                with opener.open(origin + '/api/bootstrap', timeout=3) as response:
                    admin_token = json.load(response)['token']
            authenticated = request.Request(origin + '/api/network/maintenance/' + action,
                                            data=json.dumps({'leaseToken': lease_token}).encode(),
                                            headers={'X-Admin-Token': admin_token, 'X-Exam-Request': '1',
                                                     'Content-Type': 'application/json'})
            with opener.open(authenticated, timeout=3) as response:
                return admin_token, json.load(response)
        except error.HTTPError as response:
            with response:
                if response.code == 409:
                    detail = json.loads(response.read(8192)).get('error')
                    if isinstance(detail, str) and detail:
                        raise NetworkMaintenanceConflict(detail)
            raise

    def prepare_network(self):
        if sys.platform != 'darwin':
            return {'ok': False, 'message': 'تهيئة الشبكة المباشرة متاحة على الماك حاليًا.'}
        try:
            wired = wired_service(DEFAULT_CONFIG)
        except (OSError, subprocess.SubprocessError):
            return {'ok': False, 'message': 'تعذر فحص كابل الشبكة. لم يتم تغيير إعدادات الشبكة.'}
        if wired and wired['ambiguous']:
            return {'ok': False, 'message': 'يوجد أكثر من كابل Ethernet نشط. اترك كابل Omada وحده، واستخدم Wi-Fi للإنترنت، ثم حاول مرة أخرى.'}
        if not wired or not wired['active']:
            return {'ok': False, 'message': 'وصّل كابل Omada بمحوّل Ethernet وتأكد أن الجهاز يعمل ثم حاول مرة أخرى.'}
        try:
            admin_token, lease = self._network_maintenance('acquire')
            lease_token, expires_at = lease['leaseToken'], lease['expiresAt']
            if (not isinstance(lease_token, str) or len(lease_token) < 32
                    or not isinstance(expires_at, (int, float))
                    or not time.time() + 240 < expires_at < time.time() + 660):
                raise ValueError('Invalid network maintenance authorization')
        except NetworkMaintenanceConflict as conflict:
            return {'ok': False, 'message': str(conflict)}
        except error.HTTPError:
            return {'ok': False, 'message': 'تعذر الحصول على تصريح تهيئة الشبكة. لم يتم تغيير الشبكة.'}
        except (OSError, ValueError, KeyError, TypeError):
            return {'ok': False, 'message': 'تعذر التأكد من القاعات. لم يتم تغيير الشبكة.'}
        result = {'ok': False, 'message': 'تعذرت تهيئة الشبكة.'}
        uncertain_completion = False
        if self._timeline:
            self._timeline.record('setup_started')
        try:
            result = self._prepare_network(expires_at, wired)
        except subprocess.TimeoutExpired:
            # macOS may keep its elevated child alive after osascript exits.
            # Keep the lease; the installer's /var/run lock also blocks rooms.
            uncertain_completion = True
            result = {'ok': False, 'message': 'انتهت مهلة تهيئة الشبكة. انتظر حتى ١٠ دقائق؛ لو استمر منع فتح القاعات، أعد تشغيل الماك ثم افحص الاتصال.'}
        except subprocess.CalledProcessError as failure:
            uncertain_completion = True
            detail = (failure.stderr or failure.stdout or '').strip()[-500:]
            result = {'ok': False, 'message': (detail or 'تعذرت تهيئة الشبكة.') + ' انتظر حتى ١٠ دقائق؛ لو استمر منع فتح القاعات، أعد تشغيل الماك ثم افحص الاتصال.'}
        except (OSError, ValueError, KeyError, TypeError, subprocess.SubprocessError):
            result = {'ok': False, 'message': 'تعذرت تهيئة الشبكة أو انتهت مهلة تصريح macOS. افحص الاتصال قبل فتح القاعات.'}
        finally:
            if self._timeline:
                self._timeline.record('setup_finished', outcome='uncertain' if uncertain_completion else 'ok' if result['ok'] else 'failed')
            if not uncertain_completion:
                try:
                    self._network_maintenance('release', admin_token, lease_token)
                except (OSError, ValueError, KeyError, TypeError):
                    result['message'] += ' تعذر إنهاء وضع تهيئة الشبكة؛ انتظر حتى ١٠ دقائق قبل فتح القاعة.'
        return result

    def _prepare_network(self, expires_at, wired):
        portal = self._portal_settings.installation() if self._portal_settings else {'mode': 'preserve'}
        source = bundle_path()
        script = source / 'mac-direct-setup.sh'
        if not script.is_file():
            return {'ok': False, 'message': 'ملفات تهيئة الشبكة غير موجودة في نسخة البرنامج الحالية.'}
        if not (source / 'dnsmasq').is_file():
            return {'ok': False, 'message': 'مكوّن توزيع العناوين غير موجود داخل نسخة البرنامج. ثبّت النسخة الأحدث.'}
        # osascript shows the normal macOS administrator prompt. The password
        # stays with macOS and never enters the web view or this Python process.
        result = command('osascript',
                         '-e', 'on run argv',
                         '-e', 'with timeout of 180 seconds',
                         '-e', 'do shell script ("/bin/sh " & quoted form of (item 1 of argv) & " " & quoted form of (item 2 of argv) & " " & quoted form of (item 3 of argv) & " " & quoted form of (item 4 of argv) & " " & quoted form of (item 5 of argv) & " " & quoted form of (item 6 of argv) & " " & quoted form of (item 7 of argv)) with administrator privileges',
                         '-e', 'end timeout',
                         '-e', 'end run', str(script), wired['service'], wired['device'], portal.get('hostname', ''), portal.get('directory', ''), str(int(expires_at)), portal['mode'], timeout=185)
        if '(-1712)' in (result.stderr or ''):
            raise subprocess.TimeoutExpired('osascript', 180)
        if result.returncode:
            message = (result.stderr or result.stdout).strip()
            if 'User canceled' in message or '(-128)' in message:
                return {'ok': False, 'message': 'أُلغيت نافذة تصريح macOS؛ لم تكتمل التهيئة.'}
            # An AppleEvent error need not mean that the elevated installer has
            # stopped. Do not release its lease on an unconfirmed completion.
            result.check_returncode()
        verification_deadline = time.monotonic() + 10
        for _ in range(10):
            state = self.refresh_network_status()
            if state.get('inspectionError'):
                return {'ok': False, 'message': state['message'], 'status': state}
            if (state['ready'] or not state['ethernetActive'] or state.get('ethernetDeviceChanged')
                    or not state.get('ethernetAddressReady') or not state.get('dhcpConfigurationReady')
                    or time.monotonic() >= verification_deadline):
                break
            time.sleep(1)
        if state['ready']:
            message = 'تم تجهيز الماك لشبكة الامتحان المباشرة.'
        elif not state['ethernetActive']:
            message = 'خدمات الماك مجهزة، لكن كابل Omada غير متصل. وصّل الكابل ثم افحص الاتصال.'
        elif state.get('ethernetDeviceChanged') or not state.get('ethernetAddressReady'):
            message = state['dhcpDetail']
        elif not state['dhcpRunning']:
            message = f'توزيع العناوين لم يبدأ: {state["dhcpDetail"]}'
        elif not state.get('dhcpConfigurationReady') or state.get('dhcpSocketReady') is not True:
            message = state['dhcpDetail']
        elif state.get('androidProbe'):
            message = state['androidProbe']['detail']
        else:
            message = 'خدمة صفحة الطلاب لم تبدأ بعد. اضغط فحص الاتصال.'
        return {'ok': state['ready'], 'message': message, 'status': state}
