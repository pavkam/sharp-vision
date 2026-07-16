# Showcase testing

## Showcase testing

The showcase catalog test fixes the exact 26-page inventory and requires Button
to be the initial page and Canvas to occupy index 1. Every page contains wrapped
marked-Text Overview documentation and builds a fresh, detached, independently
owned live tree. Each entry's tree contains the control named by that sidebar
entry; the Theming page is the deliberate composition-page exception.

Navigation tests require the executable showcase policy to emit xterm any-event
tracking (`1003`) and SGR cell-mouse mode enables before its first frame. They
then drive raw SGR primary-pointer input through the public Application to
select an entry in the intrinsically bordered `Dock` sidebar, move and focus
sidebar selection through decoded arrow input, activate Button through keyboard
input, scroll the main pane with wheel reports, edit TextInput through decoded
text, and retain selection after pixel-aware resize. Its ScrollBar proof
requires an intermediate SGR move to commit a value before the release reaches
the endpoint. A separate startup test requires the first frame to commit, the
initial sidebar entry to take focus, and shutdown to complete without runtime
failure.

The same runtime suite targets an SGR wheel report at the overflowing multiline
`TextInput` specimen. It proves decoded terminal input advances the editor's own
`VerticalOffset` while the enclosing documentation Stack's intrinsic scroll
offset remains unchanged, protecting leaf-first wheel routing from future
regressions.

Virtual-screen assertions render all 26 pages at 30 by 8, 80 by 24, and 140 by
40 cells. They verify selected identity, the `SHARP VISION` sidebar identity,
component navigation, non-default cell colors, page headings, automatic
overflow, semantic text, and every wide-cell continuation relationship. The
discovery helpers traverse every registered owned-control slot and never assume
descendants are limited to `Container.Children`. The checked-in
[live tmux capture](../images/showcase-dashboard.png) is visually reviewed but
does not replace cell, event, focus, resize, or scrolling assertions. It is also
a required live interaction smoke test: it sends Down, proves the initial Button
selection moves to Canvas, and sends Up to return to Button. A no-button SGR
motion report then proves Canvas receives the visible hover marker, and a
terminal leave report clears it. The smoke test next sends independent complete
SGR clicks for Canvas and Button, waiting for each visible page change without
adding a trailing key that could mask input buffering. It also opens and selects
the Figlet font dropdown, then drags the ScrollBar thumb with SGR press, motion,
and release reports, asserting each visible committed value.

Every page must contain a wrapped marked `Text` Overview. Practical-recipe
guidance is optional; when a page supplies it, the text must wrap. Pages compose
`Doc.Example` blocks rather than mandatory property or interaction tables, and
live specimens use intrinsic control chrome when they need a frame.

Canvas has dedicated virtual-screen assertions and must retain its labeled
fixed, percentage, edge-constraint, and clipping stages within the viewport.
Button and Window assertions continue to cover intrinsic composite and
block-glyph shadow properties through their live specimens. The Theming page
also proves that an unprivileged showcase-authored derivative with a custom
render override calls `RenderChrome` before custom content (its caption and
body), preserving its intrinsic rounded frame.

The Prism page test activates its explicit phase-step Button and renders before
and after the mutation. The live diagonal FIGlet control and its content retain
identical bounds, the Prism-only virtual-screen text stays identical, one stored
content cell changes foreground, and the deterministic phase status advances
from zero to one of sixty.

The TextInput rendering suite additionally requires a configured background to
fill every arranged cell, including the empty cells following short text. The
showcase applies that full-surface editor style to every editable, read-only,
password, limited, multiline, and Figlet text input.

Showcase examples compile as production code and use no internal APIs,
reflection shortcuts, fake controls, or rendering behavior unavailable to
library consumers.

The capture renderer has its own ANSI-to-HTML test, while
`scripts/capture-showcase.sh` fails if the Release app exits early, never
renders `SHARP VISION`, `Overview`, and the final Button specimen, or does not
produce a valid PNG.
