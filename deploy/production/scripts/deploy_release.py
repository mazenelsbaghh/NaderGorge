#!/usr/bin/env python3
"""Fail-closed rolling application deployment in node-3/node-2/node-1 order."""

from __future__ import annotations

import argparse
import datetime as dt
import json
import re
import uuid
import subprocess
import sys
from pathlib import Path
from typing import Callable, NamedTuple

from clusterctl import load_inventory
from release_contract import (
    RollbackCompatibilityGate,
    ReleaseContractError,
    load_migration_safety_gate,
    load_release_manifest,
    load_rollback_compatibility_gate,
    read_exact_json,
)
from ssh_transport import SshTarget, StrictSshTransport


ROLLING_ORDER = ("node-3", "node-2", "node-1")
DIGEST = re.compile(r"^sha256:[0-9a-f]{64}$")
RELEASE = re.compile(r"^(?:git-[0-9a-f]{7,40}|src-[0-9a-f]{40}|prod-[0-9]{8}-[a-z0-9-]+)$")


class DeployError(RuntimeError):
    pass


class RetainedSchema(NamedTuple):
    database_system_identifier: str
    migration_ids_sha256: str
    schema_sha256: str


class CleanupEvidenceContract(NamedTuple):
    current_release: str
    rollback_release: str
    status: str


def retained_schema_from_gate(gate: object) -> RetainedSchema:
    migration_ids = getattr(
        gate,
        "post_migration_ids_sha256",
        getattr(gate, "migration_ids_sha256", None),
    )
    schema = getattr(
        gate,
        "post_migration_schema_sha256",
        getattr(gate, "schema_sha256", None),
    )
    database = getattr(gate, "database_system_identifier", None)
    if not all(isinstance(value, str) for value in (database, migration_ids, schema)):
        raise DeployError("retained schema evidence is incomplete")
    return RetainedSchema(database, migration_ids, schema)


def reverse_rollback_order(
    advanced_nodes: list[str] | tuple[str, ...],
    failed_node_id: str,
    failed_node_has_marker: bool,
) -> tuple[str, ...]:
    ordered = (
        ((failed_node_id,) if failed_node_has_marker else ())
        + tuple(reversed(advanced_nodes))
    )
    if len(ordered) != len(set(ordered)) or any(
        node_id not in ROLLING_ORDER for node_id in ordered
    ):
        raise DeployError("automatic rollback node set is invalid")
    return ordered


def node_recovery_error(node_id: str, release_id: str) -> DeployError:
    marker = f"/var/lib/massar/deploy-recovery/{release_id}-{node_id}.json"
    return DeployError(
        f"rollout stopped with {node_id} drained; recovery marker: {marker}"
    )


def safe_failure_marker(error: BaseException, prefix: str) -> str:
    match = re.search(
        rf"MASSAR_{prefix}_FAILURE stage=[a-z0-9-]+ line=[0-9]+ status=[0-9]+",
        str(error),
    )
    return match.group(0) if match else f"MASSAR_{prefix}_FAILURE stage=unknown"


def traffic(
    root: Path,
    inventory: Path,
    known_hosts: Path,
    identity: Path,
    node_id: str,
    action: str,
) -> dict[str, str] | None:
    result = subprocess.run(
        [
            sys.executable,
            str(root / "deploy/production/scripts/manage_traffic.py"),
            "--inventory", str(inventory),
            "--known-hosts", str(known_hosts),
            "--identity", str(identity),
            "--node", node_id,
            action,
            "--yes",
        ],
        check=False,
        text=True,
        capture_output=True,
    )
    if result.returncode:
        raise DeployError(result.stderr.strip() or f"{action} failed for {node_id}")
    if action == "status":
        try:
            value = json.loads(result.stdout)
        except json.JSONDecodeError as exc:
            raise DeployError(f"invalid traffic status for {node_id}") from exc
        if (
            not isinstance(value, dict)
            or set(value) != {"node-1", "node-2", "node-3"}
            or any(not isinstance(status, str) for status in value.values())
        ):
            raise DeployError(f"incomplete traffic status for {node_id}")
        return {str(key): str(status) for key, status in value.items()}
    return None


def node_ready(
    transport: StrictSshTransport,
    target: SshTarget,
    overlay_address: str,
) -> str:
    output = transport.run(
        target,
        (
            "bash", "-lc",
            "set -euo pipefail; "
            f"curl --fail --silent -H 'Host: massar-academy.net' "
            f"http://{overlay_address}:8080/__node_ready",
        ),
        timeout_seconds=15,
    ).stdout
    try:
        value = json.loads(output)
    except json.JSONDecodeError as exc:
        raise DeployError(f"{target.node_id} returned invalid readiness JSON") from exc
    release_id = value.get("releaseId") if isinstance(value, dict) else None
    if (
        not isinstance(value, dict)
        or value.get("status") != "healthy"
        or value.get("nodeId") != target.node_id
        or not isinstance(release_id, str)
        or not RELEASE.fullmatch(release_id)
    ):
        raise DeployError(f"{target.node_id} is not directly ready")
    return release_id


