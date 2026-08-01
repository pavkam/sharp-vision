# TreeView

## Overview

`TreeView` displays hierarchical items with expandable nodes, keyboard
navigation, configurable single or multiple selection, and optional checkable
items.

## API

| Member                         | Default                    | Description                                              |
| ------------------------------ | -------------------------- | -------------------------------------------------------- |
| `Items`                        | empty                      | Typed owned root-item collection.                        |
| `SelectedItem`                 | `null`                     | First selected item in stable tree order.                |
| `SelectedItems`                | empty                      | Read-only snapshot in stable tree order.                 |
| `SelectionMode`                | `TreeSelectionMode.Single` | Allows no, one, or multiple selected items.              |
| `Indent`                       | `2` cells                  | Non-negative horizontal extent per visible depth level.  |
| `SetSelected(item, selected)`  | —                          | Adds or removes one item without replacing the rest.     |
| `CheckMark`                    | `null`                     | Shared mark layout and glyphs; null resolves Brackets.   |
| `ActualCheckMark`              | `Brackets`, read-only      | Reports the mark items render when they do not override. |
| `ScrollBarStyle`               | `null`                     | Local generated-bar style; null leaves it to the Theme.  |
| `ActualScrollBarStyle`         | `ThinBlock`, read-only     | Reports the style applied to the generated bar.          |
| `SelectionChanged`             | no subscribers             | Raised after a committed selection change.               |
| `ItemInvoked`                  | no subscribers             | Raised after pointer or keyboard activation.             |
| `SelectItem(TreeViewItem)`     | —                          | Selects an item owned by this tree.                      |
| `SelectAll()`                  | —                          | Selects every enabled item in multiple-selection mode.   |
| `ClearSelection()`             | —                          | Clears the current selection.                            |
| `ExpandAll()`, `CollapseAll()` | —                          | Changes expansion for the complete hierarchy.            |

`SelectionMode` defaults to `Single`. `Multiple` supports Control toggles, Shift
ranges over enabled visible items, `SelectAll`, `ClearSelection`, and Control+A.
`None` keeps navigation and invocation but commits no selection. Selection
belongs to the item model and remains selected when its branch is collapsed;
removing an item removes it from the selection. Disabled items are never
selected. Changing `SelectionMode` publishes its own property notification
before any selection it normalizes, so a two-way observer sees the new
configuration first.

Hierarchy depth is caller-controlled and has no fixed limit. Ownership
propagation, cycle detection, flattening, expand and collapse, item collection,
and check-state propagation all traverse iteratively, so a deep valid tree
cannot exhaust the process stack. Check-state snapshots are evaluated once per
mutation through a shared memo rather than once per affected item, which keeps a
single toggle linear in the nodes it touches instead of quadratic.

A structural change replaces the visible presentation as one validated snapshot
rather than clearing it and re-adding every item, and an unchanged flat list
costs nothing.

`SetSelected(TreeViewItem, bool)` adds or removes one item without replacing the
rest, which `SelectItem` cannot do. In `Single` mode selecting through it
replaces the selection exactly as input does; deselecting is always permitted.
Selecting a disabled item returns `false` and leaves the selection unchanged;
selecting in `None` mode is rejected.

Selection follows the same transaction contract as
[`ListView`](list-view.md#behavior). `SelectionChanging` receives owned
`AddedItems` and `RemovedItems` snapshots in stable tree order and may cancel
before commit. `SelectionChanged` reports the same committed delta after every
selected view and visual state updates, because `PreviousItem` and `CurrentItem`
describe only the first selected identity and cannot express a range,
`SelectAll`, a removal repair, or a mode change. Reentrant changes advance a
transaction version so a stale outer proposal cannot overwrite them. Mode
narrowing and structural rebuilds invalidate pending proposals, and a reentrant
change to `None` mode rejects any pending non-empty proposal.

Only caller- and input-driven changes are cancellable. Normalization the control
performs on its own behalf — narrowing `SelectionMode`, or repairing selection
after items are detached or disabled — commits regardless, because refusing it
would leave the control in a state its own configuration forbids.

The generated scrollbar is reached through nullable `ScrollBarStyle` and
resolved `ActualScrollBarStyle`. The control pins nothing on the bar it owns, so
null leaves the choice to the active Theme and the library default.

Set `TreeViewItem.IsCheckable` to display a check mark. Setting `IsChecked` on a
checkable parent propagates to checkable descendants. A parent becomes
indeterminate when its checkable children do not agree. Space toggles the
current checkable node, and clicking any cell of its mark does the same.

`CheckMark` selects the mark layout and glyph family, using the same `Brackets`,
`Tick`, and `Square` families a standalone [`CheckBox`](../input/check-box.md)
offers, and defaulting to `Brackets` so both controls render an unconfigured
mark identically. Precedence runs local `TreeViewItem.CheckMark`, then the
owning `TreeView.CheckMark`, then the library default; `ActualCheckMark` reports
the resolved value. Only layout and glyphs are shared — a row paints itself from
its own resolved style, so no CheckBox appearance profile reaches the row.

`CheckGlyphs` is a convenience projection over that mark: reading reports the
resolved glyphs, and assigning keeps the resolved layout while replacing only
its glyphs.

A checkable row reserves indent, one disclosure cell, one gap, the mark, and one
leading space before its header, and its measured width matches those cells
exactly. `Brackets` therefore shifts headers two cells further right than a
one-cell family. Every cell of the mark is a hit target.

## Example

![The TreeView control rendered in the live showcase](../../images/controls/tree-view.png)

```csharp
var treeView = new TreeView();

var tree = new TreeView { SelectionMode = TreeSelectionMode.Multiple };
var source = new TreeViewItem("src") { IsCheckable = true };
source.Children.Add(new TreeViewItem("Program.cs") { IsCheckable = true });
tree.Items.Add(source);
```

## Expected behavior

| Layer       | Required evidence                                                                             |
| ----------- | --------------------------------------------------------------------------------------------- |
| Unit        | Ownership, selection modes, validation, expansion, checking, event order, and removal repair. |
| Surface     | Exact indentation, glyphs, focus/current/selected/disabled states, clipping, and scrolling.   |
| Integration | Keyboard and pointer input through mounted routed input.                                      |

- Cover collapsed selected descendants and stable selection order.
- Cover Control toggles, Shift ranges, Control+A, and disabled-item exclusion.
- Cover parent check propagation and indeterminate repair after structural
  edits.
