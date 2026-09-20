#!/usr/bin/env python3
"""Provision the dedicated supervisor; never enable repair as an install side effect."""
import argparse
import hashlib
import json
import os
import secrets
import subprocess
import sys
import tarfile
import tempfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'deploy/production/scripts'))
from clusterctl import load_inventory, operator_transport, target
from ssh_transport import StrictSshTransport

NODE_IMAGE = 'node@sha256:f5a0871ab03b035c58bdb3007c3d177b001c2145c18e81817b71624dcf7d8bff'
DOTNET_IMAGE = 'mcr.microsoft.com/dotnet/sdk@sha256:712ffb3919c095da6129ba5ed9d0e7660ab45b966140819d1d58e081bef64293'
INSTALL = '/opt/massar-auto-repair'
PRIVATE = '/home/massar-ops/.local/share/massar-auto-repair'


class ProvisionTransport(StrictSshTransport):
    """Reuse verified SSH connections to avoid many connection handshakes."""
    def base_args(self):
        sockets = Path('/tmp') / ('massar-ssh-' + str(os.getuid()))
        sockets.mkdir(mode=0o700, exist_ok=True)
        if sockets.is_symlink() or sockets.stat().st_mode & 0o077 or sockets.stat().st_uid != os.getuid():
            raise RuntimeError('Unsafe SSH socket directory')
        return [*super().base_args(), '-o', 'ControlMaster=auto', '-o', 'ControlPersist=120',
                '-o', 'ControlPath=' + str(sockets / '%C')]

    def copy(self, host, source, destination, timeout_seconds=120):
        if not source.is_file() or not destination.startswith(PRIVATE + '/'):
            raise ValueError('Provisioning copies require a private staging destination')
        subprocess.run(['scp', *self.base_args()[1:], str(source), f'{host.user}@{host.address}:{destination}'],
                       capture_output=True, text=True, check=True, timeout=timeout_seconds)


def provision_transport(inv):
    return ProvisionTransport(Path(inv.cluster['known_hosts_file']), Path(inv.cluster['identity_file']), 30)


def install_file(ssh, host, local, destination, owner='massar-ops', mode='0600'):
    staged = PRIVATE + '/stage-' + secrets.token_hex(8)
    ssh.copy(host, local, staged)
    try:
        observed = ssh.run(host, ['sha256sum', staged]).stdout.split()[0]
        if observed != hashlib.sha256(local.read_bytes()).hexdigest():
            raise RuntimeError('Transferred file hash mismatch')
        ssh.run(host, ['sudo', '-n', '/usr/bin/install', '-o', owner, '-m', mode, staged, destination])
    finally:
        ssh.run(host, ['rm', '-f', staged])


def provision_credentials(inv, ssh, builder):
    # A separate machine token, unrelated to ChatGPT authentication. Never print it.
    local = Path.home() / '.config/massar/auto-repair'
    local.mkdir(mode=0o700, parents=True, exist_ok=True)
    if local.is_symlink() or local.stat().st_mode & 0o077:
        raise RuntimeError('Insecure local credential directory')
    token = local / 'runner.token'
    if not token.exists():
        with os.fdopen(os.open(token, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600), 'w') as stream:
            stream.write(secrets.token_hex(48) + '\n')
    if token.is_symlink() or token.stat().st_mode & 0o077 or len(token.read_text().strip()) < 32:
        raise RuntimeError('Invalid runner credential')
    env = local / 'backend.env'
    with os.fdopen(os.open(env, os.O_WRONLY | os.O_CREAT | os.O_TRUNC, 0o600), 'w') as stream:
        stream.write('AutoRepair__RunnerToken=' + token.read_text().strip() + '\n')
    for node in inv.nodes:
        host = target(inv, node)
        ssh.run(host, ['install', '-d', '-m', '0700', PRIVATE])
        install_file(ssh, host, env, '/etc/massar/auto-repair-backend.env', 'root')
        print(node.id + ': backend-only credential installed', flush=True)
    install_file(ssh, builder, token, '/etc/massar/auto-repair.token')
    # Generate on node-3. Only the public key and operator-verified host pins travel.
    script = f'''from pathlib import Path
import subprocess
p=Path('{PRIVATE}/operations_ed25519')
if not p.exists(): subprocess.run(['ssh-keygen','-q','-t','ed25519','-N','','-C','massar-auto-repair','-f',str(p)],check=True)
assert not p.is_symlink() and not p.stat().st_mode & 0o077
print(p.with_suffix('.pub').read_text().strip())
'''
    public = ssh.run(builder, ['python3', '-c', script]).stdout.strip()
    if not public.startswith('ssh-ed25519 ') or len(public.split()) != 3:
        raise RuntimeError('Invalid generated public key')
    for node in inv.nodes:
        script = '''from pathlib import Path
import sys,os
p=Path.home()/'.ssh'; p.mkdir(mode=0o700,exist_ok=True)
f=p/'authorized_keys'; assert not f.is_symlink()
old=f.read_text() if f.exists() else ''
key=sys.argv[1]
if key.split()[1] not in old:
 with f.open('a') as out: out.write(('\\n' if old and not old.endswith('\\n') else '')+'restrict '+key+'\\n')
os.chmod(f,0o600)
'''
        ssh.run(target(inv, node), ['python3', '-c', script, public])
    install_file(ssh, builder, Path(inv.cluster['known_hosts_file']), PRIVATE + '/known_hosts')
    print('Dedicated node-side identity and verified host pins installed', flush=True)


