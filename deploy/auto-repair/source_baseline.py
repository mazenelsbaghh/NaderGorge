"""Bind a shared Git source to the live application and the verified dependency cache."""
import hashlib
import json
import sys
from pathlib import Path


def dependencies(repo: Path):
    selected = [repo / project / name for project in ('frontend', 'worker')
        for name in ('package.json', 'package-lock.json')]
    selected += list((repo / 'backend').rglob('*.csproj'))
    selected += [p for p in (repo / 'backend').rglob('*')
        if p.is_file() and p.name in ('Directory.Packages.props', 'Directory.Build.props', 'NuGet.Config', 'nuget.config', 'global.json')]
    return {str(p.relative_to(repo)): hashlib.sha256(p.read_bytes()).hexdigest() for p in selected}


def verify_baseline(checkout: Path, config: dict):
    from source_sync import SourceSyncError
    from release import assert_current_release
    manifest = json.loads(Path('/opt/massar/current/manifest.json').read_text())
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
