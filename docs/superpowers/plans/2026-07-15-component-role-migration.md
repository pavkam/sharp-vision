# Component Role Hierarchy and Built-in Migration Plan

> **For agentic workers:** REQUIRED SUB-SKILL: use
> `superpowers:subagent-driven-development` or `superpowers:executing-plans`.
> Begin only after the component-foundation and intrinsic-border-shadow plans
> are committed and all repository gates pass.

**Goal:** Make public inheritance describe actual authoring roles and migrate
every built-in control without rendering, layout, input, or lifecycle drift.

**Architecture:** Add single-content, explicit-composite, and private-item-host
bases over the central registry. Only true panels retain `Container`. Migrate
the showcase and remove `View`, bypassable `Children`, and hidden scroll APIs.
`Border` and `Shadow` are already absent because the prerequisite intrinsic plan
removes them instead of laundering them into the content hierarchy.

## Task 1: Add `ContentControl`

**Production:**

- Create `src/SharpVision/Controls/ContentControl.cs`.

**Tests:**

- Create `tests/SharpVision.Tests/Controls/ContentControlTests.cs`.
- Add `ExternalContentControl.cs` and focused tests to
  `tests/SharpVision.Consumer.Tests/`.

- [ ] Add failing tests for null content, first assignment, equivalent
      assignment, valid replacement, invalid replacement preserving the old
      edge/context/focus/capture, clear, attached assignment, direct child
      disposal, owner disposal, layout with margin, stretch arrangement,
      rendering, hit testing, navigation, and popup traversal.
- [ ] Add a consumer-derived content control to prove the base is externally
      usable without tree internals.
- [ ] Implement zero-or-one public `Content` over a private owned slot.
- [ ] Supply default measure/arrange through `MeasureChild`/`ArrangeChild` and
      ordinary registry render/hit/navigation behavior.
- [ ] Run `ContentControlTests`, `TreeTests`, consumer tests, and rendering
      tests.

## Task 2: Migrate single-content window and popup controls

**Production:**

- Modify `src/SharpVision/Controls/Window.cs` and its partial files.
- Modify `src/SharpVision/Controls/Popup.cs`.

**Tests/docs:**

- Update the Window and Popup tests and public control specifications.
- Add reflection tests asserting neither derives from `Container` or exposes
  `Children`.

- [ ] For each remaining control, write/adjust the reflection and
      semantic-ownership test before changing its base.
- [ ] Rename Window and Popup `Child` to inherited `Content` across source,
      tests, showcase, and docs in the same task. The repository is pre-1.0; do
      not retain a second alias that recreates ambiguity.
- [ ] Replace direct internal child transactions with protected helpers.
- [ ] Preserve exact measurement, frame geometry, popup focus restoration,
      window default/cancel button search, and disposal event order.
- [ ] Run each control's focused suite and `GalleryRenderingTests` after its
      migration.

## Task 3: Make `Pressable` a true one-content interaction base

**Production:**

- Modify `src/SharpVision/Controls/Pressable.cs`.
- Modify `src/SharpVision/Input/CaptureManager.cs` if cancellation delivery is
  finalized here.

**Tests:**

- Modify `tests/SharpVision.Tests/Controls/PressableTests.cs`.
- Add `ExternalToggleChip.cs` and tests to the consumer project.

- [ ] Add a reflection test asserting `Pressable : ContentControl` and no
      capacity constructor/public `Children` exists.
- [ ] Add consumer behavior tests for keyboard/pointer parity, content-target
      hover, focus request, capture, cancellation, disabled/hidden cleanup, and
      one activation.
- [ ] Change the base and remove direct manager/event subscription plumbing in
      favor of protected focus/capture/cancellation hooks.
- [ ] Preserve Space hold/release, Enter, pointer-inside activation, capture
      cancellation, and no activation after unavailable state.
- [ ] Run Pressable, pointer, focus, routing, and integration tests.

## Task 4: Migrate pressable concrete controls

**Production:**

- Modify `Button.cs`, `CheckBox.cs`, `RadioButton.cs`, `MenuItem.cs`,
  `ListItem.cs`, and their partial files.

- [ ] Remove duplicate `Content` implementations and child-capacity constructors
      one control at a time.
- [ ] Convert checked/selected/indeterminate setters to the protected visual
      state invalidation contract so state-specific geometry cannot be
      under-invalidated.
- [ ] Preserve command/click order, tri-state transitions, radio-group
      atomicity, menu kinds, selected row propagation, Unicode mark fallback,
      exact cells, and style resolution.
