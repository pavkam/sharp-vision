# Dialogs

## Dialog catalog

Dialogs compose ordinary retained controls into complete, temporary user tasks.
They live in `SharpVision.Dialogs`, outside the controls namespace, and follow
the same ownership, layout, styling, input, focus, dispatcher, and modality
contracts as the surfaces your application builds itself.

| Dialog                                             | Purpose                                                                      |
| -------------------------------------------------- | ---------------------------------------------------------------------------- |
| [FilePickerDialog](file-picker-dialog.md#overview) | Choose one or more existing files through filters and directory navigation.  |
| [SaveFileDialog](save-file-dialog.md#overview)     | Choose one canonical path for a later save, with optional overwrite consent. |
| [MessageBox](message-box.md#overview)              | Present a short modal decision and return a typed semantic result.           |

`Dialog<TResult>` derives directly from `Window`, so the dialog object you
construct is the same object that is retained, drawn, made modal, and disposed.
Each dialog's asynchronous helper publishes `Closing` and `Closed`, removes the
dialog from its presentation host, disposes it, and only then settles the result
task.

Dialog types defined outside this assembly reach this same lifecycle through a
`protected` `PresentAsync` overload:

```csharp
public sealed class ExampleDialog : Dialog<bool>
{
    public ExampleDialog()
        : base(cancelledResult: false)
    {
    }

    public Task<bool> ShowAsync(Control owner, Control? initialFocus, CancellationToken cancellationToken) =>
        PresentAsync(owner, initialFocus, cancellationToken);
}
```

It resolves the owner's presentation host, attaches the dialog, and presents it
— the internal presentation-host type never appears in the signature. A subclass
typically calls this from its own asynchronous factory method, after
constructing itself, mirroring how the built-in dialogs' own `ShowAsync` helpers
already resolve their host and roll back on failure. Calling `Window.ShowModal`
directly instead bypasses this typed result plumbing and the framework's
rollback and cancellation handling, and is not a supported alternative.

You can also construct a dialog and mount it yourself as a retained modeless
surface. In that mode a semantic action leaves the dialog mounted and raises
`ResultSelected` instead of closing it, and `HasSelectedResult` and
`SelectedResult` keep the latest selection. A modeless dialog leaves Escape to
its routed ancestors rather than silently consuming it as a cancellation result.
Dialogs do not introduce a second layout, input, or rendering framework.
