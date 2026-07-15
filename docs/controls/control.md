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

Setters validate before mutation, verify dispatcher access while attached, and
raise `PropertyChanged` once after the changed value is committed. Invalid
lengths, negative constraints, inconsistent min/max, invalid enum values, and
disposed access throw documented argument or object-lifetime exceptions.

`EffectiveIsEnabled` and `EffectiveIsVisible` are computed through the complete
ancestor chain. Changing an inherited state invalidates affected descendants.
`IsHitTestVisible` affects pointer targeting only; it does not suppress drawing,
visibility, enabled state, or explicit focus.

## Children and ownership

Every `Control` owns one central registry of distinct ordered visual slots.
`Control.Parent` is therefore `Control?`; ownership is not evidence that the
parent exposes a public child collection. Slots record structural role, render
layer, hit-test and navigation participation, an optional stable part key, and
the invalidation impact of a committed mutation.

`Container.Children` is the mutable public adapter over only its container-child
slot. Private scrollbar rails, item hosts, and other framework parts use
separate slots over the same registry and never leak into `Children`.
Registering two slots with the same role is valid; slot identity, not role,
determines membership and capacity.

Cross-cutting traversal reads this registry directly instead of testing whether
an owner is a `Container`. Stable tree order is slot registration order followed
by item order. Focus navigation visits only navigation-participating edges in
that order; ordinary rendering visits normal-layer edges in that order; hit
testing visits hit-participating edges in reverse order. Popup-layer edges and
popup descendants are promoted above every ordinary sibling for both rendering
and hit testing and never paint once in the ordinary pass and again in the popup
pass. A `Popup` surface promotes itself while legacy owners still keep it in an
ordinary slot; dedicated popup slots remain the preferred ownership metadata.
Routed ancestry, inherited state, style scopes, lifecycle, focus/capture
cleanup, radio-group discovery, and disposal follow every edge regardless of its
interaction metadata.

`Add`, indexed insert and replacement, `Remove`, `Clear`, and complete-slot
replacement validate the whole proposed snapshot before changing ownership. A
control cannot have two parents, appear twice, be attached independently, or be
inserted beneath one of its own descendants. Batch failure preserves the old
order, parent links, inherited context, focus, and pointer capture.

Adding below an attached owner recursively attaches the subtree. Removing
recursively detaches it and clears its parent. Disposing an owner disposes all
owned descendants; repeated disposal is safe.

Structural transactions release focus and capture while the old tree remains
coherent, then commit slot membership, parent links, dispatcher, Unicode policy,
theme, and manager context without callbacks. Parent, theme, detach, attach, and
slot notifications run only after that complete commit. Callback failures are
remembered while remaining publication and cleanup continue; the first failure
is rethrown from a coherent new tree. Tree mutation and disposal are rejected
while any affected ownership transaction is publishing.

When a root owns focus or capture managers, that ownership propagates through
every registered slot. Removal, inherited disable/hide, and disposal
synchronously release manager state before clearing parent or dispatcher
references. Direct child disposal removes through its exact owning slot with
`ReleaseReason.Disposed`, publishes the slot change, and never emits a second
detached reason. Owner disposal continues across all slots after a descendant
callback failure and disposes each remaining descendant once. The structural
publication guard spans `OnDisposing`, unavailable notification, exact-slot
unlink, and descendant cleanup; a disposal callback cannot switch to ordinary
collection removal to publish `Detached` or bypass the exact slot.

```csharp
container.Children.Add(control);
Debug.Assert(control.Parent == container);
```

## Invalidation

Dirty phases form a dependency closure: measure implies arrange and render,
arrange implies render, and render stands alone. Property setters request the
earliest affected phase and coalesce repeated requests while they bubble to the
root. Public style-property metadata expresses that phase as ordered
`ChangeImpact.None`, `Render`, `Arrange`, or `Measure` values.

| Change                                            | Dirty phases                 |
| ------------------------------------------------- | ---------------------------- |
| Width, height, min/max, margin, padding, collapse | Measure, arrange, and render |
| Horizontal or vertical alignment                  | Arrange and render           |
| Enabled state or visible/hidden transition        | Render                       |
| Hit-test visibility                               | No layout or render phase    |
| Style replacement                                 | Maximum old/new style impact |

