from __future__ import annotations

from pathlib import Path


ROOT = Path(__file__).resolve().parents[3]


def test_legacy_audit_is_read_only_and_never_exports_row_values() -> None:
    source = (
        ROOT / "deploy/production/scripts/audit_legacy_server.py"
    ).read_text(encoding="utf-8")
    assert '"mode": "read-only"' in source
    assert "tableCounts" in source
    assert "schemaSha256" in source
    assert "environmentKeys" in source
    assert "pathSizeManifestSha256" in source
    for forbidden in (
        "pg_dump",
        "COPY TO",
        "INSERT INTO",
        "UPDATE ",
        "DELETE FROM",
        "docker cp",
        "PasswordHash",
    ):
        assert forbidden not in source
