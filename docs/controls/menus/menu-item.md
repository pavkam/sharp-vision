# MenuItem and MenuSeparator

## Overview

`MenuItem` is a sealed [`PressableBase`](../pressable.md#overview) that
represents a command, check, or radio entry inside a [Menu](menu.md#overview).
Its label comes from the inherited `Text` property, which is the item's only
caption surface.

## API

| Member                           | Default         | Purpose                                                                                        |
| -------------------------------- | --------------- | ---------------------------------------------------------------------------------------------- |
| Inherited `Text`                 | `""`            | The item's label.                                                                              |
| `Kind`                           | `Command`       | Chooses command, check, or radio activation semantics.                                         |
| `IsChecked`, `GroupName`         | `false`, `null` | Hold the check state and scope radio exclusivity.                                              |
| `ShortcutText`                   | `null`          | Shows a dim, right-aligned hint; registers no key binding.                                     |
| `Shortcut`                       | `null`          | A typed `KeyGesture` that both derives `ShortcutText` and activates the item application-wide. |
| `Submenu`                        | `null`          | An optional popup-layer child menu the item owns.                                              |
| `UncheckedGlyph`, `CheckedGlyph` | Code-owned      | Override state marks; `ResetGlyphs()` restores code-owned defaults.                            |
| `Invoked`                        | No subscribers  | Raised after activation commits, once any check state has updated.                             |
| `PerformInvoke()`                | —               | Invokes the item programmatically.                                                             |
| `Command`, `CommandParameter`    | `null`          | Inherited from `PressableBase`; runs after `Invoked`.                                          |

## Behavior

- `Kind` is one of command, check, or radio.
- `IsChecked` applies only to the check and radio kinds. `GroupName` scopes
  radio selection within the containing menu.
- `Invoked` reports the committed activation after any check or radio state
  update. The bound `Command`, if any and if `CanExecute` allows it, runs after
  `Invoked` and after the menu's own `ItemInvoked` notification. An item with an
  open submenu never reaches this: activating it toggles the submenu instead,
  and no `Invoked` or `Command` fires. Every invocation event reports one
  defined keyboard, pointer, or programmatic activation cause.

A check or radio entry reserves one cell for its code-owned selection glyph plus
one separator cell in front of the caption. The caption is measured against the
remaining constraint and arranged through the common inherited caption slot, so
state changes do not move it, and a collapsed caption contributes no margin.
When a radio selection changes, every matching item's fields are staged before
the first `PropertyChanged(IsChecked)` callback runs, and a reentrant selection
suppresses the stale outer notifications.

Menu items stretch horizontally by default. In a vertical menu every item
therefore fills the shared menu width, which lets content, shortcut hints, and
separators line up as one aligned surface. An explicit alignment set by the
caller still wins.

An item's own `Invoked` subscribers finish before the menu forwards
`Menu.ItemInvoked`. Both callbacks observe the committed check or radio state.

## Code-owned glyphs

Check and radio markers resolve from the code-owned selection glyph defaults.
`UncheckedGlyph` and `CheckedGlyph` provide validated local overrides for the
item's current `Kind`. `MenuSeparator.Glyph` overrides the code-owned menu
separator glyph in the same way. `MenuItem.ResetGlyphs()` and
`MenuSeparator.ResetGlyph()` clear the corresponding overrides.

## Shortcut text

`ShortcutText` is an optional string drawn right-aligned in dim attributes after
the item's content. It only describes a command chord; it does not bind one, so
handling the shortcut remains the application's responsibility. This is
different from an ampersand [access key](../../concepts/access-keys.md#overview)
in the item's `Text` content, which SharpVision binds automatically.

```csharp
new MenuItem
{
    Text = "Save",
    ShortcutText = "Ctrl+S",
};
```

When `ShortcutText` is set, the item's desired width grows by the shortcut's
Unicode terminal-cell width plus a two-cell gutter. A vertical menu reserves one
shared width made of its widest label, that gutter, and its widest shortcut. The
shortcut text draws at the trailing edge of the item's arranged bounds using the
resolved style with `TerminalAttributes.Dim` added, and content clips before the
gutter. Every stretched sibling therefore shares one shortcut edge, and a longer
label-only row cannot collapse the shortcut column. Setting `ShortcutText` to
null removes the hint.

`Shortcut` is an optional typed `SharpVision.Input.KeyGesture` — a validated
`Code`/`Modifiers`/character combination. When `ShortcutText` is not otherwise
set, `Shortcut` supplies its conventional display text; for example,
`new KeyGesture(Code.Character, Modifiers.Control, new Rune('s'))` displays as
`"Ctrl+S"`. An explicit `ShortcutText` assignment always wins over the derived
text, following the same local-wins-over-derived precedence used throughout the
library.

## Shortcut dispatch

Unlike `ShortcutText`, `Shortcut` is bound: a matching keyboard transition
invokes the item directly, application-wide, independent of which control
currently has focus. Dispatch is a stateless tree walk over every attached
`MenuItem`, modeled on
[access-key discovery](../../concepts/access-keys.md#overview) rather than a
registration table, so there is nothing to keep in sync as items are added,
removed, disposed, reparented, or as a submenu opens and closes.

- **Reachability does not require visibility.** An item inside a currently
  closed submenu still matches its shortcut and invokes without opening the
  submenu, because a closed submenu's content stays attached — only an enabled,
  attached item is required.
- **Dispatch runs before routed key handling.** A shortcut match short-circuits
  the key entirely: it is never routed to the focused control, so a chord bound
  as a shortcut is never also typed or otherwise handled by whatever currently
  has focus.
- **Duplicate gestures cycle from the current focus**, exactly like duplicate
  access keys: repeated presses advance from whichever match currently contains
  focus to the next one in tree order, wrapping around.
- **Only `KeyAction.Press` activates a shortcut.** Held-key repeats never
  re-invoke it.
- **Modal scopes narrow the search** to items reachable from the active modal
  plane, the same as access keys.

```csharp
var save = new MenuItem
{
    Text = "Save",
    Shortcut = new KeyGesture(Code.Character, Modifiers.Control, new Rune('s')),
};
save.Invoked += (_, _) => SaveDocument();
```

## Submenus

`Submenu` gives the item one retained popup that it owns. Activating the item
toggles the submenu, and an armed owning menu may also open it while moving
selection. The popup uses a light square frame and the semantic surface
background so it reads as part of the menu system. It prefers to open below the
item in a horizontal menu and to the right of the item in a vertical menu.
Generic popup fallback, promotion, light dismissal, and ancestor-chain
preservation are unchanged. Closing the submenu restores focus to the owning
menu. Every retained popup and nested Menu participates as a descendant of the
top owner's [single menu plane](../../concepts/modality.md#menu-planes), so
opening a nested item never creates one modal scope per submenu.

## MenuSeparator

`MenuSeparator : ControlBase` is a distinct non-interactive entry role. It is
never a `PressableBase` and never a `MenuItemKind`: it cannot be focused,
hit-tested, selected, or invoked. It measures three cells by one cell, stretches
horizontally by default, and draws a clipped horizontal rule across the complete
arranged menu width.

`MenuEntryCollection` exposes typed `Add` and `Remove` overloads for `MenuItem`
and `MenuSeparator`; it has no arbitrary `Add(Control)` entry point.

## Example

![The MenuItem and MenuSeparator controls rendered in the live showcase](../../images/controls/menu-item.png)

```csharp
menu.Items.Add(new MenuItem { Text = "Open" });
menu.Items.Add(new MenuSeparator());
menu.Items.Add(new MenuItem
{
    Text = "Auto save",
    Kind = MenuItemKind.Check,
});
```

## Expected behavior

- Each item kind follows its documented activation semantics, and assigning a
  checked state that is invalid for the current kind is rejected.
- Radio observers see atomically staged group state, and an item's `Invoked`
  subscribers always complete before `Menu.ItemInvoked` is forwarded.
- The item owns its inherited caption, lays out Unicode captions correctly, and
  measures Unicode shortcut text by terminal-cell width, aligning it to the
  trailing edge.
- Keyboard and pointer activation and hover styling behave as described above,
  and shared-width layout holds, including clipping in narrow menus.
- Submenus open in their documented placement, follow the popup lifecycle,
  restore focus to the owning menu on close, and share one scope through nested
  chains.
- A `Shortcut` invokes its item application-wide, including inside a closed
  submenu, before routed key handling ever reaches the focused control, and
  duplicate gestures cycle from the current focus like access keys.
- Separators never focus, hit test, or invoke.
- Styles resolve as documented, and rendering is deterministic down to the exact
  cells.
