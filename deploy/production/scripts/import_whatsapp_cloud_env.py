#!/usr/bin/env python3
"""Import the attached WhatsApp prototype credentials without logging values."""

from __future__ import annotations

import argparse
import ast
import os
import tempfile
import zipfile
from pathlib import Path


SOURCE_KEYS = ("ACCESS_TOKEN", "PHONE_NUMBER_ID", "VERIFY_TOKEN")


def prototype_values(archive: Path) -> dict[str, str]:
    with zipfile.ZipFile(archive) as bundle:
        candidates = [name for name in bundle.namelist() if name.endswith("/webhook.py") and not name.startswith("__MACOSX/")]
        if len(candidates) != 1:
            raise ValueError("archive must contain exactly one webhook.py")
        module = ast.parse(bundle.read(candidates[0]).decode("utf-8"))
    values: dict[str, str] = {}
    for statement in module.body:
        if not isinstance(statement, ast.Assign) or len(statement.targets) != 1:
            continue
        target = statement.targets[0]
        if isinstance(target, ast.Name) and target.id in SOURCE_KEYS and isinstance(statement.value, ast.Constant):
            values[target.id] = str(statement.value.value).strip()
    if any(not values.get(key) for key in SOURCE_KEYS):
        raise ValueError("prototype credentials are incomplete")
    return values


def parse_env(path: Path) -> tuple[list[str], dict[str, str]]:
    rows = path.read_text(encoding="utf-8").splitlines()
    values = dict(row.split("=", 1) for row in rows if row and not row.startswith("#") and "=" in row)
    return rows, values


def merge_env(path: Path, updates: dict[str, str]) -> None:
    rows, _ = parse_env(path)
    prefixes = tuple(f"{key}=" for key in updates)
    merged = [row for row in rows if not row.startswith(prefixes)]
    merged.extend(f"{key}={value}" for key, value in updates.items())
    descriptor, temporary_name = tempfile.mkstemp(prefix=".env.prod.", dir=path.parent)
    temporary = Path(temporary_name)
    try:
        with os.fdopen(descriptor, "w", encoding="utf-8") as stream:
            stream.write("\n".join(merged) + "\n")
        os.chmod(temporary, 0o600)
        os.replace(temporary, path)
    finally:
        temporary.unlink(missing_ok=True)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--archive", type=Path, required=True)
    parser.add_argument("--source-env", type=Path, required=True)
    parser.add_argument("--business-account-id", required=True)
    arguments = parser.parse_args()
    prototype = prototype_values(arguments.archive)
    _, current = parse_env(arguments.source_env)
    if not current.get("WHATSAPP_CLOUD_APP_SECRET"):
        raise ValueError("WHATSAPP_CLOUD_APP_SECRET is missing")
    merge_env(arguments.source_env, {
        "WHATSAPP_CLOUD_ACCESS_TOKEN": prototype["ACCESS_TOKEN"],
        "WHATSAPP_CLOUD_PHONE_NUMBER_ID": prototype["PHONE_NUMBER_ID"],
        "WHATSAPP_CLOUD_VERIFY_TOKEN": prototype["VERIFY_TOKEN"],
        "WHATSAPP_CLOUD_BUSINESS_ACCOUNT_ID": arguments.business_account_id,
        "WHATSAPP_CLOUD_API_VERSION": "v20.0",
    })
    print("WhatsApp Cloud configuration imported without exposing values.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
