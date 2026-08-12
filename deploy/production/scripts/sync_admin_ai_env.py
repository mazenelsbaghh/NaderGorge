#!/usr/bin/env python3
"""Enable Admin AI in root-owned app environments without exposing secrets."""

from __future__ import annotations

import argparse
from pathlib import Path
import uuid

from production_inventory import load_inventory, operator_transport
from ssh_transport import SshTarget


ROOT = Path(__file__).resolve().parents[3]
DEFAULT_KNOWN_HOSTS = Path.home() / ".ssh" / "massar_prod_known_hosts"
DEFAULT_IDENTITY_FILE = Path.home() / ".ssh" / "massar_prod_cluster_ed25519"


def target(inventory: object, node: object) -> SshTarget:
    return SshTarget(node.id, node.public_address, inventory.cluster["ssh_user"])


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--yes", action="store_true")
    arguments = parser.parse_args()

    import os

    os.environ.setdefault("MASSAR_KNOWN_HOSTS_FILE", str(DEFAULT_KNOWN_HOSTS))
    os.environ.setdefault("MASSAR_SSH_IDENTITY_FILE", str(DEFAULT_IDENTITY_FILE))
    inventory = load_inventory(
        ROOT / "deploy" / "production" / "inventory" / "production.yml",
        require_operator_files=True,
    )
    transport = operator_transport(inventory)
    for node in inventory.nodes:
        remote = target(inventory, node)
        transport.run(remote, ["test", "-f", "/etc/massar/app.env"])
        print(f"{node.id}: ready")

    if not arguments.yes:
        print("Preview complete. Re-run with --yes to enable Admin AI.")
        return 0

    remote_code = (
        "from pathlib import Path; import base64,hashlib,os; "
        "src=Path('/etc/massar/app.env'); rows=src.read_text(encoding='utf-8').splitlines(); "
        "values=dict(line.split('=',1) for line in rows if line and not line.startswith('#') and '=' in line); "
        "secret=values.get('AI_CALLBACK_SECRET',''); "
        "assert len(secret)>=32; "
        "hmac=base64.b64encode(hashlib.sha256(b'massar-admin-ai-hmac-v1\\0'+secret.encode()).digest()).decode(); "
        "rows=[line for line in rows if not line.startswith(('ADMIN_AI_ENABLED=','ADMIN_AI_HMAC_KEY='))]; "
        "rows.extend(['ADMIN_AI_ENABLED=true','ADMIN_AI_HMAC_KEY='+hmac]); "
        "dst=Path(os.environ['MASSAR_STAGED_ENV']); dst.write_text('\\n'.join(rows)+'\\n',encoding='utf-8'); os.chmod(dst,0o640)"
    )
    for node in inventory.nodes:
        remote = target(inventory, node)
        staged = f"/home/massar-ops/.massar-app-env-{uuid.uuid4().hex}"
        transport.run(remote, ["env", f"MASSAR_STAGED_ENV={staged}", "python3", "-c", remote_code])
        transport.run(remote, ["sudo", "/usr/bin/install", "-m", "0640", "-o", "root", "-g", "massar", staged, "/etc/massar/app.env"])
        transport.run(remote, ["rm", "-f", staged])
        print(f"{node.id}: enabled")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
