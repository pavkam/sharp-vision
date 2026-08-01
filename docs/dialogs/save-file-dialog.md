# SaveFileDialog

## Overview

`SharpVision.Dialogs.SaveFileDialog` is a sealed
`FileDialogBase<SaveFileResult>` specialization that chooses one canonical file
path for a later save operation. It composes the shared directory browser with a
filename input, Save action, and optional overwrite confirmation. The dialog
does not create, truncate, lock, or write the selected file; the caller owns the
actual save after a confirmed result.

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

Each setter validates before mutation. `SaveFileDialog` copies the complete
options object during construction, including an owned filter snapshot, so later
option changes cannot mutate a live dialog.

> [!IMPORTANT] **Implementation gap:** `InitialFileName` is a non-null string by
> contract, but its option setter currently accepts a runtime null. Constructing
> `SaveFileDialog` then rejects that value through `TextInput.Text`, after the
> invalid options object has already been mutated, instead of rejecting it at
> the public setter boundary.

### SaveFileResult

`Confirmed` distinguishes Save from Cancel, Escape, frame close, or external
cancellation. A confirmed result owns one fully qualified canonical `Path`. The
shared `SaveFileResult.Cancelled` value has `Confirmed == false` and a null
`Path`.

### SaveFileDialog state

`FileName` reports the current filename input. `CurrentDirectory` is the last
successfully committed canonical directory. `ShowHidden` and `FilterIndex`
report the active browser options. `IsLoading` reports an outstanding directory
enumeration, and `Status` reports loading, counts, or a recoverable error
without exposing private controls.

## Presentation and ownership

`SaveFileDialog.ShowAsync(owner, options, cancellationToken)` requires a
non-null, undisposed attached owner and dispatcher access. A container passed as
the owner is the explicit host; another owner uses its owning Screen's private
presentation slot. Outside a hosted Screen, the outermost container remains the
fallback host.

The helper attaches one temporary dialog directly to that host, presents the
same object through `ShowModal(OutsideInteraction.Ignore, fileNameInput)`, and
returns a `Task<SaveFileResult>`. Save completes with a canonical path. Cancel,
Escape, and frame close return `SaveFileResult.Cancelled`; external cancellation
cancels the task. Every completion path exits modality, restores focus, removes
the temporary dialog, and disposes it exactly once. Attachment or modal-entry
failure rolls back the temporary ownership edge.

A directly constructed dialog starts its initial directory load when attached.
Detaching or disposing it cancels the active generation. Reattachment starts a
fresh load from the last committed directory.

## Layout and appearance

The dialog is one movable Window centered in its presentation Overlay. It
requests 80% width and height, clamps width to 96 columns, and caps height at 23
chrome rows plus `MaxVisibleRows`; the default maximum is 35 rows. Window
containment keeps the complete border box inside the host after resize.

One padded Grid owns six rows:

1. A three-cell parent-directory Button and stretch directory-path input.
2. A bordered, vertically scrolling single-selection ListView.
3. A `Name:` label and stretch filename input.
4. Ellipsized loading, count, or error status.
5. The Show hidden CheckBox.
6. A two-row footer with filter ComboBox, Save, and Cancel actions.

The Window supplies the outer frame. Save and Cancel retain the standard Button
appearance; the dialog assigns no RGB colors and emits no terminal bytes.
Directory rows use `▸` plus the platform separator, file rows use `·`, and names
are markup-escaped and ellipsized.

## Interaction

- Initial modality and the first successful load keep focus in the filename
  input.
- The parent Button and Backspace from the file list navigate upward. Enter in
  the directory input loads its canonical path.
- Invoking a directory navigates into it. Selecting a file copies its basename
  into `FileName`; selecting a directory does not.
- Pointer invocation of a file updates the filename without dismissing. Keyboard
  invocation updates the filename and attempts Save.
- Typing a non-blank filename enables Save. Enter in the filename input and the
  default Save button use the same completion path.
- The result path is `GetFullPath(Path.Combine(CurrentDirectory, trimmedName))`.
  Filters constrain the browser rows but do not rewrite or append the typed
  filename.
- When `ConfirmOverwrite` is true and that path exists, a nested Yes/No
  `MessageBox` asks before completion. No or dismissal leaves the save dialog
  open; Yes returns the path. When false, an existing path is returned directly.
- Filter and hidden-entry changes replace the asynchronous directory load. Tab
  and Shift+Tab remain inside the modal plane, and outside input is consumed.

## Errors and threading

Invalid public arguments throw before observable dialog mutation, except for the
documented `InitialFileName` setter gap. Invalid filename/path composition,
missing directories, access denial, and enumeration I/O failures are recoverable
during interaction: `Status` receives concise text while the last successful
directory and rows remain committed.

Confirming a path does not prove that the caller can later create or replace the
file. Filesystem races, permissions, sharing, and the final write remain caller
responsibilities. Enumeration runs away from the dispatcher and posts only the
newest immutable snapshot back to it. Attached properties, controls, focus,
layout, completion, and overwrite-confirmation continuation are dispatcher-
affine.

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

Cover option validation and copying, cancelled and confirmed result semantics,
canonical path composition, filename enablement and trimming, directory/file
selection, keyboard and pointer invocation, navigation, filtering, hidden
entries, asynchronous load replacement, missing and denied directories,
detach/disposal/cancellation, Save/Cancel/Escape/frame close, overwrite Yes/No
and disabled confirmation, nested modality, focus restoration, host cleanup,
tiny/normal/wide layout, semantic rendering, the live showcase flow, and a real
temporary-directory integration without writing the selected file.
