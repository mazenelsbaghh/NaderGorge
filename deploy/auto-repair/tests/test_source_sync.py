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


@pytest.mark.parametrize('scenario,expected', [
    ('current', 'ready'), ('server-fix', 'needs-integration'),
    ('pending-source', 'pending-or-divergent-release'),
    ('mixed-nodes', 'pending-or-divergent-release'), ('failed-release', 'failed-release'), ('failed-live', 'failed-release'), ('invalid-journal', 'unavailable'),
])
def test_startup_distinguishes_incoming_fixes_from_live_releases(
        repositories, tmp_path, monkeypatch, scenario, expected):
    import hashlib
    import startup_check
    local, remote, _ = repositories
    (local / 'frontend').mkdir()
    parent = commit(local, 'frontend/app.ts', 'live\n')
    run(local, 'push', str(remote), 'HEAD:' + sync.REF)
    other = tmp_path / 'server'
    run(tmp_path, 'clone', '-q', str(local), str(other))
    manifests = [{'node': f'node-{number}', 'releaseId': 'git-' + parent,
                  'sourcePaths': [{'path': 'frontend/app.ts',
                      'sha256': hashlib.sha256(b'live\n').hexdigest()}]}
                 for number in range(1, 4)]
    if scenario == 'invalid-journal':
        manifests[0]['publication'] = []
    if scenario == 'failed-live':
        manifests[0]['publication'] = {'commit': parent, 'phase': 'failed'}
    if scenario == 'server-fix':
        commit(other, 'README.md', 'server tooling fix\n')
        run(other, 'push', str(remote), 'HEAD:' + sync.REF)
    elif scenario in ('pending-source', 'failed-release'):
        candidate = commit(local, 'frontend/app.ts', 'not deployed\n')
        if scenario == 'failed-release':
            manifests[0]['publication'] = {'commit': candidate, 'phase': 'failed'}
        run(local, 'push', str(remote), 'HEAD:' + sync.REF)
    elif scenario == 'mixed-nodes':
        manifests[2]['releaseId'] = 'git-' + 'f' * 40
    # The external SSH manifest reader is the only substituted boundary.
    monkeypatch.setattr(startup_check, 'live_manifests', lambda _: manifests)
    (local / 'README.md').write_text('local unfinished work\n')
    index = (local / '.git/index').read_bytes()
    if scenario == 'invalid-journal':
        with pytest.raises(sync.SourceSyncError, match='Invalid publication journal'):
            startup_check.inspect(local)
        return
    result = startup_check.inspect(local)
    assert result['status'] == expected
    assert result['dirty'] is True
    assert (local / 'README.md').read_text() == 'local unfinished work\n'
    assert (local / '.git/index').read_bytes() == index


def test_startup_offline_returns_failure_instead_of_readiness(repositories, monkeypatch, capsys):
    import startup_check
    local, _, _ = repositories
    def offline(_):
        raise OSError('private connection diagnostics')
    monkeypatch.setattr(startup_check, 'live_manifests', offline)
    monkeypatch.setattr(sys, 'argv', ['startup_check.py', '--repo', str(local)])
    assert startup_check.main() == 2
    output = capsys.readouterr().out
    assert 'unavailable' in output
    assert 'private connection diagnostics' not in output


@pytest.mark.parametrize('mutation', ['add', 'delete', 'modify'])
def test_export_rejects_source_inventory_changes_during_copy(repositories, tmp_path, monkeypatch, mutation):
    import shutil
    local, _, parent = repositories
    original_copy = shutil.copy2
    def changing_copy(source, destination, *args, **kwargs):
        result = original_copy(source, destination, *args, **kwargs)
        if mutation == 'add':
            (local / 'new-source.md').write_text('arrived during copy')
        elif mutation == 'delete':
            (local / 'README.md').unlink()
        else:
            (local / 'README.md').write_text('changed during copy')
        return result
    monkeypatch.setattr(shutil, 'copy2', changing_copy)
    with pytest.raises(sync.SourceSyncError, match='Local files changed'):
        sync.export_source(local, tmp_path / 'export', parent)


def test_public_export_excludes_local_previews_but_local_snapshot_preserves_them(repositories, tmp_path):
    local, _, parent = repositories
    (local / 'output').mkdir()
    (local / 'output/private-preview.html').write_text('private generated preview')
    exported = tmp_path / 'export'
    candidate = sync.export_source(local, exported, parent)
    assert not (exported / 'output').exists()
    assert sync.publish(exported, parent, 'codex/release/no-previews') == candidate
    snapshot = tmp_path / 'snapshot'; snapshot.mkdir()
    sync.copy_source(local, snapshot)
    assert (snapshot / 'output/private-preview.html').read_text() == 'private generated preview'


