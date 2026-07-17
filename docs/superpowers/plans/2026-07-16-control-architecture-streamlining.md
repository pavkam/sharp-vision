# Control Architecture Streamlining Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use
> superpowers:subagent-driven-development (recommended) or
> superpowers:executing-plans to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking.

**Goal:** Converge SharpVision on one deterministic retained-control model for
focus, pointer, navigation, visual state, theming, color, and rendering while
removing the generic style cascade and false component capabilities.

**Architecture:** Preserve `Control`, the owned-control registry, and the five
specialized authoring roles. Give focus, physical pointer/capture, and semantic
control behaviors separate authorities; traverse Tab hierarchically and arrows
within widgets; resolve local render-only visual state against an immutable
semantic palette and ordinary CLR overrides; keep terminal colors concrete; and
make intrinsic chrome part of a non-skippable render template.

**Tech Stack:** .NET 10, C# 14, xUnit v3, Shouldly, Microsoft Testing Platform,
SharpVision semantic cell rendering, JSON themes, Markdown/Prettier, and the
repository Make quality gates.

---

## Required reading and execution rules

Before changing files, read:

- `docs/superpowers/specs/2026-07-16-control-architecture-streamlining-design.md`;
- `.codex/skills/ui-controls/SKILL.md`;
- `.codex/skills/layout-input-events/SKILL.md`;
- `.codex/skills/docs-specifications/SKILL.md`;
- `.codex/skills/testing-quality/SKILL.md`;
- `.codex/skills/terminal-protocols/SKILL.md` and
  `.codex/skills/terminal-rendering/SKILL.md` for Tasks 10 through 14.

Use `superpowers:test-driven-development` for every behavior slice and
`superpowers:verification-before-completion` before every completion claim. Use
`superpowers:using-git-worktrees` to create an isolated branch named
`codex/control-architecture-streamlining`; the current workspace contains
user-owned edits in `Expander.cs`, `ExpanderPane.cs`, and `MenuItem.cs`.

Do not copy those dirty edits, discard them, or overwrite them. Tasks 9, 13C,
13G, and 16 may start only from an integration baseline in which the owner has
committed the relevant MenuItem/Expander edits or explicitly chosen how they are
superseded. All earlier independent tasks may proceed in the isolated worktree.

For every task:

1. update the named normative contract before implementation;
2. add one focused public-behavior test and observe the expected failure;
3. make the smallest complete architectural change for that task;
4. run the focused fixture and its nearest existing fixtures;
5. update XML documentation and showcase proof in the same slice where public
   behavior changes;
6. stage only the task’s files and make the suggested small commit;
7. keep build warnings at zero.

Never retain both old and new authorities for a state fact. Temporary adapters
may translate old data into the new internal representation, but no control may
be observably focused, hovered, pressed, selected, or styled by two systems.

## Target file structure

The exact inventory is intentionally stated up front so implementation does not
grow another private framework mid-migration.

### Create

- `src/SharpVision/Controls/ControlInteractionState.cs` — local state commits
  and notification coalescing for one Control.
- `src/SharpVision/Controls/PressBehavior.cs` — semantic keyboard/pointer
  activation and capture cleanup.
- `src/SharpVision/Controls/ItemNavigator.cs` — shared current-item and roving
  navigation algorithms.
- `src/SharpVision/Controls/RadioGroupCoordinator.cs` — ownership-root and
  exact-slot radio membership, exclusivity, and roving Tab entry.
- `src/SharpVision/Controls/ContainerScrollController.cs` — generated scrollbar
  state and behavior extracted when Container is migrated.
- `src/SharpVision/Input/InteractionTargets.cs` — immutable resolved physical,
  delivery, focus, and capture targets.
- `src/SharpVision/Input/PointerManager.cs` — physical hit path, pointer-over,
  delivery, and capture authority.
- `src/SharpVision/Input/FocusReason.cs` — focus cause.
- `src/SharpVision/Input/PostRouteCommand.cs` — None, TabNext, and TabPrevious
  application commands requested by a completed control default.
- `src/SharpVision/Input/RouteResult.cs` — handled state, post-route command,
  and validated traversal anchor.
- `src/SharpVision/Input/PointerCaptureLossReason.cs` — capture loss cause.
- `src/SharpVision/Input/FocusWithinEventArgs.cs` — focus path transition event
  data if existing focus arguments cannot express it without changing meaning.
- `src/SharpVision/Input/TerminalFocusEventArgs.cs` — renamed routed terminal
  focus transition, distinct from control focus lifecycle events.
- `src/SharpVision/Styling/ThemeColor.cs` — concrete-or-role UI color token.
- `src/SharpVision/Styling/ThemePalette.cs` — immutable role map.
- `src/SharpVision/Styling/Appearance.cs` — unresolved immutable control
  appearance used by protected defaults.
- `src/SharpVision/Styling/AppearanceResolver.cs` — ambient text, local state,
  ThemeColor, and cache resolution.
- `src/SharpVision/Styling/LegacyAppearanceDefaults.cs` — short-lived read-only
  bridge between immutable palette themes and the old resolver; delete in
  Task 13.
- `tests/SharpVision.Tests/Input/KeyboardRoutingTests.cs` — application-level
  key target/default/global command ordering.
- `tests/SharpVision.Tests/Input/HierarchicalFocusTests.cs` — parent-local Tab,
  active descendant, and scope repair.
- `tests/SharpVision.Tests/Styling/ThemeColorTests.cs` — UI token validation and
  resolution.
- `tests/SharpVision.Tests/Styling/AppearanceResolverTests.cs` — deterministic
  overlay, ambient, local override, and cache behavior.
- `tests/SharpVision.Tests/Styling/AppearanceInheritanceTests.cs` — text-only
  ambient propagation and popup/item boundaries.
- `tests/SharpVision.Tests/Styling/VisualStateResolutionTests.cs` — local state
  and fixed overlay order.
- `tests/SharpVision.Tests/Controls/ItemNavigatorTests.cs` — eligibility,
  repair, wrap, paging, and roving-entry algorithms.
- `tests/SharpVision.Tests/Controls/RadioGroupCoordinatorTests.cs` — root/slot
  membership, atomic regrouping, exclusivity, and roving Tab entry.
- `tests/SharpVision.Tests/Controls/TabControlTests.cs` if no dedicated fixture
  exists when Task 8 begins.
- `tests/SharpVision.Tests/Controls/SeparatorTests.cs`, `ProgressBarTests.cs`,
  `ExpanderTests.cs`, and `GroupBoxTests.cs` if the concurrent control-surface
  plan has not created them before Task 13.
- `tests/SharpVision.Terminal.Tests/Protocols/ColorKindTests.cs` — exhaustive
  closed terminal color representation coverage.

Every named C# type gets exactly one file named after it. If a proposed event
argument reuses an existing type cleanly, extend the existing type instead of
creating a synonym.

### Rename or replace

- Replace `src/SharpVision/Input/CaptureManager.cs` with `PointerManager.cs`;
  keep a stateless forwarding facade only until Tasks 9 and 16 migrate the dirty
  PressInteraction callers.
- Replace `src/SharpVision/Controls/PressInteraction.cs` with
  `PressBehavior.cs`.
- Rename `State` and `VisualStates` to `VisualState` and an internal
  `VisualStateOrder` only if a helper remains necessary.
- Replace mutable Theme snapshots/context with the immutable `Theme` and
  `ThemePalette` references.

Use a filesystem-aware rename only after all references are identified. Do not
leave forwarding duplicate classes after their final caller-migration task is
green; CaptureManager’s explicitly stateless facade ends in Task 16.

### Delete after the final caller migrates

- `src/SharpVision/Controls/Control.StyleProperties.cs`
- `src/SharpVision/Controls/Control.ThemeValues.cs`
- `src/SharpVision/Styling/FillMode.cs`
- `src/SharpVision/Styling/ControlHierarchy.cs`
- `src/SharpVision/Styling/ControlStyle.cs`
- `src/SharpVision/Styling/ControlStyleSnapshot.cs`
- `src/SharpVision/Styling/IControlStyle.cs`
- `src/SharpVision/Styling/IStyleLifecycle.cs`
- `src/SharpVision/Styling/IStyleProperty.cs`
- `src/SharpVision/Styling/IStyleScope.cs`
- `src/SharpVision/Styling/LegacyAppearanceDefaults.cs`
- `src/SharpVision/Styling/SemanticColor.cs`
- `src/SharpVision/Styling/StyleProperty.cs`
- `src/SharpVision/Styling/StylePropertyRegistry.cs`
- `src/SharpVision/Styling/ThemeChangedEventArgs.cs`
- `src/SharpVision/Styling/ThemeColors.cs`
- `src/SharpVision/Styling/ThemeContext.cs`
- `src/SharpVision/Styling/ThemeResolver.cs`
- `src/SharpVision/Styling/ThemeSnapshot.cs`

Delete the corresponding obsolete tests only in the task that adds replacement
behavior tests. A lower test count without replacement proof is a regression.

## Phase A — contracts and interaction authorities

### Task 1: Publish the shared behavioral contracts and baseline evidence

**Files:**

- Modify: `docs/concepts/focus.md`
- Modify: `docs/concepts/input-routing.md`
- Modify: `docs/concepts/styling.md`
- Modify: `docs/concepts/themes.md`
- Modify: `docs/architecture/rendering-pipeline.md`
- Modify: `docs/controls/control.md`
- Modify: `docs/controls/pressable.md`
- Modify: `docs/controls/items-control.md`
- Modify: `docs/testing/controls-integration.md`

- [ ] **Step 1: Capture the clean baseline**

Run these commands in the isolated worktree and save their exact summary in the
task notes:

```bash
git status --short --branch
dotnet test --project tests/SharpVision.Tests/SharpVision.Tests.csproj \
  --filter-class "*FocusTests" --timeout 60s
dotnet test --project tests/SharpVision.Tests/SharpVision.Tests.csproj \
  --filter-class "*PointerTests" --timeout 60s
dotnet test --project tests/SharpVision.Tests/SharpVision.Tests.csproj \
  --filter-class "*ThemeResolverTests" --timeout 60s
```

Expected: the baseline tests pass. Record any pre-existing failure rather than
changing an intended assertion to make it disappear.

- [ ] **Step 2: Replace shared normative definitions**

Specify all of these in one authoritative location and link from the other
pages:

- `Focusable` versus effective `CanFocus`;
- direct focus versus `ContainsFocus`;
- physical pointer-over versus captured delivery versus semantic pressed;
- per-node default routing and one application-level Tab fallback;
- handled widget cleanup followed by one explicit application command;
- hierarchical `Continue`, `Once`, `Cycle`, and `None` traversal;
- local `Current`, `Selected`, and `Checked` state;
- render-only visual-state invalidation and fixed overlay order;
- ambient text-only appearance;
- null/default/concrete Background semantics;
- immutable palette themes and concrete terminal colors;
- non-skippable intrinsic chrome order.

Also specify direct focus/pointer lifecycle event recipients and ordering,
reentrant request queuing, callback exception behavior, state-free ambient text,
explicit appearance/focus boundary metadata, logical focus persistence across
terminal focus loss, and the RadioGroupCoordinator ownership model.

Remove claims that Menu/NavigationView use a scope mode different from their
actual target policy. Remove the documented requirement for custom renderers to
call `RenderChrome`.

- [ ] **Step 3: Validate and commit the contract-only slice**

```bash
npx prettier --write docs/concepts docs/architecture docs/controls docs/testing
npm run lint:markdown
npm run lint:links
npm run test:docs
```

