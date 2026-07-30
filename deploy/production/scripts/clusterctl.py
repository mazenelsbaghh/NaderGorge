#!/usr/bin/env python3
"""Safe entry point for Massar production cluster operations."""

from __future__ import annotations

import argparse
import datetime as dt
import ipaddress
import json
import os
import re
import shlex
import shutil
import subprocess
import sys
import uuid
from dataclasses import dataclass
from pathlib import Path
from typing import Any

SCRIPT_DIR = Path(__file__).resolve().parent
if str(SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(SCRIPT_DIR))

from audit_hosts import audit_node, validate_clean_host
from bootstrap_cluster import bootstrap_foundation
from release_images import (
    assert_source_unchanged,
    build_release,
    create_release_manifest_v2,
    ReleaseManifestInputs,
    create_source_snapshot,
    create_release_bundle,
    distribute_release,
    publish_final_manifest,
    resolve_release,
    verify_local_release_artifacts,
    write_json_atomic,
)
from remote_build_release import create_remote_build_plan
from remote_builder_workflow import run_remote_builder_workflow
from ssh_transport import SshTarget, StrictSshTransport


EXIT_OK = 0
EXIT_USAGE = 2
EXIT_PREFLIGHT = 3
EXIT_PARTIAL = 4
EXIT_SAFETY = 5
EXIT_VERIFY = 6
NODE_IDS = ("node-1", "node-2", "node-3")
BUILDER_NODE_ID = "node-3"
SECRET_KEY_RE = re.compile(r"(password|passwd|token|secret|private.?key|credential)", re.I)


@dataclass(frozen=True)
class Node:
    id: str
    ssh_alias: str
    public_address: str
    overlay_address: str
    roles: tuple[str, ...]


@dataclass(frozen=True)
class Inventory:
    path: Path
    cluster: dict[str, Any]
    nodes: tuple[Node, ...]
    hostnames: dict[str, str]


def utc_now() -> str:
    return dt.datetime.now(dt.timezone.utc).isoformat().replace("+00:00", "Z")


def expand_reference(value: str, field: str) -> str:
    match = re.fullmatch(r"\$\{([A-Z][A-Z0-9_]*)\}", value)
    if not match:
        return value
    resolved = os.environ.get(match.group(1), "")
    if not resolved:
        raise ValueError(f"required environment reference for {field} is unset")
    return resolved


def reject_secret_keys(value: Any, path: str = "inventory") -> None:
    if isinstance(value, dict):
        for key, child in value.items():
            if SECRET_KEY_RE.search(str(key)):
                raise ValueError(f"{path} contains forbidden secret-like field: {key}")
            reject_secret_keys(child, f"{path}.{key}")
    elif isinstance(value, list):
        for index, child in enumerate(value):
            reject_secret_keys(child, f"{path}[{index}]")


