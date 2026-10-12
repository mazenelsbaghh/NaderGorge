import ipaddress
from pathlib import Path
import tempfile
import subprocess
import unittest

from network_diagnostics import inspect_dhcp_leases, inspect_dhcp_socket, sanitized_dhcp_log


CURRENT_TIME = 2_000_000_000


def lease(address, expires=CURRENT_TIME + 3600):
    return f'{expires} 00:11:22:33:44:55 {address} student *\n'


class DhcpDiagnosticsTest(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory(prefix='exam-dhcp-test-')
        self.addCleanup(self.directory.cleanup)
        self.config = Path(self.directory.name) / 'dnsmasq.conf'
        self.leases = Path(self.directory.name) / 'dnsmasq.leases'
        source = Path(__file__).parent / 'packaging' / 'omada-direct-dnsmasq.conf'
        self.settings = source.read_text().replace('/var/db/massar-exam-room/dnsmasq.leases', str(self.leases))
        self.config.write_text(self.settings)
        self.leases.write_text('')

    def inspect(self):
        return inspect_dhcp_leases(self.config, now=CURRENT_TIME)

    def test_nonexpired_unique_ips_including_infinite_and_range_edges_are_counted(self):
        snapshot = (lease('10.77.0.20') + lease('10.77.0.20', 0)
                    + lease('10.77.3.250', 0) + lease('10.77.0.21', CURRENT_TIME)
                    + lease('10.77.0.22', CURRENT_TIME - 1) + lease('10.77.0.2'))
        self.leases.write_text(snapshot)
        report = self.inspect()
        self.assertEqual(report['status'], 'ok')
        self.assertEqual(report['dhcpLeaseCount'], 2)
        self.assertEqual(report['outsideRangeLeaseCount'], 1)
        self.assertEqual(report['capacity'], 999)
        self.assertEqual(report['percent'], 0.2)
        self.assertEqual(report['leaseSeconds'], 3600)
        self.assertTrue(report['configVerified'])
        self.assertFalse(report['singleDhcpServerVerified'])

        self.assertEqual(self.leases.read_text(), snapshot)
        self.assertEqual(self.config.read_text(), self.settings)

    def test_missing_source_is_unknown_while_empty_existing_lease_file_is_zero(self):
        self.assertEqual(self.inspect()['dhcpLeaseCount'], 0)
        self.leases.unlink()
        report = self.inspect()
        self.assertEqual(report['status'], 'unavailable')
        self.assertIsNone(report['dhcpLeaseCount'])
        self.assertTrue(report['configAvailable'])
        self.assertFalse(report['leasesAvailable'])
        self.config.unlink()
        report = self.inspect()
        self.assertEqual(report['status'], 'unavailable')
        self.assertFalse(report['configAvailable'])
        self.assertIsNone(report['capacity'])

    def test_malformed_records_never_report_complete_or_healthy_counts(self):
        for invalid in [
            'short row\n', lease('10.77.0.999'), lease('2001:db8::1'),
            lease('10.77.0.21', -1), lease('10.77.0.21', 'tomorrow'),
            lease('10.77.0.21').replace('00:11:22:33:44:55', 'broken-mac'),
        ]:
            with self.subTest(invalid=invalid):
                self.leases.write_text(lease('10.77.0.20') + invalid)
                report = self.inspect()
                self.assertEqual(report['status'], 'invalid')
                self.assertEqual(report['dhcpLeaseCount'], 1)
                self.assertEqual(report['malformedRecordCount'], 1)
                self.assertFalse(report['leaseDataComplete'])
                self.assertIsNone(report['percent'])

    def test_capacity_warning_starts_at_eighty_percent(self):
        self.config.write_text(self.settings.replace('10.77.3.250', '10.77.0.24'))
        for count, expected_status in [(3, 'ok'), (4, 'warning'), (5, 'warning')]:
            with self.subTest(count=count):
                self.leases.write_text(''.join(lease(str(ipaddress.IPv4Address('10.77.0.20') + index))
                                              for index in range(count)))
                report = self.inspect()
                self.assertEqual(report['status'], expected_status)
                self.assertEqual(report['capacity'], 5)
                self.assertEqual(report['percent'], count * 20)

    def test_static_leases_outside_pool_consume_global_lease_limit(self):
        self.config.write_text(self.settings + '\ndhcp-lease-max=3\n')
        self.leases.write_text(lease('10.77.0.2') + lease('10.77.0.20') + lease('10.77.0.21'))
        report = self.inspect()
        self.assertEqual(report['dhcpLeaseCount'], 2)
        self.assertEqual(report['capacity'], 2)
        self.assertEqual(report['percent'], 100)
        self.assertEqual(report['status'], 'warning')

    def test_wrong_network_or_dhcp_options_never_report_verified(self):
        for settings in [
            self.settings.replace('10.77.3.250', '10.77.0.19'),
            self.settings.replace('10.77.3.250', '10.77.4.250'),
            self.settings.replace('255.255.252.0', '255.255.255.0'),
            self.settings.replace('10.77.0.20,', '10.77.0.1,'),
            self.settings.replace('option:router,10.77.0.1', 'option:router,10.77.0.2'),
            self.settings.replace('option:dns-server,10.77.0.1', 'option:dns-server,10.77.0.1,8.8.8.8'),
            self.settings.replace('local=/#/', ''),
            self.settings + '\nconf-dir=/somewhere\n',
            self.settings + '\ndhcp-lease-max=0\n',
            self.settings + '\ndhcp-range=10.77.1.1,10.77.1.5,255.255.252.0,1h\n',
        ]:
            with self.subTest(settings=settings):
                self.config.write_text(settings)
                report = self.inspect()
                self.assertEqual(report['status'], 'invalid')
                self.assertFalse(report['configVerified'])

    def test_long_and_infinite_configured_leases_warn_about_next_cohort(self):
        for duration, seconds in [('12h', 43200), ('infinite', None)]:
            with self.subTest(duration=duration):
                self.config.write_text(self.settings.replace(',1h', ',' + duration))
                report = self.inspect()
                self.assertEqual(report['status'], 'warning')
                self.assertEqual(report['leaseSeconds'], seconds)
                self.assertIn('long-leases', [issue['code'] for issue in report['findings']])

    def test_unreadable_lease_bytes_do_not_become_empty_healthy_network(self):
        self.leases.write_bytes(b'\xff\xfe')
        report = self.inspect()
        self.assertEqual(report['status'], 'invalid')
        self.assertIsNone(report['dhcpLeaseCount'])
        self.assertFalse(report['leaseDataComplete'])
        self.assertFalse(report['singleDhcpServerVerified'])


class DhcpRuntimeDiagnosticsTest(unittest.TestCase):
    def test_listener_check_distinguishes_bound_port_from_client_and_unavailable_inspection(self):
        for stdout, stderr, code, expected in [
            ('udp4 0 0 *.67 *.*\n', '', 0, True),
            ('udp4 0 0 10.77.0.1.67 *.*\n', '', 0, True),
            ('udp4 0 0 127.0.0.1.67 *.*\n', '', 0, False),
            ('udp4 0 0 10.0.0.2.68 10.0.0.1.67\n', '', 0, False),
            ('udp6 0 0 *.67 *.*\n', '', 0, False),
            ('', 'netstat: sysctl: Operation not permitted', 0, None),
            ('', 'read failed', 1, None),
        ]:
            with self.subTest(stdout=stdout, stderr=stderr):
                outcome = subprocess.CompletedProcess([], code, stdout, stderr)
                report = inspect_dhcp_socket(lambda *args, **kwargs: outcome)
                self.assertIs(report['ready'], expected)
        def timeout(*args, **kwargs):
            raise subprocess.TimeoutExpired('netstat', 3)
        self.assertIsNone(inspect_dhcp_socket(timeout)['ready'])

    def test_exported_dhcp_logs_keep_activity_and_failures_without_client_identities(self):
        source = ('Oct 10 10:20:00 dnsmasq-dhcp[123]: DHCPACK(en10) 10.77.1.22 aa:bb:cc:dd:ee:ff Private-Student-Phone\n'
                  'Oct 10 10:20:01 dnsmasq-dhcp[123]: DHCPREQUEST(en10) 10.77.1.22 aa:bb:cc:dd:ee:ff\n'
                  'dnsmasq: failed to create listening socket for 10.77.0.1: Can\'t assign requested address\n'
                  'Oct 10 10:20:02 dnsmasq: failed to parse lease database cleanly Private-Student-Phone\n')
        clean = sanitized_dhcp_log(source)
        self.assertIn('Oct 10 10:20:00 DHCPACK(en10)', clean)
        self.assertIn("Can't assign requested address", clean)
        self.assertIn('unreadable record', clean)
        for private in ('aa:bb:cc:dd:ee:ff', 'Private-Student-Phone', '10.77.1.22'):
            self.assertNotIn(private, clean)


if __name__ == '__main__':
    unittest.main()
