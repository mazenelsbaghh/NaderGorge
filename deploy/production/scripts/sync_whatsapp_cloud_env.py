#!/usr/bin/env python3
"""Synchronize WhatsApp Cloud configuration to root-owned production environments."""

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


SOURCE_TO_RUNTIME = {
    "WHATSAPP_CLOUD_ACCESS_TOKEN": "WhatsAppCloudApi__AccessToken",
    "WHATSAPP_CLOUD_PHONE_NUMBER_ID": "WhatsAppCloudApi__PhoneNumberId",
    "WHATSAPP_CLOUD_BUSINESS_ACCOUNT_ID": "WhatsAppCloudApi__BusinessAccountId",
    "WHATSAPP_CLOUD_VERIFY_TOKEN": "WhatsAppCloudApi__VerifyToken",
    "WHATSAPP_CLOUD_APP_SECRET": "WhatsAppCloudApi__AppSecret",
    "WHATSAPP_CLOUD_API_VERSION": "WhatsAppCloudApi__ApiVersion",
}


def configuration(path: Path) -> dict[str, str]:
    rows = path.read_text(encoding="utf-8").splitlines()
    values = dict(row.split("=", 1) for row in rows if row and not row.startswith("#") and "=" in row)
    missing = [key for key in SOURCE_TO_RUNTIME if not values.get(key)]
    if missing:
        raise ValueError(f"missing WhatsApp configuration keys: {', '.join(missing)}")
    configured = {key: values[key] for key in SOURCE_TO_RUNTIME}
    configured.update({runtime: values[source] for source, runtime in SOURCE_TO_RUNTIME.items()})
    return configured


def staged_file(values: dict[str, str]) -> Path:
    with tempfile.NamedTemporaryFile(mode="w", encoding="utf-8", delete=False) as stream:
        os.chmod(stream.name, 0o600)
        stream.write("\n".join(f"{key}={value}" for key, value in values.items()) + "\n")
        return Path(stream.name)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source-env", type=Path, default=ROOT / ".env.prod")
    parser.add_argument("--yes", action="store_true")
    arguments = parser.parse_args()
    values = configuration(arguments.source_env)
    inventory = load_inventory(ROOT / "deploy/production/inventory/production.yml", require_operator_files=True)
    transport = operator_transport(inventory)
    for node in inventory.nodes:
        transport.run(target(inventory, node), ["test", "-f", "/etc/massar/app.env"])
        print(f"{node.id}: ready")
    if not arguments.yes:
        print("Preview complete. Re-run with --yes to synchronize WhatsApp configuration.")
        return 0

    local_stage = staged_file(values)
    try:
        for node in inventory.nodes:
            remote = target(inventory, node)
            source = f"/home/massar-ops/.massar-whatsapp-{uuid.uuid4().hex}"
            destination = f"/home/massar-ops/.massar-app-env-{uuid.uuid4().hex}"
            transport.copy(remote, local_stage, source)
            merge_code = (
                "from pathlib import Path; import os; "
                f"incoming=Path('{source}').read_text(encoding='utf-8').splitlines(); "
                "updates=dict(line.split('=',1) for line in incoming); src=Path('/etc/massar/app.env'); "
                "rows=src.read_text(encoding='utf-8').splitlines(); prefixes=tuple(key+'=' for key in updates); "
                "rows=[line for line in rows if not line.startswith(prefixes)]; rows.extend(key+'='+value for key,value in updates.items()); "
                f"dst=Path('{destination}'); dst.write_text('\\n'.join(rows)+'\\n',encoding='utf-8'); os.chmod(dst,0o640)"
            )
            transport.run(remote, ["python3", "-c", merge_code], timeout_seconds=30)
            transport.run(remote, ["sudo", "/usr/bin/install", "-m", "0640", "-o", "root", "-g", "massar", destination, "/etc/massar/app.env"], timeout_seconds=30)
            transport.run(remote, ["rm", "-f", source, destination], timeout_seconds=10)
            print(f"{node.id}: synchronized")
    finally:
        local_stage.unlink(missing_ok=True)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
