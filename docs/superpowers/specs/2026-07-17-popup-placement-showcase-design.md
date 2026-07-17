# Popup placement showcase design

## Goal

Replace the visually ambiguous fake-editor placement specimen with a focused
diagram that makes the anchor, preferred directions, popup surface, and current
request immediately understandable.

## Visual structure

The specimen remains a retained SharpVision control tree and uses only public
layout, styling, input, and popup APIs.

- A rounded bordered stage with a semantic `Surface` background separates the
  diagram from the surrounding example surface.
- A bordered `⚓ Anchor` button occupies the center.
- Four consistently bordered direction buttons form a cross around it:
  `↑ Above`, `← Left`, `Right →`, and `↓ Below`.
- Activating a direction button opens the existing popup relative to the center
  anchor using that preferred side.
- The popup content reads `Placement preview` and retains its own rounded
  border, making it distinct from both the anchor and the direction buttons.
- A separated footer reports the requested side without resembling editor status
  chrome.

The fake menu bar, file names, workspace label, and editor status text are
removed because they do not explain popup placement.

## Behavior

Pointer, Enter, and Space activation continue through ordinary `Button` and
`Popup` behavior. The anchor remains the popup's placement target; direction
buttons only choose `PopupPlacement` and open the preview. Popup fallback and
clamping behavior are unchanged.

## Tests

Showcase tests will prove:

- the placement stage has visible rounded intrinsic chrome and a semantic
  surface;
- the center label contains the anchor emoji;
- every direction control has a visible border and directional glyph;
- activating a direction opens the popup with the requested placement; and
- the rendered specimen contains the framed diagram and visible popup preview.

The focused showcase tests run first, followed by formatting, lint, build, and
the full repository test gate.
