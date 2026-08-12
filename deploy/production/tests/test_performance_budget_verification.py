from __future__ import annotations

import importlib.util
import json
import sys
from pathlib import Path

import pytest


ROOT = Path(__file__).resolve().parents[3]
SCRIPT = ROOT / "deploy/production/scripts/verify_performance_budgets.py"
SPEC = importlib.util.spec_from_file_location("verify_performance_budgets", SCRIPT)
assert SPEC and SPEC.loader
verification = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = verification
SPEC.loader.exec_module(verification)

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
            for path in ("/login", "/register", "/student", "/admin/ai-agent")
        },
        "workflows": {
            "live-support-admin": {
                "maximumDatabaseCommands": 12,
            }
        },
    }


def test_production_gate_accepts_complete_bounded_evidence(tmp_path: Path) -> None:
    result = verification.verify(
        write(tmp_path / "budgets.json", budgets()),
        write(tmp_path / "baseline.json", evidence(1_000)),
        write(tmp_path / "candidate.json", evidence(750)),
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
    assert budgets()["routes"]["/admin/ai-agent"] == {
        "minimumInitialReductionFromBaseline": 0.25,
        "maximumSharedIncreaseFromBaseline": 0,
        "maximumDeferredBrotliBytes": 100,
        "maximumDuplicateEligibleReads": 0,
        "maximumWarmNavigationP75Ms": 300,
    }


@pytest.mark.parametrize(
    "candidate",
    [
        evidence(751),
        evidence(750, navigation=301),
        evidence(750, commands=13),
    ],
)
def test_production_gate_rejects_any_budget_breach(
    tmp_path: Path,
    candidate: dict,
) -> None:
    with pytest.raises(verification.PerformanceBudgetError, match="budgets failed"):
        verification.verify(
            write(tmp_path / "budgets.json", budgets()),
            write(tmp_path / "baseline.json", evidence(1_000)),
            write(tmp_path / "candidate.json", candidate),
        )


def test_production_gate_fails_closed_when_candidate_is_missing(tmp_path: Path) -> None:
    with pytest.raises(verification.PerformanceBudgetError, match="regular non-symlink"):
        verification.verify(
            write(tmp_path / "budgets.json", budgets()),
            write(tmp_path / "baseline.json", evidence(1_000)),
            tmp_path / "missing.json",
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
