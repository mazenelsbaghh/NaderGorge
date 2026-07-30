#!/usr/bin/env python3
"""Apply narrow, evidence-backed repairs only inside the disposable legacy stage."""

from __future__ import annotations

import argparse
import base64
import datetime as dt
import hashlib
import json
import subprocess
from pathlib import Path
from urllib.parse import unquote, urlsplit


CONTAINER = "massar-legacy-stage-166"


def psql(query: str) -> str:
    argv = [
        "docker", "exec", CONTAINER,
        "psql", "-XAt", "-v", "ON_ERROR_STOP=1",
        "-U", "postgres", "-d", "massar_platform",
    ]
    argv.extend(["-c", query])
    completed = subprocess.run(argv, text=True, capture_output=True, check=False)
    if completed.returncode:
        raise RuntimeError(completed.stderr.strip() or "staging repair failed")
    return completed.stdout.strip()


def candidates(value: str, roots: list[Path]) -> list[Path]:
    path = unquote(urlsplit(value).path or value).replace("\\", "/").lstrip("/")
    variants = {path}
    for prefix in (
        "assets/", "uploads/", "wwwroot/", "app/App_Data/", "App_Data/",
        "protected/resources/",
    ):
        if path.startswith(prefix):
            variants.add(path[len(prefix):])
    return [root / variant for root in roots for variant in variants if variant]


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--backup-dir", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    backup = args.backup_dir.expanduser().resolve()
    normalized_provider_rows = psql(
        'update lesson_videos '
        'set "Provider"=lower(trim("Provider")), "UpdatedAt"=now() '
        'where lower(trim("Provider")) in '
        "('youtube','bunny','vk','telegram','telegram-direct','rutube','google-drive') "
        'and "Provider"<>lower(trim("Provider")) returning 1;'
    ).splitlines().count("1")
    roots = [
        backup / "staging-files-assets",
        backup / "staging-files-protected",
        backup / "staging-files-app-data",
    ]
    missing = []
    for value in psql(
        'select "StoragePath" from live_support_attachments where not "IsBlocked" order by "Id";'
    ).splitlines():
        if value and not any(path.is_file() for path in candidates(value, roots)):
            missing.append(value)
    for index, value in enumerate(missing):
        encoded = base64.b64encode(value.encode()).decode("ascii")
        affected = psql(
            'update live_support_attachments '
            'set "IsBlocked"=true, "UpdatedAt"=now() '
            f"""where "StoragePath"=convert_from(decode('{encoded}','base64'),'UTF8') """
            'and not "IsBlocked" returning 1;',
        )
        if affected.splitlines().count("1") != 1:
            raise RuntimeError(f"attachment repair {index} did not affect exactly one row")
    payload = {
        "schemaVersion": 1,
        "capturedAt": dt.datetime.now(dt.timezone.utc).isoformat().replace("+00:00", "Z"),
        "status": "success",
        "isolated": True,
        "repairs": [
            "normalize-supported-video-providers",
            "block-missing-live-support-attachments",
        ],
        "affectedRows": len(missing),
        "normalizedProviderRows": normalized_provider_rows,
        "affectedPathSetSha256": hashlib.sha256(
            "\n".join(sorted(missing)).encode()
        ).hexdigest(),
        "contentDeleted": False,
    }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
    args.output.chmod(0o640)
    print(json.dumps({
        "status": "success",
        "affectedRows": len(missing),
        "normalizedProviderRows": normalized_provider_rows,
        "output": str(args.output),
    }))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
