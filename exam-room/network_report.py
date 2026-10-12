"""Typed report boundaries for persisted and authenticated backend diagnostics."""
from datetime import datetime
from http.client import HTTPException
import json
import ipaddress
from pathlib import Path
import plistlib
import sys
from urllib import error, request

from network_timeline import numeric


BODY_LIMIT = 4 * 1024 * 1024
EVENT_LIMIT = 8192
CLASSES = {'home', 'asset', 'join', 'answers', 'session', 'presence', 'lounge', 'leave',
           'client_diagnostics', 'admin_diagnostics', 'admin_api', 'report', 'other'}
KINDS = {'request', 'summary', 'startup', 'startup_failed', 'shutdown', 'maintenance', 'backup', 'startup_ready',
         'connection_lost', 'connection_restored', 'request_failed', 'js_error', 'promise_rejection'}
CATEGORIES = {'canceled', 'timeout', 'storage_full', 'storage_readonly', 'storage_io', 'descriptor_limit',
              'database_busy', 'database_corrupt', 'database_error', 'access_denied', 'not_found',
              'conflict', 'rate_limited', 'invalid_request', 'unavailable', 'internal_error',
              'write_failed', 'panic', 'address_in_use', 'address_unavailable', 'permission_denied',
              '2xx', '3xx', '4xx', '5xx', 'other'}


def allowed(value, choices):
    return isinstance(value, str) and value in choices


def timestamp(value):
    if not isinstance(value, str) or not 20 <= len(value) <= 40 or not value.endswith('Z'):
        return None
    try:
        datetime.fromisoformat(value.replace('Z', '+00:00'))
        return value
    except ValueError:
        return None


def backend_event(candidate):
    if (not isinstance(candidate, dict) or not allowed(candidate.get('kind'), KINDS)
            or candidate.get('source') not in ('backend', 'student', 'admin', 'client')
            or not timestamp(candidate.get('receivedAt'))):
        return None
    event = {key: candidate[key] for key in ('receivedAt', 'source', 'kind')}
    for field, choices in (('class', CLASSES), ('category', CATEGORIES)):
        if isinstance(candidate.get(field), str) and candidate[field] in choices:
            event[field] = candidate[field]
    for field in ('status', 'durationMs', 'count'):
        if numeric(candidate.get(field)) and (field != 'status' or candidate[field] <= 599):
            event[field] = candidate[field]
    if timestamp(candidate.get('clientTime')):
        event['clientTime'] = candidate['clientTime']
    return event


def backend_snapshot(payload):
    if (not isinstance(payload, dict) or payload.get('schemaVersion') != 1
            or not isinstance(payload.get('events'), list) or not isinstance(payload.get('available'), bool)):
        raise ValueError('invalid diagnostic schema')
    clean = {'schemaVersion': 1, 'available': payload['available'], 'clientTimeUntrusted': True,
             'capturedAt': timestamp(payload.get('capturedAt')), 'events': [], 'recorder': {}, 'summary': {'requests': []}}
    for candidate in payload['events'][-EVENT_LIMIT:]:
        event = backend_event(candidate)
        if event:
            clean['events'].append(event)
    clean['discardedEvents'] = len(payload['events']) - len(clean['events'])
    recorder = payload.get('recorder')
    if isinstance(recorder, dict):
        for field in ('queued', 'dropped', 'writeFailures', 'maxFiles', 'maxFileBytes', 'historyLimit',
                      'historyEvicted', 'clientRejectedBatches'):
            if numeric(recorder.get(field)):
                clean['recorder'][field] = recorder[field]
        if recorder.get('persistence') in ('ready', 'unavailable'):
            clean['recorder']['persistence'] = recorder['persistence']
        clean['recorder']['lastWriteAt'] = timestamp(recorder.get('lastWriteAt'))
    summary = payload.get('summary')
    if isinstance(summary, dict):
        clean['summary']['since'] = timestamp(summary.get('since'))
        counts = summary.get('requests')
        for count in counts[:128] if isinstance(counts, list) else []:
            if not isinstance(count, dict) or not allowed(count.get('class'), CLASSES) or not allowed(count.get('statusGroup'), {'2xx', '3xx', '4xx', '5xx', 'other'}):
                continue
            row = {field: count[field] for field in ('class', 'statusGroup')}
            row.update({field: count[field] for field in ('count', 'totalDurationMs', 'maxDurationMs') if numeric(count.get(field))})
            clean['summary']['requests'].append(row)
    return clean


