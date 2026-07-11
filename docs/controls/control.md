# Control base API

## Control contract

`Control` is the abstract mutable UI element. It belongs to at most one parent
and, while attached, exactly one
[`Dispatcher`](../concepts/threading.md#threading-contract). Detached trees can
be assembled on any thread. Attached mutation and disposal must run on that
dispatcher.

## Core properties

| Property                                   | Default                | Contract                                                |
| ------------------------------------------ | ---------------------- | ------------------------------------------------------- |
| `Width`, `Height`                          | `Length.Auto`          | Fixed, percentage, automatic, or proportional `Length`. |
| `MinWidth`, `MinHeight`                    | `0`                    | Non-negative cell minimums.                             |
| `MaxWidth`, `MaxHeight`                    | `int.MaxValue`         | Cell maximums not below the corresponding minimum.      |
| `Margin`                                   | Zero edges             | External non-negative `Thickness`.                      |
| `Padding`                                  | Zero edges             | Internal non-negative `Thickness`.                      |
| `HorizontalAlignment`, `VerticalAlignment` | `Stretch`              | Placement within the arranged slot.                     |
| `Visibility`                               | `Visible`              | Visible, hidden, or collapsed.                          |
| `IsEnabled`                                | `true`                 | Inherited effective input state.                        |
| `IsHitTestVisible`                         | `true`                 | Whether pointer hit testing may target the control.     |
| `CanFocus`, `TabIndex`                     | `false`, `0`           | Focus participation and deterministic order.            |
| `IsFocused`, `IsHovered`, `IsPressed`      | `false`                | Read-only committed interaction state.                  |
| `Style`                                    | `null`                 | Optional direct resource; null inherits from ancestors. |
| `Appearance`                               | Empty resolved overlay | Read-only resolved current-state overlay.               |
| `DesiredSize`                              | Empty                  | Read-only result of the last successful measure.        |
| `Bounds`                                   | Empty                  | Read-only committed arranged rectangle.                 |

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

Derived controls implement `MeasureCore(Constraint)` to report intrinsic content
size and `ArrangeCore(Rect)` to receive their committed content box. The base
class owns margin, padding, explicit/deferred length resolution, min/max
clamping, alignment, caching, collapse behavior, dispatcher checks, and
reentrancy guards. Extension points therefore deal only with content; they do
not repeat box-model arithmetic.

If an extension point changes a layout property, that invalidation remains
pending for a later transaction. If it throws, the active phase is marked dirty
again before the exception escapes.

Control content always draws through a canvas clipped to its own `Bounds`.
Containers may opt to retain only the ancestor clip for descendants; this is the
shared mechanism behind documented Overlay and Canvas unclipped-child modes.

## Styling extension point

`GetVisualState()` derives normal, hovered, focused, pressed, and disabled flags
from behavior. Controls with semantic selection override it to add checked
state. `ResolvedStyle` converts the inherited current `Appearance` into the
complete terminal cell style used by rendering.

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
