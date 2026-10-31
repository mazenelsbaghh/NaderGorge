import subprocess
import threading
import json
import io
import tempfile
from pathlib import Path
import unittest
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from unittest.mock import patch
from types import SimpleNamespace
from urllib.error import HTTPError

import network_setup


class AndroidPortalProbeTest(unittest.TestCase):
    def test_ready_requires_dns_redirect_and_actual_exam_service(self):
        for dns_ip, http_status, service_status, service_body, expected, mode in [
            ('127.0.0.1', 302, 200, '{"rooms":[],"serverTime":123}', True, 'legacy'),
            ('127.0.0.2', 302, 200, '{"rooms":[],"serverTime":123}', False, 'legacy'),
            ('127.0.0.1', 200, 200, '{"rooms":[],"serverTime":123}', False, 'legacy'),
            ('127.0.0.1', 503, 200, '{"rooms":[],"serverTime":123}', False, 'legacy'),
            ('127.0.0.1', 302, 502, 'Bad Gateway', False, 'legacy'),
            ('127.0.0.1', 302, 200, '<html>Router login</html>', False, 'legacy'),
            ('127.0.0.1', 302, 200, '{}', False, 'legacy'),
            ('127.0.0.1', 204, 200, '{"rooms":[],"serverTime":123}', True, 'direct'),
            ('127.0.0.1', 302, 200, '{"rooms":[],"serverTime":123}', False, 'direct'),
            ('127.0.0.1', 204, 502, 'Bad Gateway', False, 'direct'),
            ('127.0.0.2', 204, 200, '{"rooms":[],"serverTime":123}', False, 'direct'),
            ('127.0.0.1', 302, 200, '{"rooms":[],"serverTime":123}', True, 'welcome'),
            ('127.0.0.1', 204, 200, '{"rooms":[],"serverTime":123}', True, 'welcome'),
            ('127.0.0.1', 302, 502, 'Bad Gateway', False, 'welcome'),
        ]:
            with self.subTest(dns_ip=dns_ip, http_status=http_status, service_status=service_status, service_body=service_body):
                requests = []
                class Handler(BaseHTTPRequestHandler):
                    def do_GET(self):
                        requests.append((self.path, self.headers.get('Host')))
                        if self.path == '/api/lounge':
                            self.send_response(service_status)
                            self.end_headers()
                            self.wfile.write(service_body.encode())
                            return
                        self.send_response(http_status)
                        if http_status == 302:
                            self.send_header('Location', student_url+'welcome' if mode == 'welcome' else student_url)
                        self.end_headers()
                    def log_message(self, *_):
                        pass
                server = ThreadingHTTPServer(('127.0.0.1', 0), Handler)
                student_url = f'http://127.0.0.1:{server.server_port}/'
                thread = threading.Thread(target=server.serve_forever)
                thread.start()
                try:
                    dns_result = subprocess.CompletedProcess([], 0, dns_ip+'\n', '')
                    with patch.object(network_setup, 'command', return_value=dns_result):
                        result = network_setup.check_android_portal(student_url, mode=mode)
                    self.assertEqual(result['ready'], expected)
                    self.assertEqual(requests, [('/generate_204', 'connectivitycheck.gstatic.com'),
                                                ('/api/lounge', f'127.0.0.1:{server.server_port}')])
                finally:
                    server.shutdown()
                    thread.join()
                    server.server_close()

    def test_inactive_cable_never_reports_ready(self):
        wired = {'active': False, 'device': 'en9', 'service': 'Ethernet', 'ambiguous': False}
        with patch.object(network_setup.sys, 'platform', 'darwin'), \
             patch.object(network_setup, 'wired_service', return_value=wired), \
             patch.object(network_setup, 'service_running', return_value=True), \
             patch.object(network_setup, 'command', return_value=subprocess.CompletedProcess([], 0, 'IP address: 10.77.0.1\n', '')), \
             patch.object(network_setup, 'check_android_portal') as probe:
            status = network_setup.NetworkSetupApi().network_status()
        self.assertFalse(status['ready'])
        self.assertFalse(status['radioSettingsVerified'])
        probe.assert_not_called()