def test_dependency_inventory_includes_root_and_imported_build_inputs(repositories):
    from source_baseline import dependencies
    local, _, _ = repositories
    for name in ('global.json', 'custom.targets', 'Directory.Build.props', '.npmrc'):
        (local / name).write_text('original')
    before = dependencies(local)
    assert set(before) == {'global.json', 'custom.targets', 'Directory.Build.props', '.npmrc'}
    (local / 'custom.targets').write_text('new imported package')
    assert dependencies(local) != before


@pytest.mark.parametrize('pending', [False, True])
def test_guarded_edit_applies_only_with_verified_live_source(repositories, tmp_path, monkeypatch, pending):
    import hashlib
    import json
    import shutil
    import startup_edit
    from ssh_transport import StrictSshTransport
    local, remote, _ = repositories
    (local / 'frontend').mkdir()
    parent = commit(local, 'frontend/app.ts', 'live\n')
    run(local, 'push', str(remote), 'HEAD:' + sync.REF)
    inventory = local / 'deploy/production/inventory/production.yml'
    inventory.parent.mkdir(parents=True)
    shutil.copy2(Path(__file__).resolve().parents[2] / 'production/inventory/production.yml', inventory)
    for variable in ('MASSAR_KNOWN_HOSTS_FILE', 'MASSAR_SSH_IDENTITY_FILE'):
        credential = tmp_path / variable
        credential.write_text('disposable test transport fixture'); credential.chmod(0o600)
        monkeypatch.setenv(variable, str(credential))
    def remote_read(self, target, argv, **kwargs):
        manifest = {'releaseId': 'git-' + parent, 'sourcePaths': [{'path': 'frontend/app.ts',
            'sha256': hashlib.sha256(b'old\n' if pending else b'live\n').hexdigest()}]}
        return SimpleNamespace(returncode=0, stdout=json.dumps(manifest) if argv[0] == 'python3' else '')
    monkeypatch.setattr(StrictSshTransport, 'run', remote_read)
    commit(local, 'deploy/production/inventory/production.yml', inventory.read_text())
    from sync_monitor import SyncMonitor
    monkeypatch.setattr(shutil, 'disk_usage', lambda _: SimpleNamespace(free=30 * 1024 ** 3))
    class RecordingApi:
        def post(self, route, body):
            self.snapshot = body
            return {'accepted': True}
    api = RecordingApi()
    monitor = SyncMonitor({'source_repository': str(local), 'state_dir': str(local),
                           'verified_dependencies_source': str(local)}, api)
    monitor.check()
    assert api.snapshot['state'] == ('pending_release' if pending else 'ready')
    assert len(api.snapshot['nodes']) == 3
    patch = b'diff --git a/frontend/app.ts b/frontend/app.ts\n--- a/frontend/app.ts\n+++ b/frontend/app.ts\n@@ -1 +1 @@\n-live\n+edited\n'
    if pending:
        with pytest.raises(sync.SourceSyncError, match='Editing blocked'):
            startup_edit.guarded_apply(local, patch)
        assert (local / 'frontend/app.ts').read_text() == 'live\n'
    else:
        assert startup_edit.guarded_apply(local, patch)['status'] == 'applied'
        assert (local / 'frontend/app.ts').read_text() == 'edited\n'


def test_guarded_patch_rejects_traversal_and_preserves_outside_files(repositories, tmp_path):
    from startup_edit import apply_patch
    local, _, _ = repositories
    outside = tmp_path / 'outside.txt'; outside.write_text('safe\n')
    patch = b'diff --git a/../outside.txt b/../outside.txt\n--- a/../outside.txt\n+++ b/../outside.txt\n@@ -1 +1 @@\n-safe\n+overwritten\n'
    with pytest.raises(sync.SourceSyncError, match='Patch did not apply'):
        apply_patch(local, patch)
    assert outside.read_text() == 'safe\n'


def test_publication_journal_preserves_newer_source_and_completed_retries(tmp_path):
    import json
    journal = tmp_path / 'source-publication.json'
    class RemoteFilesystem:
        def run(self, host, argv):
            script = argv[2].replace('/var/lib/massar/rollout-locks/source-publication.json', str(journal))
            subprocess.run([sys.executable, '-c', script], check=True)
    remote = RemoteFilesystem()
    first, second = 'a' * 40, 'b' * 40
    sync.record_publication(remote, None, first, 'published')
    sync.record_publication(remote, None, first, 'failed')
    assert json.loads(journal.read_text())['phase'] == 'failed'
    sync.record_publication(remote, None, second, 'published')
    sync.record_publication(remote, None, first, 'failed')
    assert json.loads(journal.read_text())['commit'] == second
    sync.record_publication(remote, None, second, 'deployed')
    sync.record_publication(remote, None, second, 'published')
    assert json.loads(journal.read_text())['phase'] == 'deployed'
