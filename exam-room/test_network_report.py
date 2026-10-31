import errno
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
import json
from pathlib import Path
import plistlib
import tempfile
import threading
import unittest
from unittest.mock import patch

import network_setup
from network_report import backend_snapshot
from network_timeline import NetworkTimeline


STAMP = '2026-10-10T15:00:00.000Z'


class NetworkReportTest(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name)
        self.portal = self.root / 'portal'
        self.portal.mkdir()
        self.timeline = NetworkTimeline(self.root)
        self.addCleanup(self.timeline.stop)
        self.timeline._collect = lambda: {'ready': True, 'ethernetDevice': 'en10',
            'ethernetService': 'PRIVATE-DEVICE-NAME', 'answers': 'PRIVATE-ANSWER',
            'ethernetRuntimeAddresses': [{'ip': '10.77.0.1', 'mask': '255.255.252.0'}]}
        self.timeline.capture()
        portal_event = {'time': STAMP, 'kind': 'student_join', 'ip': '10.77.0.55', 'status': 502,
                        'host': 'PRIVATE-HOST', 'browser': 'PRIVATE-BROWSER', 'name': 'PRIVATE-STUDENT',
                        'answers': 'PRIVATE-ANSWER', 'durationMs': 41, 'proxyError': 'connection_refused'}
        (self.portal / 'portal-events.jsonl').write_text(json.dumps(portal_event) + '\n')
        (self.portal / 'dnsmasq.log').write_text('Oct 10 15:00:00 dnsmasq-dhcp[8]: DHCPACK(en10) 10.77.0.55 00:11:22:33:44:55 PRIVATE-STUDENT\n')
        (self.portal / 'dnsmasq-error.log').write_text('dnsmasq: failed to send to PRIVATE-STUDENT 00:11:22:33:44:55\n')
        self.backend_event = {'receivedAt': STAMP, 'source': 'backend', 'kind': 'startup_failed',
                              'category': 'address_in_use', 'name': 'PRIVATE-STUDENT', 'token': 'PRIVATE-TOKEN'}
        self.payload = {'schemaVersion': 1, 'available': True, 'capturedAt': STAMP,
                        'events': [self.backend_event], 'recorder': {'persistence': 'ready', 'dropped': 0, 'writeFailures': 0},
                        'summary': {'since': STAMP, 'requests': [{'class': 'join', 'statusGroup': '5xx', 'count': 2,
                                                               'totalDurationMs': 90, 'maxDurationMs': 50, 'phone': 'PRIVATE-PHONE'}]}}
        self.authorized = False
        self.redirect = False
        fixture = self
        class Handler(BaseHTTPRequestHandler):
            def do_GET(self):
                if self.path == '/api/bootstrap':
                    body = {'token': 'PRIVATE-TOKEN'}
                elif self.path == '/api/diagnostics':
                    fixture.authorized = self.headers.get('X-Admin-Token') == 'PRIVATE-TOKEN'
                    if not fixture.authorized:
                        self.send_error(401)
                        return
                    if fixture.redirect:
                        self.send_response(302)
                        self.send_header('Location', 'http://example.invalid/private-token')
                        self.end_headers()
                        return
                    body = fixture.payload
                else:
                    self.send_error(404)
                    return
                encoded = json.dumps(body).encode()
                self.send_response(200)
                self.send_header('Content-Type', 'application/json')
                self.send_header('Content-Length', str(len(encoded)))
                self.end_headers()
                self.wfile.write(encoded)
            def log_message(self, *_):
                pass
        self.server = ThreadingHTTPServer(('127.0.0.1', 0), Handler)
        self.thread = threading.Thread(target=self.server.serve_forever)
        self.thread.start()
        self.addCleanup(self.stop_server)
        self.api = network_setup.NetworkSetupApi(admin_port=self.server.server_port, timeline=self.timeline)
        self.scope = patch.object(network_setup, 'PORTAL_DIAGNOSTIC_DIRECTORY', self.portal)
        self.scope.start()
        self.addCleanup(self.scope.stop)

    def stop_server(self):
        if self.thread.is_alive():
            self.server.shutdown()
            self.thread.join(2)
        self.server.server_close()

    def test_authenticated_combined_export_has_history_version_and_no_private_payload_or_hardware_probes(self):
        executable = self.root / 'app/Contents/MacOS/Massar'
        executable.parent.mkdir(parents=True)
        (executable.parent.parent / 'Info.plist').write_bytes(plistlib.dumps({'CFBundleShortVersionString': '0.4.24', 'CFBundleVersion': '43'}))
        with patch.object(network_setup.Path, 'home', return_value=self.root), \
             patch('network_report.sys.executable', str(executable)), \
             patch.object(network_setup, 'command', side_effect=AssertionError('logging must not change or probe hardware')):
            result = self.api.capture_network_incident()
        self.assertTrue(result['ok'])
        self.assertTrue(self.authorized)
        report = json.loads(Path(result['path']).read_text())
        self.assertEqual(report['appVersion'], '0.4.24')
        self.assertEqual(report['buildNumber'], '43')
        self.assertEqual(report['events'][0]['ip'], '10.77.0.55')
        self.assertEqual(report['backendDiagnostics']['snapshot']['events'][0]['category'], 'address_in_use')
        self.assertEqual(report['networkTimeline']['events'][-1]['kind'], 'user_marker')
        self.assertEqual(report['incident'], {'time': result['capturedAt'], 'markerSaved': True})
        self.assertEqual(report['networkTimeline']['events'][-1]['time'], result['capturedAt'])
        self.assertEqual(report['networkTimeline']['events'][-1]['state']['ethernetDevice'], 'en10')
        self.assertNotIn('PRIVATE-', json.dumps(report))
        self.assertNotIn('00:11:22:33:44:55', json.dumps(report))

    def test_dead_backend_uses_typed_persisted_history_and_missing_sections_stay_partial(self):
        self.stop_server()
        logs = self.root / 'diagnostics'
        logs.mkdir()
        (logs / 'backend-events.jsonl').write_text(json.dumps(self.backend_event) + '\n{truncated\n')
        (self.portal / 'dnsmasq-error.log').unlink()
        with patch.object(network_setup, 'command', side_effect=AssertionError('unexpected hardware probe')):
            report = self.api.network_diagnostics()
        self.assertEqual(report['status'], 'partial')
        backend = report['backendDiagnostics']
        self.assertEqual(backend['status'], 'partial')
        self.assertTrue(backend['historical'])
        self.assertFalse(backend['snapshot']['available'])
        self.assertEqual(backend['snapshot']['malformedRecords'], 1)
        self.assertEqual(backend['snapshot']['events'][0]['kind'], 'startup_failed')
        self.assertEqual(report['dhcpLogs']['files'][1]['status'], 'missing')
        self.assertNotIn('PRIVATE-', json.dumps(report))

    def test_redirect_is_not_followed_and_lost_backend_records_are_reported_as_partial(self):
        self.redirect = True
        self.assertEqual(self.api.network_diagnostics()['backendDiagnostics']['errorCode'], 'http_error')
        self.redirect = False
        self.payload['recorder']['dropped'] = 2
        report = self.api.network_diagnostics()
        self.assertEqual(report['backendDiagnostics']['status'], 'partial')
        self.assertEqual(report['backendDiagnostics']['snapshot']['recorder']['dropped'], 2)

    def test_export_write_failure_returns_no_saved_artifact_and_does_not_raise(self):
        with patch.object(network_setup.Path, 'home', return_value=self.root), \
             patch('exam_downloads.os.fsync', side_effect=OSError(errno.ENOSPC, 'PRIVATE-PATH')):
            result = self.api.capture_network_incident()
        self.assertFalse(result['ok'])
        self.assertTrue(result['markerSaved'])
        self.assertEqual(result['status'], 'failed')
        self.assertNotIn('path', result)
        self.assertEqual(list((self.root / 'Downloads').iterdir()), [])
        self.assertEqual(self.timeline.history()['events'][-1]['time'], result['capturedAt'])

    def test_incident_report_survives_dead_backend_and_failed_timeline_write(self):
        self.stop_server()
        original_open = network_setup.os.open
        def open_except_timeline(path, *args, **kwargs):
            if Path(path) == self.timeline.path:
                raise PermissionError(errno.EACCES, 'PRIVATE-PATH')
            return original_open(path, *args, **kwargs)
        with patch.object(network_setup.Path, 'home', return_value=self.root), \
             patch('network_timeline.os.open', side_effect=open_except_timeline):
            result = self.api.capture_network_incident()
        self.assertTrue(result['ok'])
        self.assertFalse(result['markerSaved'])
        self.assertEqual(result['status'], 'partial')
        report = json.loads(Path(result['path']).read_text())
        self.assertEqual(report['incident'], {'time': result['capturedAt'], 'markerSaved': False})
        self.assertEqual(report['backendDiagnostics']['status'], 'unavailable')
        self.assertNotIn('PRIVATE-', json.dumps(report))

    def test_large_and_malformed_backend_event_arrays_are_bounded_and_redacted(self):
        payload = {**self.payload, 'events': [self.backend_event] * 8193 + [{'kind': ['PRIVATE-STUDENT']}],
                   'summary': {'requests': [{'class': ['PRIVATE'], 'statusGroup': '5xx'}]}}
        clean = backend_snapshot(payload)
        self.assertEqual(len(clean['events']), 8191)
        self.assertEqual(clean['discardedEvents'], 3)
        self.assertNotIn('PRIVATE', json.dumps(clean))


if __name__ == '__main__':
    unittest.main()
