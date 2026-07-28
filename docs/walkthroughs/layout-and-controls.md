# Compose layout and controls

## Compose layout and controls

SharpVision layout has two passes. Measure computes each control's desired
terminal-cell size; arrange commits its final rectangle. `Length.Auto` follows
content, `Length.Cells(n)` requests a fixed count, percentages use available
space, and stars divide the remainder. The
[layout contract](../concepts/layout.md#layout-contract) owns the exact
algorithms.

## Build a responsive shell

```csharp
var navigation = new ListView
{
    Width = Length.Cells(22),
    Items = new object?[] { "Dashboard", "Jobs", "Settings" },
    SelectionMode = ListSelectionMode.Single,
};

var details = new Stack
{
    HorizontalAlignment = HorizontalAlignment.Stretch,
    Spacing = 1,
    Padding = new Thickness(1),
    Children =
    {
        new Text("<accent><b>Dashboard</b></accent>"),
        new ProgressBar { Minimum = 0, Maximum = 100, Value = 68 },
        new CheckBox { Content = new Text("Run automatically") },
    },
};

var status = new Text("<d>F1 Help · Ctrl+Q Quit</d>")
{
    Height = Length.Cells(1),
    HorizontalAlignment = HorizontalAlignment.Stretch,
};

var shell = new Dock
{
    LastChildFills = true,
    Spacing = 1,
};

Dock.SetSide(status, Side.Bottom);
Dock.SetSide(navigation, Side.Left);
shell.Children.Add(status);
shell.Children.Add(navigation);
shell.Children.Add(details);
```

`Dock` consumes edges in child order. `status` reserves the bottom row,
`navigation` reserves 22 cells on the left, and the final `details` child fills
the remainder because `LastChildFills` is `true`. `Padding` is inside a
control's border box; `Margin` would reserve space outside it. The
[`Control` property guide](../controls/control.md#api) explains the shared
sizing and alignment properties.

## Choose a layout control

| Need                            | Control                                                         | Important properties                                 |
| ------------------------------- | --------------------------------------------------------------- | ---------------------------------------------------- |
| One-dimensional rows or columns | [`Stack`](../controls/layout/stack.md#stack-contract)           | `Orientation`, `Spacing`, `Reverse`                  |
| Consume outer edges, then fill  | [`Dock`](../controls/layout/dock.md#dock-contract)              | attached `Side`, `Spacing`, `LastChildFills`         |
| Rows, columns, and spans        | [`Grid`](../controls/layout/grid.md#grid-contract)              | `Rows`, `Columns`, spacing, attached row/column/span |
| Absolute or anchored placement  | [`Overlay`](../controls/layout/overlay.md#overlay-contract)     | attached `Left`, `Top`, `Right`, `Bottom`            |
| Overlapping layers              | [`Overlay`](../controls/layout/overlay.md#overlay-contract)     | attached `ZIndex`, `ClipToBounds`                    |
| Header plus one child           | [`GroupBox`](../controls/layout/group-box.md#groupbox-contract) | `Header`, `Content`, `BorderGlyphStyle`              |

## Make overflowing content scroll

Scrolling belongs to every `Container`; there is no wrapper `ScrollView`:

```csharp
details.AutoScroll = true;
details.ScrollBars = ScrollBars.Vertical;
details.ShowScrollBars = ShowScrollBars.WhenNeeded;
```

The container measures content, computes the viewport, resolves scrollbar
feedback, and routes wheel and keyboard input through the common
[scrolling algorithm](../concepts/scrolling.md#automatic-scrollbar-algorithm).
Use [`ScrollBar`](../controls/layout/scroll-bar.md#scrollbar-contract) directly
only when the range itself is part of the application's UI.

Next, [connect state and events](state-and-events.md#state-input-and-events).
