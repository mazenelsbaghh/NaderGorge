from __future__ import annotations

import importlib.util
import json
import os
import subprocess
import sys
from pathlib import Path
from types import SimpleNamespace

import pytest


ROOT = Path(__file__).resolve().parents[3]
SCRIPTS = ROOT / "deploy/production/scripts"
sys.path.insert(0, str(SCRIPTS))

COLLECT_SPEC = importlib.util.spec_from_file_location(
    "collect_backup_gate_inputs",
    SCRIPTS / "collect_backup_gate_inputs.py",
)
assert COLLECT_SPEC and COLLECT_SPEC.loader
collector = importlib.util.module_from_spec(COLLECT_SPEC)
COLLECT_SPEC.loader.exec_module(collector)

TRANSPORT_SPEC = importlib.util.spec_from_file_location(
    "strict_ssh_transport_under_test",
    SCRIPTS / "ssh_transport.py",
)
assert TRANSPORT_SPEC and TRANSPORT_SPEC.loader
transport_module = importlib.util.module_from_spec(TRANSPORT_SPEC)
sys.modules[TRANSPORT_SPEC.name] = transport_module
TRANSPORT_SPEC.loader.exec_module(transport_module)

LABEL = "20260727-120100F"
SNAPSHOT = "a" * 64


def inventory():
    return SimpleNamespace(
        cluster={"name": "massar-production", "ssh_user": "massar-ops"},
        nodes=(
            SimpleNamespace(
                id="node-1",
                public_address="192.0.2.1",
            ),
            SimpleNamespace(
                id="node-2",
                public_address="192.0.2.2",
            ),
            SimpleNamespace(
                id="node-3",
                public_address="192.0.2.3",
            ),
        ),
    )


class FakeTransport:
    def __init__(self, *, fail_fetch: bool = False) -> None:
        self.fail_fetch = fail_fetch
        self.runs: list[tuple[object, tuple[str, ...], bool]] = []
        self.fetches: list[tuple[str, Path]] = []

    def run(self, target, argv, *, timeout_seconds=60, check=True):
        self.runs.append((target, tuple(argv), check))
        return subprocess.CompletedProcess(argv, 0, "", "")

    def fetch(self, target, source, destination, *, timeout_seconds=60, max_bytes):
        self.fetches.append((source, destination))
        if self.fail_fetch:
            raise transport_module.SshTransportError("injected fetch failure")
        payload = json.dumps({"status": "success", "source": source}).encode()
        destination.write_bytes(payload)
        os.chmod(destination, 0o640)
        return len(payload)


def test_allowlist_constructs_only_four_identity_paths_and_rejects_traversal() -> None:
    sources = collector.remote_sources(LABEL, SNAPSHOT)
    assert sources == {
        "database-backup.json":
            f"/var/lib/massar/evidence/backup/database-{LABEL}.json",
        "database-restore.json":
            f"/var/lib/massar/evidence/restore/database-{LABEL}.json",
        "file-backup.json":
            f"/srv/massar-shared/.cluster-health/file-backup-{SNAPSHOT}.json",
        "file-restore.json":
            f"/var/lib/massar/evidence/restore/files-{SNAPSHOT}.json",
    }
    with pytest.raises(collector.CollectionError, match="pgBackRest"):
        collector.remote_sources("../../root", SNAPSHOT)
    with pytest.raises(collector.CollectionError, match="Restic"):
        collector.remote_sources(LABEL, "../snapshot")


def test_cli_requires_explicit_dry_run_or_yes() -> None:
    source = (SCRIPTS / "collect_backup_gate_inputs.py").read_text(encoding="utf-8")
    assert "add_mutually_exclusive_group(required=True)" in source
    assert 'approval.add_argument("--dry-run"' in source
    assert 'approval.add_argument("--yes"' in source
    assert '"sshAttempted": False' in source


def test_collection_rejects_wrong_node_and_output_symlink(tmp_path: Path) -> None:
    fake = FakeTransport()
    with pytest.raises(collector.CollectionError, match="inventory member"):
        collector.collect(
            transport=fake,
            inventory=inventory(),
            node_id="node-9",
            database_backup_id=LABEL,
            file_snapshot_id=SNAPSHOT,
            output_dir=tmp_path / "unused",
        )
    assert fake.runs == []

    target = tmp_path / "output"
    target.symlink_to(tmp_path / "elsewhere")
    with pytest.raises(collector.CollectionError, match="symlink"):
        collector.collect(
            transport=fake,
            inventory=inventory(),
            node_id="node-1",
            database_backup_id=LABEL,
            file_snapshot_id=SNAPSHOT,
            output_dir=target,
        )
    assert fake.runs == []


