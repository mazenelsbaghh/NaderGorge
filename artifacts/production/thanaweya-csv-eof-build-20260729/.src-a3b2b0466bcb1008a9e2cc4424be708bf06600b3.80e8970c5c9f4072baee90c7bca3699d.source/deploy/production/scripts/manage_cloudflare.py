#!/usr/bin/env python3
"""Install and inspect three replicas of one Cloudflare Tunnel."""

from __future__ import annotations

import argparse
import json
import re
import sys
import tempfile
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path

from clusterctl import load_inventory
from ssh_transport import SshTarget, StrictSshTransport


TUNNEL_ID = re.compile(
    r"^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$",
    re.I,
)
REHEARSAL_HOSTNAME = re.compile(
    r"^[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?\.massar-academy\.net$",
    re.I,
)


class CloudflareError(RuntimeError):
    pass


def render(tunnel_id: str, rehearsal_hostname: str | None = None) -> str:
    template = (
        Path(__file__).resolve().parents[1]
        / "config/cloudflared/config.yml.tmpl"
    ).read_text(encoding="utf-8")
    result = template.replace("__TUNNEL_UUID__", tunnel_id)
    if rehearsal_hostname:
        if not REHEARSAL_HOSTNAME.fullmatch(rehearsal_hostname):
            raise CloudflareError("rehearsal hostname must be a valid massar-academy.net subdomain")
        if f"hostname: {rehearsal_hostname}" in result:
            raise CloudflareError("rehearsal hostname must not duplicate a final hostname")
        rehearsal_ingress = (
            f"  - hostname: {rehearsal_hostname}\n"
            "    service: http://127.0.0.1:8088\n"
            "    originRequest:\n"
            "      httpHostHeader: massar-academy.net\n"
        )
        result = result.replace(
            "  - service: http_status:404\n",
            rehearsal_ingress + "  - service: http_status:404\n",
        )
    if "__" in result:
        raise CloudflareError("cloudflared template contains an unresolved placeholder")
    return result


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--inventory", required=True, type=Path)
    parser.add_argument("--known-hosts", required=True, type=Path)
    parser.add_argument("--identity", required=True, type=Path)
    parser.add_argument("--tunnel-id")
    parser.add_argument("--credentials", type=Path)
    parser.add_argument("--rehearsal-hostname")
    parser.add_argument("--dry-run", action="store_true")
    parser.add_argument("--yes", action="store_true")
    parser.add_argument("action", choices=("install", "status"))
    args = parser.parse_args()
    inventory = load_inventory(args.inventory)
    transport = StrictSshTransport(args.known_hosts, args.identity)
    targets = [
        SshTarget(node.id, node.public_address, inventory.cluster["ssh_user"])
        for node in inventory.nodes
    ]

    if args.action == "status":
        def status(target: SshTarget) -> dict[str, object]:
            output = transport.run(
                target,
                (
                    "bash", "-lc",
                    "systemctl is-active cloudflared; "
                    "curl --fail --silent http://127.0.0.1:2000/ready; "
                    "curl --fail --silent http://127.0.0.1:2000/metrics | "
                    "grep -E 'cloudflared_tunnel_(total_requests|ha_connections)' | head -n 8",
                ),
                check=False,
            )
            return {
                "node": target.node_id,
                "healthy": output.returncode == 0,
                "summary": output.stdout.splitlines()[:10],
            }
        with ThreadPoolExecutor(max_workers=3) as pool:
            values = list(pool.map(status, targets))
        print(json.dumps({"connectors": values}, sort_keys=True))
        return 0 if all(value["healthy"] for value in values) else 6

    if not args.tunnel_id or not TUNNEL_ID.fullmatch(args.tunnel_id):
        raise CloudflareError("install requires a valid --tunnel-id")
    if not args.credentials or not args.credentials.is_file():
        raise CloudflareError("install requires the external credentials JSON file")
    if args.credentials.stat().st_mode & 0o077:
        raise CloudflareError("Cloudflare credentials must be mode 0600")
    credential_data = json.loads(args.credentials.read_text(encoding="utf-8"))
    if credential_data.get("TunnelID") != args.tunnel_id:
        raise CloudflareError("credentials TunnelID does not match --tunnel-id")
    if args.dry_run:
        print(json.dumps({"tunnelId": args.tunnel_id, "replicas": 3, "status": "dry-run"}))
        return 0
    if not args.yes:
        raise CloudflareError("install requires --yes or --dry-run")

    with tempfile.NamedTemporaryFile(mode="w", encoding="utf-8", delete=False) as handle:
        handle.write(render(args.tunnel_id, args.rehearsal_hostname))
        config = Path(handle.name)
    config.chmod(0o600)
    try:
        for target in targets:
            transport.copy(target, config, "/tmp/massar-cloudflared-config.yml")
            transport.copy(target, args.credentials, "/tmp/massar-cloudflared-credentials.json")
            transport.run(
                target,
                (
                    "bash", "-lc",
                    "sudo install -d -m 0750 -o root -g massar /etc/cloudflared && "
                    "sudo install -m 0640 -o root -g massar /tmp/massar-cloudflared-config.yml /etc/cloudflared/config.yml && "
                    "sudo install -m 0600 -o root -g root /tmp/massar-cloudflared-credentials.json /etc/cloudflared/credentials.json && "
                    "rm -f /tmp/massar-cloudflared-config.yml /tmp/massar-cloudflared-credentials.json && "
                    "sudo systemctl enable --now cloudflared",
                ),
                timeout_seconds=90,
            )
    finally:
        config.unlink(missing_ok=True)
    print(json.dumps({"tunnelId": args.tunnel_id, "replicas": 3, "status": "installed"}))
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (CloudflareError, OSError, ValueError, json.JSONDecodeError) as exc:
        print(f"Cloudflare operation blocked: {exc}", file=sys.stderr)
        raise SystemExit(6)
