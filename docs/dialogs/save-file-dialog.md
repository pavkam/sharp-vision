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
| `InitialFileName`  | Empty string                                     | Rejects null; supplies the initial filename and Save-button state.    |
| `ConfirmOverwrite` | `true`                                           | Requires confirmation before returning an existing path.              |
| `ShowHidden`       | `false`                                          | Includes dot-prefixed and hidden-attribute entries initially.         |
| `MaxVisibleRows`   | `12`                                             | Rejects non-positive values; caps visible ListView content rows.      |
| `Filters`          | `[FilePickerFilter.AllFiles]`                    | Copies a non-null, non-empty list without null entries.               |
| `FilterIndex`      | `0`                                              | Must remain inside `Filters`; replacing Filters cannot invalidate it. |

Each setter validates its value before storing it. `SaveFileDialog` copies the
complete options object during construction, including an owned filter snapshot,
so changing an options object afterwards never affects a dialog that is already
showing.

| Style property            | Default | Applies to                                                                                                                              |
| ------------------------- | ------- | --------------------------------------------------------------------------------------------------------------------------------------- |
| `CancelButtonStyle`       | `null`  | The Cancel Button.                                                                                                                      |
| `ShowHiddenCheckBoxStyle` | `null`  | The Show hidden CheckBox.                                                                                                               |
| `FileListScrollBarStyle`  | `null`  | The file ListView's generated scrollbars.                                                                                               |
| `FilterScrollBarStyle`    | `null`  | The filter ComboBox's generated scrollbar.                                                                                              |
| `SaveButtonStyle`         | `null`  | The Save Button, and the overwrite-confirmation MessageBox's action Buttons unless `OverwriteStyle` also carries its own `ButtonStyle`. |
| `Style`                   | `null`  | The dialog's own frame and structural geometry (see [Theming](#theming)).                                                               |
| `OverwriteStyle`          | `null`  | The overwrite-confirmation MessageBox's frame, message face, and geometry.                                                              |

`null` for a style property lets the corresponding owned part use its own
semantic input profile.

| Text property          | Default             |
| ---------------------- | ------------------- |
| `ParentDirectoryText`  | `"↑"`               |
| `DirectoryPlaceholder` | `"Directory path"`  |
| `ShowHiddenText`       | `"Show &hidden"`    |
| `CancelText`           | `"&Cancel"`         |
| `SaveText`             | `"&Save"`           |
| `FileNameLabel`        | `"Name:"`           |
| `FileNamePlaceholder`  | `"File name"`       |
| `OverwriteTitle`       | `"Confirm Save As"` |
| `OverwriteYesText`     | `"&Yes"`            |
| `OverwriteNoText`      | `"&No"`             |

