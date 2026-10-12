"""Hold a macOS idle-sleep assertion for the lifetime of the exam server."""
import subprocess
import sys


class SleepProtection:
    def __init__(self):
        self.process = None
        self.error = ''

    def start(self, server_pid):
        if sys.platform != 'darwin':
            return
        try:
            self.process = subprocess.Popen(['/usr/bin/caffeinate', '-i', '-w', str(server_pid)],
                                            stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        except OSError as exc:
            self.error = str(exc)

    def status(self):
        return {'supported': sys.platform == 'darwin',
                'active': self.process is not None and self.process.poll() is None,
                'error': self.error}

    def stop(self):
        if self.process is None or self.process.poll() is not None:
            return
        self.process.terminate()
        try:
            self.process.wait(timeout=4)
        except subprocess.TimeoutExpired:
            self.process.kill()
            self.process.wait()
