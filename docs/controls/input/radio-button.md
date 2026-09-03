# RadioButton

## Overview

`RadioButton` is declared
`public sealed class RadioButton : InputBase, IStyled<RadioButtonStyle>`. It is
a focusable mutually exclusive selection control: at most one owned member of an
effective group is checked at a time.

## Inheritance

```mermaid
classDiagram
    ControlBase <|-- InputBase
    InputBase <|-- RadioButton
```

## API

| Member                        | Type                                                 | Default                    | Description                                                                                              |
| ----------------------------- | ---------------------------------------------------- | -------------------------- | -------------------------------------------------------------------------------------------------------- |
| `RadioButton()`               | —                                                    | —                          | Initializes an unselected RadioButton.                                                                   |
| `RadioButton(string text)`    | —                                                    | —                          | Initializes an unselected RadioButton with the given text; rejects `null`.                               |
| Inherited `Text`              | `string`                                             | `""`                       | The radio button's label.                                                                                |
| Inherited `VerticalAlignment` | `VerticalAlignment`                                  | `VerticalAlignment.Center` | Centers the desired mark and caption vertically in its arranged slot; assign `Stretch` to fill the slot. |
| `IsChecked`                   | `bool`                                               | `false`                    | Selects the member; setting `false` programmatically may leave a group empty.                            |
| `GroupName`                   | `string?`                                            | `null`                     | Exact-slot unnamed grouping, or ordinal named grouping when set.                                         |
| `StartAffix`                  | `Affix?`                                             | `null`                     | Optional leading edge-pinned decoration, reserved before the mark glyph.                                 |
| `EndAffix`                    | `Affix?`                                             | `null`                     | Optional trailing edge-pinned decoration, reserved after the caption.                                    |
| `Style`                       | `RadioButtonStyle?`                                  | `null`                     | Optional complete developer-authored presentation.                                                       |
| `ActualStyle`                 | `RadioButtonStyle`                                   | Resolved                   | Read-only; the complete local, theme-owned, or code-owned presentation.                                  |
| Inherited `Command`           | `ICommand?`                                          | `null`                     | Runs after the group selection commits, when bound and `CanExecute` allows it.                           |
| Inherited `CommandParameter`  | `object?`                                            | `null`                     | The borrowed parameter passed to `Command` queries and execution.                                        |
| `CanTabStop`                  | `bool`                                               | Computed                   | Overridden so only the checked member — or the first eligible one when none is checked — is a Tab stop.  |
| `PerformClick()`              | `void`                                               | —                          | Activates an available, visible, enabled RadioButton through its public API.                             |
| `Checked`                     | `EventHandler<RadioButtonSelectionChangedEventArgs>` | No subscribers             | Raised after this member becomes selected.                                                               |
| `Unchecked`                   | `EventHandler<RadioButtonSelectionChangedEventArgs>` | No subscribers             | Raised after this member loses selection.                                                                |
| `SelectionChanged`            | `EventHandler<RadioButtonSelectionChangedEventArgs>` | No subscribers             | Raised on the newly selected or explicitly cleared member.                                               |

The bound command, if any and if `CanExecute` allows it, runs after the group
selection commits and its events raise. Unlike selection itself — which is a
no-op when this member is already the sole checked one in its group — the
command runs on every activation, including re-selecting the current member. The
command and parameter are captured at activation entry, so selection callbacks
may rebind or dispose the control without changing that activation.
`PerformClick()` enters through the shared `InputBase.TryActivate` mutation and
effective-availability gate before group selection or command execution begins.

Group fields are staged before notifications begin. Each later publication is
revalidated against lifetime, group name, and the original owning slot or group
root, so an earlier callback may remove, move, regroup, or dispose a staged
member without publishing through that stale target.

