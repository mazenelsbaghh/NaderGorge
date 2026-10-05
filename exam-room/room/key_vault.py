"""Read the Gemini key from the local data folder, with legacy Keychain fallback."""
import logging
import os
from pathlib import Path
import re
import tempfile

try:
    import keyring
    from keyring.errors import KeyringError
except ImportError:
    keyring = None
    KeyringError = ()


SERVICE = 'Massar Exam Room'
USERNAME = 'Gemini API'
KEY_NAME = 'GEMINI_API_KEY'
KEY_LINE = re.compile(r'^\s*GEMINI_API_KEY\s*=')


def valid_key(key):
    return isinstance(key, str) and 10 <= len(key) <= 200 and not any(char.isspace() for char in key)


class KeyVault:
    def __init__(self, folder):
        self.path = Path(folder).resolve() / '.env'
        self.key = None

    def _lines(self):
        if self.path.is_symlink():
            raise OSError('The settings file must not be a symbolic link')
        if not self.path.exists():
            return []
        if self.path.stat().st_size > 65536:
            raise OSError('The settings file is too large')
        return self.path.read_text(encoding='utf-8').splitlines()

    def _env_key(self):
        for line in self._lines():
            if KEY_LINE.match(line):
                value = line.split('=', 1)[1].strip().strip('"\'')
                return value if valid_key(value) else None
        return None

    def configured(self):
        return bool(self._env_key())

    def load(self):
        key = self._env_key()
        if key:
            self.key = key
            return key
        if self.key:
            return self.key
        if keyring is not None:
            try:
                self.key = keyring.get_password(SERVICE, USERNAME)
            except KeyringError:
                logging.warning('OS credential store was unavailable')
        return self.key

    def save(self, key):
        if not valid_key(key):
            raise ValueError('Invalid Gemini API key')
        lines = [line for line in self._lines() if not KEY_LINE.match(line)]
        lines.append(f'{KEY_NAME}={key}')
        self.path.parent.mkdir(parents=True, exist_ok=True)
        name = None
        try:
            with tempfile.NamedTemporaryFile('w', encoding='utf-8', dir=self.path.parent,
                                             prefix='.env-', delete=False) as target:
                name = target.name
                os.chmod(name, 0o600)
                target.write('\n'.join(lines) + '\n')
                target.flush()
                os.fsync(target.fileno())
            os.replace(name, self.path)
            os.chmod(self.path, 0o600)
        finally:
            if name and os.path.exists(name):
                os.unlink(name)
        self.key = key
