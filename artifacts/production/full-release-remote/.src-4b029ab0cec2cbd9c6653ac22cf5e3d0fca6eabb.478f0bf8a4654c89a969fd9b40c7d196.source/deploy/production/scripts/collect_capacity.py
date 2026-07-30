#!/usr/bin/env python3
"""Collect a secret-free, read-only capacity snapshot from all three nodes."""

from __future__ import annotations

import argparse
import datetime as dt
import json
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path
from typing import Any

from clusterctl import load_inventory
from ssh_transport import SshTarget, StrictSshTransport


REMOTE_COLLECTOR = r"""
set -euo pipefail
test "$(cat /etc/massar/cluster-id)" = "massar-production"
python3 - <<'PY'
import json, os, pathlib, re, socket, subprocess, time

def command(argv, input_text=None):
    result = subprocess.run(
        argv, input=input_text, text=True, capture_output=True, timeout=15, check=False
    )
    return result.returncode, result.stdout.strip()

def cpu_sample():
    values = pathlib.Path("/proc/stat").read_text().splitlines()[0].split()[1:]
    numbers = [int(value) for value in values]
    return {
        "total": sum(numbers),
        "idle": numbers[3] + numbers[4],
        "iowait": numbers[4],
        "steal": numbers[7] if len(numbers) > 7 else 0,
    }

before = cpu_sample()
time.sleep(1)
after = cpu_sample()
delta = max(1, after["total"] - before["total"])
memory = {}
for line in pathlib.Path("/proc/meminfo").read_text().splitlines():
    key, value = line.split(":", 1)
    memory[key] = int(value.strip().split()[0]) * 1024

containers = []
code, docker_rows = command([
    "sudo", "docker", "stats", "--no-stream",
    "--format", "{{json .}}",
])
if code == 0:
    for line in docker_rows.splitlines():
        try:
            row = json.loads(line)
        except json.JSONDecodeError:
            continue
        containers.append({
            "name": row.get("Name"),
            "cpuPercent": row.get("CPUPerc"),
            "memory": row.get("MemUsage"),
            "memoryPercent": row.get("MemPerc"),
            "networkIo": row.get("NetIO"),
            "blockIo": row.get("BlockIO"),
            "pids": row.get("PIDs"),
        })

code, docker_ps = command([
    "sudo", "docker", "ps", "--format",
    '{{json .}}',
])
releases = []
if code == 0:
    for line in docker_ps.splitlines():
        try:
            row = json.loads(line)
        except json.JSONDecodeError:
            continue
        inspect_code, labels = command([
            "sudo", "docker", "inspect", row.get("ID", ""),
            "--format", '{{json .Config.Labels}}',
        ])
        parsed = json.loads(labels) if inspect_code == 0 and labels.startswith("{") else {}
        if parsed.get("net.massar.release"):
            releases.append({
                "name": row.get("Names"),
                "releaseId": parsed.get("net.massar.release"),
                "nodeId": parsed.get("net.massar.node"),
                "status": row.get("Status"),
            })

postgres = {}
postgres_script = r'''
export PGPASSWORD="$(cat /run/secrets/pgapp)"
psql -h 127.0.0.1 -p 6432 -U massar_app -d massar_platform -XAt -v ON_ERROR_STOP=1 -c "
select json_build_object(
  'connections', (select count(*) from pg_stat_activity),
  'activeConnections', (select count(*) from pg_stat_activity where state='active'),
  'waitingLocks', (select count(*) from pg_stat_activity where wait_event_type='Lock'),
  'maxConnections', current_setting('max_connections')::int,
  'databaseBytes', pg_database_size(current_database()),
  'walBytes', pg_wal_lsn_diff(pg_current_wal_lsn(), '0/0'),
  'replicationLagBytes', coalesce((select max(pg_wal_lsn_diff(pg_current_wal_lsn(), replay_lsn)) from pg_stat_replication), 0)
);"
'''
code, value = command([
    "sudo", "docker", "run", "--rm", "--network", "host",
    "-v", "/etc/massar/secrets/postgres-app-password:/run/secrets/pgapp:ro",
    "postgres:16-alpine",
    "sh", "-ec", postgres_script,
])
if code == 0 and value.startswith("{"):
    postgres = json.loads(value)

redis = {}
queues = {}
redis_script = r'''
export REDISCLI_AUTH="$REDIS_PASSWORD"
redis-cli --no-auth-warning -h 127.0.0.1 -p 6379 INFO \
  memory clients stats replication persistence |
  grep -E '^(used_memory:|maxmemory:|connected_clients:|blocked_clients:|instantaneous_ops_per_sec:|total_commands_processed:|role:|connected_slaves:|master_repl_offset:|master_link_status:|slave_repl_offset:|aof_delayed_fsync:|loading:)'
'''
code, value = command([
    "sudo", "docker", "run", "--rm", "--network", "host",
    "--env-file", "/etc/massar/app.env", "redis:7-alpine",
    "sh", "-ec", redis_script,
])
if code == 0:
    for line in value.splitlines():
        key, separator, raw = line.partition(":")
        if separator:
            redis[key] = raw.strip()

queue_script = r'''
export REDISCLI_AUTH="$REDIS_PASSWORD"
redis-cli --no-auth-warning --raw -h 127.0.0.1 -p 6379 EVAL \
  "local out={} for _,q in ipairs(ARGV) do local p='bull:'..q..':' table.insert(out,q) table.insert(out,redis.call('LLEN',p..'wait')) table.insert(out,redis.call('LLEN',p..'active')) table.insert(out,redis.call('ZCARD',p..'delayed')) table.insert(out,redis.call('ZCARD',p..'failed')) table.insert(out,redis.call('SCARD',p..'stalled')) end return out" \
  0 notifications ai-video-chapters ai-essay-grading generate-chapter-mindmaps ai-live-support-turns
'''
code, value = command([
    "sudo", "docker", "run", "--rm", "--network", "host",
    "--env-file", "/etc/massar/app.env", "redis:7-alpine",
    "sh", "-ec", queue_script,
])
if code == 0:
    rows = value.splitlines()
    for offset in range(0, len(rows), 6):
        if offset + 5 >= len(rows):
            queues = {}
            break
        try:
            queues[rows[offset]] = {
                "waiting": int(rows[offset + 1]),
                "active": int(rows[offset + 2]),
                "delayed": int(rows[offset + 3]),
                "failed": int(rows[offset + 4]),
                "stalled": int(rows[offset + 5]),
            }
        except ValueError:
            queues = {}
            break

code, patroni_raw = command(["curl", "--fail", "--silent", "http://127.0.0.1:8008/patroni"])
patroni = json.loads(patroni_raw) if code == 0 and patroni_raw.startswith("{") else {}

disk = os.statvfs("/")
payload = {
    "hostname": socket.gethostname(),
    "cpu": {
        "count": os.cpu_count(),
        "busyPercent": round((delta - (after["idle"] - before["idle"])) * 100 / delta, 2),
        "iowaitPercent": round((after["iowait"] - before["iowait"]) * 100 / delta, 2),
        "stealPercent": round((after["steal"] - before["steal"]) * 100 / delta, 2),
        "loadAverage": list(os.getloadavg()),
    },
    "memory": {
        "totalBytes": memory.get("MemTotal", 0),
        "availableBytes": memory.get("MemAvailable", 0),
        "swapTotalBytes": memory.get("SwapTotal", 0),
        "swapFreeBytes": memory.get("SwapFree", 0),
    },
    "rootDisk": {
        "totalBytes": disk.f_blocks * disk.f_frsize,
        "freeBytes": disk.f_bavail * disk.f_frsize,
    },
    "containers": containers,
    "releases": releases,
    "postgres": postgres,
    "redis": redis,
    "queues": queues,
    "patroni": {
        "role": patroni.get("role"),
        "state": patroni.get("state"),
        "timeline": patroni.get("timeline"),
        "replication": patroni.get("replication", []),
    },
}
print(json.dumps(payload, separators=(",", ":")))
PY
"""


