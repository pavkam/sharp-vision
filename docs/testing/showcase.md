# Showcase testing

## Showcase testing

The showcase catalog test contains the exact concrete shipped-control inventory
and fails when a control lacks its own page, wrapped marked-Text documentation,
meaningful property descriptions, structured interaction rows, or fresh live
example. Each example tree must contain the control named by its sidebar entry
and must be detached and independently owned.

Navigation tests require the executable showcase policy to emit xterm any-event
tracking (`1003`) and SGR cell-mouse mode enables before its first frame. They
then drive raw SGR primary-pointer input through the public Application to
select a framed dashboard entry, move and focus sidebar selection through
decoded arrow input, activate Button through keyboard input, scroll the main
pane with wheel reports, edit TextInput through decoded text, and retain
selection after pixel-aware resize. Its ScrollBar proof requires an intermediate
SGR move to commit a value before the release reaches the endpoint. A separate
startup test requires the first frame to commit, the initial sidebar entry to
take focus, and shutdown to complete without runtime failure.

The same runtime suite targets an SGR wheel report at the overflowing multiline
`TextInput` specimen. It proves decoded terminal input advances the editor's own
`VerticalOffset` while the enclosing documentation Stack's intrinsic scroll
offset remains unchanged, protecting leaf-first wheel routing from future
regressions.

Virtual-screen assertions render every page at 30 by 8, 80 by 24, and 140 by 40
cells. They verify selected identity, the `SHARP VISION` sidebar identity,
component navigation, non-default cell colors, page headings, automatic
overflow, semantic text, and every wide-cell continuation relationship. The
checked-in [live tmux capture](../images/showcase-dashboard.png) is visually
reviewed but does not replace cell, event, focus, resize, or scrolling
assertions. It is also a required live interaction smoke test: it sends Down,
then a no-button SGR motion report, proves Canvas receives the visible hover
marker, and proves a terminal leave report clears it. It next sends independent
complete SGR clicks for Canvas and Button, waiting for each visible page change
without adding a trailing key that could mask input buffering. It also opens and
selects the Figlet font dropdown, then drags the ScrollBar thumb with SGR press,
motion, and release reports, asserting each visible committed value.

Every page must also contain a Practical recipe: one borderless, full-width,
word-wrapped marked `Text` narrative that explains when to use the control,
describes each supported interaction path, and explains how resizing affects the
page. Examples remain the place for bordered live specimens. The Interaction
section is a standalone Table with Input, Behavior, and Result columns rather
than another prose card. The page test protects the narrative's borderless
structure alongside its responsive wrapping.

Canvas has dedicated virtual-screen assertions and must retain its labeled
fixed, percentage, edge-constraint, and clipping stages within the viewport.
Button and Window assertions continue to cover intrinsic composite and
block-glyph shadow properties through their live specimens.

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
