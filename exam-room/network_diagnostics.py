"""Read the offline DHCP configuration and lease snapshot without changing the network."""
import ipaddress
from pathlib import Path
import re
import subprocess
import time


DEFAULT_CONFIG = Path('/usr/local/etc/massar-exam-dnsmasq.conf')
EXAM_IP = ipaddress.IPv4Address('10.77.0.1')
EXAM_SUBNET = ipaddress.IPv4Network('10.77.0.0/22')
LEASE_WARNING_PERCENT = 80
READ_LIMIT = 2 * 1024 * 1024


def finding(code, severity, detail):
    return {'code': code, 'severity': severity, 'detail': detail}


def read_snapshot(path):
    with Path(path).open('rb') as source:
        contents = source.read(READ_LIMIT + 1)
    if len(contents) > READ_LIMIT:
        raise ValueError('snapshot too large')
    return contents.decode('utf-8')


def config_options(contents):
    options = {}
    for raw_line in contents.splitlines():
        line = raw_line.strip()
        if not line or line.startswith('#'):
            continue
        line = re.split(r'\s+#', line, maxsplit=1)[0].strip()
        name, _, setting = line.partition('=')
        options.setdefault(name.strip(), []).append(setting.strip())
    return options


def lease_seconds(setting):
    if setting == 'infinite':
        return None
    matched = re.fullmatch(r'([0-9]+)([smhdw]?)', setting)
    if not matched:
        raise ValueError('unsupported lease duration')
    seconds = int(matched[1]) * {'': 1, 's': 1, 'm': 60, 'h': 3600, 'd': 86400, 'w': 604800}[matched[2]]
    if seconds < 120:
        raise ValueError('lease shorter than dnsmasq minimum')
    return seconds


def dhcp_pool(options):
    ranges = options.get('dhcp-range', [])
    if len(ranges) != 1:
        raise ValueError('one IPv4 range required')
    start, end, netmask, duration = [part.strip() for part in ranges[0].split(',')]
    first, last = ipaddress.IPv4Address(start), ipaddress.IPv4Address(end)
    subnet = ipaddress.IPv4Network(f'{start}/{netmask}', strict=False)
    if subnet != EXAM_SUBNET or first > last or last not in subnet:
        raise ValueError('unexpected subnet or range')
    if first <= EXAM_IP <= last or first == subnet.network_address or last == subnet.broadcast_address:
        raise ValueError('pool contains reserved addresses')
    limits = options.get('dhcp-lease-max', ['1000'])
    if len(limits) != 1 or not limits[0].isdigit() or int(limits[0]) < 1:
        raise ValueError('invalid lease limit')
    return {'first': first, 'last': last, 'poolCapacity': int(last) - int(first) + 1,
            'leaseSeconds': lease_seconds(duration), 'leaseLimit': int(limits[0])}


def validate_network_options(options):
    issues = []
    aliases = {'router': {'option:router', '3'}, 'dns': {'option:dns-server', '6'}}
    for kind, names in aliases.items():
        matching = []
        for setting in options.get('dhcp-option', []) + options.get('dhcp-option-force', []):
            parts = [part.strip() for part in setting.split(',')]
            if any(part in names for part in parts):
                matching.append(parts)
        if len(matching) != 1 or len(matching[0]) != 2 or matching[0][0] not in names or matching[0][1] != str(EXAM_IP):
            issues.append(finding(f'dhcp-{kind}', 'error',
                                  'إعداد بوابة الطلاب أو DNS لا يشير وحده إلى 10.77.0.1؛ حدّث تجهيز الماك بعد انتهاء الامتحان.'))
    if (options.get('address') != ['/#/10.77.0.1'] or options.get('local') != ['/#/']
            or 'no-resolv' not in options or options.get('listen-address') != [str(EXAM_IP)]):
        issues.append(finding('offline-dns', 'error', 'إعداد DNS المحلي لا يطابق شبكة الامتحان؛ يحتاج تحديث تجهيز الماك.'))
    if 'conf-file' in options or 'conf-dir' in options:
        issues.append(finding('included-config', 'error', 'توجد ملفات إعداد إضافية؛ لا يمكن تأكيد السعة والإعدادات من هذا الملف وحده.'))
    return issues


