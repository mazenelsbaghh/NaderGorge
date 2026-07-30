from __future__ import annotations

import hashlib
import importlib.util
import json
import stat
import subprocess
import sys
from pathlib import Path

import pytest


ROOT = Path(__file__).resolve().parents[4]
SCRIPTS = ROOT / "deploy/production/scripts"
sys.path.insert(0, str(SCRIPTS))
spec = importlib.util.spec_from_file_location("ssh_transport", SCRIPTS / "ssh_transport.py")
assert spec and spec.loader
ssh_transport = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = ssh_transport
spec.loader.exec_module(ssh_transport)

plan_spec = importlib.util.spec_from_file_location(
    "release_plan",
    ROOT / ".agents/skills/ssh-server/scripts/release_plan.py",
)
assert plan_spec and plan_spec.loader
release_plan = importlib.util.module_from_spec(plan_spec)
sys.modules[plan_spec.name] = release_plan
plan_spec.loader.exec_module(release_plan)

inventory_spec = importlib.util.spec_from_file_location(
    "schema_inventory",
    ROOT / ".agents/skills/ssh-server/scripts/schema_inventory.py",
)
assert inventory_spec and inventory_spec.loader
schema_inventory = importlib.util.module_from_spec(inventory_spec)
sys.modules[inventory_spec.name] = schema_inventory
inventory_spec.loader.exec_module(schema_inventory)


def transport(tmp_path: Path):
    known_hosts = tmp_path / "known_hosts"
    identity = tmp_path / "id_ed25519"
    known_hosts.write_text("pinned-host-key\n")
    identity.write_text("test-key-material\n")
    identity.chmod(stat.S_IRUSR | stat.S_IWUSR)
    return ssh_transport.StrictSshTransport(known_hosts, identity)


def test_transport_enforces_pinned_batch_key_only_ssh(tmp_path: Path) -> None:
    arguments = transport(tmp_path).base_args()
    joined = " ".join(arguments)
    assert "BatchMode=yes" in joined
    assert "IdentitiesOnly=yes" in joined
    assert "StrictHostKeyChecking=yes" in joined
    assert "UserKnownHostsFile=" in joined
    assert "StrictHostKeyChecking=" + "no" not in joined


def test_transport_rejects_permissive_private_key(tmp_path: Path) -> None:
    known_hosts = tmp_path / "known_hosts"
    identity = tmp_path / "id_ed25519"
    known_hosts.write_text("pinned\n")
    identity.write_text("test\n")
    identity.chmod(0o644)
    with pytest.raises(ssh_transport.SshTransportError, match="permissions"):
        ssh_transport.StrictSshTransport(known_hosts, identity)


