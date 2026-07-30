from __future__ import annotations

import collections
import os
import urllib.request

import pytest


APP_NODES = {"node-1", "node-2", "node-3"}


def collect_distribution(base_url: str, host: str, request_count: int) -> collections.Counter[str]:
    observed: collections.Counter[str] = collections.Counter()
    for _ in range(request_count):
        request = urllib.request.Request(
            base_url,
            headers={"Host": host, "Connection": "close"},
        )
        with urllib.request.urlopen(request, timeout=5) as response:
            assert 200 <= response.status < 400
            node = response.headers.get("X-Massar-Node")
            release = response.headers.get("X-Massar-Release")
            assert node in APP_NODES
            assert release
            observed[node] += 1
    return observed


def assert_balanced(observed: collections.Counter[str], total: int) -> None:
    assert set(observed) == APP_NODES
    expected = total / 3
    assert all(abs(count - expected) <= max(2, total * 0.10) for count in observed.values())


def test_300_requests_reach_all_three_nodes() -> None:
    ingress = os.environ.get("MASSAR_TEST_INGRESS_URL")
    if not ingress:
        pytest.skip("MASSAR_TEST_INGRESS_URL is required for the live distribution drill")
    observed = collect_distribution(ingress, "massar-academy.net", 300)
    assert_balanced(observed, 300)


def test_drained_app_is_removed_while_two_nodes_keep_serving() -> None:
    ingress = os.environ.get("MASSAR_TEST_INGRESS_URL")
    drained = os.environ.get("MASSAR_EXPECT_DRAINED_NODE")
    if not ingress or drained not in APP_NODES:
        pytest.skip("set MASSAR_TEST_INGRESS_URL and MASSAR_EXPECT_DRAINED_NODE during a bounded drain drill")
    observed = collect_distribution(ingress, "massar-academy.net", 90)
    assert drained not in observed
    assert set(observed) == APP_NODES - {drained}
