# MenuItem

## MenuItem contract

`MenuItem` represents a command, check/radio choice, or separator inside a
[Menu](menu.md#menu-contract).

## API

- `Header` is non-null UTF-16 text measured and drawn by grapheme-safe terminal
  cells.
- `Kind` is command, check, radio, or separator.
- `IsChecked` is valid only for check/radio kinds; `GroupName` scopes radio
  selection within its containing menu.
- `Invoked` reports the committed activation after check/radio state updates.

## Interaction and rendering

Separators cannot focus, hit test, or invoke. Check toggles once; radio selects
one matching group member. Check entries reserve `[ ]`/`[x]` marker cells; radio
entries reserve `○`/`◉`, so state changes do not move the header.

## Test obligations

Cover every kind, invalid checked-state assignment, check/radio event order,
separator behavior, keyboard and pointer activation, Unicode headers, narrow
clipping, styles, and cells.
