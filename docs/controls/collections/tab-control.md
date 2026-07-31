# TabControl

## TabControl contract

`TabControl` arranges typed [`TabItem`](#tabitem) pages and coordinates a header
strip, keyboard navigation, and content participation. It extends
[`ItemsControl`](../items-control.md) with one private retained page host and a
private retained header strip. `TabControl` owns keyboard focus. Each generated
header is a pressable framework part that owns its pointer hit target, hover,
capture, pressed state, and selected appearance. Public `TabItem` objects own
semantic page content without exposing those presentation controls.

Unselected headers preserve the containing strip background. Hover changes only
their foreground; the committed selected header may use the selection
background.

The header strip occupies the first row. Each label has one cell of horizontal
padding, adjacent labels use the code-owned tab-divider glyph, and the
code-owned tab-underline glyph occupies the second row when height permits. Only
selected content participates in measure, arrangement, rendering, hit testing,
and navigation below the rule. The selected visual state belongs to its header;
page content retains its normal appearance. Unselected content remains owned and
attached with empty bounds.

## API

| Member                                    | Default                           | Purpose                                                                               |
| ----------------------------------------- | --------------------------------- | ------------------------------------------------------------------------------------- |
| `Items`                                   | Empty typed collection            | Owns `TabItem` values and their retained content pages.                               |
| `SelectedIndex`                           | `-1` until an eligible tab exists | Selects one visible and enabled page or explicitly clears selection.                  |
| `HeaderWidth`                             | `Length.Auto`                     | Applies an automatic, fixed-cell, percentage, or proportional width to every header.  |
| `HeaderOverflowPolicy`                    | `Clip`                            | Clips overflowing headers or scrolls the strip to keep the selected header reachable. |
| `DividerGlyph`, `UnderlineGlyph`          | Code-owned                        | Override one-cell strip and selected-tab markers; `ResetGlyphs()` restores defaults.  |
| `DividerColor`, `SelectionIndicatorColor` | `null`, `null`                    | Override the theme colors for the two strip parts.                                    |
| `SelectionChanged`                        | No subscribers                    | Reports selection after page participation commits.                                   |
| `CloseRequested`                          | No subscribers                    | Requests closure of a closeable page; handlers may cancel before removal.             |

## Behavior

- `Items : TabItemCollection` exposes typed `Add`/`Remove`/`Clear` operations
  for `TabItem`, plus read-only typed indexing and enumeration. Null, duplicate,
  attached, disposed, and cyclic candidates are rejected before ownership
  changes. Removal detaches without disposing.
- `SelectedIndex` tracks the selected eligible page; `-1` explicitly clears
  selection. The first effectively visible and enabled tab auto-selects. Invalid
  indexes and unavailable targets are rejected before mutation.
- `SelectionChanged` fires once after the selected page identity and retained
  content participation commit. Identical assignment is not a selection change.
- `TabItem.IsClosable` opts a page into `RequestClose` and Delete-key closure.
  `CloseRequested` is raised before removal with a cancellable
  [`TabCloseRequestedEventArgs`](#tabcloserequestedeventargs) payload. No close
  glyph or mouse-only close affordance is part of this basic contract.

`HeaderWidth` uses the shared `Length` model. `Length.Auto` preserves each
header's intrinsic width; fixed, percentage, and proportional values are
resolved against the available header-strip width. `HeaderOverflowPolicy.Scroll`
uses the existing horizontal container offset and reveals the selected header
after keyboard or programmatic selection. It does not add a second scrollbar row
to the tab strip.

Removing, disabling, or collapsing the selected page chooses the nearest
eligible successor, then predecessor, or clears selection. Clearing the
collection clears selection. Re-enabling an unselected page does not steal
selection.

Primary pointer release on a header selects that page. Left/Right move and
select with wrapping; Home/End choose the first/last eligible page. Navigation
skips effectively hidden or disabled pages. Pointer focus resolves to the
`TabControl`, while hover and pressed state remain local to the hit header. A
selected header combines `VisualState.Selected` with its own hover, pressed, or
disabled state; hovering the page or the owner does not recolor the strip.

## TabItem

`TabItem` extends
[`ContentControl`](../content-control.md#contentcontrol-contract). `Header` is a
non-null string without terminal controls, rendered in the owning tab strip.
`Content` is the single caller-replaceable owned child arranged below the rule
only while selected.

`IsClosable` defaults to `false`. It only enables the semantic close request;
dirty-state tracking and header adornments remain application concerns.

## TabCloseRequestedEventArgs

The event payload exposes the requested `Item` and a mutable `Cancel` flag. When
no handler cancels, `RequestClose` removes the item through the ordinary typed
collection path, applies nearest eligible selection repair, and raises
`SelectionChanged` if the selected index changes.

## Code-owned glyphs

`DividerGlyph` and `UnderlineGlyph` are validated one-cell local overrides for
the header row. Their defaults resolve from the code-owned separator glyph
catalog on every render. `ResetGlyphs()` clears both overrides.

Null `DividerColor` uses the normal theme border; null `SelectionIndicatorColor`
uses the focused theme foreground. Callers override only the part whose meaning
they need to change.

## Example

![The TabControl control rendered in the live showcase](../../images/controls/tab-control.png)

```csharp
var tabs = new TabControl();
tabs.Items.Add(new TabItem
{
    Header = "General",
    Content = new Stack
    {
        Children = { new Text("General settings") },
    },
});
tabs.Items.Add(new TabItem
{
    Header = "Advanced",
    Content = new Stack
    {
        Children = { new CheckBox { Content = new Text("Debug mode") } },
    },
});
```

An ampersand in `TabItem.Header` declares an
[access key](../../concepts/access-keys.md#focus-and-semantic-actions). The
private retained header renders the marker-free underlined caption; Alt plus the
key focuses the `TabControl` and selects that page. Page body text is not the
tab caption.

## Expected behavior

Cover typed ownership and validation, default/cleared selection, event order,
deterministic removal and availability repair, exact header/divider/rule
rendering, header-only selected and hover appearance, selected-content exclusion
and replacement, nested pointer activation, pointer capture cleanup, keyboard
wrapping and disabled skipping, owner focus, Unicode cells, clipping, zero/tiny
bounds, resize, stale-cell clearing, disposal, and final semantic cells.
