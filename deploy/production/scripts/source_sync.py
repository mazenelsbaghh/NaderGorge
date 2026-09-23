"""One source-only GitHub history shared by operators and the repair supervisor."""
from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import shutil
import subprocess
import uuid
from pathlib import Path

URL = 'https://github.com/mazenelsbaghh/NaderGorge.git'
REF = 'refs/heads/codex/production'
TRACKING = 'refs/remotes/massar/production'
SHA = re.compile(r'^[0-9a-f]{40}$')
CONFIG = Path('/etc/massar/source-sync.json')


class SourceSyncError(ValueError):
    pass


def git(repo: Path, *args: str) -> str:
    env = os.environ.copy()
    env['GIT_TERMINAL_PROMPT'] = '0'
    if CONFIG.exists():
        import shlex
        config = json.loads(CONFIG.read_text())
        env['GIT_SSH_COMMAND'] = shlex.join(['ssh', '-o', 'BatchMode=yes', '-o', 'IdentitiesOnly=yes',
            '-o', 'StrictHostKeyChecking=yes', '-o', 'UserKnownHostsFile=' + config['known_hosts'],
            '-i', config['identity_file']])
    argv = ['git', '--literal-pathspecs', '-c', 'core.hooksPath=/dev/null', '-c', 'core.fsmonitor=false']
    if CONFIG.exists():
        argv += ['-c', 'url.ssh://git@github.com/.insteadOf=https://github.com/']
    result = subprocess.run([*argv, '-C', str(repo), *args], env=env,
        capture_output=True, text=True, timeout=180)
    if result.returncode:
        # Remote diagnostics may contain user-configured credentials. Keep them private.
        raise SourceSyncError('Git source synchronization failed: ' + args[0])
    return result.stdout.strip()


def tip(repo: Path) -> str:
    response = git(repo, 'ls-remote', URL, REF).split()
    if len(response) != 2 or response[1] != REF or not SHA.fullmatch(response[0]):
        raise SourceSyncError('Shared production branch is missing or invalid')
    return response[0]


def fetch(repo: Path) -> str:
    git(repo, 'fetch', '--no-tags', URL, REF + ':' + TRACKING)
    return git(repo, 'rev-parse', TRACKING)


def ancestor(repo: Path, older: str, newer: str):
    if not SHA.fullmatch(older) or not SHA.fullmatch(newer):
        raise SourceSyncError('Invalid source commit identity')
    if git(repo, 'merge-base', older, newer) != older:
        raise SourceSyncError('Source omits shared fixes; integrate production before publishing')


def assert_published(repo: Path, manifest):
    assert_published_provenance(repo, {'releaseId': manifest.release_id,
                                       'gitCommit': manifest.git_commit})


def assert_published_provenance(repo: Path, provenance):
    if provenance['releaseId'] != 'git-' + str(provenance['gitCommit']):
        raise SourceSyncError('Production requires a clean published source commit')
    if tip(repo) != provenance['gitCommit']:
        raise SourceSyncError('GitHub production advanced or candidate is unpublished; synchronize and verify again')


def validate_publish_candidate(repo: Path, expected: str, branch: str,
                               selected_candidate: str | None = None) -> str:
    if not re.fullmatch(r'codex/(?:repair|release)/[a-zA-Z0-9-]+', branch):
        raise SourceSyncError('Invalid candidate branch')
    candidate = selected_candidate or git(repo, 'rev-parse', 'HEAD')
    if not SHA.fullmatch(candidate):
        raise SourceSyncError('Candidate must be a full Git commit SHA')
    if not selected_candidate and git(repo, 'status', '--porcelain'):
        raise SourceSyncError('Commit the complete verified candidate before publication')
    ancestor(repo, expected, candidate)
    if selected_candidate:
        from release_images import committed_source_entries
        approved = {str(e['path']) for e in committed_source_entries(repo, candidate)}
        tree_paths = set(filter(None, git(repo, 'ls-tree', '-r', '--name-only', '-z', candidate).split('\0')))
        if tree_paths != approved:
            raise SourceSyncError('Candidate tree contains excluded or unsafe source paths')
    else:
        approved = {str(e['path']) for e in publication_entries(repo)}
        if set(filter(None, git(repo, 'ls-files', '-z').split('\0'))) != approved:
            raise SourceSyncError('Publish only an exported source repository; local artifacts/history must stay private')
    if candidate != expected and git(repo, 'rev-list', '--parents', '-n', '1', candidate).split() != [candidate, expected]:
        raise SourceSyncError('Export one source commit on the shared parent before publication')
    if tip(repo) not in (expected, candidate):
        raise SourceSyncError('Shared source changed; merge and reverify instead of overwriting it')
    return candidate


