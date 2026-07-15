# API conformance: WPF-style overrides, `View`/`Build()`, and a lean showcase

## Status

Design approved 2026-07-13 and implemented. Its `View`/`Build()` and showcase
composition decisions remain current. The original inventory snapshots are
superseded by the later
[intrinsic scrolling](2026-07-14-intrinsic-container-scrolling-design.md) and
[intrinsic border/shadow](2026-07-15-intrinsic-border-shadow-design.md) designs;
the live catalog is normative in the
[showcase contract](../../architecture/showcase.md#showcase-contract). The
problem statements, code sketches, and implementation precondition below record
their execution-base state; live normative docs win if later designs supersede
another detail.

## Problem

SharpVision is a retained-mode, WPF-shaped terminal UI toolkit (`Length`,
`Thickness`, `HorizontalAlignment`, `ICommand`, `Measure`/`Arrange`, routed
events, `Style`/`Theme`). Its conventions are mostly sound, but three things
make it hard to derive controls and hard to learn:

1. **Layout/render extension points are named `*Core`.** `MeasureCore`,
   `ArrangeCore`, `RenderCore` are the correct non-virtual-interface
   (template-method) seams, but the `*Core` suffix is the WinForms idiom on an
   otherwise WPF-shaped API, so it does not match what a .NET UI developer
   expects to override.
2. **There is no blessed way to build a custom component or screen.** Every
   concrete control is `sealed` except `Stack`, and `Stack` is unsealed for a
   single reason: the showcase needed a container it could subclass with a
   "build my children" hook, so `ShowcasePane : Stack` (its only subclass in the
   repo) hand-rolled `protected abstract void BuildExamples(...)`. The framework
   forced its flagship app to invent the very pattern users keep asking for.
   `Screen` has no "build the UI" hook at all — you assemble the whole tree in
   the constructor (see `Gallery()`, a 78-line constructor).
3. **The showcase — the intended exemplar — is convoluted.** It renames all 22
   controls to `ControlButton`/`ControlText`/… (the exact `XxxControl`
   anti-pattern `AGENTS.md` forbids), forces every page through a mandatory
   validated-metadata base class, routes construction through factory delegates,
   and (until recently) shipped two parallel documentation engines encoding the
   same 23 controls twice. "Show a Button" is a metadata dump plus a catalog
   edit plus a templated override, not an at-a-glance example.

## Goals

- Make the override surface match .NET UI expectations.
- Provide one obvious, safe way to build a reusable component and a screen.
- Make the showcase demonstrate the real API tersely, with real type names.
- Keep every behavior, invariant, and correctness guarantee unchanged.

## Non-goals

- No reactive rendering, virtual DOM, reconciliation, or hook-style state.
  `AGENTS.md` forbids these and this design stays traditional-OOP. `Build()` is
  one-shot construction, not a `render()` that re-runs.
- No backward-compatibility shims. The library is pre-1.0 and spec-first;
  renames are hard renames applied across code, docs, and tests in one change.
- No changes to property names, event names, the `On*` responder family,
  `Activate`, styling, theming, layout math, or the runtime event loop.

## Decisions (locked)

| #   | Decision               | Choice                                                                                                         |
| --- | ---------------------- | -------------------------------------------------------------------------------------------------------------- |
| 1   | Extension-point naming | `MeasureCore`→`MeasureOverride`, `ArrangeCore`→`ArrangeOverride`, `RenderCore`→`OnRender` (WPF names)          |
| 2   | Composition model      | New `public abstract class View : Container` with `protected abstract Control Build()`; primitives stay sealed |
| 3   | `Stack`                | Re-sealed once the showcase no longer subclasses it                                                            |
| 4   | Control names          | Keep the concise names; delete the 22 showcase aliases                                                         |
| 5   | Showcase               | Full rewrite onto `View`/`Build()`                                                                             |
| 6   | Base type name         | `View`                                                                                                         |
| 7   | `Build()` shape        | Returns the single content root: `protected abstract Control Build()`                                          |

## Design

### 1. Extension-point rename (pure, behavior-preserving)

On `Control`, rename the three `protected virtual` hooks and every override
across the ~25 controls plus any test-only subclasses:

```csharp
// before                                   // after
protected virtual Size MeasureCore(Constraint c);   protected virtual Size MeasureOverride(Constraint c);
protected virtual void ArrangeCore(Rect b);         protected virtual void ArrangeOverride(Rect b);
protected virtual void RenderCore(TerminalCanvas c); protected virtual void OnRender(TerminalCanvas c);
```

The public, non-virtual wrappers `Measure(Constraint)`, `Arrange(Rect, …)`,
`Render(TerminalCanvas)` — which own margin/padding deflation, `Length`
resolution, min/max clamping, alignment, `Bounds`/`DesiredSize` commit, clip
setup, caching, and the reentrancy/exception-reinvalidate guards — are
**unchanged** except for the internal call site names. No signature, ordering,
or behavior changes. `RenderChrome`, `VisualBounds`, and the `RenderChildren`
internal virtual are unchanged. `OnRender` sits naturally beside the existing
`On*` responders, exactly as in WPF (which has both `OnRender` and
`On*Changed`/`OnMouseDown`).

### 2. `View`: the composition base

```csharp
/// <summary>A composable control whose content is produced once by <see cref="Build"/>.</summary>
public abstract class View : Container
{
    protected View() : base(capacity: 1) { }

    /// <summary>Produces this view's content tree. Called once by the runtime,
    /// on the view's first measure, whether or not it is attached. Must return non-null.</summary>
    protected abstract Control Build();

    // Framework-owned: installs Build()'s result as the single child, exactly once.
    // Default MeasureOverride/ArrangeOverride stretch that single child to the
    // content box (a transparent passthrough); derived views normally override
    // neither — they only implement Build().
}
```

Contract:

- **Single content root.** A `View` owns exactly one child (capacity 1), the
  value returned by `Build()`. Returning `null` throws a documented
  `InvalidOperationException`. To present multiple children, return a `Stack`,
  `Dock`, `Grid`, etc.
- **Called once, lazily, on first measure — attach-agnostic.** The runtime
  invokes `Build()` exactly once per instance, on the view's first
  `MeasureOverride`, whether or not the view is attached to a dispatcher at that
  point. This avoids the virtual-call-from-constructor trap without making
  construction depend on attachment. A `View` that is never measured never
  builds. (For a `Screen` specifically, the first measure always happens after
  `Attach`/`OnAttach` in the real runtime, so a screen's `Build()` can still
  rely on state configured in `OnAttach`; see the `Screen` section below.)
- **Not reactive.** There is no rebuild in v1. After `Build()`, the content tree
  is a normal mutable subtree; change it by mutating controls, adding/removing
  children, or toggling properties — the traditional way.
- **Passthrough layout.** `View` supplies default `MeasureOverride` (measure the
  built child, return its desired size) and `ArrangeOverride` (arrange the child
  to the full content rectangle). Derived views implement only `Build()` unless
  they need custom layout.

### 3. `Screen` becomes a `View`

```csharp
public abstract class Screen : View
{
    protected Screen() { HorizontalAlignment = Stretch; VerticalAlignment = Stretch; }

    protected Application? Application { get; }           // unchanged
    protected virtual void OnAttach(Application app) { }  // unchanged
    protected virtual void OnStarted(Application app) { } // unchanged
    protected virtual void OnDispose() { }                // unchanged
    // Screen no longer overrides layout: it inherits View's single-child passthrough.
}
```

Screen lifecycle order becomes explicit and clean:

1. `Attach(application)` binds `Application`, then calls **`OnAttach`**
   (app-level configuration — theme, capability overrides).
2. **`Build()`** composes the tree (may read the theme/`Application` configured
   in `OnAttach`).
3. First layout + first committed frame.
4. **`OnStarted`** (post-frame work — initial focus).
5. **`OnDispose`** on disposal.

Your envisioned pattern now works verbatim:

```csharp
public sealed class MyApp : Screen
{
    protected override Control Build() =>
        new Dock { Children = { BuildSidebar(), BuildContent() } };
}
```

### 4. Re-seal `Stack`; keep primitives sealed

Once `ShowcasePane` no longer derives from `Stack`, mark `Stack` `sealed`
(keeping its `CA1711` suppression as the intentional concise name). All
extension is through `View`/`Screen` (compose) or the abstract bases
`Container`/`Pressable` (new primitives). This is composition-over-inheritance,
matching WPF/WinForms `UserControl`.

### 5. Delete the aliases

Remove the 22 `global using ControlXxx = SharpVision.Controls.Xxx;` lines from
the showcase `GlobalUsings.cs`. The showcase uses `Button`, `Text`, `Stack`,
`Dock`, … directly, proving the concise names are usable in real application
code and satisfying `AGENTS.md`'s own "contextual identifiers" rule.

### 6. Showcase rewrite

Rebuild `SharpVision.Showcase` as the exemplar of the new model:

- Each pane is a small `View` with a `Build()` override, using real control
  names. Target ≈ 10 readable lines for a simple pane's live examples.
- `Gallery` is a `Screen` with `Build()` (sidebar + navigation + content host),
  keeping its current responsive behavior, keyboard/pointer navigation, theme
  switching, and scroll behavior.
- Remove mandatory-metadata coupling: documentation prose (property tables,
  interaction tables) becomes optional data a pane may supply, not a
  precondition the base constructor throws on. Keep it as plain data passed to a
  small, non-inheritance documentation helper — not a templated base class that
  dictates page structure via a constructor-time virtual call.
- Collapse the near-duplicate helpers (`SampleSection`/`CanvasSection`,
  `Card`/`DemoCard`) into a minimal shared set.
- Preserve the sidebar inventory and existing capability/mouse-mode setup. The
  implemented inventory is Button, Canvas, CheckBox, ComboBox, Dock, FigletText,
  Grid, List, Menu, Overlay, Popup, RadioButton, RichText, ScrollBar, Stack,
  Table, Text, TextInput, Window, and Theming. Later intrinsic capability
  designs removed wrapper-only pages; this list records the resolved current
  catalog rather than the superseded execution-base snapshot.

Example target shape for a pane's examples:

```csharp
public sealed class ButtonPane : View
{
    protected override Control Build()
    {
        var status = new Text("Waiting");
        var button = new Button { Content = new Text("Click or press Enter") };
        button.Click += (_, e) => status.Content = $"Activated: {e.Cause}";
        return new Stack { Spacing = 1, Children = { button, status } };
    }
}
```

## Error handling

- `Build()` returning `null` → documented `InvalidOperationException` at install
  time. `Build()` throwing propagates; the view is left unbuilt and the failure
  surfaces through the normal layout/exception path (same reinvalidate-on-throw
  guard the `*Override` wrappers already use).
- All existing validation (dispatcher affinity, disposed access, child
  ownership, capacity) is unchanged; a `View` reuses `Container`/capacity-1
  child validation for its single child.

## Testing

- **Rename:** existing behavior tests pass unchanged (the seams are protected
  and tested via public behavior). Update any test-only `Control` subclasses
  that override `*Core`. Grep tests for
  `MeasureCore`/`ArrangeCore`/`RenderCore`.
- **`View`/`Build()`:** new tests — `Build()` is called exactly once; it runs on
  the view's first measure, whether or not the view is attached; its result is
  installed as the sole child; a never-measured view does not build, attached or
  not; `null` return throws; an exception in `Build()` propagates and leaves the
  phase re-invalidated; mutating the built subtree afterwards behaves like any
  container mutation.
- **`Screen`:** lifecycle order `OnAttach → Build → first frame → OnStarted`;
  `Build()` can read the theme set in `OnAttach`; one end-to-end
  `RunConsoleAsync` path through a concrete `Screen` with `Build()`.
- **Sealing:** `typeof(Stack).IsSealed` is true.
- **Showcase:** the existing showcase test contract (exact inventory, per-page
  render at 30×8 / 80×24 / 140×40, wide-cell continuation, automatic scrolling,
  the full `Application` interaction drive, and the SGR mouse-mode startup
  lease) keeps passing against the rewritten panes; update fixtures to the new
  types.

## Documentation to update in the same change

- `docs/controls/control.md` — the "Layout extension points" section
  (`MeasureOverride`/`ArrangeOverride`/`OnRender`).
- `docs/concepts/screen.md` — add the `Build()` hook and the lifecycle order.
- New `docs/concepts/custom-components.md` (or a "custom components" section) —
  `View` + `Build()`, when to compose vs. derive from an abstract base.
- `docs/architecture/showcase.md` — the new pane structure.
- `AGENTS.md` — note `View`/`Build()` as the composition pattern and the
  `*Override`/`OnRender` seam names.

## Historical implementation precondition

The execution-base working tree was mid-`stash pop`: the git index recorded
unmerged paths (`Border/Button/Popup/Shadow/Text/Window.cs`, three `Styling/*`
files, the deleted `Catalog.cs`/`Examples.cs`), and `SharpVision.Showcase.Tests`
failed on style-rules-as-errors from the in-progress showcase refactor. The
production libraries and showcase compiled. Before implementation, that work had
to be finalized to a green `make build && make test` baseline. This paragraph is
retained as historical execution context, not current API or inventory guidance.

## Proposed phasing (for the implementation plan)

0. Green baseline (precondition above).
1. `*Core` → `MeasureOverride`/`ArrangeOverride`/`OnRender` across base, all
   controls, tests, and docs. Mechanical and isolated; verify no behavior
   change.
2. `View` base + `Build()` lifecycle; `Screen : View` with the reordered
   lifecycle; re-seal `Stack`; new/updated tests and docs.
3. Showcase rewrite onto `View`/`Build()`, aliases deleted, helpers collapsed;
   showcase tests updated.
4. Final docs + `AGENTS.md` sync and full quality-gate pass.
