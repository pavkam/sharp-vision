# Button

## Overview

`Button` is a sealed [`Pressable`](../pressable.md#overview) command control
with one optional retained `Content` child. Each completed activation raises
`Click` and invokes the command once.

## API

| Member                        | Default       | Description                                                             |
| ----------------------------- | ------------- | ----------------------------------------------------------------------- |
| `Style`                       | `null`        | Optional complete developer-authored `ButtonStyle`.                     |
| `ActualStyle`                 | Theme button  | The resolved style, taken from `Style`, the Theme, or the fallback.     |
| `ButtonStyle.Standard`        | Bordered      | One horizontal padding cell with Theme-owned state appearance.          |
| `ButtonStyle.Filled`          | Shadowed fill | Two horizontal padding cells, no border, and a fractional shadow.       |
| `Content`                     | `null`        | The optional visual face, owned by the button.                          |
| `TextAlignment`               | `Center`      | How retained text aligns within the style padding.                      |
| `Command`, `CommandParameter` | `null`        | Optional command and borrowed parameter.                                |
| `IsDefault`, `IsCancel`       | `false`       | Whether the button answers the window's Enter/Escape fallback.          |

A `ButtonStyle` is complete: it carries `Padding` and the full normal and
per-state `Appearance`. `ButtonStyleSet` is the partial counterpart used to
compose Theme files; it is not a Button property. Assigning `Style` makes the
whole style local and authoritative, and assigning `null` hands ownership back
to the Theme. `ActualStyle` never returns null, and it changes when an
inherited Theme changes while `Style` is null.

Button does not expose the raw `Border` and `Shadow` properties, their reset
methods, or `SetAppearance`. Those remain protected seams for control authors,
because arbitrary chrome could break Button's border-or-shadow layout
invariant. To customize the presentation, supply one validated `ButtonStyle`:
validation rejects any reachable state that combines a visible shadow with
enabled border sides. The public `ActualBorder` and `ActualShadow` properties
remain available for inspection.

When the pointer hovers over a button, the face fill excludes the border
cells. A Theme may change any border member per state, but a change to the
face background alone never recolors the frame.

## Interaction

Pressing Space while focused starts a press on key down and activates on the
matching key up. Enter activates immediately. A primary pointer press focuses
the button and captures the pointer; releasing inside the bounds activates
once. Disabling, detaching, losing focus, or having capture cancelled clears
the press without activating. `PerformClick()` runs the same programmatic
activation path.

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

A local `Style` wins over the Theme, assigning `null` restores Theme
ownership, and replacing the Theme restyles buttons that have no local style.
Style validation rejects invalid combinations, and the standard and filled
styles render their exact documented cells. Hovering changes the face
background without recoloring the border. Space, Enter, and pointer activation
behave identically, capture cancellation clears a pending press, and the click
event and command run in their documented order. Disabled buttons never
activate, content ownership rules hold, Unicode content lays out correctly,
and `ActualStyle` raises change notifications.
