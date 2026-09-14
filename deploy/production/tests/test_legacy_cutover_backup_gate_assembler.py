from __future__ import annotations

import hashlib
import importlib.util
import json
import subprocess
import sys
import datetime as dt
from pathlib import Path

import pytest


ROOT = Path(__file__).resolve().parents[3]
SCRIPT = ROOT / "deploy/production/scripts/assemble_legacy_cutover_backup_gate.py"
SCRIPTS = ROOT / "deploy/production/scripts"
sys.path.insert(0, str(SCRIPTS))
SPEC = importlib.util.spec_from_file_location("backup_gate_assembler", SCRIPT)
assert SPEC and SPEC.loader
assembler = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(assembler)
MANAGE_SPEC = importlib.util.spec_from_file_location(
    "backup_gate_manage_contract",
    SCRIPTS / "manage_legacy_cutover.py",
)
assert MANAGE_SPEC and MANAGE_SPEC.loader
manage = importlib.util.module_from_spec(MANAGE_SPEC)
sys.modules[MANAGE_SPEC.name] = manage
MANAGE_SPEC.loader.exec_module(manage)

CLUSTER = "massar-production"
RELEASE = "git-1234567890abcdef"
CANDIDATE = "massar_platform_candidate_20260727T120000Z"
PREPARED = "2026-07-27T12:00:00Z"
OPERATION = "00000000-0000-4000-8000-000000000166"
LABEL = "20260727-120100F"
SNAPSHOT = "a" * 64


def write(path: Path, payload: dict) -> Path:
    path.write_text(json.dumps(payload), encoding="utf-8")
    return path


def rebind_restore_digests(paths: dict[str, Path]) -> None:
    for restore_name, backup_name in (
        ("database_restore_evidence", "database_backup_evidence"),
        ("file_restore_evidence", "file_backup_evidence"),
    ):
        payload = json.loads(paths[restore_name].read_text())
        payload["backupEvidenceSha256"] = hashlib.sha256(
            paths[backup_name].read_bytes()
        ).hexdigest()
        write(paths[restore_name], payload)


def evidence_set(tmp_path: Path) -> dict[str, Path]:
    common = {
        "schemaVersion": 1,
        "status": "success",
        "clusterId": CLUSTER,
        "releaseId": RELEASE,
        "startedAt": "2026-07-27T12:01:00Z",
        "completedAt": "2026-07-27T12:02:00Z",
        "capturedAt": "2026-07-27T12:02:00Z",
    }
    paths = {
        "database_backup_evidence": write(tmp_path / "db-backup.json", {
            **common,
            "producer": "pgbackrest",
            "backupLabel": LABEL,
            "backupType": "full",
            "stanza": "massar",
            "repository": 1,
            "encrypted": True,
            "replicationFactor": 3,
            "repositoryInfoSha256": "1" * 64,
            "walArchiveAgeSeconds": 0,
        }),
        "database_restore_evidence": write(tmp_path / "db-restore.json", {
            **common,
            "producer": "pgbackrest",
            "startedAt": "2026-07-27T12:03:00Z",
            "completedAt": "2026-07-27T12:04:00Z",
            "capturedAt": "2026-07-27T12:04:05Z",
            "backupLabel": LABEL,
            "isolated": True,
            "productionTarget": False,
            "integrityOk": True,
            "migrationStateOk": True,
            "loginSmokeOk": True,
            "checksumVerified": True,
            "repositoryInfoSha256": "2" * 64,
            "backupEvidenceSha256": "0" * 64,
            "recoveryTarget": "2026-07-27T12:02:30Z",
            "latestMigration": "20260726182136_EnsureSystemRoles",
            "destroyedAt": "2026-07-27T12:04:05Z",
        }),
        "file_backup_evidence": write(tmp_path / "file-backup.json", {
            **common,
            "producer": "restic",
            "snapshotId": SNAPSHOT,
            "hostname": "massar-cluster",
            "paths": ["/srv/massar-shared"],
            "encrypted": True,
            "replicationFactor": 3,
            "backupSummarySha256": "3" * 64,
            "snapshotAgeSeconds": 0,
        }),
        "file_restore_evidence": write(tmp_path / "file-restore.json", {
            **common,
            "producer": "restic",
            "startedAt": "2026-07-27T12:03:00Z",
            "completedAt": "2026-07-27T12:04:00Z",
            "capturedAt": "2026-07-27T12:04:10Z",
            "snapshotId": SNAPSHOT,
            "isolated": True,
            "productionTarget": False,
            "repositoryCheckOk": True,
            "checksumVerified": True,
            "fileSampleOk": True,
            "snapshotMetadataSha256": "4" * 64,
            "backupEvidenceSha256": "0" * 64,
            "checksum": "5" * 64,
            "destroyedAt": "2026-07-27T12:04:10Z",
        }),
    }
    rebind_restore_digests(paths)
    return paths