def count_leases(contents, pool, current_time):
    active_ips = set()
    invalid_records = 0
    for line in contents.splitlines():
        if not line.strip():
            continue
        parts = line.split()
        try:
            if len(parts) != 5 or not parts[0].isdigit():
                raise ValueError('invalid lease record')
            expires = int(parts[0])
            address = ipaddress.IPv4Address(parts[2])
            if not re.fullmatch(r'(?:[0-9a-fA-F]{2}-)?(?:[0-9a-fA-F]{2}:){5}[0-9a-fA-F]{2}|\*', parts[1]):
                raise ValueError('invalid hardware address')
        except ValueError:
            invalid_records += 1
            continue
        # dnsmasq records infinite leases as expiry zero; expired rows consume no current lease.
        if expires == 0 or expires > current_time:
            active_ips.add(address)
    in_pool = {address for address in active_ips if pool['first'] <= address <= pool['last']}
    return len(in_pool), len(active_ips - in_pool), invalid_records


def lease_summary(pool, counts):
    in_pool, outside_pool, invalid_records = counts
    capacity = min(pool['poolCapacity'], max(0, pool['leaseLimit'] - outside_pool))
    percent = round(100 * in_pool / capacity, 1) if capacity else 100.0
    issues = []
    if invalid_records:
        issues.append(finding('malformed-leases', 'error',
                              f'تعذر قراءة {invalid_records} سجل عنوان؛ العدد الظاهر حد أدنى ولا يمكن تأكيد السعة المتبقية.'))
    if percent >= LEASE_WARNING_PERCENT:
        issues.append(finding('lease-capacity', 'warning',
                              'عناوين الطلاب قاربت الامتلاء. انتظر انتهاء عناوين الأجهزة المغادرة قبل دخول دفعة جديدة؛ لا تمسح السجل أثناء الامتحان.'))
    return {'dhcpLeaseCount': in_pool, 'outsideRangeLeaseCount': outside_pool,
            'capacity': capacity, 'percent': None if invalid_records else percent,
            'malformedRecordCount': invalid_records, 'leaseDataComplete': not invalid_records}, issues


def inspect_dhcp_leases(config_path=DEFAULT_CONFIG, lease_path=None, now=None):
    """Inspect only this server's files; no claim is made about other DHCP servers or phones."""
    report = {'status': 'unavailable', 'configAvailable': False, 'leasesAvailable': False,
              'configVerified': False, 'leaseDataComplete': False, 'dhcpLeaseCount': None,
              'capacity': None, 'percent': None, 'singleDhcpServerVerified': False,
              'findings': [], 'detail': '',
              'scopeDetail': 'هذا فحص ملفات الماك فقط؛ لا يثبت غياب جهاز آخر يوزع العناوين أو وصول شبكة Omada إلى كل هاتف.'}
    try:
        options = config_options(read_snapshot(config_path))
        report['configAvailable'] = True
        report['interfaces'] = options.get('interface', [])
        pool = dhcp_pool(options)
        report.update({'poolCapacity': pool['poolCapacity'], 'leaseSeconds': pool['leaseSeconds'],
                       'leaseLimit': pool['leaseLimit']})
        report['findings'].extend(validate_network_options(options))
        lease_files = options.get('dhcp-leasefile', [])
        if len(lease_files) != 1 or not Path(lease_files[0]).is_absolute():
            raise ValueError('lease file is not a local absolute path')
    except OSError:
        report['detail'] = 'ملف إعداد توزيع العناوين غير متاح؛ تعذر فحص سعة الشبكة.'
        return report
    except (ValueError, UnicodeError):
        report['status'] = 'invalid'
        report['detail'] = 'ملف توزيع العناوين لا يطابق نطاق 10.77.0.0/22 أو يحتوي إعدادًا غير مقروء؛ راجع تجهيز الماك.'
        return report
    report['configVerified'] = not report['findings']
    report['capacity'] = min(pool['poolCapacity'], pool['leaseLimit'])
    return inspect_lease_file(report, pool, lease_path or lease_files[0], time.time() if now is None else now)


def inspect_lease_file(report, pool, lease_path, current_time):
    try:
        lease_text = read_snapshot(lease_path)
        report['leasesAvailable'] = True
        summary, issues = lease_summary(pool, count_leases(lease_text, pool, current_time))
        report.update(summary)
        report['findings'].extend(issues)
    except OSError:
        report['detail'] = 'سجل عناوين الطلاب غير متاح؛ هذا لا يعني أن عدد الأجهزة صفر. راجع تشغيل خدمة توزيع العناوين.'
        return report
    except (ValueError, UnicodeError):
        report['findings'].append(finding('unreadable-leases', 'error', 'سجل عناوين الطلاب غير مقروء؛ لا يمكن تأكيد السعة المتبقية.'))
    if pool['leaseSeconds'] is None or pool['leaseSeconds'] > 3600:
        report['findings'].append(finding('long-leases', 'warning',
                                         'مدة الاحتفاظ بالعناوين طويلة وقد تزاحم الدفعات التالية. حدّث تجهيز الماك بعد الامتحان لتصبح ساعة؛ الأجهزة المتصلة تجدد عناوينها.'))
    errors = any(issue['severity'] == 'error' for issue in report['findings'])
    report['status'] = 'invalid' if errors else 'warning' if report['findings'] else 'ok'
    report['detail'] = (' '.join(issue['detail'] for issue in report['findings']) if report['findings'] else
                        'سجل العناوين المحلي مقروء والسعة المتاحة كافية حاليًا. الأجهزة المتصلة تجدد عناوينها؛ العناوين المحجوزة ليست عدد المتصلين الآن.')
    return report


