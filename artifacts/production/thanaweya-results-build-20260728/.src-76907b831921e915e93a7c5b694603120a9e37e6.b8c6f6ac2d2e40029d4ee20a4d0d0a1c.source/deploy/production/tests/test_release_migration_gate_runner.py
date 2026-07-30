from __future__ import annotations

import datetime as dt
import importlib.util
import json
import subprocess
import sys
from dataclasses import dataclass
from pathlib import Path

import pytest


ROOT = Path(__file__).resolve().parents[3]
SCRIPTS = ROOT / "deploy/production/scripts"
sys.path.insert(0, str(SCRIPTS))


def load(name: str):
    spec = importlib.util.spec_from_file_location(name, SCRIPTS / f"{name}.py")
    assert spec and spec.loader
    module = importlib.util.module_from_spec(spec)
    sys.modules[name] = module
    spec.loader.exec_module(module)
    return module


runner = load("prepare_release_migration_gate")
runner.load_local_dependencies()
contract = sys.modules["release_contract"]
RELEASE = "git-" + "a" * 40
CURRENT = "prod-20260726-166-r1"
NOW = dt.datetime(2026, 7, 27, 12, tzinfo=dt.timezone.utc)


def manifest(path: Path) -> Path:
    path.write_text(json.dumps({
        "schemaVersion": 1,
        "releaseId": RELEASE,
        "gitCommit": "a" * 40,
        "sourceStateSha256": "b" * 64,
        "dirtySourceSnapshot": False,
        "createdAt": "2026-07-27T11:00:00Z",
        "platform": "linux/amd64",
        "images": {
            name: f"sha256:{index:064x}"
            for index, name in enumerate(contract.IMAGES, 1)
        },
        "status": "success",
        "nodeCount": 3,
        "digestParity": True,
        "distribution": {
            node: {"status": "verified", "releaseFilesSha256": "c" * 64}
            for node in contract.NODE_IDS
        },
    }), encoding="utf-8")
    return path


def inventory(monkeypatch: pytest.MonkeyPatch, tmp_path: Path):
    known_hosts = tmp_path / "known-hosts"
    identity = tmp_path / "identity"
    known_hosts.write_text("pinned", encoding="utf-8")
    identity.write_text("private", encoding="utf-8")
    identity.chmod(0o600)
    monkeypatch.setenv("MASSAR_KNOWN_HOSTS_FILE", str(known_hosts))
    monkeypatch.setenv("MASSAR_SSH_IDENTITY_FILE", str(identity))
    return runner.load_inventory(
        ROOT / "deploy/production/inventory/production.yml"
    )


def gate_payload(manifest_path: Path) -> dict[str, object]:
    return {
        "schemaVersion": 1,
        "status": "success",
        "clusterId": "massar-production",
        "releaseId": RELEASE,
        "manifestSha256": contract.file_sha256(manifest_path),
        "currentReleaseId": CURRENT,
        "currentManifestSha256": "1" * 64,
        "databaseSystemIdentifier": "7586552109940137719",
        "databaseBackupId": "20260727-110000F",
        "databaseRestoreId": "restore-gate-" + "1" * 32,
        "backupCapturedAt": "2026-07-27T11:10:00Z",
        "restoreCapturedAt": "2026-07-27T11:30:00Z",
        "validatedAt": "2026-07-27T11:40:00Z",
        "backupEncrypted": True,
        "restoreIsolated": True,
        "restoreChecksumVerified": True,
        "restoredCopyMigrationVerified": True,
        "realDataValidationVerified": True,
        "nMinusOneCompatibilityVerified": True,
        "sourceDatabaseTableCountsSha256": "d" * 64,
        "restoredDatabaseTableCountsSha256": "d" * 64,
        "preMigrationIdsSha256": "e" * 64,
        "postMigrationIdsSha256": "f" * 64,
        "postMigrationSchemaSha256": "2" * 64,
    }


@dataclass
class Result:
    returncode: int = 0
    stdout: str = ""
    stderr: str = ""


class ProducerTransport:
    def __init__(
        self,
        payload: dict[str, object],
        *,
        primaries: set[str] | None = None,
        producer_returncode: int = 0,
    ) -> None:
        self.payload = payload
        self.primaries = {"node-2"} if primaries is None else primaries
        self.producer_returncode = producer_returncode
        self.commands: list[tuple[str, tuple[str, ...]]] = []

    def run(self, target, command, **kwargs):
        self.commands.append((target.node_id, command))
        if command[0] == "curl":
            return Result(returncode=0 if target.node_id in self.primaries else 22)
        if self.producer_returncode:
            return Result(
                returncode=self.producer_returncode,
                stderr="injected restore failure",
            )
        return Result(
            stdout=runner.GATE_PREFIX
            + json.dumps(self.payload, separators=(",", ":"))
            + "\n"
        )


