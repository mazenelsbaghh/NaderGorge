from __future__ import annotations

from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
HOSTS = {
    "massar-academy.net",
    "app.massar-academy.net",
    "admin.massar-academy.net",
    "teacher.massar-academy.net",
    "staff.massar-academy.net",
    "api.massar-academy.net",
    "ws.massar-academy.net",
    "assets.massar-academy.net",
}


def test_every_node_runs_the_complete_application_stack() -> None:
    compose = (ROOT / "deploy/production/compose/compose.app.yml").read_text()
    for service in (
        "backend",
        "worker",
        "landing",
        "student",
        "admin",
        "teacher",
        "staff",
        "gateway",
    ):
        assert f"  {service}:\n" in compose
    assert "\n  db:\n" not in compose
    assert "\n  redis:\n" not in compose
    assert "subnet: 172.29.0.0/24" in compose


def test_worker_keeps_shared_storage_group_after_dropping_root() -> None:
    # Regression for the 2026-07-30 subtitle EACCES incident on shared storage.
    compose = (ROOT / "deploy/production/compose/compose.app.yml").read_text()
    entrypoint = (ROOT / "worker/docker-entrypoint.sh").read_text()

    worker = compose.split("\n  worker:\n", 1)[1].split("\n  landing:\n", 1)[0]
    assert "group_add:" in worker
    assert "MASSAR_SHARED_GID: ${MASSAR_SHARED_GID:" in worker

    assert 'shared_gid="${MASSAR_SHARED_GID:-}"' in entrypoint
    assert 'usermod --append --groups "$shared_group" worker' in entrypoint
    assert entrypoint.index("prepare_shared_storage_group") < entrypoint.index(
        'exec gosu worker "$@"'
    )


def test_haproxy_balances_every_approved_host_across_three_nodes() -> None:
    config = (ROOT / "deploy/production/config/haproxy/postgres.cfg").read_text()
    for host in HOSTS:
        assert host in config
    assert "http-request deny deny_status 421 unless approved_host" in config
    app_backend = config.split("backend massar_nodes", 1)[1].split(
        "backend massar_signalr_nodes", 1
    )[0]
    assert "balance roundrobin" in app_backend
    assert app_backend.count(":8080 check") == 3


def test_signalr_hubs_use_websocket_safe_failover_without_stale_affinity() -> None:
    """WebSocket-only clients can reconnect to any healthy Redis-backed node."""
    config = (ROOT / "deploy/production/config/haproxy/postgres.cfg").read_text()
    frontend = config.split("frontend massar_ingress", 1)[1].split(
        "backend massar_nodes", 1
    )[0]
    signalr_backend = config.split("backend massar_signalr_nodes", 1)[1]

    assert "acl signalr_hub path_beg /hubs/" in frontend
    assert "use_backend massar_signalr_nodes if signalr_hub" in frontend
    assert "balance roundrobin" in signalr_backend
    assert "MASSAR_SIGNALR_NODE" not in signalr_backend
    assert "cookie node-" not in signalr_backend


def test_node_gateway_has_exact_surface_routes_and_private_asset_denials() -> None:
    config = (
        ROOT / "deploy/production/config/nginx/massar-node.conf.template"
    ).read_text()
    configured = {
        line.split("server_name", 1)[1].strip(" ;")
        for line in config.splitlines()
        if line.strip().startswith("server_name ")
    }
    assert HOSTS <= configured
    assert "location ^~ /protected/ { return 404; }" in config
    assert "location ^~ /private/ { return 404; }" in config
    assert "proxy_set_header Upgrade $http_upgrade;" in config


def test_node_readiness_keeps_release_identity_headers_in_its_nested_location() -> None:
    config = (
        ROOT / "deploy/production/config/nginx/massar-node.conf.template"
    ).read_text()
    readiness = config.split("location = /__node_ready {", 1)[1].split("    }", 1)[0]
    assert 'add_header X-Massar-Node "${MASSAR_NODE_ID}" always;' in readiness
    assert 'add_header X-Massar-Release "${MASSAR_RELEASE_ID}" always;' in readiness
    assert "proxy_pass http://backend:5245/api/health/ready;" in readiness
    assert "return 200" not in readiness
