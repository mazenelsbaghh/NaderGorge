#!/bin/zsh
cd -- "${0:A:h}"
if ! command -v python3 >/dev/null 2>&1 || ! command -v go >/dev/null 2>&1; then
  echo 'Source mode needs Python 3.10 or later and Go.'
  read -r '?Press Return to close.'
  exit 1
fi
python3 run.py
read -r '?Press Return to close.'
