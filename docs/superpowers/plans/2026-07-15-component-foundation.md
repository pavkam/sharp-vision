# Component Foundation and External Contract Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: use
> `superpowers:subagent-driven-development` or `superpowers:executing-plans`.
> Execute tasks in order. Write each behavior test first and observe its
> intended failure before implementation.

**Goal:** Establish a green, externally provable `Control` extension kernel and
one central owned-control tree before changing concrete inheritance.

**Architecture:** Correct known state/theme bugs, replace styling-only `Impact`
with general `ChangeImpact`, compile real consumer specimens without friend
access, then move ownership and traversal from `Container` into `Control`.

**Design:**
`docs/superpowers/specs/2026-07-15-component-architecture-v2-design.md`.

## Task 0: Reconfirm the isolated baseline

**Files:** none.

- [ ] Run `git status --short` in the architecture worktree. Expected before
      plan-doc commits: only the intentional architecture documents.
- [ ] Run `make test`. Expected: zero warnings/errors and at least 1,377 passing
      tests.
- [ ] Record any inherited failure before editing. Do not absorb unrelated
      repairs into this branch.

## Task 1: Publish palette changes correctly

**Tests:**

- Modify `tests/SharpVision.Tests/Styling/ColorRoleTests.cs`.
- Modify `tests/SharpVision.Tests/Styling/ThemeApplicationTests.cs`.

**Production:**

- Modify `src/SharpVision/Styling/Theme.cs`.
- Modify `docs/concepts/themes.md`.

- [ ] Add `SetColor_WhenConcreteValueChanges_IncrementsVersionAndRaisesChanged`.
      Assert one event, `TargetType == typeof(Control)`, and render impact.
- [ ] Add `SetColor_WhenValueIsEquivalent_IsNoOp`.
- [ ] Add `SetColor_WhenValueIsDeferredRole_ThrowsBeforeMutation` and assert
      version/color/event state is unchanged.
- [ ] Add an application integration test that installs a mutable theme, changes
      a semantic color on the dispatcher, and observes the newly resolved color
      from the attached root.
- [ ] Run:

  ```bash
  dotnet test --project tests/SharpVision.Tests \
    --filter-class "*ColorRoleTests|*ThemeApplicationTests" --timeout 120s
  ```

  Expected red reason: `SetColor` neither publishes nor rejects role colors.

- [ ] Implement validation-before-mutation, equivalent-value no-op, `Version++`,
      and one render-impact event outside the lock.
- [ ] Re-run the focused tests to green.
- [ ] Update the mutable-theme publication contract in `themes.md`.

## Task 2: Correct style impact and cascade ordering

**Tests:**

- Modify `tests/SharpVision.Tests/Styling/ThemeTests.cs`.
- Modify `tests/SharpVision.Tests/Styling/StyleScopeTests.cs`.
- Modify `tests/SharpVision.Tests/Styling/ThemeResolverTests.cs`.
- Modify `tests/SharpVision.Tests/Styling/StylePropertyTests.cs`.

**Production:**

- Create `src/SharpVision/Controls/ChangeImpact.cs`.
- Delete `src/SharpVision/Styling/Impact.cs` after all callers migrate.
- Modify `src/SharpVision/Styling/IStyleProperty.cs`.
- Modify `src/SharpVision/Styling/IControlStyle.cs`.
- Modify `src/SharpVision/Styling/StyleProperty.cs`.
- Modify `src/SharpVision/Styling/ControlStyle.cs`.
- Modify `src/SharpVision/Styling/ControlStyleSnapshot.cs`.
- Modify `src/SharpVision/Styling/ThemeChangedEventArgs.cs`.
- Modify `src/SharpVision/Styling/Theme.cs`.
- Modify `src/SharpVision/Styling/ThemeResolver.cs`.
- Modify `src/SharpVision/Controls/Control.cs`.
- Modify `src/SharpVision/Controls/Control.ThemeValues.cs`.
- Modify `src/SharpVision/Runtime/Application.cs`.
- Modify all style-property registrations and affected tests/docs.