def verify_rollback_prestate(
    *,
    inventory: object,
    transport: StrictSshTransport,
    current_manifest: object,
    gate: RollbackCompatibilityGate,
) -> None:
    """Bind rollback to the exact running release and live database state."""
    if (
        getattr(current_manifest, "release_id", None) != gate.current_release_id
        or getattr(current_manifest, "sha256", None)
        != gate.current_manifest_sha256
    ):
        raise DeployError("rollback current manifest does not match the validated gate")
    nodes = tuple(getattr(inventory, "nodes"))
    cluster = getattr(inventory, "cluster")
    for node in nodes:
        target = SshTarget(node.id, node.public_address, cluster["ssh_user"])
        script = f"""
set -euo pipefail
test "$(cat /etc/massar/cluster-id)" = "massar-production"
test -L /opt/massar/current
test "$(sha256sum /opt/massar/current/manifest.json | awk '{{print $1}}')" = "{gate.current_manifest_sha256}"
test "$(python3 -c 'import json; print(json.load(open("/opt/massar/current/manifest.json"))["releaseId"])')" = "{gate.current_release_id}"
curl --fail --silent -H 'Host: massar-academy.net' \
  http://{node.overlay_address}:8080/__node_ready |
  python3 -c 'import json,sys; value=json.load(sys.stdin); raise SystemExit(0 if value.get("status")=="healthy" and value.get("releaseId")=="{gate.current_release_id}" and value.get("nodeId")=="{node.id}" else 1)'
"""
        transport.run(
            target,
            ("bash", "-lc", script),
            timeout_seconds=30,
        )

    control = nodes[0]
    target = SshTarget(control.id, control.public_address, cluster["ssh_user"])
    script = f"""
set -euo pipefail
readarray -t state < <(
  sudo docker run --rm --network host \
    -v /etc/massar/secrets/postgres-superuser-password:/run/secrets/pgsuper:ro \
    postgres:16-alpine sh -ec '
      set -euo pipefail
      export PGPASSWORD="$(cat /run/secrets/pgsuper)"
      psql -h 127.0.0.1 -p 6432 -U postgres -d postgres -XAt \
        -v ON_ERROR_STOP=1 -c "select system_identifier from pg_control_system();"
      psql -h 127.0.0.1 -p 6432 -U postgres -d massar_platform -XAt \
        -v ON_ERROR_STOP=1 \
        -c '"'"'select "MigrationId" from "__EFMigrationsHistory" order by "MigrationId";'"'"' |
        sha256sum | awk '"'"'{{print $1}}'"'"'
      pg_dump -h 127.0.0.1 -p 6432 -U postgres -d massar_platform \
        --schema-only --no-owner --no-privileges --quote-all-identifiers |
        sed -E "/^-- (Dumped from|Dumped by|Started on|Completed on)/d; /^.(un)?restrict[[:space:]]/d" |
        sha256sum | awk '"'"'{{print $1}}'"'"'
    '
)
test "${{#state[@]}}" -eq 3
test "${{state[0]}}" = "{gate.database_system_identifier}"
test "${{state[1]}}" = "{gate.migration_ids_sha256}"
test "${{state[2]}}" = "{gate.schema_sha256}"
"""
    transport.run(
        target,
        ("bash", "-lc", script),
        timeout_seconds=120,
    )


def assert_rollout_quorum(
    *,
    root: Path,
    inventory_path: Path,
    known_hosts: Path,
    identity: Path,
    inventory: object,
    transport: StrictSshTransport,
    rollout_node: str,
    require_drained: bool,
    allow_target_drained: bool = False,
    traffic_reader: Callable[..., dict[str, str] | None] = traffic,
    readiness_reader: Callable[[StrictSshTransport, SshTarget, str], str] = node_ready,
) -> dict[str, dict[str, str]]:
    nodes = tuple(getattr(inventory, "nodes"))
    states: dict[str, dict[str, str]] = {}
    for node in nodes:
        value = traffic_reader(
            root, inventory_path, known_hosts, identity, node.id, "status"
        )
        if value is None:
            raise DeployError(f"missing ingress state for {node.id}")
        states[node.id] = value
    for backend_node, ingress_states in states.items():
        if backend_node == rollout_node and require_drained:
            expected = {"DRAIN"}
        elif backend_node == rollout_node and allow_target_drained:
            observed = {
                "DRAIN" if status.startswith("DRAIN") else "UP"
                if status.startswith("UP") else status
                for status in ingress_states.values()
            }
            expected = observed if len(observed) == 1 and observed <= {"UP", "DRAIN"} else set()
        else:
            expected = {"UP"}
        if (
            len(expected) != 1
            or not all(
                any(status.startswith(prefix) for prefix in expected)
                for status in ingress_states.values()
            )
        ):
            description = "UP or consistently DRAIN" if allow_target_drained else next(
                iter(expected), "the required state"
            )
            raise DeployError(
                f"rollout quorum failed: {backend_node} is not {description} on every ingress"
            )
    for node in nodes:
        if node.id == rollout_node:
            continue
        readiness_reader(
            transport,
            SshTarget(
                node.id,
                node.public_address,
                getattr(inventory, "cluster")["ssh_user"],
            ),
            node.overlay_address,
        )
    return states


def reconcile_inconsistent_ingress_traffic(
    *,
    root: Path,
    inventory_path: Path,
    known_hosts: Path,
    identity: Path,
    inventory: object,
    traffic_reader: Callable[..., dict[str, str] | None] = traffic,
    traffic_writer: Callable[..., dict[str, str] | None] = traffic,
) -> tuple[str, ...]:
    """Repair only split HAProxy runtime state left by an interrupted rollout.

    A deliberate maintenance drain is reported as DRAIN by every ingress and
    must remain untouched.  A mix of UP and DRAIN for the same backend is not
    a valid steady state and otherwise prevents the next rollout from reaching
    its own recovery path before it has drained a node.
    """
    repaired: list[str] = []
    for node in getattr(inventory, "nodes"):
        states = traffic_reader(
            root, inventory_path, known_hosts, identity, node.id, "status"
        )
        if states is None:
            raise DeployError(f"missing ingress state for {node.id}")
        normalized = {
            "DRAIN" if status.startswith("DRAIN") else "UP"
            if status.startswith("UP") else status
            for status in states.values()
        }
        if normalized == {"UP", "DRAIN"}:
            traffic_writer(
                root, inventory_path, known_hosts, identity, node.id, "undrain"
            )
            repaired.append(node.id)
    return tuple(repaired)