def publish(repo: Path, expected: str, branch: str, selected_candidate: str | None = None) -> str:
    candidate = validate_publish_candidate(repo, expected, branch, selected_candidate)
    if tip(repo) == candidate:
        return candidate
    # The first push preserves the candidate even if another publisher wins the CAS.
    git(repo, 'push', URL, candidate + ':refs/heads/' + branch)
    # Ancestry was checked above: the lease implements compare-and-swap, never a history rewrite.
    git(repo, 'push', '--force-with-lease=' + REF + ':' + expected, URL, candidate + ':' + REF)
    if tip(repo) != candidate:
        raise SourceSyncError('Shared source changed immediately after publication')
    return candidate


def record_publication(transport, host, commit: str, phase: str):
    if not SHA.fullmatch(commit) or phase not in ('published', 'deploying', 'deployed', 'failed', 'rolled_back'):
        raise SourceSyncError('Invalid publication observation')
    script = """import json,os,uuid
from pathlib import Path
from datetime import datetime,timezone
p=Path('/var/lib/massar/rollout-locks/source-publication.json')
old=json.loads(p.read_text()) if p.exists() else None
commit,phase=COMMIT,PHASE
if (phase=='published' and old and old['commit']==commit) or (phase!='published' and old and old['commit']!=commit):
 raise SystemExit(0)
value={'commit':commit,'phase':phase,'observedAt':datetime.now(timezone.utc).isoformat()}
tmp=p.with_name('source-publication-'+uuid.uuid4().hex+'.tmp')
tmp.write_text(json.dumps(value));tmp.chmod(0o600);os.replace(tmp,p)
""".replace('COMMIT', repr(commit)).replace('PHASE', repr(phase))
    transport.run(host, ['python3', '-c', script])


def mark_failed(repo: Path, expected: str):
    from clusterctl import load_inventory, target, operator_transport
    from deploy_release import RolloutLock
    inventory = load_inventory(repo / 'deploy/production/inventory/production.yml', require_operator_files=True)
    transport, host = operator_transport(inventory), target(inventory, inventory.nodes[0])
    lock = RolloutLock(transport, host, str(uuid.uuid4()))
    lock.acquire()
    try:
        if tip(repo) != expected:
            raise SourceSyncError('Cannot mark a different shared publication failed')
        record_publication(transport, host, expected, 'failed')
    finally:
        lock.release()


def publish_locked(repo: Path, expected: str, branch: str, candidate: str | None = None) -> str:
    from clusterctl import load_inventory, target, operator_transport
    from deploy_release import RolloutLock
    inventory = load_inventory(repo / 'deploy/production/inventory/production.yml', require_operator_files=True)
    lock = RolloutLock(operator_transport(inventory), target(inventory, inventory.nodes[0]), str(uuid.uuid4()))
    lock.acquire()
    try:
        candidate = publish(repo, expected, branch, candidate)
        record_publication(lock.transport, lock.target, candidate, 'published')
        return candidate
    finally:
        lock.release()


def publication_entries(repo: Path):
    from release_images import release_source_entries
    # Generated previews and exports are local artifacts, never implicit public source.
    return [entry for entry in release_source_entries(repo)
            if Path(str(entry['path'])).parts[0] not in ('output', 'outputs', 'artifacts')]


def copy_entries(repo: Path, destination: Path, entries):
    for entry in entries:
        source = repo / str(entry['path'])
        if hashlib.sha256(source.read_bytes()).hexdigest() != entry['sha256']:
            raise SourceSyncError('Local files changed during source preparation')
        output = destination / str(entry['path'])
        output.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, output)
    return entries


def copy_source(repo: Path, destination: Path):
    from release_images import release_source_entries
    return copy_entries(repo, destination, release_source_entries(repo))


def export_source(repo: Path, destination: Path, expected: str):
    if destination.exists():
        raise SourceSyncError('Destination already exists')
    destination.mkdir(parents=True)
    git(destination, 'init', '-q')
    git(destination, 'fetch', '--no-tags', URL, REF)
    if git(destination, 'rev-parse', 'FETCH_HEAD') != expected:
        raise SourceSyncError('Shared source advanced; integrate again')
    git(destination, 'checkout', '-q', '-b', 'codex/release/' + uuid.uuid4().hex, expected)
    for child in destination.iterdir():
        if child.name != '.git':
            shutil.rmtree(child) if child.is_dir() and not child.is_symlink() else child.unlink()
    entries = copy_entries(repo, destination, publication_entries(repo))
    git(destination, 'add', '-f', '-A', '--', '.')
    git(destination, '-c', 'user.name=Massar Source', '-c', 'user.email=source@localhost',
        'commit', '--allow-empty', '-qm', 'Integrate reviewed local source with shared production')
    # Compare the complete inventory, including additions/deletions and file modes.
    if publication_entries(repo) != entries:
        raise SourceSyncError('Local files changed; discard candidate and prepare again')
    if tip(repo) != expected:
        raise SourceSyncError('Shared source advanced during export; integrate again')
    return git(destination, 'rev-parse', 'HEAD')


