#!/usr/bin/env python3
"""Render WireGuard node configs from public-key references."""

from __future__ import annotations

from pathlib import Path
from typing import Mapping


def render_wireguard(
    node_id: str,
    nodes: list[dict[str, str]],
    public_keys: Mapping[str, str],
    private_key_path: str = "/etc/massar/secrets/wireguard-private-key",
    listen_port: int = 51820,
) -> str:
    current = next((node for node in nodes if node["id"] == node_id), None)
    if current is None:
        raise ValueError(f"unknown node: {node_id}")
    if set(public_keys) != {node["id"] for node in nodes}:
        raise ValueError("one reviewed public key is required for every node")
    lines = [
        "[Interface]",
        f"Address = {current['overlay_address']}/32",
        f"ListenPort = {listen_port}",
        f"PrivateKey = <read-at-install:{private_key_path}>",
        "SaveConfig = false",
    ]
    for peer in nodes:
        if peer["id"] == node_id:
            continue
        lines.extend([
            "",
            "[Peer]",
            f"# {peer['id']}",
            f"PublicKey = {public_keys[peer['id']]}",
            f"AllowedIPs = {peer['overlay_address']}/32",
            f"Endpoint = {peer['public_address']}:{listen_port}",
            "PersistentKeepalive = 25",
        ])
    return "\n".join(lines) + "\n"


def installable_config(rendered: str, private_key: str) -> str:
    if not private_key.strip():
        raise ValueError("private key value is empty")
    return rendered.replace("<read-at-install:/etc/massar/secrets/wireguard-private-key>", private_key.strip())
