#!/usr/bin/env python3
"""Synchronize the OCR Vision key to root-owned app environments without logging it."""

from __future__ import annotations

import argparse
import os
import sys
import tempfile
import uuid
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
sys.path.insert(0, str(ROOT / "deploy" / "production" / "scripts"))

from clusterctl import load_inventory, operator_transport, target  # noqa: E402

KEY = "GOOGLE_CLOUD_VISION_API_KEY"
DEFAULT_KNOWN_HOSTS = "/Users/mazenelsbagh/.ssh/massar_prod_known_hosts"
DEFAULT_IDENTITY_FILE = "/Users/mazenelsbagh/.ssh/massar_prod_cluster_ed25519"


def key_from_env(path: Path) -> str:
    for line in path.read_text(encoding="utf-8").splitlines():
        if line.startswith(f"{KEY}="):
            value = line.split("=", 1)[1].strip()
            if value and "\n" not in value and "\r" not in value:
                return value
    raise ValueError(f"{KEY} is missing or empty in {path}")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--source-env", type=Path, default=ROOT / ".env.prod")
    parser.add_argument("--yes", action="store_true")
    args = parser.parse_args()
    os.environ.setdefault("MASSAR_KNOWN_HOSTS_FILE", DEFAULT_KNOWN_HOSTS)
    os.environ.setdefault("MASSAR_SSH_IDENTITY_FILE", DEFAULT_IDENTITY_FILE)
    key = key_from_env(args.source_env)
    inventory = load_inventory(
        ROOT / "deploy" / "production" / "inventory" / "production.yml",
        require_operator_files=True,
    )
    transport = operator_transport(inventory)

    for node in inventory.nodes:
        remote = target(inventory, node)
        transport.run(remote, ["test", "-f", "/etc/massar/app.env"])
        print(f"{node.id}: ready")

    if not args.yes:
        print("Preview complete. Re-run with --yes to synchronize the OCR key.")
        return 0

    with tempfile.NamedTemporaryFile(mode="w", encoding="utf-8", delete=False) as staged:
        os.chmod(staged.name, 0o600)
        staged.write(f"{KEY}={key}\n")
        staged_path = Path(staged.name)
    try:
        for node in inventory.nodes:
            remote = target(inventory, node)
            destination = f"/home/massar-ops/.massar-ocr-key-{uuid.uuid4().hex}"
            staged_env = f"/home/massar-ops/.massar-app-env-{uuid.uuid4().hex}"
            transport.copy(remote, staged_path, destination)
            py_code = (
                "from pathlib import Path; import os; "
                f"src=Path('{destination}'); dst=Path('/etc/massar/app.env'); tmp=Path('{staged_env}'); "
                "line=src.read_text(encoding='utf-8').strip(); "
                "rows=[x for x in dst.read_text(encoding='utf-8').splitlines() if not x.startswith('GOOGLE_CLOUD_VISION_API_KEY=')]; "
                "rows.append(line); tmp.write_text('\\n'.join(rows)+'\\n', encoding='utf-8'); "
                "os.chmod(tmp, 0o640)"
            )
            transport.run(remote, ["python3", "-c", py_code], timeout_seconds=30)
            transport.run(
                remote,
                ["sudo", "/usr/bin/install", "-m", "0640", "-o", "root", "-g", "massar", staged_env, "/etc/massar/app.env"],
                timeout_seconds=30,
            )
            transport.run(remote, ["rm", "-f", destination, staged_env], timeout_seconds=10)
            print(f"{node.id}: synchronized")
    finally:
        staged_path.unlink(missing_ok=True)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
