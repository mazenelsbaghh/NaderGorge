from __future__ import annotations

import importlib.util
import datetime as dt
import json
import os
import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parents[3]
PATH = ROOT / "deploy/production/scripts/accept_production.py"
sys.path.insert(0, str(PATH.parent))
SPEC = importlib.util.spec_from_file_location("accept_production", PATH)
assert SPEC and SPEC.loader
acceptance = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(acceptance)


RELEASE_ID = "src-" + "a" * 40


def green_evidence(root: Path, captured_at: str | None = None) -> None:
    captured_at = captured_at or dt.datetime.now(
        dt.timezone.utc
    ).isoformat().replace("+00:00", "Z")
    common = {
        "schemaVersion": 1,
        "status": "success",
        "releaseId": RELEASE_ID,
        "capturedAt": captured_at,
        "sourceEvidenceSha256": "f" * 64,
    }
    values = {
        "release.json": {
            "schemaVersion": 1,
            "status": "success",
            "releaseId": RELEASE_ID,
            "createdAt": captured_at,
            "digestParity": True,
            "nodeCount": 3,
            "sourceEvidenceSha256": "e" * 64,
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
            "passedScenarios": sorted(acceptance.PRE_DNS_CHAOS_SCENARIOS),
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
    for name, value in values.items():
        (root / name).write_text(json.dumps(value))


def test_green_evidence_produces_no_blockers(tmp_path: Path) -> None:
    green_evidence(tmp_path)
    assert acceptance.evaluate(tmp_path) == []


def test_any_missing_failed_or_stale_gate_blocks_go(tmp_path: Path) -> None:
    green_evidence(tmp_path)
    (tmp_path / "manual-qa.json").unlink()
    database = json.loads((tmp_path / "database-backup.json").read_text())
    database["walArchiveAgeSeconds"] = 301
    (tmp_path / "database-backup.json").write_text(json.dumps(database))
    reasons = acceptance.evaluate(tmp_path)
    assert "missing:manual-qa.json" in reasons
    assert "database-wal-older-than-five-minutes" in reasons


def test_malformed_or_cross_release_evidence_blocks_go(tmp_path: Path) -> None:
    green_evidence(tmp_path)
    load = json.loads((tmp_path / "load.json").read_text())
    load["durationSeconds"] = "1800"
    (tmp_path / "load.json").write_text(json.dumps(load))
    reasons = acceptance.evaluate(tmp_path)
    assert "invalid:load.json" in reasons

    green_evidence(tmp_path)
    load = json.loads((tmp_path / "load.json").read_text())
    load["releaseId"] = "src-" + "b" * 40
    (tmp_path / "load.json").write_text(json.dumps(load))
    reasons = acceptance.evaluate(tmp_path)
    assert "release-mismatch:load.json" in reasons


def test_pre_dns_acceptance_does_not_require_cloudflare_tunnel_drill(
    tmp_path: Path,
) -> None:
    green_evidence(tmp_path)
    chaos = json.loads((tmp_path / "chaos.json").read_text())
    assert "tunnel" not in chaos["passedScenarios"]
    assert acceptance.evaluate(tmp_path) == []


def test_malformed_chaos_scenarios_fail_closed_without_crashing(
    tmp_path: Path,
) -> None:
    green_evidence(tmp_path)
    chaos = json.loads((tmp_path / "chaos.json").read_text())
    chaos["passedScenarios"] = [{"name": "ingress"}]
    (tmp_path / "chaos.json").write_text(json.dumps(chaos))
    assert "invalid:chaos.json" in acceptance.evaluate(tmp_path)


def test_effective_backup_age_includes_elapsed_time(tmp_path: Path) -> None:
    captured = dt.datetime(2026, 7, 27, 12, 0, tzinfo=dt.timezone.utc)
    green_evidence(tmp_path, captured.isoformat().replace("+00:00", "Z"))
    reasons = acceptance.evaluate(
        tmp_path,
        now=captured + dt.timedelta(seconds=301),
    )
    assert "database-wal-older-than-five-minutes" in reasons


def test_main_signature_binds_exact_evidence_digests(
    tmp_path: Path,
    monkeypatch,
) -> None:
    evidence = tmp_path / "evidence"
    evidence.mkdir()
    green_evidence(evidence)
    key = tmp_path / "acceptance.key"
    key.write_bytes(b"k" * 32)
    os.chmod(key, 0o600)
    output = tmp_path / "decision.json"
    monkeypatch.setattr(
        "sys.argv",
        [
            "accept_production.py",
            "--evidence-root",
            str(evidence),
            "--signing-key-file",
            str(key),
            "--output",
            str(output),
        ],
    )
    assert acceptance.main() == 0
    decision = json.loads(output.read_text())
    assert set(decision["evidenceDigests"]) == set(acceptance.REQUIRED)
    assert all(
        len(value) == 64 for value in decision["evidenceDigests"].values()
    )
