# Showcase testing

## Showcase testing

The showcase catalog test contains the exact concrete shipped-control inventory
and fails when a control lacks its own page, wrapped marked-`Text`
documentation, a fresh live example, progressive teaching sections, or a compact
escaped C# excerpt. Each example tree must contain the control named by its
sidebar entry and must be detached and independently owned. Property tables and
separate Practical recipe prose are not required; application-shaped recipes
live beside their specimens.

## Progressive content contract

Every page has at least four subject-specific teaching areas, a live subject
control, one reproducible source excerpt, an application-shaped composition, and
a relevant state, interaction, layout, Unicode, or constrained-size proof. The
content test requires these stable areas:

| Page        | Required teaching areas                                                            |
| ----------- | ---------------------------------------------------------------------------------- |
| Button      | Start here; Commands; Window roles; Chrome and states                              |
| Canvas      | Canvas layout; Constraints; Drawing fundamentals; Useful custom drawing            |
| CheckBox    | Two-state choice; Three-state policy; Marks; Form recipe                           |
| ComboBox    | Start here; Commit versus dismiss; Long choices; Constrained placement             |
| Dock        | Application shell; Order and spacing; Sizing from the remainder; Constrained space |
| FigletText  | Live editor; Font comparison; Layout options; Large output                         |
| Grid        | Track fundamentals; Percentage and limits; Responsive form; Constrained space      |
| List        | Single selection; Selection modes; Templates; Long data                            |
| Menu        | Command menu; Menu bar; Popup composition; Selection and invocation                |
| Overlay     | Layering; Stable ties; Pointer transparency; Clipping                              |
| Popup       | Anchored menu; Placement; Fallback and clamp; Lifecycle                            |
| RadioButton | Named group; Arrow traversal; Unnamed scope; Events                                |
| ScrollBar   | Range anatomy; Input parity; Live range; Tiny rails                                |
| Stack       | Orientation; Mixed sizing; Visibility; Constrained space                           |
| Table       | Column sizing; Interactive cells; Dynamic rows; Boundary states                    |
| Text        | Safe content; Markup; Overflow; Unicode                                            |
| TextInput   | Editing and submission; Selection; Clipboard and history; Multiline                |
| Window      | Frame and title; Shadows; Default and cancel; Boundaries                           |
| Theming     | Application theme; Catalog; Visual states; Third-party controls                    |

The table is a minimum navigation contract, not a maximum example count. Tests
also exercise page-defining behavior: programmatic activation, custom marks,
empty and multiple selection, responsive form layout, dynamic rows, popup
lifecycle order, FIGfont comparison, theme catalog metadata, opposing Canvas
constraints, and custom semantic drawing.

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
`VerticalOffset` while the enclosing page-body documentation `Stack` retains its
previous offset, protecting leaf-first wheel routing from future regressions. A
separate shell test scrolls that body and requires the fixed page
identity/Overview header to retain its exact bounds.

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

Current `Doc.Page` panes use a fixed Surface header followed by wrapped
`Doc.Section` groups in an independently scrolling body. Tests require one
explicit emoji prefix and bold Accent text on every section, bold plain example
titles, dim descriptions, and Info source labels. Each `Doc.Example` contains
actionable marked-`Text` guidance, one live specimen, and optionally a framed C#
excerpt escaped through `Text.Escape`. Tests render special characters in an
excerpt to prove generic syntax, backslashes, and comparison operators remain
literal visible text. Footer geometry tests require Theme, picker, Quit, and the
`Ctrl+C` hint to remain non-overlapping at typical and constrained heights.

Canvas has dedicated layout assertions for fixed, percentage, trailing-edge,
opposing-edge stretch, explicit-size precedence, intrinsic, negative-origin,
layering, clipping, and pointer-transparent stages. Separate custom-control
samples prove line/box topology, shade and quadrants, fill/clear, Unicode clip
repair, exact line/circle/ellipse cells and clipping, deterministic charting,
and routed pointer readout through semantic cells. Grid tests require visible
two-to-one star interiors. Table tests require intrinsic interactive cells,
fully visible headerless shortcut rows, and deliberate both-axis overflow with
aligned chrome. Every sample validates wide-cell continuation ownership under
the [rendering correctness oracle](rendering.md#correctness-oracle). Shadow is
intrinsic control chrome rather than a catalog page: focused control-frame tests
prove composite and block-glyph footprints, clipping, wide-cell styling, and
hit-test exclusion, while Button and Window retain visible composite,
block-glyph, and shadow-disabled variants.

Layer tests drive Above, Below, Left, and Right controls and require the same
open Popup surface to move around one anchor. Popup and Window surfaces must
overlap populated backdrop bounds. Theme showcase tests require a shadowless
baseline and semantic type-styled Button, a concise semantic readout, and no raw
`Glyphs` record formatting in visible text.

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
