from datetime import datetime, timedelta, timezone
import json
from pathlib import Path
import tempfile
import subprocess
import unittest
from unittest.mock import patch

from portal_settings import PortalSettings
import portal_settings


class PortalCertificateStorageTest(unittest.TestCase):
    def setUp(self):
        directory = tempfile.TemporaryDirectory(prefix='massar-installed-portal-fixture-')
        self.addCleanup(directory.cleanup)
        for name, filename in [('INSTALLED_METADATA', 'metadata.json'), ('INSTALLED_MODE', 'mode.json')]:
            replacement = patch.object(portal_settings, name, Path(directory.name)/filename)
            replacement.start()
            self.addCleanup(replacement.stop)

    def test_validation_failure_and_cancel_preserve_existing_candidate(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            inputs = [root / 'fullchain.pem', root / 'privkey.pem']
            for path in inputs:
                path.write_text('invalid certificate material')
            picks = iter(inputs)
            settings = PortalSettings(root, root / 'checker', lambda _: next(picks))
            settings.directory.mkdir()
            previous = settings.directory / 'candidate.json'
            previous.write_text('{"existing":"candidate"}')
            # The certificate checker is a subprocess boundary; emulate its rejection.
            with patch.object(portal_settings.subprocess, 'run') as check:
                check.return_value.returncode = 1
                with self.assertRaisesRegex(ValueError, 'الشهادة غير صالحة'):
                    settings.import_certificate('exam.massar-academy.net')
            self.assertEqual(previous.read_text(), '{"existing":"candidate"}')
            self.assertEqual(list(settings.directory.glob('certificate-*')), [])
            settings.choose_file = lambda _: None
            self.assertEqual(settings.import_certificate('exam.massar-academy.net'), {'saved': False})
            self.assertEqual(previous.read_text(), '{"existing":"candidate"}')

    def test_verified_import_retains_private_permissions_without_returning_key(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            inputs = [root / 'certificate.pem', root / 'key.pem']
            inputs[0].write_text('certificate')
            inputs[1].write_text('PRIVATE KEY MATERIAL')
            picks = iter(inputs)
            settings = PortalSettings(root, root / 'checker', lambda _: next(picks))
            metadata = {'hostname':'exam.massar-academy.net','expiresAt':(datetime.now(timezone.utc)+timedelta(days=50)).isoformat(),'daysRemaining':50}
            with patch.object(portal_settings.subprocess, 'run') as check:
                check.return_value.returncode = 0
                check.return_value.stdout = json.dumps(metadata)
                selected = settings.import_certificate(metadata['hostname'])
                install = settings.installation()
            self.assertTrue(selected['saved'])
            self.assertNotIn('PRIVATE', json.dumps(selected))
            stored = Path(install['directory'])
            self.assertEqual((stored/'privkey.pem').read_text(),'PRIVATE KEY MATERIAL')
            self.assertEqual((stored/'privkey.pem').stat().st_mode & 0o777,0o600)
            self.assertEqual(stored.stat().st_mode & 0o777,0o700)

    def test_invalid_hostname_never_opens_file_picker(self):
        settings = PortalSettings('/unused','/unused',lambda _: self.fail('picker opened for invalid hostname'))
        for hostname in ['https://example.com/','example.com\n--flag','127.0.0.1','evil\".com','-exam.example.com']:
            with self.subTest(hostname=hostname), self.assertRaises(ValueError):
                settings.import_certificate(hostname)

    def test_status_distinguishes_absent_expired_renewal_and_unreachable(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            settings = PortalSettings(root,root/'checker',lambda _: None)
            installed = root/'installed.json'
            with patch.object(portal_settings, 'INSTALLED_METADATA',installed), \
                 patch.object(portal_settings, 'INSTALLED_MODE', root/'applied-mode.json'):
                self.assertEqual(settings.status()['state'],'legacy')
                for days, reachable, state in [(-1,True,'expired'),(3,True,'expiring'),(50,False,'unreachable'),(50,True,'ready')]:
                    with self.subTest(days=days,reachable=reachable):
                        installed.write_text(json.dumps({'hostname':'exam.massar-academy.net','expiresAt':(datetime.now(timezone.utc)+timedelta(days=days)).isoformat()}))
                        checked = subprocess.CompletedProcess([], 0 if reachable else 1,
                                                              json.dumps({'ready': reachable, 'hostname': 'exam.massar-academy.net'}), '')
                        with patch.object(portal_settings.subprocess, 'run', return_value=checked):
                            status=settings.status()
                        self.assertEqual(status['state'],state)
                        self.assertFalse(status['phoneVerified'])
                installed.write_text('{broken')
                self.assertEqual(settings.status()['state'],'error')


class PortalModeSelectionTest(unittest.TestCase):
    def setUp(self):
        directory = tempfile.TemporaryDirectory(prefix='massar-portal-mode-')
        self.addCleanup(directory.cleanup)
        self.root = Path(directory.name)
        self.installed = self.root / 'installed.json'
        self.applied = self.root / 'portal-mode.json'
        self.settings = PortalSettings(self.root, self.root/'checker', lambda _: None)
        for name, value in [('INSTALLED_METADATA', self.installed), ('INSTALLED_MODE', self.applied)]:
            replacement = patch.object(portal_settings, name, value)
            replacement.start()
            self.addCleanup(replacement.stop)

    def certificate_metadata(self, days=50):
        return {'hostname': 'exam.massar-academy.net',
                'expiresAt': (datetime.now(timezone.utc)+timedelta(days=days)).isoformat()}

    def healthy_status(self):
        checked = subprocess.CompletedProcess([], 0, json.dumps({'ready': True, 'hostname': 'exam.massar-academy.net'}), '')
        with patch.object(portal_settings.subprocess, 'run', return_value=checked):
            return self.settings.status()

    def test_selecting_legacy_keeps_live_https_until_installer_applies_change(self):
        original = json.dumps(self.certificate_metadata())
        self.installed.write_text(original)
        self.applied.write_text('{"mode":"https"}')
        self.settings.select_mode('legacy')
        # A fresh process sees the same choice, but actual HTTPS remains live.
        self.settings = PortalSettings(self.root, self.root/'checker', lambda _: None)
        pending = self.healthy_status()
        self.assertEqual((pending['desiredMode'], pending['appliedMode'], pending['state']), ('legacy', 'https', 'ready'))
        self.assertTrue(pending['pendingModeChange'])
        self.assertEqual(self.settings.installation(), {'mode': 'legacy'})
        self.assertEqual(self.installed.read_text(), original)
        self.applied.write_text('{"mode":"legacy"}')
        with patch.object(portal_settings.subprocess, 'run', side_effect=AssertionError('legacy must not probe HTTPS')):
            applied = self.settings.status()
        self.assertEqual(applied['state'], 'legacy')
        self.assertFalse(applied['pendingModeChange'])
        self.assertIsNone(applied['installed'])
        self.assertEqual(self.installed.read_text(), original)

    def test_selecting_https_keeps_legacy_live_and_can_reuse_installed_certificate(self):
        self.installed.write_text(json.dumps(self.certificate_metadata()))
        self.applied.write_text('{"mode":"legacy"}')
        self.settings.select_mode('https')
        with patch.object(portal_settings.subprocess, 'run', side_effect=AssertionError('pending HTTPS is not live')):
            pending = self.settings.status()
            installation = self.settings.installation()
        self.assertEqual(installation, {'mode': 'https', 'hostname': '', 'directory': ''})
        self.assertEqual((pending['desiredMode'], pending['appliedMode'], pending['state']), ('https', 'legacy', 'legacy'))
        self.assertTrue(pending['pendingModeChange'])
        self.applied.write_text('{"mode":"https"}')
        applied = self.healthy_status()
        self.assertEqual(applied['state'], 'ready')
        self.assertFalse(applied['pendingModeChange'])

    def test_explicit_legacy_recovers_damaged_or_expired_saved_certificates_and_mode(self):
        self.settings.select_mode('legacy')
        candidate = self.settings.directory/'candidate.json'
        preserved = self.settings.directory/'certificate-original'
        preserved.mkdir()
        key = preserved/'privkey.pem'
        key.write_text('preserve private certificate material')
        for metadata in ('{broken', json.dumps(self.certificate_metadata(days=-5))):
            with self.subTest(metadata=metadata):
                candidate.write_text(metadata)
                self.installed.write_text(metadata)
                self.applied.write_text('{broken')
                with patch.object(portal_settings.subprocess, 'run', side_effect=AssertionError('legacy must bypass certificate checks')):
                    self.assertEqual(self.settings.installation(), {'mode': 'legacy'})
                    unknown = self.settings.status()
                    self.assertEqual(unknown['state'], 'error')
                    self.assertEqual(unknown['desiredMode'], 'legacy')
                    self.applied.write_text('{"mode":"legacy"}')
                    applied = self.settings.status()
                self.assertEqual(applied['state'], 'legacy')
                self.assertFalse(applied['pendingModeChange'])
                self.assertEqual(candidate.read_text(), metadata)
                self.assertEqual(self.installed.read_text(), metadata)
                self.assertEqual(key.read_text(), 'preserve private certificate material')

    def test_invalid_or_failed_mode_save_preserves_previous_choice(self):
        self.applied.write_text('{"mode":"legacy"}')
        self.settings.select_mode('legacy')
        for invalid in ('preserve', 'auto', None, True):
            with self.subTest(invalid=invalid), self.assertRaises(ValueError):
                self.settings.select_mode(invalid)
        with patch.object(portal_settings.os, 'replace', side_effect=OSError('disk unavailable')):
            with self.assertRaises(OSError):
                self.settings.select_mode('https')
        self.assertEqual(self.settings.installation(), {'mode': 'legacy'})
        self.assertEqual(self.settings.status()['desiredMode'], 'legacy')
        self.assertEqual(list(self.settings.directory.glob('mode-*')), [])
        self.assertEqual((self.settings.directory/'mode.json').stat().st_mode & 0o777, 0o600)

    def test_unknown_mode_metadata_never_silently_falls_back(self):
        self.settings.directory.mkdir()
        selected = self.settings.directory/'mode.json'
        for invalid in ('{broken', '{"mode":"automatic"}', '[]'):
            with self.subTest(invalid=invalid):
                selected.write_text(invalid)
                self.assertEqual(self.settings.status()['state'], 'error')
                with self.assertRaises(ValueError):
                    self.settings.installation()
        selected.unlink()
        self.installed.write_text(json.dumps(self.certificate_metadata()))
        self.applied.write_text('{broken')
        with self.assertRaises(ValueError):
            self.settings.installation()
        self.settings.select_mode('https')
        self.assertEqual(self.settings.installation(), {'mode': 'https', 'hostname': '', 'directory': ''})

    def test_backward_compatible_defaults_and_certificate_renewal_keep_live_state(self):
        self.assertEqual(self.settings.installation(), {'mode': 'legacy'})
        self.settings.directory.mkdir()
        candidate = self.settings.directory/'candidate.json'
        candidate.write_text(json.dumps(self.certificate_metadata(days=60)))
        pending = self.settings.status()
        self.assertEqual((pending['desiredMode'], pending['appliedMode'], pending['state']), ('https', 'legacy', 'legacy'))
        self.assertTrue(pending['pendingModeChange'])
        self.installed.write_text(json.dumps(self.certificate_metadata(days=40)))
        installed = self.healthy_status()
        self.assertEqual((installed['desiredMode'], installed['appliedMode'], installed['state']), ('https', 'https', 'ready'))
        self.assertFalse(installed['pendingModeChange'])
        self.assertTrue(installed['pendingCertificateChange'])

    def test_new_local_profile_keeps_explicit_installed_legacy_mode_despite_saved_cert(self):
        self.installed.write_text(json.dumps(self.certificate_metadata()))
        self.applied.write_text('{"mode":"legacy"}')
        self.settings.directory.mkdir()
        (self.settings.directory/'candidate.json').write_text(json.dumps(self.certificate_metadata()))
        with patch.object(portal_settings.subprocess, 'run', side_effect=AssertionError('saved cert must not re-enable HTTPS')):
            self.assertEqual(self.settings.installation(), {'mode': 'legacy'})
            status = self.settings.status()
        self.assertEqual((status['desiredMode'], status['appliedMode'], status['state']), ('legacy', 'legacy', 'legacy'))
        self.assertFalse(status['pendingModeChange'])


if __name__=='__main__':
    unittest.main()
