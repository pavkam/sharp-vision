# SaveFileDialog

## Overview

`SharpVision.Dialogs.SaveFileDialog` is a sealed
`FileDialogBase<SaveFileResult>` specialization for choosing one canonical file
path for a later save. It combines the shared directory browser with a filename
input, a Save action, and an optional overwrite confirmation. The dialog never
creates, truncates, locks, or writes the selected file; after a confirmed
result, the actual save belongs to the caller.

## API

### SaveFileOptions

| Property           | Default                                          | Validation and effect                                                 |
| ------------------ | ------------------------------------------------ | --------------------------------------------------------------------- |
| `Title`            | `Save As`                                        | Rejects null or blank values.                                         |
| `InitialDirectory` | Construction-time `Environment.CurrentDirectory` | Rejects null or blank values; dialog construction canonicalizes it.   |
| `InitialFileName`  | Empty string                                     | Supplies the initial filename and Save-button state.                  |
| `ConfirmOverwrite` | `true`                                           | Requires confirmation before returning an existing path.              |
| `ShowHidden`       | `false`                                          | Includes dot-prefixed and hidden-attribute entries initially.         |
| `MaxVisibleRows`   | `12`                                             | Rejects non-positive values; caps visible ListView content rows.      |
| `Filters`          | `[FilePickerFilter.AllFiles]`                    | Copies a non-null, non-empty list without null entries.               |
| `FilterIndex`      | `0`                                              | Must remain inside `Filters`; replacing Filters cannot invalidate it. |

Each setter validates its value before storing it. `SaveFileDialog` copies the
complete options object during construction, including an owned filter snapshot,
so changing an options object afterwards never affects a dialog that is already
showing.

| Style property            | Default | Applies to                                                                  |
| ------------------------- | ------- | --------------------------------------------------------------------------- |
| `CancelButtonStyle`       | `null`  | The Cancel Button.                                                          |
| `ShowHiddenCheckBoxStyle` | `null`  | The Show hidden CheckBox.                                                   |
| `FileListScrollBarStyle`  | `null`  | The file ListView's generated scrollbars.                                   |
| `FilterScrollBarStyle`    | `null`  | The filter ComboBox's generated scrollbar.                                  |
| `SaveButtonStyle`         | `null`  | The Save Button and the overwrite-confirmation MessageBox's Yes/No actions. |

`null` for a style property lets the corresponding owned part use its own
semantic input profile.

> [!IMPORTANT]
>
> **Implementation gap:** `InitialFileName` is a non-null string by contract,
> but its option setter currently accepts a runtime null. Constructing
> `SaveFileDialog` then rejects that value through `TextInput.Text`, after the
> invalid options object has already been mutated, instead of rejecting it at
> the public setter boundary.

### SaveFileResult

`Confirmed` tells you whether the user chose Save rather than Cancel, Escape,
frame close, or external cancellation. A confirmed result owns one fully
qualified canonical `Path`. The shared `SaveFileResult.Cancelled` value has
`Confirmed == false` and a null `Path`.

### SaveFileDialog state

`FileName` reports the current contents of the filename input.
`CurrentDirectory` is the last directory that was successfully committed, as a
canonical path. `ShowHidden` and `FilterIndex` report the active browser
options. `IsLoading` reports whether a directory enumeration is still
outstanding, and `Status` describes loading progress, entry counts, or a
recoverable error without exposing any private controls.

`CancelButtonStyle`/`ActualCancelButtonStyle`, `ShowHiddenCheckBoxStyle`/
`ActualShowHiddenCheckBoxStyle`, `FileListScrollBarStyle`/
`ActualFileListScrollBarStyle`, `FilterScrollBarStyle`/
`ActualFilterScrollBarStyle` (inherited from `FileDialogBase`), and
`SaveButtonStyle`/`ActualSaveButtonStyle` expose the complete local presentation
applied to each owned part, and the value each part actually resolves, without
exposing the owned control instances themselves. Since the
overwrite-confirmation `MessageBox` is constructed fresh and discarded per
confirmation rather than retained, it has no independent style surface of its
own — its Yes/No actions inherit `SaveButtonStyle` instead, on the reasoning
that "replace it" is the confirmation's affirmative, accept-like action.

## Presentation and ownership

`SaveFileDialog.ShowAsync(owner, options, cancellationToken)` requires a
non-null, undisposed, attached owner and must be called on the owner's
dispatcher. Passing a container as the owner makes that container the explicit
host; any other owner uses its owning Screen's private presentation slot.
Outside a hosted Screen, the outermost container serves as the fallback host.

