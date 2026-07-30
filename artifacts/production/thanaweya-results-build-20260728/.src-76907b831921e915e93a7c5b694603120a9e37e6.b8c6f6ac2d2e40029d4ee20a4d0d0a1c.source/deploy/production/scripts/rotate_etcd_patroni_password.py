#!/usr/bin/env python3
"""Rotate the Patroni etcd password without exposing it in argv or logs."""

from __future__ import annotations

import os
import pty
import select
import sys
import time
from pathlib import Path


BASE = [
    "etcdctl",
    "--endpoints=https://10.77.0.11:2379,https://10.77.0.12:2379,https://10.77.0.13:2379",
    "--cacert=/etc/massar/pki/etcd/ca.crt",
]


def read_secret(path: str) -> bytes:
    value = Path(path).read_bytes().strip()
    if len(value) < 32 or b"\n" in value or b"\r" in value:
        raise RuntimeError("invalid secret input")
    return value


def main() -> int:
    if len(sys.argv) != 3:
        raise RuntimeError("expected root and replacement Patroni secret paths")
    root_password = read_secret(sys.argv[1])
    replacement = read_secret(sys.argv[2])
    environment = {
        **os.environ,
        "ETCDCTL_API": "3",
        "ETCDCTL_USER": "root",
        "ETCDCTL_PASSWORD": root_password.decode("ascii"),
    }

    pid, descriptor = pty.fork()
    if pid == 0:
        os.execvpe(
            BASE[0],
            [*BASE, "user", "passwd", "patroni"],
            environment,
        )

    sent = 0
    scan = b""
    deadline = time.time() + 20
    while time.time() < deadline:
        child, status = os.waitpid(pid, os.WNOHANG)
        if child:
            if os.waitstatus_to_exitcode(status) != 0 or sent != 2:
                raise RuntimeError("etcd Patroni password rotation failed")
            return 0
        readable, _, _ = select.select([descriptor], [], [], 0.5)
        if not readable:
            continue
        try:
            data = os.read(descriptor, 4096)
        except OSError:
            data = b""
        scan += data
        lowered = scan.lower()
        if sent < 2 and b"password" in lowered:
            position = lowered.index(b"password") + len(b"password")
            scan = scan[position:]
            os.write(descriptor, replacement + b"\n")
            sent += 1
    raise RuntimeError("etcd Patroni password rotation timed out")


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except RuntimeError as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        raise SystemExit(2)