class RolloutLock:
    """Fail closed when another operator owns the global release rollout."""

    path = "/var/lib/massar/rollout-locks/release-rollout.lock"

    def __init__(
        self,
        transport: StrictSshTransport,
        target: SshTarget,
        operation_id: str,
    ) -> None:
        self.transport = transport
        self.target = target
        self.operation_id = operation_id
        self.acquired = False

    def acquire(self) -> None:
        script = f"""
set -euo pipefail
test "$(cat /etc/massar/cluster-id)" = "massar-production"
test -d /var/lib/massar/rollout-locks
test "$(stat -c '%U:%G:%a' /var/lib/massar/rollout-locks)" = "massar-ops:massar:700"
if mkdir {self.path} 2>/dev/null; then
  printf '%s\n' '{self.operation_id}' > {self.path}/owner
elif test "$(cat {self.path}/owner 2>/dev/null || true)" != "{self.operation_id}"; then
  exit 75
fi
"""
        result = self.transport.run(
            self.target,
            ("bash", "-lc", script),
            timeout_seconds=30,
            check=False,
        )
        if result.returncode:
            raise DeployError("another rollout owns the global Production lock")
        self.acquired = True

    def release(self) -> None:
        if not self.acquired:
            return
        script = f"""
set -euo pipefail
test "$(cat {self.path}/owner)" = "{self.operation_id}"
rm -f {self.path}/owner
rmdir {self.path}
"""
        result = self.transport.run(
            self.target,
            ("bash", "-lc", script),
            timeout_seconds=30,
            check=False,
        )
        if result.returncode:
            raise DeployError(
                f"rollout completed but lock recovery is required: {self.operation_id}"
            )
        self.acquired = False


def matching_recovery_marker(
    transport: StrictSshTransport,
    target: SshTarget,
    release_id: str,
) -> bool:
    marker = f"/var/lib/massar/deploy-recovery/{release_id}-{target.node_id}.json"
    script = f"""
set -euo pipefail
python3 - {marker} '{release_id}' '{target.node_id}' <<'PY'
import json,pathlib,sys
path=pathlib.Path(sys.argv[1])
if not path.is_file() or path.is_symlink():
    raise SystemExit(1)
value=json.loads(path.read_text())
if (value.get("schemaVersion") != 1
        or value.get("status") != "recovery-required"
        or value.get("releaseId") != sys.argv[2]
        or value.get("nodeId") != sys.argv[3]):
    raise SystemExit(1)
PY
"""
    result = transport.run(
        target,
        ("bash", "-lc", script),
        timeout_seconds=30,
        check=False,
    )
    return result.returncode == 0


def all_nodes_running_release(
    inventory: object,
    transport: StrictSshTransport,
    release_id: str,
) -> bool:
    cluster = getattr(inventory, "cluster")
    for node in getattr(inventory, "nodes"):
        target = SshTarget(node.id, node.public_address, cluster["ssh_user"])
        if node_ready(transport, target, node.overlay_address) != release_id:
            return False
    return True


