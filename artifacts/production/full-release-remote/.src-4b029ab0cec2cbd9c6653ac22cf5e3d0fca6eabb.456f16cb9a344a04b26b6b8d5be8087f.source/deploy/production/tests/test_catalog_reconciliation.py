from __future__ import annotations

import importlib.util
from pathlib import Path


ROOT = Path(__file__).resolve().parents[3]
PATH = ROOT / "deploy/production/scripts/compare_legacy_catalog.py"
SPEC = importlib.util.spec_from_file_location("compare_legacy_catalog", PATH)
assert SPEC and SPEC.loader
module = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(module)


def test_reconciliation_preserves_durable_state_and_resets_only_ephemeral() -> None:
    for table in module.DURABLE_PRESERVE:
        assert module.classification(table) == "DURABLE_PRESERVE"
    assert module.classification("VideoPlaybackSessions") == "RESET_ON_IMPORT"
    assert module.classification("cluster_leases") == "RESET_ON_IMPORT"
    assert module.RESET_ON_IMPORT == {"VideoPlaybackSessions", "cluster_leases"}
    assert module.classification("audit_logs") == "SENSITIVE_PRESERVE_REVIEW"
    assert module.classification("subjects") == "REFERENCE_REVIEW"
    assert module.classification("lessons") == "CONTENT_REVIEW"
    assert module.classification("student_profiles") == "RELATIONAL_DATA_REVIEW"


def test_production_catalog_audit_is_read_only() -> None:
    source = (
        ROOT / "deploy/production/scripts/audit_production_catalog.py"
    ).read_text(encoding="utf-8")
    assert '"mode": "read-only"' in source
    for forbidden in ("INSERT INTO", "UPDATE ", "DELETE FROM", "DROP TABLE", "TRUNCATE"):
        assert forbidden not in source
