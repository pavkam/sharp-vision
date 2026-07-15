# Button

## Button contract

`Button` is a sealed [`Pressable`](../pressable.md#pressable-contract) command
control with one optional inherited `Content` child. One completed activation
raises `Click` and invokes its command once.

## API

- `Content` uses managed parent ownership.
- `Command` and `CommandParameter` provide optional command activation.
- `IsDefault` and `IsCancel` participate in window-level Enter/Escape handling
  only when no focused control consumes the key.
- `Glyphs`, `HasShadow`, and `ShadowOffset` configure the default button chrome.
- `ShadowMode` chooses the quiet composite lift or a visible block-glyph shadow;
  `ShadowGlyph` supplies the validated printable, one-cell block Rune.
- `Click` is a control event raised after pressed state is released and before
  command execution; command failure follows runtime exception policy.

Buttons render a rounded one-cell border and a compact composite shadow by
default. The border itself reserves the default one-cell content inset;
inherited `Padding` defaults to zero and adds further spacing only when set.
`Glyphs`, `HasShadow`, `ShadowOffset`, `ShadowMode`, and `ShadowGlyph` expose
the chrome choices when an application needs a different surface. A composite
shadow preserves the graphemes beneath its translated footprint and dims their
style; block-glyph mode replaces that footprint outside the button body with the
configured shade Rune. The shared
[chrome contract](../../concepts/styling.md#shared-chrome) defines the same two
footprint semantics for controls that need intrinsic shadow decoration.

Hover and focus appearance apply to the complete Button face, including its
physical border, while the detached shadow retains normal dim styling. During a
held pointer or Space press, a shadowed Button translates its face and owned
content by `ShadowOffset`; that face covers the shadow footprint and makes the
control read as physically pressed. Immediate press handling and the next layout
pass commit the same translated content rectangle; this corrects the former
double border-and-padding inset on the immediate path. Releasing restores the
original face before raising `Click`.

When `HasShadow` is false, the Button remains in its arranged box throughout a
press: there is no absent shadow to cover. It still resolves the full
[`State.Pressed`](../../concepts/styling.md#visual-states) appearance over its
face and border, so a pressed background, foreground, or attribute provides the
visual acknowledgement without pretending to have physical depth.

The shipped control exposes `Click` as a conventional CLR event carrying
`ActivationEventArgs`; it uses the same committed activation pipeline as routed
keyboard and pointer input. `PerformClick()` uses Programmatic cause and rejects
disabled or hidden controls. A non-null `ICommand` is queried with the exact
borrowed `CommandParameter`; false suppresses both Click and execution. Command
replacement observes `CanExecuteChanged` and raises standard property change
notification without retaining disposed Buttons.

`Content` is the atomic capacity-one child. Measure and arrange include its
margin inside the Button's border-and-padding content box, and rendering remains
semantic through the child's inherited active style. When the resolved
appearance defines a background, Button fills its entire arranged surface before
rendering content, so optional padding remains part of the visible interactive
target. `IsDefault` and `IsCancel` are stored for Window fallback routing in
Phase 5C.

## Interaction

Space presses on key down and activates on matching key up while focused. Enter
activates directly. A primary pointer press focuses the Button, captures it, and
sets pressed only while the pointer remains inside; release inside activates
once. The committed focus state resolves the Button's `Focused` visual style
until another control receives focus. Disable, detach, focus loss policy, or
capture cancellation clears pressed without activation.

## Example

```csharp
var save = new Button { Content = new Text("Save") };
save.Click += (_, _) => Save();
```

## Test obligations

Cover Space/Enter/pointer parity, capture movement, cancellation,
hover-frame-versus-normal-shadow styling, pressed face translation and shadow
occlusion, default/cancel routing, command ordering/failure, disabled/hidden
state, focus, content ownership, combined visual states, Unicode/tiny layout,
and final cells/events.
