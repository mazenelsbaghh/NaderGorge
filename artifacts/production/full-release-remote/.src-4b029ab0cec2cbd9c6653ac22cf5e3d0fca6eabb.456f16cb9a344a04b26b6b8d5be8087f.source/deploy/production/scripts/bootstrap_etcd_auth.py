#!/usr/bin/env python3
"""Initialize etcd password auth without placing passwords in argv or logs."""

from __future__ import annotations

import os
import pty
import select
import subprocess
import sys
import time
from pathlib import Path


BASE = [
    "etcdctl",
    "--endpoints=https://10.77.0.11:2379,https://10.77.0.12:2379,https://10.77.0.13:2379",
    "--cacert=/etc/massar/pki/etcd/ca.crt",
]
ENV = {**os.environ, "ETCDCTL_API": "3"}


class EtcdAuthError(RuntimeError):
    pass


def secret(path: str) -> bytes:
    value = Path(path).read_bytes().strip()
    if len(value) < 32 or b"\n" in value:
        raise EtcdAuthError("invalid secret input")
    return value


def call(*args: str, env: dict[str, str] | None = None, check: bool = True) -> bool:
    result = subprocess.run(
        [*BASE, *args],
        env=env or ENV,
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
        check=False,
    )
    if check and result.returncode:
        raise EtcdAuthError(f"etcdctl command failed: {' '.join(args)}")
    return result.returncode == 0


def add_password_user(name: str, password: bytes) -> None:
    pid, descriptor = pty.fork()
    if pid == 0:
        os.execvpe(BASE[0], [*BASE, "user", "add", name], ENV)
    sent = 0
    scan = b""
    deadline = time.time() + 20
    while time.time() < deadline:
        child, status = os.waitpid(pid, os.WNOHANG)
        if child:
            if os.waitstatus_to_exitcode(status) or sent != 2:
                raise EtcdAuthError(f"could not create etcd user {name}")
            return
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
            os.write(descriptor, password + b"\n")
            sent += 1
    raise EtcdAuthError(f"timed out creating etcd user {name}")


def authenticated_environment(root_password: bytes) -> dict[str, str]:
    return {
        **ENV,
        "ETCDCTL_USER": "root",
        "ETCDCTL_PASSWORD": root_password.decode("ascii"),
    }


def main() -> int:
    if len(sys.argv) != 3:
        raise EtcdAuthError("expected root and Patroni secret paths")
    root_password = secret(sys.argv[1])
    patroni_password = secret(sys.argv[2])
    root_env = authenticated_environment(root_password)

    if call("auth", "status", env=root_env, check=False):
        return 0
    if not call("auth", "status", check=False):
        raise EtcdAuthError("authentication is enabled but supplied root credential failed")

    if call("user", "get", "root", check=False):
        call("user", "delete", "root")
    if call("user", "get", "patroni", check=False):
        call("user", "delete", "patroni")
    add_password_user("root", root_password)
    call("user", "grant-role", "root", "root")
    add_password_user("patroni", patroni_password)
    if not call("role", "get", "patroni", check=False):
        call("role", "add", "patroni")
    call("role", "grant-permission", "patroni", "readwrite", "/service/", "--prefix")
    call("user", "grant-role", "patroni", "patroni")
    call("auth", "enable")
    call("auth", "status", env=root_env)
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except EtcdAuthError as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        raise SystemExit(2)
