# ContentControl base API

## ContentControl contract

`ContentControl` is the abstract authoring role for a control that owns zero or
one publicly replaceable `Control`. It derives directly from
[`Control`](control.md#control-contract). Its non-virtual `Content` property is
`null` by default; a derived role may observe committed changes through
`OnContentChanged(previous, current)` without replacing the ownership engine.

Use `ContentControl` when arbitrary callers may replace one semantic content
value. A component whose retained implementation tree is private uses the
separate composition role described by the
[component architecture](../superpowers/specs/2026-07-15-component-architecture-v2-design.md#compositecontrol).
A general-purpose panel whose callers may add arbitrary children remains a
`Container`. Focusable single-face controls derive from
[`Pressable`](pressable.md#pressable-contract), which inherits this exact
content transaction instead of adding another content property.

## Ownership and mutation

The base constructor registers one capacity-one, normal-layer content slot
before a derived constructor can register private parts. The slot participates
in hit testing and focus navigation and has `ChangeImpact.Measure`. It uses the
same [owned-control transaction](control.md#children-and-ownership) as every
other visual edge.

Assigning a detached control commits it as `Content`. Assigning `null` clears
the slot. Replacement detaches the previous control but does not dispose it;
ownership of that detached control returns to the caller. Assigning the
identical instance or clearing an already-empty slot is a no-op. Validation
rejects disposed, attached, already-owned, duplicate-slot, cross-parent, and
cyclic candidates before changing the old edge, inherited context, focus, or
pointer capture.

While attached, every setter call is dispatcher-affine. Dispatcher and lifetime
checks occur before equivalence is accepted, so off-dispatcher replacement,
clear, and identical assignment all throw without mutation. Setting content can
throw:

- `ArgumentException` when the candidate already belongs to a tree or would
  create a cycle;
- `InvalidOperationException` for off-dispatcher access or reentrant structural
  publication; or
- `ObjectDisposedException` when the owner or candidate is disposed.

## Change publication

After a successful structural commit and parent/theme/detach/attach publication,
the registry requests `Measure` invalidation once. The base then snapshots the
previous and current controls, updates its published content state, calls
`OnContentChanged(previous, current)`, and raises
`PropertyChanged(nameof(Content))` exactly once. Both callbacks observe the
complete new structure: old content is detached, current content has this owner,
and `Content` returns the current value. A property subscriber may run layout at
the unchanged viewport and immediately observe the current content measured and
arranged; that layout consumes the already-pending invalidation without a
redundant pass after notification.

Equivalent and rejected operations call neither callback. Direct disposal of the
current child removes it through its exact content slot, clears `Content`, and
publishes the same `(previous, null)` callback and property notification.

If `OnContentChanged` throws, the property notification is still attempted. The
hook failure wins over a later property-handler failure, but an earlier failure
from availability or structural publication remains the ownership transaction's
authoritative first exception. Callback failure never rolls back the structural
commit.

`OnContentChanged` and the property notification run while guarded structural
publication remains active. They may inspect and lay out the committed tree, but
attempts to mutate any owned slot, replace or clear `Content`, or dispose the
owner or either affected content control throw `InvalidOperationException`. The
guard prevents callbacks from turning one atomic publication into a nested
transaction.

## Layout and traversal

Visible or hidden content is measured once through `MeasureChild`. The reported
content size adds the child's horizontal and vertical margin with saturating
integer arithmetic. Arrangement passes the owner's complete content box to
`ArrangeChild(content, bounds, ResolvedAxes.Both)`; the child applies its margin
inside that slot, and both axes are the sizes resolved by this parent.

Collapsed content contributes neither desired size nor margin and enters neither
child layout override. The base child transactions still clear its prior
`DesiredSize` and `Bounds`. The owner follows the shared
[border and padding box model](../concepts/layout.md#passes-and-rounding).

Rendering, normal and popup hit testing, routed ancestry, focus navigation,
theme and Unicode context, inherited availability, lifecycle, and popup
traversal all follow the registered content edge. `ContentControl` adds no
role-specific traversal override.

## Disposal

Disposing a current child directly clears `Content` and publishes one content
change. Disposing the owner disposes the currently assigned child exactly once.
Owner disposal continues to completion when the content hook or a property
handler throws, then rethrows the first recorded callback failure from the fully
disposed tree. Previously replaced or cleared content remains caller-owned and
is not disposed by the former owner.

## Example

```csharp
public sealed class Card: ContentControl
{
    public Card(Control content)
    {
        ArgumentNullException.ThrowIfNull(content);
        Content = content;
    }

    protected override void OnContentChanged(Control? previous, Control? current)
    {
        // The ownership transaction is already committed here.
    }
}
```

## Test obligations

Tests cover null/assignment/equivalence/replacement/clear, every ownership
rejection, dispatcher affinity including equivalent assignment, callback and
property ordering, callback failures, direct-child and owner disposal, collapsed
content, margin saturation, both-axis arrangement, rendering, hit testing,
navigation, popup traversal, and compilation from the unfriended consumer
assembly.
