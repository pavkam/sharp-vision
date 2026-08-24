#!/usr/bin/env bash

set -euo pipefail

root="$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)"
output="${1:-$root/docs/images/showcase-dashboard.png}"
session="sharpvision-capture-$$"
temporary="$(mktemp -d)"
plain="$temporary/pane.txt"
ansi="$temporary/pane.ansi"
html="$temporary/pane.html"
page_filter_row=""

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

filter_pages() {
  local name="$1"

  if [[ -z "$page_filter_row" ]]; then
    printf 'The page filter is not visible.\n' >&2
    exit 1
  fi

  send_sgr 0 10 "$page_filter_row" M
  send_sgr 0 10 "$page_filter_row" m
  tmux send-keys -t "$session" C-a
  tmux send-keys -t "$session" BSpace
  tmux send-keys -t "$session" -l "$name"

  for _ in {1..50}; do
    tmux capture-pane -t "$session" -p -J >"$plain"

    if grep -Eq "[·›] $name" "$plain"; then
      return
    fi

    sleep 0.1
  done

  printf 'The %s filtered navigation entry is not visible.\n' "$name" >&2
  exit 1
}

select_page() {
  local name="$1"
  local witness="$2"
  local navigation_row

  filter_pages "$name"

  for _ in {1..50}; do
    tmux capture-pane -t "$session" -p -J >"$plain"

    if grep -q "› $name" "$plain" && grep -q "$witness" "$plain"; then
      return
    fi

    if grep -q "· $name" "$plain"; then
      break
    fi

    sleep 0.1
  done

  navigation_row="$(find_row "· $name")"

  if [[ -z "$navigation_row" ]]; then
    printf 'The %s filtered navigation entry is not visible.\n' "$name" >&2
    exit 1
  fi

  send_sgr 0 10 "$navigation_row" M
  send_sgr 0 10 "$navigation_row" m

  for _ in {1..50}; do
    tmux capture-pane -t "$session" -p -J >"$plain"

    if grep -q "› $name" "$plain" && grep -q "$witness" "$plain"; then
      return
    fi

    sleep 0.1
  done

  printf 'The showcase did not select the %s page through the page filter.\n' "$name" >&2
  exit 1
}