def persisted_backend(directory):
    candidates, missing, unreadable, malformed = [], 0, 0, 0
    for name in ('backend-events.jsonl.1', 'backend-events.jsonl'):
        try:
            with (Path(directory) / 'diagnostics' / name).open('rb') as source:
                content = source.read(BODY_LIMIT // 2 + 1)
        except FileNotFoundError:
            missing += 1
            continue
        except OSError:
            unreadable += 1
            continue
        if len(content) > BODY_LIMIT // 2:
            unreadable += 1
            content = content[:BODY_LIMIT // 2].rsplit(b'\n', 1)[0]
        for line in content.splitlines():
            try:
                event = backend_event(json.loads(line))
            except (ValueError, UnicodeError):
                event = None
            if event:
                candidates.append(event)
            else:
                malformed += 1
    return {'schemaVersion': 1, 'available': False, 'clientTimeUntrusted': True,
            'capturedAt': candidates[-1]['receivedAt'] if candidates else None,
            'events': candidates[-EVENT_LIMIT:], 'discardedEvents': max(0, len(candidates) - EVENT_LIMIT),
            'missingFiles': missing, 'unreadableFiles': unreadable, 'malformedRecords': malformed}


def read_backend_diagnostics(admin_port, directory, opener):
    origin = f'http://127.0.0.1:{int(admin_port)}'
    failure_code = 'unavailable'
    try:
        with opener.open(origin + '/api/bootstrap', timeout=2) as response:
            bootstrap = json.loads(response.read(16384))
            token = bootstrap.get('token') if isinstance(bootstrap, dict) else None
            if response.status != 200 or not isinstance(token, str) or not token or len(token) > 1024:
                raise ValueError('invalid bootstrap')
        outgoing = request.Request(origin + '/api/diagnostics', headers={'X-Admin-Token': token})
        with opener.open(outgoing, timeout=3) as response:
            content = response.read(BODY_LIMIT + 1)
            length = response.headers.get('Content-Length')
            if (response.status != 200 or len(content) > BODY_LIMIT
                    or response.headers.get_content_type() != 'application/json'
                    or length is not None and (not length.isdigit() or int(length) != len(content))):
                raise ValueError('invalid diagnostic response')
            snapshot = backend_snapshot(json.loads(content))
        recorder = snapshot['recorder']
        incomplete = (snapshot['discardedEvents'] or recorder.get('persistence') != 'ready'
                      or any(recorder.get(field, 0) for field in ('dropped', 'writeFailures', 'clientRejectedBatches')))
        return {'status': ('unavailable' if not snapshot['available'] else 'partial' if incomplete else 'ok'), 'capturedAt': snapshot['capturedAt'],
                'historical': False, 'coverage': {'retentionLimited': bool(recorder.get('historyEvicted')),
                                                'historyEvicted': recorder.get('historyEvicted', 0)}, 'snapshot': snapshot}
    except error.HTTPError as response:
        response.close()
        failure_code = 'http_error'
    except (OSError, error.URLError, HTTPException):
        failure_code = 'connection_failed'
    except (ValueError, UnicodeError):
        failure_code = 'invalid_response'
    snapshot = persisted_backend(directory) if directory else {'available': False, 'events': [], 'capturedAt': None}
    return {'status': 'partial' if snapshot['events'] else 'unavailable', 'capturedAt': snapshot['capturedAt'],
            'historical': True, 'errorCode': failure_code,
            'coverage': {'retentionLimited': bool(snapshot.get('discardedEvents')), 'historyEvicted': snapshot.get('discardedEvents', 0)},
            'snapshot': snapshot}


def portal_event(candidate):
    allowed = {'probe', 'capport', 'student_page', 'room_connection', 'student_join', 'answer_save',
               'student_session', 'asset_error', 'request_error'}
    if not isinstance(candidate, dict) or not isinstance(candidate.get('kind'), str) or candidate['kind'] not in allowed or not timestamp(candidate.get('time')):
        return None
    event = {'time': candidate['time'], 'kind': candidate['kind']}
    try:
        address = ipaddress.IPv4Address(candidate.get('ip', ''))
        if address in ipaddress.IPv4Network('10.77.0.0/22') or address.is_loopback:
            event['ip'] = str(address)
    except (TypeError, ValueError):
        pass
    for field in ('status', 'durationMs'):
        if numeric(candidate.get(field)) and (field != 'status' or candidate[field] <= 599):
            event[field] = candidate[field]
    if candidate.get('method') in ('GET', 'HEAD', 'POST', 'PUT', 'DELETE', 'OPTIONS', 'PATCH'):
        event['method'] = candidate['method']
    if isinstance(candidate.get('proxyError'), str) and candidate['proxyError'] in {'request_canceled', 'connection_refused', 'descriptor_limit',
                                      'connection_reset', 'connection_closed', 'timeout', 'transport_error'}:
        event['proxyError'] = candidate['proxyError']
    return event


def app_release():
    try:
        with (Path(sys.executable).parent.parent / 'Info.plist').open('rb') as source:
            metadata = plistlib.load(source)
        if not isinstance(metadata, dict):
            raise ValueError('invalid bundle metadata')
        version, build = metadata.get('CFBundleShortVersionString'), metadata.get('CFBundleVersion')
        return {'appVersion': version if isinstance(version, str) and all(part.isdigit() for part in version.split('.')) else None,
                'buildNumber': build if isinstance(build, str) and build.isdigit() else None}
    except (OSError, ValueError, plistlib.InvalidFileException):
        return {'appVersion': None, 'buildNumber': None}


def report_complete(report):
    timeline = report['networkTimeline']
    recorder = timeline.get('recording', {})
    return (timeline['status'] == 'ok' and recorder.get('active') and not recorder.get('writeFailureCount')
            and report['backendDiagnostics']['status'] == 'ok'
            and not report['networkStatus'].get('inspectionError') and bool(report['networkStatus'].get('sampledAt'))
            and any(entry['name'] == 'portal-events.jsonl' and entry['status'] == 'ok' for entry in report['portalLogs']['files'])
            and all(entry['status'] == 'ok' for entry in report['dhcpLogs']['files']))
