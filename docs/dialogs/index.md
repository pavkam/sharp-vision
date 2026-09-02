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
Each dialog's asynchronous helper publishes `Closing`, removes the dialog from
its presentation host, publishes `Closed`, disposes it, and only then settles
the result task. Inherited `FadeInDuration` and `FadeOutDuration` default to
zero. With a positive fade-out, the selected result is latched but the dialog
remains attached, modal, focused, undisposed, and task-pending until shared
`FadeProgress` reaches zero:

```mermaid
sequenceDiagram
    participant Caller
    participant Dialog
    participant FloatingSurfaceBase
    participant PresentationHost
    participant Awaiter
    Caller->>Dialog: Complete(result) / Cancel()
    Dialog->>FloatingSurfaceBase: CloseSurface(...)
    FloatingSurfaceBase->>FloatingSurfaceBase: RaiseSurfaceClosing() [Closing]
    opt positive FadeOutDuration
        FloatingSurfaceBase->>FloatingSurfaceBase: Consume input while FadeProgress decreases
    end
    FloatingSurfaceBase->>PresentationHost: Remove(dialog)
    FloatingSurfaceBase->>FloatingSurfaceBase: RaiseSurfaceClosed() [Closed]
    FloatingSurfaceBase-->>Dialog: closure completed
    Dialog->>Dialog: Dispose()
    Dialog->>Awaiter: TrySetResult(result) / TrySetCanceled()
```

Built-in dialogs also share the base action-bar composition: a horizontal
separator sits directly above a shadow-aware action host, while each concrete
dialog retains control of its own Button alignment and semantics.

Dialog types defined outside this assembly reach this same lifecycle through a
`protected` `PresentAsync` overload:

```csharp
public sealed class ExampleDialog : Dialog<bool>
{
    public ExampleDialog()
        : base(cancelledResult: false)
    {
    }

    public Task<bool> ShowAsync(ControlBase owner, ControlBase? initialFocus, CancellationToken cancellationToken) =>
        PresentAsync(owner, initialFocus, cancellationToken);
}
```

It resolves the owner's presentation host, attaches the dialog, and presents it
— the internal presentation-host type never appears in the signature. A subclass
typically calls this from its own asynchronous factory method, after
constructing itself. Every built-in `ShowAsync` helper calls this same
owner-facing transaction, so owner validation, host resolution, attachment,
synchronous rollback, disposal, and the one-shot guard cannot drift by dialog
family. Calling `Window.ShowModal` directly instead bypasses this typed result
plumbing and the framework's rollback and cancellation handling, and is not a
supported alternative.

> [!WARNING]
>
> Nothing prevents that call, and the failure mode is severe: a dialog shown
> through `Window.ShowModal` never receives its completion plumbing, so every
> later `Complete` or `Cancel` only raises `ResultSelected` — the modal scope is
> never released, the surface stays mounted and modal indefinitely, and no task
> ever settles. Always present a dialog through its `ShowAsync` helper or
> `PresentAsync`.

You can also construct a dialog and mount it yourself as a retained modeless
surface. In that mode a semantic action leaves the dialog mounted and raises
`ResultSelected` instead of closing it, and `HasSelectedResult` and
`SelectedResult` keep the latest selection. Result publication is versioned: if
a property observer synchronously selects a newer result, only that current
transition reaches `ResultSelected`; the superseded outer publication stops.
Unmodified Escape commits the dialog's typed cancellation result, marks the key
handled, and leaves the modeless surface mounted; command-modified Escape
remains available to routed ancestors. Dialogs do not introduce a second layout,
input, or rendering framework.

A presented dialog detached while completion is queued or visually exiting
cannot attach to another dispatcher until the original completion transaction
finishes. Attachment validation rejects that ownership change before the new
tree mutates; the old presentation then settles and disposes on its original
dispatcher. Direct hide, structural detachment, or disposal cancels an active
fade and resolves the retained completion coherently, without inventing another
`Closing` or `Closed` event.
