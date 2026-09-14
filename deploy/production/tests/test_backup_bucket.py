from __future__ import annotations

import importlib.util
import json
import subprocess
import sys
from pathlib import Path
from types import SimpleNamespace

import pytest


ROOT = Path(__file__).resolve().parents[3]
SCRIPTS = ROOT / "deploy/production/scripts"
sys.path.insert(0, str(SCRIPTS))
SPEC = importlib.util.spec_from_file_location(
    "manage_backup_bucket",
    SCRIPTS / "manage_backup_bucket.py",
)
assert SPEC and SPEC.loader
bucket = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(bucket)


def test_garage_template_is_three_way_internal_storage() -> None:
    template = (
        ROOT / "deploy/production/config/garage/garage.toml.tmpl"
    ).read_text(encoding="utf-8")
    assert "replication_factor = 3" in template
    assert 'rpc_bind_addr = "__OVERLAY_ADDRESS__:8738"' in template
    assert 'api_bind_addr = "0.0.0.0:9000"' in template
    assert "amazonaws.com" not in template
    assert "r2.cloudflarestorage.com" not in template


def test_backup_bucket_image_is_pinned_and_not_publicly_routed() -> None:
    service = (
        ROOT / "deploy/production/systemd/massar-backup-bucket.service"
    ).read_text(encoding="utf-8")
    assert "dxflrs/garage@sha256:" in service
    assert "--network host" in service
    assert "--rpc-secret-file /run/secrets/garage-rpc" in service
    assert "--admin-token-file /run/secrets/garage-admin" in service
    assert "9000:9000" not in service
    firewall = (
        ROOT / "deploy/production/config/firewall/massar-production.nft"
    ).read_text(encoding="utf-8")
    assert 'iifname "wg0"' in firewall
    assert "8738-8742, 9000" in firewall


def test_pgbackrest_tls_proxy_is_loopback_only_and_digest_pinned() -> None:
    proxy = (
        ROOT / "deploy/production/config/garage/haproxy-tls.cfg"
    ).read_text(encoding="utf-8")
    service = (
        ROOT / "deploy/production/systemd/massar-backup-tls-proxy.service"
    ).read_text(encoding="utf-8")
    assert "bind 127.0.0.1:9443 ssl" in proxy
    assert "server local-garage 127.0.0.1:9000 check" in proxy
    assert "haproxy@sha256:" in service
    assert "9443:9443" not in service
    assert "--read-only --cap-drop ALL --security-opt no-new-privileges" in service


def test_pgbackrest_and_restic_use_the_local_three_node_endpoint() -> None:
    pgbackrest = (
        ROOT / "deploy/production/config/pgbackrest/pgbackrest.conf.tmpl"
    ).read_text(encoding="utf-8")
    restic = (
        ROOT / "deploy/production/config/backup/files/restic.env.example"
    ).read_text(encoding="utf-8")
    assert "repo1-s3-endpoint=127.0.0.1" in pgbackrest
    assert "repo1-storage-port=9443" in pgbackrest
    assert "repo1-storage-ca-file=/etc/massar/pki/backup-ca.crt" in pgbackrest
    assert "repo1-s3-bucket=massar-backups" in pgbackrest
    assert "repo1-cipher-type=aes-256-cbc" in pgbackrest
    assert "s3:http://127.0.0.1:9000/massar-backups/" in restic
    assert "RESTIC_PASSWORD_FILE=" in restic


def test_patroni_archives_wal_with_a_five_minute_bound() -> None:
    patroni = (
        ROOT / "deploy/production/config/patroni/patroni.yml.tmpl"
    ).read_text(encoding="utf-8")
    assert 'archive_mode: "on"' in patroni
    assert 'archive_command: "pgbackrest --stanza=massar archive-push %p"' in patroni
    assert "archive_timeout: 300" in patroni
    configurator = (
        ROOT / "deploy/production/scripts/configure_database_archiving.py"
    ).read_text(encoding="utf-8")
    assert "replicas-first-current-primary-last" in configurator
    assert "exactly one primary" in configurator
    assert '"patroni.service"' in configurator
    assert '"/usr/bin/systemctl"' in configurator


def test_secret_parser_keeps_values_out_of_templates(tmp_path: Path) -> None:
    output = """Key name: massar-backup-client
Key ID: GK0123456789abcdef0123456789abcdef
Secret key: 0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef
"""
    access, secret = bucket.store_client_secrets(tmp_path, output)
    assert access.stat().st_mode & 0o777 == 0o600
    assert secret.stat().st_mode & 0o777 == 0o600
    assert access.read_text(encoding="utf-8").startswith("GK")
    with pytest.raises(bucket.BackupBucketError):
        bucket.store_client_secrets(tmp_path, "not a Garage key")


def test_tls_assets_are_generated_with_protected_private_material(
    tmp_path: Path,
) -> None:
    ca_certificate, tls_bundle = bucket.ensure_tls_assets(tmp_path)
    assert "BEGIN CERTIFICATE" in ca_certificate.read_text(encoding="utf-8")
    bundle = tls_bundle.read_text(encoding="utf-8")
    assert "BEGIN CERTIFICATE" in bundle
    assert "BEGIN PRIVATE KEY" in bundle
    assert tls_bundle.stat().st_mode & 0o777 == 0o600
    assert (tmp_path / "backup-ca.key").stat().st_mode & 0o777 == 0o600


def test_inventory_assigns_backup_role_to_every_node() -> None:
    inventory = json.loads(
        (ROOT / "deploy/production/inventory/production.yml").read_text(
            encoding="utf-8"
        )
    )
    assert all("backup-object" in node["roles"] for node in inventory["nodes"])


def test_schedule_activation_is_guarded_and_primary_owned() -> None:
    source = (
        ROOT / "deploy/production/scripts/manage_backup_bucket.py"
    ).read_text(encoding="utf-8")
    assert "schedule activation requires successful database and file restore evidence" in source
    assert "massar-db-restore-scheduled.service" in source
    assert '"--property=Unit"' in source
    assert '"restart"' in source


def test_release_tool_sync_dry_run_is_read_only_and_reports_all_nodes(
    capsys: pytest.CaptureFixture[str],
) -> None:
    nodes = tuple(
        SimpleNamespace(id=f"node-{index}", public_address=f"192.0.2.{index}")
        for index in (1, 2, 3)
    )
    inventory = SimpleNamespace(nodes=nodes, cluster={"ssh_user": "massar-ops"})

    class Transport:
        def __init__(self) -> None:
            self.copies: list[tuple[object, ...]] = []

        def run(self, _target, argv, **_kwargs):
            return subprocess.CompletedProcess(
                argv,
                1,
                stdout="",
                stderr="missing",
            )

        def copy(self, *args, **_kwargs) -> None:
            self.copies.append(args)

    transport = Transport()
    bucket.sync_release_tools(
        transport,
        inventory,
        nodes,
        confirmed=False,
    )

    evidence = json.loads(capsys.readouterr().out)
    assert evidence["status"] == "dry-run"
    assert set(evidence["nodes"]) == {"node-1", "node-2", "node-3"}
    assert all(node["action"] == "update" for node in evidence["nodes"].values())
    assert transport.copies == []
