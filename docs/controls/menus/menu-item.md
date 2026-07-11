# MenuItem

## MenuItem contract

`MenuItem` represents a command, check/radio choice, separator, or submenu entry
inside a [Menu](menu.md#menu-contract).

## API

- `Header` and optional `Icon` use managed control ownership.
- `Command`, `CommandParameter`, and `InputGestureText` describe activation.
- `Kind` is command, check, radio, or separator.
- `IsChecked` is valid for check/radio kinds; `GroupName` applies to radio kind.
- `Items` contains submenu items; separators reject commands/content that would
  make them interactive.
- `Invoked`, checked-state events, and submenu events follow committed state.

## Interaction and rendering

Disabled/separator items cannot focus or invoke. Check toggles once; radio
selects one effective group member; submenu activation opens rather than running
a command unless explicitly configured. Header, shortcut, check mark, submenu
indicator, and state styling align through grid-like columns.

## Test obligations

Cover every kind, invalid property combinations, command/event order, check and
radio transitions, disabled/separator behavior, submenu ownership, keyboard and
pointer activation, Unicode headers, narrow clipping, style states, and cells.
