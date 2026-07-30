from __future__ import annotations

import importlib.util
import stat
import subprocess
import sys
from pathlib import Path

import pytest


ROOT = Path(__file__).resolve().parents[4]
SCRIPTS = ROOT / "deploy/production/scripts"
sys.path.insert(0, str(SCRIPTS))
spec = importlib.util.spec_from_file_location("ssh_transport", SCRIPTS / "ssh_transport.py")
assert spec and spec.loader
ssh_transport = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = ssh_transport
spec.loader.exec_module(ssh_transport)


def transport(tmp_path: Path):
    known_hosts = tmp_path / "known_hosts"
    identity = tmp_path / "id_ed25519"
    known_hosts.write_text("pinned-host-key\n")
    identity.write_text("test-key-material\n")
    identity.chmod(stat.S_IRUSR | stat.S_IWUSR)
    return ssh_transport.StrictSshTransport(known_hosts, identity)


def test_transport_enforces_pinned_batch_key_only_ssh(tmp_path: Path) -> None:
    arguments = transport(tmp_path).base_args()
    joined = " ".join(arguments)
    assert "BatchMode=yes" in joined
    assert "IdentitiesOnly=yes" in joined
    assert "StrictHostKeyChecking=yes" in joined
    assert "UserKnownHostsFile=" in joined
    assert "StrictHostKeyChecking=" + "no" not in joined


def test_transport_rejects_permissive_private_key(tmp_path: Path) -> None:
    known_hosts = tmp_path / "known_hosts"
    identity = tmp_path / "id_ed25519"
    known_hosts.write_text("pinned\n")
    identity.write_text("test\n")
    identity.chmod(0o644)
    with pytest.raises(ssh_transport.SshTransportError, match="permissions"):
        ssh_transport.StrictSshTransport(known_hosts, identity)


def test_remote_arguments_are_shell_quoted_and_output_is_captured(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    captured: dict[str, object] = {}

    def fake_run(argv, **kwargs):
        captured["argv"] = argv
        return subprocess.CompletedProcess(argv, 0, "ok", "")

    monkeypatch.setattr(ssh_transport.subprocess, "run", fake_run)
    target = ssh_transport.SshTarget("node-1", "approved.example", "massar-ops")
    result = transport(tmp_path).run(target, ("printf", "%s", "value with spaces"))
    assert result.stdout == "ok"
    assert captured["argv"][-1] == "printf %s 'value with spaces'"


def test_skill_requires_dry_run_confirmation_and_non_root_operation() -> None:
    skill = (ROOT / ".agents/skills/ssh-server/SKILL.md").read_text(encoding="utf-8")
    assert "--dry-run" in skill
    assert "--yes" in skill
    assert "massar-ops" in skill
    assert "Strict host-key verification is mandatory" in skill
    assert "restore against the production DB" in skill
