# Control-wide text selection delivery plan

## Goal

Move the reusable cross-child selection system from `Document` into
`ControlBase` while preserving the established `Document`, `CodeView`, and
`TextInput` APIs.

## Decisions

- Use text-qualified inherited names because item, date, tab, tree, and table
  controls already own unrelated `Selection` APIs.
- Default `IsTextSelectionEnabled` to `false`; existing text-specialized
  controls opt in explicitly.
- Keep editor mutation (`CutSelection`, replacement, undo, and paste) off the
  base class.
- Preserve authoritative projections and typed rendering in specialized controls
  while sharing map, ownership, routing, clipboard, and lifecycle contracts.

## Delivery phases

1. Freeze the inherited API and default opt-out with detached tests.
2. Move the immutable semantic map, source identity, glyph indexes, and default
   retained-child aggregation into `SharpVision`.
3. Add nearest-owner pointer arbitration, Unicode-safe keyboard navigation,
   bounded autoscroll, final adornment rendering, lifecycle cleanup, and
   application clipboard routing.
4. Adapt `Document`, delete its duplicate map types, and keep link-specific
   click arbitration and document projection only.
5. Adapt `CodeView` and `TextInput`, preserving convenience APIs, event order,
   password policy, and editor-only mutation.
6. Update normative concepts and control pages, add an ordinary composite to
   Showcase, accept intentional public API snapshots, and run repository gates.

## Verification

- Focused RED/GREEN tests cover the inherited API, source replacement,
  cross-child drag, Unicode boundaries, visual-row keyboard movement,
  autoscroll, final cell styles, specialized adapters, and clipboard routing.
- Existing complete `Document`, `CodeView`, and `TextInput` suites remain
  regression oracles.
- Completion requires `make format`, `make lint`, `make build`, and `make test`
  with no warnings or failures.