The `Arrange` impact always requests arrange plus render, while `Measure`
requests all three phases. Assigning an equivalent value through `SetValue` is a
no-op. Replacing either `Control.Style` or a type style in `Theme` uses the
maximum aggregate impact of the removed and new styles, so removing geometric
values still invalidates their previous layout.

Third-party controls use the same phase vocabulary for ordinary CLR state.
`SetProperty(ref field, value, impact)` verifies dispatcher affinity, rejects an
unknown `ChangeImpact`, suppresses equivalent assignments, commits the field,
invalidates, and then raises `PropertyChanged` once. A coordinated mutation that
has already committed its fields uses `NotifyPropertyChanged(name, impact)`.
`Invalidate(impact)` requests work without a property notification, while
`InvalidateVisualState()` clears resolved appearance caches and requests the
strongest phase required by active styles. None of these seams exposes the
framework's pending phase flags.

## Lifecycle and events

Attachment commits the same dispatcher, Unicode policy, theme, and manager
context across the complete subtree before any lifecycle callback.
`OnAttached()` therefore sees every sibling fully attached. During application
startup it additionally sees the installed focus and capture managers and may
request either service immediately. Detachment clears complete subtree context
before any `OnDetached()` callback, so every sibling is already detached. Direct
`Attach` and `Detach` operations accept only an unowned root; owned controls
change lifecycle context exclusively through their registry edge, so a child can
never attach or detach independently from its parent. `OnDisposing()` runs at
most once before owned state is released; if that hook throws, base cleanup
completes before the original exception is rethrown. These hooks and
`OnParentChanged(Control?, Control?)` always observe committed ownership state.

Focus and pointer capture are synchronously released when a control becomes
unavailable or leaves the owned tree. Derived controls request those behaviors
through the target-safe helpers documented in
[Input routing](../concepts/input-routing.md#pointer-capture-and-coordinates).

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

A derived owner lays out only its direct children through
`MeasureChild(child, constraint)` and `ArrangeChild(child, slot, resolvedAxes)`.
Both reject null and non-direct children before entering the child's internal
transaction. `ArrangeChild` also rejects undefined `ResolvedAxes` flags;
`Width`, `Height`, or `Both` means the parent already resolved that border-box
dimension. Raw `Measure`, `Arrange`, `Render`, phase flags, and layout managers
remain internal.

If an extension point changes a layout property, that invalidation remains
pending for a later transaction. If it throws, the active phase is marked dirty
again before the exception escapes.

Building a composite control out of existing controls, rather than a new
primitive, does not use these seams directly; derive from
[`View`](../concepts/custom-components.md#custom-components-contract) and
implement `Build()` instead.

Control content always draws through a canvas clipped to its own `Bounds`. The
protected `ClipsChildren` override defaults to true. Containers may return false
to retain only the ancestor clip for descendants; this is the shared mechanism
behind documented Overlay and Canvas unclipped-child modes. An unclipped owner
also hit-tests eligible ordinary children outside its own `Bounds`, while the
owner itself remains a target only inside that box. Enabling intrinsic
`AutoScroll` restores viewport and owner-bounds clipping regardless of the
override.

## Styling extension point

Style properties, themes, and visual-state resolution are defined in
[Styling and visual states](../concepts/styling.md#styling-contract). This
control exposes its registered properties through `GetValue`, `SetValue`, and
`ClearValue`; each operation validates applicability before observable mutation.

`GetVisualState()` derives normal, hovered, focused, pressed, and disabled flags
from behavior. Controls with semantic selection override it to add checked
state. Only interactive (focusable) controls are ever marked hovered, so the
hovered flag never appears on static content such as text or tables.
`GetResolvedStyle` converts the active theme cascade into the complete terminal
cell style used by rendering.

Primitive controls also read the protected inherited `CellPolicy` during
measurement and drawing. This keeps grapheme width decisions identical to the
application and frame without exposing policy mutation to derived code.

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
