"""Bounded, identity-free network history collected while the desktop app is open."""
import copy
from datetime import datetime, timezone
import errno
import ipaddress
import json
import os
from pathlib import Path
import re
import shutil
import threading
import time
import uuid
from omada_monitor import safe_snapshot as safe_omada_snapshot


SAMPLE_SECONDS = 10
HEARTBEAT_SECONDS = 60
MAX_FILE_BYTES = 512 * 1024
BACKUP_FILES = 3
EVENT_KINDS = {'app_start', 'app_end', 'previous_run_incomplete', 'state_changed', 'heartbeat',
               'sampling_gap', 'sample_failed', 'recording_resumed', 'setup_started', 'setup_finished', 'user_marker'}


def utc_time(seconds=None):
    return datetime.fromtimestamp(time.time() if seconds is None else seconds, timezone.utc).isoformat(timespec='milliseconds').replace('+00:00', 'Z')


def numeric(value):
    return isinstance(value, (int, float)) and not isinstance(value, bool) and 0 <= value < 10**18


def safe_state(snapshot):
    """An allowlist prevents future UI details or command errors leaking into history."""
    state = {name: snapshot[name] for name in (
        'supported', 'ready', 'inspectionError', 'ethernetActive', 'ethernetAmbiguous',
        'ethernetAddressReady', 'ethernetDeviceChanged', 'dhcpRunning', 'dhcpConfigurationReady',
        'dhcpSocketReady', 'configValid', 'portalRunning') if isinstance(snapshot.get(name), bool) or snapshot.get(name, '') is None}
    device = snapshot.get('ethernetDevice', '')
    if isinstance(device, str) and re.fullmatch(r'en[0-9]+', device):
        state['ethernetDevice'] = device
    state['ethernetRuntimeAddresses'] = []
    addresses = snapshot.get('ethernetRuntimeAddresses')
    for address in addresses[:8] if isinstance(addresses, list) else []:
        try:
            state['ethernetRuntimeAddresses'].append({'ip': str(ipaddress.IPv4Address(address['ip'])),
                                                      'mask': str(ipaddress.IPv4Address(address['mask']))})
        except (KeyError, TypeError, ValueError):
            continue
    for field, keys in (
        ('androidProbe', ('ready', 'dnsReady', 'httpRedirectReady', 'httpConnectivityReady', 'studentServiceReady')),
        ('sleepProtection', ('supported', 'active'))):
        section = snapshot.get(field)
        section = section if isinstance(section, dict) else {}
        state[field] = {key: section[key] for key in keys if isinstance(section.get(key), bool)}
    leases = snapshot.get('dhcpLeases')
    leases = leases if isinstance(leases, dict) else {}
    state['dhcpLeases'] = {key: leases[key] for key in (
        'dhcpLeaseCount', 'capacity', 'percent', 'outsideRangeLeaseCount', 'malformedRecordCount')
        if numeric(leases.get(key)) or leases.get(key, '') is None}
    for key in ('configVerified', 'leasesAvailable', 'leaseDataComplete'):
        if isinstance(leases.get(key), bool):
            state['dhcpLeases'][key] = leases[key]
    free_bytes = snapshot.get('diskFreeBytes')
    state['diskFreeBytes'] = int(free_bytes) if numeric(free_bytes) else None
    if isinstance(snapshot.get('omada'), dict):
        state['omada'] = safe_omada_snapshot(snapshot['omada'])
    return state


def safe_event(candidate):
    if not isinstance(candidate, dict) or not isinstance(candidate.get('kind'), str) or candidate['kind'] not in EVENT_KINDS:
        return None
    if not isinstance(candidate.get('runId'), str) or not re.fullmatch(r'[0-9a-f]{32}', candidate['runId']):
        return None
    stamp = candidate.get('time', '')
    if not isinstance(stamp, str) or not re.fullmatch(r'[0-9T:Z.\-]{20,30}', stamp):
        return None
    try:
        datetime.fromisoformat(stamp.replace('Z', '+00:00'))
    except ValueError:
        return None
    event = {'kind': candidate['kind'], 'time': stamp, 'runId': candidate['runId']}
    for key in ('elapsedSeconds', 'durationMs', 'wallGapSeconds', 'monotonicGapSeconds', 'missedEvents'):
        if numeric(candidate.get(key)):
            event[key] = candidate[key]
    if isinstance(candidate.get('state'), dict):
        event['state'] = safe_state(candidate['state'])
    if candidate.get('outcome') in ('ok', 'failed', 'uncertain', 'startup_failed', 'normal'):
        event['outcome'] = candidate['outcome']
    return event


