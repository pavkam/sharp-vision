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
- Create `tests/SharpVision.Tests/Support/ProbeContentControl.cs`.
- Add `ExternalContentControl.cs` and focused tests to
  `tests/SharpVision.Consumer.Tests/`.

**Documentation:**

- Create `docs/controls/content-control.md`.
- Update `docs/controls/index.md`, `docs/controls/control.md`, `docs/index.md`,
  `docs/architecture/memory-ownership.md`,
  `docs/architecture/project-structure.md`,
  `docs/testing/controls-integration.md`, and the role design's `ContentControl`
  section links.

- [x] Add the abstract public surface exactly as
      `public abstract ContentControl : Control`, with a non-virtual
      `Control? Content` property and protected virtual
      `OnContentChanged(Control? previous, Control? current)` callback.
- [x] Register one capacity-one normal-layer owned slot with role `Content`, hit
      testing and focus navigation enabled, and `ChangeImpact.Measure`. Register
      it in the `ContentControl` constructor before a derived constructor can
      register parts.
- [x] Add failing ownership tests for null clear, first assignment, equivalent
      assignment, replacement, duplicate/cycle/cross-parent rejection, and
      invalid replacement preserving the old edge, context, focus, and capture.
      Every rejected operation must be atomic; replacement detaches but never
      disposes the previous content.
- [x] Add failing dispatcher tests proving attached replacement, clear, and
      equivalent assignment all verify dispatcher access before observing
      equivalence or mutating ownership.
- [x] Add failing notification-order tests proving structural publication is
      complete before `OnContentChanged`; previous/current are cached before
      callbacks; and `PropertyChanged(nameof(Content))` publishes exactly once
      after every successful assignment, replacement, clear, or direct-child
      disposal, but never after equivalent or rejected operations. If
      `OnContentChanged` throws, publish `PropertyChanged` against the coherent
      new structure before propagating failure; an earlier ownership-transaction
      callback remains the authoritative first exception. Request the slot's
      measure invalidation once after lifecycle publication and before these
      notifications, so subscriber-driven layout consumes current work without
      leaving a redundant pass.
- [x] Prove `OnContentChanged` remains inside guarded publication: attempts to
      mutate another owned slot, replace or clear `Content`, or dispose either
      the owner or either affected content control are rejected as reentrant
      without disturbing the committed edge.
- [x] Add failing disposal tests proving direct child disposal clears and
      notifies the property, while owner disposal disposes its current child
      exactly once even when content-change or property callbacks throw.
- [x] Add failing layout tests proving collapsed content contributes neither
      size nor margin and enters neither child layout override, while base child
      transactions clear stale desired size and bounds. Visible content is
      measured through `MeasureChild`; its desired size includes margin with
      saturating arithmetic; and arrangement uses
      `ArrangeChild(content, slot, ResolvedAxes.Both)` so stretch behavior is
      deterministic.
- [x] Add failing render, hit-test, focus-navigation, and popup-traversal tests
      that rely on the ordinary owned registry rather than role-specific
      traversal overrides.
- [x] Implement the smallest zero-or-one slot adapter. Rely on the registry for
      validation, structural commit, context propagation, rendering, hit
      testing, navigation, popup traversal, direct-child removal, and disposal;
      do not duplicate those engines in `ContentControl`.
- [x] Add `ProbeContentControl` in its own test-support file and an unfriended
      consumer-derived `ExternalContentControl` specimen proving layout and
      ownership through only public/protected APIs.
- [x] Document purpose, inheritance, defaults, ownership, threading,
      notification/callback order and failures, disposal, layout, rendering, hit
      testing, navigation, popup traversal, examples, validation, and every
      public/protected exception in XML and the control specification.
- [x] Run `ContentControlTests`, `TreeTests`, the unfriended consumer tests,
      relevant rendering/traversal tests, Markdown formatting/link/spec tests,
      and `git diff --check`.

## Task 2: Migrate single-content window and popup controls

