from __future__ import annotations

import json
from pathlib import Path
import sys
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[3]
sys.path.insert(0, str(ROOT / "deploy/production/scripts"))
from sync_baileys_env import credentials


class BaileysReleaseTests(unittest.TestCase):
    def test_existing_credentials_survive_repeated_provisioning(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "private" / "credentials.json"
            first = credentials(path)
            self.assertEqual(first, credentials(path))
            self.assertEqual(0o600, path.stat().st_mode & 0o777)
            self.assertEqual(0o700, path.parent.stat().st_mode & 0o777)

    def test_insecure_or_invalid_existing_keys_are_rejected_without_rotation(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "credentials.json"
            credentials(path)
            original = path.read_bytes()
            path.chmod(0o644)
            with self.assertRaises(ValueError):
                credentials(path)
            self.assertEqual(original, path.read_bytes())
            path.chmod(0o600)
            path.write_text(json.dumps({"BAILEYS_API_KEY": "short", "BAILEYS_AUTH_KEY": "bad"}))
            with self.assertRaises(ValueError):
                credentials(path)

    def test_bridge_is_private_and_reuses_the_immutable_worker_image(self):
        compose = (ROOT / "deploy/production/compose/compose.app.yml").read_text()
        bridge = compose.split("\n  baileys:\n", 1)[1].split("\n  landing:\n", 1)[0]
        self.assertIn("profiles: [whatsapp]", bridge)
        self.assertIn("network_mode: host", bridge)
        self.assertIn("BAILEYS_HOST: ${MASSAR_OVERLAY_IP:", bridge)
        self.assertIn("${MASSAR_WORKER_IMAGE:", bridge)
        self.assertNotIn("ports:", bridge)
        firewall = (ROOT / "deploy/production/config/firewall/massar-production.nft").read_text()
        self.assertIn('iifname "massar-app0" ip saddr 172.29.0.0/24 tcp dport 3002 accept', firewall)
        frontend = compose.split("x-frontend-internal-api:", 1)[1].split("\nservices:", 1)[0]
        for key in ("BAILEYS_API_KEY", "BAILEYS_AUTH_KEY", "Baileys__ApiKey"):
            self.assertIn(f'{key}: ""', frontend)


if __name__ == "__main__":
    unittest.main()
