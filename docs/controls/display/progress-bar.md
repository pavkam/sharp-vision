# ProgressBar

## ProgressBar contract

`ProgressBar` displays a non-interactive visual progress indicator as a filled
bar. It cannot receive focus, is excluded from hit testing, and owns no
children.

## API

- `Value` is clamped between `Minimum` and `Maximum`.
- `Minimum` and `Maximum` define the value range. Default range is 0 to 1.
- `IsIndeterminate` indicates an unknown duration (not yet animated).
- `Orientation` controls horizontal or vertical bar layout.

## Example

```csharp
var bar = new ProgressBar { Maximum = 100, Value = 42 };
```

## Test obligations

Cover value clamping, horizontal and vertical rendering, zero bounds, range
changes, style inheritance, and final cells.
