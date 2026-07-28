# Box model

## Box-model contract

Every `Control` uses the same physical box model in terminal-cell units:

```text
margin -> border box -> border -> padding -> content
```

`Width`, `Height`, minimums, maximums, alignment, `Bounds`, and `DesiredSize`
describe the border box. `Margin` is external and never painted by the control.
The enabled one-cell edges of [`Border`](intrinsic-chrome.md#border-api) occupy
the border box. `Padding` separates that border from `ContentBounds`.

Margin, border, and padding never collapse. Opposing extents use checked or
saturating arithmetic at their documented boundary, and deflation never creates
a negative width or height.

## API

| Member                  | Type                  | Default              | Contract                                                                                 |
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

`Thickness` is an immutable physical-edge value. Its constructors accept one
uniform extent, horizontal/vertical extents, or left/top/right/bottom extents.
Every edge is non-negative; an opposing-edge sum that exceeds `int.MaxValue`
throws `OverflowException` before construction completes.

`Length` and the complete measure/arrange algorithm are specified by
[Layout](layout.md#layout-contract). Border appearance and shadow overflow are
specified by [Intrinsic chrome](intrinsic-chrome.md#intrinsic-chrome-contract).

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

Percentage and automatic dimensions inside unbounded measure follow the
[layout length rules](layout.md#lengths). A shadow expands visual bounds only;
it never changes desired size, content bounds, or the parent's layout slot.

## Painting and hit testing

- The control body may paint the border box, including padding and the backdrop
  beneath border glyphs.
- Margin cells retain the surface already painted by the parent.
- Border glyphs overlay the body on enabled edges.
- Content and descendants receive the border-and-padding-deflated rectangle.
- Hit testing uses arranged ownership and clipping rules; shadow overflow does
  not enlarge the authored hit target.
- `Visibility.Collapsed` contributes no desired size and skips content layout;
  `Visibility.Hidden` participates in layout but not painting or input.

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

The parent reserves the margin. `panel.Bounds` begins inside that reservation;
its border occupies one cell on each enabled edge, and its children are arranged
inside the remaining padded `ContentBounds`.

## Test obligations

| Layer      | Required evidence                                                                                           |
| ---------- | ----------------------------------------------------------------------------------------------------------- |
| Unit       | Constructor and setter validation, exact desired size, bounds, content bounds, alignment, and invalidation. |
| Surface    | Distinct parent/body/border/content styles proving which cells each box layer owns.                         |
| Randomized | Fixed-seed containment, non-negative extents, deterministic repetition, and saturated tiny-view behavior.   |

- Cover every edge independently and all four edges together.
- Cover zero-size and smaller-than-inset slots.
- Cover automatic, fixed, percentage, and proportional dimensions.
- Cover hidden and collapsed participation.
- Cover border changes from local values, visual states, and theme publication.
