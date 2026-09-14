"""Disposable integration services, with no host ports or production network."""
import subprocess
import time
import uuid


class TestServices:
    __test__ = False

    def __init__(self, config, root):
        self.config, self.root = config, root
        self.network = 'massar-repair-test-' + uuid.uuid4().hex
        self.names = []

    def start(self):
        from runner import DOCKER, command, RepairFailure
        command([*DOCKER, 'network', 'create', '--internal', self.network], self.root)
        try:
            for service, image in (('postgres', self.config['postgres_image']), ('redis', self.config['redis_image'])):
                name = self.network + '-' + service
                self.names.append(name)
                argv = [*DOCKER, 'run', '-d', '--rm', '--pull=never', '--name', name, '--network', self.network,
                    '--network-alias', service, '--memory=1g', '--cpus=1', '--pids-limit=128']
                if service == 'postgres':
                    argv += ['-e', 'POSTGRES_HOST_AUTH_METHOD=trust', '-e', 'POSTGRES_DB=repair_test']
                command([*argv, image], self.root)
            for _ in range(30):
                ready = subprocess.run([*DOCKER, 'exec', self.names[0], 'pg_isready', '-U', 'postgres'], capture_output=True)
                if ready.returncode == 0:
                    return
                time.sleep(1)
            raise RepairFailure('Isolated PostgreSQL did not become ready')
        except (OSError, RuntimeError, subprocess.SubprocessError):
            self.close()
            raise

    def close(self):
        from runner import DOCKER
        for name in self.names:
            subprocess.run([*DOCKER, 'rm', '-f', name], capture_output=True, check=False, timeout=30)
        subprocess.run([*DOCKER, 'network', 'rm', self.network], capture_output=True, check=False, timeout=30)
