import json
import subprocess
from types import SimpleNamespace

import pytest
from deploy_release import DeployError, node_ready
from ssh_transport import SshTarget


class DelayedConnection:
    def __init__(self, response):
        self.response = response

    def run(self, target, command, *, timeout_seconds):
        # A 20-second SSH handshake occurred during the incident. The service
        # itself must still answer within the separate 10-second HTTP budget.
        if timeout_seconds < 20:
            raise subprocess.TimeoutExpired('ssh readiness probe', timeout_seconds)
        assert '--connect-timeout 3 --max-time 10' in command[-1]
        return SimpleNamespace(stdout=json.dumps(self.response))


def test_slow_ssh_handshake_does_not_reject_a_healthy_service():
    release = 'git-' + 'a' * 40
    response = {'status': 'healthy', 'nodeId': 'node-2', 'releaseId': release}
    assert node_ready(DelayedConnection(response), SshTarget('node-2', '192.0.2.2', 'massar-ops'), '10.77.0.12') == release


@pytest.mark.parametrize('field,value', [('status', 'unhealthy'), ('nodeId', 'node-1'), ('releaseId', 'unverified')])
def test_longer_connection_budget_still_rejects_invalid_readiness(field, value):
    response = {'status': 'healthy', 'nodeId': 'node-2', 'releaseId': 'git-' + 'a' * 40, field: value}
    with pytest.raises(DeployError):
        node_ready(DelayedConnection(response), SshTarget('node-2', '192.0.2.2', 'massar-ops'), '10.77.0.12')
