# Container base API

## Container contract

`Container : Control` is the abstract authoring role for a layout control whose
ordered children are part of its public API. It exposes one caller-managed
[`Children`](#children-and-ownership) collection and supplies shared auto-size,
scrolling, rendering, clipping, and pointer-target traversal. It is not the base
for every control that owns descendants: use
[`ContentControl`](content-control.md#contentcontrol-contract) for one
replaceable content value and
[`CompositeControl`](composite-control.md#compositecontrol-contract) for a
private retained implementation tree.

`Container` does not choose a layout algorithm. Every concrete subclass must
override `MeasureOverride` and `ArrangeOverride`; the abstract declarations make
an incomplete layout control a compile-time error.

## API

| Member                          | Availability | Purpose                                                           |
| ------------------------------- | ------------ | ----------------------------------------------------------------- |
| `Container()`                   | Protected    | Creates an effectively unbounded public child collection.         |
| `Container(int capacity)`       | Protected    | Creates a child collection with a non-negative maximum count.     |
| `Children`                      | Public       | Gets the mutable ordered caller-managed child collection.         |
| `MeasureOverride(Constraint)`   | Protected    | Measures owned children and returns their intrinsic content size. |
| `ArrangeOverride(Rect)`         | Protected    | Assigns the final content-box slots of owned children.            |
| `AutoSize` and `AutoSizeMode`   | Public       | Select intrinsic grow and shrink behavior.                        |
| `AutoScroll` and scroll members | Public       | Select viewport, offset, scrollbar, and interaction behavior.     |

The finite constructor rejects a negative capacity with
`ArgumentOutOfRangeException`. Concrete controls normally use the unbounded
constructor; specialized presenters may impose a finite semantic limit.

## Children and ownership

`Children` is the public adapter over exactly one container-child ownership
slot. Add, insert, replacement, removal, clearing, and enumeration preserve
stable order. Mutations reject null, disposed, attached, duplicate,
cross-parent, and cyclic candidates before changing the existing tree. Attached
mutation is dispatcher-affine. Removal detaches without disposing; disposing the
container disposes every child still owned by it.

Private scrollbar parts use a separate framework slot and never appear in
`Children`. Cross-cutting rendering, routed ancestry, inherited context,
lifecycle, focus, capture, and disposal traverse the central ownership registry
described by the [base ownership contract](control.md#children-and-ownership).

## Layout

`MeasureOverride` must measure each relevant child through `MeasureChild`, honor
collapsed visibility and margins, and return a non-negative intrinsic content
extent. `ArrangeOverride` must arrange each relevant child through
`ArrangeChild`; the supplied `ResolvedAxes` state records dimensions already
fixed by the parent algorithm. Layout code must not add, remove, or replace
children during measure or arrange.

Use a shipped semantic panel when its algorithm matches the application:
[`Stack`](layout/stack.md#stack-contract),
[`Grid`](layout/grid.md#grid-contract), [`Dock`](layout/dock.md#dock-contract),
or [`Overlay`](layout/overlay.md#overlay-contract). The shared
[layout contract](../concepts/layout.md#layout-contract) owns constraints,
alignment, margins, sizing, and rounding.

## Auto-size and scrolling

`AutoSize` sizes the container border box from the measured content extent;
`AutoSizeMode` selects grow-and-shrink or grow-only behavior. The exact rules
are defined by [grow and shrink](../concepts/layout.md#grow-and-shrink).

`AutoScroll` turns the container into a viewport over its own layout algorithm.
The inherited scroll properties select axes, offsets, bar reservation, chrome,
line and page changes, and interaction. Generated bars are private framework
parts. The [scrolling contract](../concepts/scrolling.md#scrolling-contract)
defines feedback, clipping, nested wheel propagation, and offset validation.

## Example

```csharp
public sealed class SharedSlot : Container
{
    protected override Size MeasureOverride(Constraint constraint)
    {
        var width = 0;
        var height = 0;

        foreach (var child in Children)
        {
            var desired = MeasureChild(child, constraint);

            if (child.Visibility == Visibility.Collapsed)
            {
                continue;
            }

            width = Math.Max(
                width,
                (int) Math.Min(int.MaxValue, (long) desired.Width + child.Margin.Horizontal));
            height = Math.Max(
                height,
                (int) Math.Min(int.MaxValue, (long) desired.Height + child.Margin.Vertical));
        }

        return new Size(width, height);
    }

    protected override void ArrangeOverride(Rect bounds)
    {
        foreach (var child in Children)
        {
            ArrangeChild(child, bounds);
        }
    }
}
```

## Test obligations

Tests prove the abstract public shape, child capacity and ownership rejection,
dispatcher affinity, stable order, layout invalidation, measure and arrange
extension points, rendering and pointer traversal, disposal, auto-size, scroll
geometry, clipping, nested propagation, generated-part encapsulation, and
reflection proof that concrete subclasses must implement both layout passes.
