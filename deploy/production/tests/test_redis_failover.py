from __future__ import annotations

from pathlib import Path


ROOT = Path(__file__).resolve().parents[3]


def test_redis_drill_resolves_current_master_preserves_data_and_rejoins_replica() -> None:
    source = (
        ROOT / "deploy/production/scripts/run_redis_failover_drill.sh"
    ).read_text(encoding="utf-8")
    assert "SENTINEL get-master-addr-by-name massar-redis" in source
    assert "SET \"$probe_key\" preserved" in source
    assert "sudo systemctl stop redis-server" in source
    assert "trap cleanup EXIT HUP INT TERM" in source
    assert "SENTINEL ckquorum massar-redis" in source
    assert "Sentinels disagree" in source
    assert 'connected_slaves:2' in source
    assert 'masters" -eq 1' in source
    assert 'replicas" -eq 2' in source
    assert "failover-drill.lock" in source
    assert "GET \"$probe_key\"" in source
    assert "ROLE" in source
    assert "slave" in source
    assert "mapfile" not in source
    assert "mktemp \"${TMPDIR:-/tmp}/massar-redis-nodes.XXXXXX\"" in source
    assert "while IFS= read -r row" in source
    assert "declare -A" not in source
    assert "public_for_node()" in source
    assert "node_for_overlay()" in source
    assert "printf -v quoted '%q' \"$argument\"" in source
    assert 'ssh "${SSH_OPTIONS[@]}" "$SSH_USER@$(public_for_node "$node_id")" "$remote_command"' in source
    for node in ("191.218.161.76", "191.218.161.78", "168.231.106.230"):
        assert node not in source