Expected: all documentation checks pass. Do not add long-lived red tests in this
task; each following task adds and runs its own regression immediately before
implementation. Commit `docs: define streamlined control contracts`.

### Task 2: Introduce the effective focus API and atomic focus transaction

**Files:**

- Modify: `src/SharpVision/Controls/Control.cs`
- Create: `src/SharpVision/Controls/ControlInteractionState.cs`
- Modify: `src/SharpVision/Input/FocusManager.cs`
- Create: `src/SharpVision/Input/FocusReason.cs`
- Modify: `src/SharpVision/Input/FocusChangingEventArgs.cs`
- Modify: `src/SharpVision/Input/FocusChangedEventArgs.cs`
- Create: `src/SharpVision/Input/FocusWithinEventArgs.cs`
- Create: `src/SharpVision/Input/TerminalFocusEventArgs.cs`
- Modify: `src/SharpVision/Input/Events.cs`
- Delete after callers migrate: `src/SharpVision/Input/FocusEventArgs.cs`
- Modify: `src/SharpVision/Controls/OwnedControlRegistry.cs`
- Modify: `src/SharpVision/Runtime/Application.cs`
- Modify: focusable controls under `src/SharpVision/Controls/`
- Modify: `tests/SharpVision.Tests/Input/FocusTests.cs`
- Modify: `tests/SharpVision.Tests/Controls/PropertyTests.cs`
- Modify: `tests/SharpVision.Tests/Controls/TreeTests.cs`
- Modify: `tests/SharpVision.Tests/Controls/OwnedControlRegistryTests.cs`
- Modify: `tests/SharpVision.Tests/Input/RoutingTests.cs`
- Modify: `tests/SharpVision.Tests/Integration/TerminalInputTests.cs`
- Modify: `tests/SharpVision.Consumer.Tests/` focus API specimens.

- [ ] **Step 1: Extend the failing focus surface**

Add:

```text
CanFocus_WhenFocusableButDetached_ReturnsFalse
CanFocus_WhenAttachedVisibleAndEnabled_ReturnsTrue
Focus_WhenCalledByConsumer_MovesFocusAndReturnsTrue
Focus_WhenChangingHandlerDisablesTarget_ReturnsFalseWithoutPartialState
ContainsFocus_WhenFocusMovesBetweenDescendants_DoesNotLeaveAndReenterOwner
Focus_WhenFocusedControlIsDetached_PerformsNonCancellableCleanup
Focus_WhenScopeIsReentered_RestoresItsActiveDescendant
Focus_WhenCallbacksRun_FollowsDirectAndLcaPathOrder
Focus_WhenRequestedReentrantly_QueuesAndRevalidatesAfterCurrentTransaction
Focus_WhenCallbackThrows_KeepsCommittedStateAndReleasesTransaction
TerminalFocus_WhenLost_PreservesLogicalControlFocusAndReleasesCapture
```

Assert the complete event order and public state observed inside each callback.
During Changing, old focus remains committed. During Lost/Got, the new atomic
state is already observable.

- [ ] **Step 2: Verify RED**

Run `*FocusTests` and the consumer fixture. Expected: compilation fails for
`Focusable`, `Focus()`, and `ContainsFocus`, then behavioral assertions fail as
the surface is introduced.

- [ ] **Step 3: Add configuration and effective eligibility**

Replace settable `CanFocus` with:

```csharp
public bool Focusable { get; set; }
public bool CanFocus { get; }
public bool Focus();
public bool IsFocused { get; }
public bool ContainsFocus { get; }
```

Effective `CanFocus` requires a live attached control, `Focusable`, effective
visibility, effective enabled state, and a valid focus manager. `Focus()`
returns false rather than throwing for an ineligible but otherwise valid
control. Public setters validate before observable state changes.

Set `Focusable = true` only in genuine interactive public controls. Keep the
base default false. Update probes to set `Focusable`, not computed `CanFocus`.

- [ ] **Step 4: Implement one focus commit**

Move direct and within flags behind `ControlInteractionState`. In
`FocusManager`:

1. validate and raise cancellable Changing;
2. revalidate after callbacks;
3. compute old/new paths and lowest common ancestor;
4. commit manager target, direct flags, within flags, and active descendants;
5. invalidate each changed control once;
6. raise old direct LostFocus, old-branch FocusLeft deepest-to-shallow,
   new-branch FocusEntered shallow-to-deep, new direct GotFocus, then Changed.

Focus callbacks are direct rather than routed. Queue reentrant focus requests
FIFO and revalidate after the active transaction. Callback exceptions do not
roll back state; release internal transaction state in `finally`, propagate the
first exception, and resume queued work on the next dispatcher turn.

Cleanup caused by disable, hide, detach, or disposal ignores cancellation,
preserves the original exception, and selects the documented repair target or
null. Terminal focus loss preserves logical focus/ContainsFocus, releases
capture and pressed state, and suspends input/cursor exposure until regain.

- [ ] **Step 5: Disambiguate terminal focus input**

Rename `Events.Focus` to `Events.TerminalFocusChanged` and `FocusEventArgs` to
`TerminalFocusEventArgs`. Migrate Runtime/Application, integration tests,
showcase handlers, and consumer specimens. Control Got/Lost/Entered/Left events
use their direct lifecycle argument types and never reuse routed terminal focus
arguments.

- [ ] **Step 6: Migrate callers and prove no mutable `CanFocus` remains**

```bash
rg -n '\bCanFocus\s*=' src tests
```

Expected: no output. Assertions may still read `CanFocus`.

- [ ] **Step 7: Verify GREEN and commit**

```bash
dotnet test --project tests/SharpVision.Tests/SharpVision.Tests.csproj \
  --filter-class "*FocusTests" --timeout 60s
dotnet test --project tests/SharpVision.Tests/SharpVision.Tests.csproj \
  --filter-class "*OwnedControlRegistryTests" --timeout 60s
dotnet test --project tests/SharpVision.Consumer.Tests/SharpVision.Consumer.Tests.csproj \
  --timeout 60s
```

Expected: all pass. Commit `refactor: make focus eligibility explicit`.

### Task 3: Fix key routing and run global commands once

**Files:**

- Modify: `src/SharpVision/Input/Router.cs`
- Create: `src/SharpVision/Input/PostRouteCommand.cs`
- Create: `src/SharpVision/Input/RouteResult.cs`
- Modify: `src/SharpVision/Runtime/Application.cs`
- Modify: `src/SharpVision/Controls/Control.cs`
- Modify: `tests/SharpVision.Tests/Input/RoutingTests.cs`
- Modify: `tests/SharpVision.Tests/Input/KeyboardRoutingTests.cs`
- Modify: `tests/SharpVision.Tests/Integration/TerminalInputTests.cs`
- Modify: `docs/concepts/input-routing.md`

- [ ] **Step 1: Add ordering and cancellation tests**

Add a recording tree with root, composite ancestor, and focused TextInput.
Assert:

```text
preview root
preview ancestor
preview target
bubble target
default target
```

When target default handles Left, ancestor bubble/default do not run. When no
node handles Tab, Application calls traversal once. A cancelled traversal still
consumes the Tab report. A key with no focused control routes to Root before the
global fallback.

Add a probe default that closes/removes its focused subtree, returns
ContinueWithApplicationCommand(TabNext or TabPrevious), and supplies a stable
outside anchor. Assert cleanup runs first, traversal runs once in the requested
direction, an unavailable anchor repairs to its live ancestor, and cancellation
does not repeat cleanup or traversal.

- [ ] **Step 2: Verify RED**

Run `*RoutingTests`, `*KeyboardRoutingTests`, and `*TerminalInputTests`.

- [ ] **Step 3: Pair each node’s handler and default**

Change Router so preview remains root-to-target, then for each node
target-to-root it invokes that node’s bubble handlers followed immediately by
that node’s default. Stop ordinary processing when handled. Preserve explicit
handled-event observers if the current contract supports them.

Remove Tab traversal from `Control.InvokeDefault`. Application owns the single
unhandled-Tab fallback after routing to `Focused ?? Root`. RouteResult can also
carry a handled control’s explicit TabNext/TabPrevious request and stable
traversal anchor; Application executes that post-route command exactly once.

- [ ] **Step 4: Verify exact event order and end-to-end input**

Run the three focused fixtures. Expected: the new ordering, first-Tab behavior,
and one-attempt behavior pass without changing unrelated pointer routing.

- [ ] **Step 5: Commit**

Commit `fix: route key defaults at each control boundary`.

### Task 4: Replace flat traversal with hierarchical parent-local scopes

**Files:**

- Modify: `src/SharpVision/Input/TabNavigation.cs`
- Modify: `src/SharpVision/Input/FocusManager.cs`
- Modify: `src/SharpVision/Controls/OwnedControlOptions.cs`
- Modify: `src/SharpVision/Controls/OwnedControlRegistry.cs`
- Modify: `src/SharpVision/Controls/Control.cs`
- Create: `tests/SharpVision.Tests/Input/HierarchicalFocusTests.cs`
- Replace intended assertions in:
  `tests/SharpVision.Tests/Input/TabNavigationScopeTests.cs`
- Modify: `tests/SharpVision.Tests/Controls/ContainerScrollTests.cs`
- Modify: `tests/SharpVision.Tests/Controls/TextInputTests.cs`
- Modify: `docs/concepts/focus.md`

- [ ] **Step 1: Write the traversal matrix**

Cover:

```text
MoveNext_WhenGrandchildTabIndexIsLower_DoesNotCompeteWithUncle
MovePrevious_WhenSiblingTabIndexesTie_UsesInsertionOrder
Continue_WhenSubtreeEnds_ContinuesOutside
Once_WhenScopeIsReentered_RestoresActiveDescendant
Once_WhenNoActiveDescendant_UsesSelfThenDirectionalDescendantFallback
Cycle_WhenEndIsReached_WrapsInsideScope
Cycle_WhenEnteredFromOutside_UsesDirectionalEndpointWithoutPrematureWrap
None_WhenSelfAndChildrenAreFocusable_ContributesSelfOnly
Continue_WhenSelfAndChildAreFocusable_OrdersBothByDirection
Traversal_WhenActiveChildIsRemoved_RepairsDeterministically
Traversal_WhenEveryCandidateIsUnavailable_LeavesFocusUnchanged
Container_WhenGeneratedBarsAppear_DoesNotChangeTabSequence
TextInput_WhenPrivateBarIsClicked_DoesNotMoveFocusToBar
```

- [ ] **Step 2: Verify RED**

Run `*HierarchicalFocusTests`, `*TabNavigationScopeTests`, and
`*ContainerScrollTests`. Expect flat-order, duplicate-mode, and private-bar
assertions to fail.

- [ ] **Step 3: Implement the recursive traversal algorithm**

For each owner, obtain only direct owned slots that participate in navigation,
sort by `(TabIndex, insertion order)`, and recurse by mode. Maintain active
descendant per Once/Cycle scope. Do not flatten the complete tree before
sorting.

Implement the exact mode contract: Continue contributes self then descendants
forward and the reverse order backward; Once contributes remembered eligible
descendant, then self, then first/last directional descendant; Cycle uses
Continue order and wraps only from inside; None excludes descendants but leaves
eligible self. Slot `participatesInNavigation: false` excludes the whole node.

Replace modes with Continue, Once, Cycle, None. Keep `Contained` only as a
short-lived obsolete alias inside this task if required to keep intermediate
commits compiling; remove it before the task commit.

