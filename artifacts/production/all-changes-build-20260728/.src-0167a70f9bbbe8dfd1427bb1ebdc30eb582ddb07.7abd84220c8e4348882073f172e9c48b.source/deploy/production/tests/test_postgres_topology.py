from __future__ import annotations

from pathlib import Path


ROOT = Path(__file__).resolve().parents[3]


def test_etcd_is_three_member_authenticated_tls_cluster() -> None:
    template = (ROOT / "deploy/production/config/etcd/massar-etcd.env.tmpl").read_text()
    assert "node-1=https://10.77.0.11:2380" in template
    assert "node-2=https://10.77.0.12:2380" in template
    assert "node-3=https://10.77.0.13:2380" in template
    assert "ETCD_CLIENT_CERT_AUTH=false" in template
    assert "ETCD_TRUSTED_CA_FILE" not in template
    assert "ETCD_PEER_CLIENT_CERT_AUTH=true" in template
    assert "0.0.0.0" not in template


def test_patroni_has_one_safe_synchronous_writer_contract() -> None:
    template = (ROOT / "deploy/production/config/patroni/patroni.yml.tmpl").read_text()
    assert "synchronous_mode: quorum" in template
    assert "synchronous_mode_strict: true" in template
    assert "synchronous_node_count: 1" in template
    assert "data-checksums" in template
    assert "use_pg_rewind: true" in template
    assert "cacert: /etc/massar/pki/etcd/ca.crt" in template
    assert "username: patroni" in template
    assert "password: __ETCD_PATRONI_PASSWORD__" in template
    assert "10.77.0.0/24 scram-sha-256" in template


def test_haproxy_exposes_only_patroni_primary_as_writer() -> None:
    config = (ROOT / "deploy/production/config/haproxy/postgres.cfg").read_text()
    postgres_backend = config.split("backend patroni_primary", 1)[1].split(
        "frontend massar_ingress", 1
    )[0]
    assert "bind 0.0.0.0:6432" in config
    assert "option httpchk GET /primary" in config
    assert postgres_backend.count(" check port 8008") == 3
    assert "roundrobin" not in postgres_backend
