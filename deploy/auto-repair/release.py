"""Release through the existing immutable three-node production protocol."""
from __future__ import annotations

import json
import os
import subprocess
import sys
import time
from pathlib import Path


def execute(argv: list[str], checkout: Path, lease) -> str:
    from runner import RepairFailure
    output = checkout.parent / 'release-command.log'
    environment = {key: os.environ[key] for key in ('PATH', 'HOME', 'MASSAR_KNOWN_HOSTS_FILE', 'MASSAR_SSH_IDENTITY_FILE') if key in os.environ}
    environment['PYTHONPATH'] = ''
    with output.open('w') as stream:
        proc = subprocess.Popen(argv, cwd=checkout, env=environment, stdout=stream, stderr=subprocess.STDOUT, start_new_session=True)
        deadline = time.monotonic() + 7200
        while proc.poll() is None:
            # Never kill a rolling migration/deploy midway. Its own bounded protocol completes,
            # but the next stage cannot start after lease loss.
            if time.monotonic() > deadline:
                lease.lost.set()
            time.sleep(2)
    if proc.returncode:
        raise RepairFailure('Production gate failed; inspect private release evidence. No further rollout was started.')
    if lease.lost.is_set():
        raise RepairFailure('Lease lost during release; reconcile server state')
    return output.read_text()[-20_000:]


def cluster(checkout: Path, arguments: list[str], lease) -> str:
    return execute([sys.executable, str(checkout / 'deploy/production/scripts/clusterctl.py'),
        '--inventory', str(checkout / 'deploy/production/inventory/production.yml'), *arguments], checkout, lease)


def deploy_release(checkout: Path, config: dict, lease) -> str:
    from runner import RepairFailure
    # Checkout tooling cannot be modified by the model; policy only permits application source.
    expected = (checkout.parent / 'baseline-release').read_text().strip()
    assert_current_release(checkout, expected)
    from source_sync import publish_locked
    parent = (checkout.parent / 'shared-parent').read_text().strip()
    commit = publish_locked(checkout, parent, 'codex/repair/' + lease.incident['id'])
    try:
        lease.report('deploying', 'حُفظ الإصلاح في GitHub قبل النشر. Commit: ' + commit + ' · codex/repair/' + lease.incident['id'])
        return deploy_published(checkout, lease, expected)
    except (OSError, ValueError, RuntimeError, subprocess.SubprocessError):
        from source_sync import mark_failed
        try:
            mark_failed(checkout, commit)
        except (OSError, ValueError, RuntimeError, subprocess.SubprocessError):
            print('Publication failure observation unavailable; reconcile the shared source', flush=True)
        raise


