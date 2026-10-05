#!/bin/sh
set -eu
ROOT=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
PLIST=/Library/LaunchDaemons/com.massar.examroom.dhcp.plist
CONFIG=/usr/local/etc/massar-exam-dnsmasq.conf
sudo -v
if [ -f "$PLIST" ]; then
  sudo launchctl bootout system "$PLIST" || true
  sudo rm "$PLIST"
fi
sudo rm -f "$CONFIG"
sudo networksetup -setdhcp Ethernet
"$ROOT/uninstall-macos-portal.sh"
echo 'Direct exam network removed. Ethernet has returned to DHCP.'
