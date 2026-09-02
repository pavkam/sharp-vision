# Task 4 implementation report

## RED

- `MenuTests.Constructor_WhenCreated_UsesConfigurableFifteenCellMinimumWidth` failed because `Menu.MinWidth` was `10cells`.
- The new ContextMenu and Breadcrumb overflow tests failed with `10cells` and `1cells`, respectively.
- The new CommandBar mounted tests failed because the connected popup omitted the expected top/bottom frame edge.

## GREEN

- `Menu` now defaults to `Length.Cells(15)` while callers can set a narrower inherited value.
- CommandBar and Breadcrumb overflow menus no longer override the shared default.
- CommandBar overflow no longer connects its chrome to the trigger, so both below and flipped-above presentations retain a full frame.
- Focused serial checks passed: MenuTests (150), MenuSurfaceTests (39), ContextMenuSurfaceTests (26), BreadcrumbSurfaceTests (23), and CommandBarSurfaceTests (19).

## Files

- Production: Menu, CommandBar, BreadcrumbOverflowButton.
- Coverage: menu, context-menu, breadcrumb, and command-bar fixtures.
- Documentation and Showcase prose: Menu, CommandBar, Breadcrumb pages and panes.

## Concerns

- `make format && make lint && make build && make test` was started, but the shared environment still has those long-running commands active; no final aggregate result was available before this report.