**Production:**

- Modify `src/SharpVision/Controls/Window.cs` and its partial files.
- Modify `src/SharpVision/Controls/Popup.cs`.

**Tests/docs:**

- Update the Window and Popup tests and public control specifications.
- Update ComboBox tests, popup/window showcase panes, API examples, and current
  architecture/index documentation that names their single-content surface.
- Add reflection tests asserting neither derives from `Container` or exposes
  `Children` or the retired `Child` alias.

- [x] For each remaining control, write/adjust the reflection and
      semantic-ownership test before changing its base. Prove `Window` and
      `Popup` derive from `ContentControl`, expose only inherited `Content`, and
      retain no capacity constructor.
- [x] Rename Window and Popup `Child` to inherited `Content` across source,
      tests, showcase, and docs in the same task. The repository is pre-1.0; do
      not retain a second alias that recreates ambiguity.
- [x] Replace direct internal child transactions with protected
      `MeasureChild`/`ArrangeChild(..., ResolvedAxes.Both)` calls. Preserve
      visible/open Window title, frame, shadow, and Popup surface geometry;
      intentionally correct collapsed Window and closed Popup geometry to the
      `ContentControl` contract by clearing stale content state and omitting
      collapsed margins.
- [x] Override `Popup.OnContentChanged`, call the base hook, and set newly
      committed content to Visible while open or Collapsed while closed. Leave
      detached previous content at its committed visibility, allow this ordinary
      property mutation under guarded publication, and preserve coherent
      structure/property notification if either hook or subscriber callbacks
      throw.
- [x] Preserve exact visible/open measurement and frame geometry, popup focus
      restoration, popup promotion/layer behavior and event order, window
      default/cancel registry traversal, direct-content disposal, and owner
      disposal order. Collapsed/closed geometry is intentionally corrected to
      the inherited `ContentControl` contract.
- [x] Search production, tests, showcase, and current specifications for stale
      Window/Popup `Child` use or assumptions that either type exposes
      `Children`; do not rewrite general panel-child APIs.
- [x] Run Window, Popup, ComboBox, ownership/traversal/routing/focus, and
      focused showcase suites plus documentation, build, format, link, and diff
      checks.

## Task 3: Atomically migrate the complete `Pressable` interaction wave

`Pressable` cannot become independently green. Every concrete subclass calls its
capacity constructor, several compile against inherited `Children`, and
`ComboBox` both stores its Popup in that collection and hides Container scroll
members. Treat the kernel, all subclasses, ListItem, and ComboBox as one
staged-red/atomic-green change. Do not commit, hand off, or run a claimed green
checkpoint between individual production edits.

**Resolved role decisions:**

1. `MenuItem : Pressable` uses inherited `Content` as its sole visible face;
   remove `Header`. A separator is not pressable, so remove
   `MenuItemKind.Separator` and add `MenuSeparator : Control`.
2. `ComboBox : Control`, not `Pressable` or `ContentControl`. Its closed face is
   the selected item text and it exposes neither `Content` nor `Children`.
   `Pressable` and `ComboBox` delegate input mechanics to one internal composed
   press interaction helper instead of using dishonest inheritance.
3. `ComboBox` owns exactly one private Popup. `Popup.Content` owns the private
   List; registering both parts directly on ComboBox would violate one-parent
   ownership.

**Production files changed together:**

- `src/SharpVision/Controls/Control.cs`: add the protected
  `SetVisualStateProperty<T>` mutation seam and make selected-state propagation
  clear resolved-style caches before dynamic style-impact invalidation.
- `src/SharpVision/Controls/PressInteraction.cs`: add the internal reusable
  keyboard, pointer, focus, capture, cancellation, and pressed-state machine.
- `src/SharpVision/Controls/Pressable.cs`: change the base to `ContentControl`,
  replace the capacity constructor with a protected parameterless constructor,
  and delegate interaction to `PressInteraction` through protected hooks.
