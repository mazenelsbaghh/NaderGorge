"""Regression coverage for the 2026-10-19 missing isolated runtime prerequisites."""
import io
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import threading
from types import SimpleNamespace

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from runner import DOCKER, RepairFailure, Runner
from verification import TestServices


@pytest.fixture
def docker_boundary(monkeypatch):
    commands = []
    replies = {'role': 'slave\n127.0.0.1\n1\nconnect\n-1\n',
               'write': "READONLY You can't write against a read only replica.\n"}

    def external_process(argv, **kwargs):
        commands.append(list(argv))
        assert kwargs.get('timeout', 0) > 0
        output = ''
        if argv[:4] == [*DOCKER, 'network'] and 'inspect' in argv:
            output = json.dumps([{'Id': 'a' * 64}])
        elif argv[-1] == 'ROLE':
            if replies.get('timeout'):
                raise subprocess.TimeoutExpired(argv, kwargs['timeout'])
            output = replies['role']
        elif 'redis-cli' in argv and 'SET' in argv:
            output = replies['write']
        return subprocess.CompletedProcess(argv, 0, stdout=output, stderr='')

    monkeypatch.setattr(subprocess, 'run', external_process)
    monkeypatch.setattr('time.sleep', lambda _seconds: None)
    return commands, replies


def runtime_config():
    return {'postgres_image': 'sha256:' + 'b' * 64, 'redis_image': 'sha256:' + 'c' * 64,
            'agent_network': 'massar-repair-internal', 'codex_home': '/synthetic-codex-home',
            'https_proxy': 'http://massar-repair-proxy:3128'}


def test_disposable_services_keep_readonly_replica_separate_from_unavailable_primary(tmp_path, docker_boundary):
    commands, _ = docker_boundary
    config = runtime_config()
    services = TestServices(config, tmp_path)
    services.start()
    services.close()

    containers = [argv for argv in commands if argv[:5] == [*DOCKER, 'run', '-d']]
    aliases = {argv[argv.index('--network-alias') + 1]: argv for argv in containers}
    assert set(aliases) == {'postgres', 'redis', 'redis-unavailable', 'redis-readonly'}
    for argv in containers:
        assert '--pull=never' in argv
        assert argv[argv.index('--network') + 1] == services.network
        assert not {'-p', '--publish', '--network=host', '-v', '--mount'} & set(argv)
    readonly = aliases['redis-readonly']
    assert readonly[-7:] == [config['redis_image'], 'redis-server', '--replicaof', '127.0.0.1', '1', '--replica-read-only', 'yes']
    assert aliases['redis-unavailable'][-4:] == [config['redis_image'], 'redis-server', '--min-replicas-to-write', '1']
    assert any(argv[-1] == 'ROLE' for argv in commands)
    assert any(argv[-3:] == ['SET', 'massar-repair:readiness', '1'] for argv in commands)
    assert {argv[-1] for argv in commands if argv[:5] == [*DOCKER, 'rm', '-f']} == set(services.names)
    assert [*DOCKER, 'network', 'rm', services.network] in commands


@pytest.mark.parametrize(('role', 'write'), [('master\n0\n0\n', 'OK\n'),
                                            ('slave\n127.0.0.1\n1\nconnect\n-1\n', 'NOREPLICAS Not enough good replicas to write.\n')])
def test_wrong_redis_role_or_wrong_write_failure_exhausts_readiness_and_cleans_up(tmp_path, docker_boundary, role, write):
    commands, replies = docker_boundary
    replies.update(role=role, write=write)
    services = TestServices(runtime_config(), tmp_path)

    with pytest.raises(RepairFailure, match='read-only Redis did not become ready'):
        services.start()

    assert sum(argv[-1] == 'ROLE' for argv in commands) == 30
    assert {argv[-1] for argv in commands if argv[:5] == [*DOCKER, 'rm', '-f']} == set(services.names)
    assert [*DOCKER, 'network', 'rm', services.network] in commands
    assert any('delete' in argv and 'blocked_bridges' in argv for argv in commands)


