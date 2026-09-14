#!/usr/bin/env python3
"""Install direct WireGuard image transfer without changing application services."""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import subprocess
import sys
import tempfile
from pathlib import Path

from clusterctl import load_inventory, redact
from ssh_transport import SshTarget, StrictSshTransport


SOURCE = Path(__file__).with_name("node_image_transfer.py")
REMOTE_HELPER = "/home/massar-ops/.local/libexec/massar-node-image-transfer.py"
KEY_COMMENT = "massar-node-image-transfer"
PUBLIC_KEY_RE = re.compile(r"ssh-ed25519 [A-Za-z0-9+/]+={0,2}(?: [^\r\n]*)?")


def target(inventory, node) -> SshTarget:
    return SshTarget(node.id, node.public_address, inventory.cluster["ssh_user"])


def pinned_receiver_hosts(inventory, known_hosts: Path) -> str:
    pins = []
    for node in inventory.nodes[:2]:
        lookup = subprocess.run(["ssh-keygen", "-F", node.public_address, "-f", str(known_hosts)],
                                check=True, capture_output=True, text=True)
        keys = [line.split() for line in lookup.stdout.splitlines() if line and not line.startswith("#")]
        ed25519 = {fields[2] for fields in keys if len(fields) >= 3 and fields[1] == "ssh-ed25519"}
        if len(ed25519) != 1:
            raise ValueError(f"{node.id} requires exactly one operator-pinned Ed25519 host key")
        pins.append(f"{node.id} ssh-ed25519 {ed25519.pop()}")
    return "\n".join(pins) + "\n"


def prepare_builder_key(transport, builder: SshTarget) -> str:
    script = """
set -euo pipefail
test "$(id -un)" = massar-ops
test "$HOME" = /home/massar-ops
test "$(cat /etc/massar/cluster-id)" = massar-production
test ! -L "$HOME/.ssh"
install -d -m 0700 "$HOME/.ssh"
key="$HOME/.ssh/massar-image-transfer-ed25519"
test ! -L "$key"
if ! test -e "$key"; then
  test ! -e "$key.pub"
  ssh-keygen -q -t ed25519 -N '' -C massar-node-image-transfer -f "$key"
fi
test "$(stat -c '%U:%a' "$key")" = massar-ops:600
ssh-keygen -y -f "$key"
"""
    public_key = transport.run(builder, ("bash", "-lc", script), timeout_seconds=30).stdout.strip()
    if not PUBLIC_KEY_RE.fullmatch(public_key):
        raise ValueError("builder did not return an Ed25519 public key")
    return public_key


def receiver_authorization(public_key: str, builder_address: str) -> str:
    key_type, key_body = public_key.split()[:2]
    command = f"/usr/bin/python3 -I {REMOTE_HELPER} receive"
    return f'restrict,from="{builder_address}",command="{command}" {key_type} {key_body} {KEY_COMMENT}'


def install_script(stage: str, payload_sha: str, helper_sha: str) -> str:
    # Only hashes and a generated staging path are embedded. Key material and
    # configuration travel as files through the operator's pinned SSH session.
    return f"""
import hashlib,json,os,pathlib,pwd,tempfile
stage=pathlib.Path({stage!r})
home=pathlib.Path.home()
if pwd.getpwuid(os.getuid()).pw_name!='massar-ops' or str(home)!='/home/massar-ops':
    raise SystemExit('installer must run as massar-ops in its configured home')
if pathlib.Path('/etc/massar/cluster-id').read_text().strip()!='massar-production':
    raise SystemExit('wrong cluster marker')
def read_verified(name,expected):
    path=stage/name
    if path.is_symlink() or not path.is_file() or hashlib.sha256(path.read_bytes()).hexdigest()!=expected:
        raise SystemExit('installer payload checksum mismatch')
    return path.read_bytes()
payload=json.loads(read_verified('payload.json',{payload_sha!r}))
helper=read_verified('node_image_transfer.py',{helper_sha!r})
for relative in ('.local','.local/libexec','.config','.ssh'):
    directory=home/relative
    if directory.is_symlink(): raise SystemExit('unsafe installation directory')
    directory.mkdir(mode=0o700,exist_ok=True)
    if directory.stat().st_uid!=os.getuid() or directory.stat().st_mode & 0o022:
        raise SystemExit('installation directory is writable by another user')
def publish(relative,content,mode):
    path=home/relative
    if path.is_symlink(): raise SystemExit('unsafe installation file')
    if path.exists() and (not path.is_file() or path.stat().st_uid!=os.getuid()):
        raise SystemExit('installation file is not owned and regular')
    fd,temporary=tempfile.mkstemp(prefix='.massar-transfer-',dir=path.parent)
    try:
        with os.fdopen(fd,'wb') as output:
            os.fchmod(output.fileno(),mode); output.write(content); output.flush(); os.fsync(output.fileno())
        os.replace(temporary,path)
    finally:
        if os.path.exists(temporary): os.unlink(temporary)
publish('.local/libexec/massar-node-image-transfer.py',helper,0o700)
publish('.config/massar-image-transfer.json',json.dumps(payload['configuration']).encode(),0o600)
if payload['configuration']['nodeId']=='node-3':
    publish('.ssh/massar-image-transfer-known-hosts',payload['knownHosts'].encode(),0o600)
else:
    path=home/'.ssh/authorized_keys'
    if path.is_symlink() or (path.exists() and (not path.is_file() or path.stat().st_uid!=os.getuid())):
        raise SystemExit('unsafe authorized_keys')
    lines=path.read_text().splitlines() if path.exists() else []
    managed=[line for line in lines if line.split()[-1:]==[{KEY_COMMENT!r}]]
    if len(managed)>1: raise SystemExit('duplicate managed image transfer keys')
    authorization=payload['authorization']
    public_key=authorization.split(' ssh-ed25519 ',1)[1].split()[0]
    if any(public_key in line and line not in managed for line in lines):
        raise SystemExit('transfer identity already has unmanaged authorization')
    retained=[line for line in lines if line not in managed]
    publish('.ssh/authorized_keys',('\\n'.join([*retained,authorization])+'\\n').encode(),0o600)
print(json.dumps({{'node':payload['configuration']['nodeId'],'status':'installed','helperSha256':{helper_sha!r}}}))
"""


