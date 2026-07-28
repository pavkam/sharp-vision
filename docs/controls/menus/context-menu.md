# ContextMenu

## ContextMenu contract

`ContextMenu` displays a vertical menu at an arbitrary cell position with light
dismiss.

## API

| Member                         | Default          | Contract                                                   |
| ------------------------------ | ---------------- | ---------------------------------------------------------- |
| `Items`                        | empty            | Typed managed menu entries.                                |
| `IsOpen`                       | `false`          | Read-only committed popup visibility.                      |
| `Presentation`                 | retained `Popup` | Presentation control used by the owning screen.            |
| `Opening`, `Closing`, `Closed` | no subscribers   | Ordered lifecycle notifications around visibility changes. |
| `Show(int row, int col)`       | —                | Opens at a zero-based root-cell position when attached.    |
| `Close()`                      | —                | Idempotently closes and clears the fixed origin.           |

## Example

```csharp
var contextMenu = new ContextMenu();
```

## Test obligations

| Layer       | Required evidence                                                                            |
| ----------- | -------------------------------------------------------------------------------------------- |
| Unit        | Item ownership, unattached no-op, coordinates, event order, close idempotence, and disposal. |
| Surface     | Root-relative placement, clipping, menu appearance, and elevated rendering.                  |
| Integration | Right-click opening, keyboard navigation, invocation close, and outside light dismiss.       |
