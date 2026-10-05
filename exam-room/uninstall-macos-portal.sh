#!/bin/sh
set -eu
PLIST=/Library/LaunchDaemons/com.massar.examroom.portal.plist
BIN=/usr/local/libexec/massar-exam-portal-proxy
sudo -v
if [ -f "$PLIST" ]; then
  sudo launchctl bootout system "$PLIST" || true
  sudo rm "$PLIST"
fi
sudo rm -f "$BIN"
echo 'Portal helper removed.'
