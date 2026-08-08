# StatusBar and StatusBarItem

## Overview

`StatusBar` is a persistent horizontal strip for useful, non-critical state
about the current application context. Applications conventionally place it at
the bottom edge of a primary screen with
`Dock.SetSide(statusBar, DockSide.Bottom)`. It is not a second menu or command
bar, and it must not be the only place an application communicates an error or
an action that requires attention.

The design follows the established desktop status-bar model: a horizontal area
split into multiple status parts, showing concise current-window and contextual
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
`StatusBarItemCollection` through `Items`. The collection accepts only detached
`StatusBarItem` instances and supports `Add`, `Insert`, `Remove`, `RemoveAt`,
`Move`, `IndexOf`, `Clear`, a settable indexer, and enumeration. Ordinary
control ownership rules reject duplicates, cycles, and cross-parent insertion.
Removing, clearing, or replacing an item detaches it without disposing it;
disposing the bar disposes any items it still owns.

`Spacing` is the number of terminal cells between adjacent visible items. It
defaults to one and must be non-negative; a negative assignment throws
`ArgumentOutOfRangeException` before any state changes.

`StatusBarItem` derives from `ContentControl`. `Content` may be any detached
control — a `Text` for ordinary status, or an explicitly interactive control
when direct access to a related command makes sense. The item itself adds no
activation, focus, or keyboard behavior.

`StatusBarItem.Alignment` accepts `StatusBarItemAlignment.Left` or
`StatusBarItemAlignment.Right` and defaults to `Left`. An undefined value throws
`ArgumentOutOfRangeException` before any state changes.

Presence and appearance of each separator are separate, independently settable
facts:

| Member                                         | Type    | Default | Purpose                                                                                                                                                                                                      |
| ---------------------------------------------- | ------- | ------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `ShowLeftSeparator` / `ShowRightSeparator`     | `bool`  | `false` | Whether a separator is drawn on that side. Reserves exactly one cell in measure and arrange when `true`; reserves none when `false`.                                                                         |
| `LeftSeparator` / `RightSeparator`             | `Rune?` | `null`  | An optional per-item glyph override for that side. Ignored unless the matching `Show*Separator` is `true`.                                                                                                   |
| `ActualLeftSeparator` / `ActualRightSeparator` | `Rune`  | —       | The glyph actually drawn: the override above when one is assigned, otherwise the themed `StatusBarItemStyle.LeftSeparatorGlyph`/`RightSeparatorGlyph` (`│`, via `StatusBarSeparatorGlyphs.Bar`, by default). |

A non-null `LeftSeparator` or `RightSeparator` value must be printable and
exactly one cell wide under the default Unicode width policy; a control or wide
glyph throws `ArgumentException` before any state changes, whether or not the
matching `Show*Separator` is set. Under a negotiated wide ambiguous-width
policy, fixed separator chrome that no longer occupies one cell renders with the
portable `|` fallback rather than overwriting an adjacent cell.

`StatusBarSeparatorGlyphs` supplies these predefined values:

| Member       | Glyph | Intended use                         |
| ------------ | ----- | ------------------------------------ |
| `Whitespace` | ` `   | Quiet separation without visible ink |
| `Bar`        | `│`   | Strong boundary between status parts |
| `Bullet`     | `•`   | Compact contextual grouping          |
| `Chevron`    | `›`   | Directional or hierarchical context  |
| `Diamond`    | `◆`   | Distinct mode or environment marker  |

An application whose visual language needs a different one-cell separator can
assign any other `Rune` that passes the same validation.

## Defaults and appearance

`StatusBar` defaults to a one-cell fixed height, stretched horizontal alignment,
the active theme's `StatusBar.normal` background, and one cell of inter-item
spacing. It stays hit-testable so ordinary routed pointer events can be
observed, but it is not focusable and is not a Tab stop. `StatusBarItem` is also
non-focusable by default. Interactive retained content keeps its own normal
input and focus behavior.

The inherited `Face` remains theme-owned, so the shared normal appearance
supplies foreground and background and retained text follows ambient
inheritance. Assign a complete local `Face` only for a deliberate
product-specific treatment.

Applications may change the inherited control appearance, height, padding,
border, and shadow properties. Multi-line or taller retained content is clipped
by the one-cell bar unless the application explicitly requests a taller
`Height`. Built-in interactive content owns its theme-safe state presentation,
so a retained `CheckBox` or other standard control needs no child appearance
configuration. Custom controls remain responsible for the shared
[visual-state contrast contract](../../concepts/styling.md#visual-states).

## Layout and clipping

Visible left-aligned items keep their collection order from the leading edge,
and visible right-aligned items keep their collection order as a group at the
trailing edge. Alignment does not partition the public collection, so
application code can update or enumerate status items in one semantic order.

The trailing group reserves its natural width before the leading group is
arranged. When width is constrained, the rightmost trailing items keep their
space first; earlier trailing items receive partial or zero-width slots, and
leading items get only whatever cells remain. All child bounds stay contained by
the bar. This keeps compact mode and position indicators stable while a longer
descriptive message yields first. Collapsed items consume neither width nor
spacing; hidden items retain their layout space under the shared visibility
contract.

Within one item, the left separator receives the first available cell, the right
separator receives the last distinct available cell, and retained content uses
the cells between them. At a one-cell width with both `Show*Separator` flags
set, only the left separator renders; content and the right separator receive
zero width.

Measure returns the sum of visible item outer widths plus spacing, with the
maximum visible outer height; the arithmetic saturates at `int.MaxValue`.
Arrangement responds to item content, margin, visibility, alignment, spacing,
and viewport changes through the normal measure/arrange invalidation path.

## Example

![The StatusBar control rendered in the live showcase](../../images/controls/status-bar.png)

```csharp
var status = new StatusBar();
status.Items.Add(new StatusBarItem
{
    Content = new Text("Ready")
});
status.Items.Add(new StatusBarItem
{
    Alignment = StatusBarItemAlignment.Right,
    ShowLeftSeparator = true,
    Content = new Text("UTF-8")
});
status.Items.Add(new StatusBarItem
{
    Alignment = StatusBarItemAlignment.Right,
    ShowLeftSeparator = true,
    LeftSeparator = StatusBarSeparatorGlyphs.Bullet,
    Content = new Text("Ln 12, Col 8")
});
status.Items.Add(new StatusBarItem
{
    Alignment = StatusBarItemAlignment.Right,
    Content = new CheckBox { Text = "Autosave" }
});

Dock.SetSide(status, DockSide.Bottom);
```

## Expected behavior

Tests demonstrate the defaults, validation before mutation, typed ownership and
detached reuse, mixed collection order, left and right edge anchoring, spacing,
alignment mutation, zero and tiny widths, trailing-item priority, exact mounted
cells, the separator presets and their validation, separator measurement and
clipping, pointer observation without implicit activation, exclusion from focus
and Tab order, and interactive retained content. The showcase includes ordinary
document status, right-aligned mode and position parts, a playing activity
spinner, a live pointer-coordinate item, and a retained CheckBox inside a full
editor workspace. Its surface tests show that a CheckBox needs no local
appearance configuration, that the theme preserves contrast through the normal,
hover, focus, checked, and disabled states, and that transient activity copy
preserves the adjacent branch geometry.
