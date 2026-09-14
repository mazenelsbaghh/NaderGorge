#!/usr/bin/env python3
"""Compatibility wrapper for the production schema audit."""

from __future__ import annotations

import runpy
from pathlib import Path


ROOT = Path(__file__).resolve().parents[4]
AUDITOR = ROOT / "deploy/production/scripts/audit_database.py"

if not AUDITOR.is_file():
    raise SystemExit(
        "production schema auditor is not installed yet; "
        "do not use the retired single-server checker"
    )

runpy.run_path(str(AUDITOR), run_name="__main__")
