# CheckBox

## Overview

`CheckBox` is a sealed [`Pressable<CheckBoxStyle>`](../pressable.md#overview)
toggle whose caption is its inherited `Text`, with either two- or three-state
behavior.

## API

| Member                         | Default        | Description                                                    |
| ------------------------------ | -------------- | -------------------------------------------------------------- |
| `IsChecked`                    | `false`        | `false`, `true`, or `null` when three-state mode permits it.   |
| `IsThreeState`                 | `false`        | Selects the two-state or three-state activation cycle.         |
| `Style`                        | `null`         | Optional complete developer-authored `CheckBoxStyle`.          |
| `ActualStyle`                  | Theme checkbox | The resolved style; never null.                                |
| `CheckBoxStyle.Brackets`       | Theme default  | Fixed-width `[ ]`, `[✓]`, and `[─]` presentation.              |
| `CheckBoxStyle.Tick`, `Square` | Presets        | Complete one-cell presentations.                               |
| Inherited `Text`               | `""`           | The checkbox's label.                                          |
| State events                   | No subscribers | Report committed transitions in deterministic order.           |
| `Command`, `CommandParameter`  | `null`         | Inherited from `PressableBase`; runs after the toggle commits. |

Completing an activation always commits the toggle and raises the state events
first; the bound command, if any and if `CanExecute` allows it, runs last. A
command that cannot execute never suppresses the toggle itself.

`CheckBoxStyle` bundles a `CheckBoxMarkStyle`, a complete set of
`CheckBoxGlyphs`, and the full appearance profile. `CheckBoxStyle.With(...)`
copies selected members and may overlay an `AppearanceProfileSet`; theme JSON
remains semantic-only. Assigning `Style` replaces the complete Theme-owned
presentation, and assigning `null` restores it. Every glyph is printable and one
cell wide under the normal width policy. The brackets style reserves three
cells; the other styles reserve one.

Raw border, shadow, and state-appearance authoring stays protected. When
composing a third-party control around a CheckBox, inspect `ActualStyle`,
`ActualBorder`, and `ActualShadow` instead.

## Behavior

Two-state activation cycles between `false` and `true`; three-state activation
cycles `false → true → null → false`. Turning three-state mode off while the
value is indeterminate commits `false` before any notifications are published.
Space and primary-pointer activation share the same transition path. The
disabled state wins over all interactive states, and focused or selected styling
applies to the mark and content together as one semantic item.

## Example

![The CheckBox control rendered in the live showcase](../../images/controls/check-box.png)

![The CheckBox control with its check mark set in the live showcase](../../images/controls/check-box-checked.png)

```csharp
var option = new CheckBox
{
    Text = "Include &hidden files",
    Style = CheckBoxStyle.Square
};
```

## Expected behavior

Both activation cycles behave as documented, and state events arrive in their
committed order. Assigning `null` while three-state mode is disabled is
rejected. Style validation and precedence hold, assigning `null` restores the
Theme style, and a Theme replacement restyles checkboxes that have no local
style. Space and pointer activation behave identically, disabled and focused
states render correctly, a change in mark width relayouts as expected, Unicode
glyphs fall back safely, and rendering produces the exact documented cells.
