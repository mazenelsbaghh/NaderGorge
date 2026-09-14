#!/usr/bin/env python3
"""Run on node-3 after provisioning, with an explicit preview/apply boundary."""
import argparse
import hashlib
import json
import os
import shutil
import subprocess
import uuid
from pathlib import Path

NODE = 'node@sha256:f5a0871ab03b035c58bdb3007c3d177b001c2145c18e81817b71624dcf7d8bff'
SDK = 'mcr.microsoft.com/dotnet/sdk@sha256:712ffb3919c095da6129ba5ed9d0e7660ab45b966140819d1d58e081bef64293'
POSTGRES = 'postgres@sha256:57c72fd2a128e416c7fcc499958864df5301e940bca0a56f58fddf30ffc07777'
REDIS = 'redis@sha256:ff02b58f971e7d7d156a1267e283fcbbeee91773b6aa36c49dac28ecfe28eadf'
DOCKER = ['sudo', '-n', '/usr/bin/docker']
HERE = Path('/opt/massar-auto-repair')
PRIVATE = Path.home() / '.local/share/massar-auto-repair'


def run(args, cwd=None):
    return subprocess.check_output(args, cwd=cwd, text=True).strip()


def install_text(text, destination, mode='0644'):
    staged = PRIVATE / 'runtime-staged'
    staged.write_text(text)
    staged.chmod(0o600)
    try:
        run(['sudo', '-n', '/usr/bin/install', '-m', mode, str(staged), destination])
    finally:
        staged.unlink()


def source_snapshot(manifest):
    build_source = Path('/var/lib/massar/builds') / manifest['releaseId'] / 'source'
    source = PRIVATE / ('input-' + manifest['releaseId'])
    destination = PRIVATE / ('source-' + manifest['releaseId'])
    if destination.exists():
        if (destination / '.git').is_dir():
            assert not run(['git', 'status', '--porcelain'], destination), 'Source snapshot was modified'
            return destination
        destination.rename(destination.with_name(destination.name + '-incomplete-' + uuid.uuid4().hex[:8]))
    if not source.exists():
        source.mkdir(mode=0o700)
        run(['sudo', '-n', '/usr/bin/cp', '-a', str(build_source) + '/.', str(source)])
        run(['sudo', '-n', '/usr/bin/chown', '-R', str(os.getuid()) + ':' + str(os.getgid()), str(source)])
    # Copy only the release manifest's verified regular files; never its build artifacts.
    destination.mkdir(mode=0o700)
    for entry in manifest['sourcePaths']:
        relative = entry['path']
        assert not Path(relative).is_absolute() and '..' not in Path(relative).parts
        path = source / relative
        assert path.is_file() and not path.is_symlink()
        assert hashlib.sha256(path.read_bytes()).hexdigest() == entry['sha256'], 'Source differs from deployed manifest'
        output = destination / relative
        output.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(path, output)
    run(['git', 'init', '-q'], destination)
    run(['git', 'add', '-f', '.'], destination)
    run(['git', '-c', 'user.name=Massar Repair', '-c', 'user.email=repair@localhost',
         'commit', '-qm', 'Verified source of ' + manifest['releaseId']], destination)
    return destination


def build_agent(source):
    context = PRIVATE / 'image-context'
    if context.exists():
        shutil.rmtree(context)
    context.mkdir(mode=0o700)
    # Source is already bound to a reviewed release and its secret-scanned manifest.
    shutil.copytree(source / 'backend', context / 'backend',
                    ignore=shutil.ignore_patterns('bin', 'obj', '.env*', 'appsettings*.json'))
    for project in ('frontend', 'worker'):
        (context / project).mkdir()
        for name in ('package.json', 'package-lock.json'):
            shutil.copy2(source / project / name, context / project / name)
    (context / 'deploy/auto-repair').mkdir(parents=True)
    for name in ('Dockerfile', 'review-schema.json', 'fonts.mjs'):
        shutil.copy2(HERE / name, context / 'deploy/auto-repair' / name)
    log = PRIVATE / 'agent-build.log'
    with log.open('w') as stream:
        result = subprocess.run([*DOCKER, 'build', '--pull=false', '--network=default',
            '--build-arg', 'NODE_IMAGE=' + NODE, '--build-arg', 'DOTNET_IMAGE=' + SDK,
            '--build-arg', 'CODEX_VERSION=0.154.0', '-f', str(context / 'deploy/auto-repair/Dockerfile'),
            '-t', 'massar/repair-agent:prepared', str(context)], stdout=stream, stderr=subprocess.STDOUT)
    if result.returncode:
        raise RuntimeError('Agent image build failed; inspect private agent-build.log')
    return run([*DOCKER, 'image', 'inspect', 'massar/repair-agent:prepared', '--format', '{{.Id}}'])


