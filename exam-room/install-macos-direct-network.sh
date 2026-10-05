#!/bin/sh
# Prepare the Mac as the DHCP/DNS server for a directly attached EAP620 HD.
set -eu

if [ "$(uname -s)" != Darwin ]; then
  echo 'This installer runs only on macOS.' >&2
  exit 1
fi

ROOT=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
DNSMASQ=/opt/homebrew/opt/dnsmasq/sbin/dnsmasq
CONFIG=/usr/local/etc/massar-exam-dnsmasq.conf
PLIST=/Library/LaunchDaemons/com.massar.examroom.dhcp.plist
TEMP_PLIST=$(mktemp "${TMPDIR:-/tmp}/massar-dhcp-plist.XXXXXX")
trap 'rm -f "$TEMP_PLIST"' EXIT HUP INT TERM

if [ ! -x "$DNSMASQ" ]; then
  echo 'dnsmasq is missing. Install it first with: brew install dnsmasq' >&2
  exit 1
fi
if ! networksetup -getinfo Ethernet | grep -q '^DHCP Configuration$' && [ ! -f "$PLIST" ]; then
  echo 'Ethernet has an existing manual address. Its configuration was not changed.' >&2
  exit 1
fi
"$DNSMASQ" --test --conf-file="$ROOT/packaging/omada-direct-dnsmasq.conf"
cat > "$TEMP_PLIST" <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>Label</key><string>com.massar.examroom.dhcp</string>
  <key>ProgramArguments</key>
  <array>
    <string>/opt/homebrew/opt/dnsmasq/sbin/dnsmasq</string>
    <string>--keep-in-foreground</string>
    <string>--conf-file=/usr/local/etc/massar-exam-dnsmasq.conf</string>
  </array>
  <key>RunAtLoad</key><true/>
  <key>KeepAlive</key><true/>
</dict>
</plist>
PLIST
plutil -lint "$TEMP_PLIST"

sudo -v
sudo mkdir -p /usr/local/etc /var/db/massar-exam-room
if [ -f "$PLIST" ]; then
  sudo launchctl bootout system "$PLIST" || true
fi
sudo install -o root -g wheel -m 0644 "$ROOT/packaging/omada-direct-dnsmasq.conf" "$CONFIG"
sudo install -o root -g wheel -m 0644 "$TEMP_PLIST" "$PLIST"
sudo networksetup -setmanual Ethernet 10.77.0.1 255.255.252.0 0.0.0.0
sudo launchctl bootstrap system "$PLIST"
sudo launchctl enable system/com.massar.examroom.dhcp
sudo launchctl kickstart -k system/com.massar.examroom.dhcp
"$ROOT/install-macos-portal.sh"
echo 'Direct network ready: Mac 10.77.0.1, EAP reservation 10.77.0.2, student DHCP pool 10.77.0.20–10.77.3.250.'
