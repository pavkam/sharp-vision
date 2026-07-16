# Separator

## Separator contract

`Separator` draws one non-interactive horizontal or vertical divider line. It
cannot receive focus, is excluded from hit testing, and owns no children.

## API

- `Orientation` controls horizontal (line fills width) or vertical (line fills
  height) layout. Default is `Horizontal`.

## Example

```csharp
var separator = new Separator { Orientation = Orientation.Horizontal };
```

## Test obligations

Cover horizontal and vertical rendering, zero bounds, orientation changes, style
inheritance, and final cells.
