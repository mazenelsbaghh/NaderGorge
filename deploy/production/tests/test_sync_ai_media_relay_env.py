from __future__ import annotations

import importlib.util
from pathlib import Path

import pytest


ROOT = Path(__file__).resolve().parents[3]
SPEC = importlib.util.spec_from_file_location(
    "sync_ai_media_relay_env",
    ROOT / "deploy/production/scripts/sync_ai_media_relay_env.py",
)
assert SPEC and SPEC.loader
module = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(module)


def test_read_secret_accepts_strong_single_line_value(tmp_path: Path) -> None:
    path = tmp_path / "secret"
    path.write_text("a" * 64 + "\n", encoding="utf-8")
    assert module.read_secret(path) == "a" * 64


@pytest.mark.parametrize("value", ["short", "a" * 31, "a" * 32 + "\nsecond"])
def test_read_secret_rejects_weak_or_multiline_values(
    tmp_path: Path,
    value: str,
) -> None:
    path = tmp_path / "secret"
    path.write_text(value, encoding="utf-8")
    with pytest.raises(ValueError):
        module.read_secret(path)