def deploy_published(checkout: Path, lease, expected: str):
    from runner import RepairFailure
    cluster(checkout, ['status', '--node', 'all', '--evidence-dir', str(checkout / 'artifacts/production/repair-status')], lease)
    provenance = execute([sys.executable, '-c',
        'import sys,json; from pathlib import Path; sys.path.insert(0,"deploy/production/scripts"); from release_images import source_state; print(json.dumps(source_state(Path("."))))'], checkout, lease)
    release_id = json.loads(provenance)['releaseId']
    previous_manifest = checkout.parent / 'previous-manifest.json'
    cluster(checkout, ['collect-current-manifest', '--node', 'all', '--manifest-output', str(previous_manifest),
        '--output', str(checkout.parent / 'previous-manifest-evidence.json')], lease)
    build_dir = checkout / 'artifacts/production/build'
    build = ['build', '--node', 'all', '--release', release_id, '--remote-builder', '--evidence-dir', str(build_dir)]
    cluster(checkout, [*build, '--dry-run'], lease)
    cluster(checkout, [*build, '--yes'], lease)
    manifest = build_dir / release_id / 'manifest.json'
    gate = checkout / 'artifacts/production/migration-gates' / (release_id + '.json')
    gate_argv = [sys.executable, str(checkout / 'deploy/production/scripts/prepare_release_migration_gate.py'),
        '--inventory', str(checkout / 'deploy/production/inventory/production.yml'),
        '--known-hosts', os.environ['MASSAR_KNOWN_HOSTS_FILE'], '--identity', os.environ['MASSAR_SSH_IDENTITY_FILE'],
        '--release', release_id, '--manifest', str(manifest), '--output', str(gate)]
    execute([*gate_argv, '--dry-run'], checkout, lease)
    execute([*gate_argv, '--yes'], checkout, lease)
    assert_current_release(checkout, expected)
    for stage in ('migrate', 'deploy'):
        if lease.lost.is_set():
            raise RepairFailure('Lease lost before ' + stage)
        argv = [stage, '--node', 'all', '--release', release_id, '--manifest', str(manifest),
            '--backup-evidence', str(gate), '--evidence-dir', str(checkout / 'artifacts/production' / ('repair-' + stage))]
        cluster(checkout, [*argv, '--dry-run'], lease)
        cluster(checkout, [*argv, '--yes'], lease)
    cluster(checkout, ['status', '--node', 'all', '--evidence-dir', str(checkout / 'artifacts/production/repair-final')], lease)
    assert_current_release(checkout, release_id)
    lease.report('deploying', 'تم التحقق من الإصدار ' + release_id + ' على node-3 وnode-2 وnode-1', releaseId=release_id)
    return release_id


def monitor_release(checkout: Path, config: dict, lease):
    from runner import RepairFailure
    baseline_count = lease.api.post(f"{lease.incident['id']}/heartbeat", {'leaseToken': lease.incident['leaseToken']})['occurrences']
    for _ in range(10):
        if lease.stop.wait(30) or lease.lost.is_set():
            raise RepairFailure('Monitoring lost contact; repair is not complete')
        cluster(checkout, ['status', '--node', 'all', '--evidence-dir', str(checkout / 'artifacts/production/repair-monitor')], lease)
        observed = lease.api.post(f"{lease.incident['id']}/heartbeat", {'leaseToken': lease.incident['leaseToken']})
        if observed['occurrences'] > baseline_count:
            raise RepairFailure('Incident recurred after deployment; needs further diagnosis')


def assert_current_release(checkout: Path, expected: str):
    from runner import RepairFailure
    scripts = str(checkout / 'deploy/production/scripts')
    if scripts not in sys.path:
        sys.path.insert(0, scripts)
    from clusterctl import load_inventory, target
    from ssh_transport import StrictSshTransport
    inventory = load_inventory(checkout / 'deploy/production/inventory/production.yml')
    ssh = StrictSshTransport(Path(os.environ['MASSAR_KNOWN_HOSTS_FILE']), Path(os.environ['MASSAR_SSH_IDENTITY_FILE']))
    for node in inventory.nodes:
        response = ssh.run(target(inventory, node), ['python3', '-c',
            'import json; print(json.load(open("/opt/massar/current/manifest.json"))["releaseId"])'])
        if response.stdout.strip() != expected:
            raise RepairFailure('Live release changed on ' + node.id + '; refresh source baseline before repair')


def rollback_release(checkout: Path, release_id: str, lease):
    previous = checkout.parent / 'previous-manifest.json'
    previous_id = json.loads(previous.read_text())['releaseId']
    current = checkout / 'artifacts/production/build' / release_id / 'manifest.json'
    gate = checkout / 'artifacts/production/migration-gates' / (release_id + '.json')
    arguments = ['rollback', '--node', 'all', '--release', previous_id, '--manifest', str(previous),
        '--current-manifest', str(current), '--compatibility-evidence', str(gate)]
    cluster(checkout, [*arguments, '--dry-run'], lease)
    cluster(checkout, [*arguments, '--yes'], lease)
    cluster(checkout, ['status', '--node', 'all', '--evidence-dir', str(checkout / 'artifacts/production/repair-rollback')], lease)
    assert_current_release(checkout, previous_id)
    return previous_id
