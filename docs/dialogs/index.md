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
dialog from its presentation host, disposes it, and only then settles the
result task.

You can also construct a dialog and mount it yourself as a retained modeless
surface. In that mode a semantic action leaves the dialog mounted and raises
`ResultSelected` instead of closing it, and `HasSelectedResult` and
`SelectedResult` keep the latest selection. A modeless dialog leaves Escape to
its routed ancestors rather than silently consuming it as a cancellation
result. Dialogs do not introduce a second layout, input, or rendering
framework.