def provision_files(inv, ssh, builder):
    files = ['runner.py', 'policy.py', 'collector.py', 'release.py', 'verification.py', 'verify.sh',
             'source_baseline.py', 'sync_monitor.py', 'review-schema.json', 'SKILL.md', 'proxy.mjs', 'fonts.mjs', 'Dockerfile', 'prepare_runtime.py', 'acceptance.py']
    with tempfile.TemporaryDirectory() as directory:
        env = Path(directory) / 'env'
        env.write_text(f'MASSAR_KNOWN_HOSTS_FILE={PRIVATE}/known_hosts\nMASSAR_SSH_IDENTITY_FILE={PRIVATE}/operations_ed25519\n')
        archive = Path(directory) / 'files.tar'
        with tarfile.open(archive, 'w') as bundle:
            for name in [*files, 'massar-auto-repair.service']:
                bundle.add(ROOT / 'deploy/auto-repair' / name, arcname=name)
            bundle.add(ROOT / 'deploy/production/scripts/source_sync.py', arcname='source_sync.py')
            bundle.add(ROOT / 'deploy/production/scripts/startup_check.py', arcname='startup_check.py')
            bundle.add(env, arcname='env')
        staged = PRIVATE + '/files-' + secrets.token_hex(8) + '.tar'
        ssh.copy(builder, archive, staged)
        digest = hashlib.sha256(archive.read_bytes()).hexdigest()
        script = '''import hashlib,subprocess,sys,tarfile,tempfile
from pathlib import Path
p=Path(sys.argv[1]); assert hashlib.sha256(p.read_bytes()).hexdigest()==sys.argv[2]
def run(args): subprocess.run(args,check=True,capture_output=True)
run(['sudo','-n','/usr/bin/install','-d','-o','root','-m','0755','/opt/massar-auto-repair'])
run(['sudo','-n','/usr/bin/install','-d','-o','massar-ops','-m','0700','/var/lib/massar/auto-repair'])
with tempfile.TemporaryDirectory(dir=p.parent) as directory:
 with tarfile.open(p) as bundle:
  assert all(m.isfile() and '/' not in m.name and m.name not in ('.','..') for m in bundle.getmembers())
  bundle.extractall(directory,filter='data')
 for f in Path(directory).iterdir():
  dest='/opt/massar-auto-repair/'+f.name; mode='0644'
  if f.name=='env': dest='/etc/massar/auto-repair.env'; mode='0600'
  if f.name=='massar-auto-repair.service': dest='/etc/systemd/system/'+f.name
  run(['sudo','-n','/usr/bin/install','-o','root','-m',mode,str(f),dest])
run(['sudo','-n','/usr/bin/systemctl','daemon-reload'])
p.unlink()
'''
        ssh.run(builder, ['python3', '-c', script, staged, digest], timeout_seconds=90)
    print('Supervisor files installed; activation state unchanged', flush=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    mode = parser.add_mutually_exclusive_group(required=True)
    mode.add_argument('--dry-run', action='store_true')
    mode.add_argument('--yes', action='store_true')
    args = parser.parse_args()
    inv = load_inventory(ROOT / 'deploy/production/inventory/production.yml', require_operator_files=True)
    if args.dry_run:
        print(json.dumps({'node': 'node-3', 'steps': ['dedicated machine token in backend-only env files on three nodes',
              'new node-side SSH identity; public authorization on three nodes', 'verified host pins',
              'root-owned supervisor files and disabled systemd unit'], 'applicationRestart': False,
              'autoRepairEnabled': False, 'autoDeployEnabled': False}))
        return
    ssh = provision_transport(inv)
    builder = target(inv, next(node for node in inv.nodes if node.id == 'node-3'))
    provision_credentials(inv, ssh, builder)
    provision_files(inv, ssh, builder)


if __name__ == '__main__':
    main()
