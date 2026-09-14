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

prepare_secret FIREBASE_APPLICATION_CREDENTIALS

prepare_shared_storage_group() {
  shared_gid="${MASSAR_SHARED_GID:-}"
  if [ -z "$shared_gid" ]; then
    return 0
  fi

  case "$shared_gid" in
    *[!0-9]*)
      echo "MASSAR_SHARED_GID must be a numeric group id." >&2
      exit 1
      ;;
  esac

  shared_group="$(getent group "$shared_gid" | cut -d: -f1)"
  if [ -z "$shared_group" ]; then
    shared_group="massar-shared"
    groupadd --system --gid "$shared_gid" "$shared_group"
  fi

  usermod --append --groups "$shared_group" worker
}

prepare_shared_storage_group

exec gosu worker "$@"