- [ ] Run each focused suite, `StateModelTests`, and `InteractiveControlTests`
      before the next control.

## Task 5: Add deterministic `CompositeControl`

**Production:**

- Create `src/SharpVision/Controls/CompositeControl.cs`.

**Tests:**

- Create `tests/SharpVision.Tests/Controls/CompositeControlTests.cs`.
- Add `StatusCard.cs` and tests to the consumer project.

- [ ] Add failing tests for construction-time initialization, private root,
      first layout with no tree mutation/redundant pass, duplicate
      initialization, null/owned/attached/disposed/cyclic candidate rejection,
      attached context propagation, layout, render, hit, navigation, popup,
      direct root disposal, owner disposal, and callback failure consistency.
- [ ] Implement protected one-shot `InitializeContent` and protected non-null
      `Content` after initialization.
- [ ] Reject use before initialization during attach and layout with a
      documented `InvalidOperationException`.
- [ ] Do not call a virtual method from a base constructor and do not construct
      lazily from measure/render.
- [ ] Prove the consumer composite is not a `Container` and has no public
      `Children`.

## Task 6: Migrate `Screen` and the showcase, then remove `View`

**Production:**

- Modify `src/SharpVision/Controls/Screen.cs`.
- Delete `src/SharpVision/Controls/View.cs`.
- Modify `src/SharpVision.Showcase/Gallery.cs`.
- Modify every pane under `src/SharpVision.Showcase/Panes/` that derives from
  `View` or overrides `Build`.

**Tests/docs:**

- Delete/replace `tests/SharpVision.Tests/Controls/ViewTests.cs` with screen and
  composite tests.
- Modify showcase startup/render/interaction tests.
- Rewrite `docs/concepts/custom-components.md` and screen lifecycle docs.
- Modify `AGENTS.md` to name `CompositeControl.InitializeContent`.

- [ ] Add lifecycle proof: constructor composition → application attach hook →
      first layout/frame → started hook → dispose hook.
- [ ] Convert every concrete component to call `InitializeContent` from its
      constructor after creating/storing the required retained controls.
- [ ] Verify no constructor needs a running `Application`; application-specific
      configuration remains in `OnAttach` and focus work in `OnStarted`.
- [ ] Delete `View`, every `Build` override, and docs describing measure-time
      construction.
- [ ] Run screen/application tests and every showcase screen size/inventory/
      interaction test.
- [ ] Assert first measure of each representative pane leaves ownership and
      pending measure state unchanged after completion.

## Task 7: Add `ItemsControl` with a private presentation host

**Production:**

- Create `src/SharpVision/Controls/ItemsControl.cs`.

**Tests:**

- Create `tests/SharpVision.Tests/Controls/ItemsControlTests.cs`.
- Add `TagCloud.cs` and tests to the consumer project.

- [ ] Add failing tests for one-shot private host initialization, protected
      realized-container access, validated batch replacement, caller inability
      to mutate the host, layout/render/hit/navigation, style scope, focus/
      capture cleanup, item-container disposal, and callback failure atomicity.
- [ ] Implement a private `Container` host owned through the central registry.
      Expose protected read-only item-container inspection and validated
      insert/remove/replace/clear helpers; do not impose a public data item
      type.
- [ ] Supply passthrough layout and semantic-owner style scope behavior.
- [ ] Prove a third-party `TagCloud` can expose its own typed item collection
      and realize controls without accessing ownership internals.

## Task 8: Migrate `List`

**Production:** modify `src/SharpVision/Controls/List.cs` and `ListItem.cs`.

**Tests/docs:** update `ListTests`, integration/performance tests, list docs,
and showcase pane.

- [ ] Add reflection tests that `List : ItemsControl`, exposes no `Children`,
      and declares no hidden members.
- [ ] Move the existing private scrolling `Stack` into the ItemsControl host.
- [ ] Preserve atomic item/template replacement; improve it to validate and
      prepare every realized control before replacing the complete batch.
- [ ] Remove `new` from `VerticalOffset`, `ScrollBars`, `ShowScrollBars`,
      `ScrollBarChrome`, and `ScrollBarFill`; these are now ordinary List APIs
      delegated to its one private host.
- [ ] Preserve selection cancellation/order, item reuse/disposal contract,
      keyboard/pointer behavior, bring-into-view, style scope, Unicode variable
      height, and exact cells.
