#!/usr/bin/env python3
"""Render the reviewed non-root operator bootstrap script."""

from __future__ import annotations

import shlex


def render_operator_bootstrap(public_key: str, operator: str = "massar-ops") -> str:
    if not public_key.startswith(("ssh-ed25519 ", "sk-ssh-ed25519@openssh.com ")):
        raise ValueError("only Ed25519 operator public keys are accepted")
    quoted_key = shlex.quote(public_key.strip())
    quoted_operator = shlex.quote(operator)
    return f"""set -euo pipefail
getent group massar >/dev/null 2>&1 || groupadd --system massar
id -u {quoted_operator} >/dev/null 2>&1 || useradd --create-home --shell /bin/bash {quoted_operator}
usermod --append --groups massar {quoted_operator}
install -d -m 0700 -o {quoted_operator} -g {quoted_operator} /home/{operator}/.ssh
install -m 0600 -o {quoted_operator} -g {quoted_operator} /dev/null /home/{operator}/.ssh/authorized_keys
printf '%s\\n' {quoted_key} > /home/{operator}/.ssh/authorized_keys
install -d -m 0750 /etc/sudoers.d
printf '%s\\n' '{operator} ALL=(root) NOPASSWD: /usr/bin/systemctl, /usr/bin/docker, /usr/bin/journalctl, /usr/sbin/nft, /usr/bin/install, /usr/bin/mount, /usr/bin/umount, /usr/bin/test, /usr/bin/tee, /usr/bin/chmod, /usr/bin/timedatectl' '{operator} ALL=(root) NOPASSWD: /usr/local/sbin/massar-produce-release-migration-gate --root-produce *' > /etc/sudoers.d/massar-ops
chmod 0440 /etc/sudoers.d/massar-ops
visudo -cf /etc/sudoers.d/massar-ops
"""


def render_disable_routine_password_login() -> str:
    return """set -euo pipefail
install -d -m 0755 /etc/ssh/sshd_config.d
# OpenSSH uses the first value found; this must sort before cloud-init's
# 50-cloud-init.conf.
cat > /etc/ssh/sshd_config.d/40-massar-production.conf <<'EOF'
PermitRootLogin prohibit-password
PasswordAuthentication no
KbdInteractiveAuthentication no
PubkeyAuthentication yes
EOF
sshd -t
systemctl reload ssh
"""
