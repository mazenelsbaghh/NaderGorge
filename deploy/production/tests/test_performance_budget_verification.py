from __future__ import annotations

import importlib.util
import json
import sys
from pathlib import Path

import pytest


ROOT = Path(__file__).resolve().parents[3]
SCRIPT = ROOT / "deploy/production/scripts/verify_performance_budgets.py"
SCRIPTS = SCRIPT.parent
TESTS = ROOT / "deploy/production/tests"
sys.path.insert(0, str(SCRIPTS))
sys.path.insert(0, str(TESTS))
SPEC = importlib.util.spec_from_file_location("verify_performance_budgets", SCRIPT)
assert SPEC and SPEC.loader
verification = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = verification
SPEC.loader.exec_module(verification)

import assemble_performance_evidence as assembler  # noqa: E402
from performance_evidence_support import (  # noqa: E402
    create_raw_evidence,
    initialize_repository,
)

ADMIN_AI_PERFORMANCE_CONTRACT = {
    "route": {
        "maximumWarmNavigationP75Ms": 300,
        "maximumDuplicateEligibleReads": 0,
    },
    "worker": {
        "concurrency": 4,
        "maximumQueueAgeMs": 300_000,
        "ordinaryProviderDeadlineMs": 30_000,
    },
    "requestsPerMinute": {
        "turnAdmissionsPerAdmin": 10,
        "confirmationsPerAdmin": 20,
        "secureInputsPerAdmin": 10,
        "internalCallbacksPerSourceIp": 120,
    },
    "query": {
        "maximumModelSteps": 3,
        "maximumReadCallsPerTurn": 6,
        "maximumReadCallsPerStep": 4,
        "maximumRedactedContextBytes": 65_536,
        "maximumRecordsPerInvocation": 200,
        "maximumQueryTimeoutMs": 5_000,
    },
}


def write(path: Path, value: object) -> Path:
    path.write_text(json.dumps(value), encoding="utf-8")
    return path


def evidence(initial: int, *, navigation: int = 250, commands: int = 12) -> dict:
    routes = {}
    for name in ("login", "register", "student", "admin/ai-agent"):
        routes[name] = {
            "pathname": f"/{name}",
            "initial": {"brotliBytes": initial},
            "shared": {"brotliBytes": 500},
            "deferred": {"brotliBytes": 100},
            "requests": {"duplicateEligibleReads": 0},
            "navigation": {"warmP75Ms": navigation},
        }
    return {
        "routes": routes,
        "workflows": {
            "live-support-admin": {
                "maximumDatabaseCommandsObserved": commands,
            }
        },
    }


def budgets() -> dict:
    return {
        "routes": {
            path: {
                "minimumInitialReductionFromBaseline": 0.25,
                "maximumSharedIncreaseFromBaseline": 0,
                "maximumDeferredBrotliBytes": 100,
                "maximumDuplicateEligibleReads": 0,
                "maximumWarmNavigationP75Ms": 300,
            }
            for path in ("/login", "/register", "/student")
        },
        "workflows": {
            "live-support-admin": {
                "maximumDatabaseCommands": 12,
            }
        },
    }


def test_production_gate_accepts_complete_bounded_evidence(tmp_path: Path) -> None:
    repository = initialize_repository(tmp_path)
    raw_root, candidate = create_raw_evidence(assembler, repository)
    result = verification.verify(
        verification.VerificationRequest(
            budgets=write(tmp_path / "budgets.json", budgets()),
            baseline=write(tmp_path / "baseline.json", evidence(1_000)),
            candidate=candidate,
            repository=repository,
            raw_root=raw_root,
        )
    )
    assert result["passed"] is True
    assert result["workflows"]["passed"] is True


def test_admin_ai_release_budget_contract_matches_reviewed_protocol() -> None:
    assert ADMIN_AI_PERFORMANCE_CONTRACT == {
        "route": {
            "maximumWarmNavigationP75Ms": 300,
            "maximumDuplicateEligibleReads": 0,
        },
        "worker": {
            "concurrency": 4,
            "maximumQueueAgeMs": 300_000,
            "ordinaryProviderDeadlineMs": 30_000,
        },
        "requestsPerMinute": {
            "turnAdmissionsPerAdmin": 10,
            "confirmationsPerAdmin": 20,
            "secureInputsPerAdmin": 10,
            "internalCallbacksPerSourceIp": 120,
        },
        "query": {
            "maximumModelSteps": 3,
            "maximumReadCallsPerTurn": 6,
            "maximumReadCallsPerStep": 4,
            "maximumRedactedContextBytes": 65_536,
            "maximumRecordsPerInvocation": 200,
            "maximumQueryTimeoutMs": 5_000,
        },
    }
@pytest.mark.parametrize("breach", ["initial", "navigation", "query"])
def test_production_gate_rejects_any_budget_breach(
    tmp_path: Path,
    breach: str,
) -> None:
    repository = initialize_repository(tmp_path)
    raw_root, candidate = create_raw_evidence(assembler, repository)
    breached_budgets = budgets()
    if breach == "initial":
        breached_budgets["routes"]["/login"]["minimumInitialReductionFromBaseline"] = 1
    elif breach == "navigation":
        breached_budgets["routes"]["/student"]["maximumWarmNavigationP75Ms"] = 149
    else:
        breached_budgets["workflows"]["live-support-admin"]["maximumDatabaseCommands"] = 4
    with pytest.raises(verification.PerformanceBudgetError, match="budgets failed"):
        verification.verify(
            verification.VerificationRequest(
                budgets=write(tmp_path / "budgets.json", breached_budgets),
                baseline=write(tmp_path / "baseline.json", evidence(1_000)),
                candidate=candidate,
                repository=repository,
                raw_root=raw_root,
            )
        )


def test_production_gate_fails_closed_when_candidate_is_missing(tmp_path: Path) -> None:
    with pytest.raises(verification.PerformanceBudgetError, match="regular non-symlink"):
        verification.verify(
            verification.VerificationRequest(
                budgets=write(tmp_path / "budgets.json", budgets()),
                baseline=write(tmp_path / "baseline.json", evidence(1_000)),
                candidate=tmp_path / "missing.json",
                repository=tmp_path,
                raw_root=tmp_path,
            )
        )


def test_production_gate_rejects_matrix_run_without_real_workflows(
    tmp_path: Path,
) -> None:
    matrix = json.loads(verification.DEFAULT_MATRIX.read_text(encoding="utf-8"))
    matrix["runs"][0]["workflowProbesRequired"] = False
    with pytest.raises(
        verification.PerformanceBudgetError,
        match="every production performance run",
    ):
        verification.validate_workflow_matrix(
            write(tmp_path / "performance-matrix.json", matrix)
        )
