# Collections and Navigation

## Load this reference when

Changing ItemsControl, ScrollableItemsControl, ListView, TreeView, Table,
TabControl, ComboBox item semantics, selection, current item, incremental
collections, templates, or navigation.

## Normative documentation

- [ItemsControl](../../../../docs/controls/items-control.md#overview)
- [ScrollableItemsControl](../../../../docs/controls/items-control.md#scrollableitemscontrol)
- [ListView](../../../../docs/controls/collections/list-view.md#overview)
- [TreeView](../../../../docs/controls/collections/tree-view.md#overview)
- [Table](../../../../docs/controls/layout/table.md#overview)
- [TabControl](../../../../docs/controls/collections/tab-control.md#overview)
- [NavigationView](../../../../docs/controls/navigation/navigation-view.md#overview)
- [Data-binding proof](../../../../docs/testing/controls-integration.md#data-binding-proof)

## Code map

- Semantic collections: `src/SharpVision/Controls/Collections/`
- Navigation: `src/SharpVision/Navigation/`
- Tests: matching `Controls/Collections/` and `Navigation/` folders
- Showcase: collection and navigation panes under `examples/Showcase/Panes/`

## Workflow

1. Separate semantic items and selection from private presentation controls.
2. Define identity, ordering, duplicates, replacement, current/selected state,
   keyboard/pointer parity, and collection-delta behavior.
3. Test incremental add/remove/move/replace/reset, resize, scrolling, focus,
   disabled items, and unavailable selection.
4. Keep selection notification and binding order deterministic.

## Project-specific traps

- Do not expose the private presentation host through `Children`.
- A single-host scrolling item owner derives from `ScrollableItemsControl`; keep
  extent, offsets, scroll policy, scrollbar style, and `ScrollChanged` there
  instead of repeating forwarding members in each sealed control.
- A forwarded event identifies the semantic owner as sender, never the retained
  host that implemented it.
- Do not rebuild every item for a semantic delta that can preserve identity.
- Directional navigation and Tab navigation are separate contracts.
- Range-capable selectors keep Shift keyboard movement aligned with pointer and
  programmatic range selection. Linear wrapping is explicit and opt-in.

## Focused verification

```bash
dotnet test --project tests/SharpVision.Tests \
  --filter-namespace "SharpVision.Tests.Controls.Collections*" \
  --minimum-expected-tests 1 --timeout 60s
dotnet test --project tests/SharpVision.Tests \
  --filter-namespace "SharpVision.Tests.Navigation*" \
  --minimum-expected-tests 1 --timeout 60s
```