def recover_node(
    transport: StrictSshTransport,
    target: SshTarget,
    node: object,
    failed_release: str,
    retained_schema: RetainedSchema | None = None,
) -> str:
    marker = (
        f"/var/lib/massar/deploy-recovery/{failed_release}-{target.node_id}.json"
    )
    overlay = getattr(node, "overlay_address")
    retained_schema_verification = ""
    if retained_schema is not None:
        retained_schema_verification = f"""
stage="verify-prior-app-against-retained-schema"
readarray -t retained_state < <(
  sudo docker run --rm --network host \
    -v /etc/massar/secrets/postgres-superuser-password:/run/secrets/pgsuper:ro \
    postgres:16-alpine sh -ec '
      set -euo pipefail
      export PGPASSWORD="$(cat /run/secrets/pgsuper)"
      psql -h 127.0.0.1 -p 6432 -U postgres -d postgres -XAt \
        -v ON_ERROR_STOP=1 -c "select system_identifier from pg_control_system();"
      psql -h 127.0.0.1 -p 6432 -U postgres -d massar_platform -XAt \
        -v ON_ERROR_STOP=1 \
        -c '"'"'select "MigrationId" from "__EFMigrationsHistory" order by "MigrationId";'"'"' |
        sha256sum | awk '"'"'{{print $1}}'"'"'
      pg_dump -h 127.0.0.1 -p 6432 -U postgres -d massar_platform \
        --schema-only --no-owner --no-privileges --quote-all-identifiers |
        sed -E "/^-- (Dumped from|Dumped by|Started on|Completed on)/d; /^.(un)?restrict[[:space:]]/d" |
        sha256sum | awk '"'"'{{print $1}}'"'"'
    '
)
test "${{#retained_state[@]}}" -eq 3
test "${{retained_state[0]}}" = "{retained_schema.database_system_identifier}"
test "${{retained_state[1]}}" = "{retained_schema.migration_ids_sha256}"
test "${{retained_state[2]}}" = "{retained_schema.schema_sha256}"
"""
    script = f"""
set -Eeuo pipefail
stage="verify-recovery-marker"
runtime_env=""
report_exit() {{
  status="$1"
  test -z "$runtime_env" || rm -f "$runtime_env"
  if test "$status" -ne 0; then
    printf 'MASSAR_RECOVERY_FAILURE stage=%s line=%s status=%s\n' \
      "$stage" "$2" "$status" >&2
  fi
}}
fail_stage() {{
  exit "$1"
}}
trap 'report_exit "$?" "$LINENO"' EXIT
test "$(cat /etc/massar/cluster-id)" = "massar-production"
mapfile -t recovery < <(python3 - {marker} '{failed_release}' '{target.node_id}' <<'PY'
import hashlib,json,pathlib,re,sys
path=pathlib.Path(sys.argv[1]); value=json.loads(path.read_text())
release=re.compile(r"^(?:git-[0-9a-f]{{7,40}}|src-[0-9a-f]{{40}}|prod-[0-9]{{8}}-[a-z0-9-]+)$")
previous=value.get("previousReleaseRoot")
operation=value.get("operationId")
release_root=pathlib.Path("/opt/massar/releases").resolve()
previous_path=pathlib.Path(previous).resolve() if isinstance(previous,str) else None
if (value.get("schemaVersion") != 1 or value.get("status") != "recovery-required"
        or value.get("releaseId") != sys.argv[2] or value.get("nodeId") != sys.argv[3]
        or previous_path is None or previous_path.parent != release_root
        or not isinstance(operation,str)
        or not re.fullmatch(r"[0-9a-f]{{8}}-[0-9a-f]{{4}}-4[0-9a-f]{{3}}-[89ab][0-9a-f]{{3}}-[0-9a-f]{{12}}",operation)):
    raise SystemExit("invalid recovery marker")
previous=str(previous_path)
manifest_bytes=(previous_path/"manifest.json").read_bytes()
manifest=json.loads(manifest_bytes)
release_id=manifest.get("releaseId"); images=manifest.get("images",{{}})
if not isinstance(release_id,str) or not release.fullmatch(release_id):
    raise SystemExit("invalid previous release")
for name in ("backend","frontend","worker"):
    digest=images.get(name)
    if not isinstance(digest,str) or not re.fullmatch(r"sha256:[0-9a-f]{{64}}",digest):
        raise SystemExit("invalid previous image")
print(previous); print(release_id)
print(hashlib.sha256(manifest_bytes).hexdigest())
print(images["backend"]); print(images["frontend"]); print(images["worker"])
print(operation.replace("-",""))
PY
)
test "${{#recovery[@]}}" -eq 7
previous="${{recovery[0]}}"; previous_release="${{recovery[1]}}"
previous_manifest_sha256="${{recovery[2]}}"
stage="verify-previous-images"
test "$(sudo docker image inspect "massar/backend:$previous_release" --format '{{{{.Id}}}}')" = "${{recovery[3]}}"
test "$(sudo docker image inspect "massar/frontend:$previous_release" --format '{{{{.Id}}}}')" = "${{recovery[4]}}"
test "$(sudo docker image inspect "massar/worker:$previous_release" --format '{{{{.Id}}}}')" = "${{recovery[5]}}"
shared_gid="$(getent group massar | cut -d: -f3)"
case "$shared_gid" in ''|*[!0-9]*) fail_stage 32 "$LINENO" ;; esac
services="backend worker landing student admin teacher staff gateway"
stage="write-previous-runtime-environment"
runtime_env="/tmp/massar-runtime-recovery-${{recovery[6]}}-{target.node_id}.env"
(umask 077; printf '%s\n' \
  "MASSAR_NODE_ID={target.node_id}" \
  "MASSAR_OVERLAY_IP={overlay}" \
  "MASSAR_RELEASE_ID=$previous_release" \
  "MASSAR_RELEASE_ROOT=$previous" \
  "MASSAR_BACKEND_IMAGE=massar/backend:$previous_release" \
  "MASSAR_FRONTEND_IMAGE=massar/frontend:$previous_release" \
  "MASSAR_WORKER_IMAGE=massar/worker:$previous_release" \
  "MASSAR_SHARED_GID=$shared_gid" >"$runtime_env")
compose() {{
  sudo docker compose --env-file /etc/massar/app.env --env-file "$runtime_env" \
    -f "$previous/deploy/production/compose/compose.base.yml" \
    -f "$previous/deploy/production/compose/compose.app.yml" "$@"
}}
stage="restore-previous-compose"
compose rm --stop --force release-evidence >/dev/null 2>&1 || true
compose up -d --no-build --force-recreate --remove-orphans $services
stage="wait-previous-health"
for attempt in $(seq 1 60); do
  healthy=1
  for service in backend worker landing student admin teacher staff gateway; do
    container_id="$(compose ps -q "$service")"
    test -n "$container_id" || healthy=0
    if test -n "$container_id"; then
      test "$(sudo docker inspect --format '{{{{.State.Status}}}}' "$container_id")" = running || healthy=0
      test "$(sudo docker inspect --format '{{{{.State.Health.Status}}}}' "$container_id")" = healthy || healthy=0
    fi
  done
  test "$healthy" = 1 && break
  test "$attempt" -lt 60 || fail_stage 23 "$LINENO"
  sleep 2
done
stage="verify-previous-readiness"
ready="$(curl --fail --silent -H 'Host: massar-academy.net' http://{overlay}:8080/__node_ready)"
printf '%s' "$ready" | grep -Fq "\\"releaseId\\":\\"$previous_release\\""
{retained_schema_verification}
stage="restore-current-pointer"
pointer_result="$(
  sudo /usr/local/sbin/massar-normalize-current-release switch \
    "$previous_release" "$previous_manifest_sha256" "${{recovery[6]}}"
)"
printf '%s' "$pointer_result" | python3 -c '
import json,sys
value=json.load(sys.stdin)
raise SystemExit(0 if value.get("status") in ("switched","already-current") else 1)
'
stage="remove-recovery-marker"
rm -f {marker}
rm -f "$runtime_env"
runtime_env=""
stage="success"
printf '%s\n' "$previous_release"
"""
    return transport.run(
        target,
        ("bash", "-lc", script),
        timeout_seconds=300,
    ).stdout.strip()


def recover_failed_node(
    *,
    transport: StrictSshTransport,
    target: SshTarget,
    node: object,
    release_id: str,
    root: Path,
    inventory_path: Path,
    known_hosts: Path,
    identity: Path,
    inventory: object,
    retained_schema: RetainedSchema | None = None,
    traffic_writer: Callable[..., dict[str, str] | None] = traffic,
    quorum_checker: Callable[..., dict[str, dict[str, str]]] = assert_rollout_quorum,
) -> str:
    if retained_schema is None:
        previous_release = recover_node(transport, target, node, release_id)
    else:
        previous_release = recover_node(
            transport, target, node, release_id, retained_schema
        )
    traffic_writer(
        root, inventory_path, known_hosts, identity, target.node_id, "undrain"
    )
    quorum_checker(
        root=root,
        inventory_path=inventory_path,
        known_hosts=known_hosts,
        identity=identity,
        inventory=inventory,
        transport=transport,
        rollout_node=target.node_id,
        require_drained=False,
    )
    return previous_release