def test_runner_uses_real_primary_operation_and_emits_consumer_valid_gate(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    manifest_path = manifest(tmp_path / "manifest.json")
    cluster = inventory(monkeypatch, tmp_path)
    transport = ProducerTransport(gate_payload(manifest_path))
    output = tmp_path / "migration-gate.json"

    payload = runner.prepare(
        inventory=cluster,
        transport=transport,
        release_id=RELEASE,
        manifest_path=manifest_path,
        output=output,
        now=NOW,
    )

    assert payload["databaseBackupId"] == "20260727-110000F"
    assert output.stat().st_mode & 0o777 == 0o640
    assert [node for node, _ in transport.commands[:3]] == [
        "node-1", "node-2", "node-3",
    ]
    assert transport.commands[3][0] == "node-2"
    remote_command = transport.commands[3][1]
    assert remote_command[:3] == (
        "sudo",
        "/usr/local/sbin/massar-produce-release-migration-gate",
        "--root-produce",
    )
    assert len(remote_command) == 7
    assert all("bash" not in argument for argument in remote_command)
    remote_script = runner.remote_producer_script(
        operation_id=remote_command[3],
        release_id=remote_command[4],
        manifest_sha256=remote_command[5],
        migrator_digest=remote_command[6],
    )
    assert "systemctl start --wait massar-pgbackrest-full.service" in remote_script
    assert "--type=immediate --target-action=promote restore" in remote_script
    assert '"massar/migrator:$release_id"' in remote_script
    assert "--user 0:0" in remote_script
    assert "setpriv --reuid=65532 --regid=65532 --clear-groups" in remote_script
    assert "Host=127.0.0.1;Port=6544" in remote_script
    assert "host.docker.internal" not in remote_script
    assert "pre_target_migration_count" not in remote_script
    assert "post_migration_count - pre_migration_count" in remote_script
    assert "pre_cluster_leases_count" in remote_script
    assert (
        'test "$post_cluster_leases_count" = "$pre_cluster_leases_count"'
        in remote_script
    )
    assert (
        "(select count(*) from cluster_leases) +"
        not in remote_script
    )
    assert "MASSAR_GATE_FAILURE" in remote_script
    assert "synchronous_commit=off" in remote_script
    assert "/^.(un)?restrict[[:space:]]/d" in remote_script
    assert "massar/backend:$current_release" in remote_script
    assert "massar/backend:$compatibility_release" in remote_script
    assert "api/health/ready" in remote_script
    assert remote_script.index('rm -rf --one-file-system "$restore_root"') < (
        remote_script.index(runner.GATE_PREFIX)
    )
    assert subprocess.run(
        ["bash", "-n"],
        input=remote_script,
        text=True,
        capture_output=True,
        check=False,
    ).returncode == 0
    loaded_manifest = contract.load_release_manifest(manifest_path, RELEASE)
    contract.load_migration_safety_gate(output, manifest=loaded_manifest, now=NOW)


def test_runner_can_bind_fresh_gate_to_an_explicit_n_minus_one_manifest(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    manifest_path = manifest(tmp_path / "manifest.json")
    compatibility_path = manifest(tmp_path / "compatibility.json")
    compatibility_value = json.loads(compatibility_path.read_text(encoding="utf-8"))
    compatibility_release = "git-" + "9" * 40
    compatibility_value["releaseId"] = compatibility_release
    compatibility_value["gitCommit"] = "9" * 40
    compatibility_path.write_text(
        json.dumps(compatibility_value),
        encoding="utf-8",
    )
    cluster = inventory(monkeypatch, tmp_path)
    payload = gate_payload(manifest_path)
    payload["currentReleaseId"] = compatibility_release
    payload["currentManifestSha256"] = contract.file_sha256(compatibility_path)
    transport = ProducerTransport(payload)

    runner.prepare(
        inventory=cluster,
        transport=transport,
        release_id=RELEASE,
        manifest_path=manifest_path,
        compatibility_manifest_path=compatibility_path,
        output=tmp_path / "rollback-gate.json",
        now=NOW,
    )

    remote_command = transport.commands[3][1]
    assert len(remote_command) == 10
    assert remote_command[7:] == (
        compatibility_release,
        contract.file_sha256(compatibility_path),
        compatibility_value["images"]["backend"],
    )
    remote_script = runner.remote_producer_script(
        operation_id=remote_command[3],
        release_id=remote_command[4],
        manifest_sha256=remote_command[5],
        migrator_digest=remote_command[6],
        compatibility_release_id=remote_command[7],
        compatibility_manifest_sha256=remote_command[8],
        compatibility_backend_digest=remote_command[9],
    )
    assert f'readonly requested_compatibility_release="{compatibility_release}"' in remote_script
    assert subprocess.run(
        ["bash", "-n"],
        input=remote_script,
        text=True,
        capture_output=True,
        check=False,
    ).returncode == 0


def test_root_helper_is_installed_and_only_fixed_validated_entry_is_allowlisted(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    bootstrap = (
        ROOT / "deploy/production/scripts/bootstrap_access.py"
    ).read_text(encoding="utf-8")
    installer = (
        ROOT / "deploy/production/scripts/manage_backup_bucket.py"
    ).read_text(encoding="utf-8")
    helper = "/usr/local/sbin/massar-produce-release-migration-gate"
    assert f"{helper} --root-produce *" in bootstrap
    assert f"/tmp/prepare_release_migration_gate.py {helper}" in installer
    sudoers = (
        ROOT
        / "deploy/production/config/sudoers/massar-release-migration-gate"
    ).read_text(encoding="utf-8")
    assert sudoers.strip() == (
        f"massar-ops ALL=(root) NOPASSWD: {helper} --root-produce *"
    )
    assert (
        "/tmp/massar-release-migration-gate.sudoers "
        "/etc/sudoers.d/massar-release-migration-gate"
    ) in installer
    assert "sudo bash" not in bootstrap
    monkeypatch.setattr(runner.os, "geteuid", lambda: 0)
    with pytest.raises(runner.GatePreparationError, match="arguments are invalid"):
        runner.root_main([
            "gate-" + "1" * 32,
            RELEASE,
            "not-a-manifest-hash",
            "sha256:" + "2" * 64,
        ])


@pytest.mark.parametrize("primaries", [set(), {"node-1", "node-2"}])
def test_runner_blocks_without_exactly_one_primary(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
    primaries: set[str],
) -> None:
    manifest_path = manifest(tmp_path / "manifest.json")
    cluster = inventory(monkeypatch, tmp_path)
    transport = ProducerTransport(
        gate_payload(manifest_path),
        primaries=primaries,
    )
    with pytest.raises(runner.GatePreparationError, match="exactly one"):
        runner.prepare(
            inventory=cluster,
            transport=transport,
            release_id=RELEASE,
            manifest_path=manifest_path,
            output=tmp_path / "gate.json",
            now=NOW,
        )
    assert len(transport.commands) == 3


def test_failure_injection_publishes_no_local_gate(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    manifest_path = manifest(tmp_path / "manifest.json")
    cluster = inventory(monkeypatch, tmp_path)
    transport = ProducerTransport(
        gate_payload(manifest_path),
        producer_returncode=55,
    )
    output = tmp_path / "gate.json"
    with pytest.raises(runner.GatePreparationError, match="injected restore failure"):
        runner.prepare(
            inventory=cluster,
            transport=transport,
            release_id=RELEASE,
            manifest_path=manifest_path,
            output=output,
            now=NOW,
        )
    assert not output.exists()


def test_dry_run_never_constructs_transport_or_attempts_ssh(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
    capsys: pytest.CaptureFixture[str],
) -> None:
    manifest_path = manifest(tmp_path / "manifest.json")
    cluster = inventory(monkeypatch, tmp_path)
    constructed = False

    def forbidden_transport(*args, **kwargs):
        nonlocal constructed
        constructed = True
        raise AssertionError("SSH transport must not be constructed")

    monkeypatch.setattr(runner, "StrictSshTransport", forbidden_transport)
    monkeypatch.setattr(sys, "argv", [
        "prepare_release_migration_gate.py",
        "--inventory", str(cluster.path),
        "--known-hosts", str(tmp_path / "missing-known-hosts"),
        "--identity", str(tmp_path / "missing-identity"),
        "--release", RELEASE,
        "--manifest", str(manifest_path),
        "--output", str(tmp_path / "gate.json"),
        "--dry-run",
    ])
    assert runner.main() == 0
    value = json.loads(capsys.readouterr().out)
    assert value["sshAttempted"] is False
    assert constructed is False
    assert not (tmp_path / "gate.json").exists()
