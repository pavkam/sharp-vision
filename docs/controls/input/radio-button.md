# RadioButton

## RadioButton contract

`RadioButton` is a sealed [`Pressable`](../pressable.md#pressable-contract)
selection control. At most one owned member in an effective group is checked.

## API

| Member                         | Default            | Contract                                                        |
| ------------------------------ | ------------------ | --------------------------------------------------------------- |
| `IsChecked`                    | `false`            | Selects the member; programmatic false may leave a group empty. |
| `GroupName`                    | `null`             | Exact-slot unnamed grouping or ordinal named grouping.          |
| `Style`                        | `null`             | Optional complete developer-authored `RadioButtonStyle`.        |
| `ActualStyle`                  | Theme radio button | Always-present resolved style.                                  |
| `RadioButtonStyle.Parentheses` | Theme default      | Fixed-width `( )` and `(•)` presentation.                       |
| `RadioButtonStyle.Glyph`       | Preset             | Compact one-cell circle presentation.                           |
| `Content`                      | `null`             | Owns the optional label or richer visual.                       |
| Group-selection events         | No subscribers     | Report staged old/new members and activation cause.             |

`RadioButtonStyle` contains `RadioButtonMarkStyle`, complete
`RadioButtonGlyphs`, and the full appearance profile. `RadioButtonStyleSet` is
the partial Theme-file composition type. Assigning `Style` replaces the whole
Theme-owned presentation; assigning null restores it. `ActualStyle` never
returns null. Parentheses reserve three cells and use a bullet for the selected
interior; glyph style reserves one cell. The standard profile supplies the Theme
accent as its checked foreground. A developer-authored checked appearance
replaces that color for the complete mark.

RadioButton does not publish raw border, shadow, or state-appearance mutation.
Third-party composition inspects `ActualStyle`, `ActualBorder`, and
`ActualShadow`.

## Group and interaction behavior

User activation selects and never toggles false. Unnamed members group only in
their exact ownership slot; named groups use ordinal matching throughout the
ownership root. Reparenting and regrouping resolve exclusivity atomically.
Unchecked notification precedes Checked, followed by SelectionChanged.

Space and pointer select. Arrow keys move focus and selection through eligible
members with wrapping. Disabled, hidden, collapsed, and detached members are
skipped. Disabled styling wins even when a retained member remains selected.

## Example

```csharp
var compact = new RadioButton
{
    GroupName = "density",
    Content = new Text("Compact")
};

var glyph = new RadioButton
{
    GroupName = "density",
    Content = new Text("Comfortable"),
    Style = RadioButtonStyle.Glyph
};
```

## Test obligations

Cover exclusivity, regroup/reparent, event order, arrows, disabled skipping,
unnamed/named scope, style validation and precedence, null restoration, Theme
replacement, both mark layouts, Unicode ownership, and exact terminal rows.
