# MessageBox

## MessageBox contract

`MessageBox` lives in `SharpVision.Dialogs` and is a retained, measured
[`Window`](../controls/windows/window.md#window-contract) specialization for
short user decisions. The MessageBox object itself renders the title,
grapheme-safe wrapped message, centered action row, and
[modal presentation](../concepts/modality.md#popup-and-window-presentations)
when shown asynchronously; there is no nested proxy Window.

## API

| Member              | Default                | Contract                                                                 |
| ------------------- | ---------------------- | ------------------------------------------------------------------------ |
| `Message`           | required               | Non-null message rendered as grapheme-safe wrapped `Text`.               |
| `Title`             | `"Message"`            | Non-null Window title.                                                   |
| `Buttons`           | `MessageBoxButtons.Ok` | Defined semantic action layout.                                          |
| `SelectedResult`    | default enum value     | Last directly mounted selection.                                         |
| `HasSelectedResult` | `false`                | Distinguishes no modeless selection from an enum default.                |
| `ResultSelected`    | no subscribers         | Publishes a directly mounted keyboard or pointer choice.                 |
| `ShowAsync(...)`    | —                      | Presents one temporary modal MessageBox and returns its semantic result. |

`MessageBoxButtons` defines the supported layouts:

- `Ok`: OK
- `OkCancel`: OK, Cancel
- `YesNo`: Yes, No
- `YesNoCancel`: Yes, No, Cancel

`MessageBoxResult` returns `Ok`, `Cancel`, `Yes`, or `No`. A button activation
completes the returned task with its semantic result. Closing the frame or
dismissing the surface completes with `Cancel`.

The constructors accept a non-null `message`, an optional non-null `title`
(defaulting to `Message`), and a defined `MessageBoxButtons` value. The static
overloads are:

```csharp
var result = await MessageBox.ShowAsync(owner, "Delete the draft?");
var result = await MessageBox.ShowAsync(owner, "Delete the draft?", "Confirm");
var result = await MessageBox.ShowAsync(owner, "Delete the draft?", MessageBoxButtons.YesNo);
var result = await MessageBox.ShowAsync(
    owner,
    "Delete the draft?",
    "Confirm",
    MessageBoxButtons.YesNoCancel);
```

`owner` must resolve to an owning Screen, an explicit container, or an outermost
fallback container. In a hosted application, the helper adds one temporary
MessageBox to the Screen's private presentation slot. A bounded card, pane, or
showcase stage therefore identifies ownership without constraining the modal
surface or exposing framework children. Outside a Screen, an explicit or
outermost container remains a supported host. The helper enters a Window modal
presentation with outside interaction ignored. Normal completion publishes
`Closing` and `Closed`, removes and disposes the MessageBox, and then settles
the returned task. Calls are dispatcher-affine.

A directly mounted MessageBox is modeless. Keyboard or pointer button activation
updates `SelectedResult`, sets `HasSelectedResult`, and raises `ResultSelected`
without removing or disposing the surface. Escape remains unhandled by the
MessageBox in this mode so an ancestor may apply its own policy.

## Interaction

The inherited Window uses dialog defaults: paired frame, centered header, fixed
placement, leading close control, and Escape-close fallback. Its desired size
comes from its title, wrapped message, and button labels. Its content is a
two-row grid: the top row is an intrinsic message area whose centered, wrapped
text begins two empty interior rows below the title edge; the bottom row is an
intrinsic action row separated by three cells. Moving the Window therefore adds
placement offsets without changing its measured height. The button group is
centered horizontally and its buttons share the widest label width. Button
captions use the Button default centered text alignment, and dialog composition
does not override their kind, face, border, or shadow. Message text is measured
against the application host's available width and wraps without a hard-coded
box width. The window has a 32-by-8-cell minimum footprint for consistent dialog
proportions, and is centered on both axes across that host. Focus enters the
first affirmative button, Tab remains inside the modal plane, and outside
pointer, keyboard, text, paste, and wheel input is consumed by the shared
modality manager.

Generated actions declare conventional `&OK`, `&Cancel`, `&Yes`, and `&No`
[access keys](../concepts/access-keys.md#focus-and-semantic-actions). Message
prose remains ordinary rich `Text` and does not interpret ampersands.

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

Tests cover enum stability, argument validation, title/message retention,
dialog-role composition, message wrapping under a small viewport, all four
button layouts, application-wide centering from a bounded owner, deliberate
message offset, centered captions, modal default and Escape activation, modeless
keyboard and pointer result publication, modeless Escape propagation, ordered
close lifecycle, result completion, host cleanup, and focus restoration.
