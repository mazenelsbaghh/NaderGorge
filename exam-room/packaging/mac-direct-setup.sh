#!/bin/sh
# Only the native app invokes this privileged installer. All candidates are
# checked before stopping services; failed activation restores the prior state.
set -eu
PATH=/usr/bin:/bin:/usr/sbin:/sbin
export PATH
umask 077

if [ "$(id -u)" -ne 0 ]; then
  echo 'Administrator authorization is required.' >&2
  exit 1
fi

SOURCE=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
ETHERNET_SERVICE=${1:-}
ETHERNET_DEVICE=${2:-}
CAPPORT_HOSTNAME=${3:-}
CERT_SOURCE=${4:-}
LEASE_DEADLINE=${5:-}
PORTAL_MODE=${6:-preserve}
DNSMASQ="$SOURCE/dnsmasq"
PROXY_SOURCE="$SOURCE/massar-exam-portal-proxy"
DNS_BIN=/usr/local/libexec/massar-exam-dnsmasq
DNS_CONFIG=/usr/local/etc/massar-exam-dnsmasq.conf
DNS_PLIST=/Library/LaunchDaemons/com.massar.examroom.dhcp.plist
PROXY_BIN=/usr/local/libexec/massar-exam-portal-proxy
PROXY_PLIST=/Library/LaunchDaemons/com.massar.examroom.portal.plist
CAP_DIR=/var/db/massar-exam-room/capport
CAP_META=/var/db/massar-exam-room/capport-public.json
MODE_FILE=/var/db/massar-exam-room/portal-mode.json
SETUP_LOCK=/var/run/massar-exam-network-setup.lock
DNS_LABEL=com.massar.examroom.dhcp
PROXY_LABEL=com.massar.examroom.portal
MANAGED_FILES="$DNS_BIN $DNS_CONFIG $DNS_PLIST $PROXY_BIN $PROXY_PLIST $CAP_DIR/fullchain.pem $CAP_DIR/privkey.pem $CAP_META $MODE_FILE"

fail() { echo "$1" >&2; exit 1; }
check_deadline() {
  case "$LEASE_DEADLINE" in ''|*[!0-9]*) fail 'تصريح تهيئة الشبكة غير صالح؛ أعد المحاولة من البرنامج.';; esac
  [ "${#LEASE_DEADLINE}" -le 10 ] || fail 'تصريح تهيئة الشبكة غير صالح؛ أعد المحاولة من البرنامج.'
  [ "$LEASE_DEADLINE" -gt "$(date +%s)" ] || fail 'انتهى تصريح تهيئة الشبكة؛ أعد المحاولة من البرنامج. لم يتم تغيير الشبكة.'
}
check_deadline
case "$PORTAL_MODE" in preserve|direct|legacy|https|welcome) ;; *) fail 'طريقة الدخول غير صالحة؛ اخترها من البرنامج.';; esac
[ -n "$ETHERNET_SERVICE" ] || fail 'اسم منفذ Ethernet غير موجود.'
printf '%s\n' "$ETHERNET_DEVICE" | grep -Eq '^en[0-9]+$' || fail 'منفذ Ethernet غير صالح.'
[ -x "$DNSMASQ" ] && [ -x "$PROXY_SOURCE" ] || fail 'ملفات تشغيل الشبكة غير مكتملة؛ ثبّت نسخة البرنامج كاملة.'

STAGE=$(mktemp -d)
TRANSACTION_STARTED=0
COMMITTED=0
KEEP_STAGE=0
INTERRUPTED=0
LOCK_HELD=0
DNS_LOADED=0
PROXY_LOADED=0
CAP_DIR_EXISTED=0
[ ! -d "$CAP_DIR" ] || CAP_DIR_EXISTED=1

restore_service() {
  label=$1
  plist=$2
  loaded=$3
  launchctl enable "system/$label" || ROLLBACK_OK=0
  if [ "$loaded" = 1 ]; then launchctl bootstrap system "$plist" || ROLLBACK_OK=0; fi
  if grep -Eq "\"$label\"[[:space:]]*=>[[:space:]]*true" "$STAGE/disabled"; then
    launchctl disable "system/$label" || ROLLBACK_OK=0
  fi
}

