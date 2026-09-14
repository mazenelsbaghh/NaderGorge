"""Bind a shared Git source to the live application and the verified dependency cache."""
import json
import sys
from pathlib import Path

LIVE_MANIFEST = Path('/opt/massar/current/manifest.json')


def dependency_path(relative: str) -> bool:
    path = Path(relative)
    name = path.name.lower()
    return (name in ('package.json', 'package-lock.json', 'pnpm-lock.yaml', 'yarn.lock',
                     '.npmrc', 'global.json', 'nuget.config', 'packages.lock.json')
            or path.suffix.lower() in ('.csproj', '.fsproj', '.vbproj', '.props', '.targets')
            or relative in ('deploy/auto-repair/Dockerfile', 'deploy/auto-repair/fonts.mjs'))


def dependencies(repo: Path):
    from release_images import release_source_entries
    return {str(entry['path']): entry['sha256'] for entry in release_source_entries(repo)
            if dependency_path(str(entry['path']))}


def verify_baseline(checkout: Path, config: dict):
    from source_sync import SourceSyncError
    from release import assert_current_release
    manifest = json.loads(LIVE_MANIFEST.read_text())
    expected = {e['path']: e['sha256'] for e in manifest['sourcePaths']
        if e['path'].startswith(('backend/', 'frontend/', 'worker/'))}
    sys.path.insert(0, str(checkout / 'deploy/production/scripts'))
    from release_images import release_source_entries
    actual = {e['path']: e['sha256'] for e in release_source_entries(checkout)
        if e['path'].startswith(('backend/', 'frontend/', 'worker/'))}
    if actual != expected:
        raise SourceSyncError('GitHub source does not match the live application; finish or reconcile the pending release before repair')
    if dependencies(checkout) != dependencies(Path(config['verified_dependencies_source'])):
        raise SourceSyncError('Shared dependencies changed; prepare and verify a new repair image before continuing')
    assert_current_release(checkout, manifest['releaseId'])
    return manifest['releaseId']


def refresh_shared_source(config: dict):
    from source_sync import fetch, git
    repo = Path(config['source_repository'])
    shared = fetch(repo)
    git(repo, 'checkout', '--detach', shared)
    verify_baseline(repo, config)