class NetworkTimeline:
    def __init__(self, directory):
        self.directory = Path(directory) / 'network-diagnostics'
        self.path = self.directory / 'network-timeline.jsonl'
        self.run_id = uuid.uuid4().hex
        self.started = time.monotonic()
        self._sample_lock = threading.Lock()
        self._state_lock = threading.Lock()
        self._io_lock = threading.Lock()
        self._stop = threading.Event()
        self._thread = None
        self._collect = None
        self._snapshot = None
        self._sample_wall = None
        self._sample_monotonic = None
        self._last_state = None
        self._last_recorded = None
        self._last_write = None
        self._write_failures = 0
        self._missed_events = 0
        self._last_error = None
        previous = self.history()
        if previous['events'] and previous['events'][-1]['kind'] != 'app_end':
            self.record('previous_run_incomplete')
        self.record('app_start')

    def record(self, kind, **fields):
        event = safe_event({'kind': kind, 'time': utc_time(), 'runId': self.run_id,
                            'elapsedSeconds': round(time.monotonic() - self.started, 3), **fields})
        if event is None:
            return None
        with self._io_lock:
            if self._stop.is_set() and kind != 'app_end':
                return None
            try:
                self.directory.mkdir(parents=True, exist_ok=True)
                batch = []
                if self._missed_events:
                    batch.append({**event, 'kind': 'recording_resumed', 'missedEvents': self._missed_events})
                batch.append(event)
                encoded = ''.join(json.dumps(item, separators=(',', ':'), ensure_ascii=True) + '\n' for item in batch).encode()
                if self.path.exists() and self.path.stat().st_size + len(encoded) > MAX_FILE_BYTES:
                    self.path.with_suffix('.jsonl.' + str(BACKUP_FILES)).unlink(missing_ok=True)
                    for index in range(BACKUP_FILES - 1, 0, -1):
                        older = self.path.with_suffix('.jsonl.' + str(index))
                        if older.exists():
                            older.replace(self.path.with_suffix('.jsonl.' + str(index + 1)))
                    self.path.replace(self.path.with_suffix('.jsonl.1'))
                descriptor = os.open(self.path, os.O_WRONLY | os.O_APPEND | os.O_CREAT | getattr(os, 'O_NOFOLLOW', 0), 0o600)
                with os.fdopen(descriptor, 'ab') as destination:
                    destination.write(encoded)
                self._last_write = event['time']
                self._last_error = None
                self._missed_events = 0
                return event['time']
            except OSError as failure:
                self._write_failures += 1
                self._missed_events += 1
                self._last_error = ('disk_full' if failure.errno in (errno.ENOSPC, errno.EDQUOT) else
                                    'permission_denied' if failure.errno in (errno.EACCES, errno.EPERM) else 'io_error')
                return None

    def start(self, collect):
        self._collect = collect
        self._thread = threading.Thread(target=self._run, name='network-diagnostics', daemon=True)
        self._thread.start()

    def _run(self):
        while not self._stop.is_set():
            started = time.monotonic()
            self.capture()
            self._stop.wait(max(0.1, SAMPLE_SECONDS - (time.monotonic() - started)))

    def capture(self):
        if self._collect is None or not self._sample_lock.acquire(timeout=20):
            return {'ready': False, 'inspectionError': True, 'message': 'فحص الشبكة جارٍ؛ انتظر قليلًا ثم أعد الفحص.'}
        try:
            started, wall = time.monotonic(), time.time()
            try:
                snapshot = self._collect()
                if not isinstance(snapshot, dict):
                    raise ValueError('invalid diagnostic snapshot')
            except Exception:
                # This boundary owns diagnostics only. A broken probe must not
                # stop future samples or affect the exam/UI that it observes.
                snapshot = {'ready': False, 'inspectionError': True,
                            'message': 'تعذر جمع حالة الشبكة؛ ستتم إعادة الفحص تلقائيًا.'}
                self.record('sample_failed')
            if self._stop.is_set():
                return self.current()
            try:
                snapshot['diskFreeBytes'] = shutil.disk_usage(self.directory.parent).free
            except OSError:
                snapshot['diskFreeBytes'] = None
            duration = round((time.monotonic() - started) * 1000)
            clean = safe_state(snapshot)
            if self._sample_wall is not None:
                wall_gap, monotonic_gap = wall - self._sample_wall, started - self._sample_monotonic
                if max(wall_gap, monotonic_gap) > SAMPLE_SECONDS * 3:
                    self.record('sampling_gap', wallGapSeconds=max(0, round(wall_gap, 3)),
                                monotonicGapSeconds=max(0, round(monotonic_gap, 3)))
            # Small disk/lease-count changes belong in heartbeats; link/service
            # transitions are recorded immediately without writing every sample.
            comparable = {key: value for key, value in clean.items() if key not in ('diskFreeBytes', 'dhcpLeases')}
            if comparable != self._last_state or self._last_recorded is None or started - self._last_recorded >= HEARTBEAT_SECONDS:
                self.record('state_changed' if comparable != self._last_state else 'heartbeat', state=clean, durationMs=duration)
                self._last_recorded, self._last_state = started, comparable
            with self._state_lock:
                self._snapshot = copy.deepcopy(snapshot)
                self._sample_wall, self._sample_monotonic = wall, started
            return self.current()
        finally:
            self._sample_lock.release()

    def current(self):
        with self._state_lock:
            if self._snapshot is None:
                return {'supported': True, 'ready': False, 'inspectionError': True,
                        'message': 'الفحص الأول للشبكة جارٍ؛ ستظهر النتيجة خلال لحظات.'}
            snapshot = copy.deepcopy(self._snapshot)
            snapshot['sampledAt'] = utc_time(self._sample_wall)
            snapshot['sampleAgeSeconds'] = round(max(0, time.time() - self._sample_wall,
                                                     time.monotonic() - self._sample_monotonic), 1)
            if snapshot['sampleAgeSeconds'] > SAMPLE_SECONDS * 3:
                snapshot.update(ready=False, inspectionError=True, message='نتيجة فحص الشبكة قديمة؛ تعذر تأكيد الحالة الحالية. أعد الفحص.')
            return snapshot

    def history(self):
        events, unreadable, malformed, available = [], 0, 0, 0
        with self._io_lock:
            for suffix in [f'.{index}' for index in range(BACKUP_FILES, 0, -1)] + ['']:
                try:
                    with Path(str(self.path) + suffix).open('rb') as source:
                        content = source.read(MAX_FILE_BYTES + 1)
                    available += 1
                except FileNotFoundError:
                    continue
                except OSError:
                    unreadable += 1
                    continue
                if len(content) > MAX_FILE_BYTES:
                    unreadable += 1
                    content = content[:MAX_FILE_BYTES].rsplit(b'\n', 1)[0]
                for line in content.splitlines():
                    try:
                        event = safe_event(json.loads(line))
                    except (ValueError, UnicodeError):
                        event = None
                    if event is None:
                        malformed += 1
                    else:
                        events.append(event)
            return {'schemaVersion': 1, 'status': ('unavailable' if not available else
                    'partial' if unreadable or malformed or self._missed_events else 'ok'),
                    'recording': {'active': bool(self._thread and self._thread.is_alive() and not self._stop.is_set()),
                                  'lastSampleAt': utc_time(self._sample_wall) if self._sample_wall else None,
                                  'lastWriteAt': self._last_write, 'writeFailureCount': self._write_failures,
                                  'lastErrorCode': self._last_error},
                    'retention': {'maxFileBytes': MAX_FILE_BYTES, 'backupFiles': BACKUP_FILES},
                    'unreadableFiles': unreadable, 'malformedRecords': malformed, 'events': events}

    def stop(self, outcome='normal'):
        if self._stop.is_set():
            return
        self._stop.set()
        # A stuck diagnostic disk must never delay terminating the exam server.
        # New samples cannot write after stop; this last write is best effort.
        ending = threading.Thread(target=self.record, args=('app_end',), kwargs={'outcome': outcome},
                                  name='network-diagnostics-end', daemon=True)
        ending.start()
        ending.join(timeout=1)