def assemble(
    tmp_path: Path,
    sources: dict[str, Path],
    *,
    prepared: str = PREPARED,
    now: dt.datetime = dt.datetime(2026, 7, 27, 12, 5, tzinfo=dt.timezone.utc),
) -> dict:
    inventory = write(tmp_path / "inventory.json", {"cluster": {"name": CLUSTER}})
    dump = tmp_path / "candidate.dump.gpg"
    archive = tmp_path / "files.tar.gpg"
    dump.write_bytes(b"database")
    archive.write_bytes(b"files")
    manifest = write(tmp_path / "candidate.json", {
        "schemaVersion": 1,
        "status": "success",
        "backupId": "legacy-20260727T120000Z",
        "candidateDump": {
            "path": str(dump),
            "sha256": hashlib.sha256(b"database").hexdigest(),
        },
        "fileArchive": {
            "path": str(archive),
            "sha256": hashlib.sha256(b"files").hexdigest(),
        },
    })
    return assembler.assemble(
        **sources,
        inventory=inventory,
        cluster_id=CLUSTER,
        release_id=RELEASE,
        candidate_database=CANDIDATE,
        candidate_prepared_at=prepared,
        candidate_manifest=manifest,
        operation_id=OPERATION,
        now=now,
    )


def test_assembles_real_ids_and_computed_binding_digests(tmp_path: Path) -> None:
    sources = evidence_set(tmp_path)
    result = assemble(tmp_path, sources)
    assert result["schemaVersion"] == 2
    assert result["databaseBackupId"] == LABEL
    assert result["fileSnapshotId"] == SNAPSHOT
    assert result["clusterId"] == CLUSTER
    assert result["operationId"] == OPERATION
    assert result["candidateBackupId"] == "legacy-20260727T120000Z"
    assert result["candidateDumpSha256"] == hashlib.sha256(b"database").hexdigest()
    assert result["capturedAt"] == "2026-07-27T12:04:10Z"
    assert result["databaseBackupEvidenceSha256"] == hashlib.sha256(
        sources["database_backup_evidence"].read_bytes()
    ).hexdigest()
    assert result["inventorySha256"] == hashlib.sha256(
        (tmp_path / "inventory.json").read_bytes()
    ).hexdigest()


def test_schema_v2_output_matches_cutover_validator_exactly(tmp_path: Path) -> None:
    result = assemble(tmp_path, evidence_set(tmp_path))
    gate = write(tmp_path / "gate.json", result)
    validated = manage.validate_backup_gate(
        gate,
        prepared_at=dt.datetime(2026, 7, 27, 12, tzinfo=dt.timezone.utc),
        now=dt.datetime(2026, 7, 27, 12, 5, tzinfo=dt.timezone.utc),
        cluster_name=CLUSTER,
        inventory_sha256=result["inventorySha256"],
        release_id=RELEASE,
        candidate_database=CANDIDATE,
        candidate_prepared_at=PREPARED,
        candidate_manifest_sha256=result["candidateManifestSha256"],
        operation_id=OPERATION,
        candidate_backup_id=result["candidateBackupId"],
        candidate_dump_sha256=result["candidateDumpSha256"],
        file_archive_sha256=result["fileArchiveSha256"],
    )
    assert validated == result

    result["inventorySha256"] = "f" * 64
    write(gate, result)
    with pytest.raises(manage.LegacyCutoverError, match="digest-matched"):
        manage.validate_backup_gate(
            gate,
            prepared_at=dt.datetime(2026, 7, 27, 12, tzinfo=dt.timezone.utc),
            now=dt.datetime(2026, 7, 27, 12, 5, tzinfo=dt.timezone.utc),
            cluster_name=CLUSTER,
            inventory_sha256=validated["inventorySha256"],
            release_id=RELEASE,
            candidate_database=CANDIDATE,
            candidate_prepared_at=PREPARED,
            candidate_manifest_sha256=validated["candidateManifestSha256"],
            operation_id=OPERATION,
            candidate_backup_id=validated["candidateBackupId"],
            candidate_dump_sha256=validated["candidateDumpSha256"],
            file_archive_sha256=validated["fileArchiveSha256"],
        )


