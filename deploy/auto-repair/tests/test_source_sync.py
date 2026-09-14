"""Real Git remotes exercise preservation, stale publications and private integration."""
import subprocess
import sys
from pathlib import Path
from types import SimpleNamespace

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / 'production/scripts'))
import source_sync as sync


def run(repo, *args):
    return subprocess.check_output(['git', '-C', str(repo), *args], text=True).strip()


def commit(repo, name, content):
    (repo / name).write_text(content)
    run(repo, 'add', name)
    run(repo, '-c', 'user.name=Test', '-c', 'user.email=test@localhost', 'commit', '-qm', name)
    return run(repo, 'rev-parse', 'HEAD')


@pytest.fixture
def repositories(tmp_path, monkeypatch):
    remote = tmp_path / 'remote.git'; remote.mkdir(); run(remote, 'init', '--bare', '-q')
    local = tmp_path / 'local'; local.mkdir(); run(local, 'init', '-q')
    parent = commit(local, 'README.md', 'base\n')
    run(local, 'push', str(remote), 'HEAD:' + sync.REF)
    monkeypatch.setattr(sync, 'URL', str(remote))
    monkeypatch.setattr(sync, 'CONFIG', tmp_path / 'absent-config')
    return local, remote, parent


def test_verified_candidate_is_preserved_and_retry_is_idempotent(repositories):
    local, remote, parent = repositories
    candidate = commit(local, 'README.md', 'repair\n')
    assert sync.publish(local, parent, 'codex/repair/test') == candidate
    assert sync.publish(local, parent, 'codex/repair/test') == candidate
    assert run(remote, 'rev-parse', sync.REF) == candidate
    assert run(remote, 'rev-parse', 'refs/heads/codex/repair/test') == candidate
    sync.assert_published(local, SimpleNamespace(release_id='git-' + candidate, git_commit=candidate))


def test_stale_operator_cannot_erase_a_server_fix(repositories, tmp_path):
    local, remote, parent = repositories
    other = tmp_path / 'other'; run(tmp_path, 'clone', '-q', str(local), str(other))
    server = commit(local, 'server.md', 'server fix\n')
    sync.publish(local, parent, 'codex/repair/server')
    stale = commit(other, 'local.md', 'local work\n')
    with pytest.raises(sync.SourceSyncError, match='changed'):
        sync.publish(other, parent, 'codex/release/stale')
    assert run(remote, 'rev-parse', sync.REF) == server
    with pytest.raises(sync.SourceSyncError, match='advanced'):
        sync.assert_published(other, SimpleNamespace(release_id='git-' + stale, git_commit=stale))


def test_dirty_local_work_is_preserved_and_conflicts_stay_in_isolated_workspace(repositories, tmp_path):
    local, remote, parent = repositories
    other = tmp_path / 'other'; run(tmp_path, 'clone', '-q', str(local), str(other))
    commit(other, 'README.md', 'server change\n')
    sync.publish(other, parent, 'codex/repair/server')
    (local / 'README.md').write_text('unsaved local change\n')
    original_index = (local / '.git/index').read_bytes()
    response = sync.integrate(local, tmp_path / 'integration')
    assert response['status'] == 'conflict'
    assert response['conflicts'] == ['README.md']
    assert (local / 'README.md').read_text() == 'unsaved local change\n'
    assert (local / '.git/index').read_bytes() == original_index
    assert run(local, 'rev-parse', 'HEAD') == parent


def test_export_keeps_private_history_out_of_shared_source(repositories, tmp_path):
    local, remote, parent = repositories
    commit(local, 'private-history.md', 'historical local content\n')
    run(local, 'rm', 'private-history.md')
    run(local, '-c', 'user.name=Test', '-c', 'user.email=test@localhost', 'commit', '-qm', 'remove private file')
    commit(local, 'README.md', 'integrated source\n')
    exported = tmp_path / 'exported'
    candidate = sync.export_source(local, exported, parent)
    sync.publish(exported, parent, 'codex/release/exported')
    assert run(remote, 'rev-list', '--parents', '-n', '1', candidate).split() == [candidate, parent]
    assert 'private-history.md' not in run(remote, 'log', '--all', '--name-only', '--format=')


def test_uncommitted_or_unpublished_source_cannot_deploy(repositories):
    local, _, parent = repositories
    with pytest.raises(sync.SourceSyncError, match='clean published'):
        sync.assert_published(local, SimpleNamespace(release_id='src-' + 'a' * 40, git_commit=parent))
    (local / 'README.md').write_text('uncommitted\n')
    with pytest.raises(sync.SourceSyncError, match='Commit'):
        sync.publish(local, parent, 'codex/release/dirty')


def test_concurrent_publishers_preserve_exactly_one_shared_successor(repositories, tmp_path):
    from concurrent.futures import ThreadPoolExecutor
    local, remote, parent = repositories
    other = tmp_path / 'other'; run(tmp_path, 'clone', '-q', str(local), str(other))
    first = commit(local, 'first.md', 'first\n')
    second = commit(other, 'second.md', 'second\n')
    def attempt(repo, branch):
        try:
            return sync.publish(repo, parent, branch)
        except sync.SourceSyncError:
            return None
    with ThreadPoolExecutor(max_workers=2) as pool:
        jobs = [pool.submit(attempt, local, 'codex/repair/first'), pool.submit(attempt, other, 'codex/repair/second')]
        published = [job.result() for job in jobs]
    assert sum(commit_id is not None for commit_id in published) == 1
    assert run(remote, 'rev-parse', sync.REF) == next(commit_id for commit_id in published if commit_id)
    assert run(remote, 'rev-parse', sync.REF) in (first, second)


def test_dirty_parent_with_empty_gitlink_can_export_only_regular_source(repositories, tmp_path):
    local, _, parent = repositories
    (local / 'unused-submodule').mkdir()
    run(local, 'update-index', '--add', '--cacheinfo', '160000,' + parent + ',unused-submodule')
    run(local, '-c', 'user.name=Test', '-c', 'user.email=test@localhost', 'commit', '-qm', 'record gitlink')
    (local / 'README.md').write_text('local source change\n')
    destination = tmp_path / 'source'; destination.mkdir()
    entries = sync.copy_source(local, destination)
    assert [entry['path'] for entry in entries] == ['README.md']
    assert (destination / 'README.md').read_text() == 'local source change\n'
    assert not (destination / 'unused-submodule').exists()


def test_pending_operator_release_does_not_consume_incident_attempts(repositories, tmp_path, monkeypatch):
    import json
    sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
    import source_baseline
    from runner import Runner
    local, _, _ = repositories
    live = tmp_path / 'live.json'
    live.write_text(json.dumps({'releaseId': 'git-' + 'a' * 40,
        'sourcePaths': [{'path': 'frontend/src/live.ts', 'sha256': 'b' * 64}]}))
    monkeypatch.setattr(source_baseline, 'LIVE_MANIFEST', live)
    class MachineApi:
        def post(self, path, body):
            raise AssertionError('A pending source release must not claim an incident')
    runner = Runner.__new__(Runner)
    runner.root = tmp_path
    # Disk capacity is an external prerequisite, not the source/claim behavior under test.
    import shutil
    monkeypatch.setattr(shutil, 'disk_usage', lambda _: SimpleNamespace(free=30 * 1024 ** 3))
    runner.config = {'source_repository': str(local)}
    runner.api = MachineApi()
    with pytest.raises(sync.SourceSyncError, match='does not match the live'):
        runner.run_one()
