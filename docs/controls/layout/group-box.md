# GroupBox

## GroupBox contract

`GroupBox` frames one owned content control with a titled border, providing a
visual grouping mechanism. It extends
[`ContentControl`](../content-control.md#contentcontrol-contract) and renders a
border frame with an optional header label in the top edge, like a
[`Window`](../windows/window.md) without window-specific drag behavior or a
default shadow.

## API

| Member                               | Default                | Purpose                                                |
| ------------------------------------ | ---------------------- | ------------------------------------------------------ |
| `Header`                             | Empty                  | Writes validated single-line text into the top border. |
| `Content`                            | `null`                 | Owns one child inside the framed content box.          |
| Inherited `Face`, `Border`, `Shadow` | `Container` theme role | Supply the complete body, titled frame, and depth.     |

## Behavior

- `Header` is a non-null single-line string containing no terminal controls.
  Default is empty. A non-empty header renders as one leading and trailing space
  inside the preserved top corners and clips only within that interior.
- The global `Container` profile supplies the terminal-safe frame family and
  appearance. Assign a complete local composite when this group needs a
  different body, border, or shadow; local values remain authoritative across
  theme replacement.
- `Content` is the single owned child, arranged inside the one-cell border
  inset. Use a [`Stack`](stack.md), [`Grid`](grid.md), or other layout container
  as content for multiple children.

The frame uses the intrinsic `Control` border properties rather than a wrapper
control. Header cell width participates in measurement, including combining and
wide graphemes. It overlays retained content, so child shadows cannot replace
final frame cells. Inherited `Shadow` remains available when the group itself
needs visual depth. Descendants receive normal ambient face inheritance, while
an explicit child `Face` remains authoritative.

## Example

```csharp
var group = new GroupBox
{
    Header = "Settings",
    Content = new Stack
    {
        Children =
        {
            new CheckBox { Content = new Text("Auto save") },
            new CheckBox { Content = new Text("Line numbers") },
        },
    },
};
```

An ampersand in `Header` declares an
[access key](../../concepts/access-keys.md#focus-and-semantic-actions). The
marker occupies no cells, its grapheme is underlined, and Alt plus the key
focuses the first eligible descendant in hierarchical tab order.

## Expected behavior

Cover header rendering, empty header, content arrangement inside border, measure
width expansion for wide headers, zero bounds, glyph families, style states,
child-shadow frame preservation, and final cells.

Mounted cross-layer coverage in
[`GroupBoxSurfaceTests`](../../../tests/SharpVision.Tests/Controls/Layout/GroupBoxSurfaceTests.cs)
proves continuous and interrupted frames, wide-header continuation ownership,
tiny clipping, resize reveal, once-inset content, and scoped style inheritance.
The [`GroupBoxPane`](../../../examples/Showcase/Panes/GroupBoxPane.cs)
demonstrates empty, titled, Unicode, styled, ASCII, nested-content, and tiny
specimens in the gallery.