@pytest.mark.parametrize(
    ("source", "field", "value", "message"),
    [
        ("database_restore_evidence", "backupLabel", "20260727-120200F", "backup label"),
        ("file_restore_evidence", "snapshotId", "b" * 64, "snapshot ID"),
        ("database_restore_evidence", "isolated", False, "isolated restore"),
        ("file_restore_evidence", "productionTarget", True, "isolated restore"),
        ("database_backup_evidence", "replicationFactor", 2, "durability"),
    ],
)
def test_rejects_unbound_or_unverified_evidence(
    tmp_path: Path,
    source: str,
    field: str,
    value: object,
    message: str,
) -> None:
    sources = evidence_set(tmp_path)
    payload = json.loads(sources[source].read_text())
    payload[field] = value
    write(sources[source], payload)
    with pytest.raises(assembler.BackupGateError, match=message):
        assemble(tmp_path, sources)


def test_rejects_evidence_before_candidate_prepare(tmp_path: Path) -> None:
    sources = evidence_set(tmp_path)
    payload = json.loads(sources["database_backup_evidence"].read_text())
    payload["startedAt"] = "2026-07-27T11:58:00Z"
    payload["completedAt"] = "2026-07-27T11:59:00Z"
    write(sources["database_backup_evidence"], payload)
    rebind_restore_digests(sources)
    with pytest.raises(assembler.BackupGateError, match="predates candidate"):
        assemble(tmp_path, sources)


@pytest.mark.parametrize(
    ("captured", "message"),
    [
        ("2026-07-27T12:08:01Z", "future-dated"),
        ("2026-07-27T11:49:59Z", "stale"),
    ],
)
def test_rejects_future_or_stale_evidence(
    tmp_path: Path,
    captured: str,
    message: str,
) -> None:
    sources = evidence_set(tmp_path)
    for source in sources.values():
        payload = json.loads(source.read_text())
        payload["startedAt"] = captured
        payload["completedAt"] = captured
        payload["capturedAt"] = captured
        if "destroyedAt" in payload:
            payload["destroyedAt"] = captured
        write(source, payload)
    rebind_restore_digests(sources)
    with pytest.raises(assembler.BackupGateError, match=message):
        assemble(tmp_path, sources, prepared="2026-07-27T11:00:00Z")


def test_rejects_candidate_artifact_digest_tamper(tmp_path: Path) -> None:
    sources = evidence_set(tmp_path)
    assemble(tmp_path, sources)
    (tmp_path / "candidate.dump.gpg").write_bytes(b"tampered")
    inventory = tmp_path / "inventory.json"
    manifest = tmp_path / "candidate.json"
    with pytest.raises(assembler.BackupGateError, match="digest mismatch"):
        assembler.assemble(
            **sources,
            inventory=inventory,
            cluster_id=CLUSTER,
            release_id=RELEASE,
            candidate_database=CANDIDATE,
            candidate_prepared_at=PREPARED,
            candidate_manifest=manifest,
            operation_id=OPERATION,
            now=dt.datetime(2026, 7, 27, 12, 5, tzinfo=dt.timezone.utc),
        )


