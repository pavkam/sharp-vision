# FilePickerDialog

## FilePickerDialog contract

`SharpVision.Dialogs.FilePickerDialog` is a sealed
`FileDialogBase<FilePickerResult>` specialization that chooses existing local
files. It is one responsive
[`Window`](../controls/windows/window.md#window-contract) surface containing a
location bar, a scrolling `ListView`, a filter selector, a hidden-entry toggle,
status text, and Open and Cancel actions. Directories are navigation targets and
never appear in accepted results.

The picker does not create, rename, delete, save, or select directories. Use
[`SaveFileDialog`](save-file-dialog.md#savefiledialog-contract) when the user
must choose a path for a later write. A directory picker requires a separate
result and validation contract.

## API

### FilePickerFilter

`new FilePickerFilter(name, patterns)` snapshots one non-blank display name and
one or more non-blank basename patterns. `*` matches zero or more characters and
`?` matches one character. Matching is ordinal and case-insensitive on every
platform. Patterns apply only to file basenames; directories remain visible for
navigation.

Null or blank names, null or empty pattern collections, null or blank patterns,
rooted patterns, and patterns containing directory separators throw before the
filter is constructed. `FilePickerFilter.AllFiles` is the canonical `All files`
filter with pattern `*`.

### FilePickerOptions

| Property           | Default                                          | Validation and effect                                                 |
| ------------------ | ------------------------------------------------ | --------------------------------------------------------------------- |
| `Title`            | `Open File`                                      | Rejects null or blank values.                                         |
| `InitialDirectory` | Construction-time `Environment.CurrentDirectory` | Rejects null or blank values; dialog construction canonicalizes it.   |
| `AllowMultiple`    | `false`                                          | Selects single or multiple ListView semantics.                        |
| `ShowHidden`       | `false`                                          | Includes dot-prefixed and hidden-attribute entries initially.         |
| `MaxVisibleRows`   | `20`                                             | Rejects non-positive values; caps visible ListView content rows.      |
| `Filters`          | `[FilePickerFilter.AllFiles]`                    | Copies a non-null, non-empty list without null entries.               |
| `FilterIndex`      | `0`                                              | Must remain inside `Filters`; replacing Filters cannot invalidate it. |

Each setter validates before mutation. `FilePickerDialog` copies the complete
options value during construction, so later option changes cannot mutate a live
dialog.

### FilePickerResult

`Accepted` distinguishes Open from Cancel, Escape, or frame close. `Paths` is an
owned read-only snapshot of fully qualified canonical file paths in stable
display order. `SelectedPath` returns the first path or null. A cancelled result
has `Accepted == false`, an empty `Paths`, and a null `SelectedPath`.

### FilePickerDialog state

`CurrentDirectory` is the last successfully committed canonical directory.
`ShowHidden` and `FilterIndex` report current options. `SelectedPaths` contains
only selected file rows. `IsLoading` reports an outstanding enumeration, and
`Status` reports loading, counts, selection, or a recoverable error without
exposing private controls.

## Presentation and ownership

`FilePickerDialog.ShowAsync(owner, options, cancellationToken)` requires a
non-null, undisposed attached owner and dispatcher access. A container passed as
the owner is the explicit host; another owner uses its owning Screen's private
presentation slot. Outside a hosted Screen, the outermost container remains the
fallback host. A nested form layout is therefore not changed by hosted dialog
insertion. The helper attaches one temporary picker directly to that host,
presents the same object through
`ShowModal(OutsideInteraction.Ignore, initialFocus)`, and returns a
`Task<FilePickerResult>`.

Open completes with selected paths. Cancel, Escape, and the frame close target
return the semantic cancelled result. External cancellation cancels the Task.
Every completion path cancels pending enumeration, exits modality, restores
focus, removes the temporary picker, and disposes it exactly once. Attachment or
modal-entry failure rolls back the temporary ownership edge.

A directly constructed picker starts its initial load when attached. Detaching
or disposing it cancels the current generation. Reattachment starts a fresh load
from the last committed directory.

## Layout and appearance

The dialog is a direct Window child of the presentation Overlay. It requests 80%
width and 80% height and clamps its width to at most 96 columns. Its maximum
height is 19 chrome rows plus `MaxVisibleRows`; the default is therefore 39
rows. There is no hard minimum width or height: both dimensions remain
proportional below their caps. File dialogs use fixed dialog placement, so the
presentation Overlay centers them without title dragging. Window containment
keeps the complete border box inside the host after resize, including tiny
hosts.

One padded Grid uses one explicit star column so every row reaches the trailing
content edge, and owns five rows:

1. An exactly three-cell bordered parent-directory Button with `↑` in its middle
   cell, plus a star-sized editable directory path without visible scrollbars.
2. A star-sized bordered ListView with automatic vertical scrolling. It consumes
   available height until its content reaches `MaxVisibleRows`; the two border
   rows do not count toward that limit.
3. Ellipsized folder and file counts immediately below the ListView.
4. The Show hidden CheckBox above the filter.
5. A two-row footer with a borderless one-row filter ComboBox at the leading
   edge, followed by naturally sized Open and Cancel actions using the default
   Button appearance.

The Window supplies the outer frame. Dialog composition does not select a Button
kind or override action borders, faces, or shadows. Directory rows use a
single-cell `▸` prefix and trailing platform separator; file rows use a
single-cell `·` prefix. Filenames are markup-escaped and ellipsized. ListView
focus, current, selected, hovered, and disabled cells use shared semantic theme
roles; the picker assigns no RGB colors and emits no terminal bytes.

## Enumeration and ordering

Enumeration runs away from the UI dispatcher and returns only immediate
children. Each request owns a cancellation token and increasing generation.
Completion posts an immutable entry snapshot to the dispatcher; cancelled,
stale, detached, or disposed completions cannot mutate controls.

Every committed snapshot is ordered directories first, then names using
case-insensitive ordinal comparison with an ordinal tie-breaker. The rule is
applied at the dialog boundary as well as by the local filesystem provider, so
custom providers produce the same basic browsing order. Entries compare by
canonical path when a refresh replaces the snapshot; an existing selected file
therefore remains selected when it is still present, while filtered or removed
entries naturally leave the selection.

Directories sort first. Directories and files each sort by ordinal-ignore-case
name and then ordinal name as a deterministic tie-breaker. Filters remove only
files. An entry is hidden when its basename starts with `.` or its attributes
include `FileAttributes.Hidden`. Symbolic links follow their reported entry
attributes; enumeration never recurses, so link cycles cannot create traversal
cycles.

## Interaction

- Initial modality focuses the location input; the first successful load moves
  focus to the ListView when the location input still owns it.
- The bordered `↑` Button and Backspace from the ListView navigate to the
  parent. The Button disables at a root.
- Enter in the location input loads its canonical directory.
- Keyboard or primary-pointer invocation of a directory navigates into it.
- ListView selection follows existing single, Control-toggle, and Shift-range
  semantics. Directory rows may become current or selected visually, but
  `SelectedPaths` filters them out.
- Primary-pointer file invocation selects without dismissing. Enter on a file
  accepts the current file selection. Open is enabled only with selected files.
- Changing Filter or Show hidden starts a replacement load. Successful
  publication remaps selection by canonical path: entries still present remain
  selected, while filtered or removed entries leave the selection. Failure
  retains prior rows and selection.
- Tab and Shift+Tab remain inside the modal Window. Outside pointer and wheel
  input are consumed without background activation.

## Errors and threading

Invalid API arguments throw before mutation. Missing directories, access denial,
malformed paths, and enumeration I/O failures are recoverable during
interaction: `Status` receives concise error text while the last successful
directory, rows, and selection remain committed. Files disappearing after a
snapshot may still fail when the caller opens them; the result never claims a
file lease.

The attached tree, properties, selection, events, layout, rendering, and result
completion are dispatcher-affine. Filesystem work never calls controls.
Dispatcher callbacks run outside filesystem locks, and no picker callback emits
ANSI, CSI, OSC, or other terminal strings.

## Example

```csharp
using SharpVision.Dialogs;

var result = await FilePickerDialog.ShowAsync(
    openButton,
    new FilePickerOptions
    {
        Title = "Open sources",
        InitialDirectory = Environment.CurrentDirectory,
        AllowMultiple = true,
        MaxVisibleRows = 20,
        Filters =
        [
            new FilePickerFilter("C# source", "*.cs", "*.csx"),
            new FilePickerFilter("Documents", "*.md", "*.txt"),
            FilePickerFilter.AllFiles
        ]
    });

if (result.Accepted)
{
    OpenFiles(result.Paths);
}
```

## Expected behavior

Cover filter and option validation, copied ownership, result immutability,
canonical paths, deterministic ordering, filters, dot and attribute hidden
entries, missing and denied directories, synchronous and asynchronous failure,
cancellation, stale completions, detach, disposal, dispatcher affinity,
single/multiple selection, directory exclusion, parent/path/Backspace
navigation, keyboard and pointer invocation, Open/Cancel/Escape/frame close,
modal isolation, Tab confinement, focus restoration, host cleanup, tiny/normal/
wide resize, captured top-border dragging, default and configured visible-row
caps, exact compact-control geometry, glyph and semantic selected cells, real
temporary-directory integration, and the live showcase flow.
