from __future__ import annotations

import importlib.util
import json
import os
import sys
from pathlib import Path

import pytest


ROOT = Path(__file__).resolve().parents[3]
SCRIPTS = ROOT / "deploy/production/scripts"


def load(name: str):
    spec = importlib.util.spec_from_file_location(name, SCRIPTS / f"{name}.py")
    assert spec and spec.loader
    module = importlib.util.module_from_spec(spec)
    sys.modules[name] = module
    spec.loader.exec_module(module)
    return module


load("remote_build_release")
builder = load("remote_builder_executor")


RELEASE = "src-" + "a" * 40


@pytest.fixture(autouse=True)
def root_owned_builder(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setattr(builder.os, "geteuid", lambda: 0)
    monkeypatch.setattr(builder.grp, "getgrnam", lambda _name: type("Group", (), {"gr_gid": 456})())
    monkeypatch.setattr(builder.os, "chown", lambda *_args: None)
    monkeypatch.setattr(builder, "patroni_role", lambda: "replica")


def workspace(monkeypatch: pytest.MonkeyPatch, tmp_path: Path) -> Path:
    root = tmp_path / "remote-builds"
    root.mkdir()
    target = root / RELEASE
    source = target / "source"
    for relative in (
        "backend/Dockerfile",
        "backend/Dockerfile.migrator",
        "frontend/Dockerfile",
        "worker/Dockerfile",
        "deploy/production/config.txt",
    ):
        path = source / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(relative + "\n")
    cluster = tmp_path / "cluster-id"
    node = tmp_path / "node-id"
    cluster.write_text("massar-production\n")
    node.write_text("node-3\n")
    monkeypatch.setattr(builder, "BUILD_ROOT", root)
    monkeypatch.setattr(builder, "CLUSTER_MARKER", cluster)
    monkeypatch.setattr(builder, "NODE_ID_MARKER", node)
    return target


def test_frontend_build_contract_embeds_the_immutable_release_id() -> None:
    _, dockerfile, arguments = builder._build_spec(Path("/source"), RELEASE)["frontend"]
    assert dockerfile == Path("/source/frontend/Dockerfile")
    assert f"NEXT_PUBLIC_RELEASE_ID={RELEASE}" in arguments
    frontend_dockerfile = (ROOT / "frontend/Dockerfile").read_text(encoding="utf-8")
    release_images_source = (SCRIPTS / "release_images.py").read_text(encoding="utf-8")
    assert "ARG NEXT_PUBLIC_RELEASE_ID" in frontend_dockerfile
    assert '"NEXT_PUBLIC_RELEASE_ID": release_id' in release_images_source


def test_remote_builder_builds_all_images_in_its_immutable_workspace(
    monkeypatch: pytest.MonkeyPatch,
    tmp_path: Path,
) -> None:
    target = workspace(monkeypatch, tmp_path)
    source_sha256 = builder.source_digest(target / "source")
    calls: list[list[str]] = []

    def fake_run(argv):
        value = list(argv)
        calls.append(value)
        if value[:3] == ["sudo", "/usr/bin/docker", "save"]:
            output = Path(value[value.index("--output") + 1])
            output.write_bytes((output.name + " image bytes").encode())

    def fake_command(argv):
        value = list(argv)
        if value[-1] == "{{.Architecture}}/{{.Os}}":
            return "amd64/linux"
        name = next(name for name in builder.IMAGES if f"/{name}:" in value[4])
        return f"sha256:{(builder.IMAGES.index(name) + 1):064x}"

    monkeypatch.setattr(builder, "run", fake_run)
    monkeypatch.setattr(builder, "command", fake_command)
    manifest = builder.execute(
        workspace=target, release_id=RELEASE, source_sha256=source_sha256
    )

    assert manifest["status"] == "success"
    assert manifest["builderNodeId"] == "node-3"
    assert set(manifest["images"]) == set(builder.IMAGES)
    assert all("--platform" in call and "linux/amd64" in call for call in calls[::2])
    assert all((target / "artifacts" / f"{name}.tar").is_file() for name in builder.IMAGES)
    evidence = json.loads((target / "build-evidence.json").read_text())
    assert evidence["manifestSha256"] == builder.sha256_file(target / "builder-manifest.json")


def test_remote_builder_refuses_wrong_node_before_any_container_command(
    monkeypatch: pytest.MonkeyPatch,
    tmp_path: Path,
) -> None:
    target = workspace(monkeypatch, tmp_path)
    builder.NODE_ID_MARKER.write_text("node-1\n")
    invoked = False

    def fake_run(argv):
        nonlocal invoked
        invoked = True

    monkeypatch.setattr(builder, "run", fake_run)
    with pytest.raises(builder.RemoteBuilderError, match="pinned to node-3"):
        builder.execute(
            workspace=target,
            release_id=RELEASE,
            source_sha256=builder.source_digest(target / "source"),
        )
    assert invoked is False


def test_remote_builder_refuses_source_digest_mismatch_before_any_container_command(
    monkeypatch: pytest.MonkeyPatch,
    tmp_path: Path,
) -> None:
    target = workspace(monkeypatch, tmp_path)
    invoked = False

    def fake_run(argv):
        nonlocal invoked
        invoked = True

    monkeypatch.setattr(builder, "run", fake_run)
    with pytest.raises(builder.RemoteBuilderError, match="source digest"):
        builder.execute(
            workspace=target, release_id=RELEASE, source_sha256="0" * 64
        )
    assert invoked is False


def test_remote_builder_refuses_postgresql_leader(
    monkeypatch: pytest.MonkeyPatch,
    tmp_path: Path,
) -> None:
    target = workspace(monkeypatch, tmp_path)
    with pytest.raises(builder.RemoteBuilderError, match="PostgreSQL leader"):
        builder.preflight(
            workspace=target,
            release_id=RELEASE,
            expected_source_sha256=builder.source_digest(target / "source"),
            patroni_role_reader=lambda: "leader",
        )


def test_remote_builder_allows_redis_master_when_postgresql_is_replica(
    monkeypatch: pytest.MonkeyPatch,
    tmp_path: Path,
) -> None:
    target = workspace(monkeypatch, tmp_path)

    assert builder.preflight(
        workspace=target,
        release_id=RELEASE,
        expected_source_sha256=builder.source_digest(target / "source"),
        patroni_role_reader=lambda: "replica",
    ) == target.resolve()


def test_remote_builder_reuses_a_matching_immutable_cache(
    monkeypatch: pytest.MonkeyPatch,
    tmp_path: Path,
) -> None:
    target = workspace(monkeypatch, tmp_path)
    source_sha256 = builder.source_digest(target / "source")

    def fake_run(argv):
        value = list(argv)
        if value[:3] == ["sudo", "/usr/bin/docker", "save"]:
            Path(value[value.index("--output") + 1]).write_bytes(b"image")

    monkeypatch.setattr(builder, "run", fake_run)
    monkeypatch.setattr(
        builder,
        "command",
        lambda argv: "amd64/linux" if list(argv)[-1] == "{{.Architecture}}/{{.Os}}" else "sha256:" + "a" * 64,
    )
    first = builder.execute(
        workspace=target, release_id=RELEASE, source_sha256=source_sha256
    )
    monkeypatch.setattr(builder, "run", lambda argv: pytest.fail("matching cache must not rebuild"))
    second = builder.execute(
        workspace=target, release_id=RELEASE, source_sha256=source_sha256
    )
    assert second == first


def test_matching_staged_retry_reuses_existing_immutable_source(
    monkeypatch: pytest.MonkeyPatch,
    tmp_path: Path,
) -> None:
    target = workspace(monkeypatch, tmp_path)
    expected = builder.source_digest(target / "source")
    staging = tmp_path / f"massar-build-source-{RELEASE}"
    staging.mkdir()
    staging.chmod(0o700)
    (staging / "retry-marker").write_text("discard me")
    monkeypatch.setattr(
        builder.pwd,
        "getpwnam",
        lambda _name: type("User", (), {"pw_uid": os.getuid()})(),
    )
    monkeypatch.setattr(builder, "Path", lambda _value: staging)

    builder.materialize_staged_source(
        workspace=target,
        release_id=RELEASE,
        expected_source_sha256=expected,
        staging=staging,
    )

    assert builder.source_digest(target / "source") == expected
    assert not staging.exists()


def test_staged_retry_refuses_to_replace_mismatched_immutable_source(
    monkeypatch: pytest.MonkeyPatch,
    tmp_path: Path,
) -> None:
    target = workspace(monkeypatch, tmp_path)
    staging = tmp_path / f"massar-build-source-{RELEASE}"
    staging.mkdir()
    staging.chmod(0o700)
    monkeypatch.setattr(
        builder.pwd,
        "getpwnam",
        lambda _name: type("User", (), {"pw_uid": os.getuid()})(),
    )
    monkeypatch.setattr(builder, "Path", lambda _value: staging)

    with pytest.raises(builder.RemoteBuilderError, match="does not match"):
        builder.materialize_staged_source(
            workspace=target,
            release_id=RELEASE,
            expected_source_sha256="0" * 64,
            staging=staging,
        )

    assert staging.exists()


def test_builder_command_requires_explicit_confirmation(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setattr(sys, "argv", ["remote_builder_executor.py", "--workspace", "/tmp/work", "--source-staging", f"/tmp/massar-build-source-{RELEASE}", "--release", RELEASE, "--source-sha256", "a" * 64])
    with pytest.raises(builder.RemoteBuilderError, match="requires --yes"):
        builder.main()


def test_remote_builder_is_self_contained_for_root_owned_installation() -> None:
    source = (SCRIPTS / "remote_builder_executor.py").read_text(encoding="utf-8")
    sudoers = (ROOT / "deploy/production/config/sudoers/massar-remote-builder").read_text(encoding="utf-8")
    installer = (SCRIPTS / "manage_backup_bucket.py").read_text(encoding="utf-8")
    assert "from remote_build_release" not in source
    assert sudoers.strip() == "massar-ops ALL=(root) NOPASSWD: /usr/local/sbin/massar-remote-builder"
    assert "remote_builder_executor.py /usr/local/sbin/massar-remote-builder" in installer


def test_remote_builder_cache_permissions_keep_source_private_and_artifacts_group_readable(
    tmp_path: Path,
) -> None:
    workspace_path = tmp_path / "workspace"
    source = workspace_path / "source"
    source.mkdir(parents=True)
    builder.secure_cache_layout(workspace_path)
    artifacts = workspace_path / "artifacts"
    artifacts.mkdir()
    archive = artifacts / "backend.tar"
    archive.write_bytes(b"image")
    builder.secure_relay_directory(artifacts, 456)
    builder.secure_relay_file(archive, 456)
    assert (workspace_path.stat().st_mode & 0o777) == 0o750
    assert (source.stat().st_mode & 0o777) == 0o700
    assert (artifacts.stat().st_mode & 0o777) == 0o750
    assert (archive.stat().st_mode & 0o777) == 0o640


def test_recovery_removes_only_stale_uuid_build_dirs_after_dead_lock(
    monkeypatch: pytest.MonkeyPatch,
    tmp_path: Path,
) -> None:
    stale = tmp_path / (".artifacts." + "a" * 32 + ".building")
    stale.mkdir()
    preserved = tmp_path / ".artifacts.not-a-uuid.building"
    preserved.mkdir()
    (tmp_path / builder.LOCK_NAME).write_text("777777\n")
    monkeypatch.setattr(builder.os, "kill", lambda _pid, _signal: (_ for _ in ()).throw(ProcessLookupError()))
    builder.recover_stale_builds(tmp_path)
    assert not stale.exists()
    assert preserved.exists()
    assert not (tmp_path / builder.LOCK_NAME).exists()


def test_recovery_refuses_to_remove_when_executor_lock_is_active(
    monkeypatch: pytest.MonkeyPatch,
    tmp_path: Path,
) -> None:
    stale = tmp_path / (".artifacts." + "b" * 32 + ".building")
    stale.mkdir()
    (tmp_path / builder.LOCK_NAME).write_text("42\n")
    monkeypatch.setattr(builder.os, "kill", lambda _pid, _signal: None)
    with pytest.raises(builder.RemoteBuilderError, match="already active"):
        builder.recover_stale_builds(tmp_path)
    assert stale.exists()


def test_failure_record_is_group_readable_without_world_access(tmp_path: Path) -> None:
    builder.write_failure_record(tmp_path, 456, builder.RemoteBuilderError("build failed"))
    record = tmp_path / "build-error.json"
    assert (record.stat().st_mode & 0o777) == 0o640
    assert json.loads(record.read_text())["reason"] == "build failed"
