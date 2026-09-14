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
    argv = ['git', '-c', 'core.hooksPath=/dev/null', '-c', 'core.fsmonitor=false']
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
    if manifest.release_id != 'git-' + str(manifest.git_commit):
        raise SourceSyncError('Production requires a clean published source commit')
    if tip(repo) != manifest.git_commit:
        raise SourceSyncError('GitHub production advanced or candidate is unpublished; synchronize and verify again')


def publish(repo: Path, expected: str, branch: str) -> str:
    if not re.fullmatch(r'codex/(?:repair|release)/[a-zA-Z0-9-]+', branch):
        raise SourceSyncError('Invalid candidate branch')
    candidate = git(repo, 'rev-parse', 'HEAD')
    if git(repo, 'status', '--porcelain'):
        raise SourceSyncError('Commit the complete verified candidate before publication')
    ancestor(repo, expected, candidate)
    from release_images import release_source_entries
    approved = {str(e['path']) for e in release_source_entries(repo)}
    if set(filter(None, git(repo, 'ls-files', '-z').split('\0'))) != approved:
        raise SourceSyncError('Publish only an exported source repository; local artifacts/history must stay private')
    if candidate != expected and git(repo, 'rev-list', '--parents', '-n', '1', candidate).split() != [candidate, expected]:
        raise SourceSyncError('Export one source commit on the shared parent before publication')
    current = tip(repo)
    if current == candidate:
        return candidate
    if current != expected:
        raise SourceSyncError('Shared source changed; merge and reverify instead of overwriting it')
    # The first push preserves the candidate even if another publisher wins the CAS.
    git(repo, 'push', URL, candidate + ':refs/heads/' + branch)
    # Ancestry was checked above: the lease implements compare-and-swap, never a history rewrite.
    git(repo, 'push', '--force-with-lease=' + REF + ':' + expected, URL, candidate + ':' + REF)
    if tip(repo) != candidate:
        raise SourceSyncError('Shared source changed immediately after publication')
    return candidate


def publish_locked(repo: Path, expected: str, branch: str) -> str:
    from clusterctl import load_inventory, target, operator_transport
    from deploy_release import RolloutLock
    inventory = load_inventory(repo / 'deploy/production/inventory/production.yml', require_operator_files=True)
    lock = RolloutLock(operator_transport(inventory), target(inventory, inventory.nodes[0]), str(uuid.uuid4()))
    lock.acquire()
    try:
        return publish(repo, expected, branch)
    finally:
        lock.release()


def copy_source(repo: Path, destination: Path):
    from release_images import release_source_entries
    entries = release_source_entries(repo)
    for entry in entries:
        source = repo / str(entry['path'])
        if hashlib.sha256(source.read_bytes()).hexdigest() != entry['sha256']:
            raise SourceSyncError('Local files changed during source preparation')
        output = destination / str(entry['path'])
        output.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, output)
    return entries


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
    entries = copy_source(repo, destination)
    git(destination, 'add', '-f', '-A', '--', '.')
    git(destination, '-c', 'user.name=Massar Source', '-c', 'user.email=source@localhost',
        'commit', '--allow-empty', '-qm', 'Integrate reviewed local source with shared production')
    # Bind the exported snapshot to the exact files inspected, not a changing worktree.
    for entry in entries:
        if hashlib.sha256((repo / str(entry['path'])).read_bytes()).hexdigest() != entry['sha256']:
            raise SourceSyncError('Local files changed; discard candidate and prepare again')
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
    git(destination, 'add', '-f', '--', *sorted(paths))
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
    parser.add_argument('action', choices=['status', 'integrate', 'export', 'publish'])
    parser.add_argument('--repo', type=Path, default=Path.cwd())
    parser.add_argument('--destination', type=Path)
    parser.add_argument('--expected')
    parser.add_argument('--branch')
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument('--dry-run', action='store_true')
    mode.add_argument('--yes', action='store_true')
    args = parser.parse_args()
    repo = args.repo.resolve()
    shared = tip(repo)
    if args.action == 'status' or args.dry_run:
        print(json.dumps({'action': args.action, 'sharedCommit': shared, 'localCommit': git(repo, 'rev-parse', 'HEAD'),
            'dirty': bool(git(repo, 'status', '--porcelain')), 'destination': str(args.destination) if args.destination else None}))
        return
    if not args.yes:
        raise SourceSyncError('Mutation requires --dry-run then --yes')
    if args.action in ('integrate', 'export') and args.destination is None:
        raise SourceSyncError('A new isolated destination is required')
    if args.action == 'integrate':
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
        print(json.dumps({'published': publish_locked(repo, args.expected, args.branch)}))


if __name__ == '__main__':
    try:
        main()
    except (SourceSyncError, OSError, subprocess.TimeoutExpired) as exc:
        raise SystemExit(str(exc))
