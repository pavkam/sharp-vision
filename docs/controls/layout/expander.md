# Expander

## Expander contract

`Expander` displays a collapsible section with a focusable header toggle and
optional content. It extends
[`ContentControl`](../content-control.md#contentcontrol-contract). Enter, Space,
or a primary pointer click on the header toggles visibility of the content
region below. The header always renders a directional glyph (`▼` expanded, `▶`
collapsed) followed by the header text.

## API

- `Header` is a non-null string rendered beside the toggle glyph. Default is
  empty.
- `IsExpanded` controls content visibility. When `false`, only the header row
  renders and content is excluded from measure. Default is `true`.
- `ExpandedChanged` fires after `IsExpanded` commits a changed value.
- `Content` is the single owned child, arranged below the header row when
  expanded.

## Example

```csharp
var details = new Expander
{
    Header = "Advanced settings",
    IsExpanded = false,
    Content = new Stack
    {
        Children =
        {
            new CheckBox { Content = new Text("Debug mode") },
            new CheckBox { Content = new Text("Verbose logging") },
        },
    },
};
```

## Test obligations

Cover expanded and collapsed measurement, toggle event, header glyph rendering,
keyboard and pointer activation, content arrangement, zero bounds, style states,
and final cells.
