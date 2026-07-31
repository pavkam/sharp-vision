# MenuItem and MenuSeparator

## MenuItem contract

`MenuItem` is a sealed [`Pressable`](../pressable.md#pressable-contract)
command, check, or radio entry inside a [Menu](menu.md#menu-contract). It uses
inherited `Content` as its sole visible face; there is no competing text-only
`Header` property.

## API

| Member                           | Default         | Purpose                                                             |
| -------------------------------- | --------------- | ------------------------------------------------------------------- |
| `Content`                        | `null`          | Owns the item label or richer face.                                 |
| `Kind`                           | `Command`       | Selects command, check, or radio activation semantics.              |
| `IsChecked`, `GroupName`         | `false`, `null` | Store check state and scope radio exclusivity.                      |
| `ShortcutText`                   | `null`          | Adds a dim, right-aligned hint without registering a key binding.   |
| `Shortcut`                       | `null`          | A typed `KeyGesture` that derives `ShortcutText`; also unbound.      |
| `Submenu`                        | `null`          | Owns an optional popup-layer child menu.                            |
| `UncheckedGlyph`, `CheckedGlyph` | Code-owned      | Override state marks; `ResetGlyphs()` restores code-owned defaults. |
| `Invoked`                        | No subscribers  | Reports committed activation after optional check-state updates.    |
| `PerformInvoke()`                | —               | Runs the programmatic activation path.                              |

## Behavior

- `Content` is the atomic zero-or-one owned face and may contain any Control.
- `Kind` is command, check, or radio.
- `IsChecked` is valid only for check/radio kinds; `GroupName` scopes radio
  selection within its containing menu.
- `Invoked` reports the committed activation after check/radio state updates.

Check and radio entries reserve the corresponding code-owned selection glyph
plus one separator cell before content. Content is measured through the
remaining constraint and arranged through the common inherited content slot, so
state changes do not move it and collapsed content contributes no margin.
Matching radio fields are all staged before any `PropertyChanged(IsChecked)`
callback; reentrant selection suppresses stale outer notifications.

Menu items default to horizontal stretch. In a vertical menu every item
therefore consumes the shared menu width, allowing content, shortcut hints, and
separators to form one aligned surface. An explicit caller alignment remains
authoritative.

The item's own `Invoked` subscribers complete before `Menu.ItemInvoked` is
forwarded. Both callbacks observe committed check/radio state.

## Code-owned glyphs

Check and radio item markers resolve from
`the code-owned selection glyph defaults`. `UncheckedGlyph` and `CheckedGlyph`
provide validated local overrides for the item's current `Kind`.
`MenuSeparator.Glyph` similarly overrides
`the code-owned separator glyph defaults.Menu`. `MenuItem.ResetGlyphs()` and
`MenuSeparator.ResetGlyph()` clear the corresponding overrides.

## Shortcut text

`ShortcutText` is an optional string rendered right-aligned with dim attributes
after the item's content. It describes a command chord without binding it;
shortcut handling is the application's responsibility. This is distinct from an
ampersand [access key](../../concepts/access-keys.md#access-key-contract) in the
item's `Text` content, which SharpVision binds automatically.

```csharp
new MenuItem
{
    Content = new Text("Save"),
    ShortcutText = "Ctrl+S",
};
```

When set, the item's desired width includes the shortcut's Unicode terminal-cell
width plus a two-cell gutter. A vertical menu reserves one shared width equal to
its widest label, that gutter, and its widest shortcut. The shortcut text is
drawn at the trailing edge of the item's arranged bounds using the resolved
style with `Attributes.Dim` added, while content is clipped before the gutter.
Every stretched sibling therefore shares one shortcut edge without allowing a
longer label-only row to collapse the shortcut column. Setting `ShortcutText` to
null removes it.

`Shortcut` is an optional typed `SharpVision.Input.KeyGesture` (a validated
`Code`/`Modifiers`/character combination). When `ShortcutText` is otherwise
unset, `Shortcut` derives its conventional display text, for example
`new KeyGesture(Code.Character, Modifiers.Control, new Rune('s'))` displays as
`"Ctrl+S"`. An explicit `ShortcutText` assignment always wins over `Shortcut`'s
derived text, the same local-wins-over-derived precedence used throughout the
library. `Shortcut` is purely declarative like `ShortcutText` before it: it
routes no input by itself, so it does not replace the application's own
responsibility for actually invoking the item on that chord.

## Submenus

`Submenu` creates one retained popup owned by the item. Menu-item activation
toggles it; an armed owning menu may also open it while moving selection. The
popup uses a light square frame and the semantic surface background so it reads
as part of the menu system. Its preferred placement is below an item in a
horizontal menu and right of an item in a vertical menu. Generic popup fallback,
promotion, light dismissal, and ancestor-chain preservation remain unchanged.
Closing restores focus to the owning menu. Every retained popup and nested Menu
participates as a descendant of the top owner's
[single menu plane](../../concepts/modality.md#menu-planes); opening a nested
item never creates one modal scope per submenu.

## MenuSeparator contract

`MenuSeparator : Control` is the distinct non-interactive entry role. It is
never a `Pressable` or a `MenuItemKind`: it cannot focus, hit test, select, or
invoke. It measures three cells by one cell, defaults to horizontal stretch, and
draws a clipped horizontal rule across the complete arranged menu width.

`MenuEntryCollection` exposes typed `Add` and `Remove` overloads for `MenuItem`
and `MenuSeparator`; it has no arbitrary `Add(Control)` entry point.

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

Cover each item kind, invalid checked-state assignment, atomic radio observers,
item-before-menu invocation, inherited content ownership and Unicode layout,
Unicode shortcut measurement and trailing alignment, keyboard and pointer
activation, hover styling, submenu placement/lifecycle/focus restoration,
same-scope nested chains, separator focus/hit/invoke suppression, shared-width
rules, narrow clipping, styles, and exact cells.
