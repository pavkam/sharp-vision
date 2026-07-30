# SharpVision Text Editor

A terminal text editor built with SharpVision. It demonstrates multiline
editing, file open/save workflows, dirty-state confirmation, menus, keyboard
shortcuts, find/replace, context menus, dialogs, and status chrome.

## Run

```bash
dotnet run --project examples/TextEditor
```

## Features

### Editing

Full multiline text editing through the framework's `TextInput` control with
`AcceptsReturn` and `AcceptsTab` enabled. Supports grapheme-safe cursor
movement, word navigation (Ctrl+Left/Right), and automatic scrolling for content
larger than the viewport.

### Menu bar

A horizontal menu bar at the top provides grouped commands:

| Group  | Items                                    |
| ------ | ---------------------------------------- |
| File   | New, Open, Save, Save As, Quit           |
| Edit   | Undo, Redo, Cut, Copy, Paste, Select All |
| View   | Word Wrap                                |
| Search | Find, Replace                            |

Opening a dropdown creates one dismissing modal menu plane for the complete
submenu chain. Invoking a command closes that plane before the editor command or
another modal surface begins.

### File workflows

Open uses `FilePickerDialog`; Save As uses `SaveFileDialog` with overwrite
confirmation. New, Open, and Quit ask whether to save a dirty document through
`MessageBox`. Failed reads and writes preserve the current document state and
surface the error in a message box.

### Find and replace

Ctrl+F opens a draggable Find dialog built with `Window`. Ctrl+H opens the same
dialog with a Replace row visible. The dialog supports:

- **Find next** — case-insensitive search that wraps around the document.
- **Replace** — replaces the current match and advances to the next.
- **Replace all** — replaces every occurrence in one pass.
- **Match count** — the status line shows the total number of matches.

The dialog auto-populates the search box with the current selection. It is a
modeless Window: opening moves focus to the search field, while the editor and
menus remain available. Its compact form keeps labels, fields, and shadow-free
actions aligned without crowding the fields: right-aligned labels share one
automatic column, both editors share one proportional column, status owns a
quiet line, and all actions sit in one trailing footer. The bounded responsive
width stays compact, the initial inset preserves the editor border, the title
bar can be dragged anywhere inside the editor canvas, and terminal resize pushes
the complete Window border box back into view.

### Context menu

Right-click anywhere in the editor to open a vertical `Menu` through
`Popup.OpenModal` with Cut, Copy, Paste, Select All, Find, and Replace actions.
Outside input dismisses the popup without replaying that input into the editor.

### Status bar

The bottom bar shows:

- Current document state (`Ready`).
- Current line, column, and optional selection length.
- Word-wrap state (`Wrap`).
- Encoding (`UTF-8`).
- Current file name (`Untitled` until a file is selected).

The example uses one leading `StatusBarItem` and four right-aligned items, so
the document message uses the available space while compact context remains
anchored.

### Keyboard shortcuts

| Shortcut     | Action           |
| ------------ | ---------------- |
| Ctrl+N       | New file         |
| Ctrl+O       | Open file        |
| Ctrl+S       | Save             |
| Ctrl+Shift+S | Save as          |
| Ctrl+Z       | Undo             |
| Ctrl+Y       | Redo             |
| Ctrl+X       | Cut              |
| Ctrl+C       | Copy             |
| Ctrl+V       | Paste            |
| Ctrl+A       | Select all       |
| Ctrl+F       | Find             |
| Ctrl+H       | Find and replace |
| Alt+Z        | Toggle word wrap |
| Ctrl+Q       | Quit             |

## Architecture

| File                   | Role                                          |
| ---------------------- | --------------------------------------------- |
| `Program.cs`           | Entry point via `ConsoleApplication.RunAsync` |
| `EditorScreen.cs`      | Screen subclass — layout, menus, shortcuts    |
| `FindReplaceDialog.cs` | Find/replace logic and Window dialog          |
| `GlobalUsings.cs`      | Shared framework imports                      |

The editor uses no custom drawing. Every visual element is a standard
SharpVision control: `TextInput`, `Menu`, `MenuItem`, `Popup`, `Window`,
`StatusBar`, `StatusBarItem`, `Button`, `Text`, `Dock`, `Stack`, and `Overlay`.