Replace mutable `IsTabStop` with mutable configuration `TabStop` and read-only
effective `IsTabStop`. FocusManager uses only the effective value, combining
TabStop, CanFocus, slot navigation participation, and any coordinator policy.
Add property tests for config changes, effective notifications, unavailable
controls, and full-slot exclusion.

- [ ] **Step 4: Exclude framework parts by default**

Register generated Container and TextInput scrollbars with:

```csharp
Focusable = false;
TabStop = false;
participatesInNavigation: false;
```

A standalone ScrollBar constructor still sets `TabStop = true`; its effective
IsTabStop follows eligibility. Pointer use of private scrollbars must preserve
focus on the owning widget.

- [ ] **Step 5: Remove obsolete tests rather than preserving wrong behavior**

Replace tests that celebrate child → horizontal bar → vertical bar order with
proof that dynamic overflow does not alter public traversal. Replace identical
Cycle/Contained tests with distinct Continue/Once/Cycle/None contracts.

- [ ] **Step 6: Verify GREEN and commit**

Run all four focused fixtures plus `*FocusTests`. Commit
`refactor: make tab traversal hierarchical`.

### Task 5: Make PointerManager the sole physical pointer and capture authority

**Files:**

- Create: `src/SharpVision/Input/InteractionTargets.cs`
- Create: `src/SharpVision/Input/PointerManager.cs`
- Create: `src/SharpVision/Input/PointerCaptureLossReason.cs`
- Modify: `src/SharpVision/Runtime/PointerDevice.cs`
- Modify: `src/SharpVision/Runtime/Application.cs`
- Modify: `src/SharpVision/Controls/Control.cs`
- Modify: `src/SharpVision/Controls/OwnedControlRegistry.cs`
- Modify into a stateless facade: `src/SharpVision/Input/CaptureManager.cs`
- Modify: `src/SharpVision/Input/CaptureCancelledEventArgs.cs`
- Modify every direct capture consumer found in `src/`, consumer tests, and
  focused fixtures, including Control, ScrollBar, TextInput, and
  PressInteraction.
- Modify: `tests/SharpVision.Tests/Input/PointerTests.cs`
- Modify: `tests/SharpVision.Tests/Runtime/PointerDeviceTests.cs`
- Modify: `tests/SharpVision.Tests/Runtime/ApplicationPointerTests.cs`
- Modify: `tests/SharpVision.Tests/Integration/PixelPointerTests.cs`
- Modify: `docs/concepts/input-routing.md`

- [ ] **Step 1: Add physical-path and capture-loss tests**

Add:

```text
Move_WhenLeafChanges_UpdatesPointerOverOnBothAncestryPathsInOrder
Layout_WhenStationaryPointerHasANewHit_RecomputesPointerOverBeforeFrame
Visibility_WhenHoveredLeafBecomesHidden_RehitsStoredPointer
Capture_WhenTransferred_NotifiesPreviousOwnerBeforePublishingNewOwner
Capture_WhenOwnerIsDetached_RaisesLossAndClearsDelivery
Press_WhenSecondaryButtonTargetsPassiveLeaf_DoesNotSetPressedState
```

Assert deepest-old exits before new-path enters and that pointer coordinates
remain cell/pixel faithful.

- [ ] **Step 2: Verify RED**

Run pointer, application pointer, and pixel pointer fixtures.

- [ ] **Step 3: Introduce resolved interaction targets**

`InteractionTargets` carries physical leaf/path, captured delivery target,
nearest focus target, and capture owner. Keep it immutable and validate
construction. Do not cache it across tree mutation. Routed PressBehavior, not
this pre-route value, owns semantic activation.

- [ ] **Step 4: Replace hover ownership with physical ancestry**

Expose `IsPointerOver`, `IsPointerDirectlyOver`, and `HasPointerCapture`. Remove
`OwnsPointerState`, `OwnsHover`, and related test overrides. PointerManager
diffs ancestry and commits enter/exit state through `ControlInteractionState`.

- [ ] **Step 5: Make capture transfer observable**

Use a two-phase transfer: validate the candidate; clear the former owner and its
effective capture flag; notify it with Transferred while no new owner is
observable; revalidate; then commit the candidate. Queue reentrant capture
requests FIFO. If the callback throws, finish cleanup, still commit an eligible
candidate, then rethrow the first exception. Handle Explicit, Unavailable, and
TerminalFocusLost with the same invariant that no stale owner remains.

- [ ] **Step 6: Remove semantic press mutation from the pointer manager**

PointerManager chooses delivery and primary-click focus but never calls a
generic `SetPressed` on the raw leaf. Remove raw pressed state from
PointerDevice if it claims semantic ownership; expose physical button state
under an unambiguous name only if consumers need it.

- [ ] **Step 7: Re-hit after committed geometry changes**

Queue one pointer re-hit after layout/resize/visibility/hit-test changes.
Coalesce multiple invalidations and complete it before rendering the next frame.
Never perform recursive layout from pointer state.

- [ ] **Step 8: Verify GREEN and commit**

Before verification, inventory every caller:

```bash
rg -n 'CaptureManager|CaptureOwner|CaptureCancelled|Capture\(|ReleaseCapture' \
  src tests/SharpVision.Consumer.Tests tests/SharpVision.PackageConsumer
```

Migrate all clean callers to PointerManager. Keep CaptureManager only as a
stateless forwarding facade over the same PointerManager for dirty
PressInteraction callers; it stores no capture state and raises no independent
events. Run all named fixtures plus consumer tests and a solution build. Commit
`refactor: centralize physical pointer state`.

### Task 6: Replace PressInteraction with one semantic PressBehavior

**Files:**

- Create: `src/SharpVision/Controls/PressBehavior.cs`
- Modify: `src/SharpVision/Controls/Pressable.cs`
- Modify: `src/SharpVision/Controls/Button.cs`
- Modify: `src/SharpVision/Controls/CheckBox.cs`
- Modify: `src/SharpVision/Controls/RadioButton.cs`
- Modify: `src/SharpVision/Controls/ListItem.cs`
- Modify: `src/SharpVision/Controls/ScrollBar.cs`
- Modify: `src/SharpVision/Controls/ComboBox.cs`
- Retain temporarily: `src/SharpVision/Controls/PressInteraction.cs` only for
  the dirty MenuItem and Expander migrations deferred to Tasks 9 and 16.
- Modify: `tests/SharpVision.Tests/Controls/PressableTests.cs`
- Modify: `tests/SharpVision.Tests/Controls/ButtonTests.cs`
- Modify: `tests/SharpVision.Tests/Controls/CheckBoxTests.cs`
- Modify: `tests/SharpVision.Tests/Controls/RadioButtonTests.cs`
- Modify: `tests/SharpVision.Tests/Controls/ScrollBarTests.cs`
- Modify: `tests/SharpVision.Tests/Input/PointerTests.cs`
- Modify: `docs/controls/pressable.md`

- [ ] **Step 1: Add the complete press state machine tests**

Cover primary press, exit-disarm, re-entry rearm, inside release activation,
outside release, secondary button, keyboard press/release, capture transfer,
explicit capture loss, hide, disable, detach, disposal, terminal focus loss, and
callback exception cleanup.

The nested-content regression must assert:

```csharp
button.IsPressed.ShouldBeTrue();
text.IsPressed.ShouldBeFalse();
pointer.DeliveryTarget.ShouldBe(text);
pointer.CaptureOwner.ShouldBe(button);
```

- [ ] **Step 2: Verify RED**

Run `*PressableTests`, `*ButtonTests`, and the pointer regression.

- [ ] **Step 3: Bind one behavior directly to its owner**

Replace the delegate bundle with a constructor that validates and stores its
semantic owner plus a documented activation-bounds callback. PressBehavior
subscribes through internal control seams, owns capture/arming/IsPressed, and
has one idempotent cancellation path.

- [ ] **Step 4: Migrate activating controls one family at a time**

Migrate Pressable first, then Button/CheckBox/RadioButton, ListItem, ScrollBar,
and ComboBox. Run the nearest fixture after each family. Leave the user-owned
MenuItem and Expander files on their existing PressInteraction path until their
explicit reconciliation tasks. No individual control may have both behaviors. Do
not make NavigationViewItem inherit Pressable merely to reuse the behavior; Task
9 corrects that role.

- [ ] **Step 5: Verify no competing implementation remains**

```bash
rg -n 'PressInteraction|SetPressed\(' src tests
```

Expected at this checkpoint: PressInteraction references are confined to
MenuItem, Expander, their focused tests, and the temporary implementation file;
`SetPressed` is confined to the single internal state commit used by
PressBehavior/legacy adapter/test infrastructure. Task 16 removes the last
legacy references.

- [ ] **Step 6: Commit**

Commit `refactor: give press behavior one semantic owner`.

## Phase B — widget navigation and local behavior state

### Task 7: Introduce Current and shared ItemNavigator

**Files:**

- Create: `src/SharpVision/Controls/ItemNavigator.cs`
- Modify: `src/SharpVision/Controls/Control.cs`
- Modify: `src/SharpVision/Controls/ItemsControl.cs`
- Modify: `src/SharpVision/Controls/OwnedControlOptions.cs`
- Modify: `src/SharpVision/Controls/OwnedControlRegistry.cs`
- Modify: `tests/SharpVision.Tests/Controls/ItemsControlTests.cs`
- Create/modify: `tests/SharpVision.Tests/Controls/ItemNavigatorTests.cs`
- Modify: `docs/controls/items-control.md`

- [ ] **Step 1: Specify current versus selected**

State that Current is the keyboard/navigation anchor, Selected is committed data
selection, and focus may stay on the owning widget. Neither state is recursively
inherited. An owner deliberately commits these flags only to its realized item
root.

- [ ] **Step 2: Add algorithm tests**

Test empty inputs, first eligible, next/previous, optional wrap, Home/End/Page,
disabled/hidden/separator skipping, removal before/at/after current, collection
replacement, and roving Tab stop updates. Keep ItemNavigator independent of
rendering and control-specific selection policy.

- [ ] **Step 3: Verify RED and implement the smallest navigator**

Use callbacks or a narrow internal interface for count, eligibility, and item
lookup. Keep current index repair deterministic. Do not put List/Menu-specific
selection events in the navigator.

- [ ] **Step 4: Remove recursive selection propagation**

Delete `Control.SetSelectedState` descendant traversal. Add internal local
commits for Current and Selected. Update ItemsControl realization to apply them
only to the item face it owns.

- [ ] **Step 5: Verify and commit**

Run `*ItemNavigatorTests` and `*ItemsControlTests`. Commit
`refactor: separate current item from selection`.

### Task 8: Migrate List, radio groups, and TabControl navigation

**Files:**

- Modify: `src/SharpVision/Controls/List.cs`
- Modify: `src/SharpVision/Controls/ListItem.cs`
- Modify: `src/SharpVision/Controls/RadioButton.cs`
- Create: `src/SharpVision/Controls/RadioGroupCoordinator.cs`
- Delete after migration: `src/SharpVision/Controls/RadioGroup.cs`
- Modify: `src/SharpVision/Controls/OwnedControlRegistry.cs`
- Modify: `src/SharpVision/Controls/TabControl.cs`
- Modify: `src/SharpVision/Controls/TabItem.cs`
- Modify: `tests/SharpVision.Tests/Controls/ListTests.cs`
- Modify: `tests/SharpVision.Tests/Controls/RadioButtonTests.cs`
- Create: `tests/SharpVision.Tests/Controls/RadioGroupCoordinatorTests.cs`
- Create or modify: `tests/SharpVision.Tests/Controls/TabControlTests.cs`
- Modify: `docs/controls/collections/list.md`
- Modify: `docs/controls/input/radio-button.md`
- Modify: `docs/controls/collections/tab-control.md`
- Modify matching showcase panes and showcase screen tests.