def test_readiness_probe_timeout_cleans_up_disposable_services(tmp_path, docker_boundary):
    commands, replies = docker_boundary
    replies['timeout'] = True
    services = TestServices(runtime_config(), tmp_path)

    with pytest.raises(subprocess.TimeoutExpired):
        services.start()

    assert {argv[-1] for argv in commands if argv[:5] == [*DOCKER, 'rm', '-f']} == set(services.names)
    assert [*DOCKER, 'network', 'rm', services.network] in commands


@pytest.mark.parametrize('mode', ['diagnostic', 'verification'])
def test_runner_containers_receive_every_disposable_database_and_both_redis_failure_modes(
        tmp_path, docker_boundary, monkeypatch, mode):
    workspace = tmp_path / 'workspace'
    workspace.mkdir()
    launched = []

    class ContainerProcess:
        def __init__(self, argv, **_kwargs):
            launched.append(argv)
            self.stdin = io.StringIO()
            self.returncode = 0
            if mode == 'diagnostic':
                (workspace / '.repair-response.json').write_text('{"safeToDeploy":false}')

        def poll(self): return self.returncode
        def wait(self, **_kwargs): return self.returncode

    monkeypatch.setattr(subprocess, 'Popen', ContainerProcess)
    supervisor = Runner.__new__(Runner)
    supervisor.root, supervisor.image = tmp_path, 'sha256:' + 'a' * 64
    supervisor.config, supervisor.stopping = runtime_config(), False
    lease = SimpleNamespace(lost=threading.Event(), paused=False)
    if mode == 'diagnostic':
        supervisor.container(workspace, {'instructions': 'Synthetic disposable diagnosis'}, lease)
    else:
        supervisor.verify(workspace, lease)

    argv, = launched
    environment = dict(argv[index + 1].split('=', 1) for index, argument in enumerate(argv[:-1]) if argument == '-e')
    assert environment['FINANCE_REPAIR_TEST_DB'] == 'Host=127.0.0.1;Database=finance_repair_test;Username=postgres'
    assert environment['TEACHER_CODE_TEST_DB'] == 'Host=127.0.0.1;Database=teacher_code_finance_test;Username=postgres'
    assert environment['MIM_GAME_TEST_DB'] == 'Host=127.0.0.1;Database=mim_game_test;Username=postgres'
    assert environment['TEST_REDIS_READONLY_CONNECTION'] == 'redis-readonly:6379,abortConnect=false'
    assert environment['TEST_REDIS_UNAVAILABLE_CONNECTION'] == 'redis-unavailable:6379'
    assert environment['REFUND_TEST_REDIS'] == 'redis:6379'
    assert environment['RUN_REDIS_INTEGRATION_TESTS'] == '1'
    if mode == 'diagnostic':
        assert 'redis-readonly' in environment['NO_PROXY'].split(',')
    assert any(argument.startswith('--network=container:massar-repair-test-') for argument in argv)
    assert not {'-p', '--publish', '--network=host'} & set(argv)


def test_legacy_timestamp_fixtures_run_in_separate_complementary_test_processes():
    script = (Path(__file__).resolve().parents[1] / 'verify.sh').read_text()
    filters = re.findall(r"--filter '([^']+)'", script)
    assert len(filters) == 2
    positive = {part.removeprefix('FullyQualifiedName~') for part in filters[0].split('|')}
    negative = {part.removeprefix('FullyQualifiedName!~') for part in filters[1].split('&')}
    assert positive == negative == {
        'NaderGorge.Application.Tests.AutoRepairPostgresTests.',
        'NaderGorge.Application.Tests.TeacherPaidFundingCorrectionTests.',
        'NaderGorge.Application.Tests.Finance.CodeCollectionMigrationGateTests.',
        'NaderGorge.Application.Tests.Finance.CodeBatchPostgresTests.',
        'NaderGorge.Application.Tests.MimGames.MimGamePostgresConcurrencyTests.',
    }


