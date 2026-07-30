from __future__ import annotations

import hashlib
import importlib.util
import io
import json
import os
import sys
import tarfile
from pathlib import Path

import pytest


ROOT = Path(__file__).resolve().parents[3]
SCRIPT = ROOT / "deploy/production/scripts/install_immutable_release.py"
spec = importlib.util.spec_from_file_location("install_immutable_release", SCRIPT)
assert spec and spec.loader
installer = importlib.util.module_from_spec(spec)
sys.modules["install_immutable_release"] = installer
spec.loader.exec_module(installer)

RELEASE = "src-" + "a" * 40


def sha256(content: bytes) -> str:
    return hashlib.sha256(content).hexdigest()


def manifest(*, final: bool = False) -> bytes:
    value = {
        "schemaVersion": 1,
        "status": "success",
        "releaseId": RELEASE,
        "images": {
            name: f"sha256:{index:064x}"
            for index, name in enumerate(
                ("backend", "frontend", "worker", "migrator"), 1
            )
        },
        "nodeCount": 3,
        "digestParity": final,
    }
    return (json.dumps(value, sort_keys=True) + "\n").encode()


def bundle(member_name: str = "deploy/production/compose/compose.app.yml") -> bytes:
    output = io.BytesIO()
    with tarfile.open(fileobj=output, mode="w:gz") as archive:
        content = b"services: {}\n"
        item = tarfile.TarInfo(member_name)
        item.size = len(content)
        item.mode = 0o644
        archive.addfile(item, io.BytesIO(content))
    return output.getvalue()


@pytest.fixture
def release_environment(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> Path:
    base = tmp_path / "opt/massar/releases"
    base.mkdir(parents=True)
    marker = tmp_path / "cluster-id"
    marker.write_text("massar-production\n")
    incoming = tmp_path / "incoming"
    incoming.mkdir()
    monkeypatch.setattr(installer, "BASE", base)
    monkeypatch.setattr(installer, "INCOMING", incoming)
    monkeypatch.setattr(installer, "CLUSTER_MARKER", marker)
    monkeypatch.setattr(installer, "LOCK_FILE", tmp_path / "run/install.lock")
    monkeypatch.setattr(installer.os, "geteuid", lambda: 0)
    monkeypatch.setattr(installer, "operator_uid", os.getuid)
    return tmp_path


def write_incoming(tmp_path: Path, bundle_content: bytes, manifest_content: bytes) -> None:
    incoming = tmp_path / "incoming" / f"massar-{RELEASE}"
    incoming.mkdir()
    (incoming / "release-files.tar.gz").write_bytes(bundle_content)
    (incoming / "manifest.json").write_bytes(manifest_content)


def test_install_is_digest_bound_atomic_and_refuses_existing_root(
    release_environment: Path,
) -> None:
    bundle_content = bundle()
    manifest_content = manifest()
    write_incoming(release_environment, bundle_content, manifest_content)

    result = installer.install_release(
        RELEASE, sha256(bundle_content), sha256(manifest_content)
    )

    root = installer.BASE / RELEASE
    assert result["status"] == "installed"
    assert (root / "deploy/production/compose/compose.app.yml").is_file()
    assert (root / "manifest.json").read_bytes() == manifest_content
    assert (root / ".initial-manifest.sha256").read_text().strip() == sha256(
        manifest_content
    )
    assert not (installer.BASE / f".{RELEASE}.staging").exists()
    with pytest.raises(installer.ReleaseInstallError, match="already exists"):
        installer.install_release(
            RELEASE, sha256(bundle_content), sha256(manifest_content)
        )


@pytest.mark.parametrize(
    "member_name",
    ("../../etc/passwd", "deploy/production/../../escape"),
)
def test_install_rejects_traversal_and_cleans_staging(
    release_environment: Path,
    member_name: str,
) -> None:
    bundle_content = bundle(member_name)
    manifest_content = manifest()
    write_incoming(release_environment, bundle_content, manifest_content)

    with pytest.raises(installer.ReleaseInstallError, match="invalid path"):
        installer.install_release(
            RELEASE, sha256(bundle_content), sha256(manifest_content)
        )

    assert not (installer.BASE / RELEASE).exists()
    assert not (installer.BASE / f".{RELEASE}.staging").exists()


def test_install_rejects_digest_mismatch_before_staging(
    release_environment: Path,
) -> None:
    bundle_content = bundle()
    manifest_content = manifest()
    write_incoming(release_environment, bundle_content, manifest_content)

    with pytest.raises(installer.ReleaseInstallError, match="bundle digest"):
        installer.install_release(RELEASE, "0" * 64, sha256(manifest_content))

    assert not (installer.BASE / f".{RELEASE}.staging").exists()


def test_final_manifest_only_transitions_from_bound_initial_manifest(
    release_environment: Path,
) -> None:
    bundle_content = bundle()
    initial = manifest()
    write_incoming(release_environment, bundle_content, initial)
    installer.install_release(RELEASE, sha256(bundle_content), sha256(initial))
    final = manifest(final=True)
    final_path = release_environment / "incoming" / f"massar-{RELEASE}-manifest.json"
    final_path.write_bytes(final)

    result = installer.publish_final_manifest(RELEASE, sha256(final))

    assert result["status"] == "published"
    assert (installer.BASE / RELEASE / "manifest.json").read_bytes() == final
    assert installer.publish_final_manifest(RELEASE, sha256(final))["status"] == "verified"

    (installer.BASE / RELEASE / "manifest.json").write_bytes(b"tampered")
    with pytest.raises(installer.ReleaseInstallError, match="neither initial"):
        installer.publish_final_manifest(RELEASE, sha256(final))


def test_release_workflow_uses_narrow_helper_without_broad_root_file_commands() -> None:
    release_source = (
        ROOT / "deploy/production/scripts/release_images.py"
    ).read_text(encoding="utf-8")
    sync_source = (
        ROOT / "deploy/production/scripts/manage_backup_bucket.py"
    ).read_text(encoding="utf-8")
    sudoers = (
        ROOT
        / "deploy/production/config/sudoers/massar-immutable-release-install"
    ).read_text(encoding="utf-8")

    assert "massar-install-immutable-release" in release_source
    assert "publish-final-manifest" in release_source
    assert "sudo rm " not in release_source
    assert "sudo tar " not in release_source
    assert "sudo mv " not in release_source
    assert "install_immutable_release.py" in sync_source
    assert sudoers.strip() == (
        "massar-ops ALL=(root) NOPASSWD: "
        "/usr/local/sbin/massar-install-immutable-release *"
    )
