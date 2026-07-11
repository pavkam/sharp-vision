# Showcase testing

## Showcase testing

The showcase catalog test reflects the shipped public control inventory and
fails when a control lacks a registered page, RichText documentation, or
interactive example.

Navigation tests drive keyboard and pointer input through public runtime paths,
select every page, interact with representative variants, and assert live event
and state log entries. Resize tests cover documented minimum, typical, and large
terminal sizes plus sidebar collapse/restore and automatic scrolling.

Virtual-screen assertions verify semantic cells, styles, focus, clipping, and
scrollbar presence. Small reviewed snapshots may supplement these assertions.
The test never treats a snapshot update as automatic approval.

Showcase examples compile as production code and use no internal APIs,
reflection shortcuts, fake controls, or rendering behavior unavailable to
library consumers.