def test_remote_arguments_are_shell_quoted_and_output_is_captured(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    captured: dict[str, object] = {}

    def fake_run(argv, **kwargs):
        captured["argv"] = argv
        return subprocess.CompletedProcess(argv, 0, "ok", "")

    monkeypatch.setattr(ssh_transport.subprocess, "run", fake_run)
    target = ssh_transport.SshTarget("node-1", "approved.example", "massar-ops")
    result = transport(tmp_path).run(target, ("printf", "%s", "value with spaces"))
    assert result.stdout == "ok"
    assert captured["argv"][-1] == "printf %s 'value with spaces'"


def test_skill_requires_dry_run_confirmation_and_non_root_operation() -> None:
    skill = (ROOT / ".agents/skills/ssh-server/SKILL.md").read_text(encoding="utf-8")
    assert "--dry-run" in skill
    assert "--yes" in skill
    assert "massar-ops" in skill
    assert "Strict host-key verification is mandatory" in skill
    assert "restore against the production DB" in skill


def test_release_plan_selects_affected_images() -> None:
    plan = release_plan.classify(
        "HEAD^",
        (
            "frontend/src/app/page.tsx",
            "worker/src/index.ts",
        ),
    )
    assert plan.components == ("frontend", "worker")
    assert plan.local_images == ("frontend", "worker")
    assert plan.database_changed is False
    assert plan.migration_required is False


def test_release_plan_blocks_entity_change_without_migration() -> None:
    plan = release_plan.classify(
        "HEAD^",
        (
            "backend/src/NaderGorge.Domain/Entities/Student.cs",
            "backend/src/NaderGorge.API/Controllers/StudentController.cs",
        ),
    )
    assert plan.database_changed is True
    assert plan.migration_required is True
    assert plan.local_images == ("backend", "migrator")


def test_release_plan_accepts_entity_change_with_numbered_migration() -> None:
    migration_name = "20260730120000_AddStudentField"
    plan = release_plan.classify(
        "HEAD^",
        (
            "backend/src/NaderGorge.Domain/Entities/Student.cs",
            "backend/src/NaderGorge.Infrastructure/Migrations/"
            f"{migration_name}.cs",
            "backend/src/NaderGorge.Infrastructure/Migrations/"
            f"{migration_name}.Designer.cs",
            "backend/src/NaderGorge.Infrastructure/Migrations/"
            "AppDbContextModelSnapshot.cs",
        ),
    )
    assert plan.database_changed is True
    assert plan.migration_added is True
    assert plan.migration_required is False


def test_release_plan_does_not_treat_edited_old_migration_as_new() -> None:
    migration = (
        "backend/src/NaderGorge.Infrastructure/Migrations/"
        "20260730120000_AddStudentField.cs"
    )
    plan = release_plan.classify(
        "HEAD^",
        (
            "backend/src/NaderGorge.Domain/Entities/Student.cs",
            migration,
        ),
        lambda path: path == migration,
    )
    assert plan.migration_added is False
    assert plan.migration_required is True


def test_release_plan_rejects_designer_without_main_migration() -> None:
    plan = release_plan.classify(
        "HEAD^",
        (
            "backend/src/NaderGorge.Domain/Entities/Student.cs",
            "backend/src/NaderGorge.Infrastructure/Migrations/"
            "20260730120000_AddStudentField.Designer.cs",
            "backend/src/NaderGorge.Infrastructure/Migrations/"
            "AppDbContextModelSnapshot.cs",
        ),
    )
    assert plan.migration_added is False
    assert plan.migration_required is True


def test_local_build_maps_images_to_real_compose_services() -> None:
    plan = release_plan.classify(
        "HEAD^",
        (
            "frontend/src/app/page.tsx",
            "docker/nginx/Dockerfile",
        ),
    )
    assert plan.local_images == ("frontend", "gateway")
    assert plan.local_services == ("landing", "gateway")


def test_changed_paths_includes_deletions(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    calls: list[tuple[str, ...]] = []

    def fake_git(*arguments: str, check: bool = True) -> str:
        calls.append(arguments)
        if arguments[:2] == ("diff", "--name-only"):
            return "backend/src/NaderGorge.Domain/Entities/Deleted.cs\n"
        return ""

    monkeypatch.setattr(release_plan, "git", fake_git)
    paths = release_plan.changed_paths("base")
    assert paths == (
        "backend/src/NaderGorge.Domain/Entities/Deleted.cs",
    )
    assert all("--diff-filter" not in call for call in calls)


def test_auto_base_uses_reviewed_origin_main_merge_base(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    monkeypatch.setattr(release_plan, "git_succeeds", lambda *args: True)
    monkeypatch.setattr(
        release_plan,
        "git",
        lambda *args, **kwargs: "reviewed-merge-base\n",
    )
    assert release_plan.resolve_base("AUTO") == "reviewed-merge-base"


def make_dry(*arguments: str) -> str:
    return subprocess.run(
        ("make", "-n", *arguments),
        cwd=ROOT,
        check=True,
        capture_output=True,
        text=True,
    ).stdout


def test_make_preview_and_mutation_targets_keep_confirmation_boundary() -> None:
    release = "RELEASE=git-0123456789abcdef0123456789abcdef01234567"
    preview = make_dry("prod-build-preview", release)
    mutation = make_dry("prod-build", release)
    deploy_alias = make_dry("deploy", release)
    assert "--yes" not in preview
    assert "--yes" in mutation
    assert "--yes" not in deploy_alias


def test_schema_inventory_reports_missing_tables_and_pending_migrations(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    snapshot = tmp_path / "Snapshot.cs"
    snapshot.write_text(
        'entity.ToTable("students");\nentity.ToTable("lessons");\n',
        encoding="utf-8",
    )
    migrations = tmp_path / "Migrations"
    migrations.mkdir()
    (migrations / "20260101000000_Initial.cs").write_text("// migration\n")
    (migrations / "20260102000000_AddLessons.cs").write_text("// migration\n")
    actual = tmp_path / "actual.json"
    actual.write_text(
        '{"status":"success","latestMigration":"20260101000000_Initial",'
        '"migrationIds":["20260101000000_Initial"],'
        '"tableCounts":{"students":4,"__EFMigrationsHistory":1}}',
        encoding="utf-8",
    )
    monkeypatch.setattr(schema_inventory, "MIGRATIONS", migrations)
    result = schema_inventory.compare(actual, snapshot)
    assert result["missingTables"] == ["lessons"]
    assert result["pendingMigrations"] == ["20260102000000_AddLessons"]
    assert result["extraTables"] == []
    assert result["status"] == "drift"


def test_schema_inventory_match_is_order_independent(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    snapshot = tmp_path / "Snapshot.cs"
    snapshot.write_text(
        'a.ToTable("z_table");\nb.ToTable("a_table");\n', encoding="utf-8"
    )
    migrations = tmp_path / "Migrations"
    migrations.mkdir()
    migration = "20260101000000_Initial"
    (migrations / f"{migration}.cs").write_text("// migration\n")
    actual = tmp_path / "actual.json"
    actual.write_text(
        json.dumps(
            {
                "status": "success",
                "latestMigration": migration,
                "migrationIds": [migration],
                "tableCounts": {"z_table": 0, "a_table": 0},
            }
        ),
        encoding="utf-8",
    )
    monkeypatch.setattr(schema_inventory, "MIGRATIONS", migrations)
    assert schema_inventory.compare(actual, snapshot)["status"] == "match"


def test_schema_inventory_extra_table_is_drift(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    snapshot = tmp_path / "Snapshot.cs"
    snapshot.write_text('a.ToTable("expected");\n', encoding="utf-8")
    migrations = tmp_path / "Migrations"
    migrations.mkdir()
    migration = "20260101000000_Initial"
    (migrations / f"{migration}.cs").write_text("// migration\n")
    actual = tmp_path / "actual.json"
    actual.write_text(
        json.dumps(
            {
                "status": "success",
                "latestMigration": migration,
                "migrationIds": [migration],
                "tableCounts": {"expected": 0, "rogue": 0},
            }
        )
    )
    monkeypatch.setattr(schema_inventory, "MIGRATIONS", migrations)
    result = schema_inventory.compare(actual, snapshot)
    assert result["status"] == "drift"
    assert result["extraTables"] == ["rogue"]


def test_repository_schema_inventory_covers_full_snapshot() -> None:
    tables, migrations = schema_inventory.expected_contract()
    assert len(tables) >= 200
    assert len(migrations) >= 130
    assert "users" in tables
    assert "web_vitals_metrics" in tables


def test_db_only_repair_requires_migrations_in_current_release(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    repair_spec = importlib.util.spec_from_file_location(
        "database_repair_plan",
        ROOT / ".agents/skills/ssh-server/scripts/database_repair_plan.py",
    )
    assert repair_spec and repair_spec.loader
    module = importlib.util.module_from_spec(repair_spec)
    sys.modules[repair_spec.name] = module
    repair_spec.loader.exec_module(module)
    migration = "20260101000000_AddSafeTable"
    migrations = tmp_path / "Migrations"
    migrations.mkdir()
    (migrations / f"{migration}.cs").write_text(
        'migrationBuilder.CreateTable(name: "safe_table", columns: table => new {});'
    )
    monkeypatch.setattr(module, "MIGRATIONS", migrations)
    comparison = tmp_path / "comparison.json"
    comparison.write_text(
        json.dumps(
            {
                "expectedMigrations": [migration],
                "actualMigrations": [],
                "pendingMigrations": [migration],
                "unexpectedMigrations": [],
                "missingTables": ["safe_table"],
                "extraTables": [],
            }
        )
    )
    manifest = tmp_path / "manifest.json"
    manifest.write_text(
        json.dumps(
            {
                "releaseId": "git-" + "a" * 40,
                "images": {"migrator": "sha256:" + "b" * 64},
                "migrationSet": [migration],
            }
        )
    )
    assert module.plan(comparison, manifest)["status"] == "eligible"
    manifest.write_text(
        json.dumps(
            {
                "releaseId": "git-" + "a" * 40,
                "images": {"migrator": "sha256:" + "b" * 64},
                "migrationSet": [],
            }
        )
    )
    with pytest.raises(module.RepairPlanError, match="current Production migrator"):
        module.plan(comparison, manifest)
    manifest.write_text(
        json.dumps(
            {
                "releaseId": "git-" + "a" * 40,
                "images": {"migrator": "sha256:" + "b" * 64},
                "migrationSet": [migration],
            }
        )
    )
    value = json.loads(comparison.read_text())
    value["extraTables"] = ["rogue"]
    comparison.write_text(json.dumps(value))
    with pytest.raises(module.RepairPlanError, match="extra server tables"):
        module.plan(comparison, manifest)


def test_make_db_fast_preview_and_apply_confirmation_boundary() -> None:
    preview = make_dry(
        "prod-db-fast-preview",
        "REASON=reviewed database incident",
    )
    assert "--yes" not in preview
    rejected = subprocess.run(
        ("make", "prod-db-fast", "REASON=reviewed database incident"),
        cwd=ROOT,
        check=False,
        capture_output=True,
        text=True,
    )
    assert rejected.returncode != 0
    assert "CONFIRM=DB-ONLY" in rejected.stdout + rejected.stderr
    applied = make_dry(
        "prod-db-fast",
        "REASON=reviewed database incident",
        "CONFIRM=DB-ONLY",
    )
    assert "--yes" in applied


def test_db_repair_gate_requires_target_to_still_be_current(
    tmp_path: Path,
) -> None:
    module_spec = importlib.util.spec_from_file_location(
        "verify_database_repair_gate",
        ROOT
        / ".agents/skills/ssh-server/scripts/verify_database_repair_gate.py",
    )
    assert module_spec and module_spec.loader
    module = importlib.util.module_from_spec(module_spec)
    sys.modules[module_spec.name] = module
    module_spec.loader.exec_module(module)
    release = "git-" + "a" * 40
    manifest = tmp_path / "manifest.json"
    manifest.write_text(json.dumps({"releaseId": release}))
    digest = hashlib.sha256(manifest.read_bytes()).hexdigest()
    gate = tmp_path / "gate.json"
    value = {
        "status": "success",
        "releaseId": release,
        "currentReleaseId": release,
        "manifestSha256": digest,
        "currentManifestSha256": digest,
    }
    gate.write_text(json.dumps(value))
    module.verify(gate, manifest, release)
    value["currentReleaseId"] = "git-" + "b" * 40
    gate.write_text(json.dumps(value))
    with pytest.raises(module.RepairGateError, match="concurrent rollout"):
        module.verify(gate, manifest, release)


@pytest.mark.parametrize(
    ("target", "message"),
    (
        ("ops-db-migration", "Usage: make ops-db-migration"),
        ("prod-fast-release", "REASON is required"),
    ),
)
def test_make_guards_required_mutation_inputs(
    target: str,
    message: str,
) -> None:
    completed = subprocess.run(
        ("make", target),
        cwd=ROOT,
        check=False,
        capture_output=True,
        text=True,
    )
    assert completed.returncode != 0
    assert message in completed.stdout + completed.stderr


@pytest.mark.parametrize(
    "script", ("massar.sh", "ops.sh", "database.sh", "deploy.sh")
)
def test_shell_helpers_have_valid_syntax_and_help(script: str) -> None:
    path = ROOT / ".agents/skills/ssh-server/scripts" / script
    subprocess.run(("bash", "-n", str(path)), check=True)
    completed = subprocess.run(
        ("bash", str(path), "--help"),
        check=True,
        capture_output=True,
        text=True,
    )
    assert "Usage:" in completed.stdout