- `src/SharpVision/Controls/Button.cs`, `CheckBox.cs`, `RadioButton.cs`,
  `RadioGroup.cs`, `MenuItem.cs`, `ListItem.cs`, and `ComboBox.cs`: migrate
  every content/layout/state transaction in the same cut.
- `src/SharpVision/Controls/MenuSeparator.cs`, `Menu.cs`, `MenuItems.cs`, and
  `MenuItemKind.cs`: introduce the non-interactive separator, constrain the
  mixed item collection through typed overloads, and coordinate atomic radio
  publication.
- `src/SharpVision.Showcase/Controls/NavigationItem.cs`: remove the capacity
  constructor and give its label a coherent inherited-content layout.
- `src/SharpVision.Showcase/Panes/ButtonPane.cs`, `CheckBoxPane.cs`,
  `RadioButtonPane.cs`, `MenuPane.cs`, and `ComboBoxPane.cs`: migrate examples
  and retain visible behavior.

`CaptureManager` is not part of the planned edit: `Control` already exposes
`RequestFocus`, `CapturePointer`, `HasPointerCapture`, `ReleasePointerCapture`,
and `OnPointerCaptureCancelled`. Change the manager only if a focused red test
proves those hooks cannot express the documented transaction.

**Tests and consumer proof changed together:**

- Modify `PressableTests`, `ButtonTests`, `CheckBoxTests`, `RadioButtonTests`,
  `MenuTests`, `ListTests`, `ComboBoxTests`, `PopupTests`, `StateModelTests`,
  and `InteractiveControlTests`.
- Modify `tests/SharpVision.Tests/Support/ProbePressable.cs` and create
  `tests/SharpVision.Tests/Support/OwnedTree.cs` for test-only traversal across
  every registered ownership slot; private ComboBox parts must not gain a
  production accessor for tests.
- Create `tests/SharpVision.Consumer.Tests/ExternalToggleChip.cs` and extend
  `ExternalContractTests.cs`. The external control derives from `Pressable`,
  uses inherited `Content`, toggles checked state through the protected visual
  state seam, and records activation/capture-cancellation without internal or
  friend access.
- Update showcase rendering/interaction tests for NavigationItem, every migrated
  content control, private ComboBox popup traversal, and final cells.

**Stage the red evidence before changing production:**

- [ ] Run the existing Pressable, concrete-control, List, ComboBox, Popup,
      pointer, focus, routing, state-model, integration, consumer, and showcase
      suites as the green baseline.
- [ ] Add `Type_WhenInspected_UsesSingleContentPressableRole` reflection
      coverage for `Pressable`, Button, CheckBox, RadioButton, MenuItem,
      ListItem, NavigationItem, ProbePressable, and ExternalToggleChip. Assert
      `Pressable.BaseType == typeof(ContentControl)`, inherited `Content` is not
      redeclared, no pressable type is assignable to `Container`, and
      `Children`, capacity constructors, and capture-manager fields are absent.
      Separately assert `ComboBox.BaseType == typeof(Control)`, that it exposes
      no `Content`, `Children`, hidden `new` scroll members, Popup, or List, and
      that `MenuItemKind` has no Separator value. Run it and record the expected
      failures against the current hierarchy.
- [ ] Add direct routed-pointer tests proving Pressable itself requests focus
      and capture when content is the original hit target; do not rely on
      `CaptureManager.Dispatch` pre-focusing the target. Cover inside release,
      outside release, Space cancellation, disable, Hidden, Collapsed, detach,
      terminal-focus loss, pointer-capture callback order, and release after
      cancellation producing no activation. Record the focused red result for
      the missing protected-hook implementation.
- [ ] Warm normal-state style caches, clear pending work, then activate Checked,
      Indeterminate, and Selected overlays containing measure-impact Padding.
      Assert the new resolved value is visible immediately, `Pending` is
      `Invalidation.All`, render-only overlays remain render-only, and
      equivalent assignments publish/invalidate nothing. Run these tests and
      record the stale-cache/under-invalidation failures.
