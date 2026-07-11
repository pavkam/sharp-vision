# Showcase testing

## Showcase testing

The showcase catalog test contains the exact concrete shipped-control inventory
and fails when a control lacks its own page, typed RichText documentation,
meaningful property descriptions, interaction guidance, or fresh live example.
Each example tree must contain the control named by its sidebar entry and must
be detached and independently owned.

Navigation tests drive raw SGR pointer input through the public Application,
select representative pages, activate Button through keyboard input, scroll the
main pane with wheel reports, edit TextInput through decoded text, and retain
selection after pixel-aware resize. A separate startup test requires the first
frame to commit and shutdown to complete without runtime failure.

Virtual-screen assertions render every page at 30 by 8, 80 by 24, and 140 by 40
cells. They verify selected identity, page headings, automatic overflow,
semantic text, and every wide-cell continuation relationship. The checked-in
[live tmux capture](../images/showcase-border.png) is visually reviewed but does
not replace cell, event, focus, resize, or scrolling assertions.

Showcase examples compile as production code and use no internal APIs,
reflection shortcuts, fake controls, or rendering behavior unavailable to
library consumers.

The capture renderer has its own ANSI-to-HTML test, while
`scripts/capture-showcase.sh` fails if the Release app exits early, never
renders Overview and Examples, or does not produce a valid PNG.
