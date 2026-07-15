# Custom components

## Custom components contract

`View : Container` is the composition base for building a reusable component or
screen out of existing controls. Every shipped control other than `Stack` is
`sealed`, so `View` is the supported extension point for assembling controls
into a new type without editing framework layout code.

A concrete component derives from `View` and implements
`protected abstract Control Build()`, returning the single content root — a
layout container such as `Stack`, `Dock`, or `Grid` when the component presents
more than one child. The runtime installs that root as the view's only child
once, on the view's first measure, whether or not the view is attached to a
dispatcher at that point: building is attach-agnostic. A `View` that is never
measured never builds. A null return throws a documented
`InvalidOperationException`.

`Build()` runs once. It is one-shot construction, not reactive rendering: after
it returns, mutate the installed subtree like any other control tree (add or
remove children, set properties) instead of expecting `Build()` to run again.

A `View` owns exactly one child and stretches it to fill the view's content box,
like `Button`; return a layout container (`Stack`, `Dock`, or `Grid`) when the
content should have multiple children or should not simply fill.

## Composing vs. deriving from a primitive

Compose with `View` when the new type is an arrangement of existing controls.
Derive directly from the abstract `Container` or `Pressable` base only when
introducing a genuinely new layout or interaction primitive that no shipped
container can express; that path participates directly in
`MeasureOverride`/`ArrangeOverride`/`OnRender` and takes on the box-model and
input responsibilities the
[control contract](../controls/control.md#control-contract) describes.

`Screen : View` follows the same `Build()` contract and adds
`OnAttach`/`OnStarted`/`OnDispose` lifecycle hooks; see
[Screen](screen.md#screen-contract).

## Example

```csharp
public sealed class LoginPanel : View
{
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

Prove `Build()` runs exactly once regardless of attach state, that a
never-measured view never builds, that a null return throws
`InvalidOperationException`, that an exception from `Build()` leaves the measure
phase re-invalidated, and that mutating the installed subtree behaves like any
other container mutation.
