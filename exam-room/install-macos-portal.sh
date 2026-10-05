#!/bin/sh
# Run from the source checkout on the Mac that hosts the exam.
set -eu

if [ "$(uname -s)" != Darwin ]; then
  echo 'This installer runs only on macOS.' >&2
  exit 1
fi

ROOT=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
BIN_DIR=/usr/local/libexec
BIN=$BIN_DIR/massar-exam-portal-proxy
PLIST=/Library/LaunchDaemons/com.massar.examroom.portal.plist
TEMP_BIN=$(mktemp "${TMPDIR:-/tmp}/massar-portal.XXXXXX")
TEMP_PLIST=$(mktemp "${TMPDIR:-/tmp}/massar-portal-plist.XXXXXX")
trap 'rm -f "$TEMP_BIN" "$TEMP_PLIST"' EXIT HUP INT TERM

(cd "$ROOT" && go build -trimpath -o "$TEMP_BIN" ./cmd/portal-proxy)
cat > "$TEMP_PLIST" <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>Label</key><string>com.massar.examroom.portal</string>
  <key>ProgramArguments</key>
  <array><string>/usr/local/libexec/massar-exam-portal-proxy</string></array>
  <key>RunAtLoad</key><true/>
  <key>KeepAlive</key><true/>
</dict>
</plist>
PLIST
plutil -lint "$TEMP_PLIST"
if [ ! -f "$PLIST" ] && lsof -nP -iTCP:80 -sTCP:LISTEN | grep -q .; then
  echo 'Port 80 is already in use. Stop the other service before installing the portal helper.' >&2
  exit 1
fi

# The owner enters their macOS password into sudo's own prompt; it is never
# requested by or stored in the exam app.
sudo -v
sudo mkdir -p "$BIN_DIR"
if [ -f "$PLIST" ]; then
  sudo launchctl bootout system "$PLIST" || true
fi
sudo install -o root -g wheel -m 0755 "$TEMP_BIN" "$BIN"
sudo install -o root -g wheel -m 0644 "$TEMP_PLIST" "$PLIST"
sudo launchctl bootstrap system "$PLIST"
sudo launchctl enable system/com.massar.examroom.portal
sudo launchctl kickstart -k system/com.massar.examroom.portal
echo 'Portal helper installed. Check http://<this Mac LAN IP>/ from a student phone.'