def provision_proxy():
    for name, internal in [('massar-repair-internal', True), ('massar-repair-outbound', False)]:
        check = subprocess.run([*DOCKER, 'network', 'inspect', name], capture_output=True, text=True)
        if check.returncode:
            run([*DOCKER, 'network', 'create', *(['--internal'] if internal else []), name])
        else:
            assert json.loads(check.stdout)[0]['Internal'] == internal, 'Existing network isolation differs'
    network = json.loads(run([*DOCKER, 'network', 'inspect', 'massar-repair-internal']))[0]
    bridge = 'br-' + network['Id'][:12]
    assert len(network['Id']) == 64 and all(c in '0123456789abcdef' for c in network['Id'])
    # Docker internal networking blocks forwarding, but needs an explicit host INPUT fence.
    firewall = f'''add table inet massar_repair
add set inet massar_repair blocked_bridges {{ type ifname; }}
add element inet massar_repair blocked_bridges {{ "{bridge}" }}
add chain inet massar_repair protect_host {{ type filter hook input priority -20; policy accept; }}
flush chain inet massar_repair protect_host
add rule inet massar_repair protect_host iifname @blocked_bridges drop
'''
    install_text(firewall, '/etc/massar/repair-network.nft')
    run(['sudo', '-n', '/usr/sbin/nft', '-c', '-f', '/etc/massar/repair-network.nft'])
    run(['sudo', '-n', '/usr/sbin/nft', '-f', '/etc/massar/repair-network.nft'])
    unit = f'''[Unit]
Description=Restricted HTTPS proxy for Massar Codex
After=docker.service network-online.target
Requires=docker.service
[Service]
Restart=on-failure
RestartSec=10
ExecStartPre=/usr/sbin/nft -f /etc/massar/repair-network.nft
ExecStartPre=-/usr/bin/docker rm -f massar-repair-proxy
ExecStartPre=/usr/bin/docker create --pull=never --name massar-repair-proxy --network massar-repair-outbound --user 1000:1000 --read-only --cap-drop ALL --security-opt no-new-privileges --memory 128m --cpus 0.5 --pids-limit 64 --log-opt max-size=5m --log-opt max-file=2 --mount type=bind,src={HERE}/proxy.mjs,dst=/proxy.mjs,readonly {NODE} node /proxy.mjs
ExecStartPre=/usr/bin/docker network connect --alias massar-repair-proxy massar-repair-internal massar-repair-proxy
ExecStart=/usr/bin/docker start -a massar-repair-proxy
ExecStop=/usr/bin/docker stop -t 10 massar-repair-proxy
[Install]
WantedBy=multi-user.target
'''
    install_text(unit, '/etc/systemd/system/massar-auto-repair-proxy.service')
    run(['sudo', '-n', '/usr/bin/systemctl', 'daemon-reload'])
    run(['sudo', '-n', '/usr/bin/systemctl', 'enable', '--now', 'massar-auto-repair-proxy.service'])


def main():
    parser = argparse.ArgumentParser()
    mode = parser.add_mutually_exclusive_group(required=True)
    mode.add_argument('--dry-run', action='store_true')
    mode.add_argument('--yes', action='store_true')
    parser.add_argument('--rebind-only', action='store_true', help='Reuse verified image only when deployed application source is byte-identical')
    args = parser.parse_args()
    manifest = json.loads(Path('/opt/massar/current/manifest.json').read_text())
    if args.rebind_only:
        config = json.loads(Path('/etc/massar/auto-repair.json').read_text())
        old = json.loads((Path('/opt/massar/releases') / config['baseline_release'] / 'manifest.json').read_text())
        def application_paths(value):
            return {e['path']: e['sha256'] for e in value['sourcePaths'] if e['path'].startswith(('backend/', 'frontend/', 'worker/'))}
        assert application_paths(old) == application_paths(manifest), 'Application changed; rebuild and reverify the agent environment'
        assert not Path('/var/lib/massar/auto-repair/current.json').exists(), 'Active repair baseline requires reconciliation'
        if args.dry_run:
            print(json.dumps({'rebindFrom': config['baseline_release'], 'rebindTo': manifest['releaseId'], 'applicationSourceIdentical': True}))
            return
        source = source_snapshot(manifest)
        config.update(source_repository=str(source), source_ref=run(['git', 'rev-parse', 'HEAD'], source), baseline_release=manifest['releaseId'])
        install_text(json.dumps(config, indent=2) + '\n', '/etc/massar/auto-repair.json', '0644')
        print('Rebound to deployed release with byte-identical application source', flush=True)
        return
    for image in (NODE, SDK, POSTGRES, REDIS):
        run([*DOCKER, 'image', 'inspect', image])
    if args.dry_run:
        print(json.dumps({'baseline': manifest['releaseId'], 'baseImagesPresent': True,
            'steps': ['verify deployed source file hashes', 'create private source repository',
                      'build agent with pinned CLI, locked dependencies, Chromium and FFmpeg; outbound package restore during image build only',
                      'create isolated networks and fence host INPUT from their dedicated bridge',
                      'start restricted HTTPS proxy', 'write runner config'],
            'applicationRestarts': False, 'supervisorEnabled': False}))
        return
    source = source_snapshot(manifest)
    print('Verified source snapshot ready', flush=True)
    image = build_agent(source)
    print('Agent image ready: ' + image, flush=True)
    provision_proxy()
    config = {'api_url': 'https://api.massar-academy.net', 'token_file': '/etc/massar/auto-repair.token',
        'state_dir': '/var/lib/massar/auto-repair', 'source_repository': str(source),
        'source_ref': run(['git', 'rev-parse', 'HEAD'], source), 'baseline_release': manifest['releaseId'],
        'agent_image': image, 'agent_network': 'massar-repair-internal',
        'codex_home': str(PRIVATE / 'codex-home'), 'https_proxy': 'http://massar-repair-proxy:3128',
        'postgres_image': POSTGRES, 'redis_image': REDIS}
    sync_path = Path('/etc/massar/source-sync.json')
    if sync_path.exists():
        config['verified_dependencies_source'] = str(source)
        config['source_repository'] = json.loads(sync_path.read_text())['repository']
    install_text(json.dumps(config, indent=2) + '\n', '/etc/massar/auto-repair.json', '0644')
    print('Runtime prepared; supervisor activation awaits acceptance', flush=True)


if __name__ == '__main__':
    main()
