# Hover highlight scoped to interactive controls

## Status

Design approved 2026-07-14. Ready for an implementation plan.

## Problem

Any control under the pointer is currently highlighted, most visibly in the
`White` theme. Hovering plain `Text`, a `Table`, or the `Grid` cells inside it
tints their foreground exactly as if they were buttons. Hover feedback should be
reserved for **interactive** controls (button, edit, and the like); static
content must not react to the pointer.

### Root cause

Hover is resolved in
[`CaptureManager.ResolveHover`](../../../src/SharpVision/Input/CaptureManager.cs):

```csharp
private static Control? ResolveHover(Control? physical)
{
    for (Control? current = physical; current is not null; current = current.Parent)
        if (current.OwnsHover) return current;
    return physical;   // fallback: any hit leaf becomes the hover target
}
```

Only [`Pressable`](../../../src/SharpVision/Controls/Pressable.cs) overrides
`OwnsHover => true`; the base
[`Control.OwnsHover`](../../../src/SharpVision/Controls/Control.cs) is `false`.
When the pointer is over a non-interactive control, the walk up the tree finds
no owner and the fallback returns the physical leaf. That leaf receives
`SetHovered(true)`, `GetVisualState()` adds `State.Hovered`, and the theme's
base `ControlStyle<Control>` applies its hover foreground to **every** control
type, so static content highlights.

The interactive controls that _should_ highlight all set `CanFocus = true`
(every `Pressable`: `Button`, `CheckBox`, `RadioButton`, `ComboBox`, `MenuItem`,
`ListItem`; plus `TextInput`, `ScrollBar`, `List`). The controls that should not
(`Text`, `RichText`, `FigletText`, `Table`, `Grid`, and layout containers) are
all non-focusable. `CanFocus` is therefore an exact proxy for "interactive."

## Goals

- Hover feedback appears only on interactive controls.
- Static content (`Text`, `Table`, `Grid`, layout containers) never highlights
  and never re-renders merely because the pointer passes over it.
- One uniform rule, no per-control opt-in to forget when adding a new control.

## Non-goals

- No change to pointer **event routing**: static controls still receive pointer
  events (clicks, etc.).
- No new visual-state, no theme redesign, no new public concept.
- Inline hover targets (`Hyperlink : Inline`) are out of scope; they render
  through the inline flow, not the `Control` hover path.

## Design

Hover participates **iff a control is interactive**, where interactive is
defined as `CanFocus == true`. The rule is enforced at the input-resolution
layer so non-interactive controls are never marked hovered — no `State.Hovered`,
no style-cache invalidation, no re-render on pointer move.

### Change 1 — `Control.OwnsHover` follows `CanFocus`

In [`Control.cs`](../../../src/SharpVision/Controls/Control.cs), change the
default from `=> false` to `=> CanFocus`, and update the doc comment to state
that `OwnsHover` marks an interactive hover target. Every focusable control then
owns hover; every non-focusable one does not.

### Change 2 — `ResolveHover` returns null when nothing is interactive

In [`CaptureManager.cs`](../../../src/SharpVision/Input/CaptureManager.cs), the
fallback becomes `return null` instead of `return physical`:

```csharp
private static Control? ResolveHover(Control? physical)
{
    for (Control? current = physical; current is not null; current = current.Parent)
        if (current.OwnsHover) return current;
    return null;
}
```

Hover now resolves to the nearest interactive ancestor of the hit control, or
nothing. A composite interactive control (e.g. a `Button` wrapping an inner
`Text` label) still resolves to the button, because the button owns hover while
the label does not.

### Change 3 — remove the redundant `Pressable` override

In [`Pressable.cs`](../../../src/SharpVision/Controls/Pressable.cs), delete
`OwnsHover => true`. It is now covered by the `CanFocus`-based default, and
removing it keeps a single source of truth: a `Pressable` whose `CanFocus` is
turned off consistently stops highlighting too.

### Deliberately unchanged

- **`GetVisualState()` and the `IsHovered → State.Hovered` mapping.** Gating is
  purely a matter of _which_ controls get `SetHovered(true)` called on them.
  Unit tests that call `SetHovered(true)` directly (rendering, button) keep
  exercising the overlay.
- **The theme.** The base `ControlStyle<Control>` hover foreground stays; it now
  only ever resolves for interactive controls, because only they enter
  `State.Hovered`.
- **Pointer event routing.** `Dispatch` still routes to the physical `target`;
  only hover tracking is filtered.

### Public API semantic change

`PointerDevice.Hovered` and `Control.IsHovered` now report the interactive hover
target, or `null`/`false` over static content, rather than any hit leaf.
`PointerDevice` is newly added, so this is the moment to settle the semantic.
Update the XML docs on `PointerDevice.Hovered` and `Control.IsHovered`, and any
prose that describes hover as tracking the physical leaf.

### Accepted edge case

A focusable **container** (notably `List`) highlights when the pointer is over
its own chrome between items; its items still resolve to themselves. This is
consistent with the `CanFocus` rule and is effectively invisible — hover only
changes foreground and the container draws no direct text — so it is left as is.

## Testing

Existing suites that already use interactive probes or real interactive controls
pass unchanged: the `CaptureManager` capture test uses `ProbePressable`;
`ButtonTests` uses `Button`; the gallery tests use `NavigationItem`, `Button`,
and `List` (all focusable). Tests that call `SetHovered(true)` directly are
unaffected.

Changes:

- **Update** the two
  [`PointerTests`](../../../tests/SharpVision.Tests/Input/PointerTests.cs) that
  hover a non-focusable `ProbeControl` and assert hover
  (`Dispatch_WhenPointerHitsChild_ProvidesLocalCoordinatesAsync` and
  `Dispatch_WhenPointerMovesPressesAndLeaves_UpdatesVisualStatesAsync`): set
  `CanFocus = true` on those probes so they remain valid hover targets and the
  mechanism under test is preserved.

Additions:

- **Non-interactive leaf is not hovered:** a pointer over a non-focusable
  `ProbeControl` leaves `manager.Hovered` null and `child.IsHovered` false.
- **Hover resolves to the nearest interactive ancestor:** a non-focusable child
  inside a focusable ancestor (e.g. a `CanFocus` `ProbeContainer`) resolves
  hover to the ancestor when the child is hit.

## Documentation

- [`docs/controls/control.md`](../../controls/control.md): update the
  `IsHovered` / composite-hover row and the `GetVisualState` note to state that
  hover targets are interactive (focusable) controls.
- [`docs/concepts/styling.md`](../../concepts/styling.md): note in the
  visual-states section that the hover overlay applies only to interactive
  controls.
- `PointerDevice` prose (hosting concept / XML docs) describing "current hover
  target": clarify it is the interactive hover target, or null.

Run `make format`, `make lint`, `make build`, `make test`.

## Files touched

Production:

- `src/SharpVision/Controls/Control.cs` (`OwnsHover` default + doc; `IsHovered`
  doc)
- `src/SharpVision/Input/CaptureManager.cs` (`ResolveHover` fallback)
- `src/SharpVision/Controls/Pressable.cs` (remove redundant override)
- `src/SharpVision/Runtime/PointerDevice.cs` (`Hovered` XML doc)

Tests:

- `tests/SharpVision.Tests/Input/PointerTests.cs` (two updates, two additions)

Docs:

- `docs/controls/control.md`, `docs/concepts/styling.md`, and the
  `PointerDevice` hover prose.
