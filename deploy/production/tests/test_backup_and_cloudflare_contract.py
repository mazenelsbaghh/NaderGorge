from __future__ import annotations

import importlib.util
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
sys.path.insert(0, str(ROOT / "deploy/production/scripts"))
HOSTS = [
    "massar-academy.net",
    "app.massar-academy.net",
    "admin.massar-academy.net",
    "teacher.massar-academy.net",
    "staff.massar-academy.net",
    "api.massar-academy.net",
    "ws.massar-academy.net",
    "assets.massar-academy.net",
]
MANAGER_SPEC = importlib.util.spec_from_file_location(
    "manage_cloudflare",
    ROOT / "deploy/production/scripts/manage_cloudflare.py",
)
assert MANAGER_SPEC and MANAGER_SPEC.loader
manager = importlib.util.module_from_spec(MANAGER_SPEC)
MANAGER_SPEC.loader.exec_module(manager)


def test_pgbackrest_contract_is_internal_three_node_encrypted_and_low_impact() -> None:
    config = (
        ROOT / "deploy/production/config/pgbackrest/pgbackrest.conf.tmpl"
    ).read_text()
    assert "repo1-type=s3" in config
    assert "repo1-s3-endpoint=127.0.0.1" in config
    assert "repo1-storage-port=9443" in config
    assert "repo1-s3-bucket=massar-backups" in config
    assert "repo1-cipher-type=aes-256-cbc" in config
    assert "repo1-retention-full-type=time" in config
    assert "repo1-retention-full=30" in config
    assert "archive-async=y" in config
    assert "archive-timeout=300" in config
    assert "process-max=2" in config
    assert "OnCalendar=Sun *-*-* 03:30:00 Africa/Cairo" in (
        ROOT / "deploy/production/systemd/massar-pgbackrest-full.timer"
    ).read_text()
    assert "OnCalendar=*-*-* 02:30:00 Africa/Cairo" in (
        ROOT / "deploy/production/systemd/massar-pgbackrest-diff.timer"
    ).read_text()


def test_file_backups_are_hourly_incremental_with_30_day_retention() -> None:
    backup = (ROOT / "deploy/production/scripts/backup_files.sh").read_text()
    prune = (ROOT / "deploy/production/scripts/prune_file_backups.sh").read_text()
    restore = (ROOT / "deploy/production/scripts/restore_files_sample.sh").read_text()
    assert "restic backup" in backup
    assert "flock --nonblock" in backup
    assert "--keep-within 30d --prune" in prune
    assert "restic check --read-data-subset=5%" in restore
    assert "OnCalendar=hourly" in (
        ROOT / "deploy/production/systemd/massar-files-backup.timer"
    ).read_text()
    assert "OnCalendar=*-*-01 06:00:00 Africa/Cairo" in (
        ROOT / "deploy/production/systemd/massar-files-restore-test.timer"
    ).read_text()


def test_backup_and_restore_producers_emit_release_bound_acceptance_metadata() -> None:
    for relative in (
        "backup_database.sh",
        "backup_files.sh",
        "restore_database_sample.sh",
        "restore_files_sample.sh",
    ):
        source = (
            ROOT / "deploy/production/scripts" / relative
        ).read_text(encoding="utf-8")
        assert '["releaseId"]' in source
        assert '"releaseId":release_id' in source
        assert '"capturedAt":captured' in source


def test_cloudflared_maps_exact_hosts_to_local_haproxy_and_denies_fallback() -> None:
    config = (
        ROOT / "deploy/production/config/cloudflared/config.yml.tmpl"
    ).read_text()
    configured_hosts = [
        line.split("hostname:", 1)[1].strip()
        for line in config.splitlines()
        if "hostname:" in line
    ]
    assert configured_hosts == HOSTS
    assert config.count("service: http://127.0.0.1:8088") == len(HOSTS)
    assert config.rstrip().endswith("- service: http_status:404")


def test_cloudflare_rehearsal_host_is_inserted_before_fallback_with_host_override() -> None:
    rendered = manager.render(
        "16600000-0000-4000-8000-000000000166",
        "rehearsal.massar-academy.net",
    )
    rehearsal = rendered.index("hostname: rehearsal.massar-academy.net")
    fallback = rendered.index("service: http_status:404")
    assert rehearsal < fallback
    assert "httpHostHeader: massar-academy.net" in rendered


def test_cloudflare_status_requires_an_active_supported_connector_unit() -> None:
    source = (
        ROOT / "deploy/production/scripts/manage_cloudflare.py"
    ).read_text(encoding="utf-8")
    assert "systemctl is-active --quiet massar-cloudflared-token" in source
    assert "systemctl is-active --quiet cloudflared" in source
    assert "metrics_port=2010" in source
    assert "metrics_port=2000" in source
    assert "exit 3" in source
    assert "systemctl is-active cloudflared;" not in source
