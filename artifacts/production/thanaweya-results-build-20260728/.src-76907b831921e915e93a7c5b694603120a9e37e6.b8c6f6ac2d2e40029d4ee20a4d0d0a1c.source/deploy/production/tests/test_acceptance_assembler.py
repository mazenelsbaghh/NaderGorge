from __future__ import annotations

import datetime as dt
import hashlib
import json
import sys
from pathlib import Path

import pytest


ROOT = Path(__file__).resolve().parents[3]
SCRIPTS = ROOT / "deploy/production/scripts"
sys.path.insert(0, str(SCRIPTS))

import acceptance_schema as schemas  # noqa: E402
import assemble_acceptance_evidence as assembler  # noqa: E402


RELEASE_ID = "src-" + "a" * 40


def raw_sources(root: Path, captured_at: str) -> dict[str, Path]:
    root.mkdir()
    common = {
        "schemaVersion": 1,
        "status": "success",
        "releaseId": RELEASE_ID,
        "capturedAt": captured_at,
        "rawOnlyField": "must be projected out",
    }
    values = {
        "release.json": {
            "schemaVersion": 1,
            "status": "success",
            "releaseId": RELEASE_ID,
            "createdAt": captured_at,
            "digestParity": True,
            "nodeCount": 3,
            "rawOnlyField": "must be projected out",
        },
        "cluster-health.json": {
            **common,
            "healthyNodeCount": 3,
            "postgresWriterCount": 1,
            "redisMasterCount": 1,
            "glusterSplitBrainCount": 0,
        },
        "database-backup.json": {**common, "walArchiveAgeSeconds": 0},
        "database-restore.json": {
            **common,
            "isolated": True,
            "checksumVerified": True,
        },
        "file-backup.json": {**common, "snapshotAgeSeconds": 0},
        "file-restore.json": {
            **common,
            "isolated": True,
            "checksumVerified": True,
        },
        "load.json": {
            **common,
            "durationSeconds": 1800,
            "baselineMultiplier": 2,
            "errorRate": 0,
            "p95Milliseconds": 500,
            "p99Milliseconds": 900,
            "droppedIterationRate": 0,
            "healthyNodeCount": 3,
        },
        "chaos.json": {
            **common,
            "passedScenarios": [
                "app", "files", "ingress", "postgres", "redis", "worker",
            ],
        },
        "security.json": {
            **common,
            "scope": "pre-dns",
            "directOriginDenied": True,
            "internalPortsClosed": True,
            "wrongHostDenied": True,
        },
        "automated-tests.json": {**common, "criticalFindingCount": 0},
        "manual-qa.json": {**common, "scope": "pre-dns"},
    }
    paths: dict[str, Path] = {}
    for name, value in values.items():
        path = root / name
        path.write_text(json.dumps(value), encoding="utf-8")
        paths[name] = path
    return paths


def source_map(path: Path, sources: dict[str, Path]) -> Path:
    path.write_text(json.dumps({
        "schemaVersion": 1,
        "sources": {
            name: str(source.relative_to(path.parent))
            for name, source in sources.items()
        },
    }), encoding="utf-8")
    return path


def test_assembler_emits_exact_schema_valid_atomic_bundle(tmp_path: Path) -> None:
    now = dt.datetime(2026, 7, 27, 12, 0, tzinfo=dt.timezone.utc)
    captured = now.isoformat().replace("+00:00", "Z")
    sources = raw_sources(tmp_path / "raw", captured)
    mapping = source_map(tmp_path / "sources.json", sources)
    output = tmp_path / "canonical"

    digests = assembler.assemble(mapping, output, now=now)

    assert set(digests) == set(schemas.EVIDENCE_NAMES)
    assert {path.name for path in output.iterdir()} == set(
        schemas.EVIDENCE_NAMES
    )
    for name in schemas.EVIDENCE_NAMES:
        value = json.loads((output / name).read_text())
        schemas.validate_evidence(name, value)
        assert "rawOnlyField" not in value
        assert value["sourceEvidenceSha256"] == hashlib.sha256(
            sources[name].read_bytes()
        ).hexdigest()


@pytest.mark.parametrize("failure", ["missing", "mismatch", "stale"])
def test_assembler_rejects_incomplete_cross_release_or_stale_sources(
    tmp_path: Path,
    failure: str,
) -> None:
    now = dt.datetime(2026, 7, 27, 12, 0, tzinfo=dt.timezone.utc)
    captured = (
        now - dt.timedelta(days=2) if failure == "stale" else now
    ).isoformat().replace("+00:00", "Z")
    sources = raw_sources(tmp_path / "raw", captured)
    if failure == "missing":
        sources.pop("manual-qa.json")
    elif failure == "mismatch":
        value = json.loads(sources["load.json"].read_text())
        value["releaseId"] = "src-" + "b" * 40
        sources["load.json"].write_text(json.dumps(value))
    mapping = source_map(tmp_path / "sources.json", sources)

    with pytest.raises(assembler.AssemblyError):
        assembler.assemble(mapping, tmp_path / "canonical", now=now)
    assert not (tmp_path / "canonical").exists()


def test_runtime_catalog_is_exact_and_rejects_additional_properties(
    tmp_path: Path,
) -> None:
    assert set(schemas.SCHEMA_PATHS) == set(schemas.EVIDENCE_NAMES)
    assert all(path.is_file() for path in schemas.SCHEMA_PATHS.values())
    value = {
        "schemaVersion": 1,
        "status": "success",
        "releaseId": RELEASE_ID,
        "capturedAt": "2026-07-27T12:00:00Z",
        "scope": "pre-dns",
        "sourceEvidenceSha256": "a" * 64,
        "unexpected": True,
    }
    with pytest.raises(schemas.SchemaError, match="additional property"):
        schemas.validate_evidence("manual-qa.json", value)


def test_assembler_refuses_to_replace_existing_bundle(tmp_path: Path) -> None:
    mapping = tmp_path / "sources.json"
    mapping.write_text("{}")
    output = tmp_path / "canonical"
    output.mkdir()
    with pytest.raises(assembler.AssemblyError, match="already exists"):
        assembler.assemble(mapping, output)
