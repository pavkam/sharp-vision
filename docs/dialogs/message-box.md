# MessageBox

## Overview

`MessageBox` lives in `SharpVision.Dialogs` and is a retained, measured
[`Window`](../controls/windows/window.md#overview) specialization for short user
decisions. The MessageBox object itself renders the title, the grapheme-safe
wrapped message, the centered action row, and the
[modal presentation](../concepts/modality.md#popup-and-window-presentations)
when shown asynchronously; there is no nested proxy Window.

## API

| Member              | Default                | Description                                                                                                             |
| ------------------- | ---------------------- | ----------------------------------------------------------------------------------------------------------------------- |
| `Message`           | required               | Non-null message rendered as grapheme-safe wrapped `Text`.                                                              |
| `Title`             | `"Message"`            | Non-null Window title.                                                                                                  |
| `Buttons`           | `MessageBoxButtons.Ok` | Defined semantic action layout.                                                                                         |
| `ButtonStyle`       | `null`                 | A complete local `ButtonStyle` applied to every generated action; `null` lets each Button use its own semantic profile. |
| `ActualButtonStyle` | resolved               | The resolved `ButtonStyle` currently applied to every generated action.                                                 |
| `SelectedResult`    | default enum value     | The last selection made on a directly mounted MessageBox.                                                               |
| `HasSelectedResult` | `false`                | Distinguishes "no modeless selection yet" from the enum's default value.                                                |
| `ResultSelected`    | no subscribers         | Raised when a directly mounted MessageBox takes a keyboard or pointer choice.                                           |
| `ShowAsync(...)`    | —                      | Presents one temporary modal MessageBox and returns its semantic result.                                                |

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
```

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

The window sizes itself from its title, wrapped message, and button labels. Its
content is a two-row grid: the top row is an intrinsic message area whose
centered, wrapped text begins two empty interior rows below the title edge, and
the bottom row is an intrinsic action row separated by three cells. Moving the
Window therefore only adds placement offsets; its measured height does not
change. Message text is measured against the application host's available width
and wraps without a hard-coded box width. The window keeps a 32-by-8-cell
minimum footprint for consistent dialog proportions and is centered on both axes
across that host.

The button group is centered horizontally and its buttons share the widest
label's width. Captions use the Button default centered text alignment, and
dialog composition does not select a Button kind. `ButtonStyle` overrides face,
border, shadow, and padding for every generated action, while `null` follows the
active Theme. Assigning it after construction updates every retained button
coherently and remeasures when padding or chrome changes. `ShowAsync` accepts
the same style without exposing the underlying Button instances.

Focus enters the first affirmative button, Tab stays inside the modal plane, and
pointer, keyboard, text, paste, and wheel input outside the dialog is consumed
by the shared modality manager.

Generated actions declare the conventional `&OK`, `&Cancel`, `&Yes`, and `&No`
[access keys](../concepts/access-keys.md#focus-and-semantic-actions). Message
prose stays ordinary rich `Text` and does not interpret ampersands.

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
- Shown from a bounded owner, it centers across the whole application host,
  applies the deliberate message offset, and centers its captions.
- Modal presentation honors default-button and Escape activation. A modeless
  MessageBox publishes keyboard and pointer results and lets Escape propagate.
- Completion follows the ordered close lifecycle: the result settles, the host
  is cleaned up, and focus is restored.
- `ButtonStyle` propagates across every button layout with Theme fallback,
  publishes change notification, remeasures when padding changes, and
  `ShowAsync` forwards an explicit style to every presented action.
