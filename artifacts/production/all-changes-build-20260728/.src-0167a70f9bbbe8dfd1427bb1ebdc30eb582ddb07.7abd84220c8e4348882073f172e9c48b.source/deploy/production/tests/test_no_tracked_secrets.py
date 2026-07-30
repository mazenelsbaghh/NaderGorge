from __future__ import annotations

import re
import subprocess
from pathlib import Path


ROOT = Path(__file__).resolve().parents[3]
FORBIDDEN = {
    "sshpass": re.compile(r"\bsshpass\b"),
    "disabled host verification": re.compile(r"StrictHostKeyChecking\s*=\s*no", re.I),
    "embedded private key": re.compile(r"BEGIN (?:OPENSSH|RSA|EC) PRIVATE KEY"),
    "literal password assignment": re.compile(r"(?im)^\s*(?:password|passwd)\s*=\s*[\"'][^\"'\r\n]+[\"']"),
}
SCAN_PREFIXES = ("deploy/", ".agents/skills/ssh-server/")


def tracked_files() -> list[Path]:
    output = subprocess.check_output(
        ["git", "ls-files", "--cached", "--others", "--exclude-standard", *SCAN_PREFIXES],
        cwd=ROOT,
        text=True,
    )
    return [ROOT / line for line in output.splitlines() if line]


def test_cluster_operations_contain_no_tracked_secret_or_unsafe_ssh_pattern() -> None:
    findings: list[str] = []
    for path in tracked_files():
        if not path.is_file() or path.suffix in {".png", ".jpg", ".gif", ".pyc"}:
            continue
        if path.resolve() == Path(__file__).resolve():
            continue
        text = path.read_text(encoding="utf-8", errors="ignore")
        for label, pattern in FORBIDDEN.items():
            if pattern.search(text):
                findings.append(f"{path.relative_to(ROOT)}: {label}")
    assert findings == []
