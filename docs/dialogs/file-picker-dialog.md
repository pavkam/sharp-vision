# FilePickerDialog

## Overview

`SharpVision.Dialogs.FilePickerDialog` is a sealed
`FileDialogBase<FilePickerResult>` specialization for choosing existing local
files or directories. It is a single responsive
[`Window`](../controls/windows/window.md#overview) surface that contains a
location bar, a scrolling `ListView`, a filter selector, a hidden-entry toggle,
status text, and Open and Cancel actions. A directory is always a
double-click/Enter navigation target; whether it can also be part of the
accepted result is controlled by `FilePickerOptions.SelectionMode` (see
[FilePickerOptions](#filepickeroptions) and
[Text and localization](#text-and-localization)).

The picker does not create, rename, delete, or save files. When the user needs
to choose a path for a later write, use
[`SaveFileDialog`](save-file-dialog.md#overview) instead — `SaveFileDialog`
still rejects a directory target regardless of `FilePickerDialog`'s selection
mode.

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
| `SelectionMode`    | `FileSelectionMode.Files`                        | Which entry kinds the accepted selection may contain (see below).     |
| `ShowHidden`       | `false`                                          | Includes dot-prefixed and hidden-attribute entries initially.         |
| `MaxVisibleRows`   | `20`                                             | Rejects non-positive values; caps visible ListView content rows.      |
| `Filters`          | `[FilePickerFilter.AllFiles]`                    | Copies a non-null, non-empty list without null entries.               |
| `FilterIndex`      | `0`                                              | Must remain inside `Filters`; replacing Filters cannot invalidate it. |

Each setter validates its value before storing it. `FilePickerDialog` copies the
complete options value during construction, so changing an options object
afterwards never affects a dialog that is already showing.

`FileSelectionMode` has three members: `Files` (the default - a directory is
never part of the accepted selection), `Directories` (a file is never part of
the accepted selection), and `FilesAndDirectories` (either kind can be
accepted). A `Filter` still applies only to file basenames in every mode -
directories always stay visible so the user can navigate into them regardless
of `SelectionMode`. Navigation-on-invoke (double-click or Enter on a directory)
is unaffected by `SelectionMode`; it always navigates, in every mode - only a
file commits on double-click or Enter. What `SelectionMode` changes is which
rows count toward the accepted selection the Open Button commits: with
`Directories` or `FilesAndDirectories`, a selected directory row enables Open
and is included in the accepted `FilePickerResult` alongside any selected files.

| Style property            | Default | Applies to                                                                |
| ------------------------- | ------- | ------------------------------------------------------------------------- |
| `CancelButtonStyle`       | `null`  | The Cancel Button.                                                        |
| `ShowHiddenCheckBoxStyle` | `null`  | The Show hidden CheckBox.                                                 |
| `FileListScrollBarStyle`  | `null`  | The file ListView's generated scrollbars.                                 |
| `FilterScrollBarStyle`    | `null`  | The filter ComboBox's generated scrollbar.                                |
| `OpenButtonStyle`         | `null`  | The Open Button.                                                          |
| `Style`                   | `null`  | The dialog's own frame and structural geometry (see [Theming](#theming)). |

`null` for a style property lets the corresponding owned part use its own
semantic input profile.

| Text property          | Default            |
| ---------------------- | ------------------ |
| `ParentDirectoryText`  | `"↑"`              |
| `DirectoryPlaceholder` | `"Directory path"` |
| `ShowHiddenText`       | `"Show &hidden"`   |
| `CancelText`           | `"&Cancel"`        |
| `OpenText`             | `"&Open"`          |

Each setter rejects null. `FilePickerDialog` copies these values the same way it
copies every other option (see [Text and localization](#text-and-localization)).

### FilePickerResult

`IsAccepted` tells you whether the user chose Open rather than Cancel, Escape,
or the frame close control. `Paths` is a read-only snapshot, owned by the
result, of fully qualified canonical paths in stable display order.
`SelectedPath` returns the first path, or null when there is none. `Entries` is
a parallel read-only snapshot of `FilePickerResultEntry` (`Path`, `IsDirectory`)
in the same order as `Paths`, so a caller of a `Directories` or
`FilesAndDirectories` picker can tell which accepted paths are directories
without probing the filesystem again; every entry's `IsDirectory` is `false`
for the default `Files` mode. A cancelled result has `IsAccepted == false`, an
empty `Paths` and `Entries`, and a null `SelectedPath`.

### FilePickerDialog state

`CurrentDirectory` is the last directory that was successfully committed, as a
canonical path. `ShowHidden` and `FilterIndex` report the current options.
`SelectedPaths` contains the selected rows that `SelectionMode` accepts - only
files by default, only directories or both when a directory-aware mode is
active. `IsLoading` reports whether an enumeration is still outstanding, and
`Status` describes loading progress, entry counts, the current selection, or a
recoverable error without exposing any private controls.

`CancelButtonStyle`/`ActualCancelButtonStyle`, `ShowHiddenCheckBoxStyle`/
`ActualShowHiddenCheckBoxStyle`, `FileListScrollBarStyle`/
`ActualFileListScrollBarStyle`, `FilterScrollBarStyle`/
`ActualFilterScrollBarStyle` (inherited from `FileDialogBase`), and
`OpenButtonStyle`/`ActualOpenButtonStyle` expose the complete local presentation
applied to each owned part, and the value each part actually resolves, without
exposing the owned control instances themselves. `Style`/`ActualStyle` own the
dialog's own frame and structural geometry (see [Theming](#theming)).

## Theming

`FilePickerDialogStyle`
(`sealed record FilePickerDialogStyle : FileDialogStyle`) is the dialog's own
complete aggregate: `Face`/`Border`/`Shadow` (falling back to the Window role's
own semantic appearance, including its ActiveBorder-on-FocusWithin default),
`RootPadding`, `ContentSpacing`, and `FileListBorder`. `FilePickerDialogStyle`
declares no `styles.*` theme key of its own: the frame follows `window`'s role
section with the standard local → fallback precedence, while
`RootPadding`/`ContentSpacing`/`FileListBorder` stay code-owned, reachable only
through a locally assigned `Style`. `Style`/`ActualStyle` follow the same
contract as every other themed control; a live Theme swap still updates the
frame on the next layout pass, even without a local `Style`.

`CloseGlyph`, `CloseLeftBracket`, `CloseRightBracket`, and the four
`CloseMarkColor`/`CloseMarkActiveColor`/`CloseMarkPressedColor`/`CloseMarkDisabledColor`
fields are inherited from `WindowStyle` through `FileDialogStyle`, and resolve
through `FilePickerDialogStyle` itself, copied verbatim from the fallback's own
resolved `window` role section - `FilePickerDialogStyle` declares no `styles.*`
theme key of its own, so a theme's `window` section drives the close mark this
dialog renders, and only a locally assigned `Style` can give it a close mark
independent of `window`.

**Precedence with the owned-part styles above**: `Style` owns the frame and
structural geometry only. Every named part-style property (`CancelButtonStyle`,
`ShowHiddenCheckBoxStyle`, `FileListScrollBarStyle`, `FilterScrollBarStyle`,
`OpenButtonStyle`) remains the sole authority for its own control, resolved
independently through that control's own declared fallback, exactly as before
this aggregate existed. The two mechanisms compose rather than compete.

## Text and localization

`ParentDirectoryText`, `DirectoryPlaceholder`, `ShowHiddenText`, and
`CancelText` (inherited from `FileDialogBase`), plus `OpenText`, replace their
retained control's caption or placeholder in place — no control is ever
recreated. `CountFormat` (`Func<int, int, string>`, folder count then file
count) builds the entry-count status text after a successful load;
`SelectionFormat` (`Func<int, string>`) builds the status text while at least
one file is selected. `ReadyText` and `LoadingText` (inherited) set the idle and
in-flight status wording. Every setter validates non-null before mutating.
`FilePickerOptions` carries the caption and placeholder texts, plus
`CountFormat`, `SelectionFormat`, `ReadyText`, and `LoadingText`, to
`ShowAsync` — a null option value leaves the constructed dialog's own default
in place.

## Presentation and ownership

`FilePickerDialog.ShowAsync(owner, options, cancellationToken)` requires a
non-null, undisposed, attached owner and must be called on the owner's
dispatcher. Passing an `Overlay` as the owner makes that Overlay the explicit
host; any other owner uses its owning Screen's private presentation slot.
Outside a hosted Screen, the outermost `Overlay` ancestor serves as the fallback
host — with neither a Screen nor an Overlay ancestor, `ShowAsync` throws — so
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
columns. Its maximum height is 19 chrome rows plus `MaxVisibleRows`, which makes
the default maximum 39 rows. There is no hard minimum width or height: below the
caps, both dimensions stay proportional. File dialogs are movable, like the
sibling Save dialog, so a user can drag them by the title bar within the
presentation Overlay. Window containment keeps the complete border box inside
the host after a resize, including tiny hosts.

One padded Grid uses a single explicit star column, so every row reaches the
trailing content edge. Five rows stack visually — the metadata row lives inside
the shared list-area grid rather than the root grid:

1. A bordered parent-directory Button, exactly five cells wide so its themed
   padding leaves `↑` visible in the middle cell, next to a star-sized editable
   directory path without visible scrollbars.
2. A star-sized bordered ListView with automatic vertical scrolling. It grows
   into the available height until its content reaches `MaxVisibleRows`; the two
   border rows do not count toward that limit.
3. A full-width metadata row immediately below the ListView, split equally
   between the filter ComboBox on the leading half and ellipsized folder and
   file counts aligned to the trailing edge of the other half.
4. The Show hidden CheckBox.
5. A full-width horizontal Separator directly above naturally sized, trailing
   Open and Cancel actions. Their action host is flush with the content bottom
   when neither Button has a shadow, and reserves only the resolved downward
   shadow rows when either style enables one.

The Window supplies the outer frame. Directory rows use a single-cell `▸` prefix
and a trailing platform separator, and file rows use a single-cell `·` prefix.
Filenames are markup-escaped and ellipsized. ListView focus, current, selected,
hovered, and disabled cells use the shared semantic colors; the picker assigns
no RGB colors and emits no terminal bytes.

## Enumeration and ordering

Enumeration runs away from the UI dispatcher and returns only the directory's
immediate children. Each request owns a cancellation token and an increasing
generation number. When a request completes, it posts an immutable entry
snapshot to the dispatcher; completions that are cancelled, stale, detached, or
disposed can no longer touch any control.

Loading-state publication is a transaction boundary. A start observer that
throws releases the unstarted request and restores `IsLoading`; an observer that
detaches, closes, or disposes the dialog stops the start before filesystem work.
Success and recoverable-failure completions release their request before
publishing `IsLoading = false`, then revalidate the dialog before status, focus,
or retained-child work continues.

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
  the parent directory. Backspace matches a plain command after ignoring Caps
  Lock and Num Lock; Control, Alt, Super, Hyper, Meta, and larger chords remain
  unhandled. The Button disables at a root.
- Pressing Enter in the location input loads that text's canonical directory.
- The ListView follows select-then-commit: a single primary pointer click on any
  row - file or directory - only selects it. Committing a row takes Enter or a
  second pointer click (a double-click); the Open Button runs only the accept
  path and stays disabled without a file selection. Committing a directory
  navigates into it; committing a file accepts the current file selection.
- ListView selection follows the existing single, Control-toggle, and
  Shift-range semantics. Directory rows can always become current or selected
  visually; whether `SelectedPaths` retains a selected directory row depends on
  `SelectionMode` - the default `Files` mode filters them out, exactly as
  before this mode existed.

> [!NOTE]
>
> A modifier-held click never commits. The file list runs the double-click
> invocation policy, under which Control- or Shift-held multi-clicks stay
> selection gestures — Ctrl+double-click on a directory does not navigate, and
> on a file does not accept. Enter behaves the same way under a
> non-activation-eligible modifier.

- Committing a file accepts the current file selection, whether the commit came
  from Enter, a double-click, or the Open Button. Open is enabled only while at
  least one file is selected.
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

if (result.IsAccepted)
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
  result, and parent, path, and Backspace navigation plus select-then-commit
  keyboard and pointer invocation behave as described.
- Open, Cancel, Escape, and frame close each complete the dialog. Modality
  isolates the background, Tab stays confined, focus is restored, and the host
  is cleaned up.
- Layout holds across tiny, normal, and wide resizes and captured top-border
  dragging, with the default and configured visible-row caps and the exact
  compact-control geometry.
- Selected cells render with the documented glyphs and semantic roles, and the
  whole flow is exercised against a real temporary directory and the live
  showcase.
- `Style` resolves through local → code-owned completion of the Theme's `window`
  fallback, updates the frame coherently after a live Theme swap (structural
  geometry stays code-owned unless a local `Style` moves it), and composes with
  every owned-part style property.
- Every text property updates its retained control in place and is validated
  before mutation; the caption and placeholder texts are carried through
  `FilePickerOptions` to `ShowAsync`.
