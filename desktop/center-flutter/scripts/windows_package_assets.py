"""Stage only public client assets, restoring the exact pubspec after packaging."""
import argparse
import base64
import hashlib
import json
import os
from pathlib import Path
import re
import stat
import struct
import tempfile


JOURNAL_NAME = ".massar-windows-package.json"
LOCK_NAME = ".massar-windows-build.lock"
MARKER = b"# Temporary Massar Windows client asset manifest; restore through windows_package_assets.py.\n"
CLIENT_ASSETS = frozenset({
    "assets/logo.svg", "assets/logo-light.svg",
    "assets/fonts/Tajawal-Regular.ttf", "assets/fonts/Tajawal-Medium.ttf",
    "assets/fonts/Tajawal-Bold.ttf",
})


def digest(content):
    return hashlib.sha256(content).hexdigest()


def assert_clean(root):
    if (root / JOURNAL_NAME).exists() or (root / "pubspec.yaml").read_bytes().startswith(MARKER):
        raise RuntimeError(
            "A Windows client asset staging is active or interrupted. After the build stops, "
            "run: python scripts/windows_package_assets.py recover")


def client_pubspec(original):
    """Edit the project's explicit asset list only; reject unfamiliar YAML shapes."""
    text = original.decode("utf-8")
    lines = text.splitlines(keepends=True)
    starts = [index for index, line in enumerate(lines) if line.rstrip() == "  assets:"]
    if len(starts) != 1 or sum(line.rstrip() == "flutter:" for line in lines) != 1:
        raise ValueError("Expected one explicit Flutter assets block.")
    start = starts[0] + 1
    end = start
    while end < len(lines) and (not lines[end].strip() or lines[end].startswith("    ")):
        end += 1
    kept, seen = [], set()
    for line in lines[start:end]:
        if not line.strip() or line.lstrip().startswith("#"):
            kept.append(line)
            continue
        entry = re.fullmatch(r"    - (assets/[A-Za-z0-9_./-]+)\s*(?:#.*)?", line.rstrip())
        if entry is None or ".." in entry[1].split("/") or entry[1] in seen:
            raise ValueError("Client packaging requires unique explicit asset paths.")
        seen.add(entry[1])
        if entry[1] in CLIENT_ASSETS:
            kept.append(line)
    fonts = re.findall(r"^\s*- asset: (\S+)\s*$", text, re.M)
    if len(fonts) != 3 or set(fonts) != {path for path in CLIENT_ASSETS if path.endswith(".ttf")}:
        raise ValueError("Review changed fonts before building the client package.")
    if not {"assets/logo.svg", "assets/logo-light.svg"}.issubset(seen):
        raise ValueError("The client needs both public logos.")
    return MARKER + "".join(lines[:start] + kept + lines[end:]).encode("utf-8")


def atomic_write(path, content, mode=None):
    descriptor, temporary = tempfile.mkstemp(prefix=".massar-package-", dir=path.parent)
    try:
        with os.fdopen(descriptor, "wb") as stream:
            stream.write(content)
            stream.flush()
            os.fsync(stream.fileno())
        if mode is not None:
            os.chmod(temporary, mode)
        os.replace(temporary, path)
    finally:
        if os.path.exists(temporary):
            os.unlink(temporary)


def begin(root, owner):
    assert_clean(root)
    pubspec = root / "pubspec.yaml"
    original = pubspec.read_bytes()
    staged = client_pubspec(original)
    mode = stat.S_IMODE(pubspec.stat().st_mode)
    journal = {
        "version": 1, "owner": owner, "mode": mode,
        "original": base64.b64encode(original).decode("ascii"),
        "originalSha256": digest(original), "stagedSha256": digest(staged),
    }
    # The recovery copy reaches disk before the working manifest can change.
    atomic_write(root / JOURNAL_NAME, json.dumps(journal).encode("utf-8"), 0o600)
    atomic_write(pubspec, staged, mode)


