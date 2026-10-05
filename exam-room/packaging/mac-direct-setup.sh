#!/bin/sh
# Privileged half of the in-app setup. This file and its payload are bundled
# with the desktop app; macOS asks the owner for authorization before it runs.
set -eu

if [ "$(id -u)" -ne 0 ]; then
  echo 'Administrator authorization is required.' >&2
  exit 1
fi

SOURCE=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
ETHERNET_SERVICE=${1:-}
ETHERNET_DEVICE=${2:-}
if ! printf '%s\n' "$ETHERNET_DEVICE" | grep -Eq '^en[0-9]+$'; then
  echo 'Ethernet device is invalid.' >&2
  exit 1
fi
DNSMASQ="$SOURCE/dnsmasq"
DNS_BIN=/usr/local/libexec/massar-exam-dnsmasq
DNS_CONFIG=/usr/local/etc/massar-exam-dnsmasq.conf
DNS_PLIST=/Library/LaunchDaemons/com.massar.examroom.dhcp.plist
PROXY_BIN=/usr/local/libexec/massar-exam-portal-proxy
PROXY_PLIST=/Library/LaunchDaemons/com.massar.examroom.portal.plist

if [ ! -x "$DNSMASQ" ]; then
  echo 'مكوّن توزيع العناوين غير موجود داخل نسخة البرنامج.' >&2
  exit 1
fi
if [ ! -x "$SOURCE/massar-exam-portal-proxy" ]; then
  echo 'ملف وصلة صفحة الطلاب غير موجود داخل حزمة البرنامج.' >&2
  exit 1
fi
if [ -z "$ETHERNET_SERVICE" ]; then
  echo 'Ethernet service is missing.' >&2
  exit 1
fi
if ! networksetup -listnetworkserviceorder | grep -Fq "Device: $ETHERNET_DEVICE)"; then
  echo 'Ethernet device is not available.' >&2
  exit 1
fi
TMP_CONFIG=$(mktemp)
trap 'rm -f "$TMP_CONFIG"' EXIT
sed "s/^interface=en0$/interface=$ETHERNET_DEVICE/" "$SOURCE/omada-direct-dnsmasq.conf" > "$TMP_CONFIG"
"$DNSMASQ" --test --conf-file="$TMP_CONFIG"
if ! networksetup -getinfo "$ETHERNET_SERVICE" | grep -q '^DHCP Configuration$' &&
   ! networksetup -getinfo "$ETHERNET_SERVICE" | grep -q '^IP address: 10.77.0.1$'; then
  echo 'منفذ Ethernet عليه إعداد يدوي مختلف؛ لم يتم تغييره.' >&2
  exit 1
fi
if [ ! -f "$PROXY_PLIST" ] && lsof -nP -iTCP:80 -sTCP:LISTEN | grep -q .; then
  echo 'المنفذ 80 مشغول ببرنامج آخر؛ لم يتم تغييره.' >&2
  exit 1
fi

mkdir -p /usr/local/etc /usr/local/libexec /var/db/massar-exam-room
if [ -f "$DNS_PLIST" ]; then launchctl bootout system "$DNS_PLIST" || true; fi
if [ -f "$PROXY_PLIST" ]; then launchctl bootout system "$PROXY_PLIST" || true; fi
: > /var/db/massar-exam-room/dnsmasq-error.log
: > /var/db/massar-exam-room/dnsmasq.log
install -o root -g wheel -m 0644 "$TMP_CONFIG" "$DNS_CONFIG"
install -o root -g wheel -m 0755 "$DNSMASQ" "$DNS_BIN"
install -o root -g wheel -m 0755 "$SOURCE/massar-exam-portal-proxy" "$PROXY_BIN"
cat > "$DNS_PLIST" <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
<key>Label</key><string>com.massar.examroom.dhcp</string>
<key>ProgramArguments</key><array><string>/usr/local/libexec/massar-exam-dnsmasq</string><string>--keep-in-foreground</string><string>--conf-file=/usr/local/etc/massar-exam-dnsmasq.conf</string></array>
<key>StandardErrorPath</key><string>/var/db/massar-exam-room/dnsmasq-error.log</string>
<key>RunAtLoad</key><true/><key>KeepAlive</key><true/>
</dict></plist>
PLIST
cat > "$PROXY_PLIST" <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
<key>Label</key><string>com.massar.examroom.portal</string>
<key>ProgramArguments</key><array><string>/usr/local/libexec/massar-exam-portal-proxy</string></array>
<key>RunAtLoad</key><true/><key>KeepAlive</key><true/>
</dict></plist>
PLIST
chmod 0644 "$DNS_PLIST" "$PROXY_PLIST"
chown root:wheel "$DNS_PLIST" "$PROXY_PLIST"
plutil -lint "$DNS_PLIST" "$PROXY_PLIST"
networksetup -setmanual "$ETHERNET_SERVICE" 10.77.0.1 255.255.252.0 0.0.0.0
launchctl bootstrap system "$DNS_PLIST"
launchctl bootstrap system "$PROXY_PLIST"
launchctl enable system/com.massar.examroom.dhcp
launchctl enable system/com.massar.examroom.portal
launchctl kickstart -k system/com.massar.examroom.dhcp
launchctl kickstart -k system/com.massar.examroom.portal
echo 'Mac direct exam network prepared.'
