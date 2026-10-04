"""The exact-candidate lane must preserve EF coverage and reject mixed history."""
import importlib.util
import os
from pathlib import Path
import subprocess
import sys

import pytest

ROOT = Path(__file__).resolve().parents[3]
SPEC = importlib.util.spec_from_file_location(
    "committed_release_plan", ROOT / ".agents/skills/ssh-server/scripts/release_plan.py")
planner = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = planner
SPEC.loader.exec_module(planner)


@pytest.fixture
def repository(tmp_path, monkeypatch):
    # Synthetic bare Git objects only: no project clone or development checkout.
    subprocess.run(["git", "init", "--bare", "-q", str(tmp_path)], check=True)
    monkeypatch.setattr(planner, "ROOT", tmp_path)
    return tmp_path


def commit(repo, files, parent=None):
    env = {"PATH": os.environ["PATH"], "GIT_CONFIG_GLOBAL": os.devnull, "GIT_CONFIG_NOSYSTEM": "1",
           "GIT_AUTHOR_NAME": "Fixture", "GIT_AUTHOR_EMAIL": "fixture@localhost",
           "GIT_COMMITTER_NAME": "Fixture", "GIT_COMMITTER_EMAIL": "fixture@localhost"}

    def git(*args, content=None):
        return subprocess.run(["git", "-C", str(repo), *args], input=content,
                              text=True, capture_output=True, check=True, env=env).stdout.strip()

    git("read-tree", parent if parent else "--empty")
    for path, content in files.items():
        if content is None:
            git("update-index", "--index-info", content=f"0 {'0' * 40}\t{path}\n")
            continue
        blob = git("hash-object", "-w", "--stdin", content=content)
        git("update-index", "--add", "--cacheinfo", "100644", blob, path)
    tree = git("write-tree")
    return git("commit-tree", tree, *(["-p", parent] if parent else []), content="Fixture\n")


def test_candidate_delta_does_not_include_unrelated_local_history(repository):
    model = "backend/src/NaderGorge.Domain/Entities/Lesson.cs"
    base = commit(repository, {model: "original"})
    candidate = commit(repository, {"backend/src/NaderGorge.Application/Features/Homework/HomeworkReadiness.cs": "fix"}, base)
    paths = planner.committed_changed_paths(base, candidate)
    plan = planner.classify(base, paths, lambda p: planner.git_succeeds("cat-file", "-e", f"{base}:{p}"))
    assert plan.components == ("backend",)
    assert not plan.database_changed


@pytest.mark.parametrize("migration", [False, True])
def test_committed_schema_change_still_requires_new_migration_pair_and_snapshot(repository, migration):
    model = "backend/src/NaderGorge.Domain/Entities/Lesson.cs"
    base = commit(repository, {model: "original"})
    files = {model: "changed"}
    if migration:
        prefix = "backend/src/NaderGorge.Infrastructure/Migrations/"
        files.update({prefix + "202610040001_Fixture.cs": "migration",
                      prefix + "202610040001_Fixture.Designer.cs": "designer",
                      prefix + "AppDbContextModelSnapshot.cs": "snapshot"})
    candidate = commit(repository, files, base)
    paths = planner.committed_changed_paths(base, candidate)
    plan = planner.classify(base, paths, lambda p: planner.git_succeeds("cat-file", "-e", f"{base}:{p}"))
    assert plan.database_changed
    assert plan.migration_required is not migration


def test_candidate_cannot_hide_intermediate_unreviewed_commit(repository):
    base = commit(repository, {"backend/original.cs": "base"})
    middle = commit(repository, {"backend/other.cs": "other"}, base)
    candidate = commit(repository, {"backend/fix.cs": "fix"}, middle)
    with pytest.raises(planner.PlanError, match="one forward commit"):
        planner.committed_changed_paths(base, candidate)


@pytest.mark.parametrize("relocated", [False, True])
def test_schema_deletion_or_move_out_of_schema_directory_still_blocks(repository, relocated):
    model = "backend/src/NaderGorge.Domain/Entities/Lesson.cs"
    base = commit(repository, {model: "original"})
    files = {model: None}
    if relocated:
        files["backend/src/NaderGorge.Application/MovedLesson.cs"] = "original"
    candidate = commit(repository, files, base)
    plan = planner.classify(base, planner.committed_changed_paths(base, candidate))
    assert plan.migration_required


def test_candidate_requires_explicit_full_source_identities(repository):
    base = commit(repository, {"backend/original.cs": "base"})
    candidate = commit(repository, {"backend/fix.cs": "fix"}, base)
    with pytest.raises(planner.PlanError, match="full Git commit SHAs"):
        planner.committed_changed_paths(base[:8], candidate)


def test_normal_working_tree_guard_keeps_all_four_sources(monkeypatch):
    replies = iter(["base", "backend/history.cs\n", "backend/unstaged.cs\n",
                    "backend/staged.cs\n", "backend/untracked.cs\n"])
    monkeypatch.setattr(planner, "git", lambda *args: next(replies))
    assert set(planner.changed_paths("base")) == {
        "backend/history.cs", "backend/staged.cs", "backend/untracked.cs", "backend/unstaged.cs"}