clear_page_filter() {
  send_sgr 0 10 "$page_filter_row" M
  send_sgr 0 10 "$page_filter_row" m
  tmux send-keys -t "$session" C-a
  tmux send-keys -t "$session" BSpace
  tmux send-keys -t "$session" Tab

  for _ in {1..50}; do
    tmux capture-pane -t "$session" -p -J >"$plain"

    if grep -q 'Filter pages' "$plain"; then
      return
    fi

    sleep 0.1
  done

  printf 'The showcase did not clear the page filter.\n' >&2
  exit 1
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
  "dotnet run --project examples/Showcase/SharpVision.Showcase.csproj --configuration Release --no-build"
tmux set-option -t "$session" status off

ready=false

for _ in {1..50}; do
  if ! tmux has-session -t "$session" 2>/dev/null; then
    printf 'The showcase terminated before its first page rendered.\n' >&2
    exit 1
  fi

  tmux capture-pane -t "$session" -p -J >"$plain"

  if grep -q 'Showcase' "$plain" &&
    grep -q 'Filter pages' "$plain" &&
    grep -q 'Quit  Ctrl+Q' "$plain" &&
    grep -q 'Every length kind' "$plain"; then
    ready=true
    break
  fi

  sleep 0.1
done

if [[ "$ready" != true ]]; then
  printf 'The showcase did not render its documentation page before timeout.\n' >&2
  exit 1
fi

page_filter_row="$(find_row 'Filter pages')"

if [[ -z "$page_filter_row" ]]; then
  printf 'The showcase did not expose the page filter.\n' >&2
  exit 1
fi

# The initially selected entry owns keyboard focus, so a normal terminal arrow
# must navigate immediately without a preceding click or Tab.
tmux send-keys -t "$session" Down

keyboard=false

for _ in {1..50}; do
  tmux capture-pane -t "$session" -p -J >"$plain"

  if grep -q '› Border' "$plain" && grep -q 'All built-in and custom families' "$plain"; then
    keyboard=true
    break
  fi

  sleep 0.1
done

if [[ "$keyboard" != true ]]; then
  printf 'The showcase did not navigate from Control to Border with the injected Down key.\n' >&2
  exit 1
fi

tmux send-keys -t "$session" Up

returned=false

for _ in {1..50}; do
  tmux capture-pane -t "$session" -p -J >"$plain"

  if grep -q '› Control' "$plain" && grep -q 'Every length kind' "$plain"; then
    returned=true
    break
  fi

  sleep 0.1
done

if [[ "$returned" != true ]]; then
  printf 'The showcase did not return from Border to Control with the injected Up key.\n' >&2
  exit 1
fi

# The complete Concepts-first catalog is taller than the 40-row capture
# viewport. Filtering selects the sole Canvas match and navigates to it without
# depending on an off-screen sidebar row.
filter_pages 'Canvas'

canvas=false

for _ in {1..50}; do
  tmux capture-pane -t "$session" -p -J >"$plain"

  if grep -q '› Canvas' "$plain" && grep -q 'Drawing fundamentals' "$plain"; then
    canvas=true
    break
  fi

  sleep 0.1
done

if [[ "$canvas" != true ]]; then
  printf 'The showcase did not navigate to the sole filtered Canvas page.\n' >&2
  exit 1
fi

# Prove the Document page owns one selection across ordinary text, a link, and
# embedded controls. Keep the drag entirely inside its mixed-content specimen;
# the changed status text is a deterministic semantic witness, while the ANSI
# capture retains the visible selection face for manual inspection.
select_page 'Document' 'Selection across mixed content'

document_start_row=""
document_end_row=""

for _ in {1..60}; do
  tmux capture-pane -t "$session" -p -J >"$plain"
  document_start_row="$(find_row 'One continuous selection')"
  document_end_row="$(find_row 'Ship')"

  if [[ -n "$document_start_row" && -n "$document_end_row" ]]; then
    break
  fi

  send_sgr 65 118 20 M
  sleep 0.08
done

if [[ -z "$document_start_row" || -z "$document_end_row" ]]; then
  printf 'The Document mixed-selection specimen did not become visible.\n' >&2
  exit 1
fi

document_start_column="$(find_column "$document_start_row" 'One continuous selection')"
document_end_column="$(find_column "$document_end_row" 'Ship')"

if [[ "$document_start_column" == 0 || "$document_end_column" == 0 ]]; then
  printf 'The Document mixed-selection geometry is incomplete.\n' >&2
  exit 1
fi

send_sgr 0 "$((document_start_column + 1))" "$document_start_row" M
send_sgr 32 "$((document_end_column + 2))" "$document_end_row" M
send_sgr 0 "$((document_end_column + 2))" "$document_end_row" m

document_selected=false

for _ in {1..50}; do
  tmux capture-pane -t "$session" -p -J >"$plain"

  if grep -Eq 'Selection: [1-9][0-9]* UTF-16 code unit\(s\)' "$plain"; then
    document_selected=true
    break
  fi

  send_sgr 65 118 20 M
  sleep 0.1
done

if [[ "$document_selected" != true ]]; then
  printf 'The Document did not retain the injected mixed-content selection.\n' >&2
  exit 1
fi

# The sidebar remains a pointer target after the Document selection gesture.
select_page 'Button' 'Activation log: waiting'

# Use real pointer reports to choose a font on the FigletText page.
select_page 'FigletText' 'Type text, then choose a font'

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
  candidate_font_row="$(find_row 'standard')"

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

  if grep -q 'Classy' "$plain"; then
    dropdown=true
    break
  fi

  sleep 0.1
done

if [[ "$dropdown" != true ]]; then
  printf 'The showcase did not open the Figlet font dropdown.\n' >&2
  exit 1
fi

font_row="$(find_row 'Classy')"

if [[ -z "$font_row" ]]; then
  printf 'The first Figlet font option is not visible.\n' >&2
  exit 1
fi

send_sgr 0 34 "$font_row" M
send_sgr 0 34 "$font_row" m

font=false

for _ in {1..50}; do
  tmux capture-pane -t "$session" -p -J >"$plain"

  if grep -q 'Previewing Classy' "$plain"; then
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
select_page 'ScrollBar' 'Drag the thumb'

scrollbar_value_row="$(find_row 'Thumb value:')"

if [[ -z "$scrollbar_value_row" || "$scrollbar_value_row" -le 2 ]]; then
  printf 'The horizontal ScrollBar value label is not visible.\n' >&2
  exit 1
fi

scrollbar_row=$((scrollbar_value_row - 2))

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
select_page 'Button' 'Click or press Enter'
clear_page_filter

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