rollback() {
  set +e
  ROLLBACK_OK=1
  for label in "$DNS_LABEL" "$PROXY_LABEL"; do
    if launchctl print "system/$label" >/dev/null 2>&1; then
      launchctl bootout "system/$label" || ROLLBACK_OK=0
    fi
  done
  for target in $MANAGED_FILES; do
    name=$(basename "$target")
    if [ -f "$STAGE/old/$name" ]; then
      cp -p "$STAGE/old/$name" "$target" || ROLLBACK_OK=0
    else
      rm -f "$target" || ROLLBACK_OK=0
    fi
  done
  if [ "$CAP_DIR_EXISTED" = 0 ] && [ -d "$CAP_DIR" ]; then rmdir "$CAP_DIR" || ROLLBACK_OK=0; fi
  if grep -q '^DHCP Configuration$' "$STAGE/interface"; then
    client_id=$(sed -n 's/^Client ID: //p' "$STAGE/interface")
    [ -n "$client_id" ] || client_id=Empty
    networksetup -setdhcp "$ETHERNET_SERVICE" "$client_id" || ROLLBACK_OK=0
  else
    old_ip=$(sed -n 's/^IP address: //p' "$STAGE/interface")
    old_mask=$(sed -n 's/^Subnet mask: //p' "$STAGE/interface")
    old_router=$(sed -n 's/^Router: //p' "$STAGE/interface")
    case "$old_router" in ''|none) old_router=0.0.0.0;; esac
    networksetup -setmanual "$ETHERNET_SERVICE" "$old_ip" "$old_mask" "$old_router" || ROLLBACK_OK=0
  fi
  restore_service "$DNS_LABEL" "$DNS_PLIST" "$DNS_LOADED"
  restore_service "$PROXY_LABEL" "$PROXY_PLIST" "$PROXY_LOADED"
  [ "$ROLLBACK_OK" = 1 ]
}

finish() {
  exit_code=$?
  trap - EXIT HUP INT TERM
  if [ "$INTERRUPTED" = 1 ]; then
    # A foreground privileged command may outlive the interrupted shell. Do
    # not race it with rollback or let the app open a room while it can mutate.
    KEEP_STAGE=1
    echo "توقفت تهيئة الشبكة بصورة غير مؤكدة؛ احتُفظ بالقفل ونسخة الإدارة في $STAGE. أعد تشغيل الماك ثم افحص الاتصال قبل فتح القاعات." >&2
  elif [ "$TRANSACTION_STARTED" = 1 ] && [ "$COMMITTED" = 0 ]; then
    if rollback; then
      echo 'لم يكتمل التحديث؛ تمت إعادة ملفات الشبكة وإعداد المنفذ والخدمات السابقة.' >&2
    else
      KEEP_STAGE=1
      echo "لم تكتمل استعادة الشبكة؛ احتُفظ بالقفل ونسخة خاصة بالإدارة في $STAGE. راجع المشرف وأعد تشغيل الماك قبل فحص الشبكة وبدء امتحان." >&2
    fi
  fi
  if [ "$KEEP_STAGE" = 0 ]; then rm -rf "$STAGE"; fi
  if [ "$LOCK_HELD" = 1 ] && [ "$KEEP_STAGE" = 0 ]; then rmdir "$SETUP_LOCK"; fi
  exit "$exit_code"
}
trap finish EXIT
trap 'INTERRUPTED=1; exit 129' HUP
trap 'INTERRUPTED=1; exit 130' INT
trap 'INTERRUPTED=1; exit 143' TERM

