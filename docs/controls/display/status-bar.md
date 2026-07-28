# StatusBar and StatusBarItem

## StatusBar contract

`StatusBar` is a persistent horizontal strip for useful, non-critical state
about the current application context. Applications conventionally place it at
the bottom edge of a primary screen with `Dock.SetSide(statusBar, Side.Bottom)`.
It is not a second menu or command bar, and it must not be the only place an
application communicates an error or action that requires attention.

The design follows the established desktop status-bar model: a horizontal area
split into multiple status parts, with concise current-window and contextual
information. The
[Windows status-bar control](https://learn.microsoft.com/en-us/windows/win32/controls/status-bars)
defines the conventional bottom-edge, multi-part surface, while the
[Windows status-bar design guidance](https://learn.microsoft.com/en-us/windows/win32/uxguide/ctrl-status-bars)
distinguishes useful background context from critical notifications and
toolbars. For a terminal-specific point of comparison,
[Terminal.Gui StatusBar](https://gui-cs.github.io/Terminal.Gui/api/Terminal.Gui.Views.StatusBar.html)
also models a context-sensitive bottom bar.

## API

`StatusBar` derives from `ItemsControl` and exposes a typed
`StatusBarItemCollection` collection through `Items`. The collection accepts
only detached `StatusBarItem` instances and supports `Add`, `Remove`, `Clear`,
indexed read-only access, and enumeration. Ordinary control ownership rules
reject duplicates, cycles, and cross-parent insertion. Removing or clearing an
item detaches it without disposing it; disposing the bar disposes items it still
owns.

`Spacing` is the non-negative number of terminal cells between adjacent visible
items. Its default is one. A negative assignment throws
`ArgumentOutOfRangeException` before state changes.

`StatusBarItem` derives from `ContentControl`. `Content` may be any detached
control, including `Text` for ordinary status or an explicitly interactive
control when direct access to a related command is appropriate. The item itself
does not add activation, focus, or keyboard behavior.

`StatusBarItem.Alignment` accepts `StatusBarItemAlignment.Left` or
`StatusBarItemAlignment.Right` and defaults to `Left`. An undefined value throws
`ArgumentOutOfRangeException` before state changes.

`LeftSeparator` and `RightSeparator` are nullable `Rune` values rendered before
and after the retained content. Null, the default, reserves no cell. A non-null
value must be printable and exactly one cell wide under the default Unicode
width policy; a control or wide glyph throws `ArgumentException` before state
changes. Each configured separator reserves exactly one cell in measure and
arrange. Under a negotiated wide ambiguous-width policy, fixed separator chrome
that no longer occupies one cell renders with the portable `|` fallback rather
than overwriting an adjacent cell.

`StatusBarSeparatorGlyphs` supplies these predefined values:

| Member       | Glyph | Intended use                         |
| ------------ | ----- | ------------------------------------ |
| `Whitespace` | ` `   | Quiet separation without visible ink |
| `Bar`        | `│`   | Strong boundary between status parts |
| `Bullet`     | `•`   | Compact contextual grouping          |
| `Chevron`    | `›`   | Directional or hierarchical context  |
| `Diamond`    | `◆`   | Distinct mode or environment marker  |

Applications may assign another validated `Rune` when its visual language needs
a different one-cell separator.

## Defaults and appearance

`StatusBar` defaults to a one-cell fixed height, stretched horizontal alignment,
the active theme's `StatusBar.normal` background, and one cell of inter-item
spacing. It remains hit-testable for ordinary routed pointer observation, but is
not focusable and is not a Tab stop. `StatusBarItem` is also non-focusable by
default. Interactive retained content keeps its own normal input and focus
semantics.

The inherited `Face` remains theme-owned so the shared normal appearance
supplies foreground/background and retained text follows ambient inheritance. An
application assigns a complete local `Face` only for a deliberate
product-specific treatment.

Applications may change inherited control appearance, height, padding, border,
and shadow properties. Multi-line or taller retained content is clipped by a
one-cell bar unless the application explicitly requests a taller `Height`.
Built-in interactive content owns theme-safe state presentation, so an
application may retain a `CheckBox` or other standard control without assigning
child appearance states. Custom controls remain responsible for the shared
[visual-state contrast contract](../../concepts/styling.md#visual-states).

## Layout and clipping

Visible left-aligned items preserve collection order from the leading edge.
Visible right-aligned items preserve collection order as a group at the trailing
edge. Alignment does not partition the public collection, so application code
may update or enumerate status items in one semantic order.

The trailing group reserves its natural width before the leading group is
arranged. At constrained widths, rightmost trailing items retain space first;
earlier trailing items receive partial or zero-width slots, and leading items
receive only the remaining cells. All child bounds remain contained by the bar.
This makes compact mode and position indicators stable while a longer
descriptive message yields first. Collapsed items consume neither width nor
spacing; hidden items retain layout space under the shared visibility contract.

Within one item, the left separator receives the first available cell, the right
separator receives the last distinct available cell, and retained content uses
the cells between them. At a one-cell width with both separators configured, the
left separator renders and content plus the right separator receive zero width.

Measure returns the sum of visible item outer widths and spacing, with the
maximum visible outer height. Arithmetic saturates at `int.MaxValue`.
Arrangement responds to item content, margin, visibility, alignment, spacing,
and viewport changes through the normal measure/arrange invalidation path.

## Example

```csharp
var status = new StatusBar();
status.Items.Add(new StatusBarItem
{
    Content = new Text("Ready")
});
status.Items.Add(new StatusBarItem
{
    Alignment = StatusBarItemAlignment.Right,
    LeftSeparator = StatusBarSeparatorGlyphs.Bar,
    Content = new Text("UTF-8")
});
status.Items.Add(new StatusBarItem
{
    Alignment = StatusBarItemAlignment.Right,
    LeftSeparator = StatusBarSeparatorGlyphs.Bullet,
    Content = new Text("Ln 12, Col 8")
});
status.Items.Add(new StatusBarItem
{
    Alignment = StatusBarItemAlignment.Right,
    Content = new CheckBox { Content = new Text("Autosave") }
});

Dock.SetSide(status, Side.Bottom);
```

## Test obligations

Tests cover defaults, validation-before-mutation, typed ownership and detached
reuse, mixed collection order, left/right edge anchoring, spacing, alignment
mutation, zero and tiny widths, trailing-item priority, exact mounted cells,
separator presets and validation, separator measurement and clipping, pointer
observation without implicit activation, focus and Tab exclusion, and
interactive retained content. The showcase includes ordinary document status,
right-aligned mode/position parts, a playing activity spinner, a live
pointer-coordinate item, and a retained CheckBox inside a full editor workspace.
Its surface tests verify that a CheckBox needs no local appearance
configuration, the theme preserves contrast through normal, hover, focus,
checked, and disabled states, and transient activity copy preserves the adjacent
branch geometry.
