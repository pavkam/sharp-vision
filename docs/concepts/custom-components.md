# Custom components

## Custom components contract

`View : Container` is the composition base for building a reusable component or
screen out of existing controls. Shipped concrete controls, including `Stack`,
are sealed; `Control`, `Container`, `Pressable`, `View`, and `Screen` are the
current abstract extension bases. `View` is the supported extension point for
assembling controls into a new type without editing framework layout code.

A concrete component derives from `View` and implements
`protected abstract Control Build()`, returning the single content root — a
layout container such as `Stack`, `Dock`, or `Grid` when the component presents
more than one child. The runtime installs that root as the view's only child
after the first successful `Build()` call and ownership transfer, whether or not
the view is attached to a dispatcher at that point: building is attach-agnostic.
A `View` that is never measured never builds. A null return throws a documented
`InvalidOperationException` and leaves the view unbuilt.

`Build()` runs once after successful completion. If it throws, returns null, or
produces content that cannot be installed, the measure failure propagates and
the next measure retries `Build()`. After successful installation, mutate the
subtree like any other control tree (add or remove children, set properties)
instead of expecting reactive rebuilding.

A `View` owns exactly one built content root and stretches it to fill the view's
border-and-padding-deflated content box. Return a layout container (`Stack`,
`Dock`, or `Grid`) when the content should have multiple children or needs a
distinct layout or chrome node. The composition-root ownership role remains
reserved for the later role-migration phase.

## Composing vs. deriving from a primitive

Compose with `View` when the new type is an arrangement of existing controls.
Derive directly from `Control` for a new leaf, `Container` for a genuinely new
multi-child layout, or `Pressable` for a new interaction primitive that no
shipped control can express. That path participates directly in
`MeasureOverride`/`ArrangeOverride`/`OnRender` and takes on the box-model and
input responsibilities the
[control contract](../controls/control.md#control-contract) describes.
Externally derived primitives mutate through the protected extension kernel and
never call internal layout, focus, capture, or rendering transactions.

## Chrome and custom rendering

Border and shadow are intrinsic `Control` properties, not wrapper controls. Set
`BorderThickness`, `BorderGlyphs`, `HasShadow`, and the related style properties
on the component itself when it owns that chrome. When the frame needs distinct
bounds, margin, styling, routed ancestry, or disposal identity, return an
ordinary `Dock`, `Grid`, or `Stack` as the built content root and configure the
chrome on that node.

A primitive that fully overrides `OnRender` and opts into intrinsic chrome calls
`RenderChrome` before custom content. Its custom content uses `ContentBounds`;
the base layout pipeline has already reserved border and padding, so the
override must not repeat that deflation. `HasShadow = true` with the base
`(0,0)` `ShadowOffset` has no visible footprint; configure a non-zero offset
when the component needs a visible shadow.

`Screen : View` follows the same `Build()` contract and adds
`OnAttach`/`OnStarted`/`OnDispose` lifecycle hooks; see
[Screen](screen.md#screen-contract).

## Example

```csharp
public sealed class LoginPanel : View
{
    public LoginPanel()
    {
        BorderThickness = new Thickness(1);
        BorderGlyphs = Glyphs.Rounded;
        Padding = new Thickness(1);
        HasShadow = true;
        ShadowOffset = new Point(1, 1);
        ShadowAttributes = Attributes.Dim;
    }

    protected override Control Build() =>
        new Stack
        {
            Spacing = 1,
            Children =
            {
                new Text("Sign in"),
                new TextInput(),
                new Button { Content = new Text("Go") },
            },
        };
}
```

## Tests

Prove a successfully completed `Build()` runs exactly once regardless of attach
state, that a never-measured view never builds, that null and throwing attempts
propagate and retry on the next measure, that failed attempts leave the measure
phase re-invalidated, and that mutating the installed subtree behaves like any
other container mutation.
