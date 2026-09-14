#!/usr/bin/env python3
"""Install the pinned official CLI on node-3 through the strict transport."""
import argparse
import json
import sys
from pathlib import Path

SCRIPTS = Path(__file__).resolve().parents[1] / 'production/scripts'
sys.path.insert(0, str(SCRIPTS))
from clusterctl import load_inventory, target
from ssh_transport import StrictSshTransport

REMOTE = r'''
import base64, hashlib, io, json, os, platform, subprocess, tarfile, urllib.request
from pathlib import Path
assert platform.machine() == "x86_64", "Pinned package requires x86_64"
version = "0.147.0"
with urllib.request.urlopen("https://registry.npmjs.org/@openai%2Fcodex/" + version + "-linux-x64", timeout=30) as response:
    metadata = json.load(response)
url = metadata["dist"]["tarball"]
assert url.startswith("https://registry.npmjs.org/@openai/codex/-/"), "Unexpected package origin"
with urllib.request.urlopen(url, timeout=180) as response:
    archive = response.read(350_000_001)
assert len(archive) <= 350_000_000, "Package exceeds budget"
actual = "sha512-" + base64.b64encode(hashlib.sha512(archive).digest()).decode()
assert actual == metadata["dist"]["integrity"], "Package integrity mismatch"
root = Path.home() / ".local/lib/massar-codex" / version
root.mkdir(parents=True, exist_ok=True, mode=0o700)
with tarfile.open(fileobj=io.BytesIO(archive), mode="r:gz") as bundle:
    members = [m for m in bundle.getmembers() if m.isfile() and m.name.endswith("/bin/codex")]
    assert len(members) == 1, "Unexpected native package layout"
    prefix = Path(members[0].name).parent.parent
    for member in bundle.getmembers():
        if not member.isfile() or not Path(member.name).is_relative_to(prefix):
            continue
        relative = Path(member.name).relative_to(prefix)
        assert ".." not in relative.parts, "Unsafe archive path"
        destination = root / relative
        assert destination.resolve().is_relative_to(root.resolve()), "Archive path escapes install root"
        destination.parent.mkdir(parents=True, exist_ok=True, mode=0o700)
        payload = bundle.extractfile(member).read()
        if destination.exists():
            assert destination.read_bytes() == payload, "Installed file differs from pinned package"
        else:
            destination.write_bytes(payload)
            destination.chmod(0o755 if member.mode & 0o111 else 0o644)
    binary = root / "bin/codex"
auth = Path.home() / ".local/share/massar-auto-repair/codex-home"
auth.mkdir(parents=True, exist_ok=True, mode=0o700)
print(json.dumps({"binary": str(binary), "codexHome": str(auth), "version": subprocess.check_output([str(binary), "--version"], text=True).strip(), "integrity": actual}))
'''

if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    mode = parser.add_mutually_exclusive_group(required=True)
    mode.add_argument('--dry-run', action='store_true')
    mode.add_argument('--yes', action='store_true')
    args = parser.parse_args()
    inventory = load_inventory(SCRIPTS.parent / 'inventory/production.yml', require_operator_files=True)
    if args.dry_run:
        print(json.dumps({'node': 'node-3', 'version': '0.147.0', 'origin': 'registry.npmjs.org/@openai/codex', 'integrity': 'npm sha512 verified', 'scope': 'dedicated user CLI/auth directories; no app restart or service activation'}))
    else:
        transport = StrictSshTransport(Path(inventory.cluster['known_hosts_file']), Path(inventory.cluster['identity_file']))
        receipt = transport.run(target(inventory, next(n for n in inventory.nodes if n.id == 'node-3')), ['python3', '-c', REMOTE], timeout_seconds=240)
        print(receipt.stdout)
