import shutil
import socket
import struct
import subprocess
import tempfile
import time
import unittest
from pathlib import Path


DNSMASQ = shutil.which('dnsmasq') or next((str(path) for path in [
    Path('/opt/homebrew/opt/dnsmasq/sbin/dnsmasq'),
    Path('/usr/local/opt/dnsmasq/sbin/dnsmasq'),
] if path.is_file()), None)


def dns_query(port, record_type):
    hostname = b'\x11connectivitycheck\x07gstatic\x03com\x00'
    packet = struct.pack('!HHHHHH', 123, 0x100, 1, 0, 0, 0)
    packet += hostname + struct.pack('!HH', record_type, 1)
    with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as client:
        client.settimeout(.2)
        client.sendto(packet, ('127.0.0.1', port))
        return client.recvfrom(4096)[0]


@unittest.skipUnless(DNSMASQ, 'Requires the dnsmasq binary used by desktop packaging')
class OfflineDnsTest(unittest.TestCase):
    def test_probe_records_resolve_without_upstream_dns(self):
        # Regression: dnsmasq 2.93 refused AAAA/HTTPS lookups in the offline config.
        source = Path(__file__).parent / 'packaging' / 'omada-direct-dnsmasq.conf'
        excluded = ('interface=', 'listen-address=', 'dhcp-', 'pid-file=', 'log-facility=')
        settings = [line for line in source.read_text().splitlines()
                    if not line.startswith(excluded)]
        with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as reservation:
            reservation.bind(('127.0.0.1', 0))
            port = reservation.getsockname()[1]
        with tempfile.TemporaryDirectory(prefix='massar-dns-test-') as directory:
            config = Path(directory) / 'dnsmasq.conf'
            settings += ['listen-address=127.0.0.1', f'port={port}',
                         f'pid-file={directory}/dnsmasq.pid']
            config.write_text('\n'.join(settings) + '\n')
            with subprocess.Popen([DNSMASQ, '--no-daemon', f'--conf-file={config}'],
                                  stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                                  text=True) as server:
                try:
                    deadline = time.monotonic() + 3
                    while True:
                        if server.poll() is not None:
                            self.fail(server.stdout.read())
                        try:
                            dns_query(port, 1)
                            break
                        except TimeoutError:
                            if time.monotonic() >= deadline:
                                raise
                    for record_type, answer_count in [(1, 1), (28, 0), (65, 0)]:
                        with self.subTest(record_type=record_type):
                            reply = dns_query(port, record_type)
                            header = struct.unpack('!HHHHHH', reply[:12])
                            self.assertEqual(header[1] & 15, 0, 'DNS query was refused or failed')
                            self.assertEqual(header[3], answer_count)
                            if record_type == 1:
                                self.assertEqual(socket.inet_ntoa(reply[-4:]), '10.77.0.1')
                finally:
                    server.terminate()
                    server.communicate(timeout=3)


if __name__ == '__main__':
    unittest.main()