def rollback_nodes_in_reverse(
    *,
    node_ids: tuple[str, ...],
    failed_node_id: str | None,
    release_id: str,
    retained_schema: RetainedSchema,
    root: Path,
    inventory_path: Path,
    known_hosts: Path,
    identity: Path,
    inventory: object,
    transport: StrictSshTransport,
    traffic_writer: Callable[..., dict[str, str] | None] = traffic,
    quorum_checker: Callable[..., dict[str, dict[str, str]]] = assert_rollout_quorum,
    recovery: Callable[..., str] = recover_failed_node,
) -> dict[str, str]:
    """Restore every advanced app node in exact reverse advancement order."""
    by_id = {node.id: node for node in getattr(inventory, "nodes")}
    restored: dict[str, str] = {}
    for node_id in node_ids:
        node = by_id[node_id]
        target = SshTarget(
            node.id,
            node.public_address,
            getattr(inventory, "cluster")["ssh_user"],
        )
        if node_id != failed_node_id:
            traffic_writer(
                root, inventory_path, known_hosts, identity, node_id, "drain"
            )
        quorum_checker(
            root=root,
            inventory_path=inventory_path,
            known_hosts=known_hosts,
            identity=identity,
            inventory=inventory,
            transport=transport,
            rollout_node=node_id,
            require_drained=True,
        )
        restored[node_id] = recovery(
            transport=transport,
            target=target,
            node=node,
            release_id=release_id,
            retained_schema=retained_schema,
            root=root,
            inventory_path=inventory_path,
            known_hosts=known_hosts,
            identity=identity,
            inventory=inventory,
            traffic_writer=traffic_writer,
            quorum_checker=quorum_checker,
        )
    return restored


def clear_recovery_marker(
    transport: StrictSshTransport,
    target: SshTarget,
    release_id: str,
) -> None:
    marker = f"/var/lib/massar/deploy-recovery/{release_id}-{target.node_id}.json"
    script = f"""
set -euo pipefail
python3 - {marker} '{release_id}' '{target.node_id}' <<'PY'
import json,pathlib,sys
path=pathlib.Path(sys.argv[1])
if not path.exists() and not path.is_symlink():
    raise SystemExit(0)
if path.is_symlink():
    raise SystemExit("invalid recovery marker")
value=json.loads(path.read_text())
if (value.get("schemaVersion") != 1
        or value.get("status") != "recovery-required"
        or value.get("releaseId") != sys.argv[2]
        or value.get("nodeId") != sys.argv[3]):
    raise SystemExit("invalid recovery marker")
path.unlink()
PY
"""
    transport.run(target, ("bash", "-lc", script), timeout_seconds=30)


def parse_release_cleanup_evidence(
    stdout: str,
    target: SshTarget,
    contract: CleanupEvidenceContract,
) -> dict[str, object]:
    try:
        evidence = json.loads(stdout)
    except json.JSONDecodeError as exc:
        raise DeployError(f"invalid release cleanup evidence from {target.node_id}") from exc
    if (
        not isinstance(evidence, dict)
        or evidence.get("status") != contract.status
        or evidence.get("nodeId") != target.node_id
        or evidence.get("currentReleaseId") != contract.current_release
        or evidence.get("rollbackReleaseId") != contract.rollback_release
    ):
        raise DeployError(f"release cleanup evidence mismatch on {target.node_id}")
    return evidence


def preview_release_artifact_cleanup(
    transport: StrictSshTransport,
    target: SshTarget,
    current_release: str,
) -> dict[str, object]:
    completed = transport.run(
        target,
        (
            "sudo",
            "/usr/local/sbin/massar-install-immutable-release",
            "prune-release-artifacts",
            current_release,
            current_release,
            target.node_id,
            "--dry-run",
        ),
        timeout_seconds=120,
    )
    return parse_release_cleanup_evidence(
        completed.stdout,
        target,
        CleanupEvidenceContract(current_release, current_release, "dry-run"),
    )


def prune_release_artifacts(
    transport: StrictSshTransport,
    target: SshTarget,
    current_release: str,
    rollback_release: str,
) -> dict[str, object]:
    completed = transport.run(
        target,
        (
            "sudo",
            "/usr/local/sbin/massar-install-immutable-release",
            "prune-release-artifacts",
            current_release,
            rollback_release,
            target.node_id,
            "--yes",
        ),
        timeout_seconds=900,
    )
    return parse_release_cleanup_evidence(
        completed.stdout,
        target,
        CleanupEvidenceContract(current_release, rollback_release, "pruned"),
    )