The helper attaches one temporary dialog directly to that host, presents the
same object through `ShowModal(OutsideInteraction.Ignore, fileNameInput)`, and
returns a `Task<SaveFileResult>`. Save completes the task with a canonical path.
Cancel, Escape, and frame close complete it with `SaveFileResult.Cancelled`, and
external cancellation cancels the task itself. Whichever way the dialog
completes, it exits modality, restores focus, removes the temporary dialog from
its host, and disposes it exactly once. If attachment or modal entry fails, the
temporary ownership edge is rolled back.

A dialog you construct directly starts its initial directory load when it is
attached. Detaching or disposing it cancels the active load generation, and
reattaching it starts a fresh load from the last committed directory.

## Layout and appearance

The dialog is one movable Window centered in its presentation Overlay. It
requests 80% of the host's width and height, clamps its width to 96 columns, and
caps its height at 23 chrome rows plus `MaxVisibleRows`, which makes the default
maximum 35 rows. Window containment keeps the complete border box inside the
host after a resize.

One padded Grid owns six rows:

1. A three-cell parent-directory Button next to a stretching directory-path
   input.
2. A bordered, vertically scrolling single-selection ListView.
3. A `Name:` label next to a stretching filename input.
4. Ellipsized loading, count, or error status.
5. The Show hidden CheckBox.
6. A two-row footer with the filter ComboBox and the Save and Cancel actions.

The Window supplies the outer frame, and the dialog assigns no RGB colors and
emits no terminal bytes on its own. Directory rows use `▸` plus the platform
separator, file rows use `·`, and names are markup-escaped and ellipsized.

## Interaction

- When modality begins, focus starts in the filename input, and the first
  successful load leaves it there.
- The parent Button, and Backspace pressed in the file list, navigate to the
  parent directory. Pressing Enter in the directory input loads that text's
  canonical path.
- Invoking a directory navigates into it. Selecting a file copies its basename
  into `FileName`; selecting a directory does not.
- Invoking a file updates the filename and then attempts the Save, whether the
  invocation came from the pointer or from the keyboard.
- Typing a non-blank filename enables Save. Pressing Enter in the filename input
  and activating the default Save button run the same completion path.
- The result path is `GetFullPath(Path.Combine(CurrentDirectory, trimmedName))`.
  Filters constrain which rows the browser shows; they never rewrite or append
  to the typed filename.
- When `ConfirmOverwrite` is true and that path already exists, a nested Yes/No
  `MessageBox` asks before completing. Choosing No or dismissing the box leaves
  the save dialog open; choosing Yes returns the path. When `ConfirmOverwrite`
  is false, an existing path is returned directly.
- Changing the filter or the hidden-entry toggle replaces the asynchronous
  directory load. Tab and Shift+Tab stay inside the modal plane, and input
  outside the dialog is consumed.

## Errors and threading

Invalid public arguments throw before the dialog changes observably, except for
the documented `InitialFileName` setter gap. Invalid filename or path
composition, missing directories, access denial, and enumeration I/O failures
are recoverable while the dialog is open: `Status` shows concise text while the
last successful directory and rows stay committed.

A confirmed path does not prove that the caller can later create or replace the
file. Filesystem races, permissions, sharing, and the final write remain the
caller's responsibility. Enumeration runs away from the dispatcher and posts
only the newest immutable snapshot back to it. Attached properties, controls,
focus, layout, completion, and the overwrite-confirmation continuation are
dispatcher-affine.

## Example

```csharp
using SharpVision.Dialogs;

var result = await SaveFileDialog.ShowAsync(
    saveButton,
    new SaveFileOptions
    {
        Title = "Save report",
        InitialDirectory = Environment.CurrentDirectory,
        InitialFileName = "report.csv",
        ConfirmOverwrite = true,
        Filters =
        [
            new FilePickerFilter("CSV files", "*.csv"),
            FilePickerFilter.AllFiles
        ]
    });

if (result.Path is { } path)
{
    await File.WriteAllTextAsync(path, reportText);
}
```

## Expected behavior

The behavior above is verified end to end, so callers can rely on it:

- Options validate and are copied, cancelled and confirmed results keep their
  semantics, and the returned path is composed canonically.
- Filename enablement and trimming, directory and file selection, keyboard and
  pointer invocation, navigation, filtering, and hidden entries behave as
  described above.
- Asynchronous loads replace each other cleanly, missing and denied directories
  stay recoverable, and detach, disposal, and cancellation tear the dialog down
  safely.
- Save, Cancel, Escape, and frame close each complete the dialog, and overwrite
  confirmation honors Yes, No, and the disabled setting through nested modality.
- Focus is restored and the host is cleaned up on every completion path.
- Layout holds across tiny, normal, and wide hosts with semantic rendering, and
  the whole flow is exercised in the live showcase and against a real temporary
  directory without writing the selected file.