# A second installer must not overwrite the first transaction's rollback state.
mkdir "$SETUP_LOCK" 2>/dev/null || fail 'يوجد تجهيز شبكة آخر جارٍ، أو تجهيز سابق توقف فجأة؛ إذا لم توجد تهيئة جارية أعد تشغيل الماك ثم افحص الاتصال.'
LOCK_HELD=1
check_wired_connection() {
  networksetup -listnetworkserviceorder > "$STAGE/service-order"
  awk -v service="$ETHERNET_SERVICE" -v device="$ETHERNET_DEVICE" '
    /^\([0-9]+\) / { current=$0; sub(/^\([0-9]+\) /, "", current) }
    current==service && index($0, "Device: " device ")") { found=1 }
    END { exit !found }
  ' "$STAGE/service-order" || fail 'اتغيّر منفذ Ethernet أثناء التجهيز؛ ثبّت الهَب والكابل وأعد المحاولة. لم يتم تغيير الإعداد.'
  ifconfig "$ETHERNET_DEVICE" > "$STAGE/link" || fail 'تعذر قراءة منفذ Ethernet؛ ثبّت الهَب وأعد المحاولة. لم يتم تغيير الإعداد.'
  grep -Eq '^[[:space:]]*status: active$' "$STAGE/link" || fail 'كابل Ethernet غير متصل؛ ثبّت الهَب وكابل Omada وأعد المحاولة. لم يتم تغيير الإعداد.'
}
exam_address_ready() {
  ifconfig "$ETHERNET_DEVICE" > "$STAGE/link" 2>/dev/null || return 1
  grep -Eq '^[[:space:]]*status: active$' "$STAGE/link" || return 1
  awk '$1=="inet" && $2=="10.77.0.1" && $3=="netmask" &&
       (tolower($4)=="0xfffffc00" || $4=="255.255.252.0") { found=1 }
       END { exit !found }' "$STAGE/link"
}
check_wired_connection
networksetup -getinfo "$ETHERNET_SERVICE" > "$STAGE/interface"
if ! grep -q '^DHCP Configuration$' "$STAGE/interface" &&
   ! grep -q '^IP address: 10.77.0.1$' "$STAGE/interface"; then
  fail 'منفذ Ethernet عليه إعداد يدوي مختلف؛ لم يتم تغييره.'
fi
if ! grep -q '^DHCP Configuration$' "$STAGE/interface"; then
  grep -Eq '^Subnet mask: [0-9.]+$' "$STAGE/interface" || fail 'تعذر حفظ إعداد منفذ Ethernet السابق؛ لم يتم تغييره.'
fi
launchctl print-disabled system > "$STAGE/disabled"
if launchctl print "system/$DNS_LABEL" > "$STAGE/dhcp-state" 2>/dev/null; then DNS_LOADED=1; fi
if launchctl print "system/$PROXY_LABEL" > "$STAGE/proxy-state" 2>/dev/null; then PROXY_LOADED=1; fi
[ "$DNS_LOADED" = 0 ] || [ -f "$DNS_PLIST" ] || fail 'خدمة توزيع العناوين مسجلة بدون ملفها؛ راجع تثبيت البرنامج قبل التحديث.'
[ "$PROXY_LOADED" = 0 ] || [ -f "$PROXY_PLIST" ] || fail 'خدمة الدخول مسجلة بدون ملفها؛ راجع تثبيت البرنامج قبل التحديث.'
OWN_PROXY_PID=$(sed -n 's/^[[:space:]]*pid = \([0-9][0-9]*\).*$/\1/p' "$STAGE/proxy-state")
OWN_DNS_PID=$(sed -n 's/^[[:space:]]*pid = \([0-9][0-9]*\).*$/\1/p' "$STAGE/dhcp-state")
list_port_owners() {
  endpoint=$1
  set -- "-i$endpoint"
  case "$endpoint" in TCP:*) set -- "$@" -sTCP:LISTEN;; esac
  if ! lsof -nP -Fpn "$@" > "$STAGE/port-list" 2> "$STAGE/port-error"; then
    [ ! -s "$STAGE/port-error" ] || return 1
  fi
  # -i also selects outbound client traffic to these remote ports.
  # Only a local bound port can conflict with this server.
  awk -v port="${endpoint##*:}" '/^p/ { pid=substr($0,2) }
    /^n/ { local=substr($0,2); sub(/->.*/, "", local); if (local ~ ":" port "$") print pid }' \
    "$STAGE/port-list" > "$STAGE/port-owners"
}
check_port_owner() {
  allowed_pid=$2
  list_port_owners "$1" || fail 'تعذر فحص منافذ DNS وDHCP؛ لم يتم تغيير الإعداد.'
  while IFS= read -r owner; do
    [ -z "$owner" ] || [ "$owner" = "$allowed_pid" ] || fail "المنفذ $endpoint مستخدم بواسطة خدمة أخرى. أوقف مشاركة الإنترنت أو موزع العناوين الآخر قبل التهيئة؛ لم يتم تغيير الإعداد."
  done < "$STAGE/port-owners"
}
# DNS/DHCP conflicts must be detected before replacing a working installation.
check_port_owner UDP:67 "$OWN_DNS_PID"
check_port_owner UDP:53 "$OWN_DNS_PID"
check_port_owner TCP:53 "$OWN_DNS_PID"
if [ "$PORTAL_MODE" = preserve ]; then
  if [ -f "$MODE_FILE" ]; then
    PORTAL_MODE=$(plutil -extract mode raw -o - "$MODE_FILE") || fail 'تعذر قراءة وضع الشبكة السابق؛ اختر الوضع العادي أو HTTPS صراحةً.'
  elif [ -n "$CAPPORT_HOSTNAME" ] || [ -n "$CERT_SOURCE" ] || [ -f "$CAP_META" ]; then
    # Older app versions selected HTTPS by passing certificate arguments.
    PORTAL_MODE=https
  elif [ -d "$CAP_DIR" ] || { [ -f "$DNS_CONFIG" ] && grep -q 'dhcp-option-force=114,' "$DNS_CONFIG"; }; then
    fail 'إعداد شهادة الدخول السابق غير مكتمل؛ اختر شهادة جديدة أو اختر الوضع العادي صراحةً.'
  else
    PORTAL_MODE=direct
  fi