`RadioButtonStyle : InputStyle` is a complete immutable presentation: it bundles
a `RadioButtonMarkStyle`, a complete `RadioButtonGlyphs` pair (unchecked and
checked), and the inherited `Face`/`Border`/`Shadow`.
`RadioButtonStyle.Parentheses` is the default fixed-width `( )`/`(•)`
presentation reserving three cells; `Glyph` is a compact one-cell circle preset.
A `with` expression creates a validated member-wise copy of
`RadioButtonStyle.Default` (`Parentheses`) or of any resolved style. RadioButton
declares no `styles.*` theme key of its own: its code-owned mark style and glyph
pair come from the active theme's root-level `glyphs` field whenever no local
`Style` is assigned (see [themes.md](../../concepts/themes.md#glyph-families)).
Assigning `Style` replaces the whole Theme-owned presentation, and assigning
`null` restores it. `ActualStyle` never returns null. The parentheses style
marks the selected interior with a bullet; the glyph style reserves one cell.
The checked state defaults to the Theme accent foreground, and a
developer-authored checked appearance replaces that color for the complete mark.

`StartAffix` and `EndAffix` each reserve a fixed cell column for
application-owned, per-instance content - never theme-authored - outside the
mark and the caption's own alignment box: `StartAffix` sits before the mark
glyph, `EndAffix` sits after the caption. The gap between a present affix and
its neighbor comes from the shared `InputStyle.AffixGap` (see
[styling.md](../../concepts/styling.md#instance-content-affix)). When the
control is too narrow for everything, the caption shrinks first, then the end
affix drops whole, then the start affix - never a partial cluster - and the
decision is re-evaluated against the control's actual bounds on every render. A
same-width content or color swap on either property repaints without
remeasuring, so an affix can animate (a spinner swapping frames, for example) at
render cost only.

RadioButton does not expose raw border, shadow, or state-appearance mutation.
For third-party composition, inspect `ActualStyle`, `ActualBorder`, and
`ActualShadow` instead.

## Keyboard

| Key            | Behavior                                                                          |
| -------------- | --------------------------------------------------------------------------------- |
| Enter          | Selects this radio button immediately.                                            |
| Space          | Selects this radio button on key release.                                         |
| Left / Up      | Moves focus and selection to the previous eligible member, wrapping at the start. |
| Right / Down   | Moves focus and selection to the next eligible member, wrapping at the end.       |
| Alt+access key | Focuses and selects the radio button when `Text` declares that access key.        |

The arrow keys follow the shared
[keyboard modifier policy](../../concepts/input-routing.md#keyboard-modifier-policy)
for scalar navigation: Caps Lock and Num Lock ride along, while an arrow
carrying Shift, Control, Alt, Super, Hyper, or Meta neither moves the selection
nor is consumed, so a chord bound elsewhere reaches the shortcut that expects
it.

## Group and interaction behavior

User activation selects a member and never toggles it back to false. Unnamed
members group only within their exact ownership slot; named groups match by
ordinal comparison throughout the ownership root. Reparenting and regrouping
resolve exclusivity atomically. The `Unchecked` notification precedes `Checked`,
followed by `SelectionChanged`.

Space and pointer both select. The arrow keys move focus and selection through
eligible members, wrapping at the ends, and repeat while held — a held arrow
keeps walking the group and re-raising the selection events. Disabled, hidden,
collapsed, and detached members are skipped. Disabled styling wins even when a
retained member remains selected.

> [!NOTE]
>
> Tab enters a radio group exactly once: only the checked member — or the first
> eligible member when none is checked — is a Tab stop, and the arrow keys do
> the walking inside the group. Tab does not visit each RadioButton.

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

| Scope               | Observable evidence                                                       |
| ------------------- | ------------------------------------------------------------------------- |
| Public API          | Validation, defaults, state changes, and deterministic output.            |
| Integrated behavior | Cross-component behavior through the real ownership and routing boundary. |

- Group exclusivity holds through regrouping and reparenting, and events arrive
  in the documented order.
- Arrow keys move the selection while skipping disabled members, and unnamed and
  named scopes group exactly as described.
- Style validation and precedence hold, assigning `null` restores the Theme
  style, and a Theme replacement restyles members that have no local style.
- Both mark layouts render correctly, Unicode captions stay owned by their
  member, and rendering produces the exact terminal rows.