def deploy_node(
    transport: StrictSshTransport,
    target: SshTarget,
    node: object,
    release_id: str,
    images: dict[str, str],
    manifest_sha256: str,
    operation_id: str,
) -> str:
    release_root = f"/opt/massar/releases/{release_id}"
    services = "backend worker landing student admin teacher staff gateway"
    recovery_marker = (
        f"/var/lib/massar/deploy-recovery/{release_id}-{target.node_id}.json"
    )
    pointer_operation = operation_id.replace("-", "")
    script = f"""
set -Eeuo pipefail
stage="verify-release"
runtime_env=""
report_exit() {{
  status="$1"
  test -z "$runtime_env" || rm -f "$runtime_env"
  if test "$status" -ne 0; then
    printf 'MASSAR_DEPLOY_FAILURE stage=%s line=%s status=%s\n' \
      "$stage" "$2" "$status" >&2
  fi
}}
fail_stage() {{
  exit "$1"
}}
trap 'report_exit "$?" "$LINENO"' EXIT
test "$(cat /etc/massar/cluster-id)" = "massar-production"
test -d {release_root}
test -f {release_root}/deploy/production/compose/compose.base.yml
test -f {release_root}/deploy/production/compose/compose.app.yml
test "$(sha256sum {release_root}/manifest.json | awk '{{print $1}}')" = "{manifest_sha256}"
test "$(sudo docker image inspect massar/backend:{release_id} --format '{{{{.Id}}}}')" = "{images['backend']}"
test "$(sudo docker image inspect massar/frontend:{release_id} --format '{{{{.Id}}}}')" = "{images['frontend']}"
test "$(sudo docker image inspect massar/worker:{release_id} --format '{{{{.Id}}}}')" = "{images['worker']}"
stage="verify-shared-storage"
shared_gid="$(getent group massar | cut -d: -f3)"
case "$shared_gid" in ''|*[!0-9]*) fail_stage 32 "$LINENO" ;; esac
test "$(stat -c %g /srv/massar-shared/public)" = "$shared_gid"
test "$((8#$(stat -c %a /srv/massar-shared/public) & 8#020))" -ne 0
current=/opt/massar/current
previous=""
if test -L "$current"; then
  previous="$(readlink -f "$current")"
elif test -d "$current"; then
  test -z "$(find "$current" -mindepth 1 -maxdepth 1 -print -quit)"
elif test -e "$current"; then
  fail_stage 31 "$LINENO"
fi
stage="write-recovery-marker"
sudo install -d -m 0700 -o massar-ops -g massar /var/lib/massar/deploy-recovery
test ! -e {recovery_marker}
printf '{{"schemaVersion":1,"status":"recovery-required","operationId":"%s","nodeId":"%s","releaseId":"%s","previousReleaseRoot":"%s"}}\n' \
  '{operation_id}' '{target.node_id}' '{release_id}' "$previous" |
  (umask 077; set -o noclobber; cat > {recovery_marker})
stage="write-runtime-environment"
runtime_env="/tmp/massar-runtime-{pointer_operation}-{target.node_id}.env"
(umask 077; printf '%s\n' \
  "MASSAR_NODE_ID={target.node_id}" \
  "MASSAR_OVERLAY_IP={getattr(node, "overlay_address")}" \
  "MASSAR_RELEASE_ID={release_id}" \
  "MASSAR_RELEASE_ROOT={release_root}" \
  "MASSAR_BACKEND_IMAGE=massar/backend:{release_id}" \
  "MASSAR_FRONTEND_IMAGE=massar/frontend:{release_id}" \
  "MASSAR_WORKER_IMAGE=massar/worker:{release_id}" \
  "MASSAR_SHARED_GID=$shared_gid" >"$runtime_env")
compose() {{
  sudo docker compose --env-file /etc/massar/app.env --env-file "$runtime_env" \
    -f {release_root}/deploy/production/compose/compose.base.yml \
    -f {release_root}/deploy/production/compose/compose.app.yml "$@"
}}
stage="validate-compose"
compose config -q
stage="start-compose"
compose rm --stop --force release-evidence >/dev/null 2>&1 || true
compose up -d --no-build --force-recreate --remove-orphans {services}
stage="wait-container-health"
for attempt in $(seq 1 60); do
  healthy=1
  for service in {services}; do
    container_id="$(compose ps -q "$service")"
    test -n "$container_id" || healthy=0
    if test -n "$container_id"; then
      test "$(sudo docker inspect --format '{{{{.State.Status}}}}' "$container_id")" = running || healthy=0
      test "$(sudo docker inspect --format '{{{{.State.Health.Status}}}}' "$container_id")" = healthy || healthy=0
    fi
  done
  test "$healthy" = 1 && break
  test "$attempt" -lt 60 || fail_stage 23 "$LINENO"
  sleep 2
done
stage="verify-worker-readiness"
worker_id="$(compose ps -q worker)"
sudo docker exec "$worker_id" curl --fail --silent http://127.0.0.1:3001/ready >/dev/null
stage="verify-shared-write"
worker_groups="$(
  sudo docker exec --user 10001:10001 "$worker_id" id -G
)"
printf '%s\n' "$worker_groups" | tr " " "\\n" | grep -qx "$shared_gid"
probe="/shared/public/.massar-worker-write-{operation_id}"
if ! sudo docker exec --user 10001:10001 \
  "$worker_id" sh -ec \
  'printf ready > "$1"; test "$(cat "$1")" = ready' sh "$probe"; then
  sudo docker exec "$worker_id" rm -f "$probe" >/dev/null 2>&1 || true
  fail_stage 25 "$LINENO"
fi
sudo docker exec "$worker_id" rm -f "$probe"
stage="verify-surfaces"
for probe in \
  massar-academy.net/ \
  app.massar-academy.net/ \
  admin.massar-academy.net/ \
  teacher.massar-academy.net/ \
  staff.massar-academy.net/ \
  api.massar-academy.net/api/health/ready \
  ws.massar-academy.net/api/health/ready; do
  host="${{probe%%/*}}"
  path="/${{probe#*/}}"
  headers="$(curl --fail --silent --show-error --dump-header - --output /dev/null \
    -H "Host: $host" "http://{getattr(node, "overlay_address")}:8080$path")"
  printf '%s\n' "$headers" | tr -d '\\r' |
    grep -Fiqx "X-Massar-Release: {release_id}" ||
    fail_stage 24 "$LINENO"
done
ready="$(curl --fail --silent -H 'Host: massar-academy.net' \
  http://{getattr(node, "overlay_address")}:8080/__node_ready)"
printf '%s' "$ready" | grep -Fq '"releaseId":"{release_id}"'
stage="switch-current-pointer"
pointer_result="$(
  sudo /usr/local/sbin/massar-normalize-current-release switch \
    "{release_id}" "{manifest_sha256}" "{pointer_operation}"
)"
printf '%s' "$pointer_result" | python3 -c '
import json,sys
value=json.load(sys.stdin)
raise SystemExit(0 if value.get("status") in ("switched","already-current") else 1)
'
test "$(readlink -f "$current")" = "{release_root}"
stage="collect-compose-evidence"
compose_state="$(compose ps --format json)"
rm -f "$runtime_env"
runtime_env=""
stage="success"
printf '%s\n' "$compose_state"
"""
    return transport.run(
        target,
        ("bash", "-lc", script),
        timeout_seconds=300,
    ).stdout


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--inventory", required=True, type=Path)
    parser.add_argument("--known-hosts", required=True, type=Path)
    parser.add_argument("--identity", required=True, type=Path)
    parser.add_argument("--release", required=True)
    parser.add_argument("--manifest", required=True, type=Path)
    parser.add_argument("--backup-evidence", type=Path)
    parser.add_argument("--rollback-current-manifest", type=Path)
    parser.add_argument("--rollback-evidence", type=Path)
    parser.add_argument("--dry-run", action="store_true")
    parser.add_argument("--yes", action="store_true")
    parser.add_argument("--force-reconfigure", action="store_true")
    args = parser.parse_args()
    if not RELEASE.fullmatch(args.release):
        raise DeployError("invalid immutable release ID")
    inventory = load_inventory(args.inventory)
    manifest = load_release_manifest(args.manifest, args.release)
    rollback_requested = (
        args.rollback_current_manifest is not None
        or args.rollback_evidence is not None
    )
    rollback_gate: RollbackCompatibilityGate | None = None
    migration_gate = None
    current_manifest = None
    if rollback_requested:
        if (
            args.rollback_current_manifest is None
            or args.rollback_evidence is None
            or args.backup_evidence is not None
        ):
            raise DeployError(
                "rollback deploy requires --rollback-current-manifest and "
                "--rollback-evidence, without --backup-evidence"
            )
        _, current_value = read_exact_json(
            args.rollback_current_manifest,
            "rollback current release manifest",
        )
        current_release = current_value.get("releaseId")
        if not isinstance(current_release, str):
            raise DeployError("rollback current release identity is missing")
        current_manifest = load_release_manifest(
            args.rollback_current_manifest,
            current_release,
        )
        rollback_gate = load_rollback_compatibility_gate(
            args.rollback_evidence,
            current_manifest=current_manifest,
            target_manifest=manifest,
            now=dt.datetime.now(dt.timezone.utc),
        )
    else:
        if args.backup_evidence is None:
            raise DeployError("deployment requires --backup-evidence")
        migration_gate = load_migration_safety_gate(
            args.backup_evidence,
            manifest=manifest,
            now=dt.datetime.now(dt.timezone.utc),
        )
    previous_release = (
        rollback_gate.current_release_id
        if rollback_gate is not None
        else migration_gate.current_release_id
    )
    images = manifest.images
    if args.dry_run:
        print(json.dumps({"release": args.release, "order": ROLLING_ORDER, "status": "dry-run"}))
        return 0
    if not args.yes:
        raise DeployError("deployment requires --yes or --dry-run")

    root = Path(__file__).resolve().parents[3]
    transport = StrictSshTransport(args.known_hosts, args.identity)
    if rollback_gate is not None:
        assert current_manifest is not None
        verify_rollback_prestate(
            inventory=inventory,
            transport=transport,
            current_manifest=current_manifest,
            gate=rollback_gate,
        )
    by_id = {node.id: node for node in inventory.nodes}
    for node in inventory.nodes:
        target = SshTarget(
            node.id,
            node.public_address,
            inventory.cluster["ssh_user"],
        )
        cleanup_current_release = previous_release
        if matching_recovery_marker(transport, target, args.release):
            if node_ready(transport, target, node.overlay_address) == args.release:
                cleanup_current_release = args.release
        preview_release_artifact_cleanup(
            transport,
            target,
            cleanup_current_release,
        )
    operation_id = str(uuid.uuid4())
    control = inventory.nodes[0]
    lock = RolloutLock(
        transport,
        SshTarget(
            control.id,
            control.public_address,
            inventory.cluster["ssh_user"],
        ),
        operation_id,
    )
    evidence: dict[str, str] = {}
    cleanup_evidence: dict[str, dict[str, object]] = {}
    advanced_nodes: list[str] = []
    retained_schema = retained_schema_from_gate(
        rollback_gate if rollback_gate is not None else migration_gate
    )
    lock.acquire()
    reconcile_inconsistent_ingress_traffic(
        root=root,
        inventory_path=args.inventory,
        known_hosts=args.known_hosts,
        identity=args.identity,
        inventory=inventory,
    )
    rollout_error: Exception | None = None
    rollback_attempted = False
    rollout_complete = False
    unadvanced_drained_node: str | None = None
    try:
        completed_retry = not args.force_reconfigure and all_nodes_running_release(
            inventory, transport, args.release
        )
        if completed_retry:
            assert_rollout_quorum(
                root=root,
                inventory_path=args.inventory,
                known_hosts=args.known_hosts,
                identity=args.identity,
                inventory=inventory,
                transport=transport,
                rollout_node=ROLLING_ORDER[0],
                require_drained=False,
            )
        else:
            for node_id in ROLLING_ORDER:
                node = by_id[node_id]
                target = SshTarget(
                    node.id,
                    node.public_address,
                    inventory.cluster["ssh_user"],
                )
                resumable = matching_recovery_marker(
                    transport, target, args.release
                )
                assert_rollout_quorum(
                    root=root,
                    inventory_path=args.inventory,
                    known_hosts=args.known_hosts,
                    identity=args.identity,
                    inventory=inventory,
                    transport=transport,
                    rollout_node=node_id,
                    require_drained=False,
                    allow_target_drained=resumable,
                )
                # A previous attempt may have completed this node, then failed
                # before the cluster-wide post-update gate.  Its recovery marker
                # must remain until every node advances, but deploying it again
                # would correctly refuse to overwrite that marker.  Treat a
                # healthy node already serving this exact release as resumed and
                # continue the rolling sequence from the next node.
                if resumable and node_ready(
                    transport, target, node.overlay_address
                ) == args.release:
                    advanced_nodes.append(node_id)
                    traffic(
                        root, args.inventory, args.known_hosts,
                        args.identity, node_id, "undrain",
                    )
                    assert_rollout_quorum(
                        root=root,
                        inventory_path=args.inventory,
                        known_hosts=args.known_hosts,
                        identity=args.identity,
                        inventory=inventory,
                        transport=transport,
                        rollout_node=node_id,
                        require_drained=False,
                    )
                    continue
                traffic(
                    root, args.inventory, args.known_hosts,
                    args.identity, node_id, "drain",
                )
                unadvanced_drained_node = node_id
                assert_rollout_quorum(
                    root=root,
                    inventory_path=args.inventory,
                    known_hosts=args.known_hosts,
                    identity=args.identity,
                    inventory=inventory,
                    transport=transport,
                    rollout_node=node_id,
                    require_drained=True,
                )
                try:
                    evidence[node_id] = deploy_node(
                        transport, target, node, args.release, images,
                        manifest.sha256, operation_id,
                    )
                    advanced_nodes.append(node_id)
                    unadvanced_drained_node = None
                except Exception as exc:
                    failed_has_marker = matching_recovery_marker(
                        transport, target, args.release
                    )
                    if failed_has_marker:
                        unadvanced_drained_node = None
                    rollback_order = reverse_rollback_order(
                        advanced_nodes,
                        node_id,
                        failed_has_marker,
                    )
                    try:
                        rollback_attempted = True
                        restored = rollback_nodes_in_reverse(
                            node_ids=rollback_order,
                            failed_node_id=node_id if failed_has_marker else None,
                            release_id=args.release,
                            retained_schema=retained_schema,
                            root=root,
                            inventory_path=args.inventory,
                            known_hosts=args.known_hosts,
                            identity=args.identity,
                            inventory=inventory,
                            transport=transport,
                        )
                    except Exception as recovery_exc:
                        deploy_marker = safe_failure_marker(exc, "DEPLOY")
                        recovery_marker = safe_failure_marker(recovery_exc, "RECOVERY")
                        raise DeployError(
                            f"{node_recovery_error(node_id, args.release)}; "
                            f"advanced={','.join(advanced_nodes) or 'none'}; "
                            f"rollback-order={','.join(rollback_order) or 'none'}; "
                            f"{deploy_marker}; {recovery_marker}"
                        ) from recovery_exc
                    deploy_marker = safe_failure_marker(exc, "DEPLOY")
                    advanced_nodes.clear()
                    raise DeployError(
                        f"rollout failed on {node_id}; application rollback order "
                        f"{','.join(rollback_order) or 'none'} restored "
                        f"{','.join(restored) or 'none'} against retained schema; "
                        f"{deploy_marker}"
                    ) from exc
                traffic(
                    root, args.inventory, args.known_hosts,
                    args.identity, node_id, "undrain",
                )
                assert_rollout_quorum(
                    root=root,
                    inventory_path=args.inventory,
                    known_hosts=args.known_hosts,
                    identity=args.identity,
                    inventory=inventory,
                    transport=transport,
                    rollout_node=node_id,
                    require_drained=False,
                )
        rollout_complete = True
        for node_id in ROLLING_ORDER:
            node = by_id[node_id]
            clear_recovery_marker(
                transport,
                SshTarget(
                    node.id,
                    node.public_address,
                    inventory.cluster["ssh_user"],
                ),
                args.release,
            )
        for node_id in ROLLING_ORDER:
            node = by_id[node_id]
            cleanup_evidence[node_id] = prune_release_artifacts(
                transport,
                SshTarget(
                    node.id,
                    node.public_address,
                    inventory.cluster["ssh_user"],
                ),
                args.release,
                previous_release,
            )
    except Exception as exc:
        rollout_error = exc
        if advanced_nodes and not rollback_attempted and not rollout_complete:
            rollback_attempted = True
            rollback_order = tuple(reversed(advanced_nodes))
            try:
                rollback_nodes_in_reverse(
                    node_ids=rollback_order,
                    failed_node_id=None,
                    release_id=args.release,
                    retained_schema=retained_schema,
                    root=root,
                    inventory_path=args.inventory,
                    known_hosts=args.known_hosts,
                    identity=args.identity,
                    inventory=inventory,
                    transport=transport,
                )
                rollout_error = DeployError(
                    f"post-update gate failed; application rollback order "
                    f"{','.join(rollback_order)} completed against retained schema; "
                    f"{safe_failure_marker(exc, 'DEPLOY')}"
                )
                advanced_nodes.clear()
            except Exception as recovery_exc:
                rollout_error = DeployError(
                    f"post-update gate failed and reverse application rollback "
                    f"requires recovery; order={','.join(rollback_order)}; "
                    f"{safe_failure_marker(exc, 'DEPLOY')}; "
                    f"{safe_failure_marker(recovery_exc, 'RECOVERY')}"
                )
        if unadvanced_drained_node is not None:
            try:
                traffic(
                    root, args.inventory, args.known_hosts,
                    args.identity, unadvanced_drained_node, "undrain",
                )
                assert_rollout_quorum(
                    root=root,
                    inventory_path=args.inventory,
                    known_hosts=args.known_hosts,
                    identity=args.identity,
                    inventory=inventory,
                    transport=transport,
                    rollout_node=unadvanced_drained_node,
                    require_drained=False,
                )
                unadvanced_drained_node = None
            except Exception as recovery_exc:
                rollout_error = DeployError(
                    f"unchanged node {unadvanced_drained_node} could not be "
                    f"returned to service; "
                    f"{safe_failure_marker(exc, 'DEPLOY')}; "
                    f"{safe_failure_marker(recovery_exc, 'RECOVERY')}"
                )
    try:
        lock.release()
    except Exception as exc:
        if rollout_error is None:
            rollout_error = exc
    if rollout_error is not None:
        raise rollout_error
    print(
        json.dumps(
            {
                "release": args.release,
                "order": ROLLING_ORDER,
                "cleanup": cleanup_evidence,
                "status": "success",
            }
        )
    )
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (
        DeployError, ReleaseContractError, OSError, ValueError,
        json.JSONDecodeError,
    ) as exc:
        print(f"deployment blocked: {exc}", file=sys.stderr)
        raise SystemExit(6)
