#!/usr/bin/env python3
"""Install a published, reviewed image-only maintenance timer, one node at a time."""
from __future__ import annotations

import argparse
import datetime as dt
import hashlib
import json
import shlex
import tempfile
import uuid
from pathlib import Path
from contextlib import contextmanager

from clusterctl import load_inventory, operator_transport, target
from deploy_release import RolloutLock
from source_sync import git, tip, record_publication

ROOT = Path(__file__).resolve().parents[3]
SCRIPT = "deploy/production/scripts/scheduled_image_cleanup.py"
SERVICE = "deploy/production/systemd/massar-image-cleanup.service"
TIMER = "deploy/production/systemd/massar-image-cleanup.timer.in"
INSTALLER = "deploy/production/scripts/install_image_cleanup.py"
ASSETS = (SCRIPT, SERVICE, TIMER, INSTALLER)


def verify_source(commit: str) -> None:
    if tip(ROOT) != commit:
        raise RuntimeError("Shared source advanced or the maintenance candidate is unpublished")
    for relative in ASSETS:
        if git(ROOT, "hash-object", "--", relative) != git(ROOT, "rev-parse", commit + ":" + relative):
            raise RuntimeError("Local maintenance assets differ from the published source")


def assets_for_node(inventory, node) -> dict[str, bytes]:
    builder = next(member for member in inventory.nodes if member.id == "node-3")
    configuration = {"nodeId": node.id,
                     "overlayAddresses": [member.overlay_address for member in inventory.nodes],
                     "repositoryPrefixes": ["massar", builder.overlay_address + ":5443/massar"]}
    hour = {"node-3": "04", "node-2": "05", "node-1": "06"}[node.id]
    service = (ROOT / SERVICE).read_bytes()
    return {
        "/usr/local/sbin/massar-image-cleanup": (ROOT / SCRIPT).read_bytes(),
        "/etc/massar/image-cleanup.json": (json.dumps(configuration, indent=2) + "\n").encode(),
        "/etc/systemd/system/massar-image-cleanup.service": service,
        "/etc/systemd/system/massar-image-cleanup-preview.service": service.replace(b"--yes", b"--dry-run"),
        "/etc/systemd/system/massar-image-cleanup.timer":
            (ROOT / TIMER).read_text().replace("@HOUR@", hour).encode(),
    }


def preview_node(transport, remote, assets: dict[str, bytes]) -> dict:
    paths = list(assets)
    inspection = """import hashlib,json
from pathlib import Path
report={}
for destination in PATHS:
 p=Path(destination)
 if p.is_symlink():raise SystemExit('Refusing a symlink maintenance destination')
 if p.exists():
  try: report[destination]=hashlib.sha256(p.read_bytes()).hexdigest()
  except PermissionError: report[destination]='unreadable'
 else:report[destination]=None
print(json.dumps(report))
""".replace("PATHS", repr(paths))
    existing = json.loads(transport.run(remote, ["python3", "-c", inspection]).stdout)
    for destination, previous in existing.items():
        expected = hashlib.sha256(assets[destination]).hexdigest()
        if previous not in (None, expected):
            raise RuntimeError(f"Existing maintenance asset differs: {remote.node_id} {destination}")
    return existing


def installation_commands(assets: dict[str, bytes], existing: dict, stage: str) -> list[str]:
    commands = ["set -euo pipefail"]
    for index, (destination, content) in enumerate(assets.items()):
        digest = hashlib.sha256(content).hexdigest()
        staged = stage + "/" + str(index)
        permissions = "0755" if destination.endswith("/massar-image-cleanup") else "0644"
        if existing[destination] is None:
            commands.append("test ! -e " + shlex.quote(destination))
        else:
            commands.append(f"test \"$(sha256sum {destination} | cut -d' ' -f1)\" = {existing[destination]}")
        commands.extend([
            f"printf '%s  %s\\n' {digest} {staged} | sha256sum -c -",
            f"sudo -n /usr/bin/install -o root -g root -m {permissions} {staged} {destination}",
            f"test \"$(stat -c '%U:%G:%a' {destination})\" = root:root:{permissions[1:]}",
        ])
    return commands


@contextmanager
def staged_assets(transport, remote, assets: dict[str, bytes]):
    stage = "/tmp/massar-image-cleanup-" + uuid.uuid4().hex
    transport.run(remote, ["install", "-d", "-m", "0700", stage])
    try:
        with tempfile.TemporaryDirectory(prefix="massar-cleanup-assets-") as temporary:
            for index, content in enumerate(assets.values()):
                local = Path(temporary) / str(index)
                local.write_bytes(content)
                transport.copy(remote, local, stage + "/" + str(index))
        yield stage
    finally:
        transport.run(remote, ["bash", "-lc", "rm -rf -- " + shlex.quote(stage)])


