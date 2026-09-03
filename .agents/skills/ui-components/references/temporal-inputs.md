# Temporal inputs

## Normative documentation

- [InputBase authoring API](../../../../docs/controls/input-base.md#overview)
- [DateInput](../../../../docs/controls/input/date-input.md#overview)
- [TimeInput](../../../../docs/controls/input/time-input.md#overview)
- [DateTimeInput](../../../../docs/controls/input/date-time-input.md#overview)
- [Keyboard modifier policy](../../../../docs/concepts/input-routing.md#keyboard-modifier-policy)

## Inspection route

Read the three temporal controls together with `InputBase`,
`SegmentFieldBehavior`, their nearest tests, and all three Showcase panes.
Compare repeated focus, pointer, key classification, rendering, and
popup-opening plumbing before leaving behavior in one concrete control.

## Evidence

- Add direct state tests for format-driven arithmetic and modifier handling.
- Add mounted-surface tests when cells, pointer selection, focus, popup routing,
  or placeholder styling changes.
- Drive the live Showcase through tmux at wide, normal, and narrow sizes. Cover
  nullable placeholders, fractional formats, segment boundaries, and both
  keyboard and pointer popup paths.
- Run the focused `SharpVision.Tests` classes serially, then the repository
  quality gates.

## Traps

- A .NET format token is not editable until parsing, rendering, digit entry,
  stepping, clearing, placeholders, and tests agree on its precision.
- Uppercase `F` fractions may format to fewer cells, including zero; reserve the
  declared run width so the editable segment remains focusable and targetable.
- Command-modified scalar keys stay available to the application; lock modifiers
  are incidental.
- Preserve every `TerminalStyle` channel when adding Reverse or Dim attributes.
- Calendar acceptance preserves the time portion, sub-second ticks, and
  `DateTimeKind` unless the documented bound repair requires otherwise.