`OverwriteMessageFormat` (`Func<string, string>`, default builds
`'{name}' already exists.\nDo you want to replace it?`) formats the confirmation
message from the existing file's display name, supplied structurally rather than
through unchecked caller `string.Format` composition. Every text setter rejects
null. `SaveFileDialog` copies these values the same way it copies every other
option (see [Text and localization](#text-and-localization)).

### SaveFileResult

`IsConfirmed` tells you whether the user chose Save rather than Cancel, Escape,
frame close, or external cancellation. A confirmed result owns one fully
qualified canonical `Path`. The shared `SaveFileResult.Cancelled` value has
`IsConfirmed == false` and a null `Path`.

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
exposing the owned control instances themselves. `Style`/`ActualStyle` own the
dialog's own frame and structural geometry (see [Theming](#theming)). Since the
overwrite-confirmation `MessageBox` is constructed fresh and discarded per
confirmation rather than retained, it is presented through
[`MessageBoxOptions`](message-box.md#theming) built from `OverwriteStyle`,
`OverwriteTitle`, `OverwriteYesText`, `OverwriteNoText`, and `SaveButtonStyle`
(forwarded as the options' `ButtonStyle`, since "replace it" is the
confirmation's affirmative, accept-like action) — see
[Text and localization](#text-and-localization).

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
caps its height at 21 chrome rows plus `MaxVisibleRows`, which makes the default
maximum 33 rows. Window containment keeps the complete border box inside the
host after a resize.

One padded Grid owns six rows:

1. A five-cell parent-directory Button whose themed padding leaves `↑` visible,
   next to a stretching directory-path input.
2. A bordered, vertically scrolling single-selection ListView.
3. A `Name:` label next to a stretching filename input.
4. A full-width metadata row split equally between the filter ComboBox on the
   leading half and ellipsized loading, count, or error status aligned to the
   trailing edge of the other half.
5. The Show hidden CheckBox.
6. A full-width horizontal Separator directly above trailing Save and Cancel
   actions. Their action host is flush with the content bottom without shadows
   and reserves only the resolved downward shadow rows when either style enables
   one.

The Window supplies the outer frame, and the dialog assigns no RGB colors and
emits no terminal bytes on its own. Directory rows use `▸` plus the platform
separator, file rows use `·`, and names are markup-escaped and ellipsized.

## Interaction

- When modality begins, focus starts in the filename input, and the first
  successful load leaves it there.
- The parent Button, and Backspace pressed in the file list, navigate to the
  parent directory. Pressing Enter in the directory input loads that text's
  canonical path.
- The file list follows select-then-commit: a single primary pointer click on
  any row - file or directory - only selects it. Committing a row takes Enter,
  the Save Button, or a second pointer click (a double-click). Committing a
  directory navigates into it. Selecting a file copies its basename into
  `FileName`; selecting a directory does not.
- Committing a file updates the filename and then attempts the Save, whether the
  commit came from Enter, a double-click, or the Save Button.
- Typing a non-blank filename enables Save. Pressing Enter in the filename input
  and activating the default Save button run the same completion path.
- The result path is `GetFullPath(Path.Combine(CurrentDirectory, trimmedName))`.
  Filters constrain which rows the browser shows; they never rewrite or append
  to the typed filename.
- When `ConfirmOverwrite` is true and that path already exists, a nested Yes/No
  `MessageBox` — configured through `OverwriteTitle`, `OverwriteMessageFormat`,
  `OverwriteYesText`, `OverwriteNoText`, and `OverwriteStyle` — asks before
  completing. Choosing No or dismissing the box leaves the save dialog open;
  choosing Yes returns the path. When `ConfirmOverwrite` is false, an existing
  path is returned directly.
- Changing the filter or the hidden-entry toggle replaces the asynchronous
  directory load. Tab and Shift+Tab stay inside the modal plane, and input
  outside the dialog is consumed.

## Errors and threading

Invalid public arguments throw before the dialog changes observably. Invalid
filename or path composition, missing directories, access denial, and
enumeration I/O failures are recoverable while the dialog is open: `Status`
shows concise text while the last successful directory and rows stay committed.

A confirmed path does not prove that the caller can later create or replace the
file. Filesystem races, permissions, sharing, and the final write remain the
caller's responsibility. Enumeration runs away from the dispatcher and posts
only the newest immutable snapshot back to it. Attached properties, controls,
focus, layout, completion, and the overwrite-confirmation continuation are
dispatcher-affine.

## Theming

`SaveFileDialogStyle` (`sealed record SaveFileDialogStyle : FileDialogStyle`) is
the dialog's own complete aggregate: `Face`/`Border`/`Shadow` (falling back to
the Window role's own semantic appearance), `RootPadding`, `ContentSpacing`, and
`FileListBorder`. A Theme authors it through its own `saveFileDialog` style
section, resolved with the standard local → Theme → fallback precedence.
`Style`/`ActualStyle` follow the same contract as every other themed control; a
live Theme swap updates the frame and structural geometry together on the next
layout pass, even without a local `Style`.

`CloseGlyph`, `CloseLeftBracket`, `CloseRightBracket`, and the four
`CloseMarkColor`/`CloseMarkActiveColor`/`CloseMarkPressedColor`/`CloseMarkDisabledColor`
fields are inherited from `WindowStyle` through `FileDialogStyle`, and resolve
through `SaveFileDialogStyle` itself, so a theme's `saveFileDialog` section —
not its `window` section — drives the close mark this dialog renders.

**Precedence with the owned-part styles above**: `Style` owns the frame and
structural geometry only. Every named part-style property (`CancelButtonStyle`,
`ShowHiddenCheckBoxStyle`, `FileListScrollBarStyle`, `FilterScrollBarStyle`,
`SaveButtonStyle`) remains the sole authority for its own control, resolved
independently through that control's own Theme key. `OverwriteStyle` is a
distinct `MessageBoxStyle?` applied only to the overwrite-confirmation
MessageBox (see [MessageBox theming](message-box.md#theming)); it does not
affect the save dialog's own frame.

## Text and localization

`ParentDirectoryText`, `DirectoryPlaceholder`, `ShowHiddenText`, and
`CancelText` (inherited from `FileDialogBase`), plus `SaveText`,
`FileNameLabel`, and `FileNamePlaceholder`, replace their retained control's
caption, label, or placeholder in place — no control is ever recreated.
`OverwriteTitle`, `OverwriteMessageFormat`, `OverwriteYesText`, and
`OverwriteNoText` configure the nested confirmation MessageBox through
[`MessageBoxOptions`](message-box.md#api) each time it is shown; `ReadyText` and
`LoadingText` (inherited) set the idle and in-flight status wording. Every
setter validates non-null before mutating, and `SaveFileOptions` carries the
same values through its `Copy()` snapshot for `ShowAsync`.

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
- Filename enablement and trimming, directory and file selection,
  select-then-commit keyboard and pointer invocation, navigation, filtering, and
  hidden entries behave as described above.
- Asynchronous loads replace each other cleanly, missing and denied directories
  stay recoverable, and detach, disposal, and cancellation tear the dialog down
  safely.
- Save, Cancel, Escape, and frame close each complete the dialog, and overwrite
  confirmation honors Yes, No, and the disabled setting through nested modality.
- Focus is restored and the host is cleaned up on every completion path.
- Layout holds across tiny, normal, and wide hosts with semantic rendering, and
  the whole flow is exercised in the live showcase and against a real temporary
  directory without writing the selected file.
- `Style` resolves through local → Theme `saveFileDialog` section → Window
  fallback, updates the frame and structural geometry coherently (including
  after a live Theme swap), and composes with every owned-part style property.
- Every text property updates its retained control in place, is validated before
  mutation, and is carried through `SaveFileOptions.Copy()` to `ShowAsync`; the
  overwrite confirmation uses the configured title, message formatter, captions,
  and style.
