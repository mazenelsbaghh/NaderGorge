from __future__ import annotations

import importlib.util
from pathlib import Path


ROOT = Path(__file__).resolve().parents[3]
COLLECTOR_PATH = ROOT / "deploy/production/scripts/collect_cluster_health.py"
SPEC = importlib.util.spec_from_file_location("collect_cluster_health", COLLECTOR_PATH)
assert SPEC and SPEC.loader
collector = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(collector)


def test_health_timer_collects_every_minute_and_covers_cluster_dependencies() -> None:
    source = COLLECTOR_PATH.read_text(encoding="utf-8")
    timer = (
        ROOT / "deploy/production/systemd/massar-cluster-health.timer"
    ).read_text(encoding="utf-8")
    for dependency in (
        "chrony", "docker", "etcd", "patroni", "haproxy",
        "redis-server", "redis-sentinel", "glusterd", "cloudflared",
    ):
        assert dependency in source
    for metric in (
        "rootDiskFreePercent",
        "databaseBackupAgeSeconds",
        "fileBackupAgeSeconds",
        "restoreEvidenceAgeSeconds",
        "loadAverage",
        "cpu",
        "memory",
        "dockerStats",
        "postgres",
        "redis",
        "queues",
    ):
        assert metric in source
    assert "OnUnitActiveSec=1min" in timer


def test_cpu_sample_reports_utilization_and_iowait_from_counter_deltas() -> None:
    first = "cpu 100 0 50 850 10 0 0 0 0 0"
    second = "cpu 140 0 70 930 20 0 0 0 0 0"
    sample = collector.cpu_sample(first, second)
    assert sample["utilizationPercent"] == 40.0
    assert sample["iowaitPercent"] == 6.67


def test_meminfo_uses_available_memory_and_reports_swap_use() -> None:
    result = collector.parse_meminfo(
        "MemTotal:       1000 kB\n"
        "MemFree:         100 kB\n"
        "MemAvailable:    400 kB\n"
        "SwapTotal:       200 kB\n"
        "SwapFree:        150 kB\n"
    )
    assert result == {
        "totalBytes": 1_024_000,
        "availableBytes": 409_600,
        "usedPercent": 60.0,
        "swapUsedBytes": 51_200,
    }


def test_redis_and_bullmq_parsers_ignore_noise_and_preserve_queue_counts() -> None:
    redis = collector.parse_redis_info(
        "# Clients\nconnected_clients:12\nblocked_clients:1\n"
        "# Memory\nused_memory:2048\nmem_fragmentation_ratio:1.25\n"
        "secret_not_allowed:ignored\n"
    )
    assert redis == {
        "connected_clients": 12,
        "blocked_clients": 1,
        "used_memory": 2048,
        "mem_fragmentation_ratio": 1.25,
    }
    queues = collector.parse_bullmq_counts(
        "notifications\n3\n1\n2\n4\n0\n"
        "ai-video-chapters\n5\n2\n1\n0\n1"
    )
    assert queues["notifications"] == {
        "waiting": 3,
        "active": 1,
        "delayed": 2,
        "failed": 4,
        "stalled": 0,
    }
    assert queues["ai-video-chapters"]["waiting"] == 5


def test_docker_stats_parser_drops_invalid_lines_without_failing_snapshot() -> None:
    assert collector.parse_docker_stats(
        '{"Name":"backend","CPUPerc":"12.5%"}\nnot-json\n'
        '{"Name":"worker","MemUsage":"512MiB / 31GiB"}'
    ) == [
        {"Name": "backend", "CPUPerc": "12.5%"},
        {"Name": "worker", "MemUsage": "512MiB / 31GiB"},
    ]