class NetworkDiagnosticsTest(unittest.TestCase):
    def test_failure_status_redacts_private_lease_errors_before_export(self):
        service = subprocess.CompletedProcess([], 0, 'state = exited\nlast exit code = 2\n', '')
        with patch.object(network_setup, 'command', return_value=service), \
             patch.object(network_setup, 'diagnostic_tail', return_value='dnsmasq: failed to parse lease for Alice 00:11:22:33:44:55\n'):
            detail = network_setup.dhcp_failure_detail()
        self.assertIn('unreadable record', detail)
        self.assertIn('قد يكون سابقًا', detail)
        self.assertNotIn('Alice', detail)
        self.assertNotIn('00:11:22:33:44:55', detail)

    def test_tail_drops_partial_first_line(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'events'
            path.write_text('partial-long-line\n{"status":302}\n')
            self.assertEqual(network_setup.diagnostic_tail(path, 19), '{"status":302}\n')
            self.assertEqual(network_setup.diagnostic_tail(Path(directory) / 'missing'), '')

    def test_export_keeps_real_events_and_service_state(self):
        api = network_setup.NetworkSetupApi()
        report = {'events': [{'ip': '10.77.0.25', 'kind': 'probe', 'status': 302}], 'networkStatus': {'ready': False}}
        with tempfile.TemporaryDirectory() as directory, \
             patch.object(network_setup.Path, 'home', return_value=Path(directory)), \
             patch.object(api, 'network_diagnostics', return_value=report):
            result = api.export_network_diagnostics()
            saved = json.loads(Path(result['path']).read_text())
        self.assertEqual(saved['events'][0]['status'], 302)
        self.assertFalse(saved['networkStatus']['ready'])


class DhcpReadinessTest(unittest.TestCase):
    def test_os_inspection_failure_returns_unknown_without_changing_network(self):
        for failure in (subprocess.TimeoutExpired('networksetup', 3), OSError('device unavailable'),
                        subprocess.CompletedProcess([], 1, '', 'permission denied')):
            with self.subTest(failure=failure), \
                 patch.object(network_setup, 'command', side_effect=lambda *args, **kwargs: self.os_failure(failure)):
                status = network_setup.NetworkSetupApi().network_status()
            self.assertFalse(status['ready'])
            self.assertTrue(status['inspectionError'])
            self.assertNotIn('dhcpRunning', status)

    @staticmethod
    def os_failure(outcome):
        if isinstance(outcome, Exception):
            raise outcome
        return outcome

    def test_disconnected_hub_reports_configured_adapter_instead_of_first_old_adapter(self):
        services = '(1) Old USB\n(Hardware Port: USB, Device: en5)\n(2) Current USB\n(Hardware Port: USB, Device: en10)\n'
        def run(*args, **kwargs):
            output = services if args[0] == 'networksetup' else 'status: inactive\n'
            return subprocess.CompletedProcess(args, 0, output, '')
        with tempfile.TemporaryDirectory() as directory:
            config = Path(directory) / 'dnsmasq.conf'
            config.write_text('interface=en10\n')
            with patch.object(network_setup, 'command', side_effect=run):
                selected = network_setup.wired_service(config)
        self.assertEqual(selected['device'], 'en10')
        self.assertFalse(selected['active'])
        self.assertEqual([entry['device'] for entry in selected['candidates']], ['en5', 'en10'])

    def test_running_process_and_http_never_hide_missing_dhcp_socket_or_wrong_subnet(self):
        with tempfile.TemporaryDirectory() as directory:
            base = Path(directory)
            config = base / 'dnsmasq.conf'
            leases = base / 'leases'
            leases.write_text('')
            (base / 'dnsmasq').touch()
            settings = (Path(__file__).parent / 'packaging/omada-direct-dnsmasq.conf').read_text()
            config.write_text(settings.replace('interface=en0', 'interface=en10').replace('/var/db/massar-exam-room/dnsmasq.leases', str(leases)))
            def open_response(url, **kwargs):
                if isinstance(url, network_setup.request.Request):
                    raise HTTPError(url.full_url, 302, 'redirect', {'Location': 'http://10.77.0.1/'}, io.BytesIO())
                response = io.BytesIO(b'{"rooms":[],"serverTime":123}')
                response.status = 200
                return response
            for socket_text, socket_error, mask, runtime_address, device, expected in [
                ('udp4 0 0 *.67 *.*\n', '', '255.255.252.0', 'inet 10.77.0.1 netmask 0xfffffc00', 'en10', True),
                ('', '', '255.255.252.0', 'inet 10.77.0.1 netmask 0xfffffc00', 'en10', False),
                ('', 'Operation not permitted', '255.255.252.0', 'inet 10.77.0.1 netmask 0xfffffc00', 'en10', False),
                ('udp4 0 0 *.67 *.*\n', '', '255.255.255.0', 'inet 10.77.0.1 netmask 0xfffffc00', 'en10', False),
                ('udp4 0 0 *.67 *.*\n', '', '255.255.252.0', '', 'en10', False),
                ('udp4 0 0 *.67 *.*\n', '', '255.255.252.0', 'inet 10.77.0.1 netmask 0xffffff00', 'en10', False),
                ('udp4 0 0 *.67 *.*\n', '', '255.255.252.0', 'inet 10.77.0.1 netmask 0xfffffc00', 'en11', False),
            ]:
                with self.subTest(socket_text=socket_text, socket_error=socket_error, mask=mask, runtime_address=runtime_address, device=device):
                    def run(*args, **kwargs):
                        if args[0] == 'networksetup':
                            text = (f'(1) USB\n(Hardware Port: USB, Device: {device})\n' if args[1] == '-listnetworkserviceorder'
                                    else f'IP address: 10.77.0.1\nSubnet mask: {mask}\n')
                        elif args[0] == 'ifconfig': text = 'status: active\n '+runtime_address+'\n'
                        elif args[0] == 'launchctl': text = 'state = running\npid = 12\n'
                        elif args[0] == '/usr/sbin/netstat': return subprocess.CompletedProcess(args, 0, socket_text, socket_error)
                        elif args[0] == '/usr/bin/dig': text = '10.77.0.1\n'
                        else: text = 'syntax check OK'
                        return subprocess.CompletedProcess(args, 0, text, '')
                    with patch.object(network_setup, 'DEFAULT_CONFIG', config), \
                         patch.object(network_setup, 'bundle_path', return_value=base), \
                         patch.object(network_setup, 'command', side_effect=run), \
                         patch.object(network_setup.request, 'build_opener', return_value=SimpleNamespace(open=open_response)):
                        status = network_setup.NetworkSetupApi().network_status()
                    self.assertEqual(status['ready'], expected)
                    self.assertTrue(status['dhcpRunning'])
                    self.assertEqual(status['ethernetDeviceChanged'], device != 'en10')
                    if status['ethernetAddressReady']:
                        self.assertTrue(status['androidProbe']['ready'])
                    else:
                        self.assertIsNone(status['androidProbe'])
                        self.assertTrue(status['ethernetDetail'])
                    if socket_error: self.assertIsNone(status['dhcpSocketReady'])

if __name__ == '__main__':
    unittest.main()