fi
case "$PORTAL_MODE" in direct|legacy|https|welcome) ;; *) fail 'وضع الشبكة السابق غير صالح؛ اختر طريقة الدخول صراحةً.';; esac
printf '{"mode":"%s"}\n' "$PORTAL_MODE" > "$STAGE/portal-mode.json"
PORTS=80
if [ "$PORTAL_MODE" = https ]; then PORTS='80 443'; fi
for port in $PORTS; do
  if ! lsof -nP -t "-iTCP:$port" -sTCP:LISTEN > "$STAGE/port-$port" 2> "$STAGE/port-error"; then
    [ ! -s "$STAGE/port-error" ] || fail 'تعذر فحص البرامج التي تستخدم منافذ الشبكة؛ لم يتم تغيير الإعداد.'
  fi
  while IFS= read -r owner; do
    [ -z "$owner" ] || [ "$owner" = "$OWN_PROXY_PID" ] || fail "المنفذ $port مشغول ببرنامج آخر؛ أغلقه قبل تهيئة الشبكة. لم يتم تغيير الإعداد."
  done < "$STAGE/port-$port"
done

if [ "$PORTAL_MODE" = https ]; then
  if [ -z "$CAPPORT_HOSTNAME" ] && [ -z "$CERT_SOURCE" ]; then
    [ -f "$CAP_META" ] || fail 'وضع HTTPS يحتاج شهادة بوابة؛ اختر fullchain.pem وprivkey.pem من البرنامج أولًا.'
    CAPPORT_HOSTNAME=$(plutil -extract hostname raw -o - "$CAP_META") || fail 'بيانات شهادة الدخول غير مقروءة؛ اختر الشهادة من جديد قبل التحديث.'
    CERT_SOURCE="$CAP_DIR"
  fi
