import json
import subprocess
import sys
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from policy import assess_patch, patch_hash, redact, validate_review
from runner import Runner


@pytest.mark.parametrize('path', ['Makefile', 'deploy/production/scripts/clusterctl.py', '.agents/skills/ssh-server/SKILL.md', 'frontend/package.json', 'frontend/src/../../secret'])
def test_agent_cannot_change_release_authority(tmp_path, path):
    with pytest.raises(ValueError):
        assess_patch([path], tmp_path)


@pytest.mark.parametrize('path', ['backend/src/Finance/RefundService.cs', 'frontend/src/auth.ts', 'backend/src/Domain/Entities/Student.cs', 'backend/src/API/AutoRepair/RepairPolicy.cs', 'backend/src/API/Middleware/RedisRateLimitingMiddleware.cs'])
def test_sensitive_change_always_needs_owner(tmp_path, path):
    assert assess_patch([path], tmp_path) == [path]


def test_removing_authorization_needs_owner_even_in_an_ordinary_controller(tmp_path):
    path = 'backend/src/API/Controllers/LessonsController.cs'
    patch = b'--- a/LessonsController.cs\n+++ b/LessonsController.cs\n-[Authorize]\n+[AllowAnonymous]\n'
    assert assess_patch([path], tmp_path, patch) == [path]
    assert assess_patch([path], tmp_path, b'@@ ordinary display fix @@\n-return "bad";\n+return "fixed";\n') == []


def test_symlink_cannot_escape_source_workspace(tmp_path):
    directory = tmp_path / 'frontend/src'
    directory.mkdir(parents=True)
    (directory / 'page.tsx').symlink_to('/etc/passwd')
    with pytest.raises(ValueError):
        assess_patch(['frontend/src/page.tsx'], tmp_path)


def test_review_without_evidence_cannot_authorize_deployment():
    review = {'summary': 'fixed', 'safeToDeploy': True, 'critical': False, 'reproduction': '', 'verification': 'passed'}
    with pytest.raises(ValueError):
        validate_review(review)


def test_approval_binds_source_and_base_revision():
    assert patch_hash(b'patch', 'base-a') != patch_hash(b'patch', 'base-b')
    assert patch_hash(b'patch', 'base-a') != patch_hash(b'changed', 'base-a')


def test_agent_patch_survives_sealing_without_exposing_git_authority(tmp_path):
    source = tmp_path / 'source'
    source.mkdir()
    def git(*arguments):
        return subprocess.run(['git', '-C', str(source), *arguments], text=True, capture_output=True, check=True).stdout.strip()
    git('init')
    (source / 'frontend/src').mkdir(parents=True)
    (source / 'frontend/src/page.tsx').write_text('before\n')
    git('add', '.')
    git('-c', 'user.name=test', '-c', 'user.email=test@localhost', 'commit', '-m', 'baseline')
    runner = Runner.__new__(Runner)
    runner.root = tmp_path
    runner.config = {'source_repository': str(source), 'source_ref': git('rev-parse', 'HEAD'), 'baseline_release': 'git-test'}
    folder = tmp_path / 'incident'
    folder.mkdir()
    checkout = folder / 'checkout'
    git('clone', '--no-hardlinks', str(source), str(checkout))
    baseline = git('rev-parse', 'HEAD')
    workspace = runner.source_workspace(checkout, folder, baseline)
    assert not (workspace / '.git').exists()
    (workspace / 'frontend/src/page.tsx').write_text('after\n')
    patch, paths = runner.snapshot(checkout, workspace, baseline)
    assert paths == ['frontend/src/page.tsx']
    runner.git(checkout, ['reset', '--hard', baseline])
    patch_file = tmp_path / 'fix.patch'
    patch_file.write_bytes(patch)
    runner.git(checkout, ['apply', '--index', str(patch_file)])
    assert (checkout / 'frontend/src/page.tsx').read_text() == 'after\n'
    assert b'+after' in patch


def test_container_logs_ignore_normal_requests_and_deduplicate_by_source_timestamp():
    from collector import log_entries
    logs = '2026-09-14T00:00:00.123456789Z GET /health 200\n2026-09-14T00:00:01.123456789Z Error: token=private-value\n'
    first = log_entries(logs, 'node-1', 'admin')
    assert len(first) == 1
    assert 'private-value' not in first[0]['message']
    assert first == log_entries(logs, 'node-1', 'admin')
    assert first[0]['id'] != log_entries(logs, 'node-2', 'admin')[0]['id']
