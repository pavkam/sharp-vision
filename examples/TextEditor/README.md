# SharpVision Text Editor

A terminal text editor built with the SharpVision TUI framework. Demonstrates
multiline editing, menus, keyboard shortcuts, find/replace, and context menus.

## Run

```bash
dotnet run --project examples/TextEditor
```

## Features

### Editing

Full multiline text editing through the framework's `TextInput` control with
`AcceptsReturn` and `AcceptsTab` enabled. Supports grapheme-safe cursor
movement, word navigation (Ctrl+Left/Right), and automatic scrolling for
content larger than the viewport.

### Menu bar

A horizontal menu bar at the top provides grouped commands:

| Group  | Items                                        |
|--------|----------------------------------------------|
| File   | New (Ctrl+N), Quit (Ctrl+Q)                  |
| Edit   | Undo, Redo, Cut, Copy, Paste, Select All     |
| Search | Find (Ctrl+F), Replace (Ctrl+H)              |

### Find and replace

Ctrl+F opens a draggable Find dialog built with `Window`. Ctrl+H opens the
same dialog with a Replace row visible. The dialog supports:

- **Find next** — case-insensitive search that wraps around the document.
- **Replace** — replaces the current match and advances to the next.
- **Replace all** — replaces every occurrence in one pass.
- **Match count** — the status line shows the total number of matches.

The dialog auto-populates the search box with the current selection.

### Context menu

Right-click anywhere in the editor to open a vertical `Menu` inside a `Popup`
with Cut, Copy, Paste, Select All, Find, and Replace actions.

### Status bar

The bottom bar shows:

- Current line and column (`Ln 1, Col 1`).
- Selection length when text is selected (`Sel 42`).
- Encoding indicator (`UTF-8`).

### Keyboard shortcuts

| Shortcut | Action           |
|----------|------------------|
| Ctrl+N   | New file         |
| Ctrl+Z   | Undo             |
| Ctrl+Y   | Redo             |
| Ctrl+X   | Cut              |
| Ctrl+C   | Copy             |
| Ctrl+V   | Paste            |
| Ctrl+A   | Select all       |
| Ctrl+F   | Find             |
| Ctrl+H   | Find and replace |
| Ctrl+Q   | Quit             |

## Architecture

| File                  | Role                                              |
|-----------------------|---------------------------------------------------|
| `Program.cs`          | Entry point via `ConsoleApplication.RunAsync`     |
| `EditorScreen.cs`     | Screen subclass — layout, menus, shortcuts        |
| `FindReplaceDialog.cs`| Find/replace logic and Window dialog              |
| `GlobalUsings.cs`     | Shared framework imports                          |

The editor uses no custom drawing. Every visual element is a standard
SharpVision control: `TextInput`, `Menu`, `MenuItem`, `Popup`, `Window`,
`Button`, `Text`, `Dock`, `Stack`, and `Overlay`.
