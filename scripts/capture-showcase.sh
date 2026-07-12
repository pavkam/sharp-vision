#!/usr/bin/env bash

set -euo pipefail

root="$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)"
output="${1:-$root/docs/images/showcase-dashboard.png}"
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

send_sgr() {
  local code="$1"
  local x="$2"
  local y="$3"
  local suffix="$4"
  local bytes
  local arguments=()
  bytes="$(printf '\033[<%s;%s;%s%s' "$code" "$x" "$y" "$suffix" | od -An -tx1 | tr -d ' \n')"

  while [[ -n "$bytes" ]]; do
    arguments+=("${bytes:0:2}")
    bytes="${bytes:2}"
  done

  tmux send-keys -t "$session" -H "${arguments[@]}"
}

find_row() {
  local text="$1"
  awk -v text="$text" 'index($0, text) { print NR; exit }' "$plain"
}

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

# The initially selected entry owns keyboard focus, so a normal terminal arrow
# must navigate immediately without a preceding click or Tab.
tmux send-keys -t "$session" Down

keyboard=false

for _ in {1..50}; do
  tmux capture-pane -t "$session" -p -J >"$plain"

  if grep -q '› Button' "$plain"; then
    keyboard=true
    break
  fi

  sleep 0.1
done

if [[ "$keyboard" != true ]]; then
  printf 'The showcase did not handle the injected Down key.\n' >&2
  exit 1
fi

# A complete primary SGR press/release must activate navigation on its own.
# Do not append a key here: that would hide host-side input buffering defects.
tmux send-keys -t "$session" -H 1b 5b 3c 30 3b 33 3b 39 4d 1b 5b 3c 30 3b 33 3b 39 6d

canvas=false

for _ in {1..50}; do
  tmux capture-pane -t "$session" -p -J >"$plain"

  if grep -q '› Canvas' "$plain"; then
    canvas=true
    break
  fi

  sleep 0.1
done

if [[ "$canvas" != true ]]; then
  printf 'The showcase did not handle the injected Canvas SGR mouse click.\n' >&2
  exit 1
fi

tmux send-keys -t "$session" -H 1b 5b 3c 30 3b 33 3b 38 4d 1b 5b 3c 30 3b 33 3b 38 6d

button=false

for _ in {1..50}; do
  tmux capture-pane -t "$session" -p -J >"$plain"

  if grep -q '› Button' "$plain"; then
    button=true
    break
  fi

  sleep 0.1
done

if [[ "$button" != true ]]; then
  printf 'The showcase did not handle the injected Button SGR mouse click.\n' >&2
  exit 1
fi

# Button is catalog index 1. Four ordinary arrows reach the FigletText editor,
# where a real pointer press opens the 400-font dropdown and another selects
# the first visible catalog entry.
tmux send-keys -t "$session" Down Down Down Down

figlet=false

for _ in {1..50}; do
  tmux capture-pane -t "$session" -p -J >"$plain"

  if grep -q 'Type text, then choose a font' "$plain"; then
    figlet=true
    break
  fi

  sleep 0.1
done

if [[ "$figlet" != true ]]; then
  printf 'The showcase did not navigate to the FigletText editor.\n' >&2
  exit 1
fi

font_row="$(find_row 'Font: Standard')"

if [[ -z "$font_row" ]]; then
  printf 'The Figlet font selector is not visible.\n' >&2
  exit 1
fi

send_sgr 0 34 "$font_row" M
send_sgr 0 34 "$font_row" m

dropdown=false

for _ in {1..50}; do
  tmux capture-pane -t "$session" -p -J >"$plain"

  if grep -q '1Row' "$plain"; then
    dropdown=true
    break
  fi

  sleep 0.1
done

if [[ "$dropdown" != true ]]; then
  printf 'The showcase did not open the Figlet font dropdown.\n' >&2
  exit 1
fi

font_row="$(find_row '1Row')"

if [[ -z "$font_row" ]]; then
  printf 'The first Figlet font option is not visible.\n' >&2
  exit 1
fi

send_sgr 0 34 "$font_row" M
send_sgr 0 34 "$font_row" m

font=false

for _ in {1..50}; do
  tmux capture-pane -t "$session" -p -J >"$plain"

  if grep -q 'Previewing 1Row' "$plain"; then
    font=true
    break
  fi

  sleep 0.1
done

if [[ "$font" != true ]]; then
  printf 'The showcase did not select the Figlet font from the dropdown.\n' >&2
  exit 1
fi

# The sidebar remains a pointer target even after the dropdown returns focus.
# Select ScrollBar, then drag its horizontal thumb from its initial geometry to
# the right edge with a complete SGR press/move/release sequence.
tmux send-keys -t "$session" -H 1b 5b 3c 30 3b 33 3b 31 38 4d 1b 5b 3c 30 3b 33 3b 31 38 6d

scrollbar=false

for _ in {1..50}; do
  tmux capture-pane -t "$session" -p -J >"$plain"

  if grep -q 'Drag the solid thumb' "$plain"; then
    scrollbar=true
    break
  fi

  sleep 0.1
done

if [[ "$scrollbar" != true ]]; then
  printf 'The showcase did not select the ScrollBar page.\n' >&2
  exit 1
fi

scrollbar_row="$(find_row '◀')"

if [[ -z "$scrollbar_row" ]]; then
  printf 'The horizontal ScrollBar thumb is not visible.\n' >&2
  exit 1
fi

send_sgr 0 41 "$scrollbar_row" M
send_sgr 32 59 "$scrollbar_row" M
send_sgr 0 59 "$scrollbar_row" m

dragged=false

for _ in {1..50}; do
  tmux capture-pane -t "$session" -p -J >"$plain"

  if grep -q 'Thumb value: 100' "$plain"; then
    dragged=true
    break
  fi

  sleep 0.1
done

if [[ "$dragged" != true ]]; then
  printf 'The showcase did not handle the injected ScrollBar thumb drag.\n' >&2
  exit 1
fi

# Keep the checked-in dashboard image centered on the concise Button example.
tmux send-keys -t "$session" -H 1b 5b 3c 30 3b 33 3b 38 4d 1b 5b 3c 30 3b 33 3b 38 6d

for _ in {1..50}; do
  tmux capture-pane -t "$session" -p -J >"$plain"

  if grep -q 'Click or press Enter' "$plain"; then
    break
  fi

  sleep 0.1
done

if ! grep -q '› Button' "$plain"; then
  printf 'The showcase did not return to the Button page for capture.\n' >&2
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