def test_rejects_backup_evidence_modified_after_restore(tmp_path: Path) -> None:
    sources = evidence_set(tmp_path)
    payload = json.loads(sources["database_backup_evidence"].read_text())
    payload["walArchiveAgeSeconds"] = 1
    write(sources["database_backup_evidence"], payload)
    with pytest.raises(assembler.BackupGateError, match="isolated restore"):
        assemble(tmp_path, sources)


def test_rejects_extra_fields_symlinks_and_existing_output(tmp_path: Path) -> None:
    sources = evidence_set(tmp_path)
    payload = json.loads(sources["file_backup_evidence"].read_text())
    payload["operatorClaim"] = "trust-me"
    write(sources["file_backup_evidence"], payload)
    with pytest.raises(assembler.BackupGateError, match="fields do not match"):
        assemble(tmp_path, sources)

    target = tmp_path / "existing.json"
    target.write_text("{}")
    with pytest.raises(assembler.BackupGateError, match="must not already exist"):
        assembler.write_new_evidence(target, {"status": "success"})


def test_cli_does_not_import_or_execute_backup_tools(tmp_path: Path) -> None:
    source = SCRIPT.read_text(encoding="utf-8")
    for forbidden in (
        "subprocess",
        "StrictSshTransport",
        "pgbackrest ",
        "restic backup",
        "restic restore",
    ):
        assert forbidden not in source

    sources = evidence_set(tmp_path)
    now = dt.datetime.now(dt.timezone.utc).replace(microsecond=0)
    prepared = now - dt.timedelta(minutes=4)
    for name, source in sources.items():
        payload = json.loads(source.read_text())
        is_restore = "restore" in name
        payload["startedAt"] = (
            now - dt.timedelta(seconds=90 if is_restore else 180)
        ).isoformat().replace("+00:00", "Z")
        payload["completedAt"] = (
            now - dt.timedelta(seconds=60 if is_restore else 120)
        ).isoformat().replace("+00:00", "Z")
        if "destroyedAt" in payload:
            payload["destroyedAt"] = (
                now - dt.timedelta(seconds=50)
            ).isoformat().replace("+00:00", "Z")
            payload["capturedAt"] = payload["destroyedAt"]
        else:
            payload["capturedAt"] = payload["completedAt"]
        write(source, payload)
    rebind_restore_digests(sources)
    inventory = write(tmp_path / "inventory.json", {"cluster": {"name": CLUSTER}})
    dump = tmp_path / "candidate.dump.gpg"
    archive = tmp_path / "files.tar.gpg"
    dump.write_bytes(b"database")
    archive.write_bytes(b"files")
    manifest = write(tmp_path / "candidate.json", {
        "schemaVersion": 1,
        "status": "success",
        "backupId": "legacy-20260727T120000Z",
        "candidateDump": {
            "path": str(dump),
            "sha256": hashlib.sha256(b"database").hexdigest(),
        },
        "fileArchive": {
            "path": str(archive),
            "sha256": hashlib.sha256(b"files").hexdigest(),
        },
    })
    output = tmp_path / "gate.json"
    completed = subprocess.run(
        [
            sys.executable,
            str(SCRIPT),
            "--database-backup-evidence", str(sources["database_backup_evidence"]),
            "--database-restore-evidence", str(sources["database_restore_evidence"]),
            "--file-backup-evidence", str(sources["file_backup_evidence"]),
            "--file-restore-evidence", str(sources["file_restore_evidence"]),
            "--inventory", str(inventory),
            "--cluster-id", CLUSTER,
            "--release-id", RELEASE,
            "--candidate-database", CANDIDATE,
            "--candidate-prepared-at", prepared.isoformat().replace("+00:00", "Z"),
            "--candidate-manifest", str(manifest),
            "--operation-id", OPERATION,
            "--output", str(output),
        ],
        text=True,
        capture_output=True,
        check=False,
    )
    assert completed.returncode == 0, completed.stderr
    assert json.loads(output.read_text())["status"] == "success"
    assert output.stat().st_mode & 0o027 == 0
