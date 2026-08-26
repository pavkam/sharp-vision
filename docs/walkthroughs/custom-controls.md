# Build a custom control

Custom controls are retained mutable objects. Do not introduce virtual trees,
function components, reconciliation, or hook-style state.

## Choose the right base type

| Requirement                                                                                                                    | Base type                                         | Ownership model                                                                                                      |
| ------------------------------------------------------------------------------------------------------------------------------ | ------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------- |
| Draw one custom cell surface                                                                                                   | `ControlBase`                                     | No public child                                                                                                      |
| Expose replaceable single content                                                                                              | `ContentControl`                                  | Caller owns zero or one content value                                                                                |
| Reuse press/capture activation, a text caption, a command, a value editor, or a popup - opting into only the capabilities used | [`InputBase`](../controls/input-base.md#overview) | Whatever ownership role the concrete control needs; `EnableCaption` gives a `Text`-only caption with no public child |
| Retain private implementation controls                                                                                         | `CompositeControlBase`                            | Constructor creates one permanent private root                                                                       |
| Expose typed semantic items                                                                                                    | `ItemsControl`                                    | Public items, private presentation controls                                                                          |
| Expose arbitrary public children                                                                                               | `Container`                                       | Ordered caller-managed child collection                                                                              |

The contracts for [`Container`](../controls/container.md#overview),
[`ContentControl`](../controls/content-control.md#overview),
[`CompositeControlBase`](../controls/composite-control.md#overview), and
[`ItemsControl`](../controls/items-control.md#overview) define the ownership
differences.

## Build a retained composite

```csharp
internal sealed class StatusCard : CompositeControlBase
{
    private readonly Text _message;

    public StatusCard()
    {
        _message = new Text("Waiting");

        InitializeContent(new GroupBox
        {
            HeaderText = "Status",
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

Derive directly from `ControlBase` and override `MeasureOverride`,
`ArrangeOverride`, or `OnRenderContent` only when the public component genuinely
owns new layout or cell-rendering behavior. Custom content rendering draws
through the frame-owned `SharpVision.Terminal.Rendering.TerminalCanvas` and
never writes terminal escape bytes. To place ordinary controls above that
drawing, compose the drawing control and those controls in an
[`Overlay`](../controls/layout/overlay.md#overview).

## Compose input capabilities

A value editor or popup-backed input derives from
[`InputBase`](../controls/input-base.md#overview) directly and calls only the
`Enable*` methods its own behavior needs. This external `RatingField` enables
press activation and a popup, but never touches segment editing - so it never
allocates that engine at all:

```csharp
public sealed class RatingField : InputBase
{
    private readonly ListView _choices;

    public RatingField()
    {
        _choices = new ListView
        {
            Items = ["★", "★★", "★★★", "★★★★", "★★★★★"],
            IsTabStop = false,
        };
        _choices.ItemInvoked += (_, e) =>
        {
            SelectedIndex = e.Index;
            IsOpen = false;
        };
        EnablePopup(_choices, focusOnOpen: false);
        EnablePressActivation();
    }

    public int SelectedIndex { get; private set; } = -1;

    protected override void Activate(ActivationCause cause) => IsOpen = !IsOpen;

    protected override void OnRenderContent(TerminalCanvas canvas)
    {
        var glyph = ResolveDropDownGlyph(new Rune('v'));
        canvas.DrawRune(glyph, new Point(ContentBounds.Right - DropDownIndicatorWidth, ContentBounds.Y), ResolvedStyle);
    }
}
```

`IsOpen` is already `public` on `InputBase`, so `RatingField` inherits its
open/close surface without declaring anything - the same inherited property
`ComboBox`, `DateInput`, and `DateTimeInput` expose. Calling `EnablePopup` a
second time, or reading `IsOpen` before `EnablePopup` ever runs, both throw
`InvalidOperationException` rather than silently no-op, so a capability mistake
fails where it happens instead of producing an inert control.

## Complete the component

A shipped control is complete only when its
[control specification](../controls/index.md#control-catalog), XML
documentation, behavioral tests, mounted rendering proof, and
[showcase page](../architecture/showcase.md#overview) agree. The
[custom-component contract](../concepts/custom-components.md#overview) defines
the full authoring and testing obligations.
