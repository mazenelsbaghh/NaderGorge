from __future__ import annotations

import json
from pathlib import Path


ROOT = Path(__file__).resolve().parents[3]


def test_gluster_has_two_full_data_bricks_and_one_arbiter() -> None:
    topology = json.loads(
        (ROOT / "deploy/production/config/gluster/topology.json").read_text()
    )
    assert topology["replica_count"] == 3
    assert topology["arbiter_count"] == 1
    assert topology["bricks"] == [
        "node-1.cluster.internal:/srv/gluster/massar/brick",
        "node-2.cluster.internal:/srv/gluster/massar/brick",
        "node-3.cluster.internal:/srv/gluster/massar/brick",
    ]
    assert topology["mountpoint"] == "/srv/massar-shared"
