# ContextMenu

## Overview

`ContextMenu` shows a vertical menu at any cell position you choose and closes
through light dismiss when the user interacts outside of it. Attach one to a
control through `ControlBase.ContextMenu` so it opens on right-click, or open it
yourself at an explicit position with `Show`.

## API

| Member                         | Default        | Description                                                                                                              |
| ------------------------------ | -------------- | ------------------------------------------------------------------------------------------------------------------------ |
| `Items`                        | empty          | The typed collection of menu entries the menu manages.                                                                   |
| `IsOpen`                       | `false`        | Reports the committed popup visibility. Read-only.                                                                       |
| `Opening`, `Closing`, `Closed` | no subscribers | Lifecycle notifications raised in order around visibility changes.                                                       |
| `Show(int row, int col)`       | —              | Opens the menu at a zero-based root-cell position; a no-op until the menu is assigned to some `ControlBase.ContextMenu`. |
| `Close()`                      | —              | Closes the menu and clears its fixed origin; safe to call again.                                                         |

## Ownership

Assigning a menu to `ControlBase.ContextMenu` gives that control ownership of
the menu's retained popup presentation, an internal implementation detail not
exposed on the public API. Only one control can own a menu at a time: assigning
the same `ContextMenu` instance to a second control throws `ArgumentException`,
and the second control keeps whatever context menu it already had.

## Example

![The ContextMenu control rendered in the live showcase](../../images/controls/context-menu.png)

```csharp
var contextMenu = new ContextMenu();
```

## Expected behavior

- The menu owns its items, `Show` interprets its arguments as zero-based
  root-cell coordinates, calling `Show` while the menu is unattached does
  nothing, `Opening`, `Closing`, and `Closed` fire in order, `Close` is
  idempotent, and disposing the menu cleans it up.
- The open surface is placed relative to the root, clips to it, renders with
  menu appearance, and draws elevated above ordinary content.
- Right-click opens the menu on its owning control, keyboard navigation works
  within it, invoking an item closes it, and a press outside dismisses it
  through light dismiss.
