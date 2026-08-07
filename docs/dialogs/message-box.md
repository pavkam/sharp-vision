# MessageBox

## Overview

`MessageBox` lives in `SharpVision.Dialogs` and is a retained, measured
[`Window`](../controls/windows/window.md#overview) specialization for short user
decisions. The MessageBox object itself renders the title, the grapheme-safe
wrapped message, the centered action row, and the
[modal presentation](../concepts/modality.md#popup-and-window-presentations)
when shown asynchronously; there is no nested proxy Window.

## API

| Member                 | Default                | Description                                                                                                                                      |
| ---------------------- | ---------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------ |
| `Message`              | required               | Non-null message rendered as grapheme-safe wrapped `Text`.                                                                                       |
| `Title`                | `"Message"`            | Non-null Window title.                                                                                                                           |
| `Buttons`              | `MessageBoxButtons.Ok` | Defined semantic action layout.                                                                                                                  |
| `Style`                | `null`                 | A complete local `MessageBoxStyle` owning the frame, message face, and content geometry; `null` follows the active Theme's `messageBox` section. |
| `ActualStyle`          | resolved               | The resolved `MessageBoxStyle` currently applied.                                                                                                |
| `ButtonStyle`          | `null`                 | A complete local `ButtonStyle` applied to every generated action; `null` lets each Button use its own semantic profile.                          |
| `ActualButtonStyle`    | resolved               | The resolved `ButtonStyle` currently applied to every generated action.                                                                          |
| `SeparatorStyle`       | `null`                 | A complete local `SeparatorStyle` applied to the divider above the action row; `null` follows the active Theme's `separator` section.            |
| `ActualSeparatorStyle` | resolved               | The resolved `SeparatorStyle` currently applied to the divider.                                                                                  |
| `OkText`               | `"&OK"`                | Non-null caption for the OK action, when the current layout includes one.                                                                        |
| `CancelText`           | `"&Cancel"`            | Non-null caption for the Cancel action, when the current layout includes one.                                                                    |
| `YesText`              | `"&Yes"`               | Non-null caption for the Yes action, when the current layout includes one.                                                                       |
| `NoText`               | `"&No"`                | Non-null caption for the No action, when the current layout includes one.                                                                        |
| `SelectedResult`       | default enum value     | The last selection made on a directly mounted MessageBox.                                                                                        |
| `HasSelectedResult`    | `false`                | Distinguishes "no modeless selection yet" from the enum's default value.                                                                         |
| `ResultSelected`       | no subscribers         | Raised when a directly mounted MessageBox takes a keyboard or pointer choice.                                                                    |
| `ShowAsync(...)`       | —                      | Presents one temporary modal MessageBox and returns its semantic result.                                                                         |

`MessageBoxButtons` defines the supported layouts:

- `Ok`: OK
- `OkCancel`: OK, Cancel
- `YesNo`: Yes, No
- `YesNoCancel`: Yes, No, Cancel

`MessageBoxResult` is `Ok`, `Cancel`, `Yes`, or `No`. Activating a button
completes the returned task with that button's semantic result. Closing the
frame or dismissing the surface completes the task with `Cancel`.

The constructors accept a non-null `message`, an optional non-null `title`
(defaulting to `Message`), and a defined `MessageBoxButtons` value. The static
overloads are:

```csharp
{
    var result = await MessageBox.ShowAsync(owner, "Delete the draft?");
}
{
    var result = await MessageBox.ShowAsync(owner, "Delete the draft?", "Confirm");
}
{
    var result = await MessageBox.ShowAsync(owner, "Delete the draft?", MessageBoxButtons.YesNo);
}
{
    var result = await MessageBox.ShowAsync(
        owner,
        "Delete the draft?",
        "Confirm",
        MessageBoxButtons.YesNoCancel);
}
{
    var result = await MessageBox.ShowAsync(
        owner,
        "Delete the draft?",
        "Confirm",
        MessageBoxButtons.YesNoCancel,
        buttonStyle);
}
{
    var result = await MessageBox.ShowAsync(
        owner,
        "¿Eliminar el borrador?",
        new MessageBoxOptions
        {
            Title = "Confirmar",
            Buttons = MessageBoxButtons.YesNoCancel,
            YesText = "&Sí",
            NoText = "&No",
            CancelText = "&Cancelar"
        });
}
```

The `MessageBoxOptions` overload configures title, layout, all four captions,
and a local `Style` in one call, without exposing the generated Buttons or
divider - the preferred way to localize or restyle a presented MessageBox
instead of multiplying `ShowAsync` overloads.

`owner` must resolve to an owning Screen, an explicit container, or the
outermost fallback container. In a hosted application the helper adds one
temporary MessageBox to the Screen's private presentation slot, so a bounded
card, pane, or showcase stage can identify ownership without constraining the
modal surface or exposing framework children. Outside a Screen, an explicit or
outermost container is still a supported host. The helper enters a Window modal
presentation with outside interaction ignored. On normal completion it publishes
`Closing` and `Closed`, removes and disposes the MessageBox, and then settles
the returned task. Calls are dispatcher-affine.

A directly mounted MessageBox is modeless. Activating a button with the keyboard
or pointer updates `SelectedResult`, sets `HasSelectedResult`, and raises
`ResultSelected` without removing or disposing the surface. In this mode the
MessageBox leaves Escape unhandled so an ancestor can apply its own policy.

## Interaction

The inherited Window uses the dialog defaults for a paired frame and a centered
header, but overrides the rest: the box is movable rather than fixed, renders no
close control (`CanClose` is `false`), and implements its own Escape-to-Cancel
handling rather than the inherited Escape-close fallback, which is dead code
here because that fallback also requires `CanClose`.

The window sizes itself from its title, wrapped message, and button labels, but
never past **80% of the available presentation-host width** - the Overlay or
Screen plane `ShowAsync` presents into, not the bounds of a small owner control.
This is a cap, not a target: a short message stays compact, and only a message
long enough to need the room grows the box toward that width. The cap is
recomputed on every layout pass from the incoming measure constraint, so a live
presentation resize retargets it automatically with no explicit resize handling.
Its content is a two-row grid: the top row is an intrinsic message area whose
centered, wrapped text begins two empty interior rows below the title edge, and
the bottom row is the shared dialog action bar. The action bar renders a
horizontal divider spanning the content width directly against the centered
action row. Moving the Window therefore only adds placement offsets; its
measured height does not change. Message text wraps within the capped width by
grapheme cluster. The window keeps a 32-by-8-cell minimum footprint for
consistent dialog proportions - never forced past a host too small to
accommodate it - and is centered on both axes across that host.

The button group is centered horizontally and its buttons share the widest
label's width. Captions use the Button default centered text alignment, and
dialog composition does not select a Button kind. `ButtonStyle` overrides face,
border, shadow, and padding for every generated action, while `null` follows the
active Theme. Assigning it after construction updates every retained button
coherently and remeasures when padding or chrome changes. The action host stays
flush below non-shadow Buttons and reserves only their resolved downward shadow
rows. `ShowAsync` accepts the same style without exposing the underlying Button
instances.

Focus enters the first affirmative button, Tab stays inside the modal plane, and
pointer, keyboard, text, paste, and wheel input outside the dialog is consumed
by the shared modality manager.

Generated actions declare the conventional `&OK`, `&Cancel`, `&Yes`, and `&No`
[access keys](../concepts/access-keys.md#focus-and-semantic-actions) by default.
`OkText`, `CancelText`, `YesText`, and `NoText` replace a caption in place on
the retained Button that owns that semantic action - no Button is ever
recreated, so its `MessageBoxResult`, default/cancel role, focus state, and
event order are unaffected. Changing a caption remeasures every generated action
to the widest current label, keeping their widths equal. Setting a caption for
an action absent from the current `Buttons` layout stores the value without
touching any retained Button. A caller-supplied caption keeps whatever ampersand
access key it carries. Message prose stays ordinary rich `Text` and does not
interpret ampersands.

## Theming

`MessageBoxStyle` is the complete aggregate presentation: the frame
(`Face`/`Border`/`Shadow`, falling back to the Window role's own semantic
appearance - including its ActiveBorder-on-FocusWithin default), `MessageFace`
for the message text, and `MessageMargin`/`ActionBarMargin` for the two pieces
of content geometry that are genuinely presentation choices rather than fixed
layout. A Theme authors it through its own `messageBox` style section, resolved
with the standard local &rarr; Theme &rarr; fallback precedence; `Style` and
`ActualStyle` follow the same contract as every other themed control. A live
Theme swap updates the frame, message face, and content geometry together on the
next layout pass, even without a local `Style`.

The divider above the action row is a canonical `Separator` with its own
`SeparatorStyle`/`ActualStyle` contract (its "separator" theme key falls back to
the generic control role). `MessageBox.SeparatorStyle` forwards a complete local
override to the retained divider through the same part-style binding
`ButtonStyle` already uses; `null` returns the divider to independent Theme
ownership instead of pinning a previously resolved value.

## Example

```csharp
var result = await MessageBox.ShowAsync(
    owner,
    "Delete the draft?",
    "Confirm",
    MessageBoxButtons.YesNoCancel);

if (result == MessageBoxResult.Yes)
{
    DeleteDraft();
}
```

## Expected behavior

The behavior above is verified end to end, so callers can rely on it:

- The result enums are stable, arguments are validated, and the title and
  message are retained as given.
- The window composes with the dialog role, wraps its message under a small
  viewport, and renders all four button layouts.
- The outer width never exceeds 80% of the available presentation width, stays
  compact for short messages, and recomputes the cap after a presentation
  resize. A horizontal divider consistently separates the message from the
  action row.
- Shown from a bounded owner, it centers across the whole application host,
  applies the deliberate message offset, and centers its captions.
- Modal presentation honors default-button and Escape activation. A modeless
  MessageBox publishes keyboard and pointer results and lets Escape propagate.
- Completion follows the ordered close lifecycle: the result settles, the host
  is cleaned up, and focus is restored.
- `ButtonStyle` propagates across every button layout with Theme fallback,
  publishes change notification, remeasures when padding changes, and
  `ShowAsync` forwards an explicit style to every presented action.
- `Style` resolves through local &rarr; Theme `messageBox` section &rarr; Window
  fallback, updates the frame, message face, and content geometry coherently
  (including after a live Theme swap), and resetting it restores Theme
  ownership.
- `SeparatorStyle` forwards a complete local override to the retained divider
  and resetting it returns the divider to independent Theme ownership rather
  than pinning a resolved value.
- Each of `OkText`, `CancelText`, `YesText`, and `NoText` updates only its
  semantic action in place, preserves the corresponding `MessageBoxResult`,
  default/cancel role, focus, and event order, remeasures every generated action
  to equal widths, keeps ampersand access keys, and is validated before any
  observable mutation.
- The `MessageBoxOptions` overload applies the configured title, layout,
  captions, and style without exposing the generated Buttons or divider.
