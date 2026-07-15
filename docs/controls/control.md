# Control base API

## Control contract

`Control` is the abstract mutable UI element. It belongs to at most one parent
and, while attached, exactly one
[`Dispatcher`](../concepts/threading.md#threading-contract). Detached trees can
be assembled on any thread. Attached mutation and disposal must run on that
dispatcher.

## Core properties

| Property                                   | Default        | Contract                                                                                                                                     |
| ------------------------------------------ | -------------- | -------------------------------------------------------------------------------------------------------------------------------------------- |
| `Width`, `Height`                          | `Length.Auto`  | Fixed, percentage, automatic, or proportional `Length`.                                                                                      |
| `MinWidth`, `MinHeight`                    | `0`            | Non-negative cell minimums.                                                                                                                  |
| `MaxWidth`, `MaxHeight`                    | `int.MaxValue` | Cell maximums not below the corresponding minimum.                                                                                           |
| `Margin`                                   | Zero edges     | External non-negative `Thickness`.                                                                                                           |
| `Padding`                                  | Zero edges     | Internal non-negative `Thickness`.                                                                                                           |
| `HorizontalAlignment`, `VerticalAlignment` | `Stretch`      | Placement within the arranged slot.                                                                                                          |
| `Visibility`                               | `Visible`      | Visible, hidden, or collapsed.                                                                                                               |
| `IsEnabled`                                | `true`         | Inherited effective input state.                                                                                                             |
| `IsHitTestVisible`                         | `true`         | Whether pointer hit testing may target the control.                                                                                          |
| `CanFocus`, `TabIndex`                     | `false`, `0`   | Focus participation and deterministic order.                                                                                                 |
| `IsFocused`, `IsHovered`, `IsPressed`      | `false`        | Read-only committed interaction state; only interactive (focusable) controls are hovered, and composite hover belongs to its semantic owner. |
| `Style`                                    | `null`         | Optional per-instance `IControlStyle` overlay; null uses only the theme chain.                                                               |
| `DesiredSize`                              | Empty          | Read-only result of the last successful measure.                                                                                             |
| `Bounds`                                   | Empty          | Read-only committed arranged rectangle.                                                                                                      |

## Intrinsic chrome

Every control owns border, shadow, and body-fill properties; there are no
dedicated border or shadow wrapper controls. Use an ordinary container such as
`Dock` when chrome needs a distinct ownership, layout, and rendering node around
another control.

| Property                                                   | `Control` default | Contract                                                                                     |
| ---------------------------------------------------------- | ----------------- | -------------------------------------------------------------------------------------------- |
| `FillMode`                                                 | `Transparent`     | Chooses whether body fill preserves or replaces destination cells.                           |
| `BorderThickness`                                          | Zero edges        | Reserves and draws independently enabled zero-or-one-cell edges.                             |
| `BorderGlyphs`                                             | `Glyphs.Default`  | Supplies the validated physical glyph family for the enabled edges.                          |
| `BorderColor`, `BorderAttributes`                          | `null`            | Optionally override border color and attributes.                                             |
| `HasShadow`                                                | `false`           | Enables translated visual overflow without reserving layout or changing hit testing.         |
| `ShadowMode`                                               | `Composite`       | Preserves destination graphemes or replaces the footprint with `ShadowGlyph`.                |
| `ShadowOffset`                                             | `(0, 0)`          | Supplies the signed cell translation used to compute the shadow footprint and visual bounds. |
| `ShadowGlyph`                                              | `▓`               | Supplies the printable, exactly one-cell-wide block-mode Rune.                               |
| `ShadowForeground`, `ShadowBackground`, `ShadowAttributes` | `null`            | Optionally override only the shadow footprint's resolved style.                              |

`BorderThickness` rejects an edge greater than one cell and invalidates measure,
arrange, and render. The remaining border and shadow properties invalidate
render only. `ShadowMode` rejects undefined enum values; `ShadowGlyph` rejects
control or non-one-cell Runes. Validation occurs before observable state
changes. Derived controls may publish different class defaults; for example,
`Button` owns a one-cell rounded border and compact dim shadow.

The border box includes border cells. Base measure and arrange remove margin,
resolve that box, then reserve `BorderThickness` before `Padding`, so extension
points receive only the content box. Shadow overflow expands `VisualBounds`, is
clipped by ancestor policy and the frame, and remains outside `Bounds`; it does
not affect desired size, arranged child slots, or pointer targeting.

Setters validate before mutation, verify dispatcher access while attached, and
raise `PropertyChanged` once after the changed value is committed. Invalid
lengths, negative constraints, inconsistent min/max, invalid enum values, and
disposed access throw documented argument or object-lifetime exceptions.

`EffectiveIsEnabled` and `EffectiveIsVisible` are computed through the complete
ancestor chain. Changing an inherited state invalidates affected descendants.
`IsHitTestVisible` affects pointer targeting only; it does not suppress drawing,
visibility, enabled state, or explicit focus.

## Children and ownership

`Container.Children` is the mutable ordered collection for traditional component
composition. `Add`, indexed insert and replacement, `Remove`, and `Clear`
validate the complete operation before changing ownership. A control cannot have
two parents, appear twice, or be inserted beneath one of its own descendants.

Adding below an attached container recursively attaches the subtree. Removing
recursively detaches it and clears its parent. Disposing a container disposes
all owned descendants; repeated disposal is safe.

Specialized single-child containers use the same collection with capacity one.
Their child property validates a complete replacement before detaching the
previous child, so a failed assignment preserves ownership, dispatcher, focus,
and pointer capture.

When a root owns focus or capture managers, that ownership propagates with the
tree. Removal, inherited disable/hide, and disposal synchronously release
manager state before clearing parent or dispatcher references.

```csharp
container.Children.Add(control);
Debug.Assert(control.Parent == container);
```

## Invalidation

Dirty phases form a dependency closure: measure implies arrange and render,
arrange implies render, and render stands alone. Property setters request the
earliest affected phase and coalesce repeated requests while they bubble to the
root.

| Change                                            | Dirty phases                 |
| ------------------------------------------------- | ---------------------------- |
| Width, height, min/max, margin, padding, collapse | Measure, arrange, and render |
| Horizontal or vertical alignment                  | Arrange and render           |
| Enabled state or visible/hidden transition        | Render                       |
| Hit-test visibility                               | No layout or render phase    |
| Border thickness                                  | Measure, arrange, and render |
| Other border or shadow chrome                     | Render                       |

## Lifecycle and events

Attachment assigns the same dispatcher recursively. Detachment clears it
recursively. Focus and pointer capture are synchronously released when a control
becomes unavailable or leaves the owned tree.

`AddHandler<TArgs>` registers a typed synchronous handler and returns an
idempotent removal token. Routed arguments expose `OriginalSource`, retargetable
`Source`, route `Phase`, and `Handled`; preview and bubble use stable ancestry
and handler snapshots even when a callback mutates the tree. The standard
[`Events`](../concepts/input-routing.md#routed-event-api) cover key, text,
pointer, paste, and terminal focus payloads.

## Layout extension points

Derived controls implement `MeasureOverride(Constraint)` to report intrinsic
content size and `ArrangeOverride(Rect)` to receive their committed content box.
The base class owns margin, padding, explicit/deferred length resolution,
min/max clamping, alignment, caching, collapse behavior, dispatcher checks, and
reentrancy guards. Extension points therefore deal only with content; they do
not repeat box-model arithmetic.

If an extension point changes a layout property, that invalidation remains
pending for a later transaction. If it throws, the active phase is marked dirty
again before the exception escapes.

Building a composite control out of existing controls, rather than a new
primitive, does not use these seams directly; derive from
[`View`](../concepts/custom-components.md#custom-components-contract) and
implement `Build()` instead.

Control content always draws through a canvas clipped to its own `Bounds`.
Containers may opt to retain only the ancestor clip for descendants; this is the
shared mechanism behind documented Overlay and Canvas unclipped-child modes.

## Styling extension point

Style properties, themes, and visual-state resolution are defined in
[Styling and visual states](../concepts/styling.md#styling-contract). This
control exposes only its own properties and class defaults here.

`GetVisualState()` derives normal, hovered, focused, pressed, and disabled flags
from behavior. Controls with semantic selection override it to add checked
state. Only interactive (focusable) controls are ever marked hovered, so the
hovered flag never appears on static content such as text or tables.
`GetResolvedStyle` converts the active theme cascade into the complete terminal
cell style used by rendering.

The base `OnRender` calls `RenderChrome`, which rasterizes body fill, intrinsic
border, and shadow. A derived control that fully overrides `OnRender` opts out
of that base call and must call protected `RenderChrome` when it wants intrinsic
chrome. The complete geometry and shadow composition rules live in
[Shared chrome](../concepts/styling.md#shared-chrome).

## Example

```csharp
control.Width = Length.Cells(14);
control.Margin = new Thickness(horizontal: 1, vertical: 0);
control.IsEnabled = true;

using var registration = control.AddHandler(
    Events.Key,
    (_, args) => args.Handled = true);
```

## Test obligations

Every concrete control tests validation-before-mutation, phase-specific
invalidation, dispatcher affinity, attach/detach ownership, visibility, enabled
inheritance, focus/capture cleanup, zero/tiny bounds, and final cells.
