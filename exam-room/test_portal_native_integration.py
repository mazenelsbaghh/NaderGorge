"""Exercise the native bridge against real HTTP and the compiled certificate checker."""
import json
from pathlib import Path
import subprocess
import tempfile
import socket
import time
from urllib import error, request
import unittest
from unittest.mock import patch

import network_setup
import portal_settings
from portal_settings import PortalSettings


class NativeNetworkUpdateGuardTest(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.binary_directory = tempfile.TemporaryDirectory(prefix='massar-maintenance-test-')
        cls.addClassCleanup(cls.binary_directory.cleanup)
        cls.binary = Path(cls.binary_directory.name) / 'exam-room-test'
        subprocess.run(['go', 'build', '-o', str(cls.binary), '.'], cwd=Path(__file__).parent,
                       check=True, capture_output=True, text=True, timeout=90)

    def setUp(self):
        self.directory = tempfile.TemporaryDirectory(prefix='massar-network-lease-')
        self.addCleanup(self.directory.cleanup)
        self.bundle = Path(self.directory.name) / 'portal-helper'
        self.bundle.mkdir()
        for filename in ('mac-direct-setup.sh', 'dnsmasq'):
            (self.bundle / filename).write_text('test fixture; never executed')
        self.installed_mode = Path(self.directory.name)/'installed-mode.json'
        self.installed_certificate = Path(self.directory.name)/'installed-certificate.json'
        for name, value in [('INSTALLED_MODE', self.installed_mode), ('INSTALLED_METADATA', self.installed_certificate)]:
            replacement = patch.object(portal_settings, name, value)
            replacement.start()
            self.addCleanup(replacement.stop)
        with socket.socket() as first, socket.socket() as second:
            first.bind(('127.0.0.1', 0))
            second.bind(('127.0.0.1', 0))
            student_port, self.admin_port = first.getsockname()[1], second.getsockname()[1]
        self.server = subprocess.Popen([str(self.binary), '--port', str(student_port),
                                        '--admin-port', str(self.admin_port), '--data-dir', self.directory.name],
                                       stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        self.addCleanup(self.stop_server)
        self.opener = request.build_opener(request.ProxyHandler({}), network_setup.NoPortalRedirect())
        self.origin = f'http://127.0.0.1:{self.admin_port}'
        deadline = time.monotonic() + 10
        while True:
            try:
                with self.opener.open(self.origin + '/api/bootstrap', timeout=1) as response:
                    self.token = json.load(response)['token']
                break
            except OSError:
                if self.server.poll() is not None or time.monotonic() >= deadline:
                    self.fail('Isolated exam server did not start')
                time.sleep(0.02)
        self.api = network_setup.NetworkSetupApi(admin_port=self.admin_port)
        self.commands = []

    def stop_server(self):
        if self.server.poll() is None:
            self.server.terminate()
            self.server.wait(timeout=5)

    def post(self, path, body=None):
        outgoing = request.Request(self.origin + path, data=json.dumps(body or {}).encode(),
                                   headers={'X-Admin-Token': self.token, 'Content-Type': 'application/json',
                                            'X-Exam-Request': '1'})
        try:
            with self.opener.open(outgoing, timeout=3) as response:
                return response.status, json.load(response)
        except error.HTTPError as response:
            with response:
                return response.code, json.load(response)

    def prepare(self, authorization):
        def macos_command(*arguments, **kwargs):
            self.commands.append(arguments)
            if arguments[:2] == ('networksetup', '-listnetworkserviceorder'):
                return subprocess.CompletedProcess(arguments, 0, '(1) Test Ethernet\n(Hardware Port: Test Ethernet, Device: en99)\n', '')
            if arguments == ('ifconfig', 'en99'):
                return subprocess.CompletedProcess(arguments, 0, 'status: active\n', '')
            if arguments[0] == 'osascript':
                # Only the OS boundary is mocked: never change the machine's network.
                return authorization(arguments, kwargs)
            self.fail(f'Unexpected macOS command: {arguments}')

        with patch.object(network_setup.sys, 'platform', 'darwin'), \
             patch.object(network_setup.sys, '_MEIPASS', self.directory.name, create=True), \
             patch.object(network_setup, 'command', side_effect=macos_command):
            return self.api.prepare_network()

    def test_authorization_holds_lease_against_real_room_open_and_releases_on_cancel(self):
        created = []

        def authorize(arguments, _):
            status, exam = self.post('/api/demo')
            self.assertEqual(status, 200)
            created.append(exam['id'])
            status, _ = self.post(f"/api/exams/{exam['id']}/publish")
            self.assertEqual(status, 409, 'room opened while macOS authorization was outstanding')
            self.assertEqual(self.post('/api/network/maintenance/acquire')[0], 409)
            return subprocess.CompletedProcess(arguments, 1, '', 'User canceled (-128)')

        result = self.prepare(authorize)
        self.assertFalse(result['ok'])
        self.assertIn('أُلغيت', result['message'])
        self.assertEqual(self.post(f'/api/exams/{created[0]}/publish')[0], 200)

    def test_waiting_running_and_unavailable_admin_never_reach_authorization(self):
        status, exam = self.post('/api/demo')
        self.assertEqual(status, 200)
        self.assertEqual(self.post(f"/api/exams/{exam['id']}/publish")[0], 200)
        for state in ('waiting', 'running', 'unavailable'):
            with self.subTest(state=state):
                if state == 'running':
                    self.assertEqual(self.post(f"/api/exams/{exam['id']}/start")[0], 200)
                elif state == 'unavailable':
                    self.stop_server()
                result = self.prepare(lambda *_: self.fail('unsafe authorization'))
                self.assertFalse(result['ok'])

    def test_certificate_failure_releases_lease_before_any_authorization(self):
        status, exam = self.post('/api/demo')
        self.assertEqual(status, 200)
        settings = PortalSettings(self.directory.name, Path(self.directory.name) / 'absent-checker', lambda _: None)
        settings.directory.mkdir()
        (settings.directory / 'candidate.json').write_text('{malformed')
        self.api = network_setup.NetworkSetupApi(portal_settings=settings, admin_port=self.admin_port)
        self.assertFalse(self.prepare(lambda *_: self.fail('invalid certificate reached authorization'))['ok'])
        self.assertEqual(self.post(f"/api/exams/{exam['id']}/publish")[0], 200)

    def test_native_mode_selection_survives_cancel_without_changing_applied_mode(self):
        settings = PortalSettings(self.directory.name, Path(self.directory.name)/'checker', lambda _: None)
        self.api = network_setup.NetworkSetupApi(portal_settings=settings, admin_port=self.admin_port)
        for desired, applied in [('legacy', 'https'), ('https', 'legacy')]:
            with self.subTest(desired=desired):
                self.installed_mode.write_text(json.dumps({'mode': applied}))
                # Legacy recovery must reach authorization even with a broken
                # root certificate; HTTPS reuse is validated by the installer.
                self.installed_certificate.write_text('{broken certificate metadata')
                self.api.select_portal_mode(desired)

                def authorize(arguments, _):
                    self.assertEqual(arguments[-1], desired)
                    self.assertGreater(int(arguments[-2]), time.time())
                    self.assertEqual(arguments[-4:-2], ('', ''))
                    return subprocess.CompletedProcess(arguments, 1, '', 'User canceled (-128)')

                result = self.prepare(authorize)
                self.assertFalse(result['ok'])
                restored = PortalSettings(self.directory.name, Path(self.directory.name)/'checker', lambda _: None)
                status = restored.status()
                self.assertEqual((status['desiredMode'], status['appliedMode']), (desired, applied))
                self.assertTrue(status['pendingModeChange'])
                self.assertEqual(self.installed_mode.read_text(), json.dumps({'mode': applied}))
                self.assertEqual(self.installed_certificate.read_text(), '{broken certificate metadata')

    def assert_uncertain_completion_keeps_rooms_blocked(self, timed_out):
        def authorize(arguments, options):
            self.assertGreater(int(arguments[-2]), time.time() + options['timeout'])
            self.assertEqual(arguments[-1], 'preserve')
            self.assertLessEqual(options['timeout'], 185)
            if timed_out:
                raise subprocess.TimeoutExpired(arguments, options['timeout'])
            return subprocess.CompletedProcess(arguments, 1, '', 'Application connection is invalid (-609)')

        result = self.prepare(authorize)
        self.assertFalse(result['ok'])
        self.assertIn('١٠ دقائق', result['message'])
        status, exam = self.post('/api/demo')
        self.assertEqual(status, 200)
        self.assertEqual(self.post(f"/api/exams/{exam['id']}/publish")[0], 409)

    def test_timeout_keeps_room_opening_blocked(self):
        self.assert_uncertain_completion_keeps_rooms_blocked(timed_out=True)

    def test_appleevent_failure_keeps_room_opening_blocked(self):
        self.assert_uncertain_completion_keeps_rooms_blocked(timed_out=False)

    def test_ambiguous_or_inactive_ethernet_does_not_acquire_lease(self):
        for states in [('active', 'active'), ('inactive', 'inactive')]:
            with self.subTest(states=states):
                def macos_command(*arguments, **_):
                    if arguments[:2] == ('networksetup', '-listnetworkserviceorder'):
                        return subprocess.CompletedProcess(arguments, 0, '(1) Internet\n(Hardware Port: Ethernet, Device: en99)\n(2) Omada\n(Hardware Port: Ethernet, Device: en98)\n', '')
                    if arguments[0] == 'ifconfig':
                        state = states[0 if arguments[1] == 'en99' else 1]
                        return subprocess.CompletedProcess(arguments, 0, f'status: {state}\n', '')
                    self.fail('unsafe system command')

                with patch.object(network_setup.sys, 'platform', 'darwin'), \
                     patch.object(network_setup, 'command', side_effect=macos_command), \
                     patch.object(network_setup.request, 'build_opener', side_effect=AssertionError('no lease before cable is unambiguous')):
                    result = self.api.prepare_network()
                self.assertFalse(result['ok'])
                self.assertIn('أكثر من كابل' if states[0] == 'active' else 'وصّل كابل', result['message'])


class NativeCertificateCheckerIntegrationTest(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.fixture = tempfile.TemporaryDirectory(prefix='massar-native-certificate-')
        cls.addClassCleanup(cls.fixture.cleanup)
        cls.root = Path(cls.fixture.name)
        cls.binary = cls.root / 'portal-checker'
        source = Path(__file__).resolve().parent
        subprocess.run(['go', 'build', '-o', str(cls.binary), './cmd/portal-proxy'], cwd=source,
                       check=True, capture_output=True, text=True, timeout=90)
        config = cls.root / 'openssl.cnf'
        config.write_text('[req]\nprompt=no\ndistinguished_name=subject\nx509_extensions=server\n'
                          '[subject]\nCN=portal.example.test\n[server]\n'
                          'subjectAltName=DNS:portal.example.test\nbasicConstraints=critical,CA:FALSE\n'
                          'keyUsage=critical,digitalSignature,keyEncipherment\nextendedKeyUsage=serverAuth\n')
        subprocess.run(['/usr/bin/openssl', 'req', '-x509', '-newkey', 'rsa:2048', '-nodes', '-sha256',
                        '-days', '2', '-config', str(config), '-out', str(cls.root / 'fullchain.pem'),
                        '-keyout', str(cls.root / 'privkey.pem')], check=True,
                       capture_output=True, text=True, timeout=20)

    def test_real_checker_rejects_untrusted_or_wrong_host_without_replacing_candidate(self):
        for hostname in ('portal.example.test', 'other.example.test'):
            with self.subTest(hostname=hostname), tempfile.TemporaryDirectory() as directory:
                checked = subprocess.run([str(self.binary), '--check-capport', '--capport-hostname', hostname,
                                          '--capport-cert', str(self.root / 'fullchain.pem'),
                                          '--capport-key', str(self.root / 'privkey.pem')],
                                         capture_output=True, text=True, timeout=15)
                self.assertNotEqual(checked.returncode, 0)
                self.assertEqual(checked.stdout, '')
                selected = iter((self.root / 'fullchain.pem', self.root / 'privkey.pem'))
                settings = PortalSettings(directory, self.binary, lambda _: next(selected))
                settings.directory.mkdir()
                candidate = settings.directory / 'candidate.json'
                candidate.write_text('{"existing":"approved candidate"}')
                with self.assertRaisesRegex(ValueError, 'الشهادة غير صالحة'):
                    settings.import_certificate(hostname)
                self.assertEqual(candidate.read_text(), '{"existing":"approved candidate"}')
                self.assertEqual(list(settings.directory.glob('certificate-*')), [])


if __name__ == '__main__':
    unittest.main()
