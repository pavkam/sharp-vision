# Build a custom control

Custom controls are retained mutable objects. Do not introduce virtual trees,
function components, reconciliation, or hook-style state.

## Choose the right base type

| Requirement                            | Base type          | Ownership model                                   |
| -------------------------------------- | ------------------ | ------------------------------------------------- |
| Draw one custom cell surface           | `Control`          | No public child                                   |
| Expose replaceable single content      | `ContentControl`   | Caller owns zero or one content value             |
| Reuse press/capture activation         | `Pressable`        | Replaceable content plus activation state machine |
| Retain private implementation controls | `CompositeControl` | Constructor creates one permanent private root    |
| Expose typed semantic items            | `ItemsControl`     | Public items, private presentation controls       |
| Expose arbitrary public children       | `Container`        | Ordered caller-managed child collection           |

The contracts for [`Container`](../controls/container.md#overview),
[`ContentControl`](../controls/content-control.md#overview),
[`CompositeControl`](../controls/composite-control.md#overview), and
[`ItemsControl`](../controls/items-control.md#overview) define the ownership
differences.

## Build a retained composite

```csharp
internal sealed class StatusCard : CompositeControl
{
    private readonly Text _message;

    public StatusCard()
    {
        _message = new Text("Waiting");

        InitializeContent(new GroupBox
        {
            Header = "Status",
            Content = new Stack
            {
                Spacing = 1,
                Children =
                {
                    _message,
                    new ProgressBar
                    {
                        Minimum = 0,
                        Maximum = 100,
                        Value = 0,
                    },
                },
            },
        });
    }

    public string Message
    {
        get => _message.Content;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _message.Content = value;
        }
    }
}
```

The constructor creates the complete retained subtree and calls
`InitializeContent` once. Callers see `StatusCard`, never its private
`GroupBox`, `Stack`, or `Text`. A substantial public property validates before
mutating and must stay dispatcher-affine while attached; a production property
should also publish the component's documented property-change contract.

Derive directly from `Control` and override `MeasureOverride`,
`ArrangeOverride`, or `OnRenderContent` only when the public component
genuinely owns new layout or cell-rendering behavior. Custom content rendering
draws through the frame-owned `SharpVision.Terminal.Rendering.Canvas` and
never writes terminal escape bytes. To place ordinary controls above that
drawing, compose the drawing control and those controls in an
[`Overlay`](../controls/layout/overlay.md#overview).

## Complete the component

A shipped control is complete only when its
[control specification](../controls/index.md#control-catalog), XML
documentation, behavioral tests, mounted rendering proof, and
[showcase page](../architecture/showcase.md#overview) agree. The
[custom-component contract](../concepts/custom-components.md#overview) defines
the full authoring and testing obligations.