- [ ] Add a RadioButton observer test in which the old member's
      `PropertyChanged(IsChecked)` handler already sees the new member selected.
      Preserve final `Unchecked -> Checked -> SelectionChanged` event order and
      the existing reentrant stale-selection suppression. Add the equivalent
      Menu radio observer test if its current loop publishes a partial group.
- [ ] Add content-role red tests through the common `ContentControl` surface:
      replacement/disposal ownership for Button/CheckBox/RadioButton; prefix
      measure/arrange and Unicode cells for CheckBox/RadioButton/MenuItem;
      Button pressed-face translation; and ListItem width-only resolved
      arrangement for variable-height content. Add separate ComboBox tests for
      selected-text derivation and a private-ownership test proving
      `ComboBox -> Popup -> List`, with no public route that can replace or
      remove either private part. Add MenuSeparator tests proving it never
      focuses, hits, selects, or invokes.

**Perform one atomic production cut:**

- [ ] Implement `SetVisualStateProperty<T>(ref field, value, propertyName)` with
      dispatcher/lifetime validation before equivalence, field commit, resolved
      style-cache invalidation, dynamic aggregate visual-state invalidation, and
      one `PropertyChanged` notification. Fix `SetSelectedState` to invalidate
      each propagated control's resolved cache before calculating its style
      impact.
- [ ] Add `PressInteraction` as an owner-bound internal behavior. It accepts
      callbacks for bounds, availability, focus, capture, pressed state, and
      activation; owns no control-tree state; and exposes event, focus,
      unavailable, and capture-cancellation entry points. Change Pressable to
      `ContentControl` and delegate its protected hooks to this helper using
      only `RequestFocus`, `CapturePointer`, `HasPointerCapture`, and
      `ReleasePointerCapture`. ComboBox composes the same helper directly.
      Preserve exact Space press/repeat/release, Enter, primary-pointer
      inside/outside, focus-loss, availability, and one-activation semantics.
- [ ] Remove duplicate Content properties and capacity constructors from Button,
      CheckBox, and RadioButton. Use `MeasureChild`/`ArrangeChild`; Button keeps
      pressed shadow translation, while CheckBox and RadioButton reserve their
      mark prefixes and exclude collapsed content margins. Route checked and
      indeterminate commits through the new visual-state seam.
- [ ] Stage both RadioButton group fields before publishing either property
      notification. Continue notification and specific-event publication after
      callback failures, retain the earliest failure, and suppress stale outer
      Checked/SelectionChanged events after a reentrant selection. Resolve a
      checked member's destination group before publishing GroupName so no
      observer can see duplicate selection.
- [ ] When disabling CheckBox three-state mode from an indeterminate value,
      stage `IsThreeState = false` and `IsChecked = false` before either
      property notification. Publish property changes before semantic check
      events, and never expose the invalid false/null combination to callbacks.
- [ ] Remove MenuItem.Header and use inherited Content with prefix-aware
      `MeasureChild`/`ArrangeChild(..., ResolvedAxes.Both)`. Remove the
      Separator enum value and implement MenuSeparator as a non-focusable,
      non-hit-testable leaf. Change `MenuItems` to `IReadOnlyList<Control>`
      while exposing only typed Add/Remove overloads for MenuItem and
      MenuSeparator; arbitrary controls cannot enter. Stage all matching radio
      values before publishing changed properties in item order, suppress stale
      outer events after reentry, and retain the earliest callback failure.
- [ ] Set inherited ListItem.Content in its constructor and remove the duplicate
      member. Preserve semantic hit ownership and selected-state propagation.
      Arrange with `ArrangeChild(content, bounds, ResolvedAxes.Width)`, not
      Both, so variable-height List rows retain their current contract. This
      migration does not move List's Stack; that remains the later ItemsControl
      task. Commit and propagate selected state through the realized subtree
      before publishing ListItem.IsSelected.