- [ ] **Step 1: Add List contract tests**

Assert one external List Tab stop; no realized row/private bar Tab stops; focus
stays on List; CurrentIndex is public/readable as specified;
arrows/Home/End/Page move current without wrapping; unavailable rows are
skipped; selection follows only the configured selection mode/modifiers; nested
editor arrows remain with the editor.

- [ ] **Step 2: Add radio roving tests**

Assert exactly one eligible group member has `IsTabStop`; checked member wins,
otherwise first eligible; arrows wrap, focus, and check; disabled/hidden members
are skipped; programmatic check repairs the entry; removal repairs focus without
duplicate checked members.

`TabStop` remains caller configuration. Test setting it false on the current
entry, setting it true on another member, every member opting out, and
regrouping while configuration changes. The coordinator computes effective
read-only `IsTabStop` atomically and never overwrites caller settings.

Cover unnamed groups keyed by exact OwnedControlSlot and named groups keyed by
ordinal GroupName under one ownership root. Assert attach, regroup, reparent,
hide, disable, removal, and disposal update both affected groups atomically,
preserve stable tree order, and never retain a detached root.

- [ ] **Step 3: Add TabControl tests**

Assert one header-owner Tab stop, page descendants in hierarchical order,
Left/Right only when the header owner itself is focused, wrap/skip behavior,
selection/removal repair, and no arrow theft from page content.

Add explicit layout purity proof: measure and arrange may not raise page
Visibility changes. Selection/item mutation commits visibility before layout.

- [ ] **Step 4: Verify RED**

Run each control fixture separately and capture the intended failures.

- [ ] **Step 5: Migrate each control through ItemNavigator**

Keep focus on List/TabControl owners. RadioButton remains the direct focus
owner. One RadioGroupCoordinator per attached ownership root owns membership,
exclusivity, repair, and roving Tab eligibility; it may call ItemNavigator’s
pure next/previous helper over a group snapshot but ItemNavigator does not own
radio lifetime. Remove private item focusability unless a caller-supplied
semantic child explicitly opts in.

- [ ] **Step 6: Move TabItem visibility mutation out of layout**

Selection change, item insertion/removal, disable, and selected-page replacement
must commit visibility once. `MeasureOverride` and `ArrangeOverride` only read
the committed result.

- [ ] **Step 7: Verify, update showcase proof, and commit**

Run the four fixtures plus affected showcase tests. Commit
`refactor: use widget-level list radio and tab navigation`.

### Task 9A: Migrate Menu and the reconciled MenuItem

**Files:**

- Modify/reconcile: `src/SharpVision/Controls/Menu.cs`
- Modify/reconcile: `src/SharpVision/Controls/MenuItem.cs`
- Modify: `src/SharpVision/Controls/MenuSeparator.cs`
- Modify: `src/SharpVision/Controls/PressInteraction.cs`
- Modify: `tests/SharpVision.Tests/Controls/MenuTests.cs`
- Modify: `tests/SharpVision.Tests/Controls/MenuItemShortcutTests.cs`
- Modify Menu/MenuItem specs, showcase pane, and screen tests.

- [ ] **Step 1: Reconcile the user-owned MenuItem work**

Confirm the integration baseline contains the intended submenu disposal change.
Do not reintroduce direct popup disposal from a child if popup ownership now
belongs to a registry/lifecycle transaction. Record the chosen behavior in the
MenuItem spec before editing.

- [ ] **Step 2: Add Menu failing tests**

Assert each open Menu level, not each MenuItem, owns focus and uses
`TabNavigation.None`; exactly one eligible item is Current; arrows wrap and skip
separators/unavailable entries; submenu levels keep independent current state;
and item faces are never Tab stops.

Add forward and reverse Tab tests proving Menu snapshots an outside traversal
anchor, closes the complete chain, releases capture, returns the post-route
command, then traverses exactly once. Cancellation consumes the key without
reopening or repeating close.

- [ ] **Step 3: Verify RED**

Run `*MenuTests` and `*MenuItemShortcutTests` separately.

- [ ] **Step 4: Migrate behavior and navigation**

Move reconciled MenuItem from PressInteraction to PressBehavior. Use
ItemNavigator for Menu current/eligibility/wrap, keep selection/invocation
events on Menu, and remove item focus/TabIndex loops. Implement RouteResult
continuation for TabNext/TabPrevious.

- [ ] **Step 5: Verify, showcase, and commit**

Run Menu, routing, pointer, and matching showcase fixtures. Commit
`refactor: make menu navigation owner-focused`.

### Task 9B: Correct and migrate NavigationView roles

**Files:**

- Modify: `src/SharpVision/Controls/NavigationView.cs`
- Modify: `src/SharpVision/Controls/NavigationViewGroup.cs`
- Modify: `src/SharpVision/Controls/NavigationViewItem.cs`
- Modify: `src/SharpVision/Controls/NavigationViewSeparator.cs`
- Modify: `tests/SharpVision.Tests/Controls/ComponentRoleTests.cs`
- Modify: `tests/SharpVision.Tests/Controls/NavigationViewTests.cs`
- Modify NavigationView family specs, showcase pane, and screen tests.

- [ ] **Step 1: Add and run definitive role RED tests**

Assert NavigationView remains CompositeControl; NavigationViewGroup is
ItemsControl with one private host and no Children escape; NavigationViewItem is
a direct Control with Header/Glyph and no false Content property; item faces and
generated bars are not focusable or Tab stops.

Assert NavigationView itself owns focus with `TabNavigation.None`; Current and
Selected are distinct; Up/Down do not wrap; separators, disabled entries, and
collapsed group children are skipped; current scrolls into view; nested editors
retain their arrows.

Run `*ComponentRoleTests` and `*NavigationViewTests`; observe RED.

- [ ] **Step 2: Implement the selected roles and navigation**

Use PressBehavior directly from NavigationViewItem for pointer activation. Use
ItemNavigator from NavigationView for current repair/movement. Do not reopen the
inheritance decision during implementation.

- [ ] **Step 3: Verify and commit**

Run role, NavigationView, focus, pointer, and matching showcase fixtures. Commit
`refactor: align navigation view roles and current item`.

### Task 9C: Migrate ComboBox and transient focus restoration

**Files:**

- Modify: `src/SharpVision/Controls/ComboBox.cs`
- Modify: `src/SharpVision/Controls/Popup.cs`
- Modify: `src/SharpVision/Controls/Window.cs`
- Modify: `src/SharpVision/Controls/OwnedControlOptions.cs`
- Modify: `src/SharpVision/Controls/OwnedControlRegistry.cs`
- Modify: `tests/SharpVision.Tests/Controls/ComboBoxTests.cs`
- Modify: `tests/SharpVision.Tests/Controls/PopupTests.cs`
- Modify: `tests/SharpVision.Tests/Controls/WindowTests.cs`
- Modify corresponding specs, showcase panes, and screen tests.

- [ ] **Step 1: Add and run transient-policy RED tests**

ComboBox is the sole Tab stop closed/open and keeps focus; popup/List/ListItem
internals never enter traversal; arrows move Current without wrap; Enter
commits; Escape restores; forward/reverse Tab commits, closes, and continues
once through RouteResult.

Popup tests prove explicit appearance/focus boundary metadata independent of
render layer, prior-focus capture only when focus actually moves, deterministic
restoration to the nearest eligible target, and capture release on every close.
Window tests prove active-descendant restoration and initial directional entry.

Run the three fixtures separately and observe RED.

- [ ] **Step 2: Implement ComboBox current/commit behavior**

Use ItemNavigator for option current/repair. Preserve original selection until
commit. Snapshot the outside traversal anchor before Tab cleanup and return the
post-route command after commit/close.

- [ ] **Step 3: Implement explicit transient boundaries and restoration**

Store validated focus candidates without retaining detached trees. Set
AppearanceBoundary and FocusScopeBoundary metadata on Popup/Window roots
independently of popup render promotion. Restore after capture cleanup and
ownership removal but before the final frame; repair through the nearest live
scope when the candidate is unavailable.

- [ ] **Step 4: Verify and commit**

Run ComboBox, Popup, Window, routing, focus, pointer, and matching showcase
fixtures. Commit `refactor: make transient focus and tab continuation explicit`.

## Phase C — color, theme, appearance, and visual state

### Task 10: Close the terminal Color boundary and introduce ThemeColor

**Files:**

- Create: `src/SharpVision/Styling/ThemeColor.cs`
- Modify: `src/SharpVision.Terminal/Protocols/Color.cs`
- Modify: `src/SharpVision.Terminal/Protocols/ColorKind.cs`
- Modify: `src/SharpVision.Terminal/Rendering/CellStyle.cs`
- Modify: `src/SharpVision.Terminal/Rendering/Palette.cs`
- Modify: `src/SharpVision.Terminal/Protocols/Sgr.cs`
- Modify: `src/SharpVision.Terminal/Rendering/Encoder.cs`
- Modify: `src/SharpVision/Styling/ColorRole.cs`
- Modify: `src/SharpVision/Controls/Control.StyleProperties.cs`
- Modify: `src/SharpVision/Controls/ControlAppearance.cs`
- Modify: `src/SharpVision/Controls/Decoration.cs`
- Modify: `src/SharpVision/Controls/Table.cs`
- Modify: `src/SharpVision/Controls/Text.cs`
- Modify: `src/SharpVision/Text/Markup.cs`
- Modify: `src/SharpVision/Text/OpenTag.cs`
- Modify: `src/SharpVision/Text/Style.cs`
- Modify: `src/SharpVision/Text/StyleSpan.cs`
- Modify role-bearing files under `src/SharpVision/Styling/` through the
  short-lived ThemeColor adapter.
- Modify every current caller under `src/SharpVision.Showcase/`,
  `tests/SharpVision.Showcase.Tests/`, `tests/SharpVision.Consumer.Tests/`, and
  `examples/` reported by the Step 5 inventory.
- Modify: `tests/SharpVision.Terminal.Tests/Protocols/ColorHexTests.cs`
- Delete after replacement:
  `tests/SharpVision.Terminal.Tests/Protocols/ColorRoleKindTests.cs`
- Create: `tests/SharpVision.Terminal.Tests/Protocols/ColorKindTests.cs`
- Modify: `tests/SharpVision.Terminal.Tests/Protocols/SgrTests.cs`
- Modify:
  `tests/SharpVision.Terminal.Tests/Rendering/CellStyleRoleGuardTests.cs`
- Modify/delete after replacement:
  `tests/SharpVision.Terminal.Tests/Rendering/PaletteRoleGuardTests.cs`
- Modify/add: `tests/SharpVision.Terminal.Tests/Rendering/PaletteTests.cs`
- Create: `tests/SharpVision.Tests/Styling/ThemeColorTests.cs`
- Modify: `docs/architecture/rendering-pipeline.md`
- Modify: `docs/concepts/themes.md`

- [ ] **Step 1: Add exhaustive terminal representation tests**

For Default, Indexed boundary values, and RGB boundary values, assert projection
and exact SGR at Monochrome, Basic16, Indexed256, and TrueColor. Add an
exhaustive enum test: every defined ColorKind must construct, project, and
encode through a documented path.

