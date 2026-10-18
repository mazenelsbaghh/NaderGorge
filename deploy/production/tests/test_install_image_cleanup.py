from __future__ import annotations

import hashlib
import json
from types import SimpleNamespace

import pytest

from install_image_cleanup import preview_node


@pytest.mark.parametrize("existing_content, approved", [(b"reviewed previous unit", True),
                                                          (b"unreviewed operator change", False)])
def test_installer_only_replaces_the_candidate_or_its_reviewed_previous_version(existing_content, approved):
    path = "/etc/systemd/system/massar-image-cleanup.service"
    existing = {path: hashlib.sha256(existing_content).hexdigest()}

    class FilesystemTransport:
        def run(self, *arguments, **options):
            return SimpleNamespace(stdout=json.dumps(existing))

    candidate = {path: b"reviewed new unit"}
    previous = {path: b"reviewed previous unit"}
    remote = SimpleNamespace(node_id="node-3")
    if approved:
        assert preview_node(FilesystemTransport(), remote, candidate, previous) == existing
    else:
        with pytest.raises(RuntimeError, match="Existing maintenance asset differs"):
            preview_node(FilesystemTransport(), remote, candidate, previous)
