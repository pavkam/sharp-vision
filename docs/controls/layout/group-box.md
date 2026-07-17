# GroupBox

## GroupBox contract

`GroupBox` frames one owned content control with a titled border, providing a
visual grouping mechanism. It extends
[`ContentControl`](../content-control.md#contentcontrol-contract) and renders a
border frame with an optional header label in the top edge, like a
[`Window`](../windows/window.md) without drag or shadow support.

## API

- `Header` is a non-null single-line string containing no terminal controls.
  Default is empty. A non-empty header renders as one leading and trailing space
  inside the preserved top corners and clips only within that interior.
- `Glyphs` controls the terminal-safe glyph family for the border frame. Default
  is `Glyphs.Rounded`.
- `Background` defaults to the semantic `Surface` role and `BorderColor`
  defaults to the semantic `Border` role. Built-in themes keep those roles
  visually distinct so the frame remains visible around the opaque interior.
- `Content` is the single owned child, arranged inside the one-cell border
  inset. Use a [`Stack`](stack.md), [`Grid`](grid.md), or other layout container
  as content for multiple children.

The frame uses the intrinsic `Control` border properties rather than a wrapper
control. Header cell width participates in measurement, including combining and
wide graphemes. `GroupBox` is an `IStyleScope`, so its themed and per-instance
style resources contribute to descendant resolution while an explicit child
style remains authoritative.

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

## Test obligations

Cover header rendering, empty header, content arrangement inside border, measure
width expansion for wide headers, zero bounds, glyph families, style states, and
final cells.

Mounted cross-layer coverage in
[`GroupBoxSurfaceTests`](../../../tests/SharpVision.Tests/Controls/GroupBoxSurfaceTests.cs)
proves continuous and interrupted frames, wide-header continuation ownership,
tiny clipping, resize reveal, once-inset content, and scoped style inheritance.
The [`GroupBoxPane`](../../../src/SharpVision.Showcase/Panes/GroupBoxPane.cs)
demonstrates empty, titled, Unicode, styled, ASCII, nested-content, and tiny
specimens in the gallery.
