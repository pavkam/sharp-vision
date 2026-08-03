# RadioButton

## Overview

`RadioButton` is a sealed [`PressableBase`](../pressable.md#overview) selection
control. At most one owned member of an effective group is checked at a time.

## API

| Member                         | Default            | Description                                                     |
| ------------------------------ | ------------------ | --------------------------------------------------------------- |
| `IsChecked`                    | `false`            | Selects the member; programmatic false may leave a group empty. |
| `GroupName`                    | `null`             | Exact-slot unnamed grouping or ordinal named grouping.          |
| `Style`                        | `null`             | Optional complete developer-authored `RadioButtonStyle`.        |
| `ActualStyle`                  | Theme radio button | The resolved style; never null.                                 |
| `RadioButtonStyle.Parentheses` | Theme default      | Fixed-width `( )` and `(•)` presentation.                       |
| `RadioButtonStyle.Glyph`       | Preset             | Compact one-cell circle presentation.                           |
| Inherited `Text`               | `""`               | The radio button's label.                                       |
| Group-selection events         | No subscribers     | Report staged old/new members and the activation cause.         |
| `Command`, `CommandParameter`  | `null`             | Inherited from `PressableBase`; runs after the group commits.   |

The bound command, if any and if `CanExecute` allows it, runs after the group
selection commits and its events raise. Unlike selection itself - which is a
no-op when this member is already the sole checked one in its group - the
command runs on every activation, including re-selecting the current member.

`RadioButtonStyle` bundles a `RadioButtonMarkStyle`, a complete set of
`RadioButtonGlyphs`, and the full appearance profile. Use
`RadioButtonStyle.With(...)` for validated member-wise copies and appearance
overlays; theme JSON remains semantic-only. Assigning `Style` replaces the whole
Theme-owned presentation, and assigning `null` restores it. `ActualStyle` never
returns null. The parentheses style reserves three cells and marks the selected
interior with a bullet; the glyph style reserves one cell. The standard profile
uses the Theme accent as its checked foreground, and a developer-authored
checked appearance replaces that color for the complete mark.

RadioButton does not expose raw border, shadow, or state-appearance mutation.
For third-party composition, inspect `ActualStyle`, `ActualBorder`, and
`ActualShadow` instead.

## Group and interaction behavior

User activation selects a member and never toggles it back to false. Unnamed
members group only within their exact ownership slot; named groups match by
ordinal comparison throughout the ownership root. Reparenting and regrouping
resolve exclusivity atomically. The Unchecked notification precedes Checked,
followed by SelectionChanged.

Space and pointer both select. The arrow keys move focus and selection through
eligible members, wrapping at the ends. Disabled, hidden, collapsed, and
detached members are skipped. Disabled styling wins even when a retained member
remains selected.

## Example

![The RadioButton control rendered in the live showcase](../../images/controls/radio-button.png)

```csharp
var compact = new RadioButton
{
    GroupName = "density",
    Text = "Compact"
};

var glyph = new RadioButton
{
    GroupName = "density",
    Text = "Comfortable",
    Style = RadioButtonStyle.Glyph
};
```

## Expected behavior

Group exclusivity holds through regrouping and reparenting, and events arrive in
the documented order. Arrow keys move the selection while skipping disabled
members, and unnamed and named scopes group exactly as described. Style
validation and precedence hold, assigning `null` restores the Theme style, and a
Theme replacement restyles members that have no local style. Both mark layouts
render correctly, Unicode captions stay owned by its member, and rendering
produces the exact terminal rows.
