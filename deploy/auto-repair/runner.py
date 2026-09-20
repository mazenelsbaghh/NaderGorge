#!/usr/bin/env python3
"""Single node supervisor. The agent has no deployment key or Docker socket."""
from __future__ import annotations

import fcntl
import json
import os
import re
import signal
import shutil
import sys
import subprocess
import tarfile
import threading
import tempfile
import time
import urllib.error
import urllib.request
import uuid
# Initialize typing before Python 3.14 background imports can observe it half-loaded.
import typing  # noqa: F401
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'production/scripts'))

# Keep exceptions and supervisor imports identical when launched as a script.
if __name__ == '__main__':
    sys.modules['runner'] = sys.modules[__name__]

from policy import assess_patch, patch_hash, redact, validate_report, validate_review


DOCKER = ["sudo", "-n", "/usr/bin/docker"]


class RepairFailure(RuntimeError):
    pass


class Api:
    def __init__(self, config: dict):
        self.url = config['api_url'].rstrip('/') + '/api/internal/auto-repair/'
        if not self.url.startswith('https://'):
            raise ValueError('Repair API requires verified HTTPS')
        self.token = Path(config['token_file']).read_text().strip()
        if len(self.token) < 32:
            raise ValueError('Runner token is not configured')

    def post(self, path: str, body: dict | list) -> dict:
        request = urllib.request.Request(self.url + path, json.dumps(body).encode(),
            {'Content-Type': 'application/json', 'User-Agent': 'Massar-AutoRepair/1.0',
             'X-Repair-Token': self.token}, method='POST')
        with urllib.request.urlopen(request, timeout=30) as response:
            return json.load(response)


class Lease:
    def __init__(self, api: Api, incident: dict):
        self.api, self.incident = api, incident
        self.stop = threading.Event()
        self.lost = threading.Event()
        self.paused = False
        self.thread = threading.Thread(target=self.pulse, daemon=True)

    def pulse(self):
        while not self.stop.wait(25):
            try:
                control = self.api.post(f"{self.incident['id']}/heartbeat", {'leaseToken': self.incident['leaseToken']})
                self.paused = control['paused']
            except (urllib.error.URLError, TimeoutError, OSError):
                self.lost.set()
                return

    def report(self, status: str, detail: str, **extra):
        if self.lost.is_set():
            raise RepairFailure('Lease lost; reconcile deployment before retry')
        self.api.post(f"{self.incident['id']}/report", {'leaseToken': self.incident['leaseToken'],
            'status': status, 'detail': redact(detail), **extra})


def command(argv: list[str], cwd: Path, timeout: int = 120) -> str:
    completed = subprocess.run(argv, cwd=cwd, text=True, capture_output=True, timeout=timeout, check=False)
    if completed.returncode:
        raise RepairFailure(f"{Path(argv[0]).name} failed: {redact(completed.stderr[-3000:])}")
    return completed.stdout.strip()


