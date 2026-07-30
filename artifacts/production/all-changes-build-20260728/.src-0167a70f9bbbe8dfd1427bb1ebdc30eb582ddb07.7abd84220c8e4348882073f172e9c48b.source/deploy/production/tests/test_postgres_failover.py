from __future__ import annotations

import os
import subprocess
from pathlib import Path

import pytest


ROOT = Path(__file__).resolve().parents[3]


def test_failover_drill_preserves_acknowledged_write_and_rejoins_old_primary() -> None:
    source = (
        ROOT / "deploy/production/scripts/run_postgres_failover_drill.sh"
    ).read_text(encoding="utf-8")
    assert "MASSAR_INVENTORY" in source
    assert "StrictHostKeyChecking=yes" in source
    assert "pg_is_in_recovery" in source
    assert "acknowledged-before-failover" in source
    assert "no safe writer elected within 60 seconds" in source
    assert "running:replica" in source
    assert "trap cleanup EXIT HUP INT TERM" in source
    assert "running_members" in source
    assert "replica_members" in source
    assert "pg_stat_replication" in source
    assert 'replication_prestate" == "2|2"' in source
    assert "failover-drill.lock" in source
    assert "sudo systemctl stop patroni" in source
    assert "DROP TABLE cluster_failover_probe" in source


def test_failover_drill_has_no_fixed_public_target_or_password_transport() -> None:
    source = (
        ROOT / "deploy/production/scripts/run_postgres_failover_drill.sh"
    ).read_text(encoding="utf-8")
    for node in ("191.218.161.76", "191.218.161.78", "168.231.106.230"):
        assert node not in source
    assert "ssh" + "pass" not in source
    assert "StrictHostKeyChecking=" + "no" not in source


def test_live_failover_drill_preserves_write_and_rejoins_old_primary() -> None:
    if os.environ.get("MASSAR_RUN_POSTGRES_FAILOVER") != "1":
        pytest.skip("set MASSAR_RUN_POSTGRES_FAILOVER=1 only for the bounded production drill")
    completed = subprocess.run(
        [str(ROOT / "deploy/production/scripts/run_postgres_failover_drill.sh")],
        text=True,
        capture_output=True,
        check=False,
        env=os.environ,
    )
    assert completed.returncode == 0, completed.stderr
    assert "acknowledged-probe=preserved" in completed.stdout
    assert "former-leader-state=running:replica" in completed.stdout