def inspect_dhcp_socket(run):
    """Check the IPv4 DHCP listener; this cannot prove a phone received an offer."""
    report = {'ready': None, 'listeners': [], 'detail':
              'تعذر فحص منفذ توزيع العناوين؛ حالة تشغيل الخدمة وحدها لا تؤكد وصول DHCP للطلاب.'}
    try:
        result = run('/usr/sbin/netstat', '-an', '-p', 'udp', timeout=3)
    except (OSError, subprocess.SubprocessError):
        return report
    # macOS netstat can return zero while reporting denied sysctl access.
    if result.returncode or result.stderr.strip():
        return report
    listeners = []
    for line in result.stdout.splitlines():
        parts = line.split()
        if len(parts) >= 5 and parts[0] in ('udp4', 'udp46') and parts[3].endswith('.67'):
            listeners.append(parts[3])
    ready = any(endpoint in ('*.67', '0.0.0.0.67', '10.77.0.1.67') for endpoint in listeners)
    report.update(ready=ready, listeners=sorted(set(listeners)), detail=(
        'منفذ DHCP مفتوح على الماك؛ يلزم اتصال هاتف للتأكد من استلام عنوان.' if ready else
        'خدمة توزيع العناوين لا تستقبل على منفذ DHCP للشبكة. حدّث تجهيز الماك بعد إغلاق القاعات وراجع سجل الشبكة.'))
    return report


def sanitized_dhcp_log(contents):
    """Keep DHCP activity/failures without client MAC addresses or hostnames."""
    lines = []
    for line in contents.splitlines():
        stamp = re.search(r'\b[A-Z][a-z]{2}\s+[0-9]{1,2}\s+[0-9:]{8}\b', line)
        prefix = stamp.group(0) + ' ' if stamp else ''
        packet = re.search(r'\b(DHCP(?:DISCOVER|OFFER|REQUEST|ACK|NAK|DECLINE|RELEASE|INFORM))\(([^)]+)\)', line)
        if packet:
            interface = packet.group(2) if re.fullmatch(r'(?:en|bridge)[0-9]+', packet.group(2)) else '?'
            lines.append(prefix + packet.group(1) + '(' + interface + ')')
            continue
        message = re.search(r'(?:dnsmasq(?:-dhcp)?(?:\[[0-9]+\])?:\s*)(.*)', line)
        if not message:
            continue
        detail = message.group(1)
        if not re.search(r'failed|cannot|error|no address|permission denied|address already|DHCP, IP range|started,|exiting|no servers found', detail, re.IGNORECASE):
            continue
        # Error lines can quote client hostnames, paths or identifiers. Export
        # controlled categories rather than retaining those free-form details.
        if re.search(r'(?:invalid|parse).*lease', detail, re.IGNORECASE):
            detail = 'DHCP lease file contains an unreadable record.'
        elif re.search(r"can't assign requested address|cannot assign requested address", detail, re.IGNORECASE):
            detail = "DHCP socket: Can't assign requested address."
        elif re.search(r'address already in use', detail, re.IGNORECASE):
            detail = 'DHCP socket: Address already in use.'
        elif re.search(r'permission denied', detail, re.IGNORECASE):
            detail = 'DHCP permission denied.'
        elif re.search(r'no address', detail, re.IGNORECASE):
            detail = 'DHCP has no address available for a request.'
        elif re.search(r'DHCP, IP range', detail, re.IGNORECASE):
            detail = 'DHCP address range configured.'
        elif re.search(r'started,', detail, re.IGNORECASE):
            detail = 'dnsmasq started.'
        elif re.search(r'no servers found', detail, re.IGNORECASE):
            detail = 'No upstream DNS servers; this network uses local DNS.'
        elif re.search(r'exiting', detail, re.IGNORECASE):
            detail = 'dnsmasq exiting.'
        else:
            detail = 'DHCP service reported an error.'
        lines.append(prefix + detail[:300])
    return '\n'.join(lines[-300:])