def load_inventory(path: Path, require_operator_files: bool = False) -> Inventory:
    if not path.is_file():
        raise ValueError(f"inventory not found: {path}")
    try:
        raw = json.loads(path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as exc:
        raise ValueError(f"inventory must use the JSON-compatible YAML subset: {exc}") from exc
    if not isinstance(raw, dict):
        raise ValueError("inventory root must be a mapping")
    reject_secret_keys(raw)
    if set(raw) != {"cluster", "nodes", "hostnames"}:
        raise ValueError("inventory must contain only cluster, nodes, and hostnames")

    cluster = dict(raw["cluster"])
    if cluster.get("name") != "massar-production":
        raise ValueError("inventory cluster name must be massar-production")
    if cluster.get("domain") != "massar-academy.net":
        raise ValueError("inventory domain must be massar-academy.net")
    ipaddress.ip_network(cluster["overlay_cidr"])
    for key in ("known_hosts_file", "identity_file"):
        cluster[key] = expand_reference(str(cluster[key]), key)
        if require_operator_files:
            candidate = Path(cluster[key]).expanduser()
            if not candidate.is_file():
                raise ValueError(f"{key} does not exist: {candidate}")

    nodes_raw = raw["nodes"]
    if not isinstance(nodes_raw, list) or len(nodes_raw) != 3:
        raise ValueError("inventory must define exactly three nodes")
    nodes = tuple(
        Node(
            id=str(item["id"]),
            ssh_alias=str(item["ssh_alias"]),
            public_address=str(ipaddress.ip_address(item["public_address"])),
            overlay_address=str(ipaddress.ip_address(item["overlay_address"])),
            roles=tuple(item["roles"]),
        )
        for item in nodes_raw
    )
    if tuple(node.id for node in nodes) != NODE_IDS:
        raise ValueError("nodes must be ordered node-1, node-2, node-3")
    for attr in ("ssh_alias", "public_address", "overlay_address"):
        values = [getattr(node, attr) for node in nodes]
        if len(values) != len(set(values)):
            raise ValueError(f"duplicate node {attr}")
    network = ipaddress.ip_network(cluster["overlay_cidr"])
    if any(ipaddress.ip_address(node.overlay_address) not in network for node in nodes):
        raise ValueError("overlay node address is outside cluster.overlay_cidr")
    builder_nodes = tuple(node for node in nodes if "builder" in node.roles)
    if tuple(node.id for node in builder_nodes) != (BUILDER_NODE_ID,):
        raise ValueError("inventory must assign exactly one builder role to node-3")

    hostnames = dict(raw["hostnames"])
    expected = {
        "massar-academy.net",
        "app.massar-academy.net",
        "admin.massar-academy.net",
        "teacher.massar-academy.net",
        "staff.massar-academy.net",
        "api.massar-academy.net",
        "ws.massar-academy.net",
        "assets.massar-academy.net",
    }
    if set(hostnames) != expected:
        raise ValueError("inventory must contain the exact eight approved hostnames")
    return Inventory(path.resolve(), cluster, nodes, hostnames)


def selected_nodes(inventory: Inventory, target: str) -> tuple[Node, ...]:
    if target == "all":
        return inventory.nodes
    for node in inventory.nodes:
        if node.id == target:
            return (node,)
    raise ValueError(f"unknown node target: {target}")


def redact(value: Any) -> Any:
    if isinstance(value, dict):
        return {
            key: ("[REDACTED]" if SECRET_KEY_RE.search(str(key)) else redact(child))
            for key, child in value.items()
        }
    if isinstance(value, list):
        return [redact(child) for child in value]
    return value


def write_evidence(directory: Path, command: str, targets: tuple[Node, ...], status: str, reason: str | None) -> Path:
    directory.mkdir(parents=True, exist_ok=True)
    started = utc_now()
    payload = {
        "schemaVersion": 1,
        "operationId": str(uuid.uuid4()),
        "command": command,
        "startedAt": started,
        "completedAt": utc_now(),
        "status": status,
        "targets": [node.id for node in targets],
        "reason": reason,
    }
    path = directory / f"{started.replace(':', '').replace('-', '')}-{command}.json"
    path.write_text(json.dumps(redact(payload), ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return path


def parser() -> argparse.ArgumentParser:
    root = argparse.ArgumentParser(prog="cluster", description=__doc__)
    root.add_argument("--inventory", required=True, type=Path)
    commands = root.add_subparsers(dest="command", required=True)
    for name in (
        "audit", "bootstrap", "status", "thanaweya-log-count", "codes-log-diagnostics", "build", "migrate", "deploy", "drain",
        "failover-test", "file-failover-test", "backup", "backup-database-diff",
        "backup-database-full", "backup-database-initialize",
        "backup-files", "database-archive-config", "database-archive-status",
        "prepare-pitr-probe", "restore-test",
        "restore-files-test", "rollback",
        "bootstrap-admin", "accept", "cloudflare-status",
        "collect-current-manifest", "seal-legacy-release",
        "normalize-current-manifest",
        "legacy-prepare", "legacy-cutover", "legacy-resume", "legacy-rollback",
        "backup-repository", "backup-repository-initialize",
        "backup-repository-sync-clients",
        "backup-repository-plan", "backup-repository-status",
        "backup-schedules-activate", "backup-schedules-status",
    ):
        command = commands.add_parser(name)
        command.add_argument("--node", choices=(*NODE_IDS, "all"), default="all")
        command.add_argument("--evidence-dir", type=Path, default=Path("artifacts/production"))
        command.add_argument("--dry-run", action="store_true")
        command.add_argument("--yes", action="store_true")
        command.add_argument("--release")
        command.add_argument("--manifest", type=Path)
        command.add_argument("--backup-evidence", type=Path)
        command.add_argument("--compatibility-evidence", type=Path)
        command.add_argument("--current-manifest", type=Path)
        command.add_argument("--collector-evidence", type=Path)
        command.add_argument("--manifest-output", type=Path)
        command.add_argument("--signing-key-file", type=Path)
        command.add_argument("--output", type=Path)
        command.add_argument("--secret-dir", type=Path)
        command.add_argument("--tunnel-id")
        command.add_argument("--credentials", type=Path)
        command.add_argument("--capacity-per-node")
        command.add_argument("--candidate-db")
        command.add_argument("--bundle-manifest", type=Path)
        command.add_argument("--passphrase-file", type=Path)
        command.add_argument("--backup-gate", type=Path)
        command.add_argument("--maximum-outage-seconds", type=int, default=120)
        if name == "build":
            command.add_argument("--remote-builder", action="store_true")
    return root


READ_ONLY_COMMANDS = {
    "audit",
    "status",
    "thanaweya-log-count",
    "codes-log-diagnostics",
    "cloudflare-status",
    "collect-current-manifest",
    "backup-repository-plan",
    "backup-repository-status",
    "database-archive-status",
    "backup-schedules-status",
}


def validate_dry_run(
    args: argparse.Namespace,
    targets: tuple[Node, ...],
) -> str | None:
    if args.command in {"build", "migrate", "deploy", "rollback"} and len(targets) != 3:
        return f"{args.command} is cluster-wide and requires --node all"
    if args.command == "build" and not args.release:
        return "--release is required"
    if args.command == "drain" and len(targets) != 1:
        return "drain requires exactly one node"
    if args.command == "failover-test" and len(targets) != 3:
        return (
            "failover-test resolves the current PostgreSQL writer and Redis master "
            "dynamically and therefore requires --node all"
        )
    if args.command == "collect-current-manifest" and len(targets) != 3:
        return "collect-current-manifest requires --node all for three-node parity"
    if args.command == "normalize-current-manifest" and len(targets) != 3:
        return "normalize-current-manifest requires --node all"
    if args.command == "seal-legacy-release" and len(targets) != 3:
        return "seal-legacy-release requires --node all"
    if args.command == "file-failover-test":
        if len(targets) != 1:
            return "file-failover-test requires exactly one data-brick node"
        if not set(targets[0].roles).intersection(
            {"file-data-primary", "file-data-standby"}
        ):
            return "file-failover-test refuses the Gluster arbiter"
        if not 30 <= args.maximum_outage_seconds <= 180:
            return "--maximum-outage-seconds must be between 30 and 180"
    if args.command == "migrate" and (
        not args.release or not args.manifest or not args.backup_evidence
    ):
        return "--release, --manifest, and --backup-evidence are required"
    if args.command == "restore-test" and len(targets) != 1:
        return "restore-test requires exactly one node"
    if args.command == "prepare-pitr-probe" and len(targets) != 3:
        return "prepare-pitr-probe must target all nodes so the current writer creates the probe"
    if args.command in {"backup-files", "restore-files-test"} and len(targets) != 1:
        return f"{args.command} requires exactly one node"
    if args.command == "deploy" and (
        not args.release or not args.manifest or not args.backup_evidence
    ):
        return "--release, --manifest, and --backup-evidence are required"
    if args.command == "rollback" and (
        not args.release
        or not args.manifest
        or not args.current_manifest
        or not args.compatibility_evidence
    ):
        return (
            "--release, --manifest, --current-manifest, and "
            "--compatibility-evidence are required for rollback"
        )
    if args.command == "accept" and (
        not args.signing_key_file or not args.output
    ):
        return "--signing-key-file and --output are required"
    if args.command == "collect-current-manifest" and (
        not args.manifest_output or not args.output
    ):
        return "--manifest-output and --output are required"
    if args.command == "collect-current-manifest":
        outputs = (
            args.manifest_output.expanduser(),
            args.output.expanduser(),
        )
        if outputs[0].absolute() == outputs[1].absolute():
            return "manifest and evidence outputs must differ"
        for output in outputs:
            if ".." in output.parts:
                return "collector outputs must not contain traversal"
            if os.path.lexists(output):
                return f"collector output already exists: {output}"
            current = Path(output.absolute().anchor)
            for part in output.absolute().parts[1:-1]:
                current /= part
                if os.path.lexists(current) and (
                    current.is_symlink() or not current.is_dir()
                ):
                    return "collector output parent contains a symlink or non-directory"
    if args.command == "normalize-current-manifest" and (
        not args.manifest or not args.collector_evidence or not args.output
    ):
        return "--manifest, --collector-evidence, and --output are required"
    if args.command == "seal-legacy-release" and not args.output:
        return "--output is required"
    if args.command.startswith("legacy-"):
        if len(targets) != 3:
            return "legacy cutover commands must target all three nodes"
        if not args.candidate_db or not args.output:
            return "legacy cutover commands require --candidate-db and --output"
        if args.command == "legacy-prepare" and (
            not args.bundle_manifest or not args.passphrase_file
        ):
            return "legacy-prepare requires --bundle-manifest and --passphrase-file"
        if args.command == "legacy-cutover" and not args.backup_gate:
            return "legacy-cutover requires --backup-gate"
    if args.command == "backup-repository" and (
        not args.secret_dir or not args.capacity_per_node
    ):
        return "--secret-dir and --capacity-per-node are required"
    if args.command == "backup-repository-sync-clients" and not args.secret_dir:
        return "--secret-dir is required"
    return None


def operator_transport(inventory: Inventory) -> StrictSshTransport:
    return StrictSshTransport(
        Path(inventory.cluster["known_hosts_file"]),
        Path(inventory.cluster["identity_file"]),
    )


def target(inventory: Inventory, node: Node) -> SshTarget:
    return SshTarget(
        node.id,
        node.public_address,
        str(inventory.cluster["ssh_user"]),
    )


def execute(
    args: argparse.Namespace,
    inventory: Inventory,
    targets: tuple[Node, ...],
) -> tuple[str, str | None]:
    scripts = Path(__file__).resolve().parent
    if args.command in {"build", "migrate", "deploy", "rollback"} and len(targets) != 3:
        return "blocked", f"{args.command} is cluster-wide and requires --node all"
    transport = operator_transport(inventory)
    if args.command == "audit":
        findings: dict[str, list[str]] = {}
        for node in targets:
            payload = audit_node(transport, target(inventory, node))
            findings[node.id] = validate_clean_host(payload)
        failed = {node_id: rows for node_id, rows in findings.items() if rows}
        return ("failed", json.dumps(failed, sort_keys=True)) if failed else ("success", None)

    if args.command == "bootstrap":
        for node in targets:
            bootstrap_foundation(
                transport,
                target(inventory, node),
                dry_run=False,
            )
        return "success", None

    if args.command == "status":
        for node in targets:
            completed = transport.run(
                target(inventory, node),
                (
                    "bash", "-lc",
                    "set -e; "
                    "systemctl is-active chrony docker etcd patroni haproxy redis-server redis-sentinel glusterd; "
                    "mountpoint -q /srv/massar-shared; "
                    "sudo docker ps --filter label=com.docker.compose.project=massar_production "
                    "--filter label=com.docker.compose.service=backend --filter health=healthy -q | grep -q .; "
                    "curl --fail --silent -H 'Host: api.massar-academy.net' "
                    "http://127.0.0.1:8088/api/health/ready >/dev/null; "
                    "curl --fail --silent -H 'Host: massar-academy.net' http://127.0.0.1:8088/__node_ready >/dev/null",
                ),
                timeout_seconds=30,
                check=False,
            )
            if completed.returncode:
                return "failed", f"{node.id} health command failed"
        return "success", None

    if args.command == "thanaweya-log-count":
        counts: dict[str, int] = {}
        # Count requests only.  The command never prints, exports, or stores
        # request paths, seat numbers, IP addresses, or any other log fields.
        script = (
            "set -euo pipefail; "
            "gateway=$(sudo /usr/bin/docker ps --filter label=com.docker.compose.project=massar_production "
            "--filter label=com.docker.compose.service=gateway -q | head -n 1); "
            "test -n \"$gateway\" || { printf '0\\n'; exit 0; }; "
            "sudo /usr/bin/docker logs \"$gateway\" 2>&1 | "
            "awk '/\\\"(GET|POST) \\/thanaweya-results(\\/|\\?| )/ { count++ } "
            "/\\\"(GET|POST) \\/api\\/thanaweya-results\\/[0-9]+(\\/subjects)?(\\?| )/ { count++ } "
            "END { print count + 0 }'"
        )
        for node in targets:
            completed = transport.run(
                target(inventory, node),
                ("bash", "-lc", script),
                timeout_seconds=60,
                check=False,
            )
            if completed.returncode or not completed.stdout.strip().isdigit():
                detail = (completed.stderr or "").strip().replace("\n", " ")[:240]
                return "failed", f"{node.id} could not count thanaweya requests: {detail or 'no numeric output'}"
            counts[node.id] = int(completed.stdout.strip())
        return "success", json.dumps({"requests": sum(counts.values()), "byNode": counts}, sort_keys=True)

    if args.command == "codes-log-diagnostics":
        # A bounded, read-only incident view: only matching error lines are
        # collected. Request bodies, headers and tokens are never exported.
        script = (
            "set -euo pipefail; "
            "backend=$(sudo /usr/bin/docker ps --filter label=com.docker.compose.project=massar_production "
            "--filter label=com.docker.compose.service=backend -q | head -n 1); "
            "test -n \"$backend\" || { printf 'backend container unavailable\\n'; exit 0; }; "
            "sudo /usr/bin/docker logs --since 24h \"$backend\" 2>&1 | "
            "grep -Ei '/api/admin/codes/(groups|bulk-generate)|Unhandled exception|Npgsql|PostgresException|DbUpdateException' | "
            "sed -E 's/(access_token|authorization|bearer|token)[=: ]+[^ ,;]+/\\1=[REDACTED]/Ig' | "
            "tail -n 100 || true"
        )
        diagnostics: dict[str, list[str]] = {}
        for node in targets:
            completed = transport.run(
                target(inventory, node),
                ("bash", "-lc", script),
                timeout_seconds=60,
                check=False,
            )
            if completed.returncode:
                detail = (completed.stderr or "").strip().replace("\n", " ")[:240]
                return "failed", f"{node.id} could not collect code diagnostics: {detail or 'remote command failed'}"
            diagnostics[node.id] = [line[:500] for line in completed.stdout.splitlines() if line.strip()]
        return "success", json.dumps(diagnostics, ensure_ascii=False, sort_keys=True)

    if args.command == "build":
        if not args.release:
            return "blocked", "--release is required"
        repository = Path(__file__).resolve().parents[3]
        provenance = resolve_release(repository, args.release)
        # This is pure validation only.  The existing local build path remains
        # unchanged until the remote executor has its own reviewed rollout.
        remote_plan = create_remote_build_plan(inventory, provenance)
        if getattr(args, "remote_builder", False):
            output = args.evidence_dir / str(provenance["releaseId"])
            run_remote_builder_workflow(
                repository=repository,
                output=output,
                inventory=inventory,
                transport=transport,
                provenance=provenance,
                created_at=utc_now(),
            )
            return "success", None
        release_id = str(provenance["releaseId"])
        output = args.evidence_dir / release_id
        if output.exists() or output.is_symlink():
            assert_source_unchanged(repository, provenance)
            digests, manifest = verify_local_release_artifacts(
                output, release_id, provenance
            )
        else:
            output.parent.mkdir(parents=True, exist_ok=True)
            token = uuid.uuid4().hex
            temporary_output = output.parent / f".{release_id}.{token}.building"
            snapshot = output.parent / f".{release_id}.{token}.source"
            try:
                create_source_snapshot(
                    repository,
                    snapshot,
                    str(provenance["sourceStateSha256"]),
                )
                digests = build_release(snapshot, release_id, temporary_output)
                create_release_bundle(snapshot, temporary_output)
                assert_source_unchanged(repository, provenance)
                manifest = create_release_manifest_v2(
                    ReleaseManifestInputs(
                        repo=snapshot,
                        output=temporary_output,
                        provenance=provenance,
                        images=digests,
                        created_at=utc_now(),
                    )
                )
                write_json_atomic(temporary_output / "manifest.json", manifest)
                os.rename(temporary_output, output)
            finally:
                shutil.rmtree(snapshot, ignore_errors=True)
                shutil.rmtree(temporary_output, ignore_errors=True)
        assert_source_unchanged(repository, provenance)
        distribution = distribute_release(
            output,
            release_id,
            manifest,
            inventory.nodes,
            str(inventory.cluster["ssh_user"]),
            transport,
        )
        manifest["digestParity"] = len(distribution) == 3
        manifest["distribution"] = distribution
        write_json_atomic(output / "manifest.json", manifest)
        assert_source_unchanged(repository, provenance)
        publish_final_manifest(
            output,
            release_id,
            inventory.nodes,
            str(inventory.cluster["ssh_user"]),
            transport,
        )
        return "success", None

    if args.command == "drain":
        if len(targets) != 1:
            return "blocked", "drain requires exactly one node"
        command = [
            sys.executable, str(scripts / "manage_traffic.py"),
            "--inventory", str(args.inventory),
            "--known-hosts", inventory.cluster["known_hosts_file"],
            "--identity", inventory.cluster["identity_file"],
            "--node", targets[0].id,
            "drain", "--yes",
        ]
    elif args.command == "migrate":
        if not args.release or not args.manifest or not args.backup_evidence:
            return "blocked", "--release, --manifest, and --backup-evidence are required"
        command = [
            sys.executable, str(scripts / "migrate_release.py"),
            "--inventory", str(args.inventory),
            "--known-hosts", inventory.cluster["known_hosts_file"],
            "--identity", inventory.cluster["identity_file"],
            "--release", args.release,
            "--manifest", str(args.manifest),
            "--backup-evidence", str(args.backup_evidence),
            "--yes",
        ]
    elif args.command == "restore-test":
        if len(targets) != 1:
            return "blocked", "restore-test requires exactly one node"
        command = [
            sys.executable, str(scripts / "restore_database.py"),
            "--inventory", str(args.inventory),
            "--known-hosts", inventory.cluster["known_hosts_file"],
            "--identity", inventory.cluster["identity_file"],
            "--node", targets[0].id,
            "--yes",
        ]
    elif args.command == "failover-test":
        if len(targets) != 3:
            return (
                "blocked",
                "failover-test requires --node all because leaders are resolved dynamically",
            )
        environment = {
            **os.environ,
            "MASSAR_INVENTORY": str(args.inventory),
            "MASSAR_SSH_KEY": inventory.cluster["identity_file"],
            "MASSAR_KNOWN_HOSTS": inventory.cluster["known_hosts_file"],
            "MASSAR_SSH_USER": inventory.cluster["ssh_user"],
        }
        for drill in ("run_postgres_failover_drill.sh", "run_redis_failover_drill.sh"):
            completed = subprocess.run(
                ["bash", str(scripts / drill)],
                env=environment,
                text=True,
                capture_output=True,
                check=False,
            )
            if completed.returncode:
                return "failed", f"{drill} failed"
        return "success", None
    elif args.command == "file-failover-test":
        if len(targets) != 1:
            return "blocked", "file-failover-test requires exactly one data-brick node"
        selected_roles = set(targets[0].roles)
        if not selected_roles.intersection(
            {"file-data-primary", "file-data-standby"}
        ):
            return "blocked", "file-failover-test refuses the Gluster arbiter"
        if not 30 <= args.maximum_outage_seconds <= 180:
            return "blocked", "--maximum-outage-seconds must be between 30 and 180"
        command = [
            sys.executable,
            str(scripts / "run_file_failover_drill.py"),
            "--inventory",
            str(args.inventory),
            "--known-hosts",
            inventory.cluster["known_hosts_file"],
            "--identity",
            inventory.cluster["identity_file"],
            "--target-node",
            targets[0].id,
            "--evidence-output",
            str(
                args.evidence_dir
                / f"file-failover-{targets[0].id}-{uuid.uuid4().hex}.json"
            ),
            "--maximum-outage-seconds",
            str(args.maximum_outage_seconds),
            "--yes",
        ]
    elif args.command == "backup":
        node = inventory.nodes[0]
        completed = transport.run(
            target(inventory, node),
            (
                "sudo", "systemctl", "start",
                "massar-pgbackrest-diff.service",
                "massar-files-backup.service",
            ),
            timeout_seconds=900,
            check=False,
        )
        return (
            ("success", None)
            if completed.returncode == 0
            else ("failed", "database or file backup service failed")
        )
    elif args.command == "backup-database-initialize":
        failed: list[str] = []
        diagnostics: list[str] = []
        for node in inventory.nodes:
            completed = transport.run(
                target(inventory, node),
                (
                    "sudo",
                    "/usr/bin/systemctl",
                    "start",
                    "massar-pgbackrest-init.service",
                ),
                timeout_seconds=300,
                check=False,
            )
            if completed.returncode:
                failed.append(node.id)
                journal = transport.run(
                    target(inventory, node),
                    (
                        "bash",
                        "-lc",
                        "sudo journalctl -u massar-pgbackrest-init.service "
                        "--no-pager --output=cat -n 20 | "
                        "sed -E 's/((key|secret|password)[^ =]*=)[^ ]+/\\1[REDACTED]/Ig'",
                    ),
                    timeout_seconds=30,
                    check=False,
                )
                diagnostics.append(f"{node.id}: {journal.stdout.strip()}")
        return (
            ("success", None)
            if not failed
            else (
                "failed",
                (
                    f"pgBackRest stanza initialization failed on: {','.join(failed)}; "
                    + " | ".join(diagnostics)
                )[:2000],
            )
        )
    elif args.command in {"backup-database-full", "backup-database-diff"}:
        unit = (
            "massar-pgbackrest-full.service"
            if args.command == "backup-database-full"
            else "massar-pgbackrest-diff.service"
        )
        failed: list[str] = []
        for node in inventory.nodes:
            completed = transport.run(
                target(inventory, node),
                ("sudo", "/usr/bin/systemctl", "start", unit),
                timeout_seconds=1800,
                check=False,
            )
            if completed.returncode:
                failed.append(node.id)
        return (
            ("success", None)
            if not failed
            else ("failed", f"{unit} failed on: {','.join(failed)}")
        )
    elif args.command == "prepare-pitr-probe":
        failed: list[str] = []
        for node in targets:
            completed = transport.run(
                target(inventory, node),
                (
                    "sudo",
                    "/usr/bin/systemctl",
                    "start",
                    "massar-pitr-probe.service",
                ),
                timeout_seconds=600,
                check=False,
            )
            if completed.returncode:
                failed.append(node.id)
        return (
            ("success", None)
            if not failed
            else ("failed", f"PITR probe failed on: {','.join(failed)}")
        )
    elif args.command in {"database-archive-config", "database-archive-status"}:
        command = [
            sys.executable,
            str(scripts / "configure_database_archiving.py"),
            "--inventory",
            str(args.inventory),
            "--known-hosts",
            inventory.cluster["known_hosts_file"],
            "--identity",
            inventory.cluster["identity_file"],
            "apply" if args.command == "database-archive-config" else "status",
        ]
        if args.command == "database-archive-config":
            command.append("--yes")
    elif args.command in {"backup-files", "restore-files-test"}:
        if len(targets) != 1:
            return "blocked", f"{args.command} requires exactly one node"
        unit = (
            "massar-files-backup.service"
            if args.command == "backup-files"
            else "massar-files-restore-test.service"
        )
        completed = transport.run(
            target(inventory, targets[0]),
            ("sudo", "/usr/bin/systemctl", "start", unit),
            timeout_seconds=900,
            check=False,
        )
        return (
            ("success", None)
            if completed.returncode == 0
            else ("failed", f"{unit} failed")
        )
    elif args.command in {
        "backup-repository",
        "backup-repository-initialize",
        "backup-repository-sync-clients",
        "backup-repository-plan",
        "backup-repository-status",
        "backup-schedules-activate",
        "backup-schedules-status",
    }:
        command = [
            sys.executable,
            str(scripts / "manage_backup_bucket.py"),
            "--inventory",
            str(args.inventory),
            "--known-hosts",
            inventory.cluster["known_hosts_file"],
            "--identity",
            inventory.cluster["identity_file"],
        ]
        if args.command == "backup-repository":
            if not args.secret_dir or not args.capacity_per_node:
                return "blocked", "--secret-dir and --capacity-per-node are required"
            command.extend(
                [
                    "--secret-dir",
                    str(args.secret_dir),
                    "--capacity-per-node",
                    args.capacity_per_node,
                    "bootstrap",
                    "--yes",
                ]
            )
        elif args.command == "backup-repository-initialize":
            command.append("initialize")
        elif args.command == "backup-repository-sync-clients":
            if not args.secret_dir:
                return "blocked", "--secret-dir is required"
            command.extend(
                [
                    "--secret-dir",
                    str(args.secret_dir),
                    "--node",
                    args.node,
                    "sync-clients",
                    "--yes",
                ]
            )
        elif args.command == "backup-repository-plan":
            command.append("plan")
        elif args.command == "backup-schedules-activate":
            command.extend(("activate-schedules", "--yes"))
        elif args.command == "backup-schedules-status":
            command.append("schedule-status")
        else:
            command.append("status")
    elif args.command in {"deploy", "rollback"}:
        required = (
            (args.release, args.manifest, args.backup_evidence)
            if args.command == "deploy"
            else (
                args.release,
                args.manifest,
                args.current_manifest,
                args.compatibility_evidence,
            )
        )
        if not all(required):
            return "blocked", f"{args.command} required evidence is incomplete"
        script = "deploy_release.py" if args.command == "deploy" else "rollback_release.py"
        command = [
            sys.executable, str(scripts / script),
            "--inventory", str(args.inventory),
            "--known-hosts", inventory.cluster["known_hosts_file"],
            "--identity", inventory.cluster["identity_file"],
            "--release", args.release,
            "--manifest", str(args.manifest),
            "--yes",
        ]
        if args.command == "deploy":
            command.extend(["--backup-evidence", str(args.backup_evidence)])
        if args.command == "rollback":
            command.extend(
                [
                    "--current-manifest",
                    str(args.current_manifest),
                    "--compatibility-evidence",
                    str(args.compatibility_evidence),
                ]
            )
    elif args.command == "cloudflare-status":
        command = [
            sys.executable, str(scripts / "manage_cloudflare.py"),
            "--inventory", str(args.inventory),
            "--known-hosts", inventory.cluster["known_hosts_file"],
            "--identity", inventory.cluster["identity_file"],
            "status",
        ]
    elif args.command == "collect-current-manifest":
        if len(targets) != 3:
            return (
                "blocked",
                "collect-current-manifest requires --node all for three-node parity",
            )
        if not args.manifest_output or not args.output:
            return "blocked", "--manifest-output and --output are required"
        command = [
            sys.executable,
            str(scripts / "collect_current_release_manifest.py"),
            "--inventory",
            str(args.inventory),
            "--known-hosts",
            inventory.cluster["known_hosts_file"],
            "--identity",
            inventory.cluster["identity_file"],
            "--manifest-output",
            str(args.manifest_output),
            "--evidence-output",
            str(args.output),
        ]
    elif args.command == "normalize-current-manifest":
        if len(targets) != 3:
            return "blocked", "normalize-current-manifest requires --node all"
        if not args.manifest or not args.collector_evidence or not args.output:
            return (
                "blocked",
                "--manifest, --collector-evidence, and --output are required",
            )
        command = [
            sys.executable,
            str(scripts / "normalize_current_release_pointer.py"),
            "--inventory",
            str(args.inventory),
            "--known-hosts",
            inventory.cluster["known_hosts_file"],
            "--identity",
            inventory.cluster["identity_file"],
            "--manifest",
            str(args.manifest),
            "--collector-evidence",
            str(args.collector_evidence),
            "--evidence-output",
            str(args.output),
            "--yes",
        ]
    elif args.command == "seal-legacy-release":
        if len(targets) != 3 or not args.output:
            return "blocked", "seal-legacy-release requires --node all and --output"
        command = [
            sys.executable,
            str(scripts / "seal_legacy_release.py"),
            "--inventory", str(args.inventory),
            "--known-hosts", inventory.cluster["known_hosts_file"],
            "--identity", inventory.cluster["identity_file"],
            "--evidence-output", str(args.output),
            "--yes",
        ]
    elif args.command == "accept":
        required = (args.signing_key_file, args.output)
        if not all(required):
            return "blocked", "--signing-key-file and --output are required"
        command = [
            sys.executable, str(scripts / "accept_production.py"),
            "--evidence-root", str(args.evidence_dir),
            "--signing-key-file", str(args.signing_key_file),
            "--output", str(args.output),
        ]
    elif args.command == "bootstrap-admin":
        command = [
            sys.executable,
            str(scripts / "bootstrap_admin.py"),
        ]
    elif args.command.startswith("legacy-"):
        if not args.candidate_db or not args.output:
            return "blocked", "legacy cutover commands require --candidate-db and --output"
        action = args.command.removeprefix("legacy-")
        command = [
            sys.executable,
            str(scripts / "manage_legacy_cutover.py"),
            "--inventory", str(args.inventory),
            "--known-hosts", inventory.cluster["known_hosts_file"],
            "--identity", inventory.cluster["identity_file"],
            "--candidate-db", args.candidate_db,
            "--evidence-output", str(args.output),
            action,
            "--yes",
        ]
        if args.command == "legacy-prepare":
            if not args.bundle_manifest or not args.passphrase_file:
                return "blocked", "legacy-prepare requires --bundle-manifest and --passphrase-file"
            command[10:10] = [
                "--bundle-manifest", str(args.bundle_manifest),
                "--passphrase-file", str(args.passphrase_file),
            ]
        if args.command == "legacy-cutover":
            if not args.backup_gate:
                return "blocked", "legacy-cutover requires --backup-gate"
            command[10:10] = ["--backup-gate", str(args.backup_gate)]
    else:
        return "blocked", f"{args.command} requires its dedicated reviewed runbook"

    completed = subprocess.run(command, text=True, capture_output=True, check=False)
    if completed.returncode:
        return "failed", (completed.stderr.strip() or f"{args.command} failed")[:500]
    return "success", None


def main(argv: list[str] | None = None) -> int:
    args = parser().parse_args(argv)
    try:
        inventory = load_inventory(
            args.inventory,
            require_operator_files=not args.dry_run,
        )
        targets = selected_nodes(inventory, args.node)
    except ValueError as exc:
        print(f"preflight blocked: {exc}", file=sys.stderr)
        return EXIT_PREFLIGHT

    if args.command not in READ_ONLY_COMMANDS and not args.dry_run and not args.yes:
        print("state-changing commands require --yes or --dry-run", file=sys.stderr)
        return EXIT_SAFETY
    reason = validate_dry_run(args, targets) if args.dry_run else None
    status = "blocked" if reason else ("dry-run" if args.dry_run else "success")
    if not args.dry_run:
        try:
            status, reason = execute(args, inventory, targets)
        except (OSError, ValueError, RuntimeError, subprocess.SubprocessError) as exc:
            status, reason = "failed", str(exc)[:500]
    evidence = write_evidence(args.evidence_dir, args.command, targets, status, reason)
    print(json.dumps({
        "command": args.command,
        "status": status,
        "targets": [node.id for node in targets],
        "evidence": str(evidence),
    }, ensure_ascii=False))
    return EXIT_OK if status in {"success", "dry-run"} else EXIT_PREFLIGHT


if __name__ == "__main__":
    raise SystemExit(main())
