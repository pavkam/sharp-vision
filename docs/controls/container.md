# Container base API

## Overview

`Container : ControlBase` is the abstract base for layout controls whose ordered
children are part of their public API. It exposes one caller-managed
[`Children`](#children-and-ownership) collection and supplies the shared
auto-size, scrolling, rendering, clipping, and pointer-target traversal
behavior. It is not the base for every control that owns descendants: use
[`ContentControl`](content-control.md#overview) when there is one replaceable
content value, and [`CompositeControlBase`](composite-control.md#overview) when
the control keeps a private retained implementation tree.

`Container` does not choose a layout algorithm. Every concrete subclass must
override `MeasureOverride` and `ArrangeOverride`; because both declarations are
abstract, an incomplete layout control fails to compile.

## API

| Member                          | Availability | Purpose                                                           |
| ------------------------------- | ------------ | ----------------------------------------------------------------- |
| `Container()`                   | Protected    | Creates an effectively unbounded public child collection.         |
| `Container(int capacity)`       | Protected    | Creates a child collection with a non-negative maximum count.     |
| `Children`                      | Public       | Gets the mutable ordered caller-managed child collection.         |
| `OnChildrenChanged()`           | Protected    | Runs after a `Children` mutation structurally commits.            |
| `MeasureOverride(Constraint)`   | Protected    | Measures owned children and returns their intrinsic content size. |
| `ArrangeOverride(Rect)`         | Protected    | Assigns the final content-box slots of owned children.            |
| `AutoSize` and `AutoSizeMode`   | Public       | Select intrinsic grow and shrink behavior.                        |
| `AutoScroll` and scroll members | Public       | Select viewport, offset, scrollbar, and interaction behavior.     |

The finite constructor rejects a negative capacity with
`ArgumentOutOfRangeException`. Concrete controls normally use the unbounded
constructor; specialized presenters may impose a finite semantic limit.

## Children and ownership

`Children` is the public adapter over exactly one container-child ownership
slot. Add, insert, replacement, removal, clearing, and enumeration preserve a
stable order. Every mutation rejects null, disposed, attached, duplicate,
cross-parent, and cyclic candidates before it changes the existing tree, and
mutation while attached is dispatcher-affine. Removing a child detaches it
without disposing it; disposing the container disposes every child it still
owns.

Private scrollbar parts use a separate framework slot and never appear in
`Children`. Cross-cutting rendering, routed ancestry, inherited context,
lifecycle, focus, capture, and disposal all traverse the central ownership
registry described by the
[base ownership rules](control.md#children-and-ownership).

A derived container overrides `OnChildrenChanged` to observe a mutation of its
own `Children`, cache per-child metadata, or react to the change - the same
notification `ItemsControl` already consumes on its own private presentation
host. The hook runs after the mutation structurally commits and cannot reject a
candidate. Validation belongs at the point of insertion, because layout runs
asynchronously and cannot serve as a validation seam.

## Layout

`MeasureOverride` must measure each relevant child through `MeasureChild`, honor
collapsed visibility and margins, and return a non-negative intrinsic content
extent. `ArrangeOverride` must arrange each relevant child through
`ArrangeChild`; the supplied `ResolvedAxes` value records which dimensions the
parent algorithm has already fixed. Layout code must not add, remove, or replace
children during measure or arrange.

Use a shipped semantic panel when its algorithm matches the application:
[`Stack`](layout/stack.md#overview), [`Grid`](layout/grid.md#overview),
[`Dock`](layout/dock.md#overview), or [`Overlay`](layout/overlay.md#overview).
The shared [layout rules](../concepts/layout.md#overview) own constraints,
alignment, margins, sizing, and rounding.

## Auto-size and scrolling

`AutoSize` sizes the container border box from the measured content extent, and
`AutoSizeMode` selects grow-and-shrink or grow-only behavior. The exact rules
are defined by [grow and shrink](../concepts/layout.md#grow-and-shrink).

`AutoScroll` turns the container into a viewport over its own layout algorithm.
The inherited scroll properties select axes, offsets, bar reservation, chrome,
line and page changes, and interaction. Generated bars are private framework
parts. The [scrolling rules](../concepts/scrolling.md#overview) define feedback,
clipping, nested wheel propagation, and offset validation.

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
            if (child.Visibility == Visibility.Collapsed)
            {
                continue;
            }

            var desired = MeasureChild(child, constraint);

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

## Expected behavior

Callers can rely on the public shape described above: the child collection
enforces its capacity and rejects invalid ownership, attached mutation is
dispatcher-affine, order stays stable, and mutations invalidate layout. The
measure and arrange extension points, rendering and pointer traversal, disposal,
auto-size, scroll geometry, clipping, nested wheel propagation, and the
encapsulation of generated scrollbar parts all behave as documented, and tests
hold concrete subclasses to implementing both layout passes.
