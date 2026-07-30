#!/usr/bin/env python3
"""No-echo wrapper for the atomic .NET Admin bootstrap tool."""

from __future__ import annotations

import argparse
import getpass
import os
import subprocess
import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parents[3]
PROJECT = ROOT / "backend/src/NaderGorge.AdminBootstrap/NaderGorge.AdminBootstrap.csproj"


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--name", default="Production Owner")
    parser.add_argument("--phone")
    args = parser.parse_args()
    if not os.environ.get("ConnectionStrings__DefaultConnection"):
        print("Admin bootstrap blocked: database connection reference is missing", file=sys.stderr)
        return 3
    phone = args.phone or input("Admin phone: ").strip()
    password = getpass.getpass("Admin password: ")
    confirmation = getpass.getpass("Confirm password: ")
    if password != confirmation:
        print("Admin bootstrap blocked: passwords do not match", file=sys.stderr)
        return 2
    completed = subprocess.run(
        ["dotnet", "run", "--project", str(PROJECT), "--configuration", "Release", "--no-launch-profile"],
        input=f"{phone}\n{password}\n{args.name}\n",
        text=True,
        check=False,
    )
    password = ""
    confirmation = ""
    return completed.returncode


if __name__ == "__main__":
    raise SystemExit(main())
