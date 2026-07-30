#!/bin/sh
set -eu

prepare_secret() {
  env_name="$1"
  source_path="$(eval "printf '%s' \"\${$env_name:-}\"")"

  if [ -z "$source_path" ] || [ ! -f "$source_path" ]; then
    return 0
  fi

  dest_path="/app/.secrets/$(basename "$source_path")"
  if [ "$source_path" != "$dest_path" ]; then
    cp "$source_path" "$dest_path"
    chown worker:worker "$dest_path"
    chmod 0400 "$dest_path"
    export "$env_name=$dest_path"
  fi
}

prepare_secret GOOGLE_APPLICATION_CREDENTIALS
prepare_secret FIREBASE_APPLICATION_CREDENTIALS

exec gosu worker "$@"