@pytest.mark.parametrize('connection', [
    'Host=127.0.0.1;Database=repair_test;Username=postgres',
    'Host=production.example;Database=repair_test;Username=postgres',
    'Host=127.0.0.1;Database=production;Username=postgres',
    '',
])
def test_browser_seed_reset_requires_the_native_disposable_database(connection):
    script = (Path(__file__).resolve().parents[1] / 'verify.sh').read_text()
    browser_setup = script.split('# Real browser requests use only the disposable PostgreSQL network namespace.\n', 1)[1]
    browser_setup = browser_setup.split('dotnet run --no-build', 1)[0]
    observe = '\nprintf "%s\\n%s\\n" "$ConnectionStrings__DefaultConnection" "$E2E_ALLOW_DESTRUCTIVE_RESET"\n'
    result = subprocess.run(['bash', '-eu', '-c', browser_setup + observe],
                            env={**os.environ, 'AUTO_REPAIR_TEST_DB': connection},
                            capture_output=True, text=True, timeout=5)

    if connection == 'Host=127.0.0.1;Database=repair_test;Username=postgres':
        assert result.returncode == 0
        assert result.stdout.splitlines() == [connection, 'true']
    else:
        assert result.returncode != 0
        assert result.stdout == ''


@pytest.mark.parametrize(('mode', 'elapsed', 'interrupt', 'failure'), [
    ('verification', 3600, None, None),
    ('verification', 3601, None, 'Verification interrupted'),
    ('verification', 1, 'lease', 'Verification interrupted'),
    ('verification', 1, 'pause', 'Verification interrupted'),
    ('verification', 1, 'shutdown', 'Verification interrupted'),
    ('verification', 1, 'log', 'Verification output budget exceeded'),
    ('diagnostic', 1801, None, 'Agent stopped'),
])
def test_verifier_deadlines_and_interruptions_always_clean_up(
        tmp_path, docker_boundary, monkeypatch, mode, elapsed, interrupt, failure):
    commands, _ = docker_boundary
    workspace = tmp_path / 'workspace'
    workspace.mkdir()
    supervisor = Runner.__new__(Runner)
    supervisor.root, supervisor.image = tmp_path, 'sha256:' + 'a' * 64
    supervisor.config, supervisor.stopping = runtime_config(), False
    lease = SimpleNamespace(lost=threading.Event(), paused=False)
    clock = iter([0, elapsed])
    monkeypatch.setattr('time.monotonic', lambda: next(clock))
    processes = []

    class RunningContainer:
        def __init__(self, argv, **kwargs):
            processes.append(self)
            self.argv, self.stdin, self.returncode = argv, io.StringIO(), None
            self.poll_count, self.waited = 0, False
            if interrupt == 'log':
                kwargs['stdout'].seek(10_000_000)
                kwargs['stdout'].write('x')
                kwargs['stdout'].flush()

        def poll(self):
            self.poll_count += 1
            if self.poll_count == 1:
                if interrupt == 'lease': lease.lost.set()
                if interrupt == 'pause': lease.paused = True
                if interrupt == 'shutdown': supervisor.stopping = True
                return None
            self.returncode = 0
            return self.returncode

        def wait(self, **_kwargs):
            self.waited = True
            self.returncode = 0
            return self.returncode

    monkeypatch.setattr(subprocess, 'Popen', RunningContainer)

    def run_container():
        if mode == 'verification':
            supervisor.verify_container([*DOCKER, 'run', '--name', 'synthetic-verifier'], workspace, lease)
        else:
            services = TestServices(supervisor.config, tmp_path)
            services.names.append('synthetic-postgres')
            supervisor.diagnostic_container(workspace, {'instructions': 'Synthetic deadline check'}, lease, services)

    if failure:
        with pytest.raises(RepairFailure, match=failure):
            run_container()
    else:
        run_container()
    process, = processes
    name = process.argv[process.argv.index('--name') + 1]
    assert [*DOCKER, 'rm', '-f', name] in commands
    assert process.waited
