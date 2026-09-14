"""The service entry point must survive a transient source-release mismatch."""
import json
import runpy
import subprocess
import sys
from pathlib import Path
from types import SimpleNamespace


def test_script_entry_retries_imported_repair_failure(tmp_path, monkeypatch, capsys):
    token = tmp_path / 'token'
    token.write_text('test-only-' * 8)
    config = tmp_path / 'config.json'
    config.write_text(json.dumps({
        'state_dir': str(tmp_path), 'api_url': 'https://test.invalid', 'token_file': str(token),
        'agent_image': 'sha256:' + 'a' * 64, 'postgres_image': 'sha256:' + 'b' * 64,
        'redis_image': 'sha256:' + 'c' * 64, 'agent_network': 'test-network',
    }))
    script = Path(__file__).resolve().parents[1] / 'runner.py'
    calls = []
    def refresh(_config):
        from runner import RepairFailure
        calls.append('refresh')
        if len(calls) == 2:
            sys.modules['runner'].runner.stopping = True
        raise RepairFailure('temporary release mismatch')
    class Background:
        def __init__(self, *_args):
            self.thread = SimpleNamespace(start=lambda: None, join=lambda **_kw: None)
            self.stop = SimpleNamespace(set=lambda: None)
    def external_command(argv, **_kwargs):
        output = '[{"Internal":true,"Id":"123456789012"}]' if 'network' in argv else 'br-123456789012'
        return subprocess.CompletedProcess(argv, 0, stdout=output, stderr='')
    monkeypatch.setitem(sys.modules, 'runner', None)
    monkeypatch.setitem(sys.modules, 'source_baseline', SimpleNamespace(refresh_shared_source=refresh))
    monkeypatch.setitem(sys.modules, 'collector', SimpleNamespace(Collector=Background))
    monkeypatch.setitem(sys.modules, 'sync_monitor', SimpleNamespace(SyncMonitor=Background))
    monkeypatch.setattr(subprocess, 'run', external_command)
    monkeypatch.setattr('shutil.disk_usage', lambda _path: SimpleNamespace(free=30 * 1024 ** 3))
    monkeypatch.setattr('time.sleep', lambda _seconds: None)
    monkeypatch.setattr('signal.signal', lambda *_args: None)
    monkeypatch.setattr(sys, 'argv', [str(script), str(config)])
    runpy.run_path(str(script), run_name='__main__')
    assert calls == ['refresh', 'refresh']
    assert capsys.readouterr().out.count('Repair service unavailable: RepairFailure') == 2
