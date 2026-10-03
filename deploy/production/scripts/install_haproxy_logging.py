#!/usr/bin/env python3
"""Remove HTTP query strings from ingress logs without replacing live routing repairs."""
from __future__ import annotations

import argparse
import json
import shlex
import uuid
from pathlib import Path

from clusterctl import load_inventory, operator_transport, target
from deploy_release import RolloutLock

ROOT = Path(__file__).resolve().parents[3]
DESTINATION = "/etc/haproxy/haproxy.cfg"
FORMAT = ('    log-format "%ci:%cp [%tr] %ft %b/%s %TR/%Tw/%Tc/%Tr/%Ta %ST %B %tsc '
          '%ac/%fc/%bc/%sc/%rc %sq/%bq %{+Q}HM %{+Q}HP %{+Q}HV"')

# Only the frontend's logging directive changes. Existing routes, timeouts,
# database writer checks and any operator repairs remain byte-for-byte intact.
PREPARE = r'''
import hashlib,json,os,re,subprocess
from pathlib import Path
destination=Path('/etc/haproxy/haproxy.cfg')
old=destination.read_text()
start=old.index('frontend massar_ingress\n')
match=re.search(r'\n(?:frontend|backend|listen|defaults|global)\s',old[start+1:])
end=start+1+match.start() if match else len(old)
section=old[start:end]
desired=FORMAT+'\n'
if desired in section:
    new=old
else:
    if section.count('    option httplog\n')!=1 or 'log-format ' in section:
        raise SystemExit('Unexpected ingress log policy; inspect before replacement')
    section=section.replace('    option httplog\n',desired)
    new=old[:start]+section+old[end:]
sha=lambda text:hashlib.sha256(text.encode()).hexdigest()
staged=Path('/tmp/massar-haproxy-logging-'+sha(new)+'.cfg')
backup=Path('/tmp/massar-haproxy-logging-'+sha(old)+'.previous')
for path,content in ((staged,new),(backup,old)):
    fd=os.open(path,os.O_WRONLY|os.O_CREAT|os.O_TRUNC|os.O_NOFOLLOW,0o600)
    with os.fdopen(fd,'w') as output:output.write(content)
result=subprocess.run(['/usr/sbin/haproxy','-c','-f',str(staged)],capture_output=True,text=True,timeout=10)
if result.returncode:
    staged.unlink(missing_ok=True);backup.unlink(missing_ok=True)
    raise SystemExit('HAProxy candidate validation failed')
print(json.dumps({'previousSha256':sha(old),'sha256':sha(new),'staged':str(staged),'backup':str(backup),'changed':new!=old}))
'''


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    mode = parser.add_mutually_exclusive_group(required=True)
    mode.add_argument('--dry-run', action='store_true')
    mode.add_argument('--yes', action='store_true')
    args = parser.parse_args()
    inventory = load_inventory(ROOT / 'deploy/production/inventory/production.yml', require_operator_files=True)
    transport = operator_transport(inventory)
    lock = RolloutLock(transport, target(inventory, inventory.nodes[0]), str(uuid.uuid4()))
    if args.yes:
        lock.acquire()
    try:
        for node in reversed(inventory.nodes):
            remote = target(inventory, node)
            script = 'FORMAT=' + repr(FORMAT) + '\n' + PREPARE
            prepared = transport.run(remote, ['python3', '-c', script], timeout_seconds=60)
            candidate = json.loads(prepared.stdout)
            staged = shlex.quote(candidate['staged'])
            backup = shlex.quote(candidate['backup'])
            try:
                print(json.dumps({'node': node.id, 'sha256': candidate['sha256'],
                                  'changed': candidate['changed'], 'action': 'install' if args.yes else 'preview'}), flush=True)
                if not args.yes or not candidate['changed']:
                    continue
                command = (
                    'set -euo pipefail; '
                    f'test "$(sha256sum {DESTINATION} | cut -d\' \' -f1)" = {candidate["previousSha256"]}; '
                    'test "$(systemctl show haproxy -p CanReload --value)" = yes; '
                    f'sudo -n /usr/bin/install -m 0644 -o root -g root {staged} {DESTINATION}; '
                    'if ! sudo -n /usr/bin/systemctl reload haproxy; then '
                    f'sudo -n /usr/bin/install -m 0644 -o root -g root {backup} {DESTINATION}; '
                    'sudo -n /usr/bin/systemctl reload haproxy; exit 1; fi; '
                    f'test "$(sha256sum {DESTINATION} | cut -d\' \' -f1)" = {candidate["sha256"]}; '
                    'systemctl is-active --quiet haproxy; '
                    'curl -fsS --max-time 10 -H "Host: massar-academy.net" '
                    'http://127.0.0.1:8088/__node_ready >/dev/null'
                )
                transport.run(remote, ['bash', '-lc', command], timeout_seconds=40)
            finally:
                transport.run(remote, ['bash', '-lc', f'rm -f {staged} {backup}'], timeout_seconds=15)
    finally:
        if args.yes:
            lock.release()


if __name__ == '__main__':
    main()