- [ ] Add a theme replacement test: replacing a measure-impact style with a
      render-impact style raises measure impact because removing the old value
      can change geometry.
- [ ] Add `Resolve_WhenDescendantThemeStyleExists_WinsOverAncestorScopeTheme`.
- [ ] Add
      `Resolve_WhenDescendantInstanceStyleExists_WinsOverAncestorScopeInstance`.
- [ ] Add `Resolve_WhenThemeFocusedAndInstanceNormalExist_InstanceNormalWins`.
- [ ] Add an arrange-impact registration/application test.
- [ ] Add an equivalent local `SetValue` test asserting no notification and no
      new invalidation.
- [ ] Run the four focused classes. Expected failures must demonstrate current
      old/new impact loss, inverted scope precedence, outer-state-loop cascade,
      missing arrange impact, and equivalent-value publication.
- [ ] Introduce `ChangeImpact` with ordered values `None`, `Render`, `Arrange`,
      `Measure`; migrate every public styling API to it.
- [ ] Resolve the best matching state inside each style layer. Apply layers
      low-to-high: defaults, far-to-near scope theme chains, descendant theme
      chain, far-to-near scope instance styles, descendant instance style, then
      local value.
- [ ] Map impacts centrally: none; render; arrange+render;
      measure+arrange+render.
- [ ] Make `Control.Style` invalidate the maximum aggregate impact of removed
      and installed styles.
- [ ] Make equivalent `SetValue` assignments no-ops.
- [ ] Run all styling tests:

  ```bash
  dotnet test --project tests/SharpVision.Tests \
    --filter-class "*Styling.*" --timeout 180s
  ```

- [ ] Update `docs/concepts/styling.md`,
      `docs/concepts/theming-new-controls.md`, and `docs/controls/control.md`.

## Task 3: Release focus when eligibility changes

**Tests:** modify `tests/SharpVision.Tests/Input/FocusTests.cs`.

**Production:**

- Modify `src/SharpVision/Controls/Control.cs`.
- Modify `docs/concepts/focus.md`.

- [ ] Add
      `CanFocus_WhenFocusedControlBecomesFalse_ReleasesFocusSynchronouslyAsync`.
      Assert `FocusManager.Focused` and `IsFocused` are clear before the setter
      returns and cancellation handlers cannot retain focus.
- [ ] Run `FocusTests`; expect the new assertion to fail.
- [ ] After the changed `CanFocus` value commits, call the existing
      non-cancellable unavailability cleanup path.
- [ ] Re-run `FocusTests`, `PointerTests`, and `RoutingTests`.
- [ ] Update the focus contract.

## Task 4: Add the unfriended consumer project and compile guards

**Project:**

- Create `tests/SharpVision.Consumer.Tests/SharpVision.Consumer.Tests.csproj`.
- Create `tests/SharpVision.Consumer.Tests/GlobalUsings.cs`.
- Create `tests/SharpVision.Consumer.Tests/AssemblyBoundaryTests.cs`.
- Modify `SharpVision.slnx`.
- Modify `docs/architecture/project-structure.md`.

**Specimen files, one type each:**

- `Gauge.cs`;
- `FlowPanel.cs`;
- `InteractiveProbe.cs`;
- `ExternalContractTests.cs`.

- [ ] Configure xUnit v3, Shouldly, and Microsoft.NET.Test.Sdk; reference only
      `src/SharpVision/SharpVision.csproj`. Do not reference internal test
      helpers or Moq.
- [ ] Add a reflection assertion that no `InternalsVisibleToAttribute` names
      `SharpVision.Consumer.Tests`.
- [ ] Add a leaf specimen whose ordinary property requires protected mutation,
      whose measurement reads the current `CellPolicy`, and whose render uses
      only protected/public APIs.
- [ ] Add a custom `Container` that calls the proposed `MeasureChild` and
      `ArrangeChild` APIs.
- [ ] Add an interactive leaf that wraps proposed focus/capture helpers and
      records implicit capture cancellation.
- [ ] Add at least three tests so the project satisfies the repository test
      discovery floor.
