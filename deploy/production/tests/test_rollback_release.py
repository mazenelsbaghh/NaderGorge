from __future__ import annotations

import argparse
import importlib.util
import subprocess
import sys
from dataclasses import dataclass
from pathlib import Path


ROOT = Path(__file__).resolve().parents[3]
SCRIPTS = ROOT / "deploy/production/scripts"
sys.path.insert(0, str(SCRIPTS))


def load(alias: str, filename: str):
    spec = importlib.util.spec_from_file_location(alias, SCRIPTS / filename)
    assert spec and spec.loader
    module = importlib.util.module_from_spec(spec)
    sys.modules[alias] = module
    spec.loader.exec_module(module)
    return module


deploy = load("deploy_release_retained_schema_tests", "deploy_release.py")
rollback = load("rollback_release_retained_schema_tests", "rollback_release.py")


@dataclass
class Result:
    returncode: int = 0
    stdout: str = "git-" + "d" * 40
    stderr: str = ""


class FakeTransport:
    def __init__(self) -> None:
        self.commands: list[tuple[object, ...]] = []

    def run(self, _target, command, **_kwargs):
        self.commands.append(command)
        return Result()


def test_prior_app_is_smoked_against_exact_retained_schema() -> None:
    transport = FakeTransport()
    node = type(
        "Node",
        (),
        {
            "id": "node-2",
            "public_address": "192.0.2.2",
            "overlay_address": "10.77.0.12",
        },
    )()
    deploy.recover_node(
        transport,
        deploy.SshTarget("node-2", "192.0.2.2", "massar-ops"),
        node,
        "git-" + "a" * 40,
        deploy.RetainedSchema(
            "7586552109940137719",
            "b" * 64,
            "c" * 64,
        ),
    )
    script = transport.commands[0][-1]
    assert 'stage="verify-prior-app-against-retained-schema"' in script
    assert "pg_control_system" in script
    assert "__EFMigrationsHistory" in script
    assert "pg_dump" in script
    assert "b" * 64 in script
    assert "c" * 64 in script
    for forbidden in (
        "dotnet ef database update 0",
        "pg_restore",
        "restore_database.py",
        "restore_database_sample.sh",
    ):
        assert forbidden not in script
    assert subprocess.run(
        ["bash", "-n"],
        input=script,
        text=True,
        capture_output=True,
        check=False,
    ).returncode == 0


def test_rollback_command_is_application_only_and_retains_schema() -> None:
    args = argparse.Namespace(
        inventory=Path("/inventory.yml"),
        known_hosts=Path("/known-hosts"),
        identity=Path("/identity"),
        release="git-" + "d" * 40,
        manifest=Path("/prior-manifest.json"),
        current_manifest=Path("/current-manifest.json"),
        compatibility_evidence=Path("/compatibility.json"),
        dry_run=True,
    )
    command = rollback.build_deploy_command(args)
    assert "--rollback-current-manifest" in command
    assert "--rollback-evidence" in command
    assert "--backup-evidence" not in command
    assert "--database-restore" not in command
    assert "--down-migration" not in command
    assert rollback.ROLLBACK_POLICY == {
        "applicationOnly": True,
        "databaseAction": "retain-compatible-schema",
        "priorApplicationSmokeRequired": True,
        "forwardFixEvidenceRequiredOnSchemaDefect": True,
    }


def test_migration_failure_policy_is_forward_fix_only() -> None:
    source = (SCRIPTS / "migrate_release.py").read_text(encoding="utf-8")
    assert '"rollbackDatabaseAction": "prohibited"' in source
    assert '"schemaFailureDisposition": "reviewed-forward-fix-only"' in source
    assert "automatic database " in source
    assert "Down/restore is prohibited" in source
    assert "reviewed forward-only corrective" in source
