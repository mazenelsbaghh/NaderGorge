#!/usr/bin/env python3
"""Render the firewall and optional approved operator CIDR."""

from __future__ import annotations

import ipaddress
from pathlib import Path


def render_firewall(template: Path, operator_cidr: str | None = None) -> str:
    text = template.read_text(encoding="utf-8")
    if operator_cidr is None:
        return text
    network = ipaddress.ip_network(operator_cidr, strict=False)
    if network.version != 4:
        raise ValueError("operator CIDR must be IPv4")
    return text.replace(
        "tcp dport 22 ct state new limit rate 10/minute burst 20 packets accept",
        f"tcp dport 22 ip saddr {network} accept",
        1,
    )