def test_collection_fetches_four_files_writes_hashes_and_cleans_remote(
    tmp_path: Path,
) -> None:
    fake = FakeTransport()
    output = tmp_path / "collected"
    result = collector.collect(
        transport=fake,
        inventory=inventory(),
        node_id="node-2",
        database_backup_id=LABEL,
        file_snapshot_id=SNAPSHOT,
        output_dir=output,
    )
    assert result["status"] == "success"
    assert result["nodeId"] == "node-2"
    assert set(result["files"]) == set(collector.LOCAL_NAMES)
    assert len(fake.fetches) == 4
    assert all((output / name).is_file() for name in collector.LOCAL_NAMES)
    assert (output / "collection-evidence.json").is_file()
    for name, metadata in result["files"].items():
        assert metadata["sha256"] == collector.sha256_file(output / name)
        assert metadata["bytes"] == (output / name).stat().st_size
    assert len(fake.runs) == 2
    assert fake.runs[0][0].node_id == "node-2"
    assert fake.runs[-1][2] is False
    cleanup = fake.runs[-1][1][-1]
    assert "sudo /bin/rm -f --" in cleanup
    assert "sudo /usr/bin/rmdir --" in cleanup


def test_remote_cleanup_runs_after_fetch_failure_and_local_partial_is_removed(
    tmp_path: Path,
) -> None:
    fake = FakeTransport(fail_fetch=True)
    output = tmp_path / "failed"
    with pytest.raises(collector.CollectionError, match="injected fetch failure"):
        collector.collect(
            transport=fake,
            inventory=inventory(),
            node_id="node-1",
            database_backup_id=LABEL,
            file_snapshot_id=SNAPSHOT,
            output_dir=output,
        )
    assert len(fake.runs) == 2
    assert fake.runs[-1][2] is False
    assert not output.exists()
    assert not list(tmp_path.glob(".failed.*"))


def strict_transport(tmp_path: Path):
    known_hosts = tmp_path / "known_hosts"
    identity = tmp_path / "id_ed25519"
    known_hosts.write_text("host key\n")
    identity.write_text("private\n")
    identity.chmod(0o600)
    return transport_module.StrictSshTransport(known_hosts, identity)


def test_fetch_rejects_traversal_symlink_and_existing_destination(
    tmp_path: Path,
) -> None:
    transport = strict_transport(tmp_path)
    target = transport_module.SshTarget("node-1", "192.0.2.1", "massar-ops")
    with pytest.raises(transport_module.SshTransportError, match="normalized"):
        transport.fetch(target, "/tmp/../root", tmp_path / "traversal.json")

    existing = tmp_path / "existing.json"
    existing.write_text("{}")
    with pytest.raises(transport_module.SshTransportError, match="must not exist"):
        transport.fetch(target, "/tmp/evidence.json", existing)

    symlink = tmp_path / "symlink.json"
    symlink.symlink_to(existing)
    with pytest.raises(transport_module.SshTransportError, match="symlink"):
        transport.fetch(target, "/tmp/evidence.json", symlink)


def test_fetch_is_atomic_bounded_and_mode_0640(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    transport = strict_transport(tmp_path)
    target = transport_module.SshTarget("node-1", "192.0.2.1", "massar-ops")

    def fake_run(argv, **kwargs):
        Path(argv[-1]).write_bytes(b'{"status":"success"}')
        return subprocess.CompletedProcess(argv, 0, "", "")

    monkeypatch.setattr(transport_module.subprocess, "run", fake_run)
    destination = tmp_path / "fetched.json"
    size = transport.fetch(
        target,
        "/tmp/massar-backup-gate-abc/evidence.json",
        destination,
        max_bytes=100,
    )
    assert size == destination.stat().st_size
    assert destination.stat().st_mode & 0o777 == 0o640
    assert not list(tmp_path.glob(".fetched.json.*.fetch"))

    def oversized_run(argv, **kwargs):
        Path(argv[-1]).write_bytes(b"x" * 101)
        return subprocess.CompletedProcess(argv, 0, "", "")

    monkeypatch.setattr(transport_module.subprocess, "run", oversized_run)
    oversized = tmp_path / "oversized.json"
    with pytest.raises(transport_module.SshTransportError, match="between 1 and 100"):
        transport.fetch(
            target,
            "/tmp/massar-backup-gate-abc/evidence.json",
            oversized,
            max_bytes=100,
        )
    assert not oversized.exists()
