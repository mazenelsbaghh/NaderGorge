#!/usr/bin/env python3
"""Provision the optional desktop support service without changing operational data.

Secrets are read from private operator files and never printed or passed in argv.
HAProxy edits retain all existing routing and use compare-and-swap plus rollback.
"""
import argparse
import hashlib
import json
import shlex
import uuid
from pathlib import Path

from clusterctl import load_inventory, operator_transport, target
from deploy_release import RolloutLock

ROOT = Path(__file__).resolve().parents[3]
BASE = '/srv/massar-shared/center-desktop'
ROUTE = '''    acl massar_desktop_host hdr(host),lower,field(1,:) -m str api.massar-academy.net
    acl massar_desktop_path path_beg /v1/uploads /v1/updates/ /v1/releases/ /v1/admin/releases
    http-request set-log-level silent if massar_desktop_host massar_desktop_path
    use_backend massar_desktop_support if massar_desktop_host massar_desktop_path
'''
BACKEND = '''
backend massar_desktop_support
    mode http
    timeout server 310s
    server local_support 127.0.0.1:43880
'''
UNIT = '''[Unit]
Description=Massar private desktop support
After=network.target
RequiresMountsFor=/srv/massar-shared

[Service]
User=massar-ops
Group=massar-ops
ExecStart=/opt/massar-support/massar-support
EnvironmentFile=/etc/massar/center-support.env
Restart=on-failure
RestartSec=5
UMask=0077
NoNewPrivileges=true
PrivateTmp=true
ProtectSystem=strict
ProtectHome=true
ReadWritePaths=/srv/massar-shared/center-desktop/uploads
ReadOnlyPaths=/srv/massar-shared/center-desktop/releases
RestrictAddressFamilies=AF_UNIX AF_INET AF_INET6

[Install]
WantedBy=multi-user.target
'''

PREPARE = r'''
import hashlib,json,os,re,subprocess
from pathlib import Path
assert subprocess.check_output(['id','-u','massar-ops'],text=True).strip()=='1000'
assert subprocess.check_output(['findmnt','-n','-o','FSTYPE','/srv/massar-shared'],text=True).strip()=='fuse.glusterfs'
p=Path('/etc/haproxy/haproxy.cfg'); old=p.read_text()
if 'backend massar_desktop_support' in old:
 assert ROUTE in old and BACKEND in old
 new=old
else:
 start=old.index('frontend massar_ingress\n')
 end=re.search(r'\n(?:frontend|backend|listen|defaults|global)\s',old[start+1:])
 end=start+1+end.start() if end else len(old)
 section=old[start:end]
 marker='    default_backend massar_nodes\n'
 assert section.count(marker)==1
 section=section.replace(marker,ROUTE+marker)
 new=old[:start]+section+old[end:]+BACKEND
sha=lambda s:hashlib.sha256(s.encode()).hexdigest()
directory=Path('/tmp/massar-center-support-'+RUN)
directory.mkdir(mode=0o700)
for name,content in [('next.cfg',new),('previous.cfg',old),('support.service',UNIT)]:
 path=directory/name;path.write_text(content);path.chmod(0o600)
r=subprocess.run(['/usr/sbin/haproxy','-c','-f',str(directory/'next.cfg')],capture_output=True)
if r.returncode: raise SystemExit('HAProxy candidate failed validation')
print(json.dumps({'stage':str(directory),'old':sha(old),'new':sha(new),'changed':old!=new}))
'''

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    mode = parser.add_mutually_exclusive_group(required=True)
    mode.add_argument('--dry-run', action='store_true')
    mode.add_argument('--yes', action='store_true')
    parser.add_argument('--binary', type=Path, required=True)
    parser.add_argument('--private-dir', type=Path, required=True)
    args = parser.parse_args()
    for name in ('service.env', 'backend.env'):
        p = args.private_dir / name
        if not p.is_file() or p.is_symlink() or p.stat().st_mode & 0o077:
            raise SystemExit('Private configuration missing or permissions unsafe')
    binary_hash = hashlib.sha256(args.binary.read_bytes()).hexdigest()
    inv = load_inventory(ROOT/'deploy/production/inventory/production.yml', require_operator_files=True)
    transport = operator_transport(inv)
    lock = RolloutLock(transport, target(inv,inv.nodes[0]), str(uuid.uuid4()))
    if args.yes: lock.acquire()
    try:
        for node in reversed(inv.nodes):
            remote=target(inv,node)
            code='RUN='+repr(uuid.uuid4().hex)+'\nROUTE='+repr(ROUTE)+'\nBACKEND='+repr(BACKEND)+'\nUNIT='+repr(UNIT)+'\n'+PREPARE
            prepared=json.loads(transport.run(remote,['python3','-c',code],timeout_seconds=40).stdout)
            stage=prepared['stage']
            print(json.dumps({'node':node.id,'mode':'apply' if args.yes else 'preview','binarySha256':binary_hash,'ingressChanged':prepared['changed']}),flush=True)
            try:
                if not args.yes: continue
                for source,name in [(args.binary,'binary'),(args.private_dir/'service.env','service.env'),(args.private_dir/'backend.env','backend.env')]:
                    transport.copy(remote,source,stage+'/'+name)
                command=f'''set -euo pipefail
test "$(sha256sum {stage}/binary | cut -d' ' -f1)" = {binary_hash}
test "$(sha256sum /etc/haproxy/haproxy.cfg | cut -d' ' -f1)" = {prepared['old']}
sudo -n /usr/bin/install -d -m 0700 -o massar-ops -g massar-ops {BASE} {BASE}/uploads {BASE}/releases
sudo -n /usr/bin/install -d -m 0755 /opt/massar-support
sudo -n /usr/bin/install -m 0755 {stage}/binary /opt/massar-support/massar-support.next
sudo -n /usr/bin/mv /opt/massar-support/massar-support.next /opt/massar-support/massar-support
sudo -n /usr/bin/install -m 0600 {stage}/service.env /etc/massar/center-support.env
sudo -n /usr/bin/install -m 0600 {stage}/backend.env /etc/massar/center-desktop-backend.env
sudo -n /usr/bin/install -m 0644 {stage}/support.service /etc/systemd/system/massar-support.service
sudo -n /usr/bin/systemctl daemon-reload
sudo -n /usr/bin/systemctl enable --now massar-support.service
sudo -n /usr/bin/systemctl restart massar-support.service
systemctl is-active --quiet massar-support.service
for i in 1 2 3 4 5; do
 code=$(curl -s -o /dev/null -w '%{{http_code}}' -H 'X-Forwarded-Proto: https' http://127.0.0.1:43880/v1/uploads || true)
 test "$code" != 401 || break
 sleep 1
done
test "$code" = 401
sudo -n /usr/bin/install -m 0644 {stage}/next.cfg /etc/haproxy/haproxy.cfg
if ! sudo -n /usr/bin/systemctl reload haproxy; then
 sudo -n /usr/bin/install -m 0644 {stage}/previous.cfg /etc/haproxy/haproxy.cfg
 sudo -n /usr/bin/systemctl reload haproxy
 exit 1
fi
curl -fsS --max-time 10 -H 'Host: massar-academy.net' http://127.0.0.1:8088/__node_ready >/dev/null
'''
                transport.run(remote,['bash','-lc',command],timeout_seconds=90)
            finally:
                transport.run(remote,['bash','-lc','rm -rf -- '+shlex.quote(stage)],timeout_seconds=15)
    finally:
        if args.yes: lock.release()

if __name__=='__main__': main()
