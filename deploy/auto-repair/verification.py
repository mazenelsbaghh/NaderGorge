"""Disposable integration services, with no host ports or production network."""
import subprocess
import json
import time
import uuid


class TestServices:
    __test__ = False

    def __init__(self, config, root):
        self.config, self.root = config, root
        self.network = 'massar-repair-test-' + uuid.uuid4().hex
        self.names = []
        self.bridge = None

    def start(self):
        from runner import DOCKER, command, RepairFailure
        command([*DOCKER, 'network', 'create', '--internal', self.network], self.root)
        try:
            network = json.loads(command([*DOCKER, 'network', 'inspect', self.network], self.root))[0]
            network_id = network['Id']
            if len(network_id) != 64 or any(c not in '0123456789abcdef' for c in network_id):
                raise RepairFailure('Invalid test network identity')
            self.bridge = 'br-' + network_id[:12]
            command(['sudo', '-n', '/usr/sbin/nft', 'add', 'element', 'inet', 'massar_repair',
                     'blocked_bridges', '{', self.bridge, '}'], self.root)
            for service, image in (('postgres', self.config['postgres_image']), ('redis', self.config['redis_image']),
                                   ('redis-unavailable', self.config['redis_image'])):
                name = self.network + '-' + service
                self.names.append(name)
                argv = [*DOCKER, 'run', '-d', '--rm', '--pull=never', '--name', name, '--network', self.network,
                    '--network-alias', service, '--memory=1g', '--cpus=1', '--pids-limit=128']
                if service == 'postgres':
                    argv += ['-e', 'POSTGRES_HOST_AUTH_METHOD=trust', '-e', 'POSTGRES_DB=repair_test']
                    for alias in ('api.lvh.me', 'app.lvh.me', 'admin.lvh.me', 'staff.lvh.me', 'teacher.lvh.me'):
                        argv += ['--network-alias', alias]
                extra = ['redis-server', '--min-replicas-to-write', '1'] if service == 'redis-unavailable' else []
                command([*argv, image, *extra], self.root)
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
        removed = subprocess.run([*DOCKER, 'network', 'rm', self.network], capture_output=True, check=False, timeout=30)
        if removed.returncode == 0 and self.bridge:
            subprocess.run(['sudo', '-n', '/usr/sbin/nft', 'delete', 'element', 'inet', 'massar_repair',
                            'blocked_bridges', '{', self.bridge, '}'], capture_output=True, check=False, timeout=30)
