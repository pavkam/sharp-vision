# Dialogs

## Dialog catalog

Dialogs compose ordinary retained controls into complete, temporary user tasks.
They live in `SharpVision.Dialogs`, outside the controls namespace, and use the
same ownership, layout, styling, input, focus, dispatcher, and modality
contracts as application-authored surfaces.

| Dialog                                                              | Purpose                                                                      |
| ------------------------------------------------------------------- | ---------------------------------------------------------------------------- |
| [FilePickerDialog](file-picker-dialog.md#filepickerdialog-contract) | Choose one or more existing files through filters and directory navigation.  |
| [SaveFileDialog](save-file-dialog.md#savefiledialog-contract)       | Choose one canonical path for a later save, with optional overwrite consent. |
| [MessageBox](message-box.md#messagebox-contract)                    | Present a short modal decision and return a typed semantic result.           |

`Dialog<TResult>` derives directly from `Window`; the dialog object is the one
retained, drawn, modal, and disposed surface identity. An asynchronous helper
publishes `Closing` and `Closed`, removes that identity from its presentation
host, disposes it, and only then settles the result task.

Applications may also construct a dialog for retained modeless composition. A
semantic action then leaves the dialog mounted and publishes `ResultSelected`;
`HasSelectedResult` and `SelectedResult` retain the latest selection. Modeless
Escape remains available to routed ancestors instead of silently consuming a
cancellation result. Dialogs do not introduce a second layout, input, or
rendering framework.
