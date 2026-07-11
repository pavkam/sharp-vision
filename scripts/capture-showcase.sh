#!/usr/bin/env bash

set -euo pipefail

root="$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)"
output="${1:-$root/docs/images/showcase-border.png}"
session="sharpvision-capture-$$"
temporary="$(mktemp -d)"
plain="$temporary/pane.txt"
ansi="$temporary/pane.ansi"
html="$temporary/pane.html"

cleanup() {
  tmux kill-session -t "$session" 2>/dev/null || true
  rm -rf "$temporary"
}

trap cleanup EXIT INT TERM

for dependency in dotnet node playwright tmux; do
  if ! command -v "$dependency" >/dev/null 2>&1; then
    printf 'Required capture dependency is missing: %s\n' "$dependency" >&2
    exit 1
  fi
done

mkdir -p "$(dirname -- "$output")"
dotnet build "$root/SharpVision.slnx" --configuration Release --no-restore --verbosity minimal
tmux new-session -d -x 120 -y 40 -s "$session" -c "$root" \
  "dotnet run --project src/SharpVision.Showcase/SharpVision.Showcase.csproj --configuration Release --no-build"
tmux set-option -t "$session" status off

ready=false

for _ in {1..50}; do
  if ! tmux has-session -t "$session" 2>/dev/null; then
    printf 'The showcase terminated before its first page rendered.\n' >&2
    exit 1
  fi

  tmux capture-pane -t "$session" -p -J >"$plain"

  if grep -q 'Overview' "$plain" && grep -q 'Examples' "$plain"; then
    ready=true
    break
  fi

  sleep 0.1
done

if [[ "$ready" != true ]]; then
  printf 'The showcase did not render its documentation page before timeout.\n' >&2
  exit 1
fi

tmux capture-pane -t "$session" -p -e >"$ansi"
node "$root/scripts/render-terminal-capture.mjs" "$ansi" "$html"
playwright screenshot \
  --browser chromium \
  --viewport-size "1280,900" \
  --full-page \
  "file://$html" \
  "$output"

if ! file "$output" | grep -q 'PNG image data'; then
  printf 'The captured output is not a PNG image.\n' >&2
  exit 1
fi

printf 'Captured live SharpVision pane: %s\n' "$output"
