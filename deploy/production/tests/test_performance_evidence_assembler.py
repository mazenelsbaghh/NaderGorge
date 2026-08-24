from __future__ import annotations

import importlib.util
import json
import subprocess
import sys
from pathlib import Path

import pytest


ROOT = Path(__file__).resolve().parents[3]
SCRIPTS = ROOT / "deploy/production/scripts"
TESTS = ROOT / "deploy/production/tests"
sys.path.insert(0, str(SCRIPTS))
sys.path.insert(0, str(TESTS))
SCRIPT = SCRIPTS / "assemble_performance_evidence.py"
SPEC = importlib.util.spec_from_file_location("assemble_performance_evidence", SCRIPT)
assert SPEC and SPEC.loader
assembler = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = assembler
SPEC.loader.exec_module(assembler)

from performance_evidence_support import (  # noqa: E402
    browser_evidence,
    create_raw_evidence,
    initialize_repository,
    query_evidence,
    route_evidence,
    source_binding,
    write_json,
)


def test_source_seal_excludes_artifacts_and_survives_artifact_only_commit(
    tmp_path: Path,
) -> None:
    repository = initialize_repository(tmp_path)
    manifest_path = repository / "artifacts/performance-167/final/raw/source-manifest.json"
    manifest = assembler.seal_source(repository, manifest_path)
    (repository / "artifacts/review.json").parent.mkdir(exist_ok=True)
    (repository / "artifacts/review.json").write_text("{}\n", encoding="utf-8")
    subprocess.run(["git", "-C", str(repository), "add", "artifacts/review.json"], check=True)
    subprocess.run(["git", "-C", str(repository), "commit", "-qm", "artifact"], check=True)

    validated = assembler.validate_source_manifest(
        json.loads(manifest_path.read_text(encoding="utf-8")),
        assembler.source_state(repository),
    )

    assert validated["sourceStateSha256"] == manifest["sourceStateSha256"]
    assert validated["gitCommit"] != subprocess.check_output(
        ["git", "-C", str(repository), "rev-parse", "HEAD"], text=True
    ).strip()


def test_source_manifest_rejects_artifact_path_in_source_inventory(tmp_path: Path) -> None:
    repository = initialize_repository(tmp_path)
    state = assembler.source_state(repository)
    manifest = {"schemaVersion": 2, **state}
    manifest["sourcePaths"].append(
        {
            "path": "artifacts/forged.json",
            "status": "tracked",
            "classification": "artifact",
            "sizeBytes": 2,
            "sha256": assembler.sha256_bytes(b"{}"),
        }
    )

    with pytest.raises(assembler.PerformanceEvidenceError, match="artifact"):
        assembler.validate_source_manifest(manifest, state)


def test_browser_metrics_use_nearest_rank_and_maximum_duplicate_reads(
    tmp_path: Path,
) -> None:
    repository = initialize_repository(tmp_path)
    manifest_path = repository / "artifacts/performance-167/final/raw/source-manifest.json"
    manifest = assembler.seal_source(repository, manifest_path)
    binding = source_binding(manifest, manifest_path.read_bytes(), assembler.sha256_bytes)

    metrics, _, _, _ = assembler.browser_metrics(
        browser_evidence(binding, duplicate_count=2),
        manifest,
        binding["manifestSha256"],
    )

    assert metrics["student"]["navigation"]["warmP75Ms"] == 150
    assert metrics["student"]["requests"]["duplicateEligibleReads"] == 2


def test_route_summary_tampering_is_rejected(tmp_path: Path) -> None:
    repository = initialize_repository(tmp_path)
    manifest_path = repository / "artifacts/performance-167/final/raw/source-manifest.json"
    manifest = assembler.seal_source(repository, manifest_path)
    binding = source_binding(manifest, manifest_path.read_bytes(), assembler.sha256_bytes)
    raw = route_evidence(binding)
    raw["routes"]["login"]["initial"]["brotliBytes"] += 1

    with pytest.raises(assembler.PerformanceEvidenceError, match="aggregate"):
        assembler.route_metrics(raw, manifest, binding["manifestSha256"])


def test_query_workflow_maximum_must_come_from_every_observation(tmp_path: Path) -> None:
    repository = initialize_repository(tmp_path)
    manifest_path = repository / "artifacts/performance-167/final/raw/source-manifest.json"
    manifest = assembler.seal_source(repository, manifest_path)
    binding = source_binding(manifest, manifest_path.read_bytes(), assembler.sha256_bytes)

    with pytest.raises(assembler.PerformanceEvidenceError, match="not derived"):
        assembler.query_metrics(
            query_evidence(binding, maximum_override=4),
            manifest,
            binding["manifestSha256"],
        )


def test_candidate_validation_recomputes_raw_hashes_and_metrics(tmp_path: Path) -> None:
    repository = initialize_repository(tmp_path)
    raw_root, candidate_path = create_raw_evidence(assembler, repository)
    candidate = assembler.validate_candidate(repository, raw_root, candidate_path)

    assert candidate["routes"]["student"]["navigation"]["warmP75Ms"] == 150
    assert candidate["workflows"]["live-support-admin"][
        "maximumDatabaseCommandsObserved"
    ] == 5
    assert candidate["source"]["commitBinding"] == (
        "measured-commit-source-digest-authoritative"
    )


def test_candidate_validation_rejects_raw_evidence_changed_after_assembly(
    tmp_path: Path,
) -> None:
    repository = initialize_repository(tmp_path)
    raw_root, candidate_path = create_raw_evidence(assembler, repository)
    browser_path = raw_root / assembler.BROWSER_EVIDENCE_NAME
    browser = json.loads(browser_path.read_text(encoding="utf-8"))
    browser["routes"]["student"]["samples"][0]["warmNavigationMs"] = 999
    write_json(browser_path, browser)

    with pytest.raises(assembler.PerformanceEvidenceError, match="does not match recomputed"):
        assembler.validate_candidate(repository, raw_root, candidate_path)


def test_candidate_validation_rejects_source_changed_after_measurement(
    tmp_path: Path,
) -> None:
    repository = initialize_repository(tmp_path)
    raw_root, candidate_path = create_raw_evidence(assembler, repository)
    (repository / "application.txt").write_text("changed source\n", encoding="utf-8")

    with pytest.raises(assembler.PerformanceEvidenceError, match="source changed"):
        assembler.validate_candidate(repository, raw_root, candidate_path)


def test_candidate_output_is_create_new_and_never_overwritten(tmp_path: Path) -> None:
    repository = initialize_repository(tmp_path)
    raw_root, candidate_path = create_raw_evidence(assembler, repository)
    original = candidate_path.read_bytes()

    with pytest.raises(assembler.PerformanceEvidenceError, match="refusing to overwrite"):
        assembler.write_candidate(repository, raw_root, candidate_path)

    assert candidate_path.read_bytes() == original


def test_raw_symlink_is_rejected(tmp_path: Path) -> None:
    repository = initialize_repository(tmp_path)
    raw_root, candidate_path = create_raw_evidence(assembler, repository)
    query_path = raw_root / assembler.QUERY_EVIDENCE_NAME
    query_path.unlink()
    query_path.symlink_to(candidate_path)

    with pytest.raises(assembler.PerformanceEvidenceError, match="regular non-symlink"):
        assembler.validate_candidate(repository, raw_root, candidate_path)