def install_node(transport, node_target: SshTarget, payload: dict) -> dict:
    stage = transport.run(node_target, ("mktemp", "-d", "/tmp/massar-image-transfer-XXXXXXXXXX")).stdout.strip()
    if not re.fullmatch(r"/tmp/massar-image-transfer-[A-Za-z0-9]{10}", stage):
        raise ValueError("invalid remote installer staging directory")
    try:
        with tempfile.TemporaryDirectory(prefix="massar-image-transfer-") as local:
            payload_path = Path(local) / "payload.json"
            payload_path.write_text(json.dumps(payload))
            payload_sha = hashlib.sha256(payload_path.read_bytes()).hexdigest()
            helper_sha = hashlib.sha256(SOURCE.read_bytes()).hexdigest()
            transport.copy(node_target, SOURCE, f"{stage}/node_image_transfer.py")
            transport.copy(node_target, payload_path, f"{stage}/payload.json")
            script = install_script(stage, payload_sha, helper_sha)
            completed = transport.run(node_target, ("python3", "-I", "-c", script), timeout_seconds=60)
            return json.loads(completed.stdout)
    finally:
        transport.run(node_target, ("rm", "-rf", "--", stage))


def install(inventory, transport, known_hosts: Path) -> list[dict]:
    if inventory.cluster["ssh_user"] != "massar-ops":
        raise ValueError("direct image transfer requires massar-ops")
    hosts = pinned_receiver_hosts(inventory, known_hosts)
    builder = inventory.nodes[2]
    public_key = prepare_builder_key(transport, target(inventory, builder))
    authorization = receiver_authorization(public_key, builder.overlay_address)
    installed = []
    for node in (builder, *inventory.nodes[:2]):
        config = {"nodeId": node.id, "builderAddress": builder.overlay_address,
                  "targets": {receiver.id: receiver.overlay_address for receiver in inventory.nodes[:2]}}
        payload = {"configuration": config, "knownHosts": hosts, "authorization": authorization}
        installed.append(install_node(transport, target(inventory, node), payload))
    return installed


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--inventory", required=True, type=Path)
    parser.add_argument("--known-hosts", required=True, type=Path)
    parser.add_argument("--identity", required=True, type=Path)
    mode = parser.add_mutually_exclusive_group(required=True)
    mode.add_argument("--dry-run", action="store_true")
    mode.add_argument("--yes", action="store_true")
    args = parser.parse_args()
    inventory = load_inventory(args.inventory)
    if args.dry_run:
        pinned_receiver_hosts(inventory, args.known_hosts)
        print(json.dumps({"status": "dry-run", "nodes": [node.id for node in inventory.nodes],
                          "changes": ["node-3 dedicated identity", "pinned internal host keys", "receive-only key authorization", "user-owned transfer helper"],
                          "applicationRestarts": False, "sshAttempted": False}))
        return
    transport = StrictSshTransport(args.known_hosts, args.identity)
    print(json.dumps({"status": "success", "installed": install(inventory, transport, args.known_hosts)}))


if __name__ == "__main__":
    try:
        main()
    except (OSError, ValueError, subprocess.SubprocessError, RuntimeError) as exc:
        print(redact(f"node image transfer installation failed: {type(exc).__name__}: {exc}"), file=sys.stderr)
        raise SystemExit(6)