def integrate(repo: Path, destination: Path):
    from release_images import release_source_entries
    shared = fetch(repo)
    if destination.exists():
        raise SourceSyncError('Integration destination already exists')
    git(repo, 'worktree', 'add', '-b', 'codex/integrate/' + uuid.uuid4().hex, str(destination), 'HEAD')
    # Snapshot only approved source files; the original index and working files are untouched.
    entries = release_source_entries(repo)
    paths = {str(entry['path']) for entry in entries}
    copy_source(repo, destination)
    tracked = git(repo, 'ls-files', '-z').split('\0')
    for relative in tracked:
        if relative and not (repo / relative).exists() and (destination / relative).is_file():
            (destination / relative).unlink()
    ordered_paths = sorted(paths)
    for offset in range(0, len(ordered_paths), 100):
        git(destination, 'add', '-f', '--', *ordered_paths[offset:offset + 100])
    git(destination, 'add', '-u')
    git(destination, '-c', 'user.name=Massar Local', '-c', 'user.email=local@localhost',
        'commit', '--allow-empty', '-qm', 'Private snapshot of current local work before synchronization')
    try:
        git(destination, '-c', 'user.name=Massar Local', '-c', 'user.email=local@localhost',
            'merge', '--no-edit', shared)
    except SourceSyncError:
        conflicts = git(destination, 'diff', '--name-only', '--diff-filter=U').splitlines()
        if not conflicts:
            raise
        return {'status': 'conflict', 'workspace': str(destination), 'conflicts': conflicts,
                'sharedCommit': shared, 'originalWorkspaceUnchanged': True}
    return {'status': 'integrated', 'workspace': str(destination), 'sharedCommit': shared,
            'originalWorkspaceUnchanged': True}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('action', choices=['status', 'integrate', 'export', 'publish', 'mark-failed'])
    parser.add_argument('--repo', type=Path, default=Path.cwd())
    parser.add_argument('--destination', type=Path)
    parser.add_argument('--expected')
    parser.add_argument('--branch')
    parser.add_argument('--candidate', help='reviewed source-only Git commit prepared with a temporary index')
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument('--dry-run', action='store_true')
    mode.add_argument('--yes', action='store_true')
    args = parser.parse_args()
    repo = args.repo.resolve()
    shared = tip(repo)
    if args.action == 'publish' and args.dry_run:
        if not args.expected or not args.branch:
            raise SourceSyncError('Publication requires expected parent and candidate branch')
        candidate = validate_publish_candidate(repo, args.expected, args.branch, args.candidate)
        print(json.dumps({'action': 'publish-preview', 'sharedCommit': shared,
            'parent': args.expected, 'candidate': candidate, 'branch': args.branch}))
        return
    if args.action == 'status' or args.dry_run:
        print(json.dumps({'action': args.action, 'sharedCommit': shared, 'localCommit': git(repo, 'rev-parse', 'HEAD'),
            'dirty': bool(git(repo, 'status', '--porcelain')), 'destination': str(args.destination) if args.destination else None,
            'candidate': args.candidate}))
        return
    if not args.yes:
        raise SourceSyncError('Mutation requires --dry-run then --yes')
    if args.action in ('integrate', 'export') and args.destination is None:
        raise SourceSyncError('A new isolated destination is required')
    if args.action == 'mark-failed':
        if not args.expected:
            raise SourceSyncError('Failure reconciliation requires the exact shared commit')
        mark_failed(repo, args.expected)
        print(json.dumps({'status': 'failed', 'sharedCommit': args.expected}))
    elif args.action == 'integrate':
        print(json.dumps(integrate(repo, args.destination.resolve())))
    elif args.action == 'export':
        expected = args.expected or shared
        ancestor(repo, expected, git(repo, 'rev-parse', 'HEAD'))
        if git(repo, 'status', '--porcelain'):
            raise SourceSyncError('Resolve and commit integration before exporting')
        print(json.dumps({'commit': export_source(repo, args.destination.resolve(), expected), 'workspace': str(args.destination)}))
    else:
        if not args.expected or not args.branch:
            raise SourceSyncError('Publication requires expected parent and candidate branch')
        print(json.dumps({'published': publish_locked(repo, args.expected, args.branch, args.candidate)}))


if __name__ == '__main__':
    try:
        main()
    except (SourceSyncError, OSError, subprocess.TimeoutExpired) as exc:
        raise SystemExit(str(exc))