- [ ] Run List, scrolling, interactive integration, performance, and showcase
      tests.

## Task 9: Migrate `Menu`

**Production:** modify `Menu.cs`, `MenuItems.cs`, and `MenuItem.cs`.

- [ ] Add tests that only `MenuItems` can change semantic items and arbitrary
      controls cannot enter the realized host.
- [ ] Migrate to an ItemsControl `Stack` host while preserving horizontal/
      vertical orientation, spacing, separators, radio grouping, selection, open
      popup routing, and invocation.
- [ ] Remove every cast/late failure caused by public `Children`.
- [ ] Run Menu, popup, window, routing, and showcase tests.

## Task 10: Migrate `Table`

**Production:**

- Create `src/SharpVision/Controls/TablePresenter.cs`.
- Modify `Table.cs`, `TableRows.cs`, `TableRow.cs`, `TableColumns.cs`, and
  `TableColumn.cs` as required.

**Tests:** expand `TableTests` with batch-failure/callback-throw cases.

- [ ] Move measure/arrange of realized row cells into internal
      `TablePresenter : Container`.
- [ ] Keep `Table.Rows`/`Columns` as the only semantic mutation surfaces.
- [ ] Use a batch owned-edge transaction so a later cell failure cannot detach
      an earlier old cell or publish a partial row.
- [ ] Preserve spans, automatic column measurement, padding, headers, empty/
      tiny tables, Unicode cells, and exact rendering.
- [ ] Assert `Table` has no public `Children` and row/cell ownership agrees at
      every callback.
- [ ] Run Table, grid/track allocation, randomized layout, and showcase tests.

## Task 11: Return `TextInput` to a primitive leaf

**Production:** modify `src/SharpVision/Controls/TextInput.cs`.

- [ ] Add reflection tests asserting direct `Control` inheritance, no
      `Children`, and no hidden members.
- [ ] Register horizontal/vertical bars as private owned parts while preserving
      the editor's one scroll state and public scroll configuration.
- [ ] Remove custom lifecycle traversal/disposal overrides now supplied by the
      registry.
- [ ] Preserve editing, selection, caret, paste, Unicode clusters, password
      rendering, focus/capture, exact cells, and scrollbar geometry.
- [ ] Run TextInput, terminal input, Unicode integration, rendering, and
      performance tests.

## Task 12: Migrate `ComboBox` last

**Production:** modify `src/SharpVision/Controls/ComboBox.cs`.

- [ ] Add reflection tests asserting one inherited `Content`, no `Children`, and
      no hidden scroll members.
- [ ] Register the popup and list as private parts. Keep the closed face content
      semantic and the open popup on the popup layer.
- [ ] Delegate dropdown scroll configuration without `new` because no inherited
      Container API exists.
- [ ] Preserve open/close focus restoration, selection event order, Escape,
      placement, clipping, hit testing, and exact rendering.
- [ ] Run ComboBox, List, Popup, interactive integration, and showcase tests.

## Task 13: Extract internal Container responsibilities

**Production:**

- Create focused internal files for intrinsic container sizing and scrolling,
  such as `ContainerScrollState.cs` and `ContainerScrollLayout.cs`, with one
  named type per file.
- Reduce `Container.cs` to public multi-child/layout delegation plus its public
  intrinsic APIs.

- [ ] Keep all public `Container` members and algorithms unchanged.
- [ ] Move scrollbar creation to named private parts in the owned registry.
- [ ] Remove `private protected` fields consumed by `Canvas`; replace them with
      narrow protected geometry queries only where the panel contract requires.
- [ ] Preserve exact automatic-bar feedback, offsets, nested wheel remainder,
      thumb drag, bring-into-view, resize clamping, and randomized geometry.
- [ ] Run all Container auto-size/scroll, layout, randomized, integration,
      rendering, and performance suites.

## Task 14: Role documentation, API guards, and full gate

- [ ] Update every affected control specification and
      `docs/concepts/custom-components.md`, layout, scrolling, lifecycle,
      styling, input, focus, project structure, showcase, and testing docs.
- [ ] Update `docs/index.md`, concept/control indexes, and all section links.
- [ ] Add reflection tests for the complete target inheritance table and absence
      of hidden `new` members, plus absence of the removed `Border` and `Shadow`
      types.
- [ ] Search for and eliminate `View`, `Build()`, `public new`, stale `Child`
      APIs, non-panel `: Container`, and stale `ScrollView` references.
- [ ] Run all four repository gates from a clean worktree.