- [ ] Make ComboBox a direct Control and compose `PressInteraction`. Register
      one private popup slot; Popup.Content owns the private List, so there is
      never a duplicate parent edge. Render and measure the selected item text
      directly, use protected child transactions only for popup geometry, remove
      `new` from delegated scrollbar properties, and restore focus with
      `RequestFocus` rather than manager access. Preserve selection order,
      open/close failure semantics, Escape, clipping, promotion, hit testing,
      resize, scrollbars, and exact cells.
- [ ] Remove capacity calls from ProbePressable and NavigationItem, complete the
      NavigationItem content layout, add ExternalToggleChip, and migrate every
      test/showcase call site that currently reaches a concrete Pressable's
      `Children` or ComboBox's Popup by public collection index. Migrate all
      `.Header` and `MenuItemKind.Separator` call sites to Text content and
      MenuSeparator. Do not add compatibility constructors, aliases, or
      private-part accessors.

**Return the whole wave to green before committing:**

- [ ] Build the complete solution once all production and call-site edits are
      present. Expected result: zero capacity-constructor/Children/hiding
      compiler errors and zero warnings. Do not claim an intermediate subclass
      as independently green.
- [ ] Run focused SharpVision tests for Pressable, Button, CheckBox,
      RadioButton, Menu, List, ComboBox, Popup, Pointer, Focus, Routing,
      StateModel, and InteractiveControl; run the unfriended consumer tests and
      complete showcase rendering/interaction suites.
- [ ] Search production/tests/docs for `: Container`, `base(capacity:`,
      `.Children`, `public new`, direct FocusOwner/CaptureOwner access, and
      duplicate Content declarations scoped to the migrated wave. Every match
      must be either a true panel/owner call site or removed.
- [ ] Create `docs/controls/pressable.md`; update Button, CheckBox, RadioButton,
      MenuItem, Menu, List, ComboBox, control/index, custom-component, styling,
      input/focus, testing, and showcase documentation plus XML examples.
      Include ExternalToggleChip as the third-party extension proof and document
      MenuItem content, MenuSeparator, ComboBox selected text, and reusable
      internal interaction composition decisions.
- [ ] Run `make format`, `make lint`, `make build`, and `make test`, then
      `git diff --check`. Commit the atomic wave only after all four gates pass.

## Task 4: Add deterministic `CompositeControl`

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

## Task 5: Migrate `Screen` and the showcase, then remove `View`

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

## Task 6: Add `ItemsControl` with a private presentation host

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

## Task 7: Migrate `List`

**Production:** modify `src/SharpVision/Controls/List.cs`.

**Tests/docs:** update `ListTests`, integration/performance tests, list docs,
and showcase pane.

- [ ] Add reflection tests that `List : ItemsControl`, exposes no `Children`,
      and declares no hidden members.
- [ ] Keep the Task 3 `ListItem : Pressable` content contract intact: inherited
      `Content`, semantic hit ownership, selected-state propagation, and
      width-only resolved arrangement. Do not reintroduce a private Content
      alias while moving List's presentation host.
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

## Task 8: Migrate `Menu`

**Production:** modify `Menu.cs` and `MenuItems.cs`.

- [ ] Add tests that only `MenuItems` can change semantic items and arbitrary
      controls cannot enter the realized host.
- [ ] Preserve the single MenuItem content model resolved in Task 3. This task
      moves Menu's presentation ownership only; it must not restore `Header`,
      add a second content alias, or change separator suppression.
- [ ] Migrate to an ItemsControl `Stack` host while preserving horizontal/
      vertical orientation, spacing, separators, radio grouping, selection, open
      popup routing, and invocation.
- [ ] Remove every cast/late failure caused by public `Children`.
- [ ] Run Menu, popup, window, routing, and showcase tests.

## Task 9: Migrate `Table`

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

## Task 10: Return `TextInput` to a primitive leaf

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

## Task 11: Extract internal Container responsibilities

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

## Task 12: Role documentation, API guards, and full gate

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
