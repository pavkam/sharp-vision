# Button

## Button contract

`Button` is a sealed [`Pressable`](../pressable.md#pressable-contract) command
control with one optional retained `Content` child. One completed activation
raises `Click` and invokes its command once.

## API

| Member                        | Default       | Contract                                                                |
| ----------------------------- | ------------- | ----------------------------------------------------------------------- |
| `Style`                       | `null`        | Optional complete developer-authored `ButtonStyle`.                     |
| `ActualStyle`                 | Theme button  | Always-present style resolved from `Style`, the Theme, or the fallback. |
| `ButtonStyle.Standard`        | Bordered      | One horizontal padding cell and Theme-owned state appearance.           |
| `ButtonStyle.Filled`          | Shadowed fill | Two horizontal padding cells, no border, and a fractional shadow.       |
| `Content`                     | `null`        | Owns the optional visual face.                                          |
| `TextAlignment`               | `Center`      | Aligns retained text within the style padding.                          |
| `Command`, `CommandParameter` | `null`        | Optional command and borrowed parameter.                                |
| `IsDefault`, `IsCancel`       | `false`       | Window Enter/Escape fallback participation.                             |

`ButtonStyle` is complete: it contains `Padding` and the full normal/state
`Appearance`. `ButtonStyleSet` is partial and exists for Theme-file composition;
it is not a Button property. Assigning `Style` makes the whole style local and
authoritative. Assign `null` to restore Theme ownership. `ActualStyle` never
returns null and changes when an inherited Theme changes while `Style` is null.

Button does not publish raw `Border`, `Shadow`, their reset methods, or
`SetAppearance`. Those are protected control-authoring seams because arbitrary
chrome would invalidate Button's border-or-shadow layout invariant. A custom
presentation is supplied as one validated `ButtonStyle`; every reachable state
rejects simultaneous visible shadow and enabled border sides. Public
`ActualBorder` and `ActualShadow` remain available for inspection.

Hover face fill excludes border cells. A Theme may change any border member by
state, but a face-background transition alone never recolors the frame.

## Interaction

Space presses on key down and activates on matching key up while focused. Enter
activates directly. A primary pointer press focuses and captures the Button;
release inside activates once. Disable, detach, focus loss, or capture
cancellation clears the press without activation. `PerformClick()` uses the same
programmatic activation path.

## Example

![The Button control rendered in the live showcase](../../images/controls/button.png)

![The Button control held in its pressed state in the live showcase](../../images/controls/button-pressed.png)

```csharp
var save = new Button { Content = new Text("&Save") };
save.Click += (_, _) => Save();

var add = new Button
{
    Style = ButtonStyle.Filled,
    Content = new Text("&Add")
};
```

## Expected behavior

Cover local/Theme style precedence, null restoration, Theme replacement,
validation, exact standard and filled cells, hover border-background isolation,
Space/Enter/pointer parity, capture cancellation, command ordering, disabled
state, content ownership, Unicode layout, and `ActualStyle` notifications.
