#!/usr/bin/env python3
"""Synchronize the dedicated AI media relay secret without exposing it."""

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


def read_secret(path: Path) -> str:
    value = path.read_text(encoding="utf-8").strip()
    if len(value) < 32 or "\n" in value or "\r" in value:
        raise ValueError("AI media relay secret must be a single value of at least 32 characters")
    return value


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--secret-file", type=Path, required=True)
    parser.add_argument("--yes", action="store_true")
    arguments = parser.parse_args()
    secret = read_secret(arguments.secret_file)
    inventory = load_inventory(
        ROOT / "deploy/production/inventory/production.yml",
        require_operator_files=True,
    )
    transport = operator_transport(inventory)
    for node in inventory.nodes:
        transport.run(target(inventory, node), ["test", "-f", "/etc/massar/app.env"])
        print(f"{node.id}: ready")
    if not arguments.yes:
        print("Preview complete. Re-run with --yes to synchronize the relay secret.")
        return 0

    with tempfile.NamedTemporaryFile(mode="w", encoding="utf-8", delete=False) as stream:
        os.chmod(stream.name, 0o600)
        stream.write(secret + "\n")
        local_stage = Path(stream.name)
    try:
        for node in inventory.nodes:
            remote = target(inventory, node)
            incoming = f"/home/massar-ops/.massar-ai-media-{uuid.uuid4().hex}"
            staged = f"/home/massar-ops/.massar-app-env-{uuid.uuid4().hex}"
            transport.copy(remote, local_stage, incoming)
            merge_code = (
                "from pathlib import Path; import os; "
                f"secret=Path('{incoming}').read_text(encoding='utf-8').strip(); "
                "assert len(secret)>=32 and '\\n' not in secret and '\\r' not in secret; "
                "src=Path('/etc/massar/app.env'); rows=src.read_text(encoding='utf-8').splitlines(); "
                "rows=[line for line in rows if not line.startswith('AI_MEDIA_RELAY_SECRET=')]; "
                "rows.append('AI_MEDIA_RELAY_SECRET='+secret); "
                f"dst=Path('{staged}'); dst.write_text('\\n'.join(rows)+'\\n',encoding='utf-8'); os.chmod(dst,0o640)"
            )
            transport.run(remote, ["python3", "-c", merge_code], timeout_seconds=30)
            transport.run(
                remote,
                ["sudo", "/usr/bin/install", "-m", "0640", "-o", "root", "-g", "massar", staged, "/etc/massar/app.env"],
                timeout_seconds=30,
            )
            transport.run(remote, ["rm", "-f", incoming, staged], timeout_seconds=10)
            print(f"{node.id}: synchronized")
    finally:
        local_stage.unlink(missing_ok=True)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
