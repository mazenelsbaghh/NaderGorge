#!/usr/bin/env python3
"""Fail-closed release gate for route, request, navigation, and query budgets."""

from __future__ import annotations

import argparse
import json
import os
import stat
import subprocess
import sys
from dataclasses import dataclass
from pathlib import Path

from assemble_performance_evidence import (
    RAW_ROOT,
    PerformanceEvidenceError,
    validate_candidate,
)

ROOT = Path(__file__).resolve().parents[3]
CHECKER = ROOT / "frontend/scripts/check-route-performance-budgets.mjs"
DEFAULT_BUDGETS = ROOT / "frontend/performance-budgets.json"
DEFAULT_BASELINE = ROOT / "artifacts/performance-167/baseline/frontend-routes.json"
DEFAULT_CANDIDATE = ROOT / "artifacts/performance-167/final/frontend-routes.json"
DEFAULT_MATRIX = ROOT / "deploy/production/config/performance-matrix.json"


class PerformanceBudgetError(RuntimeError):
    pass


@dataclass(frozen=True)
class VerificationRequest:
    budgets: Path
    baseline: Path
    candidate: Path
    matrix: Path = DEFAULT_MATRIX
    repository: Path = ROOT
    raw_root: Path = RAW_ROOT


def regular_file(path: Path, label: str) -> Path:
    absolute = Path(os.path.abspath(path.expanduser()))
    try:
        mode = absolute.lstat().st_mode
    except FileNotFoundError as exc:
        raise PerformanceBudgetError(
            f"{label} must be a regular non-symlink file"
        ) from exc
    if not stat.S_ISREG(mode) or stat.S_ISLNK(mode):
        raise PerformanceBudgetError(f"{label} must be a regular non-symlink file")
    return absolute


def validate_workflow_matrix(path: Path) -> None:
    matrix_path = regular_file(path, "production performance matrix")
    try:
        matrix = json.loads(matrix_path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as exc:
        raise PerformanceBudgetError("production performance matrix must be valid JSON") from exc
    contract = matrix.get("workflowProbeContract", {})
    runner = contract.get("runner")
    required = contract.get("requiredWorkflows")
    if (
        contract.get("mode") != "authenticated-real-workflows"
        or contract.get("syntheticHealthOnlyAccepted") is not False
        or not isinstance(contract.get("minimumWorkflowRps"), int)
        or contract["minimumWorkflowRps"] < 1
        or not isinstance(runner, str)
        or not regular_file(ROOT / runner, "real workflow runner")
        or not isinstance(required, list)
        or len(required) < 5
        or len(required) != len(set(required))
    ):
        raise PerformanceBudgetError("real workflow probe contract is incomplete")
    runs = matrix.get("runs")
    if not isinstance(runs, list) or not runs or any(
        run.get("workflowProbesRequired") is not True for run in runs
    ):
        raise PerformanceBudgetError("every production performance run must require workflows")


def verify(request: VerificationRequest) -> dict[str, object]:
    validate_workflow_matrix(request.matrix)
    inputs = {
        "budgets": regular_file(request.budgets, "performance budgets"),
        "baseline": regular_file(request.baseline, "route baseline evidence"),
        "candidate": regular_file(request.candidate, "route candidate evidence"),
    }
    validate_candidate(request.repository, request.raw_root, inputs["candidate"])
    completed = subprocess.run(
        [
            "node",
            str(CHECKER),
            "--budgets",
            str(inputs["budgets"]),
            "--baseline",
            str(inputs["baseline"]),
            "--candidate",
            str(inputs["candidate"]),
        ],
        check=False,
        capture_output=True,
        text=True,
    )
    try:
        checker_report = json.loads(completed.stdout)
    except json.JSONDecodeError as exc:
        raise PerformanceBudgetError("performance checker returned invalid JSON") from exc
    if completed.returncode != 0 or checker_report.get("passed") is not True:
        violations = checker_report.get("violations")
        detail = ", ".join(violations) if isinstance(violations, list) else "unknown violation"
        raise PerformanceBudgetError(f"performance budgets failed: {detail}")
    return checker_report


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--budgets", type=Path, default=DEFAULT_BUDGETS)
    parser.add_argument("--baseline", type=Path, default=DEFAULT_BASELINE)
    parser.add_argument("--candidate", type=Path, default=DEFAULT_CANDIDATE)
    parser.add_argument("--matrix", type=Path, default=DEFAULT_MATRIX)
    parser.add_argument("--repo", type=Path, default=ROOT)
    parser.add_argument("--raw-root", type=Path, default=RAW_ROOT)
    args = parser.parse_args()
    try:
        checker_report = verify(
            VerificationRequest(
                budgets=args.budgets,
                baseline=args.baseline,
                candidate=args.candidate,
                matrix=args.matrix,
                repository=args.repo,
                raw_root=args.raw_root,
            )
        )
        print(
            json.dumps(
                {
                    "status": "passed",
                    "routeCount": len(checker_report.get("routes", [])),
                    "queryBudgetsPassed": checker_report.get("workflows", {}).get("passed"),
                },
                sort_keys=True,
            )
        )
        return 0
    except (OSError, PerformanceBudgetError, PerformanceEvidenceError) as exc:
        print(f"performance budget verification blocked: {exc}", file=sys.stderr)
        return 6


if __name__ == "__main__":
    raise SystemExit(main())
