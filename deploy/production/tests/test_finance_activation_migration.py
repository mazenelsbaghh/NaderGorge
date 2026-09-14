"""Real PostgreSQL regression for the staged September 2026 fee activation."""
from __future__ import annotations

import os
from pathlib import Path
import subprocess

import pytest

ROOT = Path(__file__).resolve().parents[3]
CONTAINER = os.environ.get("MASSAR_FINANCE_TEST_CONTAINER")


@pytest.mark.skipif(not CONTAINER, reason="Requires a disposable PostgreSQL test container")
@pytest.mark.parametrize("corrupt_amount", [False, True], ids=["valid-defaults", "wrong-default-rejected"])
def test_fee_activation_preserves_history_and_gate_detects_wrong_terms(corrupt_amount: bool) -> None:
    fixture, verification = (Path(__file__).parent / "fixtures/finance-activation.sql").read_text().split("-- VERIFY MIGRATION", 1)
    migration = (ROOT / "backend/src/NaderGorge.Infrastructure/Migrations/20260914135411_ActivateTeacherPlatformFeeDefaults.cs").read_text()
    activation_sql = migration.split('migrationBuilder.Sql("""', 1)[1].split('""");', 1)[0]
    gate = (ROOT / "deploy/production/scripts/prepare_release_migration_gate.py").read_text()
    gate_query = gate.split("default_mismatches=", 1)[1].split("<<'SQL'\n", 1)[1].split("\nSQL", 1)[0]
    corruption = ('UPDATE teacher_financial_agreements SET "AllocationValue" = 999 WHERE "IsActive";' if corrupt_amount else "")
    completed = subprocess.run(
        ["docker", "exec", "-i", str(CONTAINER), "psql", "-U", "postgres", "-XAtq", "-v", "ON_ERROR_STOP=1"],
        input=f"BEGIN;\n{fixture}\n{activation_sql}\n{verification}\n{corruption}\n{gate_query}\nROLLBACK;\n",
        text=True, capture_output=True, check=True,
    )
    assert int(completed.stdout.strip()) == (590 if corrupt_amount else 0)
