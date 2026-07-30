#!/usr/bin/env python3
"""Create short-lived load-test token files without exposing credentials.

The credential source and generated token files are deliberately external to
the repository.  This helper never prints either secret and refuses weak file
permissions, so the live load runner can exercise authenticated SignalR and
workflow probes with a disposable account.
"""

from __future__ import annotations

import argparse
import json
import os
import stat
import sys
import urllib.error
import urllib.request
from pathlib import Path


class TokenPreparationError(ValueError):
    pass


def private_regular_file(path: Path, label: str) -> Path:
    resolved = path.expanduser().resolve()
    if not resolved.is_file() or resolved.is_symlink():
        raise TokenPreparationError(f"{label} must be a regular file")
    if stat.S_IMODE(resolved.stat().st_mode) & 0o077:
        raise TokenPreparationError(f"{label} must be mode 0600")
    return resolved


def write_token(path: Path, token: str) -> None:
    target = path.expanduser().resolve()
    if target.exists() or target.is_symlink() or not target.parent.is_dir():
        raise TokenPreparationError("token output must be a new file in an existing directory")
    descriptor = os.open(target, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
    try:
        os.write(descriptor, token.encode("utf-8"))
        os.write(descriptor, b"\n")
    finally:
        os.close(descriptor)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--credentials-file", required=True, type=Path)
    parser.add_argument("--api-origin", required=True)
    parser.add_argument(
        "--surface",
        default="admin",
        choices=("student", "admin", "teacher", "staff"),
        help="Application surface authorized for the disposable test account (default: admin)",
    )
    parser.add_argument("--websocket-output", required=True, type=Path)
    parser.add_argument("--workflow-output", required=True, type=Path)
    parser.add_argument("--yes", action="store_true")
    args = parser.parse_args()
    try:
        if not args.yes:
            raise TokenPreparationError("state-changing token creation requires --yes")
        credentials = json.loads(private_regular_file(args.credentials_file, "credentials file").read_text(encoding="utf-8"))
        phone = credentials.get("phoneNumber")
        password = credentials.get("password")
        if not isinstance(phone, str) or not phone or not isinstance(password, str) or not password:
            raise TokenPreparationError("credentials require phoneNumber and password")
        origin = args.api_origin.rstrip("/")
        if not origin.startswith("https://"):
            raise TokenPreparationError("API origin must use HTTPS")
        payload = json.dumps({
            "phoneNumber": phone,
            "password": password,
            "deviceFingerprint": "massar-production-load-test",
            "deviceName": "Massar production load test",
        }).encode("utf-8")
        request = urllib.request.Request(
            f"{origin}/api/auth/login",
            data=payload,
            method="POST",
            headers={
                "Accept": "application/json",
                "Content-Type": "application/json",
                "User-Agent": "MassarProductionLoadTest/1.0",
                "X-App-Surface": args.surface,
            },
        )
        with urllib.request.urlopen(request, timeout=15) as response:
            body = json.loads(response.read().decode("utf-8"))
        token = body.get("data", {}).get("accessToken")
        if not isinstance(token, str) or token.count(".") != 2:
            raise TokenPreparationError("login did not return an access token")
        write_token(args.websocket_output, token)
        try:
            write_token(args.workflow_output, token)
        except Exception:
            args.websocket_output.unlink(missing_ok=True)
            raise
        print(json.dumps({"status": "success", "tokenFilesCreated": 2}))
        return 0
    except (TokenPreparationError, OSError, urllib.error.URLError, json.JSONDecodeError) as exc:
        print(f"load test token preparation blocked: {exc}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