Delete the test that treats Role as a valid terminal kind only after a
ThemeColor replacement test exists. Add compile-time/API proof through the
consumer project that unresolved ThemeColor cannot construct CellStyle.

- [ ] **Step 2: Add transparent-composition regression**

Draw a parent surface, draw a child with `BackgroundMode.Transparent`, encode at
all four depths, and assert the underlay survives. This test must use paint
mode, not a transparent Color sentinel.

- [ ] **Step 3: Verify current failure**

Add a focused characterization demonstrating the current transparent Color
projects differently across depths. Observe it fail against the target
expectation before removing the sentinel.

- [ ] **Step 4: Add ThemeColor**

Implement a small readonly value with explicit constructor validation,
concrete/role factories, implicit conversions from terminal Color and ColorRole,
and `TryGetColor`/`TryGetRole`. Do not add a conversion back to terminal Color.

Make `default(ThemeColor)` valid as concrete terminal default. Reject undefined
ColorRole values immediately and never expose an arbitrary numeric role id.

- [ ] **Step 5: Inventory and migrate every role/transparent caller first**

```bash
rg -l 'Color\.Role|Color\.Transparent|ColorKind\.Role|ColorKind\.Transparent|ThemeColors\.' \
  src tests examples | sort
```

Save the complete list in task notes and migrate every entry. In particular,
Text Markup/OpenTag/Style/StyleSpan must store ThemeColor through parsing and
resolve it only during Text rendering; Control/Table color style values use the
temporary ThemeColor adapter; showcase panes, showcase tests, consumer tests,
Snake/TextEditor examples, and all old role-guard tests must compile without a
terminal role color.

Run:

```bash
dotnet build SharpVision.slnx --no-restore
```

Expected: the solution builds while the now-unused terminal kinds still exist.
Do not proceed to removal until this passes.

- [ ] **Step 6: Remove non-terminal Color kinds**

Remove Role and Transparent from ColorKind; remove `Color.Role`, `RoleId`, and
`Color.Transparent`. Add honest indexed/RGB accessors and validate access by
kind. Make CellStyle accept every terminal Color value without runtime role
guards because invalid states are unrepresentable.

Keep `BackgroundMode.Transparent` unchanged. Replace ColorRoleKindTests with
ColorKindTests and replace/delete CellStyle/Palette role-guard fixtures with
compile-time/API tests proving ThemeColor cannot enter the terminal layer.

- [ ] **Step 7: Prove removed states cannot leak**

```bash
rg -n 'Color\.Role|Color\.Transparent|RoleId|ColorKind\.(Role|Transparent)' \
  src tests examples
```

Expected: no output outside historical migration docs.

- [ ] **Step 8: Verify and commit**

Run the named Terminal protocol/rendering fixtures, `*ThemeColorTests`,
`*MarkupTests`, `*TextTests`, showcase tests, consumer tests, and a fresh full
solution build. Commit `refactor: separate theme colors from terminal colors`.

### Task 11: Make Theme an immutable metadata-complete palette

**Files:**

- Create: `src/SharpVision/Styling/ThemePalette.cs`
- Create temporarily: `src/SharpVision/Styling/LegacyAppearanceDefaults.cs`
- Modify: `src/SharpVision/Styling/Theme.cs`
- Modify: `src/SharpVision/Styling/ThemeBuilder.cs`
- Modify: `src/SharpVision/Styling/ThemeDefinition.cs`
- Modify: `src/SharpVision/Styling/ThemeLoader.cs`
- Modify: `src/SharpVision/Styling/ThemeFile.cs`
- Modify: `src/SharpVision/Styling/ThemeCatalog.cs`
- Modify: `src/SharpVision/Styling/ThemeCatalogEntry.cs`
- Modify: `src/SharpVision/Styling/Themes.cs`
- Rename/internalize: `src/SharpVision/Styling/ThemeColorValue.cs`
- Modify: `src/SharpVision/Runtime/Application.cs`
- Modify: `src/SharpVision/Controls/OwnedControlRegistry.cs`
- Modify: `tests/SharpVision.Tests/Styling/ThemeTests.cs`
- Modify: `tests/SharpVision.Tests/Styling/ThemeBuilderTests.cs`
- Modify: `tests/SharpVision.Tests/Styling/ThemeLoaderTests.cs`
- Modify: `tests/SharpVision.Tests/Styling/ThemeCatalogTests.cs`
- Modify: `tests/SharpVision.Tests/Styling/CuratedThemesTests.cs`
- Modify: `tests/SharpVision.Tests/Styling/ThemeContextPropagationTests.cs`
- Modify: `docs/concepts/themes.md`

- [ ] **Step 1: Add immutable-theme tests**

Assert that loaded schema Version, Name, Slug, ColorScheme, Author, License, and
Source survive; catalog Order remains on ThemeCatalogEntry; every ColorRole is
present exactly once; invalid/missing/undefined roles fail before publication;
palette equality ignores source dictionary order; default ThemeColor resolves to
terminal default; `Theme.Resolve(ThemeColor)` resolves concrete and role tokens;
builder mutation does not alter the source Theme; and replacing
Application.Theme publishes one new reference on the dispatcher.

- [ ] **Step 2: Verify RED**

Run Theme, builder, loader, catalog, and propagation fixtures.

- [ ] **Step 3: Build immutable ThemePalette and Theme**

Defensively copy palette data once during construction. Expose read-only
metadata and palette. Remove public mutation, events, versions, freeze/clone,
and type-style dictionaries. `ThemeBuilder.From` is the only mutable editing
surface and returns a new Theme.

Until Task 13 removes the old style resolver, move the current fixed built-in
type recipe to one internal read-only `LegacyAppearanceDefaults` adapter. It is
not stored on Theme, cannot be customized, and is deleted with the resolver.
Control instance styles may remain only as the existing compatibility surface
until Task 13. This keeps every intermediate commit compiling without making
Theme mutable again.

Rename the JSON parser helper to an internal name that cannot be confused with
public ThemeColor, such as `ThemeColorParser`, and keep it in its own correctly
named file.

- [ ] **Step 4: Propagate one Theme identity**

Application owns one Theme reference. OwnedControlRegistry propagates that
reference during attach/reparent. Replacement clears descendant appearance
caches and coalesces Render invalidation. No control subscribes to mutable Theme
events.

- [ ] **Step 5: Repair curated API names**

Expose coherent Light/Dark names while preserving theme slugs. Remove the
misleading public White alias unless a compatibility decision explicitly keeps
it obsolete for one release.

- [ ] **Step 6: Verify and commit**

Run all named theme fixtures and consumer tests. Commit
`refactor: make themes immutable palettes`.

### Task 12: Move layout and behavior configuration to ordinary CLR properties

**Files:**

- Modify: `src/SharpVision/Controls/Control.cs`
- Modify: `src/SharpVision/Controls/Container.cs`
- Create: `src/SharpVision/Controls/ContainerScrollController.cs`
- Modify: `src/SharpVision/Controls/ScrollBar.cs`
- Modify: `src/SharpVision/Controls/TextInput.cs`
- Modify every control declaring non-appearance StyleProperty values.
- Modify: `tests/SharpVision.Tests/Controls/PropertyTests.cs`
- Modify: `tests/SharpVision.Tests/Controls/ControlBorderReservationTests.cs`
- Modify: `tests/SharpVision.Tests/Controls/ContainerScrollGeometryTests.cs`
- Modify: `tests/SharpVision.Tests/Styling/StylePropertyTests.cs`
- Modify: `docs/concepts/layout.md`
- Modify: `docs/concepts/styling.md`

- [ ] **Step 1: Inventory style properties by responsibility**

```bash
rg -n 'StyleProperty<|RegisterClassDefault|ResolveProperty\(' src/SharpVision
```

Classify every result as layout, behavior/configuration, or appearance. Record
the inventory in the task notes. No property may remain unclassified.

- [ ] **Step 2: Add direct-property invalidation tests**

Cover Margin, Padding, BorderThickness, Container ScrollBarChrome/Fill,
ScrollBar Chrome/Fill, TextInput ScrollBarChrome/Fill, label placement, glyph
sets, and any other non-color setting found by the inventory. Assert validation
happens before mutation and only the required phase invalidates.

- [ ] **Step 3: Verify RED where the public route changes**

Tests should call ordinary CLR properties and observe change notifications, not
style registry APIs.

- [ ] **Step 4: Convert geometry/configuration fields**

Use private backing fields and the existing `SetProperty` helper. Defaults live
with the declaring control. Remove their StyleProperty declarations and theme
entries. State cannot modify them.

Extract Container scroll behavior into ContainerScrollController while
preserving layout/ownership transactions and generated-part policy. Do not alter
the normative two-axis scrollbar feedback algorithm.

- [ ] **Step 5: Remove style-based layout invalidation**

Delete aggregate impact scanning for migrated properties. State changes request
Render only. Ordinary property setters explicitly request
Measure/Arrange/Render.

- [ ] **Step 6: Verify and commit**

Run property, layout, border, container scroll, ScrollBar, and TextInput
fixtures. Commit `refactor: make control configuration ordinary properties`.

### Task 13A: Establish AppearanceResolver as the single authority

**Files:**

- Create: `src/SharpVision/Styling/Appearance.cs`
- Create: `src/SharpVision/Styling/AppearanceResolver.cs`
- Modify: `src/SharpVision/Controls/ResolvedAppearance.cs`
- Modify: `src/SharpVision/Controls/ControlAppearance.cs` or replace it with the
  new resolver.
- Modify: `src/SharpVision/Controls/Control.cs`
- Modify: `src/SharpVision/Controls/Control.StyleProperties.cs`
- Modify: `src/SharpVision/Styling/ThemeResolver.cs` into a stateless forwarding
  compatibility adapter.
- Modify: `src/SharpVision/Styling/LegacyAppearanceDefaults.cs`
- Create: `tests/SharpVision.Tests/Styling/AppearanceResolverTests.cs`
- Create: `tests/SharpVision.Tests/Styling/AppearanceInheritanceTests.cs`
- Create: `tests/SharpVision.Tests/Styling/VisualStateResolutionTests.cs`
- Replace obsolete style/scope/resolver tests named in the delete inventory.
- Modify: `tests/SharpVision.Tests/Styling/ThirdPartyControlTests.cs`
- Modify: `tests/SharpVision.Consumer.Tests/` custom-control specimens.
- Modify: `docs/concepts/styling.md`
- Modify: `docs/concepts/theming-new-controls.md`

- [ ] **Step 1: Add appearance contract tests**

Prove:

```text
Resolve_WhenForegroundIsUnset_InheritsParentStateFreeForeground
Resolve_WhenBackgroundIsUnset_DoesNotInheritOrPaint
Resolve_WhenBackgroundIsDefault_PaintsOpaqueTerminalDefault
Resolve_WhenParentIsSelected_ChildDoesNotInheritSelectionOverlay
Resolve_WhenItemIsSelected_ChildTextUsesItemForegroundWithoutSelectedState
Resolve_WhenPopupIsOwnedBySelectedMenuItem_DoesNotReceiveSelectedState
Resolve_WhenDisabledAndChecked_DisabledAppearanceWins
Resolve_WhenLocalForegroundAndDisabled_DisabledOverlayStillWins
Resolve_WhenPointerFocusedPressed_AppliesFixedOrder
Resolve_WhenThemeIsReplaced_ReevaluatesStoredRoleToken
Resolve_WhenStateChanges_InvalidatesRenderOnly
ThirdPartyControl_WhenUsingProtectedAppearanceSeam_TracksThemeSwap
```

