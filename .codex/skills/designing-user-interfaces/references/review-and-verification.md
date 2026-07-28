# Review and Verification

## Design review

Check the surface in this order:

1. Information hierarchy: the primary task is obvious and supporting text is
   quieter.
2. Layout semantics: each panel matches the relationship among its children.
3. Alignment: labels, fields, actions, and status text share intentional edges.
4. Rhythm: spacing is consistent; margins and padding are not double-counted.
5. Chrome: every border and shadow communicates a boundary or depth.
6. Responsiveness: flexible regions absorb growth and essential controls remain
   reachable when narrow.
7. Interaction: focus order follows reading order; keyboard and pointer produce
   equivalent actions.

## Required viewport cases

Render and interact with the same retained instance at:

- a narrow size that forces flexible content toward its minimum;
- the intended normal size;
- a wide size that exposes accidental fixed widths or awkward empty space;
- a shorter size when Windows, Popups, menus, or scrolling are present;
- a resize after moving or opening a transient surface.

Test longer labels, empty values, wrapped validation, collapsed optional rows,
wide grapheme clusters, and disabled/default/cancel states where relevant.

## Observable assertions

Prefer mounted surface tests that assert:

- exact shared X positions and widths for form fields;
- aligned trailing edges for related actions;
- growth of Star content after widening;
- containment and non-negative geometry after shrinking;
- final border, shadow, text, and wide-cell continuation cells;
- hit targets and focus after final arranged geometry;
- modal outside-input consumption and focus restoration;
- Window position after drag followed by terminal resize.

Do not assert private layout helpers when public bounds and rendered cells prove
the contract.

## Professional finish checklist

- No copied magic widths used only for alignment.
- No Canvas positioning where Grid/Dock/Stack expresses the relationship.
- No redundant border/shadow wrappers.
- No action without a handler, shortcut, or documented unavailable state.
- No Popup or Window detached from the mounted tree.
- No normal-size-only proof.
- No stale cells, clipped half-glyphs, unreachable actions, or layout loops.

## Repository verification

Run the narrowest mounted fixtures during iteration, then:

```bash
make format
make lint
make build
make test
```

Treat the UI as incomplete until implementation, normative docs, tests, and the
showcase/example all agree.
