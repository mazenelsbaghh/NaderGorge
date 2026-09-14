from __future__ import annotations

from pathlib import Path


ROOT = Path(__file__).resolve().parents[3]


def test_redis_has_aof_replica_safety_and_external_secret_placeholder() -> None:
    config = (ROOT / "deploy/production/config/redis/redis.conf.tmpl").read_text()
    assert "appendonly yes" in config
    assert "appendfsync everysec" in config
    assert "min-replicas-to-write 1" in config
    assert "min-replicas-max-lag 5" in config
    assert "__REDIS_PASSWORD__" in config


def test_three_sentinels_can_use_quorum_two() -> None:
    config = (ROOT / "deploy/production/config/redis/sentinel.conf.tmpl").read_text()
    assert "sentinel monitor massar-redis 10.77.0.11 6379 2" in config
    assert "sentinel auth-pass massar-redis __REDIS_PASSWORD__" in config
    assert "requirepass __REDIS_PASSWORD__" in config
    assert "sentinel parallel-syncs massar-redis 1" in config
