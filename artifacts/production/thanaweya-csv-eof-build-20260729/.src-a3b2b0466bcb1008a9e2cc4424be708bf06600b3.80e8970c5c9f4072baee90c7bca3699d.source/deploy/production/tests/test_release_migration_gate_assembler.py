from __future__ import annotations

import datetime as dt
import importlib.util
import json
import sys
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


assembler = load("assemble_release_migration_gate")
contract = sys.modules["release_contract"]
NOW = dt.datetime(2026, 7, 27, 12, tzinfo=dt.timezone.utc)
RELEASE = "git-" + "a" * 40
CURRENT = "prod-20260726-166-r1"


def write(path: Path, value: dict[str, object]) -> Path:
    path.write_text(json.dumps(value), encoding="utf-8")
    return path


def manifest(path: Path) -> Path:
    return write(path, {
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
    })


def sources(tmp_path: Path) -> tuple[Path, Path, Path, Path]:
    release_manifest = manifest(tmp_path / "manifest.json")
    manifest_sha = contract.file_sha256(release_manifest)
    common = {
        "schemaVersion": 1,
        "status": "success",
        "clusterId": "massar-production",
        "currentReleaseId": CURRENT,
        "databaseSystemIdentifier": "7586552109940137719",
    }
    backup = write(tmp_path / "backup.json", {
        **common,
        "capturedAt": "2026-07-27T11:10:00Z",
        "backupId": "pgbackrest-20260727-001",
        "encrypted": True,
        "tableCountsSha256": "d" * 64,
        "migrationIdsSha256": "e" * 64,
    })
    restore = write(tmp_path / "restore.json", {
        **common,
        "capturedAt": "2026-07-27T11:30:00Z",
        "backupId": "pgbackrest-20260727-001",
        "restoreId": "restore-20260727-001",
        "isolated": True,
        "checksumVerified": True,
        "sourceTableCountsSha256": "d" * 64,
        "restoredTableCountsSha256": "d" * 64,
        "preMigrationIdsSha256": "e" * 64,
        "postMigrationIdsSha256": "f" * 64,
        "restoredCopyMigrationVerified": True,
        "realDataValidationVerified": True,
    })
    compatibility = write(tmp_path / "compatibility.json", {
        **common,
        "currentManifestSha256": "1" * 64,
        "targetReleaseId": RELEASE,
        "manifestSha256": manifest_sha,
        "postMigrationIdsSha256": "f" * 64,
        "postMigrationSchemaSha256": "2" * 64,
        "capturedAt": "2026-07-27T11:40:00Z",
        "nMinusOneCompatibilityVerified": True,
    })
    return release_manifest, backup, restore, compatibility


def test_assembler_produces_gate_consumed_by_migrate_and_deploy(tmp_path: Path) -> None:
    release_manifest, backup, restore, compatibility = sources(tmp_path)
    output = tmp_path / "gate.json"
    payload = assembler.assemble(
        manifest_path=release_manifest,
        release_id=RELEASE,
        backup_path=backup,
        restore_path=restore,
        compatibility_path=compatibility,
        output=output,
        now=NOW,
    )
    loaded_manifest = contract.load_release_manifest(release_manifest, RELEASE)
    gate = contract.load_migration_safety_gate(
        output,
        manifest=loaded_manifest,
        now=NOW,
    )
    assert payload["currentReleaseId"] == CURRENT
    assert gate.pre_migration_ids_sha256 == "e" * 64
    assert gate.post_migration_ids_sha256 == "f" * 64


def test_assembler_blocks_without_real_n_minus_one_smoke(tmp_path: Path) -> None:
    release_manifest, backup, restore, compatibility = sources(tmp_path)
    value = json.loads(compatibility.read_text())
    value["nMinusOneCompatibilityVerified"] = False
    compatibility.write_text(json.dumps(value))
    with pytest.raises(assembler.GateAssemblyError, match="not exactly bound"):
        assembler.assemble(
            manifest_path=release_manifest,
            release_id=RELEASE,
            backup_path=backup,
            restore_path=restore,
            compatibility_path=compatibility,
            output=tmp_path / "gate.json",
            now=NOW,
        )


def test_assembler_blocks_restore_from_different_backup(tmp_path: Path) -> None:
    release_manifest, backup, restore, compatibility = sources(tmp_path)
    value = json.loads(restore.read_text())
    value["backupId"] = "pgbackrest-20260727-999"
    restore.write_text(json.dumps(value))
    with pytest.raises(assembler.GateAssemblyError, match="not exactly bound"):
        assembler.assemble(
            manifest_path=release_manifest,
            release_id=RELEASE,
            backup_path=backup,
            restore_path=restore,
            compatibility_path=compatibility,
            output=tmp_path / "gate.json",
            now=NOW,
        )
