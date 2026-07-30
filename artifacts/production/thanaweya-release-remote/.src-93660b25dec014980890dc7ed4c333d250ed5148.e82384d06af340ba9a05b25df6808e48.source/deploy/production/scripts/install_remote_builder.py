#!/usr/bin/env python3
"""Install the fixed root-owned remote-builder helper on node-3 only."""
from __future__ import annotations
import argparse, hashlib, sys
from pathlib import Path
from clusterctl import load_inventory
from ssh_transport import SshTarget, StrictSshTransport

ROOT=Path(__file__).resolve().parents[3]
HELPER=ROOT/"deploy/production/scripts/remote_builder_executor.py"
SUDOERS=ROOT/"deploy/production/config/sudoers/massar-remote-builder"
REMOTE_HELPER="/usr/local/sbin/massar-remote-builder"
REMOTE_SUDOERS="/etc/sudoers.d/massar-remote-builder"

class RemoteBuilderInstallError(RuntimeError): pass
def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()
def target(inventory):
    nodes=tuple(inventory.nodes)
    if len(nodes)!=3 or nodes[2].id!="node-3" or "builder" not in nodes[2].roles or any("builder" in node.roles for node in nodes[:2]):
        raise RemoteBuilderInstallError("installer requires node-3 as the exactly one builder")
    return SshTarget("node-3",nodes[2].public_address,inventory.cluster["ssh_user"])
def install(inventory,transport) -> None:
    if not HELPER.is_file() or not SUDOERS.is_file(): raise RemoteBuilderInstallError("reviewed helper assets are missing")
    remote=target(inventory); helper_sha=digest(HELPER); sudoers_sha=digest(SUDOERS)
    temporary_helper="/tmp/massar-remote-builder.py"; temporary_sudoers="/tmp/massar-remote-builder.sudoers"
    transport.copy(remote,HELPER,temporary_helper,timeout_seconds=120)
    transport.copy(remote,SUDOERS,temporary_sudoers,timeout_seconds=120)
    script="set -euo pipefail; " + "trap 'rm -f /tmp/massar-remote-builder.py /tmp/massar-remote-builder.sudoers' EXIT; " + "test \"$(cat /etc/massar/cluster-id)\" = massar-production; if ! test -e /etc/massar/node-id; then printf '%s\\n' node-3 | sudo /usr/bin/tee /etc/massar/node-id >/dev/null; sudo /usr/bin/chmod 0644 /etc/massar/node-id; fi; test \"$(cat /etc/massar/node-id)\" = node-3; test \"$(stat -c '%U:%G:%a' /etc/massar/node-id)\" = root:root:644; " + f"printf '%s  %s\\n' '{helper_sha}' '{temporary_helper}' | sha256sum -c -; " + f"printf '%s  %s\\n' '{sudoers_sha}' '{temporary_sudoers}' | sha256sum -c -; " + f"/usr/sbin/visudo -cf {temporary_sudoers}; sudo /usr/bin/install -m 0755 -o root -g root {temporary_helper} {REMOTE_HELPER}; sudo /usr/bin/install -m 0440 -o root -g root {temporary_sudoers} {REMOTE_SUDOERS}; test \"$(stat -c '%U:%G:%a' {REMOTE_HELPER})\" = root:root:755; sudo -n -l {REMOTE_HELPER} | grep -F {REMOTE_HELPER} >/dev/null"
    transport.run(remote,("bash","-lc",script),timeout_seconds=180)
def arguments():
    parser=argparse.ArgumentParser(description=__doc__); parser.add_argument("--inventory",required=True,type=Path); parser.add_argument("--known-hosts",required=True,type=Path); parser.add_argument("--identity",required=True,type=Path); parser.add_argument("--node",required=True,choices=("node-3",)); parser.add_argument("--dry-run",action="store_true"); parser.add_argument("--yes",action="store_true"); return parser.parse_args()
def main() -> int:
    args=arguments(); inventory=load_inventory(args.inventory)
    if args.dry_run: print('{"status":"dry-run","node":"node-3","assets":["helper","sudoers"],"nodeMarker":"initialize-if-absent"}'); return 0
    if not args.yes: raise RemoteBuilderInstallError("installer requires --yes or --dry-run")
    install(inventory,StrictSshTransport(args.known_hosts,args.identity)); print('{"status":"success","node":"node-3"}'); return 0
if __name__=="__main__":
    try: raise SystemExit(main())
    except (RemoteBuilderInstallError,OSError) as exc: print(f"remote builder install failed: {exc}",file=sys.stderr); raise SystemExit(6)
