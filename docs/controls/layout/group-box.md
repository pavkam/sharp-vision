# GroupBox

## GroupBox contract

`GroupBox` frames one owned content control with a titled border, providing a
visual grouping mechanism. It extends
[`ContentControl`](../content-control.md#contentcontrol-contract) and renders a
border frame with an optional header label in the top edge, like a
[`Window`](../windows/window.md) without drag or shadow support.

## API

- `Header` is a non-null string rendered in the top border edge. Default is
  empty.
- `Glyphs` controls the terminal-safe glyph family for the border frame. Default
  is `Glyphs.Rounded`.
- `Content` is the single owned child, arranged inside the one-cell border
  inset. Use a [`Stack`](stack.md), [`Grid`](grid.md), or other layout container
  as content for multiple children.

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
