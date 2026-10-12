import os
import subprocess
import sys
import time
import unittest
from unittest.mock import patch
from sleep_protection import SleepProtection


@unittest.skipUnless(sys.platform == 'darwin', 'macOS sleep assertion')
class SleepProtectionTest(unittest.TestCase):
    def test_assertion_is_held_until_exam_server_exits(self):
        server = subprocess.Popen(['/bin/sleep', '15'])
        guard = SleepProtection()
        try:
            guard.start(server.pid)
            found = False
            for _ in range(30):
                assertions = subprocess.check_output(['/usr/bin/pmset', '-g', 'assertions'], text=True, errors='replace')
                found = any(str(guard.process.pid) in line and 'PreventUserIdleSystemSleep' in line
                            for line in assertions.splitlines())
                if found:
                    break
                time.sleep(.1)
            self.assertTrue(found, 'no actual macOS idle-sleep assertion')
            server.terminate()
            server.wait(timeout=3)
            guard.process.wait(timeout=3)
            self.assertFalse(guard.status()['active'])
        finally:
            guard.stop()
            if server.poll() is None:
                server.terminate()
                server.wait(timeout=3)

    def test_failed_assertion_reports_unprotected(self):
        guard = SleepProtection()
        with patch('sleep_protection.subprocess.Popen', side_effect=OSError('not available')):
            guard.start(os.getpid())
        self.assertFalse(guard.status()['active'])
        self.assertEqual(guard.status()['error'], 'not available')
