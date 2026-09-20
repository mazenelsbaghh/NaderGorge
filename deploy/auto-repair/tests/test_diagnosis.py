"""An inconclusive diagnosis must not be mistaken for a broken patch or retried."""
import json
import subprocess
import sys
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from runner import Lease, Runner


@pytest.mark.parametrize('safe', [False, True])
def test_no_repair_preserves_diagnosis_and_releases_work_without_deployment(tmp_path, monkeypatch, safe):
    source = tmp_path / 'source'
    source.mkdir()
    def git(*args):
        return subprocess.check_output(['git', '-C', str(source), *args], text=True).strip()
    git('init', '-q')
    (source / 'frontend/src').mkdir(parents=True)
    (source / 'frontend/src/page.tsx').write_text('original\n')
    git('add', '.')
    git('-c', 'user.name=test', '-c', 'user.email=test@localhost', 'commit', '-qm', 'baseline')
    baseline = git('rev-parse', 'HEAD')
    monkeypatch.setattr('source_sync.fetch', lambda _repo: baseline)
    monkeypatch.setattr('source_baseline.verify_baseline', lambda *_args: 'git-' + baseline)
    original_run = subprocess.run
    def external_process(argv, *args, **kwargs):
        if argv[:3] == ['sudo', '-n', '/usr/bin/docker']:
            return subprocess.CompletedProcess(argv, 0, stdout='', stderr='')
        return original_run(argv, *args, **kwargs)
    monkeypatch.setattr(subprocess, 'run', external_process)
    diagnosis = {'summary': 'Missing query timing token=fixture-secret', 'safeToDeploy': safe,
                 'critical': False, 'reproduction': 'Production latency not reproduced',
                 'verification': 'Behaviour tests do not measure PostgreSQL lock waits'}
    monkeypatch.setattr(Runner, 'container', lambda *_args: json.dumps(diagnosis))
    class ReportApi:
        def __init__(self): self.reports = []
        def post(self, _path, body): self.reports.append(body)
    api = ReportApi()
    incident = {'id': 'incident', 'leaseToken': 'lease', 'evidence': 'slow request', 'category': 'performance'}
    runner = Runner.__new__(Runner)
    runner.root = tmp_path
    runner.image = 'test-image'
    runner.config = {'source_repository': str(source)}
    runner.diagnose(incident, Lease(api, incident))
    assert [r['status'] for r in api.reports] == ['repairing', 'needs_evidence']
    report = api.reports[-1]
    assert diagnosis['reproduction'] in report['detail']
    assert diagnosis['verification'] in report['detail']
    assert 'fixture-secret' not in report['detail']
    assert 'proposalHash' not in report
    assert not (tmp_path / 'incident.json').exists()
    assert not (tmp_path / 'incident-lease/repair.patch').exists()
