from __future__ import annotations

import importlib.util
import os
import shutil
import subprocess
import time
from pathlib import Path

import pytest


ROOT = Path(__file__).resolve().parents[3]


def load_module(name: str, relative: str):
    spec = importlib.util.spec_from_file_location(name, ROOT / relative)
    assert spec and spec.loader
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


audit = load_module("audit_database", "deploy/production/scripts/audit_database.py")


def test_database_audit_accepts_dotnet_connection_without_logging_password(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    secret = "a-private-password-value"
    monkeypatch.delenv("DATABASE_URL", raising=False)
    monkeypatch.setenv(
        "ConnectionStrings__DefaultConnection",
        f"Host=127.0.0.1;Port=6432;Database=massar_platform;Username=massar_app;Password={secret}",
    )
    captured: dict[str, object] = {}

    def fake_run(argv, **kwargs):
        captured["argv"] = argv
        captured["env"] = kwargs["env"]
        return subprocess.CompletedProcess(argv, 0, "1\n", "")

    monkeypatch.setattr(audit.subprocess, "run", fake_run)
    assert audit.psql("SELECT 1") == "1"
    assert secret not in " ".join(captured["argv"])
    assert captured["env"]["PGPASSWORD"] == secret


def test_database_audit_rejects_incomplete_connection(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    monkeypatch.delenv("DATABASE_URL", raising=False)
    monkeypatch.setenv("ConnectionStrings__DefaultConnection", "Host=db")
    with pytest.raises(RuntimeError, match="incomplete"):
        audit.connection_environment()


def test_latest_model_migration_ensures_system_roles() -> None:
    assert audit.latest_model_migration().endswith("_EnsureSystemRoles")


def test_migrator_uses_nonblocking_advisory_lock_and_redacts_failures() -> None:
    source = (
        ROOT / "backend/src/NaderGorge.Migrator/Program.cs"
    ).read_text(encoding="utf-8")
    assert "pg_try_advisory_lock" in source
    assert "another migrator owns" in source
    assert "[REDACTED_CONNECTION]" in source
    assert "EnableSensitiveDataLogging(false)" in source


def test_admin_bootstrap_is_atomic_parameterized_and_duplicate_safe() -> None:
    source = (
        ROOT / "backend/src/NaderGorge.AdminBootstrap/Program.cs"
    ).read_text(encoding="utf-8")
    assert "BeginTransactionAsync" in source
    assert "CommitAsync" in source
    assert "phone already exists" in source
    assert "BCrypt.HashPassword" in source
    assert "PasswordHash = password" not in source
    assert "ProductionAdminBootstrap" in source


def test_admin_wrapper_uses_no_echo_and_never_passes_password_as_argument() -> None:
    source = (
        ROOT / "deploy/production/scripts/bootstrap_admin.py"
    ).read_text(encoding="utf-8")
    assert "getpass.getpass" in source
    assert 'input=f"{phone}\\n{password}\\n{args.name}\\n"' in source
    assert '"--password"' not in source


def test_release_migration_runs_clean_database_before_production() -> None:
    source = (
        ROOT / "deploy/production/scripts/migrate_release.py"
    ).read_text(encoding="utf-8")
    assert source.index("createdb") < source.rindex("Database=massar_platform")
    assert "massar_audit_" in source
    assert "cluster_leases" in source
    assert "pg_index where not indisvalid" in source
    assert "not convalidated" in source
    assert 'from roles where "Name" in (' in source
    assert "MASSAR_MIGRATION_FAILURE" in source
    assert 'sh \'select "MigrationId"' in source
    assert "load_migration_safety_gate" in source
    assert "database_system_identifier" in source
    assert "pre_migration_ids_sha256" in source
    assert "post_migration_ids_sha256" in source
    assert "manifest_sha256" in source
    assert "--env-file /etc/massar/app.env" not in source
    assert "Host=127.0.0.1;Port=6432;Database=massar_platform" in source
    assert "setpriv --reuid=65532 --regid=65532 --clear-groups" in source


def test_database_restore_is_isolated_point_in_time_and_monthly() -> None:
    restore = (
        ROOT / "deploy/production/scripts/restore_database_sample.sh"
    ).read_text(encoding="utf-8")
    timer = (
        ROOT / "deploy/production/systemd/massar-db-restore-test.timer"
    ).read_text(encoding="utf-8")
    scheduled_service = (
        ROOT / "deploy/production/systemd/massar-db-restore-scheduled.service"
    ).read_text(encoding="utf-8")
    assert "mktemp -d" in restore
    assert "age > 300" in restore
    assert "--type=time" in restore
    assert "--target-action=promote" in restore
    assert "pg_index where not indisvalid" in restore
    assert "/var/lib/massar-restore-tests" in restore
    assert "--no-archive-async" in restore
    assert "--pg1-path=%s" in restore
    assert "-c archive_mode=off" in restore
    assert "not pg_is_in_recovery()" in restore
    assert "seq 1 300" in restore
    wrapper = (
        ROOT / "deploy/production/scripts/run_database_restore_drill.sh"
    ).read_text(encoding="utf-8")
    assert "127.0.0.1:8008/primary" in wrapper
    assert "massar-prepare-pitr-probe" in wrapper
    assert "OnCalendar=*-*-01" in timer
    assert "Unit=massar-db-restore-scheduled.service" in timer
    assert "run_database_restore_drill.sh" in scheduled_service


def test_admin_bootstrap_real_postgres_atomic_duplicate_and_rollback() -> None:
    if os.environ.get("MASSAR_DATABASE_TOOLS_INTEGRATION") != "1":
        pytest.skip("set MASSAR_DATABASE_TOOLS_INTEGRATION=1 against a disposable migrated PostgreSQL database")
    connection = os.environ["ConnectionStrings__DefaultConnection"]
    first_phone = "01000000166"
    rollback_phone = "01000000266"
    credential = "Integration.166.Password"
    project = ROOT / "backend/src/NaderGorge.AdminBootstrap/NaderGorge.AdminBootstrap.csproj"
    environment = {**os.environ, "ConnectionStrings__DefaultConnection": connection}

    def execute(phone: str):
        return subprocess.run(
            ["dotnet", "run", "--project", str(project), "--no-build", "--no-launch-profile"],
            input=f"{phone}\n{credential}\nIntegration Owner\n",
            text=True,
            capture_output=True,
            check=False,
            env=environment,
        )

    audit.psql(
        f"""DELETE FROM audit_logs WHERE "PerformedByUserId" IN (SELECT "Id" FROM users WHERE "PhoneNumber" IN ('{first_phone}','{rollback_phone}'));
        DELETE FROM user_roles WHERE "UserId" IN (SELECT "Id" FROM users WHERE "PhoneNumber" IN ('{first_phone}','{rollback_phone}'));
        DELETE FROM users WHERE "PhoneNumber" IN ('{first_phone}','{rollback_phone}');"""
    )
    try:
        schema_audit = audit.collect()
        assert schema_audit["migrationModelMatch"] is True
        assert schema_audit["orphanForeignKeyRowCount"] == 0
        assert schema_audit["duplicateConstrainedKeyRowCount"] == 0
        assert schema_audit["criticalFindings"] == 0

        first = execute(first_phone)
        assert first.returncode == 0
        assert credential not in first.stdout + first.stderr
        row = audit.psql(
            f"""SELECT count(*) || ':' || (min("PasswordHash") LIKE '$2%')::text
            FROM users WHERE "PhoneNumber"='{first_phone}';"""
        )
        assert row == "1:true"
        duplicate = execute(first_phone)
        assert duplicate.returncode == 5
        assert audit.psql(
            f"""SELECT count(*) FROM users WHERE "PhoneNumber"='{first_phone}';"""
        ) == "1"

        audit.psql("""UPDATE roles SET "Name"='AdminTemporarilyMissing' WHERE "Name"='Admin';""")
        try:
            rolled_back = execute(rollback_phone)
            assert rolled_back.returncode == 6
            assert audit.psql(
                f"""SELECT count(*) FROM users WHERE "PhoneNumber"='{rollback_phone}';"""
            ) == "0"
        finally:
            audit.psql("""UPDATE roles SET "Name"='Admin' WHERE "Name"='AdminTemporarilyMissing';""")
    finally:
        audit.psql(
            f"""DELETE FROM audit_logs WHERE "PerformedByUserId" IN (SELECT "Id" FROM users WHERE "PhoneNumber" IN ('{first_phone}','{rollback_phone}'));
            DELETE FROM user_roles WHERE "UserId" IN (SELECT "Id" FROM users WHERE "PhoneNumber" IN ('{first_phone}','{rollback_phone}'));
            DELETE FROM users WHERE "PhoneNumber" IN ('{first_phone}','{rollback_phone}');"""
        )


def test_concurrent_migrator_refuses_an_owned_advisory_lock() -> None:
    if os.environ.get("MASSAR_DATABASE_TOOLS_INTEGRATION") != "1":
        pytest.skip("set MASSAR_DATABASE_TOOLS_INTEGRATION=1 against a disposable migrated PostgreSQL database")
    psql_binary = os.environ.get("PSQL_BIN") or shutil.which("psql")
    homebrew_psql = Path("/opt/homebrew/opt/libpq/bin/psql")
    if not psql_binary and homebrew_psql.is_file():
        psql_binary = str(homebrew_psql)
    if not psql_binary:
        pytest.skip("psql is required for the real migration-lock test")
    lock_environment = {
        **os.environ,
        **audit.connection_environment(),
        "PGCONNECT_TIMEOUT": "10",
    }
    lock_owner = subprocess.Popen(
        [
            psql_binary,
            "--no-psqlrc",
            "--set", "ON_ERROR_STOP=1",
            "--command",
            "SELECT pg_advisory_lock(4832779884013771991); SELECT pg_sleep(10);",
        ],
        text=True,
        stdout=subprocess.DEVNULL,
        stderr=subprocess.PIPE,
        env=lock_environment,
    )
    try:
        time.sleep(1)
        migration = subprocess.run(
            [
                "dotnet", "run",
                "--project", str(ROOT / "backend/src/NaderGorge.Migrator/NaderGorge.Migrator.csproj"),
                "--no-build", "--no-launch-profile",
            ],
            text=True,
            capture_output=True,
            check=False,
            env=os.environ,
        )
        assert migration.returncode == 5
        assert "another migrator owns" in migration.stderr
    finally:
        lock_owner.terminate()
        lock_owner.wait(timeout=5)
