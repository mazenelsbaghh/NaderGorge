#!/usr/bin/env python3
"""Emit one secret-free local node health snapshot for central retention."""

from __future__ import annotations

import datetime as dt
import json
import os
import shutil
import subprocess
import sys
import time
from pathlib import Path
from typing import Any, Mapping


SERVICES = (
    "chrony",
    "docker",
    "etcd",
    "patroni",
    "haproxy",
    "redis-server",
    "redis-sentinel",
    "glusterd",
    "cloudflared",
)

QUEUE_NAMES = (
    "notifications",
    "ai-video-chapters",
    "ai-essay-grading",
    "generate-chapter-mindmaps",
    "ai-live-support-turns",
)


def command(
    argv: list[str],
    timeout: int = 10,
    extra_env: Mapping[str, str] | None = None,
) -> tuple[int, str]:
    completed = subprocess.run(
        argv,
        text=True,
        capture_output=True,
        timeout=timeout,
        check=False,
        env={**os.environ, "LC_ALL": "C", **(extra_env or {})},
    )
    return completed.returncode, completed.stdout.strip()


def file_age_seconds(path: Path) -> int | None:
    if not path.is_file():
        return None
    return max(0, int(dt.datetime.now().timestamp() - path.stat().st_mtime))


def parse_cpu_line(value: str) -> tuple[int, int, int]:
    fields = value.split()
    if not fields or fields[0] != "cpu" or len(fields) < 6:
        raise ValueError("invalid aggregate /proc/stat CPU line")
    counters = [int(field) for field in fields[1:]]
    total = sum(counters)
    idle = counters[3] + (counters[4] if len(counters) > 4 else 0)
    iowait = counters[4] if len(counters) > 4 else 0
    return total, total - idle, iowait


def cpu_sample(
    first: str,
    second: str,
) -> dict[str, float | int]:
    first_total, first_busy, first_iowait = parse_cpu_line(first)
    second_total, second_busy, second_iowait = parse_cpu_line(second)
    total_delta = max(1, second_total - first_total)
    busy_delta = max(0, second_busy - first_busy)
    iowait_delta = max(0, second_iowait - first_iowait)
    return {
        "logicalCpuCount": os.cpu_count() or 0,
        "utilizationPercent": round(busy_delta * 100 / total_delta, 2),
        "iowaitPercent": round(iowait_delta * 100 / total_delta, 2),
    }


def read_cpu_line(path: Path = Path("/proc/stat")) -> str:
    return path.read_text(encoding="utf-8").splitlines()[0]


def collect_cpu(sample_seconds: float = 0.2) -> dict[str, float | int]:
    first = read_cpu_line()
    time.sleep(sample_seconds)
    return cpu_sample(first, read_cpu_line())


def parse_meminfo(value: str) -> dict[str, float | int]:
    fields: dict[str, int] = {}
    for line in value.splitlines():
        if ":" not in line:
            continue
        name, raw = line.split(":", 1)
        token = raw.strip().split()[0] if raw.strip() else "0"
        fields[name] = int(token) * 1024
    total = fields.get("MemTotal", 0)
    available = fields.get("MemAvailable", fields.get("MemFree", 0))
    swap_total = fields.get("SwapTotal", 0)
    swap_free = fields.get("SwapFree", 0)
    return {
        "totalBytes": total,
        "availableBytes": available,
        "usedPercent": round((total - available) * 100 / total, 2) if total else 0,
        "swapUsedBytes": max(0, swap_total - swap_free),
    }


def parse_docker_stats(value: str) -> list[dict[str, Any]]:
    snapshots: list[dict[str, Any]] = []
    for line in value.splitlines():
        try:
            row = json.loads(line)
        except json.JSONDecodeError:
            continue
        if isinstance(row, dict):
            snapshots.append(row)
    return snapshots


def parse_redis_info(value: str) -> dict[str, int | float | str]:
    wanted = {
        "role",
        "connected_clients",
        "blocked_clients",
        "used_memory",
        "used_memory_rss",
        "maxmemory",
        "mem_fragmentation_ratio",
        "instantaneous_ops_per_sec",
        "evicted_keys",
        "keyspace_hits",
        "keyspace_misses",
        "master_repl_offset",
        "slave_repl_offset",
        "second_repl_offset",
        "repl_backlog_histlen",
        "connected_slaves",
        "master_link_status",
        "aof_delayed_fsync",
        "latest_fork_usec",
        "loading",
    }
    result: dict[str, int | float | str] = {}
    for line in value.splitlines():
        if not line or line.startswith("#") or ":" not in line:
            continue
        name, raw = line.split(":", 1)
        if name not in wanted:
            continue
        cleaned = raw.strip()
        try:
            result[name] = int(cleaned)
        except ValueError:
            try:
                result[name] = float(cleaned)
            except ValueError:
                result[name] = cleaned
    return result


def parse_bullmq_counts(value: str) -> dict[str, dict[str, int]]:
    lines = value.splitlines()
    stride = 6
    queues: dict[str, dict[str, int]] = {}
    if len(lines) % stride:
        return queues
    for offset in range(0, len(lines), stride):
        name = lines[offset]
        try:
            queues[name] = {
                "waiting": int(lines[offset + 1]),
                "active": int(lines[offset + 2]),
                "delayed": int(lines[offset + 3]),
                "failed": int(lines[offset + 4]),
                "stalled": int(lines[offset + 5]),
            }
        except ValueError:
            return {}
    return queues