- [ ] **Step 2: Verify RED**

Run the new appearance fixtures, ThirdPartyControl tests, and consumer tests.

- [ ] **Step 3: Add the minimal direct appearance surface and boundary
      metadata**

Convert Foreground, Background, Attributes, Underline, UnderlineColor,
BorderColor, BorderAttributes, ShadowForeground, ShadowBackground, and other
genuine appearance values to ordinary validated properties using ThemeColor
where semantic roles are valid. Background null means no body fill. Do not add a
second generic property registry.

Back each appearance property with its value plus one local-assignment bit.
Construction leaves the bit clear; a setter marks it even for null; the getter
returns the configured normal value; `ResetAppearance()` clears all local
appearance bits. A local value replaces the type’s normal value, then semantic
state overlays still apply in fixed order. `Background = null` can override a
surface-control’s normal fill without reintroducing a transparent Color
sentinel; a meaningful Selected or Disabled overlay may still paint afterward.

Add explicit AppearanceBoundary and FocusScopeBoundary metadata independent of
render layer. Popup/Window/Screen establish it during construction/registration;
AppearanceResolver and FocusManager consume metadata without runtime type tests.

`Appearance` is an immutable unresolved value used by a protected control
default seam. `ResolvedAppearance` contains only concrete terminal values plus
the resolved BackgroundMode.

- [ ] **Step 4: Implement the six-step resolver**

Resolve control default appearance, state-free ambient text, explicit local
normal overrides, fixed active overlays ending with Disabled, then ThemeColor
tokens. Cache by control property revision, local VisualState, parent ambient
normal revision, and Theme identity. Never inherit a parent state overlay and
never walk arbitrary ancestor style scopes.

At this checkpoint the public enum may still carry its old `State` name. Treat
it only as the input bit set; do not preserve the old subset/comparator
algorithm. Task 14 performs the public rename and adds the final names without
changing the already-fixed overlay authority.

AppearanceResolver becomes the only resolver in this task. The old
ThemeResolver, type defaults, and instance Style surface are one-way inputs
through LegacyAppearanceDefaults; they own no cache or ordering. As each family
migrates, its legacy entries are removed. No control calls both resolvers.

- [ ] **Step 5: Verify the resolver foundation and commit**

Run `*AppearanceResolverTests`, `*AppearanceInheritanceTests`,
`*ThemeResolverTests`, Control property tests, Popup boundary tests, and
ThirdPartyControl tests. Commit `refactor: establish one appearance resolver`.

### Task 13B: Migrate display and inline-text appearance

**Files:**

- Modify: `src/SharpVision/Controls/Text.cs`
- Modify: `src/SharpVision/Controls/FigletText.cs`
- Modify: `src/SharpVision/Controls/Separator.cs`
- Modify: `src/SharpVision/Controls/ProgressBar.cs`
- Modify: `src/SharpVision/Controls/Decoration.cs`
- Modify: `src/SharpVision/Text/Markup.cs`
- Modify: `src/SharpVision/Text/OpenTag.cs`
- Modify: `src/SharpVision/Text/Style.cs`
- Modify: `src/SharpVision/Text/StyleSpan.cs`
- Create if still absent: `tests/SharpVision.Tests/Controls/SeparatorTests.cs`
- Create if still absent: `tests/SharpVision.Tests/Controls/ProgressBarTests.cs`
- Modify matching Text/FigletText/Separator/ProgressBar/Markup tests and specs.

- [ ] **Step 1: Add and run family RED tests**

Prove state-free ambient text inheritance, local normal overrides, late
ThemeColor resolution in nested markup spans, null background composition, and
theme replacement without reparsing content. Run `*TextTests`,
`*FigletTextTests`, `*SeparatorTests`, `*ProgressBarTests`, and `*MarkupTests`.

- [ ] **Step 2: Migrate the complete family**

Route every appearance read through ResolvedAppearance. Remove this family’s
legacy type defaults and resolver calls. Keep text glyph background transparent
unless markup explicitly supplies a background; do not infer opacity from a
nullable terminal Color.

- [ ] **Step 3: Verify and commit**

Run the five fixtures plus their mounted surface/showcase fixtures. Commit
`refactor: migrate display appearance`.

### Task 13C: Migrate pressable and toggle appearance

**Files:**

- Modify: `src/SharpVision/Controls/Pressable.cs`
- Modify: `src/SharpVision/Controls/Button.cs`
- Modify: `src/SharpVision/Controls/CheckBox.cs`
- Modify: `src/SharpVision/Controls/RadioButton.cs`
- Modify/reconcile: `src/SharpVision/Controls/Expander.cs`
- Create if still absent: `tests/SharpVision.Tests/Controls/ExpanderTests.cs`
- Modify matching control/style tests, specs, showcase panes, and screen tests.

- [ ] **Step 1: Reconcile Expander before touching the family**

Start only from the owner-approved baseline containing or deliberately
superseding the dirty Expander/chrome work. Record the baseline commit and run
Expander unit/showcase tests before adding migration assertions.

- [ ] **Step 2: Add and run family RED tests**

Prove PointerOver/Focused/Checked/Indeterminate/Pressed/Disabled fixed order,
Disabled after a local normal foreground, caller content inheriting state-free
normal text, no content-property mutation, and null Background overriding a
surface default. Run `*PressableTests`, `*ButtonTests`, `*CheckBoxTests`,
`*RadioButtonTests`, and `*ExpanderTests`.

- [ ] **Step 3: Migrate and remove compensation code**

Move the five controls to typed appearance profiles/overrides. Delete
CheckBox/RadioButton foreground synchronization. Remove their legacy theme
entries only after exact-cell tests pass.

- [ ] **Step 4: Verify and commit**

Run the five fixtures plus surface/showcase tests. Commit
`refactor: migrate pressable appearance`.

### Task 13D: Migrate panels, composition roots, and layout chrome appearance

**Files:**

- Modify: `src/SharpVision/Controls/Container.cs`
- Modify: `src/SharpVision/Controls/Canvas.cs`
- Modify: `src/SharpVision/Controls/Dock.cs`
- Modify: `src/SharpVision/Controls/Grid.cs`
- Modify: `src/SharpVision/Controls/Overlay.cs`
- Modify: `src/SharpVision/Controls/Stack.cs`
- Modify: `src/SharpVision/Controls/GroupBox.cs`
- Modify: `src/SharpVision/Controls/Prism.cs`
- Modify: `src/SharpVision/Controls/CompositeControl.cs`
- Modify: `src/SharpVision/Controls/Screen.cs`
- Create if still absent: `tests/SharpVision.Tests/Controls/GroupBoxTests.cs`
- Modify matching panel/composite/group/prism tests, specs, and showcase proof.

- [ ] **Step 1: Add and run family RED tests**

Prove panels do not inherit Background, normal text ambient values cross
ordinary composition roots, Screen begins an appearance context, and border/
shadow values resolve without state-driven geometry. Run `*Container*Tests`,
panel fixtures, `*CompositeControlTests`, `*GroupBoxTests`, and `*PrismTests`.

- [ ] **Step 2: Migrate the complete family**

Remove legacy family defaults and style resolution. Preserve all existing
measure/arrange/scroll algorithms and exact ownership behavior; this task
changes appearance only.

- [ ] **Step 3: Verify and commit**

Run named fixtures plus layout and showcase tests. Commit
`refactor: migrate panel appearance`.

### Task 13E: Migrate editor, scrollbar, and ComboBox appearance

**Files:**

- Modify: `src/SharpVision/Controls/TextInput.cs`
- Modify: `src/SharpVision/Controls/ScrollBar.cs`
- Modify: `src/SharpVision/Controls/ComboBox.cs`
- Modify matching tests, specs, showcase panes, and screen tests.

- [ ] **Step 1: Add and run family RED tests**

Prove editor cursor/selection roles, standalone versus generated ScrollBar
appearance, ComboBox open/current/disabled state, state-free content ambient
text, theme swap, and consistent null/default background behavior. Run
`*TextInputTests`, `*ScrollBarTests`, and `*ComboBoxTests`.

- [ ] **Step 2: Migrate the complete family**

Use typed cursor/selection/rail/thumb properties for meaningful customization.
Remove legacy family styles and direct SemanticColor calls. Keep conditional
body-clear removal for Task 15, but route its decision through
ResolvedAppearance.BackgroundMode now.

- [ ] **Step 3: Verify and commit**

Run the three fixtures plus pointer, surface, and showcase tests. Commit
`refactor: migrate editor and scrollbar appearance`.

### Task 13F: Migrate collection and item-face appearance

**Files:**

- Modify: `src/SharpVision/Controls/ItemsControl.cs`
- Modify: `src/SharpVision/Controls/List.cs`
- Modify: `src/SharpVision/Controls/ListItem.cs`
- Modify: `src/SharpVision/Controls/Table.cs`
- Modify: `src/SharpVision/Controls/TablePresenter.cs`
- Modify: `src/SharpVision/Controls/TabControl.cs`
- Modify: `src/SharpVision/Controls/TabItem.cs`
- Modify matching Items/List/Table/Tab tests, specs, and showcase proof.

- [ ] **Step 1: Add and run family RED tests**

Prove Current and Selected overlays belong only to each item face; descendant
text receives the face’s state-free normal ambient value; nested selectors keep
their own state; table/header and tab/page values use typed control properties.
Run `*ItemsControlTests`, `*ListTests`, `*TableTests`, and `*TabControlTests`.

- [ ] **Step 2: Migrate and remove style scopes**

Remove ItemsControl’s IStyleScope implementation and all collection-family
legacy entries. Owners pass explicit item appearance roles/properties to their
realized root; arbitrary descendant cascade is gone.

- [ ] **Step 3: Verify and commit**

Run the four fixtures plus selection, rendering, and showcase tests. Commit
`refactor: migrate collection appearance`.

### Task 13G: Migrate transient Menu, Popup, and Window appearance

**Files:**

- Modify/reconcile: `src/SharpVision/Controls/Menu.cs`
- Modify/reconcile: `src/SharpVision/Controls/MenuItem.cs`
- Modify: `src/SharpVision/Controls/MenuSeparator.cs`
- Modify: `src/SharpVision/Controls/Popup.cs`
- Modify: `src/SharpVision/Controls/Window.cs`
- Modify matching tests, specs, showcase panes, and screen tests.

- [ ] **Step 1: Confirm the Task 9A integration baseline**

Do not touch MenuItem until its owner-approved disposal/PressBehavior migration
is present. Run Menu/MenuItem tests before adding appearance assertions.

- [ ] **Step 2: Add and run family RED tests**

Prove selected/current MenuItem state does not enter Popup; Popup begins an
explicit state-free appearance context; Popup Disabled remains observable;
Window begins its own context; theme swaps and null/default backgrounds remain
consistent. Run `*MenuTests`, `*PopupTests`, and `*WindowTests`.

- [ ] **Step 3: Migrate and delete popup leakage patches**

Remove Popup’s forced Normal state, the hard-coded Popup stop from the legacy
resolver, and transient-family legacy entries. Boundary metadata and local state
must make each patch unnecessary.

- [ ] **Step 4: Verify and commit**

Run the three fixtures plus routing, focus, rendering, and showcase tests.
Commit `refactor: migrate transient appearance`.

### Task 13H: Migrate NavigationView appearance