- [ ] Add the project to the solution and run:

  ```bash
  dotnet test --project tests/SharpVision.Consumer.Tests --timeout 120s
  ```

  Expected red reason: compilation fails because the protected extension kernel
  does not exist. This failure is the contract proof, not a reason to add
  friendship.

## Task 5: Implement the protected extension kernel

**Production:**

- Create `src/SharpVision/Controls/ResolvedAxes.cs`.
- Modify `src/SharpVision/Controls/Control.cs`.
- Modify `src/SharpVision/Input/CaptureManager.cs`.
- Modify existing built-in controls only where renaming the internal helper is
  required.

**Tests:**

- Complete `tests/SharpVision.Consumer.Tests/ExternalContractTests.cs`.
- Modify `tests/SharpVision.Tests/Controls/OverrideSeamTests.cs`.
- Modify `tests/SharpVision.Tests/Input/PointerTests.cs`.

- [ ] Replace `private protected Set`/`NotifyChanged` with documented protected
      `SetProperty`/`NotifyPropertyChanged` accepting `ChangeImpact`. Keep one
      internal mapping to phase flags.
- [ ] Add protected `Invalidate(ChangeImpact)` and `InvalidateVisualState()`
      without exposing pending flags.
- [ ] Make `CellPolicy` readable to external derived controls while preserving
      its private setter and internal framework access.
- [ ] Add `MeasureChild`/`ArrangeChild`; validate null, `ResolvedAxes`, and
      direct ownership before invoking internal transactions.
- [ ] Make child clipping protected-overridable while retaining internal read
      access.
- [ ] Add protected focus/capture request, ownership query, release, and
      cancellation hook APIs. Manager cancellation clears its state before
      invoking the control hook.
- [ ] Add protected `OnAttached`, `OnDetached`, and `OnDisposing` hooks. Their
      publication order is implemented by the central registry in Task 6; before
      then, tests cover root attach/detach/dispose without claiming
      child-transaction atomicity.
- [ ] Keep dispatcher/focus/capture managers and raw transaction methods
      internal.
- [ ] Run the consumer project. Expected: compile and all tests pass.
- [ ] Run `OverrideSeamTests`, `FocusTests`, `PointerTests`, and
      `InteractiveControlTests`.
- [ ] Update `docs/controls/control.md`, `docs/concepts/input-routing.md`, and
      `docs/concepts/theming-new-controls.md` with a complete external example.

## Task 6: Centralize all owned slots in `Control`

**Production:**

- Create `src/SharpVision/Controls/OwnedControlRegistry.cs`.
- Create `src/SharpVision/Controls/OwnedControlRole.cs`.
- Create `src/SharpVision/Controls/OwnedControlLayer.cs`.
- Create `src/SharpVision/Controls/OwnedControlOptions.cs`.
- Modify `src/SharpVision/Controls/Control.cs`.
- Modify `src/SharpVision/Controls/Control.ThemeValues.cs`.
- Modify `src/SharpVision/Controls/Children.cs`.
- Modify `src/SharpVision/Controls/Container.cs`.

**Tests:**

- Expand `tests/SharpVision.Tests/Controls/TreeTests.cs`.
- Create `tests/SharpVision.Tests/Controls/OwnedControlRegistryTests.cs`.
- Extend `tests/SharpVision.Tests/Layout/RandomizedLayoutTests.cs`.

- [ ] Add contract tests for public container children and private test slots:
      null, duplicate, cycle, disposed, attached, cross-parent, capacity, index,
      replacement, clear, attached propagation, removal, direct child disposal,
      and parent disposal.
- [ ] Add observation tests in which `OnParentChanged`, theme notifications,
      focus/capture notifications, and disposal hooks inspect the tree. They
      must see a complete old or complete new state.
- [ ] Add callback-throw tests proving internal ownership remains coherent.
- [ ] Add disposal tests proving one release reason and exactly-once descendant
      disposal.
- [ ] Run the new tests red against the existing `Container`-hardwired model.
- [ ] Change `Control.Parent` to `Control?`.
- [ ] Implement one registry of role/layer/navigation/hit-test metadata and
      ordered slot membership. `Children` becomes a view over a registered slot,
      not a separate lifecycle implementation.
