"""Local certificate selection and verified status for the exam's HTTPS portal."""
from datetime import datetime, timezone
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import tempfile


INSTALLED_METADATA = Path('/var/db/massar-exam-room/capport-public.json')
INSTALLED_MODE = Path('/var/db/massar-exam-room/portal-mode.json')
HOSTNAME = re.compile(r'(?=.{1,253}$)(?:[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?\.)+[a-z][a-z0-9-]{1,62}')


def certificate_check(binary, hostname, directory):
    checked = subprocess.run([str(binary), '--check-capport', '--capport-hostname', hostname,
                              '--capport-cert', str(directory / 'fullchain.pem'),
                              '--capport-key', str(directory / 'privkey.pem')],
                             capture_output=True, text=True, timeout=15, check=False)
    if checked.returncode:
        raise ValueError('الشهادة غير صالحة لهذا العنوان أو غير موثوقة أو منتهية. اختر fullchain.pem والمفتاح المطابق لها.')
    metadata = json.loads(checked.stdout)
    if metadata.get('hostname') != hostname or not metadata.get('expiresAt'):
        raise ValueError('تعذر قراءة صلاحية الشهادة من خدمة البوابة.')
    return metadata


def read_metadata(path):
    try:
        metadata = json.loads(path.read_text())
        if not isinstance(metadata, dict) or not HOSTNAME.fullmatch(metadata.get('hostname', '')):
            raise ValueError('invalid portal metadata')
        expiry = datetime.fromisoformat(metadata['expiresAt'].replace('Z', '+00:00'))
        days = int((expiry - datetime.now(timezone.utc)).total_seconds() // 86400)
        return {**metadata, 'daysRemaining': days, 'expired': expiry <= datetime.now(timezone.utc)}
    except FileNotFoundError:
        return None


def probe_https_portal(binary, hostname):
    checked = subprocess.run([str(binary), '--check-portal', '--capport-hostname', hostname],
                             capture_output=True, text=True, timeout=8, check=False)
    if checked.returncode:
        return False
    health = json.loads(checked.stdout)
    return health.get('ready') is True and health.get('hostname') == hostname


def read_mode(path):
    try:
        settings = json.loads(path.read_text())
    except FileNotFoundError:
        return None
    if not isinstance(settings, dict) or settings.get('mode') not in ('legacy', 'https'):
        raise ValueError('إعداد طريقة الدخول غير صالح؛ اختر الطريقة من جديد قبل تجهيز الشبكة.')
    return settings['mode']


class PortalSettings:
    def __init__(self, directory, binary, choose_file):
        self.directory = Path(directory) / 'portal'
        self.binary = Path(binary)
        self.choose_file = choose_file

    def _desired_mode(self):
        selected = read_mode(self.directory / 'mode.json')
        if selected:
            return selected
        # A root-installed explicit choice also applies to a fresh local profile.
        # Only an explicit local selection can repair unknown root metadata.
        installed_mode = read_mode(INSTALLED_MODE)
        if installed_mode:
            return installed_mode
        return 'https' if (self.directory / 'candidate.json').exists() or INSTALLED_METADATA.exists() else 'legacy'

    def _applied_mode(self):
        return read_mode(INSTALLED_MODE) or ('https' if INSTALLED_METADATA.exists() else 'legacy')

    def select_mode(self, mode):
        if mode not in ('legacy', 'https'):
            raise ValueError('اختر الدخول التقليدي أو الدخول بشهادة HTTPS.')
        self.directory.mkdir(parents=True, exist_ok=True, mode=0o700)
        staged = None
        try:
            with tempfile.NamedTemporaryFile(mode='w', dir=self.directory, prefix='mode-', delete=False) as output:
                staged = Path(output.name)
                json.dump({'mode': mode}, output)
                output.write('\n')
            os.replace(staged, self.directory / 'mode.json')
        finally:
            if staged is not None:
                staged.unlink(missing_ok=True)
        return {'saved': True, 'mode': mode}

    def import_certificate(self, hostname):
        hostname = str(hostname).strip().lower()
        if not HOSTNAME.fullmatch(hostname):
            raise ValueError('اكتب اسم النطاق فقط، مثل exam.massar-academy.net، بدون https أو مسار.')
        certificate = self.choose_file('اختر شهادة البوابة الكاملة fullchain.pem')
        if not certificate:
            return {'saved': False}
        key = self.choose_file('اختر المفتاح الخاص privkey.pem')
        if not key:
            return {'saved': False}
        self.directory.mkdir(parents=True, exist_ok=True, mode=0o700)
        destination = Path(tempfile.mkdtemp(prefix='certificate-', dir=self.directory))
        try:
            for source, name in [(certificate, 'fullchain.pem'), (key, 'privkey.pem')]:
                source = Path(source)
                if source.stat().st_size > 262144:
                    raise ValueError('ملف الشهادة أو المفتاح أكبر من الحجم المتوقع.')
                with source.open('rb') as incoming, (destination / name).open('xb') as outgoing:
                    os.chmod(destination / name, 0o600)
                    shutil.copyfileobj(incoming, outgoing)
            metadata = certificate_check(self.binary, hostname, destination)
            candidate = self.directory / 'candidate.json'
            staged = self.directory / 'candidate.next.json'
            staged.write_text(json.dumps({**metadata, 'directory': str(destination)}) + '\n')
            os.chmod(staged, 0o600)
            os.replace(staged, candidate)
        except (OSError, ValueError, subprocess.SubprocessError):
            shutil.rmtree(destination)
            raise
        return {'saved': True, 'hostname': hostname, 'expiresAt': metadata['expiresAt']}

    def installation(self):
        mode = self._desired_mode()
        # Switching back to local HTTP must work even if a saved certificate
        # has expired or its selection metadata is damaged. Keep it for reuse.
        if mode == 'legacy':
            return {'mode': mode}
        candidate = read_metadata(self.directory / 'candidate.json')
        if candidate is None:
            # Only the privileged installer can validate the root-owned key.
            return {'mode': mode, 'hostname': '', 'directory': ''}
        directory = Path(candidate['directory']).resolve()
        if directory.parent != self.directory.resolve() or not directory.name.startswith('certificate-'):
            raise ValueError('مسار شهادة البوابة غير صالح؛ أعد اختيارها.')
        metadata = certificate_check(self.binary, candidate['hostname'], directory)
        return {**metadata, 'directory': str(directory), 'mode': mode}

    def status(self):
        desired_mode, applied_mode = None, None
        try:
            desired_mode = self._desired_mode()
            applied_mode = self._applied_mode()
        except (OSError, ValueError, KeyError, TypeError):
            return {'state': 'error', 'desiredMode': desired_mode, 'appliedMode': applied_mode,
                    'pendingModeChange': None, 'phoneVerified': False,
                    'detail': 'تعذر قراءة طريقة الدخول المحفوظة. اختر الطريقة من جديد؛ لو استمرت المشكلة أعد تجهيز الشبكة.'}
        modes = {'desiredMode': desired_mode, 'appliedMode': applied_mode,
                 'pendingModeChange': desired_mode != applied_mode}
        try:
            candidate = read_metadata(self.directory / 'candidate.json')
        except (OSError, ValueError, KeyError, TypeError):
            # A damaged unused candidate must not misrepresent the live mode.
            # installation() still rejects it when applying HTTPS.
            candidate = None
        public = {'candidate': {key: candidate[key] for key in ('hostname', 'expiresAt', 'daysRemaining')} if candidate else None,
                  'installed': None, 'phoneVerified': False, 'pendingCertificateChange': False, **modes}
        if applied_mode == 'legacy':
            return {**public, 'state': 'legacy', 'detail': 'طريقة الدخول الحالية هي الدخول التقليدي بالعنوان المحلي، بدون شهادة HTTPS.'}
        try:
            installed = read_metadata(INSTALLED_METADATA)
        except (OSError, ValueError, KeyError, TypeError):
            return {**public, 'state': 'error', 'detail': 'تعذر قراءة شهادة البوابة الحالية؛ اختر شهادة صحيحة أو اختر الدخول التقليدي ثم جهّز الشبكة.'}
        public['installed'] = installed
        if not installed:
            return {**public, 'state': 'error', 'detail': 'طريقة HTTPS مفعّلة لكن بيانات الشهادة غير موجودة. أعد تجهيز الشبكة بشهادة صحيحة أو اختر الدخول التقليدي.'}
        public['pendingCertificateChange'] = bool(desired_mode == 'https' and candidate and (candidate['hostname'] != installed['hostname'] or candidate['expiresAt'] != installed['expiresAt']))
        if installed['expired']:
            return {**public, 'state': 'expired', 'detail': 'شهادة البوابة انتهت. جدّدها؛ استخدم رابط الطلاب المحلي لحين التحديث.'}
        try:
            ready = probe_https_portal(self.binary, installed['hostname'])
        except (OSError, ValueError, subprocess.SubprocessError):
            ready = False
        if not ready:
            return {**public, 'state': 'unreachable', 'detail': 'بوابة HTTPS لا تستجيب بشكل موثوق؛ افحص الخدمة والكابل والشهادة.'}
        if installed['daysRemaining'] < 21:
            return {**public, 'state': 'expiring', 'detail': f'البوابة تعمل، والشهادة تنتهي خلال {installed["daysRemaining"]} يومًا. جدّدها قبل الامتحان.'}
        return {**public, 'state': 'ready', 'detail': 'بوابة HTTPS وخدمة الامتحان تستجيبان. ظهور الإشعار يحتاج تجربة الموبايلات.'}
