# Box model

## Overview

Every `Control` uses the same physical box model, measured in terminal-cell
units:

```text
margin -> border box -> border -> padding -> content
```

`Width`, `Height`, the minimums and maximums, alignment, `Bounds`, and
`DesiredSize` all describe the border box. `Margin` sits outside the border box
and is never painted by the control itself. The enabled one-cell edges of
[`Border`](intrinsic-chrome.md#border-api) occupy the border box, and `Padding`
separates that border from `ContentBounds`.

Margin, border, and padding never collapse into each other. When opposing
extents are combined, the arithmetic is checked or saturating at its documented
boundary, and deflating a rectangle never produces a negative width or height.

## API

| Member                  | Type                  | Default              | Description                                                                              |
| ----------------------- | --------------------- | -------------------- | ---------------------------------------------------------------------------------------- |
| `Width`, `Height`       | `Length`              | `Length.Auto`        | Requested border-box dimensions.                                                         |
| `MinWidth`, `MinHeight` | `int`                 | `0`                  | Non-negative border-box floors; each must not exceed its maximum.                        |
| `MaxWidth`, `MaxHeight` | `int`                 | `int.MaxValue`       | Non-negative border-box ceilings; each must not be below its minimum.                    |
| `HorizontalAlignment`   | `HorizontalAlignment` | `Left`               | Placement in the parent's final horizontal slot.                                         |
| `VerticalAlignment`     | `VerticalAlignment`   | `Stretch`            | Placement in the parent's final vertical slot.                                           |
| `Margin`                | `Thickness`           | all edges `0`        | External, non-collapsing cells reserved by the parent and never painted by this control. |
| `Padding`               | `Thickness`           | all edges `0`        | Internal cells between the enabled border and content.                                   |
| `DesiredSize`           | `Size`                | empty before measure | Committed desired border-box size, excluding margin.                                     |
| `Bounds`                | `Rect`                | empty before arrange | Committed border-box rectangle.                                                          |
| `ContentBounds`         | `Rect`                | derived              | `Bounds` deflated first by enabled border edges and then by `Padding`.                   |
| `LocalBounds`           | `Rect`                | derived              | Zero-origin rectangle with the committed `Bounds` extent.                                |

`Thickness` is an immutable value that describes the four physical edges. Its
constructors accept one uniform extent, separate horizontal and vertical
extents, or individual left/top/right/bottom extents. Every edge must be
non-negative, and if the sum of two opposing edges would exceed `int.MaxValue`,
the constructor throws `OverflowException` before the value is created.

`Length` and the complete measure/arrange algorithm are specified by
[Layout](layout.md#overview). Border appearance and shadow overflow are
specified by [Intrinsic chrome](intrinsic-chrome.md#overview).

## Measure and arrange

The common control pipeline applies the box model in this order:

1. Deflate the available slot by `Margin`.
2. Resolve requested `Width` and `Height`, then apply minimums and maximums.
3. Reserve enabled border edges.
4. Deflate the remaining content constraint by `Padding`.
5. Measure intrinsic content.
6. Add padding and border extents back to the desired border box.
7. Align the final border box inside the margin-deflated arrange slot.
8. Commit `Bounds`, then derive `ContentBounds` by border and padding deflation.

Percentage and automatic dimensions measured under an unbounded constraint
follow the [layout length rules](layout.md#lengths). A shadow only expands the
control's visual bounds; it never changes the desired size, the content bounds,
or the layout slot the parent gives the control.

## Painting and hit testing

- The control body may paint anywhere in the border box, including the padding
  cells and the backdrop beneath border glyphs.
- Margin cells keep whatever the parent already painted there.
- Border glyphs are drawn over the body on each enabled edge.
- Content and descendants receive the rectangle left after border and padding
  deflation.
- Hit testing follows arranged ownership and clipping rules, so shadow overflow
  does not enlarge the authored hit target.
- A control with `Visibility.Collapsed` contributes no desired size and skips
  content layout; one with `Visibility.Hidden` participates in layout but is
  neither painted nor hit-testable.

## Example

```csharp
var panel = new Stack
{
    Width = Length.Percent(100),
    Margin = new Thickness(horizontal: 2, vertical: 1),
    Padding = new Thickness(left: 2, top: 1, right: 2, bottom: 1),
    Border = new Border(
        BorderSide.All,
        BorderGlyphStyle.Rounded,
        ThemeColor.ControlBorder,
        Color.Transparent,
        ThemeDecoration.Border)
};
```

The parent reserves the margin. `panel.Bounds` starts inside that reservation,
the border takes one cell on each enabled edge, and the children are arranged
inside the remaining padded `ContentBounds`.

## Expected behavior

| Layer      | Observable evidence                                                                                         |
| ---------- | ----------------------------------------------------------------------------------------------------------- |
| Unit       | Constructor and setter validation, exact desired size, bounds, content bounds, alignment, and invalidation. |
| Surface    | Distinct parent/body/border/content styles proving which cells each box layer owns.                         |
| Randomized | Fixed-seed containment, non-negative extents, deterministic repetition, and saturated tiny-view behavior.   |

- The box model behaves the same for each edge independently and for all four
  edges together.
- Zero-size slots and slots smaller than the combined insets still produce
  contained, non-negative geometry.
- Automatic, fixed, percentage, and proportional dimensions all resolve through
  the same pipeline.
- Hidden controls keep their layout participation and collapsed controls
  contribute nothing, as described above.
- Border changes take effect whether they come from local values, visual states,
  or theme publication.
