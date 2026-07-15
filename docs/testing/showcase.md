# Showcase testing

## Showcase testing

The showcase catalog test contains the exact concrete shipped-control inventory
and fails when a control lacks its own page, typed RichText documentation, or a
fresh live example. Each example tree must contain the control named by its
sidebar entry and must be detached and independently owned. Property tables,
interaction tables, and Practical recipe sections are optional page content, not
catalog preconditions.

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
`VerticalOffset` while the enclosing scrolling documentation `Stack` retains its
previous offset, protecting leaf-first wheel routing from future regressions.

Virtual-screen assertions render every page at 30 by 8, 80 by 24, and 140 by 40
cells. They verify selected identity, the `SHARP VISION` sidebar identity,
component navigation, non-default cell colors, page headings, automatic
overflow, semantic text, and every wide-cell continuation relationship. The live
tmux smoke test supplements but does not replace cell, event, focus, resize, or
scrolling assertions. It sends Down, then a no-button SGR motion report, proves
Canvas receives the visible hover marker, and proves a terminal leave report
clears it. It next sends independent complete SGR clicks for Canvas and Button,
waiting for each visible page change without adding a trailing key that could
mask input buffering. It also opens and selects the Figlet font dropdown, then
drags the ScrollBar thumb with SGR press, motion, and release reports, asserting
each visible committed value.

Current `Doc.Page` panes use an Overview paragraph followed by labeled live
examples with inline RichText descriptions. A pane may still supply a Practical
recipe or structured interaction data when that improves the example; tests
require any supplied recipe to use word wrapping, but do not require either
optional section on every page.

Canvas has dedicated virtual-screen assertions for its labeled fixed,
percentage, edge-constraint, and clipping stages within the viewport. Shadow is
intrinsic control chrome rather than a catalog page: focused control-frame tests
prove composite and block-glyph footprints, clipping, wide-cell styling, and
hit-test exclusion, while the Button page retains visible composite,
block-glyph, and shadow-disabled variants.

The TextInput rendering suite additionally requires a configured background to
fill every arranged cell, including the empty cells following short text. The
showcase applies that full-surface editor style to every editable, read-only,
password, limited, multiline, and Figlet text input.

Showcase examples compile as production code and use no internal APIs,
reflection shortcuts, fake controls, or rendering behavior unavailable to
library consumers.

The ANSI-to-HTML capture renderer has its own conversion test. Live interaction
correctness remains owned by the application and tmux smoke tests above rather
than a checked-in image artifact.
