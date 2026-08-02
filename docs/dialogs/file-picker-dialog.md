# FilePickerDialog

## Overview

`SharpVision.Dialogs.FilePickerDialog` is a sealed
`FileDialogBase<FilePickerResult>` specialization for choosing existing local
files. It is a single responsive
[`Window`](../controls/windows/window.md#overview) surface that contains a
location bar, a scrolling `ListView`, a filter selector, a hidden-entry toggle,
status text, and Open and Cancel actions. Directories are navigation targets
only and never appear in accepted results.

The picker does not create, rename, delete, or save files, and it does not
select directories. When the user needs to choose a path for a later write, use
[`SaveFileDialog`](save-file-dialog.md#overview) instead. Picking a directory
would need its own result and validation contract, so there is no directory mode
here.

## API

### FilePickerFilter

`new FilePickerFilter(name, patterns)` takes one non-blank display name and one
or more non-blank basename patterns, and stores its own snapshot of both. In a
pattern, `*` matches zero or more characters and `?` matches exactly one.
Matching is ordinal and case-insensitive on every platform. Patterns apply only
to file basenames; directories always stay visible so the user can navigate.

The constructor throws before the filter is created when the name is null or
blank, the pattern collection is null or empty, any pattern is null or blank, a
pattern is rooted, or a pattern contains a directory separator.
`FilePickerFilter.AllFiles` is the canonical `All files` filter with the pattern
`*`.

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

Each setter validates its value before storing it. `FilePickerDialog` copies the
complete options value during construction, so changing an options object
afterwards never affects a dialog that is already showing.

| Style property            | Default | Applies to                                 |
| ------------------------- | ------- | ------------------------------------------ |
| `CancelButtonStyle`       | `null`  | The Cancel Button.                         |
| `ShowHiddenCheckBoxStyle` | `null`  | The Show hidden CheckBox.                  |
| `FileListScrollBarStyle`  | `null`  | The file ListView's generated scrollbars.  |
| `FilterScrollBarStyle`    | `null`  | The filter ComboBox's generated scrollbar. |
| `OpenButtonStyle`         | `null`  | The Open Button.                           |

`null` for a style property lets the corresponding owned part use its own
semantic input profile.

### FilePickerResult

`Accepted` tells you whether the user chose Open rather than Cancel, Escape, or
the frame close control. `Paths` is a read-only snapshot, owned by the result,
of fully qualified canonical file paths in stable display order. `SelectedPath`
returns the first path, or null when there is none. A cancelled result has
`Accepted == false`, an empty `Paths`, and a null `SelectedPath`.

### FilePickerDialog state

`CurrentDirectory` is the last directory that was successfully committed, as a
canonical path. `ShowHidden` and `FilterIndex` report the current options.
`SelectedPaths` contains only the selected file rows. `IsLoading` reports
whether an enumeration is still outstanding, and `Status` describes loading
progress, entry counts, the current selection, or a recoverable error without
exposing any private controls.

`CancelButtonStyle`/`ActualCancelButtonStyle`, `ShowHiddenCheckBoxStyle`/
`ActualShowHiddenCheckBoxStyle`, `FileListScrollBarStyle`/
`ActualFileListScrollBarStyle`, `FilterScrollBarStyle`/
`ActualFilterScrollBarStyle` (inherited from `FileDialogBase`), and
`OpenButtonStyle`/`ActualOpenButtonStyle` expose the complete local presentation
applied to each owned part, and the value each part actually resolves, without
exposing the owned control instances themselves. Each `Actual*` property reads
back the owned part's own resolved style — the picker performs no independent
Theme resolution of its own.

## Presentation and ownership

`FilePickerDialog.ShowAsync(owner, options, cancellationToken)` requires a
non-null, undisposed, attached owner and must be called on the owner's
dispatcher. Passing a container as the owner makes that container the explicit
host; any other owner uses its owning Screen's private presentation slot.
Outside a hosted Screen, the outermost container serves as the fallback host, so
inserting a hosted dialog never rearranges a nested form layout. The helper
attaches one temporary picker directly to that host, presents the same object
through `ShowModal(OutsideInteraction.Ignore, initialFocus)`, and returns a
`Task<FilePickerResult>`.

Open completes the task with the selected paths. Cancel, Escape, and the frame
close target complete it with the semantic cancelled result, and external
cancellation cancels the task itself. Whichever way the dialog completes, it
cancels any pending enumeration, exits modality, restores focus, removes the
temporary picker from its host, and disposes it exactly once. If attachment or
modal entry fails, the temporary ownership edge is rolled back.

A picker you construct directly starts its initial load when it is attached.
Detaching or disposing it cancels the current load generation, and reattaching
it starts a fresh load from the last committed directory.

## Layout and appearance

The dialog is a direct Window child of the presentation Overlay. It requests 80%
of the host's width and 80% of its height, and clamps its width to at most 96
columns. Its maximum height is 21 chrome rows plus `MaxVisibleRows`, which makes
the default maximum 41 rows. There is no hard minimum width or height: below the
caps, both dimensions stay proportional. File dialogs are movable, like the
sibling Save dialog, so a user can drag them by the title bar within the
presentation Overlay. Window containment keeps the complete border box inside
the host after a resize, including tiny hosts.

One padded Grid uses a single explicit star column, so every row reaches the
trailing content edge. The grid owns five rows:

1. A bordered parent-directory Button, exactly three cells wide with `↑` in its
   middle cell, next to a star-sized editable directory path without visible
   scrollbars.
2. A star-sized bordered ListView with automatic vertical scrolling. It grows
   into the available height until its content reaches `MaxVisibleRows`; the two
   border rows do not count toward that limit.
3. Ellipsized folder and file counts immediately below the ListView.
4. The Show hidden CheckBox, above the filter.
5. A two-row footer with a borderless one-row filter ComboBox at the leading
   edge, followed by naturally sized Open and Cancel actions in the default
   Button appearance.

The Window supplies the outer frame. Directory rows use a single-cell `▸` prefix
and a trailing platform separator, and file rows use a single-cell `·` prefix.
Filenames are markup-escaped and ellipsized. ListView focus, current, selected,
hovered, and disabled cells use the shared semantic theme roles; the picker
assigns no RGB colors and emits no terminal bytes.

## Enumeration and ordering

Enumeration runs away from the UI dispatcher and returns only the directory's
immediate children. Each request owns a cancellation token and an increasing
generation number. When a request completes, it posts an immutable entry
snapshot to the dispatcher; completions that are cancelled, stale, detached, or
disposed can no longer touch any control.

Every committed snapshot is ordered directories first, then by name using
case-insensitive ordinal comparison with an ordinal tie-breaker. The dialog
applies this rule at its own boundary in addition to the local filesystem
provider, so custom providers produce the same basic browsing order. When a
refresh replaces the snapshot, entries are compared by canonical path: a
selected file that is still present stays selected, while entries that were
filtered out or removed simply leave the selection.

Directories always sort before files. Within each group, entries sort by
ordinal-ignore-case name, then by ordinal name as a deterministic tie-breaker.
Filters remove only files. An entry counts as hidden when its basename starts
with `.` or its attributes include `FileAttributes.Hidden`. Symbolic links
follow their reported entry attributes, and because enumeration never recurses,
link cycles cannot create traversal cycles.

## Interaction

- When modality begins, focus starts in the location input. After the first
  successful load, focus moves to the ListView if the location input still owns
  it.
- The bordered `↑` Button, and Backspace pressed in the ListView, navigate to
  the parent directory. The Button disables at a root.
- Pressing Enter in the location input loads that text's canonical directory.
- Invoking a directory with the keyboard or the primary pointer navigates into
  it.
- ListView selection follows the existing single, Control-toggle, and
  Shift-range semantics. Directory rows can become current or selected visually,
  but `SelectedPaths` filters them out.
- Invoking a file accepts the current file selection, whether the invocation
  came from the pointer or from Enter. Open is enabled only while at least one
  file is selected.
- Changing the filter or the Show hidden toggle starts a replacement load. When
  it succeeds, the selection is remapped by canonical path: entries still
  present stay selected, while filtered or removed entries leave the selection.
  When it fails, the previous rows and selection stay in place.
- Tab and Shift+Tab stay inside the modal Window. Pointer and wheel input
  outside the dialog is consumed without activating the background.

## Errors and threading

Invalid API arguments throw before any state changes. Missing directories,
access denial, malformed paths, and enumeration I/O failures are recoverable
while the dialog is open: `Status` shows concise error text while the last
successful directory, rows, and selection stay committed. A file can still
disappear after a snapshot, so opening a returned path may fail; the result
never claims a lease on the file.

The attached tree, properties, selection, events, layout, rendering, and result
completion are all dispatcher-affine. Filesystem work never calls into controls,
dispatcher callbacks run outside filesystem locks, and no picker callback emits
ANSI, CSI, OSC, or any other terminal string.

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

The behavior above is verified end to end, so callers can rely on it:

- Filters and options validate their input, the dialog owns its copied options,
  results are immutable, and returned paths are canonical.
- Ordering is deterministic, filters apply as described, and both dot-prefixed
  and attribute-hidden entries follow the hidden-entry rule.
- Missing and denied directories, synchronous and asynchronous failures,
  cancellation, stale completions, detach, and disposal are all handled without
  violating dispatcher affinity.
- Single and multiple selection work as configured, directories never reach the
  result, and parent, path, and Backspace navigation plus keyboard and pointer
  invocation behave as described.
- Open, Cancel, Escape, and frame close each complete the dialog. Modality
  isolates the background, Tab stays confined, focus is restored, and the host
  is cleaned up.
- Layout holds across tiny, normal, and wide resizes and captured top-border
  dragging, with the default and configured visible-row caps and the exact
  compact-control geometry.
- Selected cells render with the documented glyphs and semantic roles, and the
  whole flow is exercised against a real temporary directory and the live
  showcase.
