"""Deterministic boundaries outside the model's editable workspace."""
from __future__ import annotations

import hashlib
import re
from pathlib import Path

ALLOWED_ROOTS = ('frontend/src/', 'frontend/tests/', 'backend/src/', 'backend/tests/', 'worker/src/')
CRITICAL = re.compile(r'auth|permission|security|ratelimit|session|cookie|payment|billing|financ|refund|wallet|settlement|migration|appdbcontext|\.csproj$|program\.cs$|/entities/|/configuration|aut[o-]?repair|auto-repair', re.I)
SENSITIVE_CHANGE = re.compile(r'authorize|allowanonymous|permission|requireauthorization|isadmin|jwt|password|secret|credential|ratelimit|permitlimit|\bcors\b|\bcsrf\b|content-security-policy|\brole\b|\bclaims?\b|refund|wallet|balance|ExecuteDelete|RemoveRange|DELETE\s+FROM|DROP\s+TABLE|TRUNCATE', re.I)


def redact(text: str) -> str:
    text = re.sub(r'(?i)(token|secret|password|authorization|cookie|api[_-]?key)["\s]*[:=]\s*[^\r\n,;]+', r'\1=[redacted]', text[:12000])
    text = re.sub(r'\b(?:\d{1,3}\.){3}\d{1,3}\b', '[redacted-ip]', text)
    return re.sub(r'https?://\S+|[\w.+-]+@[\w.-]+\.[A-Za-z]{2,}|\b(?:\+?20)?01[0125]\d{8}\b', '[redacted]', text)


def assess_patch(paths: list[str], workspace: Path, patch: bytes = b'') -> list[str]:
    if not paths or len(paths) > 40:
        raise ValueError('A repair must change between 1 and 40 files')
    critical = []
    sensitive = any(SENSITIVE_CHANGE.search(line[1:]) for line in patch.decode('utf-8', errors='replace').splitlines()
                    if line.startswith(('+', '-')) and not line.startswith(('+++', '---')))
    for relative in paths:
        if not relative.startswith(ALLOWED_ROOTS) or '..' in Path(relative).parts or re.search(r'[\x00-\x1f]', relative):
            raise ValueError(f'Change outside application boundary: {relative}')
        resolved = workspace / relative
        if resolved.is_symlink() or (resolved.exists() and not resolved.is_file()):
            raise ValueError('Only regular source files may change')
        if resolved.exists() and not resolved.resolve().is_relative_to(workspace.resolve()):
            raise ValueError('Change escapes workspace')
        if CRITICAL.search(relative) or sensitive:
            critical.append(relative)
    return critical


def patch_hash(patch: bytes, baseline: str) -> str:
    return hashlib.sha256(baseline.encode() + b'\0' + patch).hexdigest()


def validate_review(review: dict) -> None:
    if set(review) != {'summary', 'safeToDeploy', 'critical', 'reproduction', 'verification'}:
        raise ValueError('Invalid independent review response')
    if review['safeToDeploy'] is not True:
        raise ValueError('Independent review did not accept this repair')
    if type(review['critical']) is not bool or not all(isinstance(review[k], str) and review[k].strip() for k in ('summary', 'reproduction', 'verification')):
        raise ValueError('Review lacks reproduction or verification evidence')