class Runner:
    def __init__(self, config: dict):
        self.config = config
        self.root = Path(config['state_dir']).resolve()
        self.root.mkdir(parents=True, exist_ok=True, mode=0o700)
        self.api = Api(config)
        if not re.fullmatch(r'(?:[\w./:-]+@)?sha256:[a-f0-9]{64}', config['agent_image']):
            raise ValueError('Agent image must use an installed immutable digest')
        self.image = config['agent_image']
        for image in (self.image, config['postgres_image'], config['redis_image']):
            if not re.fullmatch(r'(?:[\w./:-]+@)?sha256:[a-f0-9]{64}', image):
                raise ValueError('All test images require installed immutable digests')
            command([*DOCKER, 'image', 'inspect', image], self.root)
        network = json.loads(command([*DOCKER, 'network', 'inspect', config['agent_network']], self.root))[0]
        if not network['Internal']:
            raise ValueError('Agent network must be internal, with a separately restricted HTTPS proxy')
        bridge = 'br-' + network['Id'][:12]
        fence = command(['sudo', '-n', '/usr/sbin/nft', 'list', 'set', 'inet', 'massar_repair', 'blocked_bridges'], self.root)
        if bridge not in fence:
            raise ValueError('Host firewall fence is missing for the agent network')
        self.stopping = False

    def git(self, checkout: Path, arguments: list[str], workspace: Path | None = None) -> str:
        argv = ['git', '-c', 'core.hooksPath=/dev/null', '-c', 'core.fsmonitor=false']
        if workspace is not None:
            argv += [f'--git-dir={checkout / ".git"}', f'--work-tree={workspace}']
        return command(argv + arguments, checkout)

    def prepare(self, folder: Path) -> tuple[Path, Path, str]:
        checkout = folder / 'checkout'
        folder.mkdir(mode=0o700)
        from source_sync import fetch
        from source_baseline import verify_baseline
        source = Path(self.config['source_repository'])
        shared = fetch(source)
        command(['git', 'clone', '--no-hardlinks', '--', str(source), str(checkout)], self.root)
        self.git(checkout, ['checkout', '--detach', shared])
        live_release = verify_baseline(checkout, self.config)
        (folder / 'baseline-release').write_text(live_release)
        (folder / 'shared-parent').write_text(shared)
        baseline = self.git(checkout, ['rev-parse', 'HEAD'])
        return checkout, self.source_workspace(checkout, folder, baseline), baseline

    def source_workspace(self, checkout: Path, folder: Path, baseline: str) -> Path:
        workspace = folder / 'workspace'
        workspace.mkdir(mode=0o700)
        archive = folder / 'source.tar'
        command(['git', 'archive', '--format=tar', '-o', str(archive), baseline], checkout)
        with tarfile.open(archive) as bundle:
            bundle.extractall(workspace, filter='data')
        archive.unlink()
        return workspace

    def seed_dependencies(self, workspace: Path):
        command([*DOCKER, 'run', '--rm', '--pull=never', '--network=none', '--read-only',
            '--cap-drop=ALL', '--security-opt=no-new-privileges', '--cpus=2', '--memory=2g',
            '--tmpfs', '/tmp:rw,nosuid,nodev,size=1g,mode=1777', '-e', 'HOME=/tmp',
            '--user', f'{os.getuid()}:{os.getgid()}', '--mount', f'type=bind,src={workspace},dst=/workspace',
            self.image, 'bash', '-c',
            'cp -R /opt/bootstrap/frontend/node_modules /workspace/frontend/ && cp -R /opt/bootstrap/worker/node_modules /workspace/worker/ && '
            + "printf '<configuration><packageSources><clear /></packageSources></configuration>\\n' >/tmp/repair-nuget.config && "
            + 'cd /workspace && dotnet restore backend/NaderGorge.sln --configfile /tmp/repair-nuget.config -p:NuGetAudit=false'],
            self.root, timeout=300)

    def container(self, workspace: Path, request: dict, lease: Lease) -> str:
        from verification import TestServices
        services = TestServices(self.config, self.root)
        services.start()
        try:
            command([*DOCKER, 'network', 'connect', self.config['agent_network'], services.names[0]], self.root)
            return self.diagnostic_container(workspace, request, lease, services)
        finally:
            services.close()

    def diagnostic_container(self, workspace: Path, request: dict, lease: Lease, services) -> str:
        if lease.lost.is_set() or lease.paused or self.stopping:
            raise RepairFailure('Agent cannot start after pause or lease loss')
        name = 'massar-repair-' + uuid.uuid4().hex
        argv = [*DOCKER, 'run', '--interactive', '--rm', '--pull=never', '--name', name, '--init', '--read-only',
            '--cap-drop=ALL', '--security-opt=no-new-privileges', '--pids-limit=256', '--cpus=2', '--memory=6g',
            '--network=container:' + services.names[0], '--user', f'{os.getuid()}:{os.getgid()}',
            '--tmpfs', '/tmp:rw,nosuid,nodev,size=1g,mode=1777', '--mount', f'type=bind,src={workspace},dst=/workspace',
            '--mount', f'type=bind,src={Path(self.config["codex_home"]).resolve()},dst=/codex',
            '-e', 'AUTO_REPAIR_TEST_DB=Host=127.0.0.1;Database=repair_test;Username=postgres',
            '-e', 'ConnectionStrings__DefaultConnection=Host=127.0.0.1;Database=massar_live_support_query_budget_disposable_repair;Username=postgres',
            '-e', 'MASSAR_LEARNING_TEST_CONNECTION=Host=127.0.0.1;Database=massar_learning_test;Username=postgres',
            '-e', 'LIVE_SUPPORT_QUERY_BUDGET_DATABASE_AUTHORIZATION=DELETE-DISPOSABLE-LIVE-SUPPORT-QUERY-BUDGET-DATABASE',
            '-e', 'ConnectionStrings__Redis=redis:6379', '-e', 'Redis__ConnectionString=redis:6379',
            '-e', 'AUTO_REPAIR_TEST_REDIS=redis:6379', '-e', 'TEST_REDIS_CONNECTION=redis:6379',
            '-e', 'TEST_REDIS_UNAVAILABLE_CONNECTION=redis-unavailable:6379', '-e', 'RUN_REDIS_INTEGRATION_TESTS=1',
            '-e', 'NO_PROXY=127.0.0.1,localhost,postgres,redis,redis-unavailable,.lvh.me',
            '-e', 'CODEX_HOME=/codex', '-e', 'HOME=/tmp', '-e', 'HTTPS_PROXY=' + self.config['https_proxy'],
            '-e', 'HTTP_PROXY=' + self.config['https_proxy'], '-w', '/workspace', self.image,
            # Codex documents this flag for externally sandboxed environments. Docker provides
            # the enforced read-only filesystem, mount boundary, network fence and resource caps;
            # nested bubblewrap namespaces are unavailable under the container's default seccomp.
            'codex', 'exec', '--ignore-user-config', '--skip-git-repo-check', '--dangerously-bypass-approvals-and-sandbox',
            '--model', 'gpt-6-astra', '-c', 'model_reasoning_effort="medium"',
            '-c', 'service_tier="default"',
            '--json', '--output-schema', '/opt/repair/review-schema.json', '-o', '/workspace/.repair-response.json', '-']
        # The report remains outside the model's transcript; only bounded final evidence is published.
        errors = tempfile.TemporaryFile(mode='w+b')
        proc = subprocess.Popen(argv, stdin=subprocess.PIPE, stdout=subprocess.DEVNULL, stderr=errors, text=True)
        assert proc.stdin is not None
        proc.stdin.write(json.dumps(request, ensure_ascii=False))
        proc.stdin.close()
        deadline = time.monotonic() + 1800
        try:
            while proc.poll() is None:
                if lease.lost.is_set() or lease.paused or self.stopping or time.monotonic() > deadline:
                    raise RepairFailure('Agent stopped: pause, lost lease, shutdown or time budget')
                if os.fstat(errors.fileno()).st_size > 1_000_000:
                    raise RepairFailure('Agent diagnostic output budget exceeded')
                time.sleep(1)
            if proc.returncode:
                errors.seek(max(0, os.fstat(errors.fileno()).st_size - 3000))
                raise RepairFailure('Codex failed: ' + redact(errors.read(3000).decode(errors='replace')))
            response = workspace / '.repair-response.json'
            if response.is_symlink() or response.stat().st_size > 32_000:
                raise RepairFailure('Invalid agent response file')
            return response.read_text()
        finally:
            subprocess.run([*DOCKER, 'rm', '-f', name], capture_output=True, check=False, timeout=30)
            proc.wait(timeout=30)
            errors.close()
            (workspace / '.repair-response.json').unlink(missing_ok=True)

    def snapshot(self, checkout: Path, workspace: Path, baseline: str) -> tuple[bytes, list[str]]:
        # Index and git configuration are never mounted into the agent container.
        self.git(checkout, ['add', '-A', '--', '.'], workspace)
        paths = self.git(checkout, ['diff', '--cached', '--name-only', '-z', baseline], workspace).split('\0')
        paths = [path for path in paths if path]
        if not paths:
            return b'', []
        assess_patch(paths, workspace)
        patch = subprocess.run(['git', f'--git-dir={checkout / ".git"}', f'--work-tree={workspace}',
            'diff', '--cached', '--binary', '--no-ext-diff', baseline], capture_output=True, check=True).stdout
        if len(patch) > 1_000_000:
            raise RepairFailure('Repair exceeds patch budget')
        return patch, paths

    def diagnose(self, incident: dict, lease: Lease):
        folder = self.root / (incident['id'] + '-' + incident['leaseToken'])
        checkout, workspace, baseline = self.prepare(folder)
        self.seed_dependencies(workspace)
        lease.report('repairing', 'بدأ فحص الكود في بيئة معزولة وتجهيز الإصلاح')
        request = {'instructions': (Path(__file__).parent / 'SKILL.md').read_text(),
            'incident': incident['evidence'], 'category': incident['category'], 'mode': 'repair'}
        diagnosis = json.loads(self.container(workspace, request, lease))
        validate_report(diagnosis)
        diagnostic_detail = '\n'.join([
            diagnosis['summary'], 'إعادة الإنتاج: ' + diagnosis['reproduction'],
            'التحقق: ' + diagnosis['verification']])
        if not diagnosis['safeToDeploy']:
            lease.report('needs_evidence', diagnostic_detail)
            return
        patch, paths = self.snapshot(checkout, workspace, baseline)
        if not paths:
            lease.report('needs_evidence', 'لم ينتج التشخيص تغييرًا قابلًا للمراجعة؛ يلزم دليل جديد قبل إعادة المحاولة.\n' + diagnostic_detail)
            return
        digest = patch_hash(patch, baseline)
        lease.report('testing', 'جارٍ التحقق المستقل من الإصلاح والاختبارات\n' + '\n'.join(paths))
        for offset in range(0, len(patch), 6000):
            lease.report('testing', 'مراجعة التغيير المقترح:\n' + patch[offset:offset + 6000].decode('utf-8', errors='replace'))
        review = json.loads(self.container(workspace, {**request, 'mode': 'review',
            'instructions': request['instructions'] + '\nReview only. Do not edit source. Run reproduction and relevant tests; refuse when prerequisites or evidence are missing.'}, lease))
        validate_review(review)
        next_patch, _ = self.snapshot(checkout, workspace, baseline)
        if next_patch != patch:
            raise RepairFailure('Review modified the proposed source')
        self.verify(workspace, lease)
        verified_patch, _ = self.snapshot(checkout, workspace, baseline)
        if verified_patch != patch:
            raise RepairFailure('Verification changed source; repair must be reviewed again')
        critical = assess_patch(paths, workspace, patch)
        # Seal the source only after independent verification; deployment never runs agent-chosen commands.
        self.git(checkout, ['reset', '--hard', baseline])
        patch_file = folder / 'repair.patch'
        patch_file.write_bytes(patch)
        self.git(checkout, ['apply', '--index', str(patch_file)])
        self.git(checkout, ['-c', 'user.name=Massar Repair', '-c', 'user.email=repair@localhost', 'commit', '-m', 'Repair incident ' + incident['id']])
        receipt = {'folder': str(folder), 'baseline': baseline, 'hash': digest, 'paths': paths, 'review': review}
        (self.root / (incident['id'] + '.json')).write_text(json.dumps(receipt))
        summary = '\n'.join([review['summary'], review['reproduction'], review['verification'], 'الملفات: ' + ', '.join(paths)])
        if critical or review['critical']:
            summary += '\nتغيير مهم يحتاج قرار المالك: ' + ', '.join(critical)
            lease.report('awaiting_approval', summary, proposalHash=digest)
        else:
            lease.report('ready', summary, proposalHash=digest)

    def verify(self, workspace: Path, lease: Lease):
        # Fixed script comes from the installed supervisor, not from the proposed source tree.
        script = Path(__file__).parent / 'verify.sh'
        name = 'massar-verify-' + uuid.uuid4().hex
        from verification import TestServices
        services = TestServices(self.config, self.root)
        services.start()
        argv = [*DOCKER, 'run', '--rm', '--pull=never', '--name', name, '--init', '--cap-drop=ALL',
            '--security-opt=no-new-privileges', '--network=container:' + services.names[0], '--cpus=2', '--memory=8g', '--pids-limit=512',
            '--user', f'{os.getuid()}:{os.getgid()}', '--mount', f'type=bind,src={workspace},dst=/workspace',
            '--mount', f'type=bind,src={script},dst=/verify.sh,readonly', '-e', 'HOME=/tmp',
            '-e', 'ConnectionStrings__DefaultConnection=Host=127.0.0.1;Database=repair_test;Username=postgres',
            '-e', 'AUTO_REPAIR_TEST_DB=Host=127.0.0.1;Database=repair_test;Username=postgres',
            '-e', 'AUTO_REPAIR_TEST_REDIS=redis:6379',
            '-e', 'TEST_REDIS_CONNECTION=redis:6379', '-e', 'RUN_REDIS_INTEGRATION_TESTS=1',
            '-e', 'TEST_REDIS_UNAVAILABLE_CONNECTION=redis-unavailable:6379',
            '-w', '/workspace', self.image, 'bash', '/verify.sh']
        try:
            self.verify_container(argv, workspace, lease)
        finally:
            services.close()

    def verify_container(self, argv: list[str], workspace: Path, lease: Lease):
        if lease.lost.is_set() or lease.paused or self.stopping:
            raise RepairFailure('Verification cannot start after pause or lease loss')
        name = argv[argv.index('--name') + 1]
        log = workspace.parent / 'verification.log'
        with log.open('w') as output:
            proc = subprocess.Popen(argv, stdout=output, stderr=subprocess.STDOUT)
            deadline = time.monotonic() + 1800
            try:
                while proc.poll() is None:
                    if lease.lost.is_set() or lease.paused or self.stopping or time.monotonic() > deadline:
                        raise RepairFailure('Verification interrupted')
                    if log.stat().st_size > 10_000_000:
                        raise RepairFailure('Verification output budget exceeded')
                    time.sleep(1)
                if proc.returncode:
                    raise RepairFailure('Verification failed: ' + redact(log.read_text()[-3000:]))
            finally:
                subprocess.run([*DOCKER, 'rm', '-f', name], capture_output=True, check=False, timeout=30)
                proc.wait(timeout=30)

    def deploy(self, incident: dict, lease: Lease):
        receipt = json.loads((self.root / (incident['id'] + '.json')).read_text())
        folder = Path(receipt['folder'])
        patch = (folder / 'repair.patch').read_bytes()
        if patch_hash(patch, receipt['baseline']) != incident['proposalHash']:
            raise RepairFailure('Approved source hash changed')
        checkout = folder / 'checkout'
        actual_patch = subprocess.run(['git', 'diff', '--binary', '--no-ext-diff', receipt['baseline'], 'HEAD'], cwd=checkout, capture_output=True, check=True).stdout
        if actual_patch != patch or self.git(checkout, ['status', '--porcelain']):
            raise RepairFailure('Sealed checkout changed after approval')
        if lease.paused:
            raise RepairFailure('Deployment is paused')
        from release import deploy_release
        lease.report('deploying', 'بدء إصدار موحد ونشر تدريجي مع بوابات النسخ الاحتياطي وفحص الصحة')
        release_id = deploy_release(folder / 'checkout', self.config, lease)
        pending = self.root / 'current.tmp'
        pending.write_text(json.dumps({'repository': str(checkout), 'ref': self.git(checkout, ['rev-parse', 'HEAD']), 'release': release_id}))
        pending.replace(self.root / 'current.json')
        lease.report('monitoring', 'اكتمل النشر وفحص الثلاثة سيرفرات؛ بدأت نافذة المراقبة', releaseId=release_id)
        from release import monitor_release
        try:
            monitor_release(folder / 'checkout', self.config, lease)
        except RepairFailure:
            from release import rollback_release
            previous_id = rollback_release(checkout, release_id, lease)
            pending.write_text(json.dumps({'repository': str(checkout), 'ref': receipt['baseline'], 'release': previous_id}))
            pending.replace(self.root / 'current.json')
            lease.report('rolled_back', 'تم التراجع إلى إصدار التطبيق السابق على الثلاثة سيرفرات بعد فشل المراقبة. مخطط قاعدة البيانات المتوافق بقي كما هو.', releaseId=previous_id)
            return
        lease.report('completed', 'نجحت بوابات النشر وفحوصات الصحة خلال نافذة المراقبة', releaseId=release_id)

    def run_one(self):
        if shutil.disk_usage(self.root).free < 20 * 1024 ** 3:
            raise RepairFailure('Less than 20 GiB free for isolated repair; service stopped')
        from source_baseline import refresh_shared_source
        refresh_shared_source(self.config)
        claimed = self.api.post('claim', {})
        incident = claimed['incident']
        if incident is None:
            return
        lease = Lease(self.api, incident)
        lease.thread.start()
        try:
            if incident['status'] == 'ready':
                self.deploy(incident, lease)
            else:
                self.diagnose(incident, lease)
        except (RepairFailure, ValueError, OSError, urllib.error.URLError, subprocess.SubprocessError) as exc:
            try:
                lease.report('failed', redact(str(exc)))
            except (RepairFailure, urllib.error.URLError, OSError):
                print('Repair interrupted; lease recovery will require reconciliation', flush=True)
        finally:
            lease.stop.set()
            lease.thread.join(timeout=35)
            workspace = self.root / (incident['id'] + '-' + incident['leaseToken']) / 'workspace'
            if workspace.is_dir() and not workspace.is_symlink():
                shutil.rmtree(workspace)

    def serve(self):
        with (self.root / 'runner.lock').open('w') as lock:
            fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
            from collector import Collector
            collector = Collector(self.config, self.api)
            collector.thread.start()
            from sync_monitor import SyncMonitor
            synchronization = SyncMonitor(self.config, self.api)
            synchronization.thread.start()
            while not self.stopping:
                try:
                    self.run_one()
                except (urllib.error.URLError, OSError, ValueError, RepairFailure, subprocess.SubprocessError) as exc:
                    print('Repair service unavailable: ' + type(exc).__name__, flush=True)
                for _ in range(30):
                    if self.stopping:
                        break
                    time.sleep(1)
            synchronization.stop.set()
            synchronization.thread.join(timeout=35)
            collector.stop.set()
            collector.thread.join(timeout=35)


if __name__ == '__main__':
    import sys
    runner = Runner(json.loads(Path(sys.argv[1]).read_text()))
    def stop_runner(signum, frame):
        runner.stopping = True
    signal.signal(signal.SIGTERM, stop_runner)
    signal.signal(signal.SIGINT, stop_runner)
    runner.serve()
