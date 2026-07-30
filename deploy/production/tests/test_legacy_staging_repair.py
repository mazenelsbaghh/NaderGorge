from __future__ import annotations

from pathlib import Path


ROOT = Path(__file__).resolve().parents[3]


def test_staging_repair_only_normalizes_providers_and_blocks_proven_missing_attachments() -> None:
    source = (
        ROOT / "deploy/production/scripts/repair_legacy_staging.py"
    ).read_text(encoding="utf-8")
    assert 'where not "IsBlocked"' in source
    assert "convert_from(decode" in source
    assert "base64.b64encode" in source
    assert '"IsBlocked"=true' in source
    assert '"affectedRows"' in source
    assert '"normalizedProviderRows"' in source
    assert "normalize-supported-video-providers" in source
    assert '"contentDeleted": False' in source
    for forbidden in ("DELETE FROM", "TRUNCATE", "DROP TABLE"):
        assert forbidden not in source
