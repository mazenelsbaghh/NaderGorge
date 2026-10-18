from __future__ import annotations

import datetime as dt
import importlib.util
import json
import sys
from pathlib import Path

import pytest

SCRIPTS = Path(__file__).resolve().parents[1] / "scripts"
spec = importlib.util.spec_from_file_location("scheduled_image_cleanup", SCRIPTS / "scheduled_image_cleanup.py")
assert spec and spec.loader
cleanup = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = cleanup
spec.loader.exec_module(cleanup)

CURRENT = "git-" + "a" * 40
ROLLBACK = "git-" + "b" * 40
OLD = "git-" + "c" * 40
CONFIGURATION = {"repositoryPrefixes": ["massar", "registry.test:5443/massar"]}


def image(identifier: int, *, repository: str = "registry.test:5443/massar/backend",
          tag: str = OLD, age_days: int = 14) -> dict:
    created = dt.datetime.now(dt.timezone.utc) - dt.timedelta(days=age_days)
    digest = f"sha256:{identifier:064x}"
    return {"ID": digest, "Repository": repository, "Tag": tag, "Digest": digest,
            "Containers": "0", "CreatedAt": created.strftime("%Y-%m-%d %H:%M:%S %z UTC")}


def test_cleanup_preserves_release_digests_across_aliases_and_every_container_image(tmp_path, monkeypatch):
    monkeypatch.setattr(cleanup, "BUILDS", tmp_path)
    rows = [image(1), image(2), image(3), image(4)]
    retention = (CURRENT, {CURRENT, ROLLBACK}, {rows[0]["ID"], rows[1]["ID"]})

    plan = cleanup.cleanup_plan(rows, retention, CONFIGURATION, {rows[2]["ID"]})

    assert plan["references"] == {cleanup.image_reference(rows[3]): rows[3]["ID"]}
    assert plan["protectedReleaseIds"] == sorted([CURRENT, ROLLBACK])


@pytest.mark.parametrize("changes", [
    {"age_days": 1}, {"tag": "verified-repair"}, {"repository": "postgres"},
    {"tag": CURRENT}, {"tag": ROLLBACK},
])
def test_unrecognized_recent_and_retained_references_protect_all_aliases(tmp_path, monkeypatch, changes):
    monkeypatch.setattr(cleanup, "BUILDS", tmp_path)
    protected = image(1, **changes)
    old_alias = image(1, tag="git-" + "d" * 40)

    plan = cleanup.cleanup_plan([protected, old_alias], (CURRENT, {CURRENT, ROLLBACK}, set()), CONFIGURATION, set())

    assert not plan["references"]


def test_untagged_registry_image_can_be_removed_but_pending_build_is_protected(tmp_path, monkeypatch):
    monkeypatch.setattr(cleanup, "BUILDS", tmp_path)
    pending_release = "git-" + "d" * 40
    (tmp_path / pending_release).mkdir()
    orphan = image(1, tag="<none>")
    pending = image(2, tag=pending_release)

    plan = cleanup.cleanup_plan([orphan, pending], (CURRENT, {CURRENT, ROLLBACK}, set()), CONFIGURATION, set())

    assert plan["references"] == {cleanup.image_reference(orphan): orphan["ID"]}


def test_retention_preserves_every_manifest_and_refuses_a_missing_rollback_root(tmp_path, monkeypatch):
    releases = tmp_path / "releases"
    releases.mkdir()
    monkeypatch.setattr(cleanup, "RELEASES", releases)
    pointer = tmp_path / "current"
    monkeypatch.setattr(cleanup, "CURRENT", pointer)
    for release, number in ((CURRENT, 1), (ROLLBACK, 2)):
        root = releases / release
        root.mkdir()
        (root / "manifest.json").write_text(json.dumps({"releaseId": release, "status": "success",
            "images": {component: f"sha256:{number:064x}" for component in cleanup.COMPONENTS}}))
    pointer.symlink_to(releases / CURRENT)

    assert cleanup.retained_releases() == (CURRENT, {CURRENT, ROLLBACK}, {f"sha256:{n:064x}" for n in (1, 2)})
    (releases / ROLLBACK / "manifest.json").unlink()
    (releases / ROLLBACK).rmdir()
    with pytest.raises(cleanup.CleanupError, match="rollback"):
        cleanup.retained_releases()


def test_symlinked_retained_manifest_cannot_redirect_retention(tmp_path, monkeypatch):
    target = tmp_path / "external.json"
    target.write_text('{}')
    link = tmp_path / "manifest.json"
    link.symlink_to(target)

    with pytest.raises(cleanup.CleanupError, match="unsafe"):
        cleanup.read_json(link)


def test_seven_day_boundary_keeps_images_exactly_at_cutoff():
    cutoff = dt.datetime(2026, 10, 1, tzinfo=dt.timezone.utc)
    candidate = image(1)
    candidate["CreatedAt"] = "2026-10-01 00:00:00 +0000 UTC"
    repositories = {candidate["Repository"]}

    assert not cleanup.eligible_image(candidate, repositories, cutoff)
    candidate["CreatedAt"] = "2026-09-30 23:59:59 +0000 UTC"
    assert cleanup.eligible_image(candidate, repositories, cutoff)


def test_another_cleanup_cannot_enter_the_same_filesystem_lock(tmp_path):
    lock = tmp_path / "cluster-cleanup.lock"
    with cleanup.exclusive_lock(lock):
        with pytest.raises(cleanup.CleanupBusy):
            with cleanup.exclusive_lock(lock):
                pytest.fail("Concurrent image deletion entered the lock")
    with cleanup.exclusive_lock(lock):
        assert lock.is_file()


def test_active_build_prevents_image_mutation(tmp_path, monkeypatch):
    monkeypatch.setattr(cleanup, "BUILDS", tmp_path)
    pending = tmp_path / OLD
    pending.mkdir()
    (pending / ".remote-builder.lock").write_text("12345\n")

    with pytest.raises(cleanup.CleanupBusy, match="build or deployment"):
        cleanup.apply_plan({"references": {"registry.test:5443/massar/backend:" + OLD: "unused"}}, CONFIGURATION)
