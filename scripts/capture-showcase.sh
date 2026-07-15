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

find_column() {
  local row="$1"
  local text="$2"
  node - "$plain" "$row" "$text" <<'NODE'
const [file, row, text] = process.argv.slice(2);
const line = require("node:fs").readFileSync(file, "utf8").split(/\r?\n/u)[Number(row) - 1] ?? "";
process.stdout.write(String(line.indexOf(text) + 1));
NODE
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

  if grep -q 'SHARP VISION' "$plain" && grep -q 'Overview' "$plain"; then
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

  if grep -q '› Canvas' "$plain" && grep -q 'Fixed placement' "$plain"; then
    keyboard=true
    break
  fi

  sleep 0.1
done

if [[ "$keyboard" != true ]]; then
  printf 'The showcase did not navigate from Button to Canvas with the injected Down key.\n' >&2
  exit 1
fi

tmux send-keys -t "$session" Up

returned=false

for _ in {1..50}; do
  tmux capture-pane -t "$session" -p -J >"$plain"

  if grep -q '› Button' "$plain" && grep -q 'Activation log: waiting' "$plain"; then
    returned=true
    break
  fi

  sleep 0.1
done

if [[ "$returned" != true ]]; then
  printf 'The showcase did not return from Canvas to Button with the injected Up key.\n' >&2
  exit 1
fi

canvas_navigation_row="$(find_row '· Canvas')"
button_navigation_row="$(find_row '› Button')"

if [[ -z "$canvas_navigation_row" || -z "$button_navigation_row" ]]; then
  printf 'The Button and Canvas sidebar entries are not both visible.\n' >&2
  exit 1
fi

# Any-event tracking must report passive pointer motion. Hover Canvas without a
# button, prove its visible marker changes, then prove the terminal leave clears it.
send_sgr 35 3 "$canvas_navigation_row" M

hovered=false

for _ in {1..50}; do
  tmux capture-pane -t "$session" -p -J >"$plain"

  if grep -q '› Canvas' "$plain"; then
    hovered=true
    break
  fi

  sleep 0.1
done

if [[ "$hovered" != true ]]; then
  printf 'The showcase did not visibly hover Canvas after passive SGR motion.\n' >&2
  exit 1
fi

send_sgr 35 0 0 M

for _ in {1..50}; do
  tmux capture-pane -t "$session" -p -J >"$plain"

  if grep -q '· Canvas' "$plain"; then
    break
  fi

  sleep 0.1
done

if ! grep -q '· Canvas' "$plain"; then
  printf 'The showcase did not clear Canvas hover after the SGR leave report.\n' >&2
  exit 1
fi

# A complete primary SGR press/release must activate navigation on its own.
# Do not append a key here: that would hide host-side input buffering defects.
send_sgr 0 10 "$canvas_navigation_row" M
send_sgr 0 10 "$canvas_navigation_row" m

canvas=false

for _ in {1..50}; do
  tmux capture-pane -t "$session" -p -J >"$plain"

  if grep -q '› Canvas' "$plain" && grep -q 'Fixed placement' "$plain"; then
    canvas=true
    break
  fi

  sleep 0.1
done

if [[ "$canvas" != true ]]; then
  printf 'The showcase did not handle the injected Canvas SGR mouse click.\n' >&2
  exit 1
fi

send_sgr 0 10 "$button_navigation_row" M
send_sgr 0 10 "$button_navigation_row" m

button=false

for _ in {1..50}; do
  tmux capture-pane -t "$session" -p -J >"$plain"

  if grep -q '› Button' "$plain" && grep -q 'Activation log: waiting' "$plain"; then
    button=true
    break
  fi

  sleep 0.1
done

if [[ "$button" != true ]]; then
  printf 'The showcase did not handle the injected Button SGR mouse click.\n' >&2
  exit 1
fi

# Select FigletText by its visible sidebar label rather than a catalog position,
# then use real pointer reports to open the ComboBox and choose a font.
figlet_navigation_row="$(find_row 'FigletText')"

if [[ -z "$figlet_navigation_row" ]]; then
  printf 'The FigletText sidebar entry is not visible.\n' >&2
  exit 1
fi

send_sgr 0 10 "$figlet_navigation_row" M
send_sgr 0 10 "$figlet_navigation_row" m

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

# The documentation headline precedes the interactive editor. Scroll the main
# reading pane using a real wheel report until the ComboBox field is visible.
for _ in {1..12}; do
  send_sgr 65 40 12 M
done

font_row=""
previous_font_row=""
stable_font_row=0

for _ in {1..100}; do
  tmux capture-pane -t "$session" -p -J >"$plain"
  candidate_font_row="$(find_row 'Standard')"

  if [[ -n "$candidate_font_row" && "$candidate_font_row" == "$previous_font_row" ]]; then
    stable_font_row=$((stable_font_row + 1))
  else
    stable_font_row=0
  fi

  if [[ "$stable_font_row" -ge 2 ]]; then
    font_row="$candidate_font_row"
    break
  fi

  previous_font_row="$candidate_font_row"
  sleep 0.05
done

if [[ -z "$font_row" ]]; then
  printf 'The Figlet font selector did not settle into a stable row.\n' >&2
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
scrollbar_navigation_row="$(find_row 'ScrollBar')"

if [[ -z "$scrollbar_navigation_row" ]]; then
  printf 'The ScrollBar sidebar entry is not visible.\n' >&2
  exit 1
fi

send_sgr 0 10 "$scrollbar_navigation_row" M
send_sgr 0 10 "$scrollbar_navigation_row" m

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

thumb_column="$(find_column "$scrollbar_row" '█')"

if [[ "$thumb_column" == 0 ]]; then
  thumb_column="$(find_column "$scrollbar_row" '▓')"
fi

end_column="$(find_column "$scrollbar_row" '▶')"

if [[ -z "$thumb_column" || "$thumb_column" == 0 || -z "$end_column" || "$end_column" == 0 ]]; then
  printf 'The rendered ScrollBar thumb geometry is incomplete.\n' >&2
  exit 1
fi

middle_column=$(((thumb_column + end_column) / 2))
final_column=$((end_column - 1))

send_sgr 0 "$thumb_column" "$scrollbar_row" M
send_sgr 32 "$middle_column" "$scrollbar_row" M

intermediate=false

for _ in {1..50}; do
  tmux capture-pane -t "$session" -p -J >"$plain"

  if grep -Eq 'Thumb value: [4-9][0-9]' "$plain"; then
    intermediate=true
    break
  fi

  sleep 0.1
done

if [[ "$intermediate" != true ]]; then
  printf 'The showcase did not update the ScrollBar during the injected SGR drag.\n' >&2
  exit 1
fi

send_sgr 32 "$final_column" "$scrollbar_row" M
send_sgr 0 "$final_column" "$scrollbar_row" m

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
button_navigation_row="$(find_row 'Button')"

if [[ -z "$button_navigation_row" ]]; then
  printf 'The Button sidebar entry is not visible.\n' >&2
  exit 1
fi

send_sgr 0 10 "$button_navigation_row" M
send_sgr 0 10 "$button_navigation_row" m

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