def install_assets(transport, remote, assets: dict[str, bytes], existing: dict) -> None:
    with staged_assets(transport, remote, assets) as stage:
        commands = installation_commands(assets, existing, stage)
        commands.extend([
            "if ! test -e /srv/massar-shared/.image-cleanup.lock; then sudo -n /usr/bin/install -o root -g root -m 0600 /dev/null /srv/massar-shared/.image-cleanup.lock; fi",
            "systemd-analyze verify /etc/systemd/system/massar-image-cleanup.service /etc/systemd/system/massar-image-cleanup-preview.service /etc/systemd/system/massar-image-cleanup.timer",
            "sudo -n /usr/bin/systemctl daemon-reload",
        ])
        transport.run(remote, ["bash", "-lc", "\n".join(commands)], timeout_seconds=90)


def run_service(transport, remote, unit: str, expected: str) -> list[dict]:
    started_at = transport.run(remote, ["date", "--utc", "+%Y-%m-%d %H:%M:%S UTC"]).stdout.strip()
    started = transport.run(remote, ["sudo", "-n", "/usr/bin/systemctl", "start", unit],
                            timeout_seconds=1900, check=False)
    journal = transport.run(remote, ["sudo", "-n", "/usr/bin/journalctl", "--no-pager", "-o", "cat",
                                     "-u", unit, "--since", started_at], timeout_seconds=30).stdout
    reports = [json.loads(line) for line in journal.splitlines() if line.startswith("{")]
    print(json.dumps({"node": remote.node_id, "unit": unit, "reports": reports}), flush=True)
    if started.returncode or not reports or reports[-1].get("status") != expected:
        raise RuntimeError(f"{remote.node_id}: maintenance {unit} did not complete; timer remains disabled")
    return reports


def enable_node(transport, remote) -> str:
    transport.run(remote, ["sudo", "-n", "/usr/bin/systemctl", "enable", "--now", "massar-image-cleanup.timer"])
    transport.run(remote, ["systemctl", "is-active", "--quiet", "massar-image-cleanup.timer"])
    transport.run(remote, ["systemctl", "is-enabled", "--quiet", "massar-image-cleanup.timer"])
    return transport.run(remote, ["systemctl", "show", "massar-image-cleanup.timer", "-p", "NextElapseUSecRealtime", "--value"]).stdout.strip()


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    mode = parser.add_mutually_exclusive_group(required=True)
    mode.add_argument("--dry-run", action="store_true")
    mode.add_argument("--yes", action="store_true")
    parser.add_argument("--source", required=True)
    arguments = parser.parse_args()
    verify_source(arguments.source)
    inventory = load_inventory(ROOT / "deploy/production/inventory/production.yml", require_operator_files=True)
    transport = operator_transport(inventory)
    lock = RolloutLock(transport, target(inventory, inventory.nodes[0]), str(uuid.uuid4()))
    evidence = {"sourceCommit": arguments.source, "nodes": []}
    if arguments.yes:
        lock.acquire()
    try:
        for node in reversed(inventory.nodes):
            verify_source(arguments.source)
            remote = target(inventory, node)
            assets = assets_for_node(inventory, node)
            existing = preview_node(transport, remote, assets)
            print(json.dumps({"node": node.id, "mode": "preview" if arguments.dry_run else "install",
                              "assets": {path: hashlib.sha256(content).hexdigest() for path, content in assets.items()}}), flush=True)
            if arguments.dry_run:
                continue
            install_assets(transport, remote, assets, existing)
            preview = run_service(transport, remote, "massar-image-cleanup-preview.service", "preview-completed")
            applied = run_service(transport, remote, "massar-image-cleanup.service", "completed")
            next_run = enable_node(transport, remote)
            evidence["nodes"].append({"node": node.id, "preview": preview, "firstRun": applied, "nextRun": next_run})
        if arguments.yes:
            record_publication(transport, target(inventory, inventory.nodes[0]), arguments.source, "deployed")
    finally:
        if arguments.yes:
            lock.release()
        directory = ROOT / "artifacts/production/image-cleanup-install"
        directory.mkdir(parents=True, exist_ok=True)
        stamp = dt.datetime.now(dt.timezone.utc).strftime("%Y%m%dT%H%M%SZ")
        (directory / (stamp + ".json")).write_text(json.dumps(evidence, indent=2) + "\n")


if __name__ == "__main__":
    main()
