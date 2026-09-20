"""Apply a reviewed Git patch only while source readiness and the shared lock hold."""
import argparse
import fcntl
import json
import subprocess
import uuid
from pathlib import Path

from clusterctl import load_inventory, operator_transport, target
from deploy_release import RolloutLock
from source_sync import SourceSyncError, git
from startup_check import inspect


def apply_patch(repo: Path, patch: bytes):
    # git apply rejects absolute/traversal paths and symlink traversal by default.
    for arguments in (['--check'], []):
        result = subprocess.run(['git', '-C', str(repo), 'apply', '--whitespace=error-all', *arguments, '-'],
                                input=patch, capture_output=True, timeout=30)
        if result.returncode:
            raise SourceSyncError('Patch did not apply cleanly; refresh the diff without overwriting local work')


def guarded_apply(repo: Path, patch: bytes):
    lock_path = Path(git(repo, 'rev-parse', '--git-path', 'massar-edit.lock'))
    if not lock_path.is_absolute():
        lock_path = repo / lock_path
    with lock_path.open('a') as local_lock:
        fcntl.flock(local_lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
        inventory = load_inventory(repo / 'deploy/production/inventory/production.yml', require_operator_files=True)
        lock = RolloutLock(operator_transport(inventory), target(inventory, inventory.nodes[0]), str(uuid.uuid4()))
        lock.acquire()
        try:
            observation = inspect(repo)
            if observation['status'] != 'ready':
                raise SourceSyncError('Editing blocked: ' + observation['status'])
            apply_patch(repo, patch)
            return {'status': 'applied', 'sharedCommit': observation['sharedCommit'], 'workspace': str(repo)}
        finally:
            lock.release()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--repo', type=Path, default=Path.cwd())
    parser.add_argument('--patch', type=Path, required=True)
    mode = parser.add_mutually_exclusive_group(required=True)
    mode.add_argument('--dry-run', action='store_true')
    mode.add_argument('--yes', action='store_true')
    args = parser.parse_args()
    repo = args.repo.resolve()
    patch = args.patch.read_bytes()
    if args.dry_run:
        result = inspect(repo)
        print(json.dumps(result))
        return 0 if result['status'] == 'ready' else 2
    print(json.dumps(guarded_apply(repo, patch)))
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
