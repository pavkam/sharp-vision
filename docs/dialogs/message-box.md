# MessageBox

## Overview

`MessageBox` lives in `SharpVision.Dialogs` and is a retained, measured
[`Window`](../controls/windows/window.md#overview) specialization for short user
decisions. The MessageBox object itself renders the title, the grapheme-safe
wrapped message, the centered action row, and the
[modal presentation](../concepts/modality.md#popup-and-window-presentations)
when shown asynchronously; there is no nested proxy Window.

## API

| Member                 | Type                     | Default                | Description                                                                                                                                                                                 |
| ---------------------- | ------------------------ | ----------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Message`              | `string`                 | required               | Non-null message rendered as grapheme-safe wrapped `Text`.                                                                                                                                  |
| `Title`                | `string`                 | `"Message"`            | Non-null Window title.                                                                                                                                                                      |
| `Buttons`              | `MessageBoxButtons`      | `MessageBoxButtons.Ok` | Defined semantic action layout.                                                                                                                                                             |
| `Style`                | `MessageBoxStyle?`       | `null`                 | A complete local `MessageBoxStyle` owning the frame, message face, and content geometry; `null` follows the code-owned default, itself falling back to the active Theme's `window` section. |
| `ActualStyle`          | `MessageBoxStyle`        | resolved               | The resolved `MessageBoxStyle` currently applied.                                                                                                                                           |
| `ButtonStyle`          | `ButtonStyle?`           | `null`                 | A complete local `ButtonStyle` applied to every generated action; `null` lets each Button use its own semantic profile.                                                                     |
| `ActualButtonStyle`    | `ButtonStyle`            | resolved               | The resolved `ButtonStyle` currently applied to every generated action.                                                                                                                     |
| `SeparatorStyle`       | `SeparatorStyle?`        | `null`                 | A complete local `SeparatorStyle` applied to the divider above the action row; `null` follows the code-owned default, itself falling back to the active Theme's `control` section.          |
| `ActualSeparatorStyle` | `SeparatorStyle`         | resolved               | The resolved `SeparatorStyle` currently applied to the divider.                                                                                                                             |
| `OkText`               | `string`                 | `"&OK"`                | Non-null caption for the OK action, when the current layout includes one.                                                                                                                   |
| `CancelText`           | `string`                 | `"&Cancel"`            | Non-null caption for the Cancel action, when the current layout includes one.                                                                                                               |
| `YesText`              | `string`                 | `"&Yes"`               | Non-null caption for the Yes action, when the current layout includes one.                                                                                                                  |
| `NoText`               | `string`                 | `"&No"`                | Non-null caption for the No action, when the current layout includes one.                                                                                                                   |
| `SelectedResult`       | `MessageBoxResult`       | default enum value     | The last selection made on a directly mounted MessageBox.                                                                                                                                   |
| `HasSelectedResult`    | `bool`                   | `false`                | Distinguishes "no modeless selection yet" from the enum's default value.                                                                                                                    |
| `ResultSelected`       | `EventHandler?`          | no subscribers         | Raised when a directly mounted MessageBox takes a keyboard or pointer choice.                                                                                                               |
| `ShowAsync(...)`       | `Task<MessageBoxResult>` | —                      | Presents one temporary modal MessageBox and returns its semantic result.                                                                                                                    |

`MessageBoxButtons` defines the supported layouts:

- `Ok`: OK
- `OkCancel`: OK, Cancel
- `YesNo`: Yes, No
- `YesNoCancel`: Yes, No, Cancel

`MessageBoxResult` is `Ok`, `Cancel`, `Yes`, or `No`. Activating a button
completes the returned task with that button's semantic result. Closing the
frame or dismissing the surface completes the task with `Cancel`.

> [!NOTE]
>
> `Cancel` is the dismissal result for every layout, including
> `MessageBoxButtons.Ok`: Escape, a close request, forced detach, disposal, and
> an outer modal scope unwinding all complete the task with `Cancel`. An awaited
> OK-only box can therefore return `Cancel` — a completed `ShowAsync` is not
> proof the user pressed OK.

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

`owner` must resolve to an owning Screen, an explicit `Overlay`, or the
outermost `Overlay` ancestor. In a hosted application the helper adds one
temporary MessageBox to the Screen's private presentation slot, so a bounded
card, pane, or showcase stage can identify ownership without constraining the
modal surface or exposing framework children. Outside a Screen, an explicit or
outermost `Overlay` is still a supported host; any other owner without an
`Overlay` ancestor fails to resolve and `ShowAsync` throws. The helper enters a
Window modal presentation through Dialog's shared owner-facing transaction with
outside interaction ignored. On normal completion it publishes `Closing` and
`Closed`, removes and disposes the MessageBox, and then settles the returned
task. Calls are dispatcher-affine; disposed owners are rejected before host
resolution.

A directly mounted MessageBox is modeless. Activating a button with the keyboard
or pointer updates `SelectedResult`, sets `HasSelectedResult`, and raises
`ResultSelected` without removing or disposing the surface. Escape still selects
`Cancel` in this mode: the MessageBox's own routed handler consumes the stroke
and publishes the cancel result, so an ancestor never sees a modeless
MessageBox's Escape. If a result-property observer synchronously invokes a newer
button choice, the newer choice supersedes the outer publication and is the only
one subsequently raised through `ResultSelected`.

## Interaction

The inherited Window uses the dialog defaults for a paired frame and a centered
header, but overrides the rest: the box is movable rather than fixed, renders no
close control (`CanClose` is `false`), and implements its own Escape-to-Cancel
handling rather than the inherited Escape-close fallback, which is dead code
here because that fallback also requires `CanClose`.

The window sizes itself from its title, wrapped message, and button labels, but
never past **80% of the available presentation-host width, floored at the
32-cell minimum width** - the Overlay or Screen plane `ShowAsync` presents into,
not the bounds of a small owner control. On a host narrower than 40 columns the
floor wins and the cap collapses to the host's own width. This is a cap, not a
target: a short message stays compact, and only a message long enough to need
the room grows the box toward that width. The cap is recomputed on every layout
pass from the incoming measure constraint, so a live presentation resize
retargets it automatically with no explicit resize handling. Its content is a
two-row grid: the top row is an intrinsic message area whose centered, wrapped
text begins two empty interior rows below the title edge, and the bottom row is
the shared dialog action bar. The action bar renders a horizontal divider
spanning the content width directly against the centered action row. Moving the
Window therefore only adds placement offsets; its measured height does not
change. Message text wraps within the capped width by grapheme cluster. The
window keeps a 32-by-8-cell minimum footprint for consistent dialog
proportions - never forced past a host too small to accommodate it - and is
centered on both axes across that host.

The window also never grows past a **20-cell height ceiling**. The message area
itself carries its own, narrower height ceiling well under that budget, and
hosts its wrapped text in a vertically scrolling region beneath it - a
`ScrollBar` fades in once the wrapped text overflows the available rows, and the
mouse wheel scrolls it. A short message never scrolls; the bar only appears once
wrapped content genuinely exceeds the rows the message area is given. Because
the message area's own ceiling - not a floor on the action bar - is what keeps
the dialog within budget, the action bar never has to compete for space at all:
its divider and buttons always keep their full, natural size, regardless of how
long the message grows.

Focus sits on the action buttons, which are siblings of the scrolling message
host rather than its ancestors, so normal routed key handling never reaches it.
MessageBox forwards Up, Down, Page Up, Page Down, Home, and End directly into
the message host instead - using the same line and page step `Container`'s own
key handling would - so a keyboard-only user can still scroll a long message
without moving focus off the buttons. Left and Right are not forwarded: the
message area only scrolls vertically, so there is no horizontal extent for them
to move. A short message that never overflows leaves all of these keys
unhandled, and a modifier beyond Shift or a lock key (Ctrl, Alt, Super, Hyper,
or Meta) makes the whole chord ineligible, so neither swallows a key meant for
something else.

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
layout. `MessageBoxStyle` declares no `styles.*` theme key of its own: the frame
follows `window`'s role section with the standard local &rarr; fallback
precedence, while `MessageFace`/`MessageMargin`/`ActionBarMargin` stay
code-owned, reachable only through a locally assigned `Style`; `Style` and
`ActualStyle` follow the same contract as every other themed control. A live
Theme swap still updates the frame on the next layout pass, even without a local
`Style` - only the code-owned members need one to move at all.

`CloseGlyph`, `CloseLeftBracket`, `CloseRightBracket`, and the four
`CloseMarkColor`/`CloseMarkActiveColor`/`CloseMarkPressedColor`/`CloseMarkDisabledColor`
fields are inherited from `WindowStyle` and resolve through `MessageBoxStyle`
itself, copied verbatim from the fallback's own resolved `window` role section

- MessageBox declares no `styles.*` theme key of its own, so a theme's `window`
  section drives the close mark MessageBox renders when `CanClose` is set to
  `true`, and only a locally assigned `Style` can give MessageBox a close mark
  independent of `window` (see [Interaction](#interaction)).

The divider above the action row is a canonical `Separator` with its own
`SeparatorStyle`/`ActualStyle` contract (declaring no `styles.*` theme key of
its own, it falls back to the generic control role's code-owned chrome).
`MessageBox.SeparatorStyle` forwards a complete local override to the retained
divider through the same part-style binding `ButtonStyle` already uses; `null`
returns the divider to independent Theme ownership instead of pinning a
previously resolved value.

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
- The outer width never exceeds the larger of 80% of the available presentation
  width and the 32-cell minimum, stays compact for short messages, and
  recomputes the cap after a presentation resize. A horizontal divider
  consistently separates the message from the action row.
- A message long enough to hit the 20-cell height ceiling scrolls within its
  message area instead of overflowing the window or truncating unreachably; the
  message area's own ceiling — not a floor on the action bar — is what keeps the
  divider and button row at their natural size.
- Shown from a bounded owner, it centers across the whole application host,
  applies the deliberate message offset, and centers its captions.
- Modal presentation honors default-button and Escape activation. A modeless
  MessageBox publishes keyboard and pointer results, and its own handler
  consumes Escape as a `Cancel` selection.
- Completion follows the ordered close lifecycle: the result settles, the host
  is cleaned up, and focus is restored.
- `ButtonStyle` propagates across every button layout with Theme fallback,
  publishes change notification, remeasures when padding changes, and
  `ShowAsync` forwards an explicit style to every presented action.
- `Style` resolves through local &rarr; code-owned completion of the Theme's
  `window` fallback, updates the frame coherently after a live Theme swap
  (message face and content geometry stay code-owned unless a local `Style`
  moves them), and resetting it restores that fallback-derived presentation.
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