def collect_node(
    transport: StrictSshTransport,
    ssh_user: str,
    node: object,
) -> dict[str, Any]:
    target = SshTarget(
        str(getattr(node, "id")),
        str(getattr(node, "public_address")),
        ssh_user,
    )
    completed = transport.run(
        target,
        ("bash", "-lc", REMOTE_COLLECTOR),
        timeout_seconds=120,
    )
    payload = json.loads(completed.stdout)
    payload["nodeId"] = target.node_id
    return payload


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--inventory", required=True, type=Path)
    parser.add_argument("--known-hosts", required=True, type=Path)
    parser.add_argument("--identity", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()

    inventory = load_inventory(args.inventory)
    transport = StrictSshTransport(args.known_hosts, args.identity)
    with ThreadPoolExecutor(max_workers=3) as pool:
        nodes = list(pool.map(
            lambda node: collect_node(
                transport,
                str(inventory.cluster["ssh_user"]),
                node,
            ),
            inventory.nodes,
        ))
    payload = {
        "schemaVersion": 1,
        "capturedAt": dt.datetime.now(dt.timezone.utc).isoformat().replace("+00:00", "Z"),
        "status": "success",
        "nodes": nodes,
    }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    temporary = args.output.with_suffix(".tmp")
    temporary.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
    temporary.chmod(0o640)
    temporary.replace(args.output)
    print(json.dumps({
        "status": "success",
        "nodeCount": len(nodes),
        "output": str(args.output),
    }))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
