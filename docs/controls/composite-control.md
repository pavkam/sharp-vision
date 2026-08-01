# CompositeControl base API

## Overview

`CompositeControl` is the abstract authoring role for a reusable component made
from a retained private tree of existing controls. It derives directly from
[`Control`](control.md#overview); it is not a `Container`, exposes no public
`Children` collection, and does not expose its implementation root as publicly
replaceable content.

Use `CompositeControl` when the component owns the identity and lifetime of its
implementation tree. Use [`ContentControl`](content-control.md#overview) when
callers own a replaceable semantic content value, and use
[`Container`](container.md#overview) only for a genuine layout control whose
arbitrary children are part of its public contract.

## API

| Member                       | Availability         | Purpose                                                                                     |
| ---------------------------- | -------------------- | ------------------------------------------------------------------------------------------- |
| `InitializeContent(Control)` | Protected, once      | Transfers one detached root into the permanent private composition slot.                    |
| `Content`                    | Protected, read-only | Lets derived behavior coordinate the committed private root without exposing it to callers. |

Callers configure the composite's public semantic properties plus the inherited
[`Control` properties](control.md#api); they cannot replace its implementation
tree.

## Construction and ownership

A concrete constructor creates its complete retained subtree and transfers one
detached root through `InitializeContent(root)`. The protected method is
non-virtual and may commit exactly one root during the component's lifetime. A
layout container such as `Stack`, `Grid`, or `Dock` serves as that root when the
component needs multiple implementation children.

`Screen` is the framework specialization. It commits the authored root through
the same permanent composition edge, then owns temporary application surfaces
through a separate private presentation slot. The protected initialization API
remains non-virtual.

The composition-root slot has capacity one, occupies the normal render layer,
participates in hit testing and focus navigation, and invalidates measure when
committed. The protected non-virtual `Content` getter returns the currently
committed root so derived behavior can coordinate retained controls. Neither
member permits callers to replace or remove the root.

`InitializeContent` rejects:

- null with `ArgumentNullException`;
- a disposed root with `ObjectDisposedException`;
- an attached, already-owned, or cyclic root with `ArgumentException`;
- a repeated call, including one after direct root disposal, with
  `InvalidOperationException`; and
- off-dispatcher access or reentrant ownership publication with
  `InvalidOperationException`.

Validation failures before structural commit do not consume initialization, so a
constructor may recover from a caught invalid candidate and transfer a valid
root. Once the ownership edge commits, initialization is permanent even if a
parent, lifecycle, or other ownership-publication callback throws. During such a
callback, `Content` already returns the committed root. Callback failure never
rolls back the edge or reopens initialization.

An uninitialized composite cannot attach as a root or enter another ownership
slot. This validation occurs before dispatcher or parent context changes. The
framework does not call a virtual factory from the base constructor, build
lazily from layout, or retry construction during rendering.

## Layout and traversal

The base measures the retained root once through `MeasureChild`. Visible and
hidden content contributes its desired border-box size plus margin using
saturating arithmetic. Collapsed content contributes neither size nor margin and
has stale desired size and bounds cleared by the shared layout transaction.

Arrangement passes the component's complete content box through
`ArrangeChild(root, bounds, ResolvedAxes.Both)`. The child applies its own
margin inside that box. The component's border and padding remain owned by the
shared [box model](../concepts/layout.md#passes-and-rounding).

Rendering, normal and popup hit testing, focus navigation, routed ancestry,
theme and Unicode context, inherited availability, lifecycle, focus and capture
cleanup, and disposal all follow the registered composition-root edge. The base
adds no parallel traversal or hidden public collection.

## Lifetime

Disposing the component disposes its retained root exactly once and continues
through the shared callback-failure rules. Disposing the root directly removes
the ownership edge, but does not turn the component into a reusable shell:
`Content`, attachment, and layout then reject the missing permanent root, and a
second `InitializeContent` call is invalid.

## Example

```csharp
public sealed class StatusCard: CompositeControl
{
    private readonly Text _status;

    public StatusCard(string label, string status)
    {
        ArgumentNullException.ThrowIfNull(label);
        ArgumentNullException.ThrowIfNull(status);

        _status = new Text(status);
        var root = new Stack { Spacing = 1 };
        root.Children.Add(new Text(label));
        root.Children.Add(_status);
        InitializeContent(root);
    }

    public string Status
    {
        get => _status.Content;
        set => _status.Content = value;
    }
}
```

## Expected behavior

Tests cover reflection shape, valid and repeated initialization, every candidate
rejection, recovery after pre-commit rejection, callback failure after commit,
use before initialization, direct-root and owner disposal, dispatcher and
context propagation, cached first layout, collapsed and margin geometry,
rendering, normal and popup hit testing, and focus navigation.
