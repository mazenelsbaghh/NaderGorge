import errno
import json
from pathlib import Path
import tempfile
import threading
import time
import unittest
from unittest.mock import patch

from network_timeline import NetworkTimeline


class NetworkTimelineTest(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name)

    def test_rotation_retains_bounded_private_history_across_restart_and_marks_unclean_end(self):
        with patch('network_timeline.MAX_FILE_BYTES', 1200):
            timeline = NetworkTimeline(self.root)
            for _ in range(45):
                timeline.record('user_marker', name='PRIVATE-STUDENT', token='PRIVATE-TOKEN', answers={'a': 'PRIVATE-ANSWER'})
            files = list(timeline.directory.glob('network-timeline.jsonl*'))
            self.assertEqual(len(files), 4)
            self.assertTrue(all(path.stat().st_size <= 1200 for path in files))
            self.assertTrue(all(path.stat().st_mode & 0o777 == 0o600 for path in files))
            next_run = NetworkTimeline(self.root)
            exported = next_run.history()
            self.assertIn('previous_run_incomplete', [event['kind'] for event in exported['events']])
            self.assertGreater(len({event['runId'] for event in exported['events']}), 1)
            self.assertNotIn('PRIVATE-', json.dumps(exported))
            next_run.stop()
            clean_run = NetworkTimeline(self.root)
            self.assertEqual(clean_run.history()['events'][-2]['kind'], 'app_end')
            clean_run.stop()

    def test_failed_disk_writes_do_not_raise_and_recovery_records_the_gap(self):
        timeline = NetworkTimeline(self.root)
        with patch('network_timeline.os.open', side_effect=OSError(errno.ENOSPC, 'PRIVATE-PATH')):
            self.assertIsNone(timeline.record('user_marker'))
            self.assertIsNone(timeline.record('user_marker'))
        unavailable = timeline.history()
        self.assertEqual(unavailable['status'], 'partial')
        self.assertEqual(unavailable['recording']['lastErrorCode'], 'disk_full')
        self.assertEqual(unavailable['recording']['writeFailureCount'], 2)
        self.assertTrue(timeline.record('user_marker'))
        restored = timeline.history()
        self.assertEqual(restored['events'][-2]['kind'], 'recording_resumed')
        self.assertEqual(restored['events'][-2]['missedEvents'], 2)
        self.assertNotIn('PRIVATE-', json.dumps(restored))
        timeline.stop()

    def test_clock_reversal_cannot_make_stale_snapshot_ready_and_sampling_gap_is_explicit(self):
        with patch('network_timeline.time.monotonic', return_value=100), patch('network_timeline.time.time', return_value=1700000100):
            timeline = NetworkTimeline(self.root)
            # The diagnostic collector is an OS boundary; all storage and
            # state transitions remain real in this deterministic clock test.
            timeline._collect = lambda: {'ready': True, 'ethernetDevice': 'en10', 'name': 'PRIVATE-STUDENT',
                'ethernetRuntimeAddresses': [{'ip': '10.77.0.1', 'mask': '255.255.252.0'}]}
            timeline.capture()
        with patch('network_timeline.time.monotonic', return_value=145), patch('network_timeline.time.time', return_value=1700000000):
            self.assertFalse(timeline.current()['ready'])
            self.assertTrue(timeline.current()['inspectionError'])
            timeline.capture()
        history = timeline.history()
        gap = next(event for event in history['events'] if event['kind'] == 'sampling_gap')
        self.assertEqual(gap['monotonicGapSeconds'], 45)
        self.assertEqual(gap['wallGapSeconds'], 0)
        self.assertNotIn('PRIVATE-', json.dumps(history))
        timeline.stop()

    def test_background_sampler_recovers_from_probe_failure_without_ui_and_never_overlaps(self):
        active = maximum = calls = 0
        observed = threading.Event()
        def collect():
            nonlocal active, maximum, calls
            active += 1
            maximum = max(maximum, active)
            calls += 1
            try:
                time.sleep(.01)
                if calls == 1:
                    raise RuntimeError('PRIVATE-COMMAND-OUTPUT')
                if calls >= 3:
                    observed.set()
                return {'ready': True, 'ethernetActive': True}
            finally:
                active -= 1
        timeline = NetworkTimeline(self.root)
        with patch('network_timeline.SAMPLE_SECONDS', .05), patch('network_timeline.HEARTBEAT_SECONDS', .05):
            timeline.start(collect)
            concurrent = threading.Thread(target=timeline.capture)
            concurrent.start()
            try:
                self.assertTrue(observed.wait(2))
            finally:
                concurrent.join(2)
                timeline.stop()
        self.assertFalse(concurrent.is_alive())
        self.assertEqual(maximum, 1)
        events = timeline.history()['events']
        self.assertIn('sample_failed', [event['kind'] for event in events])
        self.assertEqual(events[-1]['kind'], 'app_end')
        self.assertNotIn('PRIVATE-', json.dumps(events))

    def test_corrupt_and_untrusted_history_is_partial_without_exporting_unknown_fields(self):
        timeline = NetworkTimeline(self.root)
        valid = timeline.history()['events'][0]
        with timeline.path.open('a') as target:
            target.write(json.dumps({**valid, 'name': 'PRIVATE-STUDENT', 'state': {'dhcpLeases': 'PRIVATE', 'ethernetRuntimeAddresses': None}}) + '\n')
            target.write('{broken\n' + json.dumps({**valid, 'kind': ['PRIVATE']}) + '\n')
            target.write(json.dumps({**valid, 'runId': 11111111111111111111111111111111}) + '\n')
            target.write(json.dumps({**valid, 'time': '2026-99-99T99:99:99Z'}) + '\n')
        report = timeline.history()
        self.assertEqual(report['status'], 'partial')
        self.assertEqual(report['malformedRecords'], 4)
        self.assertNotIn('PRIVATE', json.dumps(report))
        timeline.stop()

    def test_shutdown_does_not_wait_for_a_blocked_diagnostic_disk(self):
        timeline = NetworkTimeline(self.root)
        timeline._io_lock.acquire()
        stopping = threading.Thread(target=timeline.stop)
        stopping.start()
        try:
            stopping.join(2)
            self.assertFalse(stopping.is_alive(), 'diagnostic I/O blocked application/server shutdown')
        finally:
            timeline._io_lock.release()
            stopping.join(2)
        deadline = time.monotonic() + 2
        while timeline.history()['events'][-1]['kind'] != 'app_end' and time.monotonic() < deadline:
            time.sleep(.01)
        self.assertEqual(timeline.history()['events'][-1]['kind'], 'app_end')


if __name__ == '__main__':
    unittest.main()
