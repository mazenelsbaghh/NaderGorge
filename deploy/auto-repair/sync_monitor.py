"""Report source readiness independently of incident claims and long-running repairs."""
import shutil
import subprocess
import sys
import threading
import urllib.error
from pathlib import Path


class SyncMonitor:
    def __init__(self, config, api):
        self.config, self.api = config, api
        self.stop = threading.Event()
        self.thread = threading.Thread(target=self.serve, daemon=True)

    def observe(self):
        repository = Path(self.config['source_repository'])
        sys.path.insert(0, str(repository / 'deploy/production/scripts'))
        from startup_check import inspect, shared_files
        from source_baseline import dependencies, dependency_path
        if shutil.disk_usage(self.config['state_dir']).free < 20 * 1024 ** 3:
            return {'state': 'storage_low', 'sharedCommit': '', 'nodes': []}
        current = inspect(repository)
        nodes = [{'nodeId': node['node'], 'releaseId': node['releaseId']} for node in current['nodes']]
        state = 'pending_release' if current['status'] == 'pending-or-divergent-release' else 'ready'
        if current['status'] == 'failed-release':
            state = 'release_failed'
        if current['status'] == 'conflict' or current['dirty']:
            state = 'unavailable'
        elif state == 'ready':
            source = shared_files(repository, current['sharedCommit'], ())
            required = {path: digest for path, digest in source.items() if dependency_path(path)}
            if required != dependencies(Path(self.config['verified_dependencies_source'])):
                state = 'dependencies_changed'
        return {'state': state, 'sharedCommit': current['sharedCommit'], 'nodes': nodes}

    def check(self):
        try:
            snapshot = self.observe()
        except (OSError, ValueError, KeyError, ImportError, RuntimeError, subprocess.SubprocessError):
            snapshot = {'state': 'unavailable', 'sharedCommit': '', 'nodes': []}
        self.api.post('synchronization', snapshot)

    def serve(self):
        while not self.stop.is_set():
            try:
                self.check()
            except (OSError, ValueError, urllib.error.URLError):
                # The persisted observation ages out; never invent a fresh successful heartbeat.
                print('Synchronization report unavailable; previous observation will expire', flush=True)
            self.stop.wait(30)
