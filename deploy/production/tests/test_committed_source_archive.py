import subprocess
import tarfile

import pytest
from release_images import committed_source_state, committed_migration_set, create_committed_archive


def test_release_archives_use_published_objects_despite_dirty_local_files(tmp_path):
    def git(*args):
        return subprocess.check_output(['git', '-C', str(tmp_path), *args], text=True).strip()
    git('init', '-q')
    files = {
        'frontend/src/page.tsx': 'export default 1;',
        'deploy/production/config/example.conf': 'published config',
        'backend/src/NaderGorge.Infrastructure/Migrations/20260924000000_Example.cs': '// migration',
    }
    for name, content in files.items():
        path = tmp_path / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(content)
    git('add', '.')
    git('-c', 'user.name=Test', '-c', 'user.email=test@example.test', 'commit', '-qm', 'fixture')
    commit = git('rev-parse', 'HEAD')
    digest = committed_source_state(tmp_path, commit)['sourceStateSha256']
    (tmp_path / 'frontend/src/page.tsx').write_text('unpublished change')
    (tmp_path / 'deploy/production/config/example.conf').unlink()
    for production_only in (False, True):
        archive = create_committed_archive(tmp_path, commit, tmp_path / f'{production_only}.tar.gz', digest,
                                           production_only=production_only)
        with tarfile.open(archive) as bundle:
            expected = {name: content for name, content in files.items()
                        if not production_only or name.startswith('deploy/production/')}
            assert set(bundle.getnames()) == set(expected)
            for name, content in expected.items():
                assert bundle.extractfile(name).read().decode() == content
    assert committed_migration_set(tmp_path, commit) == ['20260924000000_Example']
    rejected = tmp_path / 'rejected.tar.gz'
    with pytest.raises(RuntimeError, match='digest mismatch'):
        create_committed_archive(tmp_path, commit, rejected, '0' * 64)
    assert not rejected.exists()
    assert (tmp_path / 'frontend/src/page.tsx').read_text() == 'unpublished change'