def restore(root, owner=None):
    journal_path = root / JOURNAL_NAME
    if not journal_path.exists():
        assert_clean(root)
        return
    journal = json.loads(journal_path.read_text(encoding="utf-8"))
    if journal.get("version") != 1 or (owner is not None and journal.get("owner") != owner):
        raise ValueError("The asset journal belongs to a different build.")
    original = base64.b64decode(journal["original"], validate=True)
    staged = client_pubspec(original)
    if digest(original) != journal["originalSha256"] or digest(staged) != journal["stagedSha256"]:
        raise ValueError("The recovery journal failed its integrity check.")
    if not isinstance(journal.get("mode"), int) or not 0 <= journal["mode"] <= 0o777:
        raise ValueError("The recovery journal has an invalid file mode.")
    pubspec = root / "pubspec.yaml"
    if digest(pubspec.read_bytes()) not in {digest(original), digest(staged)}:
        raise RuntimeError("pubspec.yaml changed during packaging; preserved both it and the recovery journal for review.")
    atomic_write(pubspec, original, journal["mode"])
    journal_path.unlink()


def recover(root):
    # PowerShell holds this file with FileShare.None for the complete build.
    # Opening it for writing fails on Windows until that process has stopped.
    with (root / LOCK_NAME).open("a+b") as lock:
        if os.name != "nt":
            import fcntl
            fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
        restore(root)


def manifest_strings(content):
    """Read the bounded String/List/Map/double subset emitted by Flutter 3.41."""
    if len(content) > 8 * 1024 * 1024:
        raise ValueError("AssetManifest.bin exceeds the packaging limit.")
    offset = 0
    strings = []

    def take(length):
        nonlocal offset
        end = offset + length
        if end > len(content):
            raise ValueError("Truncated asset manifest.")
        chunk = content[offset:end]
        offset = end
        return chunk

    def size():
        length = take(1)[0]
        return int.from_bytes(take(2 if length == 254 else 4), "little") if length >= 254 else length

    def read(depth=0):
        if depth > 8:
            raise ValueError("Unexpected nested asset manifest.")
        tag = take(1)[0]
        if tag == 7:
            strings.append(take(size()).decode("utf-8"))
        elif tag in (12, 13):
            count = size() * (2 if tag == 13 else 1)
            if count > len(content) - offset:
                raise ValueError("Invalid asset manifest collection size.")
            for _ in range(count):
                read(depth + 1)
        elif tag == 6:
            take((-offset) % 8)
            struct.unpack("<d", take(8))
        else:
            raise ValueError("Unexpected asset manifest value type.")

    if not content or content[0] != 13:
        raise ValueError("AssetManifest.bin must be a map.")
    read()
    if offset != len(content):
        raise ValueError("Trailing asset manifest bytes.")
    return strings


def verify_client_bundle(directory):
    assets = directory / "data/flutter_assets"
    if any(path.is_symlink() for path in assets.rglob("*")):
        raise RuntimeError("Client assets must not contain symbolic links.")
    actual = {path.relative_to(assets).as_posix() for path in (assets / "assets").rglob("*") if path.is_file()}
    if actual != CLIENT_ASSETS:
        raise RuntimeError("Client bundle contains missing or unapproved application assets.")
    strings = manifest_strings((assets / "AssetManifest.bin").read_bytes())
    paths = {entry for entry in strings if entry.startswith("assets/")}
    if paths != CLIENT_ASSETS:
        raise RuntimeError("Client AssetManifest does not match its public application assets.")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("action", choices=["assert-clean", "begin", "restore", "recover", "client-inputs", "verify-client"])
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument("--owner")
    parser.add_argument("--bundle", type=Path)
    args = parser.parse_args()
    if args.action == "assert-clean":
        assert_clean(args.root)
    elif args.action in {"begin", "restore"}:
        if not args.owner:
            parser.error("begin/restore requires the build owner identifier")
        (begin if args.action == "begin" else restore)(args.root, args.owner)
    elif args.action == "recover":
        recover(args.root)
    elif args.action == "client-inputs":
        print("\n".join(sorted(CLIENT_ASSETS)))
    elif args.bundle is None:
        parser.error("verify-client requires --bundle")
    else:
        verify_client_bundle(args.bundle)


if __name__ == "__main__":
    main()
