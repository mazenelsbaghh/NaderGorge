from __future__ import annotations

import gzip
import os
import subprocess
import sys
import tarfile
from pathlib import Path

import pytest


ROOT = Path(__file__).resolve().parents[3]
SCRIPTS = ROOT / "deploy/production/scripts"
sys.path.insert(0, str(SCRIPTS))

import release_images  # noqa: E402


def test_interrupted_release_retry_keeps_identical_bundle_despite_timestamps(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    # Regression: 2026-09-08 retry was rejected by an already-installed node.
    repo = tmp_path / "repo"
    script = repo / "deploy/production/scripts/start.sh"
    write(script, "#!/bin/sh\nexit 0\n")
    script.chmod(0o755)
    first = tmp_path / "first"
    retry = tmp_path / "retry"
    first.mkdir()
    retry.mkdir()
    monkeypatch.setattr(gzip.time, "time", lambda: 1000)
    first_archive = release_images.create_release_bundle(repo, first)

    os.utime(script, (2000, 2000))
    monkeypatch.setattr(gzip.time, "time", lambda: 3000)
    retry_archive = release_images.create_release_bundle(repo, retry)

    assert retry_archive.read_bytes() == first_archive.read_bytes()
    with tarfile.open(retry_archive) as bundle:
        member = bundle.getmember("deploy/production/scripts/start.sh")
        assert member.mode == 0o755
        assert bundle.extractfile(member).read() == b"#!/bin/sh\nexit 0\n"
    assert (retry / "release-files.tar.gz.sha256").read_text().strip() == (
        release_images.file_sha256(first_archive)
    )


def test_release_bundle_digest_changes_when_deployment_content_changes(
    tmp_path: Path,
) -> None:
    repo = tmp_path / "repo"
    config = repo / "deploy/production/compose/compose.app.yml"
    write(config, "services: {}\n")
    output = tmp_path / "output"
    output.mkdir()
    archive = release_images.create_release_bundle(repo, output)
    initial_digest = release_images.file_sha256(archive)

    write(config, "services: {backend: {image: changed}}\n")
    release_images.create_release_bundle(repo, output)

    assert release_images.file_sha256(archive) != initial_digest


def git(repo: Path, *arguments: str) -> str:
    return subprocess.check_output(
        ["git", *arguments],
        cwd=repo,
        text=True,
    ).strip()


def write(path: Path, value: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(value, encoding="utf-8")


@pytest.fixture
def complete_repository(tmp_path: Path) -> Path:
    git(tmp_path, "init", "-q")
    git(tmp_path, "config", "user.email", "release-tests@example.invalid")
    git(tmp_path, "config", "user.name", "Release Tests")
    write(tmp_path / "backend/app.cs", "class App {}\n")
    write(tmp_path / "README.md", "workspace documentation\n")
    write(tmp_path / "specs/feature/spec.md", "workspace specification\n")
    write(tmp_path / "Makefile", "verify:\n\t@true\n")
    write(tmp_path / ".gitignore", "node_modules/\nartifacts/\n")
    git(tmp_path, "add", ".")
    git(tmp_path, "commit", "-qm", "initial")
    return tmp_path


def test_complete_snapshot_contains_source_and_non_application_workspace_paths(
    complete_repository: Path,
    tmp_path: Path,
) -> None:
    write(complete_repository / "docs/untracked-runbook.md", "runbook\n")
    state = release_images.source_state(complete_repository)
    listed = [entry["path"] for entry in state["sourcePaths"]]

    assert listed == sorted(listed)
    assert "backend/app.cs" in listed
    assert "README.md" in listed
    assert "specs/feature/spec.md" in listed
    assert "Makefile" in listed
    assert "docs/untracked-runbook.md" in listed

    snapshot = tmp_path / "snapshot"
    release_images.create_source_snapshot(
        complete_repository,
        snapshot,
        state["sourceStateSha256"],
    )
    assert (snapshot / "README.md").read_text(encoding="utf-8") == (
        "workspace documentation\n"
    )
    assert (snapshot / "docs/untracked-runbook.md").is_file()


def test_release_snapshot_excludes_gitlink_directories(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    gitlink = tmp_path / "tooling/spec-kit"
    gitlink.mkdir(parents=True)
    manifest = {
        "entries": [
            {
                "path": "tooling/spec-kit",
                "status": "tracked",
                "classification": "tooling",
                "entryType": "gitlink",
                "gitlinkCommit": "a" * 40,
                "sizeBytes": None,
                "sha256": "b" * 64,
            },
        ],
    }
    monkeypatch.setattr(release_images, "build_manifest", lambda _repo: manifest)

    assert release_images.release_source_entries(tmp_path) == []


def test_release_source_digest_excludes_every_top_level_artifact_path(
    complete_repository: Path,
) -> None:
    relative = "artifacts/production/snapshots/tests/probe.cs"
    write(complete_repository / relative, "class FirstProbe {}\n")
    git(complete_repository, "add", "-f", relative)
    sealed = release_images.source_state(complete_repository)

    write(complete_repository / relative, "class ChangedProbe {}\n")
    current = release_images.source_state(complete_repository)

    assert relative not in {entry["path"] for entry in sealed["sourcePaths"]}
    assert current["sourceStateSha256"] == sealed["sourceStateSha256"]


@pytest.mark.parametrize(
    ("relative", "initial", "changed"),
    [
        ("README.md", "workspace documentation\n", "changed documentation\n"),
        ("docs/new-untracked.md", None, "new untracked source\n"),
    ],
)
def test_tracked_or_untracked_delta_outside_application_roots_changes_digest(
    complete_repository: Path,
    relative: str,
    initial: str | None,
    changed: str,
) -> None:
    if initial is not None:
        assert (complete_repository / relative).read_text(encoding="utf-8") == initial
    sealed = release_images.source_state(complete_repository)

    write(complete_repository / relative, changed)
    current = release_images.source_state(complete_repository)

    assert current["sourceStateSha256"] != sealed["sourceStateSha256"]
    assert current["releaseId"] != sealed["releaseId"]


def test_post_seal_workspace_delta_invalidates_candidate_reuse(
    complete_repository: Path,
) -> None:
    sealed = release_images.source_state(complete_repository)
    write(complete_repository / "specs/feature/late.md", "late delta\n")

    with pytest.raises(RuntimeError, match="candidate invalidated") as error:
        release_images.assert_source_unchanged(complete_repository, sealed)

    assert "specs/feature/late.md" in str(error.value)
