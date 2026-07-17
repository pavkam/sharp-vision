# Expander

## Expander contract

`Expander` displays a collapsible section with a focusable header toggle and
optional content. It extends
[`ContentControl`](../content-control.md#contentcontrol-contract). Enter, Space,
or a primary pointer click on the header toggles visibility of the content
region below. The header always renders a directional glyph (`▼` expanded, `▶`
collapsed) followed by the header text.

`Expander` itself is the focus, hover, and press owner for the header row. The
header is rendered directly by the control and is not exposed as a public
presentation child. Caller content remains the one ordinary owned child.

## API

- `Header` is a non-null string rendered beside the toggle glyph. Terminal
  control characters are rejected before state changes. Default is empty.
- `IsExpanded` controls content visibility. When `false`, only the header row
  renders and content is excluded from measure, arrangement, rendering, hit
  testing, and navigation. The content remains owned and attached. Default is
  `true`.
- `ExpandedChanged` fires after `IsExpanded` commits a changed value.
- `Content` is the single owned child, arranged below the header row when
  expanded. Replacing content while collapsed immediately releases the previous
  child without changing expansion state.

Inherited disabled state applies to the semantic header owner. An unavailable
header remains visible but pointer, Space, and Enter input cannot change
expansion.

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

Cover expanded and collapsed measurement, ownership-preserving exclusion, toggle
event order, exact header glyph/text cells, keyboard and pointer activation,
disabled refusal, focus, content replacement, Unicode cell geometry,
resize/reflow, stale-cell clearing, and final cells. The showcase must include
expanded, collapsed, nested, disabled, wide-header, and replaced-content
specimens.
