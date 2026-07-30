#!/bin/sh
# Docker entrypoint for frontend containers.
# Validates that APP_SURFACE and NEXT_PUBLIC_APP_SURFACE are set before starting.

set -e

if [ -z "$APP_SURFACE" ]; then
  echo "ERROR: APP_SURFACE environment variable is not set."
  echo "Each frontend container must have APP_SURFACE set to one of: landing, student, admin, teacher, assistant"
  exit 1
fi

if [ -z "$NEXT_PUBLIC_APP_SURFACE" ]; then
  echo "ERROR: NEXT_PUBLIC_APP_SURFACE environment variable is not set."
  echo "It must match APP_SURFACE for client-side surface detection."
  exit 1
fi

VALID_SURFACES="landing student admin teacher assistant"
if ! echo "$VALID_SURFACES" | grep -qw "$APP_SURFACE"; then
  echo "ERROR: APP_SURFACE='$APP_SURFACE' is not valid."
  echo "Must be one of: $VALID_SURFACES"
  exit 1
fi

echo "Starting frontend with surface: $APP_SURFACE"
exec "$@"
