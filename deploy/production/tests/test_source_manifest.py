from __future__ import annotations

import json
import subprocess
import sys
from pathlib import Path

import pytest


ROOT = Path(__file__).resolve().parents[3]
SCRIPTS = ROOT / "deploy/production/scripts"
sys.path.insert(0, str(SCRIPTS))

import source_manifest  # noqa: E402


def git(repo: Path, *args: str) -> str:
    return subprocess.check_output(
        ["git", *args],
        cwd=repo,
        text=True,
    ).strip()


def write(path: Path, value: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(value, encoding="utf-8")


@pytest.fixture
def repository(tmp_path: Path) -> Path:
    git(tmp_path, "init", "-q")
    git(tmp_path, "config", "user.email", "manifest-tests@example.invalid")
    git(tmp_path, "config", "user.name", "Manifest Tests")
    write(tmp_path / "src/app.py", "print('initial')\n")
    write(tmp_path / "docs/readme.md", "initial docs\n")
    write(tmp_path / ".gitignore", "__pycache__/\n*.pyc\n")
    git(tmp_path, "add", ".")
    git(tmp_path, "commit", "-qm", "initial")
    return tmp_path


def entry_by_path(manifest: dict[str, object], path: str) -> dict[str, object]:
    entries = manifest["entries"]
    assert isinstance(entries, list)
    return next(entry for entry in entries if entry["path"] == path)


def test_inventory_lists_actual_untracked_files_and_tracked_changes(
    repository: Path,
) -> None:
    write(repository / "src/app.py", "print('modified')\n")
    (repository / "docs/readme.md").unlink()
    write(repository / "new/deep/first.txt", "first\n")
    write(repository / "new/deep/second.txt", "second\n")

    manifest = source_manifest.build_manifest(repository)

    assert [entry["path"] for entry in manifest["entries"]] == [
        ".gitignore",
        "docs/readme.md",
        "new/deep/first.txt",
        "new/deep/second.txt",
        "src/app.py",
    ]
    assert entry_by_path(manifest, "src/app.py")["status"] == "modified"
    assert entry_by_path(manifest, "docs/readme.md") == {
        "path": "docs/readme.md",
        "status": "deleted",
        "classification": "documentation",
        "sizeBytes": None,
        "sha256": None,
        "previousSha256": source_manifest.sha256_bytes(b"initial docs\n"),
    }
    assert entry_by_path(manifest, "new/deep/first.txt")["status"] == "untracked"
    assert manifest["counts"] == {
        "deleted": 1,
        "modified": 1,
        "tracked": 1,
        "untracked": 2,
        "total": 5,
    }


def test_workspace_digest_and_file_hashes_are_deterministic(
    repository: Path,
) -> None:
    write(repository / "frontend/src/view.tsx", "export const view = 1;\n")
    write(repository / "backend/src/Service.cs", "class Service {}\n")

    first = source_manifest.build_manifest(repository)
    second = source_manifest.build_manifest(repository)

    assert first["workspaceDigest"] == second["workspaceDigest"]
    assert first["entries"] == second["entries"]
    assert len(str(first["workspaceDigest"])) == 64
    assert entry_by_path(first, "frontend/src/view.tsx")["classification"] == "source"
    assert entry_by_path(first, "backend/src/Service.cs")["classification"] == "source"
    assert len(str(entry_by_path(first, "frontend/src/view.tsx")["sha256"])) == 64


def test_secret_content_fails_closed_without_echoing_secret(
    repository: Path,
) -> None:
    secret = "AKIA" + "ABCDEFGHIJKLMNOP"
    write(repository / "src/config.py", f'cloud_key = "{secret}"\n')

    with pytest.raises(source_manifest.ManifestSafetyError) as error:
        source_manifest.build_manifest(repository)

    message = str(error.value)
    assert secret not in message
    assert "src/config.py" in message
    assert "aws-access-key" in message


def test_android_firebase_client_config_allows_only_public_google_key(
    repository: Path,
) -> None:
    public_key = "AIza" + "A" * 35
    config = repository / "mobile/parent-android/app/google-services.json"
    write(config, json.dumps({"client": [{"api_key": public_key}]}) + "\n")

    manifest = source_manifest.build_manifest(repository)

    assert entry_by_path(
        manifest,
        "mobile/parent-android/app/google-services.json",
    )["status"] == "untracked"


def test_android_firebase_client_config_still_blocks_private_keys(
    repository: Path,
) -> None:
    private_key = "-----BEGIN " + "OPENSSH PRIVATE KEY-----"
    config = repository / "mobile/parent-android/app/google-services.json"
    write(config, json.dumps({"private_key": private_key}) + "\n")

    with pytest.raises(source_manifest.ManifestSafetyError) as error:
        source_manifest.build_manifest(repository)

    assert "private-key" in str(error.value)
    assert private_key not in str(error.value)


def test_large_artifact_is_hashed_but_not_text_scanned(
    repository: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    monkeypatch.setattr(source_manifest, "MAX_SECRET_SCAN_BYTES", 64)
    artifact = repository / "artifacts/release/workspace-manifest.json"
    write(artifact, '{"generated":"' + "x" * 80 + '"}\n')

    manifest = source_manifest.build_manifest(repository)

    entry = entry_by_path(
        manifest,
        "artifacts/release/workspace-manifest.json",
    )
    assert entry["classification"] == "artifact"
    assert entry["sha256"] == source_manifest.sha256_file(artifact)


def test_top_level_artifact_stays_artifact_when_nested_path_looks_like_test_source(
    repository: Path,
) -> None:
    relative = "artifacts/production/snapshots/tests/probe.cs"
    write(repository / relative, "class DiagnosticProbe {}\n")

    manifest = source_manifest.build_manifest(repository)

    assert entry_by_path(manifest, relative)["classification"] == "artifact"


def test_large_source_still_fails_closed(
    repository: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    monkeypatch.setattr(source_manifest, "MAX_SECRET_SCAN_BYTES", 64)
    write(repository / "src/large.txt", "x" * 80)

    with pytest.raises(source_manifest.ManifestSafetyError, match="safe secret-scan"):
        source_manifest.build_manifest(repository)


@pytest.mark.parametrize(
    "relative",
    (
        ".env.production",
        "ops/secrets/database-password",
        "certificates/origin-private.key",
    ),
)
def test_sensitive_path_fails_closed(
    repository: Path,
    relative: str,
) -> None:
    write(repository / relative, "not-printed-sensitive-material\n")
    git(repository, "add", "-f", relative)

    with pytest.raises(source_manifest.ManifestSafetyError) as error:
        source_manifest.build_manifest(repository)

    assert relative in str(error.value)
    assert "not-printed-sensitive-material" not in str(error.value)


def test_verify_rejects_any_post_seal_delta(repository: Path, tmp_path: Path) -> None:
    write(repository / "src/app.py", "print('sealed')\n")
    output = tmp_path / "workspace-manifest.json"
    source_manifest.seal_manifest(repository, output)

    source_manifest.verify_manifest(repository, output)
    write(repository / "late/change.txt", "late\n")

    with pytest.raises(source_manifest.WorkspaceDeltaError) as error:
        source_manifest.verify_manifest(repository, output)

    assert "late/change.txt" in str(error.value)
    assert "workspace changed after seal" in str(error.value)


def test_manifest_output_inside_repository_does_not_invalidate_itself(
    repository: Path,
) -> None:
    write(repository / "src/app.py", "print('sealed')\n")
    output = repository / "artifacts/performance-167/baseline/workspace-manifest.json"

    sealed = source_manifest.seal_manifest(repository, output)
    verified = source_manifest.verify_manifest(repository, output)

    assert sealed["workspaceDigest"] == verified["workspaceDigest"]
    assert sealed["excludedPaths"] == [
        "artifacts/performance-167/baseline/workspace-manifest.json"
    ]
    loaded = json.loads(output.read_text(encoding="utf-8"))
    assert loaded["secretAudit"]["status"] == "passed"


def test_clean_gitlink_is_inventory_evidence_not_a_regular_file(
    repository: Path,
) -> None:
    gitlink = repository / "tooling/spec-kit"
    gitlink.mkdir(parents=True)
    git(gitlink, "init", "-q")
    git(gitlink, "config", "user.email", "gitlink-tests@example.invalid")
    git(gitlink, "config", "user.name", "Gitlink Tests")
    write(gitlink / "README.md", "pinned tooling\n")
    git(gitlink, "add", "README.md")
    git(gitlink, "commit", "-qm", "pin tooling")
    current_commit = git(gitlink, "rev-parse", "HEAD")
    git(repository, "add", "tooling/spec-kit")

    manifest = source_manifest.build_manifest(repository)

    entry = entry_by_path(manifest, "tooling/spec-kit")
    assert entry["entryType"] == "gitlink"
    assert entry["gitlinkCommit"] == current_commit
    assert entry["sha256"] == source_manifest.sha256_bytes(
        current_commit.encode("ascii")
    )


def test_dirty_gitlink_fails_closed(repository: Path) -> None:
    gitlink = repository / "tooling/spec-kit"
    gitlink.mkdir(parents=True)
    git(gitlink, "init", "-q")
    git(gitlink, "config", "user.email", "gitlink-tests@example.invalid")
    git(gitlink, "config", "user.name", "Gitlink Tests")
    write(gitlink / "README.md", "pinned tooling\n")
    git(gitlink, "add", "README.md")
    git(gitlink, "commit", "-qm", "pin tooling")
    git(repository, "add", "tooling/spec-kit")
    write(gitlink / "late.txt", "unreviewed nested change\n")

    with pytest.raises(source_manifest.ManifestSafetyError) as error:
        source_manifest.build_manifest(repository)

    assert "tooling/spec-kit: dirty-gitlink" in str(error.value)


def test_verify_detects_changed_and_removed_files(repository: Path) -> None:
    write(repository / "first.txt", "one\n")
    write(repository / "second.txt", "two\n")
    output = repository / "manifest.json"
    source_manifest.seal_manifest(repository, output)

    write(repository / "first.txt", "changed\n")
    (repository / "second.txt").unlink()

    with pytest.raises(source_manifest.WorkspaceDeltaError) as error:
        source_manifest.verify_manifest(repository, output)

    message = str(error.value)
    assert "first.txt" in message
    assert "second.txt" in message