- [ ] Move validation, attach/detach, context propagation, focus/capture
      cleanup, and disposal into the registry transaction.
- [ ] Publish `OnAttached` only after the complete subtree has dispatcher,
      theme, Unicode, and manager context; publish `OnDetached` only after the
      complete subtree is detached; call `OnDisposing` once before owned
      descendants are released.
- [ ] Commit structural state before overridable/public notification. Coalesce
      invalidation per completed transaction.
- [ ] Make direct child disposal remove through its owning slot with
      `ReleaseReason.Disposed`, never through `parent.Children`.
- [ ] Make propagation and disposal traversal non-overridable and registry
      backed.
- [ ] Run `TreeTests`, `RandomizedLayoutTests`, all focus/capture tests, and all
      theme-context tests.

## Task 7: Move cross-cutting traversal to the owned registry

**Production:**

- Modify `src/SharpVision/Input/FocusManager.cs`.
- Modify `src/SharpVision/Input/CaptureManager.cs`.
- Modify `src/SharpVision/Input/Router.cs` only where ancestry types change.
- Modify `src/SharpVision/Styling/ThemeResolver.cs`.
- Modify `src/SharpVision/Controls/RadioGroup.cs`.
- Modify `src/SharpVision/Controls/Container.cs`.
- Modify popup/window descendant searches.

**Tests:**

- Create test-only sibling owner specimens in separate files under
  `tests/SharpVision.Tests/Support/`.
- Expand focus, routing, pointer, styling, popup, and radio tests.

- [ ] Add tests proving navigation, routed ancestry, hover ownership, style
      scopes, popup discovery, and radio grouping work through owned controls
      whose parent is not a `Container`.
- [ ] Run them red; expected failure is each `control is Container` or
      `Parent.Children` assumption.
- [ ] Make `Control` expose internal allocation-free registry navigation used by
      every cross-cutting algorithm.
- [ ] Find scrollable ancestors by walking `Control.Parent` and selecting actual
      `Container { AutoScroll: true }` nodes.
- [ ] Remove every cross-cutting `Container?` ancestry loop.
- [ ] Re-run focus, routing, pointer, styling, scrolling, popup, window, menu,
      and randomized-tree tests.

## Task 8: Prove the showcase no longer needs privileged mutation

**Production:**

- Modify `src/SharpVision.Showcase/Panes/ShowcasePanel.cs`.
- Modify `src/SharpVision.Showcase/Controls/NavigationItem.cs`.
- Modify `src/SharpVision.Showcase/Controls/PointerProbe.cs`.
- Modify `src/SharpVision/AssemblyMarker.cs`.

- [ ] Migrate showcase-derived property setters to `SetProperty` and explicit
      protected invalidation.
- [ ] Delete `InternalsVisibleTo("SharpVision.Showcase")`.
- [ ] Keep test friendship until internal invariant tests are deliberately
      replaced; do not friend the consumer project.
- [ ] Run `SharpVision.Showcase.Tests` and the consumer assembly-boundary test.

## Task 9: Foundation documentation and full gate

**Docs:**

- `docs/controls/control.md`;
- `docs/concepts/custom-components.md` (foundation portions only);
- `docs/concepts/layout.md`;
- `docs/concepts/focus.md`;
- `docs/concepts/input-routing.md`;
- `docs/concepts/lifecycle-events.md`;
- `docs/concepts/styling.md`;
- `docs/concepts/theming-new-controls.md`;
- `docs/architecture/project-structure.md`;
- `docs/testing/controls-integration.md`.

- [ ] Remove claims that friend tests constitute third-party proof.
- [ ] Document `Parent : Control?`, owned roles, protected kernel validation,
      transaction publication order, and the consumer project.
- [ ] Remove stale `ScrollView` references from integration-test docs.
- [ ] Run:

  ```bash
  make format
  make lint
  make build
  make test
  ```

- [ ] Confirm a clean worktree after commits, zero warnings/errors, and a test
      count no lower than baseline plus all new consumer/foundation tests.
