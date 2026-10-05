#!/usr/bin/env python3
"""Install independent exam webhook settings without changing platform WhatsApp."""

from __future__ import annotations

import argparse
import json
import re
import uuid
from pathlib import Path

from clusterctl import load_inventory, operator_transport, target
from sync_whatsapp_cloud_env import ROOT, staged_file


RUNTIME_FIELDS = {
    "phoneNumberId": "ExamWhatsAppCloud__PhoneNumberId",
    "businessAccountId": "ExamWhatsAppCloud__BusinessAccountId",
    "verifyToken": "ExamWhatsAppCloud__VerifyToken",
    "appSecret": "ExamWhatsAppCloud__AppSecret",
}


def configuration(path: Path) -> dict[str, str]:
    configured = json.loads(path.read_text(encoding="utf-8"))
    runtime = {}
    for source, destination in RUNTIME_FIELDS.items():
        supplied = configured.get(source)
        if not isinstance(supplied, str) or not supplied or any(char.isspace() for char in supplied):
            raise ValueError(f"invalid exam webhook setting: {source}")
        runtime[destination] = supplied
    if any(not re.fullmatch(r"[0-9]{5,30}", configured[key]) for key in ("phoneNumberId", "businessAccountId")):
        raise ValueError("invalid exam account identifiers")
    if configured["phoneNumberId"] == configured["businessAccountId"]:
        raise ValueError("exam account and phone identifiers must differ")
    return runtime


def synchronize_node(transport, remote, local_stage: Path) -> None:
    incoming = f"/home/massar-ops/.massar-exam-whatsapp-{uuid.uuid4().hex}"
    merged = f"/home/massar-ops/.massar-exam-env-{uuid.uuid4().hex}"
    try:
        transport.copy(remote, local_stage, incoming)
        merge_code = (
            "from pathlib import Path; import os; "
            f"updates=dict(row.split('=',1) for row in Path('{incoming}').read_text().splitlines()); "
            "assert all(key.startswith('ExamWhatsAppCloud__') for key in updates); "
            "rows=Path('/etc/massar/app.env').read_text().splitlines(); prefixes=tuple(key+'=' for key in updates); "
            "rows=[row for row in rows if not row.startswith(prefixes)]; rows.extend(key+'='+value for key,value in updates.items()); "
            f"destination=Path('{merged}'); destination.write_text('\\n'.join(rows)+'\\n'); os.chmod(destination,0o640)"
        )
        transport.run(remote, ["python3", "-c", merge_code], timeout_seconds=30)
        transport.run(remote, ["sudo", "/usr/bin/install", "-m", "0640", "-o", "root", "-g", "massar", merged, "/etc/massar/app.env"], timeout_seconds=30)
    finally:
        transport.run(remote, ["rm", "-f", incoming, merged], timeout_seconds=10)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source-json", required=True, type=Path)
    parser.add_argument("--yes", action="store_true")
    arguments = parser.parse_args()
    configured = configuration(arguments.source_json)
    inventory = load_inventory(ROOT / "deploy/production/inventory/production.yml", require_operator_files=True)
    transport = operator_transport(inventory)
    for node in inventory.nodes:
        transport.run(target(inventory, node), ["test", "-f", "/etc/massar/app.env"])
        print(f"{node.id}: ready")
    if not arguments.yes:
        print("Preview complete: only ExamWhatsAppCloud settings will be updated.")
        return 0
    local_stage = staged_file(configured)
    try:
        for node in inventory.nodes:
            synchronize_node(transport, target(inventory, node), local_stage)
            print(f"{node.id}: exam webhook configured")
    finally:
        local_stage.unlink(missing_ok=True)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