**Files:**

- Modify: `src/SharpVision/Controls/NavigationView.cs`
- Modify: `src/SharpVision/Controls/NavigationViewGroup.cs`
- Modify: `src/SharpVision/Controls/NavigationViewItem.cs`
- Modify: `src/SharpVision/Controls/NavigationViewSeparator.cs`
- Modify NavigationView tests, specs, showcase pane, and screen tests.

- [ ] **Step 1: Add and run family RED tests**

Prove owner Focused/FocusWithin and item Current/Selected are distinct; Header/
Glyph/content text use state-free ambient normal appearance; separators and
groups do not receive leaf selection. Run `*NavigationViewTests`.

- [ ] **Step 2: Migrate and verify**

Remove all NavigationView legacy entries/resolver calls, run NavigationView,
role, rendering, and showcase fixtures, then commit
`refactor: migrate navigation appearance`.

### Task 13I: Remove the legacy style engine and prove extension ergonomics

**Files:**

- Delete every remaining old style/scope/snapshot file in the plan’s deletion
  inventory, including `LegacyAppearanceDefaults.cs`.
- Modify: `src/SharpVision/Controls/Control.cs`
- Delete: `src/SharpVision/Controls/Control.StyleProperties.cs`
- Delete: `src/SharpVision/Controls/Control.ThemeValues.cs`
- Delete/replace: `tests/SharpVision.Tests/Styling/StylePropertyTests.cs`
- Delete/replace: `tests/SharpVision.Tests/Styling/ControlStyleTests.cs`
- Delete/replace: `tests/SharpVision.Tests/Styling/StyleScopeTests.cs`
- Delete/replace: `tests/SharpVision.Tests/Styling/ThemeResolverTests.cs`
- Delete/replace:
  `tests/SharpVision.Tests/Styling/SemanticColorResolutionTests.cs`
- Delete/replace: `tests/SharpVision.Tests/Styling/ThemeColorsTests.cs`
- Rewrite: `tests/SharpVision.Tests/Styling/ThemeContextPropagationTests.cs` as
  immutable Theme identity/appearance cache propagation proof, renaming the
  file/type if its old name becomes false.
- Modify: `tests/SharpVision.Tests/Styling/ThirdPartyControlTests.cs`
- Modify: `tests/SharpVision.Consumer.Tests/` custom-control specimens.
- Modify: `tests/SharpVision.PackageConsumer/` custom-control specimens.
- Modify: `docs/concepts/styling.md`
- Modify: `docs/concepts/theming-new-controls.md`

- [ ] **Step 1: Add and run extension-surface RED tests**

An external custom control must declare ordinary validated configuration,
override the protected default-appearance/content-render seams, inherit ambient
normal text, use ThemeColor/ColorRole, reset local appearance, track theme swap,
and receive intrinsic chrome without style registration. Run ThirdPartyControl,
consumer, and package-consumer fixtures.

- [ ] **Step 2: Delete the adapter and old public engine**

Remove Control.Style/GetValue/SetValue/ClearValue, StyleProperty, ControlStyle,
IStyleScope, ThemeResolver, snapshots/contexts, ThemeColors, SemanticColor, and
their registries/events. Keep ordinary internal `SetProperty`.

- [ ] **Step 3: Prove removal and replacement coverage**

```bash
rg -n 'StyleProperty|ControlStyle|IStyleScope|ThemeResolver|ThemeSnapshot|ThemeContext|ThemeColors|SemanticColor|\.Style\s*=|GetValue\(|SetValue\(|ClearValue\(' \
  src tests/SharpVision.Consumer.Tests tests/SharpVision.PackageConsumer
```

Expected: no old engine references. Compare deleted and added test counts by
behavior category; validation, third-party authoring, state order, inheritance,
theme swap, cache invalidation, and background semantics all retain proof.

- [ ] **Step 4: Verify and commit**

Run all styling, rendering, Text, control, consumer, and package-consumer tests
plus a solution build. Commit `refactor: remove the legacy style engine`.

### Task 14: Rename and localize VisualState

**Files:**

- Modify/rename: `src/SharpVision/Styling/State.cs`
- Modify/replace: `src/SharpVision/Styling/VisualStates.cs`
- Modify: `src/SharpVision/Controls/Control.cs`
- Modify: `src/SharpVision/Controls/ControlInteractionState.cs`
- Modify all control state readers under `src/SharpVision/Controls/`.
- Modify all affected tests under `tests/SharpVision.Tests/`.
- Modify all affected control specs and showcase state labels.

- [ ] **Step 1: Add local-state composition tests**

Assert Normal, PointerOver, FocusWithin, Focused, Current, Selected, Checked,
Indeterminate, Pressed, and Disabled. Test every single flag and representative
combinations. Assert parent and implementation-child flags remain unchanged
unless the child is the explicitly selected/current item face.

- [ ] **Step 2: Verify RED for new names and states**

Run `*VisualStateResolutionTests` and StateModel tests.

- [ ] **Step 3: Introduce VisualState and fixed order**

Rename Hovered to PointerOver and add FocusWithin/Current. State assembly reads
only local behavior facts. Delete power-set enumeration and combination
comparator code. AppearanceResolver applies active single overlays in the
normative order with Disabled last.

- [ ] **Step 4: Remove broad override seams**

Derived controls may contribute their semantic local flags, but no subclass may
replace the complete standard state set to suppress inherited behavior. Replace
Popup’s whole-state override and similar patterns with correct local facts.

- [ ] **Step 5: Prove state is render-only**

Use recording controls to assert every state transition causes at most one
Render invalidation and zero Measure/Arrange invalidations. Pointer re-hit is a
separate post-layout operation, not caused by appearance state.

- [ ] **Step 6: Audit removed names**

```bash
rg -n '\bState\.(Hovered|Normal|Focused|Selected|Checked|Pressed|Disabled)|\bVisualStates\b|IsHovered' src tests docs
```

Expected: no obsolete API references outside the historical design/plan.

- [ ] **Step 7: Verify and commit**

Run visual-state, appearance, Button, CheckBox, RadioButton, List, Menu, Popup,
and NavigationView fixtures. Commit
`refactor: make visual state local and deterministic`.

## Phase D — rendering template and component role repair

### Task 15: Make background and intrinsic chrome rendering non-skippable

**Files:**

- Modify: `src/SharpVision/Controls/Control.cs`
- Modify: `src/SharpVision/Controls/ControlChrome.cs`
- Modify: `src/SharpVision/Controls/ChromeRenderOptions.cs`
- Modify: `src/SharpVision/Controls/ResolvedAppearance.cs`
- Modify every control overriding `OnRender`.
- Verify already deleted in Task 13I: `src/SharpVision/Styling/FillMode.cs`
- Modify: `tests/SharpVision.Tests/Styling/ControlChromeTests.cs`
- Modify: `tests/SharpVision.Tests/Controls/IntrinsicBorderTests.cs`
- Modify: `tests/SharpVision.Tests/Controls/IntrinsicShadowTests.cs`
- Modify: `tests/SharpVision.Tests/Controls/RenderingTests.cs`
- Modify exact-cell fixtures for every migrated control.
- Modify: `docs/architecture/rendering-pipeline.md`
- Modify: `docs/controls/control.md`
- Modify: `.codex/skills/ui-controls/SKILL.md`

- [ ] **Step 1: Inventory render overrides**

```bash
rg -n 'override void OnRender|RenderChrome\(' src/SharpVision/Controls
```

Record every control and classify it as ordinary content, specialized chrome, or
internal no-chrome presenter.

- [ ] **Step 2: Add template-order tests**

Use a custom third-party probe to assert shadow underlay, body fill, content,
normal-layer children, and border overlay in that exact order. Add controls with
custom content that never call a chrome helper and prove border/shadow still
render.

Include a documented unclipped-child probe that draws across its owner’s border;
the final border overlay must win. A promoted popup remains later and may cover
normal-layer cells.

Add null, terminal-default, concrete, and role-backed Background cases over a
known underlay at every terminal color depth.

- [ ] **Step 3: Verify RED**

Run ControlChrome, intrinsic border/shadow, and rendering fixtures.

- [ ] **Step 4: Seal the internal render sequence**

Control resolves appearance once and owns shadow, body fill, content,
normal-layer children, then border overlay. Rename the ordinary override to
`OnRenderContent`. Provide a narrow chrome options/override seam for specialized
frames; default chrome cannot be accidentally skipped or overwritten by normal
children.

Keep popup-layer child rendering in the application popup pass. Keep clipping,
wide-cell repair, and damage tracking identical.

- [ ] **Step 5: Migrate every override**

Ordinary controls draw content only. Button, Window, Popup, and GroupBox use the
specialized chrome seam. Internal presenters explicitly opt out. Remove all
manual `RenderChrome` calls after their exact-cell tests pass.

Reconcile GroupBox’s glyph property with inherited BorderGlyphs; keep one public
source of truth.

- [ ] **Step 6: Remove divergent clears and verify FillMode is absent**

Replace unconditional TextInput/Popup clears and Text’s private opacity rule
with `ResolvedAppearance.BackgroundMode`. Confirm Task 13I removed FillMode and
all redundant assignments from source, showcase, examples, docs, and tests.

- [ ] **Step 7: Verify inventory is exhausted**

```bash
rg -n 'RenderChrome\(|FillMode|override void OnRender\(' src tests docs
```

Expected: no public/manual RenderChrome use, no FillMode, and only the new
documented render hooks.

- [ ] **Step 8: Verify and commit**

Run all rendering, border, shadow, control exact-cell, and terminal frame tests.
Commit `refactor: make intrinsic chrome part of control rendering`.

### Task 16: Finish activation, ownership roles, and atomic transactions

**Files:**

- Modify/reconcile: `src/SharpVision/Controls/Expander.cs`
- Modify: `src/SharpVision/Controls/Table.cs`
- Modify: `src/SharpVision/Controls/TablePresenter.cs`
- Modify: `src/SharpVision/Controls/TabControl.cs`
- Modify: `src/SharpVision/Controls/OwnedControlRole.cs`
- Modify: `src/SharpVision/Controls/OwnedControlOptions.cs`
- Modify: `src/SharpVision/Controls/OwnedControlRegistry.cs`
- Delete after final caller migration: `src/SharpVision/Input/CaptureManager.cs`
- Modify/delete compatibility event APIs such as
  `src/SharpVision/Input/CaptureCancelledEventArgs.cs` after mapping them to the
  new capture-loss contract.
- Modify or delete: `src/SharpVision/Controls/OwnedControlLayer.cs` and part-key
  metadata only if the behavioral inventory proves them redundant.
- Modify: `tests/SharpVision.Tests/Controls/ComponentRoleTests.cs`
- Modify: `tests/SharpVision.Tests/Controls/OwnedControlRegistryTests.cs`
- Modify: `tests/SharpVision.Tests/Controls/NavigationViewTests.cs`
- Modify: `tests/SharpVision.Tests/Controls/ContentControlTests.cs`
- Modify: `tests/SharpVision.Tests/Controls/TableTests.cs`
- Modify: affected control specs, XML docs, showcase panes, and screen tests.

- [ ] **Step 1: Reconcile Expander work before editing**

Confirm whether the user-owned chrome/content-bounds changes are now committed.
Run Expander tests and showcase screen tests on that baseline. Preserve their
intent while moving activation to PressBehavior with header bounds.

- [ ] **Step 2: Add role contract tests**

Assert:

