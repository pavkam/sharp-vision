# MenuItem and MenuSeparator

## Overview

`MenuItem` is a sealed [`Pressable`](../pressable.md#overview) that represents
a command, check, or radio entry inside a [Menu](menu.md#overview). Its label
comes from the inherited `Content` property, which is the item's only visible
face; there is no competing text-only `Header` property.

## API

| Member                           | Default         | Purpose                                                             |
| -------------------------------- | --------------- | ------------------------------------------------------------------- |
| `Content`                        | `null`          | The item's label or richer face; the item owns it.                  |
| `Kind`                           | `Command`       | Chooses command, check, or radio activation semantics.              |
| `IsChecked`, `GroupName`         | `false`, `null` | Hold the check state and scope radio exclusivity.                   |
| `ShortcutText`                   | `null`          | Shows a dim, right-aligned hint; registers no key binding.          |
| `Shortcut`                       | `null`          | A typed `KeyGesture` that derives `ShortcutText`; also unbound.     |
| `Submenu`                        | `null`          | An optional popup-layer child menu the item owns.                   |
| `UncheckedGlyph`, `CheckedGlyph` | Code-owned      | Override state marks; `ResetGlyphs()` restores code-owned defaults. |
| `Invoked`                        | No subscribers  | Raised after activation commits, once any check state has updated.  |
| `PerformInvoke()`                | —               | Invokes the item programmatically.                                  |

## Behavior

- `Content` accepts zero or one control of any type, and the item owns it.
- `Kind` is one of command, check, or radio.
- `IsChecked` applies only to the check and radio kinds. `GroupName` scopes
  radio selection within the containing menu.
- `Invoked` reports the committed activation after any check or radio state
  update.

A check or radio entry reserves one cell for its code-owned selection glyph
plus one separator cell in front of the content. Content is measured against
the remaining constraint and arranged through the common inherited content
slot, so state changes do not move it, and collapsed content contributes no
margin. When a radio selection changes, every matching item's fields are
staged before the first `PropertyChanged(IsChecked)` callback runs, and a
reentrant selection suppresses the stale outer notifications.

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

`ShortcutText` is an optional string drawn right-aligned in dim attributes
after the item's content. It only describes a command chord; it does not bind
one, so handling the shortcut remains the application's responsibility. This
is different from an ampersand
[access key](../../concepts/access-keys.md#overview) in the item's `Text`
content, which SharpVision binds automatically.

```csharp
new MenuItem
{
    Content = new Text("Save"),
    ShortcutText = "Ctrl+S",
};
```

When `ShortcutText` is set, the item's desired width grows by the shortcut's
Unicode terminal-cell width plus a two-cell gutter. A vertical menu reserves
one shared width made of its widest label, that gutter, and its widest
shortcut. The shortcut text draws at the trailing edge of the item's arranged
bounds using the resolved style with `Attributes.Dim` added, and content clips
before the gutter. Every stretched sibling therefore shares one shortcut edge,
and a longer label-only row cannot collapse the shortcut column. Setting
`ShortcutText` to null removes the hint.

`Shortcut` is an optional typed `SharpVision.Input.KeyGesture` — a validated
`Code`/`Modifiers`/character combination. When `ShortcutText` is not otherwise
set, `Shortcut` supplies its conventional display text; for example,
`new KeyGesture(Code.Character, Modifiers.Control, new Rune('s'))` displays as
`"Ctrl+S"`. An explicit `ShortcutText` assignment always wins over the derived
text, following the same local-wins-over-derived precedence used throughout
the library. Like `ShortcutText`, `Shortcut` is purely declarative: it routes
no input by itself, so the application is still responsible for actually
invoking the item when the chord arrives.

## Submenus

`Submenu` gives the item one retained popup that it owns. Activating the item
toggles the submenu, and an armed owning menu may also open it while moving
selection. The popup uses a light square frame and the semantic surface
background so it reads as part of the menu system. It prefers to open below
the item in a horizontal menu and to the right of the item in a vertical menu.
Generic popup fallback, promotion, light dismissal, and ancestor-chain
preservation are unchanged. Closing the submenu restores focus to the owning
menu. Every retained popup and nested Menu participates as a descendant of the
top owner's [single menu plane](../../concepts/modality.md#menu-planes), so
opening a nested item never creates one modal scope per submenu.

## MenuSeparator

`MenuSeparator : Control` is a distinct non-interactive entry role. It is
never a `Pressable` and never a `MenuItemKind`: it cannot be focused,
hit-tested, selected, or invoked. It measures three cells by one cell,
stretches horizontally by default, and draws a clipped horizontal rule across
the complete arranged menu width.

`MenuEntryCollection` exposes typed `Add` and `Remove` overloads for
`MenuItem` and `MenuSeparator`; it has no arbitrary `Add(Control)` entry
point.

## Example

```csharp
menu.Items.Add(new MenuItem { Content = new Text("Open") });
menu.Items.Add(new MenuSeparator());
menu.Items.Add(new MenuItem
{
    Content = new Text("Auto save"),
    Kind = MenuItemKind.Check,
});
```

## Expected behavior

- Each item kind follows its documented activation semantics, and assigning a
  checked state that is invalid for the current kind is rejected.
- Radio observers see atomically staged group state, and an item's `Invoked`
  subscribers always complete before `Menu.ItemInvoked` is forwarded.
- The item owns its inherited content, lays out Unicode content correctly,
  and measures Unicode shortcut text by terminal-cell width, aligning it to
  the trailing edge.
- Keyboard and pointer activation and hover styling behave as described
  above, and shared-width layout holds, including clipping in narrow menus.
- Submenus open in their documented placement, follow the popup lifecycle,
  restore focus to the owning menu on close, and share one scope through
  nested chains.
- Separators never focus, hit test, or invoke.
- Styles resolve as documented, and rendering is deterministic down to the
  exact cells.