fi
CAPPORT_ENABLED=0
if [ "$PORTAL_MODE" = https ]; then
  [ -n "$CAPPORT_HOSTNAME" ] && [ -n "$CERT_SOURCE" ] || fail 'أدخل اسم النطاق ومجلد الشهادة معًا.'
  case "$CERT_SOURCE" in /*) ;; *) fail 'اختر مجلد شهادة بمسار كامل.';; esac
  printf '%s\n' "$CAPPORT_HOSTNAME" | grep -Eq '^[A-Za-z0-9][A-Za-z0-9.-]*[A-Za-z0-9]$' || fail 'اسم نطاق شهادة الدخول غير صالح.'
  case "$CAPPORT_HOSTNAME" in *.*) ;; *) fail 'شهادة الدخول تحتاج اسم نطاق كاملًا.';; esac
  [ -f "$CERT_SOURCE/fullchain.pem" ] && [ -f "$CERT_SOURCE/privkey.pem" ] || fail 'مجلد الشهادة يجب أن يحتوي fullchain.pem وprivkey.pem.'
  cp "$CERT_SOURCE/fullchain.pem" "$STAGE/fullchain.pem"
  cp "$CERT_SOURCE/privkey.pem" "$STAGE/privkey.pem"
  if ! "$PROXY_SOURCE" --check-capport --capport-hostname "$CAPPORT_HOSTNAME" \
       --capport-cert "$STAGE/fullchain.pem" --capport-key "$STAGE/privkey.pem" > "$STAGE/capport-public.json"; then
    fail 'شهادة الدخول غير صالحة أو انتهت؛ حدّث الشهادة يدويًا ثم حاول مرة أخرى. لم يتم تغيير الشبكة.'
  fi
  CAPPORT_HOSTNAME=$(plutil -extract hostname raw -o - "$STAGE/capport-public.json")
  printf '%s\n' "$CAPPORT_HOSTNAME" | grep -Eq '^[A-Za-z0-9][A-Za-z0-9.-]*[A-Za-z0-9]$' || fail 'فشل التحقق من اسم نطاق شهادة الدخول.'
  CAPPORT_ENABLED=1
fi

sed "s/^interface=en0$/interface=$ETHERNET_DEVICE/" "$SOURCE/omada-direct-dnsmasq.conf" > "$STAGE/dnsmasq.conf"
if [ "$CAPPORT_ENABLED" = 1 ]; then
  printf '\ndhcp-option-force=114,https://%s/capport\n' "$CAPPORT_HOSTNAME" >> "$STAGE/dnsmasq.conf"
fi
"$DNSMASQ" --test --conf-file="$STAGE/dnsmasq.conf"
cat > "$STAGE/dhcp.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
<key>Label</key><string>$DNS_LABEL</string>
<key>ProgramArguments</key><array><string>$DNS_BIN</string><string>--keep-in-foreground</string><string>--conf-file=$DNS_CONFIG</string></array>
<key>StandardErrorPath</key><string>/var/db/massar-exam-room/dnsmasq-error.log</string>
<key>RunAtLoad</key><true/><key>KeepAlive</key><true/>
</dict></plist>
PLIST
cat > "$STAGE/portal.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
<key>Label</key><string>$PROXY_LABEL</string>
<key>ProgramArguments</key><array><string>$PROXY_BIN</string>
PLIST
if [ "$PORTAL_MODE" = direct ]; then
  printf '<string>--no-captive-portal</string>\n' >> "$STAGE/portal.plist"
fi
if [ "$PORTAL_MODE" = welcome ]; then
  printf '<string>--welcome-portal</string>\n' >> "$STAGE/portal.plist"
fi
if [ "$CAPPORT_ENABLED" = 1 ]; then
  # Hostnames accept no XML metacharacters; certificate destinations are fixed.
  cat >> "$STAGE/portal.plist" <<PLIST
<string>--capport-hostname</string><string>$CAPPORT_HOSTNAME</string>
<string>--capport-cert</string><string>$CAP_DIR/fullchain.pem</string>
<string>--capport-key</string><string>$CAP_DIR/privkey.pem</string>
PLIST
fi
cat >> "$STAGE/portal.plist" <<'PLIST'
</array><key>RunAtLoad</key><true/><key>KeepAlive</key><true/>
</dict></plist>
PLIST
plutil -lint "$STAGE/dhcp.plist" "$STAGE/portal.plist"
mkdir "$STAGE/old"
for target in $MANAGED_FILES; do
  [ ! -L "$target" ] || fail 'أحد ملفات تثبيت الشبكة رابط رمزي غير متوقع؛ لم يتم تغيير الإعداد.'
  if [ -f "$target" ]; then cp -p "$target" "$STAGE/old/$(basename "$target")"; fi
done

# The administrator prompt/preflight can outlast the original authorization.
check_wired_connection
check_deadline
TRANSACTION_STARTED=1
for directory in /usr/local/etc /usr/local/libexec /Library/LaunchDaemons; do
  if [ ! -d "$directory" ]; then install -d -o root -g wheel -m 0755 "$directory"; fi
done
# Diagnostics/public metadata must remain readable by the unprivileged app.
# Private CAPPORT keys have their own root-only subdirectory below this one.
install -d -o root -g wheel -m 0755 /var/db/massar-exam-room
if [ "$DNS_LOADED" = 1 ]; then launchctl bootout "system/$DNS_LABEL"; fi
if [ "$PROXY_LOADED" = 1 ]; then launchctl bootout "system/$PROXY_LABEL"; fi
install -o root -g wheel -m 0644 "$STAGE/dnsmasq.conf" "$DNS_CONFIG"
install -o root -g wheel -m 0755 "$DNSMASQ" "$DNS_BIN"
install -o root -g wheel -m 0755 "$PROXY_SOURCE" "$PROXY_BIN"
install -o root -g wheel -m 0644 "$STAGE/dhcp.plist" "$DNS_PLIST"
install -o root -g wheel -m 0644 "$STAGE/portal.plist" "$PROXY_PLIST"
if [ "$CAPPORT_ENABLED" = 1 ]; then
  install -d -o root -g wheel -m 0700 "$CAP_DIR"
  install -o root -g wheel -m 0600 "$STAGE/fullchain.pem" "$CAP_DIR/fullchain.pem"
  install -o root -g wheel -m 0600 "$STAGE/privkey.pem" "$CAP_DIR/privkey.pem"
  install -o root -g wheel -m 0644 "$STAGE/capport-public.json" "$CAP_META"
fi
networksetup -setmanual "$ETHERNET_SERVICE" 10.77.0.1 255.255.252.0 0.0.0.0
# USB adapters can apply the saved configuration after networksetup returns.
# Wait for the real interface before launching a server bound to that address.
attempt=0
until exam_address_ready; do
  attempt=$((attempt + 1))
  [ "$attempt" -lt 10 ] || fail 'لم يظهر العنوان 10.77.0.1/22 على منفذ Ethernet؛ ثبّت الهَب والكابل وأعد المحاولة. جارٍ استعادة الإعداد السابق.'
  sleep 1
done
launchctl enable "system/$DNS_LABEL"
launchctl enable "system/$PROXY_LABEL"
launchctl bootstrap system "$DNS_PLIST"
launchctl bootstrap system "$PROXY_PLIST"
launchctl kickstart -k "system/$DNS_LABEL"
launchctl kickstart -k "system/$PROXY_LABEL"
for label in "$DNS_LABEL" "$PROXY_LABEL"; do
  attempt=0
  while ! launchctl print "system/$label" | grep -q 'state = running'; do
    attempt=$((attempt + 1))
    [ "$attempt" -lt 10 ] || fail 'خدمة الشبكة لم تبدأ؛ جارٍ استعادة الإعداد السابق.'
    sleep 1
  done
done
attempt=0
while :; do
  dhcp_pid=$(launchctl print "system/$DNS_LABEL" | sed -n 's/^[[:space:]]*pid = \([0-9][0-9]*\).*$/\1/p')
  if [ -n "$dhcp_pid" ] && list_port_owners 4UDP:67 &&
     grep -Fxq "$dhcp_pid" "$STAGE/port-owners"; then break; fi
  attempt=$((attempt + 1))
  [ "$attempt" -lt 10 ] || fail 'خدمة DHCP لم تفتح منفذ توزيع العناوين؛ قد يكون محوّل USB مفصولًا أو العنوان 10.77.0.1 غير موجود. جارٍ استعادة الإعداد السابق.'
  sleep 1
done
exam_address_ready || fail 'انقطع منفذ Ethernet أو فقد عنوانه أثناء التجهيز؛ جارٍ استعادة الإعداد السابق. ثبّت الهَب والكابل وأعد المحاولة.'
install -o root -g wheel -m 0644 "$STAGE/portal-mode.json" "$MODE_FILE"
COMMITTED=1
echo 'تم تحديث تجهيز شبكة الامتحان والتحقق من تشغيل الخدمتين.'
