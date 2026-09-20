"""Fail-closed operator pre-edit check; never modifies working files or the index."""
from __future__ import annotations

import argparse
import hashlib
import json
import os
import subprocess
from concurrent.futures import ThreadPoolExecutor
from datetime import datetime, timezone
from pathlib import Path

import source_sync as sync
from ssh_transport import SshTransportError


def shared_files(repo: Path, commit: str, paths: tuple[str, ...]) -> dict[str, str]:
    records = sync.git(repo, 'ls-tree', '-r', '-z', commit, '--',
                       *paths).split('\0')
    blobs = []
    for record in filter(None, records):
        metadata, path = record.split('\t', 1)
        mode, kind, oid = metadata.split()
        if kind != 'blob' or mode not in ('100644', '100755'):
            raise sync.SourceSyncError('Shared application contains a non-regular source entry')
        blobs.append((path, oid))
    result = subprocess.run(['git', '-C', str(repo), 'cat-file', '--batch'],
                            input=''.join(oid + '\n' for _, oid in blobs).encode(),
                            capture_output=True, timeout=60, check=True)
    offset = 0
    hashes = {}
    for path, oid in blobs:
        end = result.stdout.index(b'\n', offset)
        object_id, kind, size = result.stdout[offset:end].decode().split()
        if object_id != oid or kind != 'blob':
            raise sync.SourceSyncError('Invalid shared source object')
        offset = end + 1
        length = int(size)
        hashes[path] = hashlib.sha256(result.stdout[offset:offset + length]).hexdigest()
        offset += length + 1
    if not hashes:
        raise sync.SourceSyncError('Shared application source is empty')
    return hashes


def shared_application(repo: Path, commit: str) -> dict[str, str]:
    return shared_files(repo, commit, ('backend', 'frontend', 'worker'))


def live_manifests(repo: Path) -> list[dict]:
    from clusterctl import load_inventory, operator_transport, target
    # The documented operator workflow invokes this gate directly. Keep the
    # strict inventory references, while supplying the same workstation paths
    # used by the reviewed read-only operational wrapper when no environment
    # has been injected (for example, a local Codex run).
    os.environ.setdefault('MASSAR_KNOWN_HOSTS_FILE', '/Users/mazenelsbagh/.ssh/massar_prod_known_hosts')
    os.environ.setdefault('MASSAR_SSH_IDENTITY_FILE', '/Users/mazenelsbagh/.ssh/massar_prod_cluster_ed25519')
    inventory = load_inventory(repo / 'deploy/production/inventory/production.yml',
                               require_operator_files=True)
    transport = operator_transport(inventory)
    # Only release metadata; never application records, environment or credentials.
    script = ('import json; from pathlib import Path; m=json.load(open("/opt/massar/current/manifest.json")); '
              'p=Path("/var/lib/massar/rollout-locks/source-publication.json"); '
              'publication=json.loads(p.read_text()) if p.exists() else None; '
              'print(json.dumps({"releaseId":m["releaseId"],"publication":publication,"sourcePaths":'
              '[e for e in m["sourcePaths"] if e["path"].startswith('
              '("backend/","frontend/","worker/"))]}))')

    def read(node):
        response = transport.run(target(inventory, node), ['python3', '-c', script])
        manifest = json.loads(response.stdout)
        manifest['node'] = node.id
        return manifest

    with ThreadPoolExecutor(max_workers=3) as pool:
        return list(pool.map(read, inventory.nodes))


def inspect(repo: Path) -> dict:
    shared = sync.fetch(repo)
    head = sync.git(repo, 'rev-parse', 'HEAD')
    dirty = bool(sync.git(repo, 'status', '--porcelain'))
    conflicts = sync.git(repo, 'diff', '--name-only', '--diff-filter=U').splitlines()
    # rev-list works for unrelated histories too; failure remains unavailable.
    missing = sync.git(repo, 'rev-list', shared, '^' + head).splitlines()
    manifests = live_manifests(repo)
    expected = shared_application(repo, shared)
    live = []
    for manifest in manifests:
        actual = {entry['path']: entry['sha256'] for entry in manifest['sourcePaths']}
        live.append({'node': manifest['node'], 'releaseId': manifest['releaseId'],
                     'applicationMatchesShared': actual == expected})
    if len(live) != 3 or len({item['node'] for item in live}) != 3:
        raise sync.SourceSyncError('Three distinct production nodes are required')
    if sync.tip(repo) != shared or sync.git(repo, 'rev-parse', 'HEAD') != head:
        raise sync.SourceSyncError('Source advanced during startup; rerun the check')
    status = 'ready'
    if missing:
        status = 'needs-integration'
    if conflicts:
        status = 'conflict'
    if len({item['releaseId'] for item in live}) != 1 or not all(
            item['applicationMatchesShared'] for item in live):
        status = 'pending-or-divergent-release'
    publication = next((m.get('publication') for m in manifests if m['node'] == 'node-1'), None)
    if publication is not None and (not isinstance(publication, dict)
            or not sync.SHA.fullmatch(str(publication.get('commit', '')))
            or publication.get('phase') not in ('published', 'deploying', 'deployed', 'failed', 'rolled_back')):
        raise sync.SourceSyncError('Invalid publication journal; reconcile its release evidence')
    if publication and publication.get('commit') == shared:
        if publication.get('phase') in ('failed', 'rolled_back'):
            status = 'failed-release'
    return {'publication': publication, 'status': status, 'checkedAt': datetime.now(timezone.utc).isoformat(),
            'workspace': str(repo), 'sharedCommit': shared, 'localCommit': head,
            'dirty': dirty, 'missingSharedCommits': missing, 'conflicts': conflicts,
            'nodes': live, 'workingFilesUnchanged': True}


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--repo', type=Path, default=Path.cwd())
    args = parser.parse_args()
    try:
        result = inspect(args.repo.resolve())
    except (OSError, ValueError, KeyError, TypeError, subprocess.SubprocessError,
            SshTransportError) as exc:
        # SSH diagnostics and Git errors can include configured credentials.
        result = {'status': 'unavailable', 'errorType': type(exc).__name__,
                  'reason': 'Could not verify GitHub and all three live manifests; inspect prerequisites privately.'}
    print(json.dumps(result, ensure_ascii=False, indent=2))
    return 0 if result['status'] == 'ready' else 2


if __name__ == '__main__':
    raise SystemExit(main())
