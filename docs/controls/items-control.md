# ItemsControl

## Overview

`ItemsControl : Control` is the base role for semantic controls that realize an
ordered set of controls inside one private presentation container. It implements
an ordinary semantic owner; its direct appearance does not create a style scope
or cascade through its private host to realized items.

A concrete constructor calls `InitializeItemsHost(Container)` exactly once. A
rejected candidate does not consume initialization. Once a host commits, it
remains the permanent presentation root even when a lifecycle callback throws.
The host is an ordinary owned control and therefore receives dispatcher, theme,
cell-policy, enabled/visible, rendering, hit-testing, focus-navigation, popup,
and disposal behavior through the shared ownership registry.

The base class exposes no `Children`, host, mutable collection, or data-item
type. Derived controls define their own semantic collection and use the
protected `ItemControlCount`, `GetItemControl`, `IndexOfItemControl`, insert,
remove, replace, clear, and complete-snapshot replacement helpers. Complete
replacement copies and validates every candidate before changing ownership.
Removed controls are detached without disposal; controls still owned when the
item owner is disposed are disposed with the private host.

`OnItemControlsChanged` runs once after each committed snapshot, including a
change caused by direct item disposal. It observes the complete new order while
guarded ownership publication remains active. A callback failure does not roll
back the committed snapshot, and reentrant ownership mutation is rejected.

The base measures the host inside its own content box, includes a visible host
margin in desired size, and arranges the host over both resolved axes. The host
owns item-specific layout and optional scrolling; `ItemsControl` itself adds no
scrolling contract.

## API

| Member group                                               | Purpose                                                               |
| ---------------------------------------------------------- | --------------------------------------------------------------------- |
| `InitializeItemsHost(Container)`                           | Commits the permanent private presentation host exactly once.         |
| `ItemControlCount`, `GetItemControl`, `IndexOfItemControl` | Inspect realized presentation controls from a derived semantic owner. |
| Insert, remove, replace, clear, and snapshot helpers       | Mutate realized controls through validated ownership transactions.    |
| `OnItemControlsChanged`                                    | Observe one committed realized-control snapshot.                      |

`ItemsControl` deliberately exposes no public `Children` collection. Concrete
types such as [`ListView`](collections/list-view.md#overview) and
[`Table`](layout/table.md#overview) publish typed semantic collections.

## Example

```csharp
public sealed class TagCloud : ItemsControl
{
    public TagCloud()
    {
        InitializeItemsHost(new Stack { Orientation = Orientation.Horizontal });
    }

    public void Add(string tag) =>
        InsertItemControl(ItemControlCount, new Text { Content = tag });
}
```

An application interacts with the semantic `TagCloud` API. It cannot replace the
host or insert arbitrary presentation children.

## Expected behavior

| Layer    | Required evidence                                                                                          |
| -------- | ---------------------------------------------------------------------------------------------------------- |
| Unit     | One-time host initialization, ownership validation, atomic snapshots, callbacks, reentrancy, and disposal. |
| Surface  | Host margin, measure/arrange delegation, rendering, hit testing, and popup traversal.                      |
| Consumer | External derivation uses only the protected authoring surface without accessing the private host.          |