- the NavigationViewGroup/NavigationViewItem role decisions completed in Task 9
  still expose no false Children or Content capability;
- Expander Content remains the collapsible body while only its header activates;
- Table visible realization swaps atomically when template callbacks throw;
- TabControl layout remains mutation-free;
- framework roles drive navigation, state, appearance-boundary, and focus-scope
  policy independently of render layer;
- unused metadata is absent rather than ceremonial.

- [ ] **Step 3: Verify RED**

Run ComponentRole, NavigationView, ContentControl/Expander, Table, TabControl,
and OwnedControlRegistry fixtures.

- [ ] **Step 4: Correct each role**

Keep the Task 9 NavigationView role choices intact. Keep Expander as
ContentControl and migrate its reconciled header activation from
PressInteraction to PressBehavior. Keep Table as ItemsControl.

Do not make ItemsControl derive CompositeControl. If shared permanent-root code
is still duplicated, extract an internal helper with one initialization path.

Delete PressInteraction after Expander is green, then run:

```bash
rg -n 'PressInteraction' src tests
```

Expected: no output outside the historical design/plan.

After PressInteraction is gone, migrate any remaining facade caller to
PointerManager and delete the stateless CaptureManager compatibility facade.
Run:

```bash
rg -n 'CaptureManager|CaptureCancelled' src tests
```

Expected: no legacy capture authority/event names; the documented
LostPointerCapture surface remains.

- [ ] **Step 5: Make Table realization atomic**

Build and validate the next realization snapshot without attaching it. Commit
ownership replacement in one registry transaction. If template creation or
validation fails, leave the old visible snapshot and focus/capture state intact.

- [ ] **Step 6: Finish or remove role metadata**

Make role/options determine navigation participation, framework exposure, state
boundaries, appearance contexts, and focus scopes independently of render layer.
Introduce typed part keys only where code consumes the type guarantee. Delete
unused ItemVisual/string metadata if no behavior uses it.

- [ ] **Step 7: Verify and commit**

Run all named fixtures and affected showcase tests. Commit
`refactor: align component inheritance with semantic roles`.

### Task 17: Remove transitional residue and verify collaborator ownership

**Files:**

- Modify only to remove forwarding residue:
  `src/SharpVision/Controls/Control.cs`
- Modify only to remove forwarding residue:
  `src/SharpVision/Controls/Container.cs`
- Verify: `src/SharpVision/Controls/ControlInteractionState.cs`
- Verify: `src/SharpVision/Controls/ContainerScrollController.cs`
- Verify: `src/SharpVision/Styling/AppearanceResolver.cs`
- Verify: `src/SharpVision/Input/InteractionTargets.cs`
- Verify: `src/SharpVision/Input/PointerManager.cs`
- Verify without splitting: `src/SharpVision/Controls/OwnedControlRegistry.cs`

- [ ] **Step 1: Audit the exact transitional residue**

```bash
rg -n 'SetHovered|OwnsHover|OwnsPointerState|HasSelectedState|ThemeContext|_localValues|_resolvedPropertyCache|CaptureManager|PressInteraction|LegacyAppearanceDefaults' \
  src/SharpVision
rg -n 'FocusOwner|CaptureOwner' src/SharpVision/Controls/Control.cs
```

Expected: no legacy state/style/capture/press names. New focus/pointer manager
references may use explicitly renamed properties, but Control contains no second
state store.

- [ ] **Step 2: Verify the already-created ownership boundaries**

ControlInteractionState owns all local interaction flags and coalesced
notifications; PointerManager owns physical path/capture; AppearanceResolver
owns its one cache/order; ContainerScrollController owns scroll state; each was
implemented in its creation/migration task rather than as a shell. If the audit
finds logic still in the wrong owner, return to that owning task and its tests;
do not invent a second extraction here.

Keep ownership/lifecycle transactions in OwnedControlRegistry. Do not create
`Control.*.cs` partial files.

- [ ] **Step 3: Verify and commit only real residue removal**

Run `*Control*Tests`, `*Container*Tests`, focus, pointer, styling, and rendering
fixtures. If files changed, commit `refactor: remove control migration residue`;
otherwise record the audit as a no-change checkpoint.

## Phase E — public proof, documentation, and removal gates

### Task 18: Update every consumer-facing contract and showcase state matrix

**Files:**

- Modify all affected pages under `docs/concepts/`, `docs/architecture/`, and
  `docs/controls/`.
- Modify `docs/index.md` and category indexes only where links/names change.
- Modify XML documentation in every affected public/internal C# type.
- Modify all affected panes under `src/SharpVision.Showcase/Panes/`.
- Modify `src/SharpVision.Showcase/Gallery.cs` if catalog labels change.
- Modify matching screen tests under `tests/SharpVision.Showcase.Tests/`.
- Modify `tests/SharpVision.Consumer.Tests/`.
- Modify `tests/SharpVision.PackageConsumer/`.
- Modify examples referenced from docs.

- [ ] **Step 1: Run removed-API audits**

```bash
rg -n 'CanFocus\s*=|IsTabStop\s*=|OwnsPointerState|OwnsHover|IsHovered|Events\.Focus|\bFocusEventArgs\b|CaptureManager|PressInteraction|TabNavigation\.Contained|\bState\.|VisualStates|StyleProperty|ControlStyle|IStyleScope|ThemeResolver|ThemeSnapshot|ThemeContext|ThemeColors|SemanticColor|Color\.Role|Color\.Transparent|ColorKind\.(Role|Transparent)|FillMode|RenderChrome\(' \
  src tests docs examples
```

Expected: only intentional historical references in the two superpowers design
artifacts. Normative docs, source, tests, examples, and consumers contain none.

- [ ] **Step 2: Build showcase state matrices**

For Button, CheckBox, RadioButton, List, Menu, TabControl, NavigationView,
ComboBox, TextInput, Popup, Window, ScrollBar, and Expander, show the meaningful
Normal, PointerOver, Focused/FocusWithin, Current, Selected/Checked, Pressed,
and Disabled combinations. Do not manufacture unsupported states for
display-only controls.

Each interactive page demonstrates keyboard and pointer parity, Tab entry, arrow
behavior, theme swap, and transparent/opaque background behavior where relevant.

- [ ] **Step 3: Update consumer specimens**

External code must be able to:

- create a custom Control with ordinary validated properties;
- override the protected appearance and content-render seams;
- use ThemeColor/ColorRole and a concrete terminal Color correctly;
- observe default ThemeColor as terminal default and reset local appearance;
- call `Focus()` and observe `CanFocus`/`ContainsFocus`;
- create a composite and an items control without internal access;
- switch immutable themes through ThemeBuilder;
- render intrinsic border/shadow without a manual chrome call.

- [ ] **Step 4: Validate docs and showcase**

```bash
npm run format
npm run lint:markdown
npm run lint:links
npm run test:docs
dotnet test --project tests/SharpVision.Showcase.Tests/SharpVision.Showcase.Tests.csproj \
  --timeout 120s
dotnet test --project tests/SharpVision.Consumer.Tests/SharpVision.Consumer.Tests.csproj \
  --timeout 120s
```

Expected: all pass with no stale API names or screen mismatches.

- [ ] **Step 5: Commit**

Commit `docs: align control architecture and showcase proof`.

### Task 19: Run the complete quality gates and perform a final architecture audit

**Files:**

- Modify only files proven necessary by gate failures caused by this branch.
- Do not absorb unrelated upstream/user changes into cleanup commits.

- [ ] **Step 1: Run focused cross-layer suites**

```bash
dotnet test --project tests/SharpVision.Terminal.Tests/SharpVision.Terminal.Tests.csproj \
  --filter-class "*Color*Tests" --timeout 120s
dotnet test --project tests/SharpVision.Terminal.Tests/SharpVision.Terminal.Tests.csproj \
  --filter-class "*SgrTests" --timeout 120s
dotnet test --project tests/SharpVision.Tests/SharpVision.Tests.csproj \
  --filter-class "*FocusTests" --timeout 120s
dotnet test --project tests/SharpVision.Tests/SharpVision.Tests.csproj \
  --filter-class "*PointerTests" --timeout 120s
dotnet test --project tests/SharpVision.Tests/SharpVision.Tests.csproj \
  --filter-class "*Appearance*Tests" --timeout 120s
dotnet test --project tests/SharpVision.Tests/SharpVision.Tests.csproj \
  --filter-class "*RenderingTests" --timeout 120s
```

Expected: all discovered tests pass with zero warnings.

- [ ] **Step 2: Run the repository gates in required order**

```bash
make format
make lint
make build
make test
```

Expected: zero format drift, zero lint/link/doc failures, zero build warnings,
zero build errors, and all discovered tests/package-consumer checks pass at or
above the configured minimum.

- [ ] **Step 3: Re-run after formatting**

Because `make format` mutates files, inspect `git diff --check` and rerun at
least `make lint`, `make build`, and `make test` after the final formatting
change. Never claim the pre-format result as final evidence.

- [ ] **Step 4: Perform the architecture audit**

Confirm in source, not only docs:

- exactly one focus owner and transaction;
- exactly one physical pointer/capture owner;
- exactly one semantic press owner;
- handled Menu/ComboBox Tab cleanup can request exactly one directional
  application traversal from a stable outside anchor;
- no recursive behavior-state propagation;
- ambient text inheritance excludes parent state overlays;
- no private framework Tab stops;
- RadioGroupCoordinator owns root/slot membership and one roving member;
- no arbitrary state combination resolver;
- no state-driven layout properties;
- no generic style/scope/snapshot system;
- no unresolved/transparent terminal Color;
- no renderer can accidentally skip intrinsic chrome;
- normal-layer children cannot overwrite the final border overlay;
- every public role exposes only capability it implements;
- current/selection/focus are distinct for every item widget.

- [ ] **Step 5: Request review**

Use `superpowers:requesting-code-review` with this plan and design as the review
contract. Ask the reviewer to prioritize behavioral authority duplication,
public API ergonomics, terminal color invariants, event ordering, and proof that
removed tests were replaced rather than merely deleted.

- [ ] **Step 6: Integrate review corrections and rerun gates**

For each accepted correction, add/retain a failing regression first, apply the
smallest fix, and rerun its focused fixture plus all four repository gates.

- [ ] **Step 7: Commit the verified completion**

Stage only intentional branch files and commit
`refactor: streamline control architecture`. Record the exact gate summaries in
the handoff or pull request.

## Handoff checklist for the executing agent

Before declaring the branch ready, answer each item with a file/test reference:

- [ ] What object owns direct focus, and where is `ContainsFocus` committed?
- [ ] What object owns physical pointer-over and capture?
- [ ] What behavior alone may change `IsPressed`?
- [ ] Where does an unhandled Tab run exactly once?
- [ ] How is parent-local TabIndex ordering proven?
- [ ] Which private-part default prevents generated scrollbars from entering
      navigation?
- [ ] Where are Current and Selected represented separately for every selector?
- [ ] What exact fixed visual-state order makes Disabled win?
- [ ] Which appearance fields inherit and which never inherit?
- [ ] How does a null Background differ from terminal `Color.Default`?
- [ ] Why can ThemeColor never reach CellStyle unresolved?
- [ ] Which tests enumerate every terminal ColorKind at every color depth?
- [ ] What render method guarantees intrinsic chrome around custom content?
- [ ] Which former inheritance capabilities were removed or made real?
- [ ] Which tests prove layout methods are observationally pure?
- [ ] Which docs, showcase screens, consumer specimens, and gate outputs prove
      the final public contract?
