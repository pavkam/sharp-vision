# Showcase testing

## Showcase testing

The showcase catalog test contains the exact concrete shipped-control inventory
and fails when a control lacks its own page, typed RichText documentation,
meaningful property descriptions, interaction guidance, or fresh live example.
Each example tree must contain the control named by its sidebar entry and must
be detached and independently owned.

Navigation tests require the executable showcase policy to emit VT200 and SGR
cell-mouse mode enables before its first frame. They then drive raw SGR primary
pointer input through the public Application to select a framed dashboard entry,
move and focus sidebar selection through decoded arrow input, activate Button
through keyboard input, scroll the main pane with wheel reports, edit TextInput
through decoded text, and retain selection after pixel-aware resize. A separate
startup test requires the first frame to commit, the initial sidebar entry to
take focus, and shutdown to complete without runtime failure.

Virtual-screen assertions render every page at 30 by 8, 80 by 24, and 140 by 40
cells. They verify selected identity, the `SHARP VISION` sidebar identity,
component navigation, non-default cell colors, page headings, automatic
overflow, semantic text, and every wide-cell continuation relationship. The
checked-in [live tmux capture](../images/showcase-dashboard.png) is visually
reviewed but does not replace cell, event, focus, resize, or scrolling
assertions. It is also a required live interaction smoke test: it sends Down,
then independent complete SGR clicks for Canvas and Button, waiting for each
visible page change without adding a trailing key that could mask input
buffering. It also opens and selects the Figlet font dropdown, then drags the
ScrollBar thumb with SGR press, motion, and release reports, asserting each
visible committed value.

Every page must also contain a Practical recipe: a full-width “When to use it”
card followed by bordered “Live example” and “Responsive” columns. The compact
card descriptions use word-aware Text wrapping; the surrounding headings,
section labels, property documentation, and interaction guidance use RichText,
which defaults to word wrapping. The page test protects that responsive default
alongside the recipe structure.

Canvas and Shadow have dedicated virtual-screen assertions: Canvas must retain
its labeled fixed, percentage, edge-constraint, and clipping stages within the
viewport, while Shadow must render separate composite and block-glyph stages
with a readable light-shade footprint.

The TextInput rendering suite additionally requires a configured background to
fill every arranged cell, including the empty cells following short text. The
showcase applies that full-surface editor style to every editable, read-only,
password, limited, multiline, and Figlet text input.

Showcase examples compile as production code and use no internal APIs,
reflection shortcuts, fake controls, or rendering behavior unavailable to
library consumers.

The capture renderer has its own ANSI-to-HTML test, while
`scripts/capture-showcase.sh` fails if the Release app exits early, never
renders Overview and Examples, or does not produce a valid PNG.