def collect_postgres() -> dict[str, Any]:
    sql = """
SELECT json_build_object(
  'role', CASE WHEN pg_is_in_recovery() THEN 'replica' ELSE 'primary' END,
  'totalConnections', (SELECT count(*) FROM pg_stat_activity),
  'activeConnections', (SELECT count(*) FROM pg_stat_activity WHERE state = 'active'),
  'waitingLocks', (SELECT count(*) FROM pg_stat_activity WHERE wait_event_type = 'Lock'),
  'maxConnections', current_setting('max_connections')::int,
  'replicationClientCount', (SELECT count(*) FROM pg_stat_replication),
  'maxReplicaLagBytes', CASE
    WHEN pg_is_in_recovery() THEN NULL
    ELSE COALESCE((
      SELECT max(pg_wal_lsn_diff(pg_current_wal_lsn(), replay_lsn))::bigint
      FROM pg_stat_replication
    ), 0)
  END
);
""".strip()
    return_code, output = command(
        ["runuser", "-u", "postgres", "--", "psql", "-XAt", "-d", "massar_platform", "-c", sql],
        timeout=5,
    )
    if return_code or not output.startswith("{"):
        return {}
    try:
        parsed = json.loads(output)
    except json.JSONDecodeError:
        return {}
    return parsed if isinstance(parsed, dict) else {}


def collect_redis_and_queues(
    secret_path: Path = Path("/etc/massar/secrets/redis-password"),
) -> tuple[dict[str, int | float | str], dict[str, dict[str, int]]]:
    if not secret_path.is_file():
        return {}, {}
    password = secret_path.read_text(encoding="utf-8").strip()
    if not password:
        return {}, {}
    redis_env = {"REDISCLI_AUTH": password}
    info_code, info = command(
        ["redis-cli", "--no-auth-warning", "-h", "127.0.0.1", "-p", "6379", "INFO"],
        timeout=5,
        extra_env=redis_env,
    )
    lua = (
        "local out={} "
        "for _,q in ipairs(ARGV) do "
        "local p='bull:'..q..':' "
        "table.insert(out,q) "
        "table.insert(out,redis.call('LLEN',p..'wait')) "
        "table.insert(out,redis.call('LLEN',p..'active')) "
        "table.insert(out,redis.call('ZCARD',p..'delayed')) "
        "table.insert(out,redis.call('ZCARD',p..'failed')) "
        "table.insert(out,redis.call('SCARD',p..'stalled')) "
        "end return out"
    )
    queue_code, queue_output = command(
        [
            "redis-cli", "--no-auth-warning", "--raw", "-h", "127.0.0.1", "-p", "6379",
            "EVAL", lua, "0", *QUEUE_NAMES,
        ],
        timeout=5,
        extra_env=redis_env,
    )
    return (
        parse_redis_info(info) if info_code == 0 else {},
        parse_bullmq_counts(queue_output) if queue_code == 0 else {},
    )


def main() -> int:
    parser = __import__("argparse").ArgumentParser()
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    root_usage = shutil.disk_usage("/")
    shared = Path("/srv/massar-shared")
    services = {
        name: command(["systemctl", "is-active", name])[1] or "unknown"
        for name in SERVICES
    }
    _, docker_health = command([
        "docker", "ps", "--format",
        "{{.Names}}|{{.Status}}|{{.Label \"net.massar.release\"}}",
    ])
    _, docker_stats = command(["docker", "stats", "--no-stream", "--format", "{{json .}}"])
    _, patroni = command(["patronictl", "-c", "/etc/patroni/config.yml", "list", "--format", "json"])
    _, gluster = command(["gluster", "volume", "heal", "massar-shared", "info", "summary"])
    redis, queues = collect_redis_and_queues()
    load_average = os.getloadavg()
    _, established = command(["ss", "-Htan", "state", "established"])
    payload = {
        "schemaVersion": 1,
        "capturedAt": dt.datetime.now(dt.timezone.utc).isoformat().replace("+00:00", "Z"),
        "nodeId": Path("/etc/massar/node-id").read_text(encoding="utf-8").strip()
        if Path("/etc/massar/node-id").is_file() else "unknown",
        "services": services,
        "rootDiskFreePercent": round(root_usage.free * 100 / root_usage.total, 2),
        "loadAverage": {
            "oneMinute": round(load_average[0], 2),
            "fiveMinutes": round(load_average[1], 2),
            "fifteenMinutes": round(load_average[2], 2),
        },
        "cpu": collect_cpu(),
        "memory": parse_meminfo(Path("/proc/meminfo").read_text(encoding="utf-8")),
        "network": {
            "establishedTcpConnections": len(established.splitlines()) if established else 0,
        },
        "sharedMounted": command(["mountpoint", "-q", str(shared)])[0] == 0,
        "databaseBackupAgeSeconds": file_age_seconds(Path("/var/lib/massar/evidence/backup/database-latest.json")),
        "fileBackupAgeSeconds": file_age_seconds(Path("/srv/massar-shared/.cluster-health/file-backup-latest.json")),
        "restoreEvidenceAgeSeconds": file_age_seconds(Path("/var/lib/massar/evidence/restore/latest.json")),
        "docker": docker_health.splitlines(),
        "dockerStats": parse_docker_stats(docker_stats),
        "patroni": json.loads(patroni) if patroni.startswith("[") else [],
        "postgres": collect_postgres(),
        "redis": redis,
        "queues": queues,
        "glusterHealthy": "Number of entries: 0" in gluster,
    }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    temporary = args.output.with_suffix(".tmp")
    temporary.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
    os.chmod(temporary, 0o640)
    os.replace(temporary, args.output)
    unhealthy = (
        payload["rootDiskFreePercent"] < 15
        or not payload["sharedMounted"]
        or any(value not in {"active", "unknown"} for value in services.values())
    )
    return 6 if unhealthy else 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, subprocess.TimeoutExpired, json.JSONDecodeError) as exc:
        print(f"cluster health collection failed: {exc}", file=sys.stderr)
        raise SystemExit(6)
