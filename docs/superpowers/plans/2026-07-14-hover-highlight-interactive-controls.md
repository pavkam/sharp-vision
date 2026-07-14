# Hover Highlight Scoped to Interactive Controls — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Hover feedback appears only on interactive controls; static content (`Text`, `Table`, `Grid`, layout containers) never highlights or re-renders on pointer move.

**Architecture:** Enforce "interactive = `CanFocus`" at the input-resolution layer. `Control.OwnsHover` follows `CanFocus`, and `CaptureManager.ResolveHover` resolves to the nearest interactive ancestor or `null` instead of falling back to the hit leaf. `GetVisualState()`, the theme, and pointer event routing are untouched.

**Tech Stack:** .NET 10, C#, xUnit v3, Shouldly assertions. Build/test/lint via `make`.

## Global Constraints

- .NET SDK 10.0.203 or a compatible latest patch in that feature band.
- Strict format and lint policy: `make format` then `make lint` must pass (includes `dotnet format --verify-no-changes`, C# type lint, markdown lint, doc link check, doc tests).
- `make test` requires at least the configured minimum discovered tests; an empty run cannot pass.
- Follow existing file conventions: file-scoped namespaces, XML doc comments on members, `Async` suffix on async test methods, tests wrapped in `dispatcher.InvokeAsync(...)` with `TestContext.Current.CancellationToken`.

Design spec: [docs/superpowers/specs/2026-07-14-hover-highlight-interactive-controls-design.md](../specs/2026-07-14-hover-highlight-interactive-controls-design.md)

---

### Task 1: Scope hover to interactive controls

This is one atomic change: `OwnsHover => CanFocus` and `ResolveHover` returning `null` must land together (either alone regresses behavior). Tests, production, and docs ship in a single commit.

**Files:**
- Modify: `src/SharpVision/Controls/Control.cs` (`OwnsHover` default + `IsHovered` doc)
- Modify: `src/SharpVision/Input/CaptureManager.cs` (`ResolveHover` fallback)
- Modify: `src/SharpVision/Controls/Pressable.cs` (remove redundant `OwnsHover` override)
- Modify: `src/SharpVision/Runtime/PointerDevice.cs` (`Hovered` XML doc)
- Test: `tests/SharpVision.Tests/Input/PointerTests.cs` (2 new tests, 2 existing updates)
- Docs: `docs/controls/control.md`, `docs/concepts/styling.md`

**Interfaces:**
- Consumes: existing `CaptureManager.Dispatch(Pointer)`, `CaptureManager.Hovered`, `Control.IsHovered`, `Control.CanFocus`, `Control.OwnsHover` (internal virtual), and test helpers `ProbeContainer : Container` / `ProbeControl : Control` (both non-focusable by default; `CanFocus` is public-settable) / `ProbePressable : Pressable`.
- Produces: no new public members. Behavior contract: only controls with `CanFocus == true` (directly or as the nearest ancestor of the hit control) become hover targets; `CaptureManager.Hovered` and `Control.IsHovered` are `null`/`false` over non-interactive content.

---

- [ ] **Step 1: Write the first failing test — non-interactive control is never hovered**

In `tests/SharpVision.Tests/Input/PointerTests.cs`, add this method inside the `PointerTests` class (place it immediately after `Dispatch_WhenPointerHitsChild_ProvidesLocalCoordinatesAsync`, before `HitTest_WhenChildrenOverlap...` or any convenient spot within the class):

```csharp
    /// <summary>Verifies a non-interactive hit target routes input but is never hovered.</summary>
    [Fact]
    public async Task Dispatch_WhenPointerHitsNonInteractiveControl_DoesNotHoverAsync()
    {
        await using Dispatcher dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            ProbeContainer root = new() { Bounds = new Rect(0, 0, 20, 10) };
            ProbeControl child = new() { Bounds = new Rect(0, 0, 10, 10) };
            root.Children.Add(child);
            root.Attach(dispatcher);
            using CaptureManager manager = new(root);

            manager.Dispatch(CreatePointer(new Point(2, 2), PointerAction.Move))
                .ShouldBeSameAs(child);

            manager.Hovered.ShouldBeNull();
            child.IsHovered.ShouldBeFalse();
        }, TestContext.Current.CancellationToken);
    }
```

- [ ] **Step 2: Write the second failing test — hover resolves to the nearest interactive ancestor**

In the same class, add:

```csharp
    /// <summary>Verifies hover resolves to the nearest interactive ancestor of the hit control.</summary>
    [Fact]
    public async Task Dispatch_WhenPointerHitsChildOfInteractiveAncestor_HoversAncestorAsync()
    {
        await using Dispatcher dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            ProbeContainer root = new() { Bounds = new Rect(0, 0, 20, 10) };
            ProbeContainer ancestor = new() { Bounds = new Rect(0, 0, 12, 8), CanFocus = true };
            ProbeControl child = new() { Bounds = new Rect(2, 2, 6, 4) };
            ancestor.Children.Add(child);
            root.Children.Add(ancestor);
            root.Attach(dispatcher);
            using CaptureManager manager = new(root);

            manager.Dispatch(CreatePointer(new Point(4, 3), PointerAction.Move))
                .ShouldBeSameAs(child);

            manager.Hovered.ShouldBeSameAs(ancestor);
            ancestor.IsHovered.ShouldBeTrue();
            child.IsHovered.ShouldBeFalse();
        }, TestContext.Current.CancellationToken);
    }
```

- [ ] **Step 3: Run the two new tests and verify they FAIL**

Run:
```bash
dotnet test tests/SharpVision.Tests/SharpVision.Tests.csproj --configuration Release \
  --filter "FullyQualifiedName~Dispatch_WhenPointerHitsNonInteractiveControl|FullyQualifiedName~Dispatch_WhenPointerHitsChildOfInteractiveAncestor"
```
Expected: both FAIL. With current code, hover falls back to the physical leaf, so `Dispatch_WhenPointerHitsNonInteractiveControl` sees `Hovered == child` (not null) and `Dispatch_WhenPointerHitsChildOfInteractiveAncestor` sees `Hovered == child` (not `ancestor`).

- [ ] **Step 4: Change `Control.OwnsHover` to follow `CanFocus`, and refine the `IsHovered` doc**

In `src/SharpVision/Controls/Control.cs`, replace the `OwnsHover` member and its doc:

Old:
```csharp
    /// <summary>Gets whether this control owns hover resolved from its hit-tested descendants.</summary>
    /// <remarks>
    /// The default preserves direct leaf hover. Composite interactive controls
    /// override this to expose one semantic hover state for their visible content.
    /// </remarks>
    internal virtual bool OwnsHover => false;
```
New:
```csharp
    /// <summary>Gets whether this control is an interactive hover target.</summary>
    /// <remarks>
    /// Hover feedback is reserved for interactive controls, so the default follows
    /// <see cref="CanFocus"/>. Hover over a non-interactive descendant resolves up to
    /// the nearest owner, which is how a composite interactive control claims one
    /// semantic hover state for its visible content.
    /// </remarks>
    internal virtual bool OwnsHover => CanFocus;
```

Then update the `IsHovered` summary. Old:
```csharp
    /// <summary>Gets whether pointer targeting currently hovers this control.</summary>
    public bool IsHovered { get; private set; }
```
New:
```csharp
    /// <summary>Gets whether the pointer currently hovers this control; only interactive (focusable) controls are marked hovered.</summary>
    public bool IsHovered { get; private set; }
```

- [ ] **Step 5: Change `ResolveHover` to return null when nothing is interactive**

In `src/SharpVision/Input/CaptureManager.cs`, replace the `ResolveHover` method:

Old:
```csharp
    private static Control? ResolveHover(Control? physical)
    {
        for (Control? current = physical; current is not null; current = current.Parent)
        {
            if (current.OwnsHover)
            {
                return current;
            }
        }

        return physical;
    }
```
New:
```csharp
    private static Control? ResolveHover(Control? physical)
    {
        for (Control? current = physical; current is not null; current = current.Parent)
        {
            if (current.OwnsHover)
            {
                return current;
            }
        }

        // No interactive ancestor owns hover, so nothing is highlighted.
        return null;
    }
```

- [ ] **Step 6: Remove the now-redundant `Pressable.OwnsHover` override**

In `src/SharpVision/Controls/Pressable.cs`, delete the override so all controls follow the single `CanFocus` rule.

Old:
```csharp
    /// <inheritdoc/>
    internal override bool OwnsHover => true;

    /// <summary>Completes one validated activation in a concrete control.</summary>
```
New:
```csharp
    /// <summary>Completes one validated activation in a concrete control.</summary>
```

- [ ] **Step 7: Update the two existing PointerTests to use interactive probes**

The two existing tests hover a non-focusable `ProbeControl` and assert hover; under the new rule they must opt the probe into interactivity.

7a. In `Dispatch_WhenPointerHitsChild_ProvidesLocalCoordinatesAsync`, add `CanFocus = true`:

Old:
```csharp
            ProbeControl child = new() { Bounds = new Rect(4, 3, 8, 4) };
```
New:
```csharp
            ProbeControl child = new() { Bounds = new Rect(4, 3, 8, 4), CanFocus = true };
```

7b. In `Dispatch_WhenPointerMovesPressesAndLeaves_UpdatesVisualStatesAsync`, add `CanFocus = true`. Match this unique block (the `Rect(0, 0, 10, 10)` literal appears in other tests, so use the surrounding lines to disambiguate):

Old:
```csharp
            ProbeContainer root = new() { Bounds = new Rect(0, 0, 20, 10) };
            ProbeControl child = new() { Bounds = new Rect(0, 0, 10, 10) };
            root.Children.Add(child);
            root.Attach(dispatcher);
            using CaptureManager manager = new(root);

            _ = manager.Dispatch(CreatePointer(new Point(2, 2), PointerAction.Move));
```
New:
```csharp
            ProbeContainer root = new() { Bounds = new Rect(0, 0, 20, 10) };
            ProbeControl child = new() { Bounds = new Rect(0, 0, 10, 10), CanFocus = true };
            root.Children.Add(child);
            root.Attach(dispatcher);
            using CaptureManager manager = new(root);

            _ = manager.Dispatch(CreatePointer(new Point(2, 2), PointerAction.Move));
```

- [ ] **Step 8: Run the full PointerTests file and verify PASS**

Run:
```bash
dotnet test tests/SharpVision.Tests/SharpVision.Tests.csproj --configuration Release \
  --filter "FullyQualifiedName~SharpVision.Tests.Input.PointerTests"
```
Expected: PASS — the two new tests now pass, the two updated tests still pass, and the `ProbePressable`-based capture test (`Dispatch_WhenCaptureIsActive_HoversPhysicalTargetAndRoutesToCaptureAsync`) is unaffected because `Pressable` is focusable.

- [ ] **Step 9: Update the `PointerDevice.Hovered` XML doc**

In `src/SharpVision/Runtime/PointerDevice.cs`:

Old:
```csharp
    /// <summary>Gets the current hover target, or null.</summary>
    public Control? Hovered => _capture()?.Hovered;
```
New:
```csharp
    /// <summary>Gets the current interactive hover target, or null when the pointer is over non-interactive content.</summary>
    public Control? Hovered => _capture()?.Hovered;
```

- [ ] **Step 10: Update the prose docs**

10a. In `docs/controls/control.md`, update the interaction-state table row.

Old:
```markdown
| `IsFocused`, `IsHovered`, `IsPressed`      | `false`        | Read-only committed interaction state; composite hover belongs to its semantic owner. |
```
New:
```markdown
| `IsFocused`, `IsHovered`, `IsPressed`      | `false`        | Read-only committed interaction state; only interactive (focusable) controls are hovered, and composite hover belongs to its semantic owner. |
```

10b. In `docs/controls/control.md`, extend the `GetVisualState()` paragraph.

Old:
```markdown
`GetVisualState()` derives normal, hovered, focused, pressed, and disabled flags
from behavior. Controls with semantic selection override it to add checked
state. `GetResolvedStyle` converts the active theme cascade into the complete
terminal cell style used by rendering.
```
New:
```markdown
`GetVisualState()` derives normal, hovered, focused, pressed, and disabled flags
from behavior. Controls with semantic selection override it to add checked
state. Only interactive (focusable) controls are ever marked hovered, so the
hovered flag never appears on static content such as text or tables.
`GetResolvedStyle` converts the active theme cascade into the complete terminal
cell style used by rendering.
```

10c. In `docs/concepts/styling.md`, add a sentence to the visual-states section.

Old:
```markdown
Standard states are normal, hovered, pressed, focused, checked, and disabled.
Measure-impact properties are normal-state values only. Render-impact properties
may vary by overlay state. Visual overlays never control behavior: `IsEnabled`
determines input acceptance.
```
New:
```markdown
Standard states are normal, hovered, pressed, focused, checked, and disabled.
The hovered overlay applies only to interactive (focusable) controls; static
content such as text and tables is never marked hovered.
Measure-impact properties are normal-state values only. Render-impact properties
may vary by overlay state. Visual overlays never control behavior: `IsEnabled`
determines input acceptance.
```

- [ ] **Step 11: Format**

Run:
```bash
make format
```
Expected: completes with "✅ Formatting complete." and no manual fixups needed.

- [ ] **Step 12: Build and run the full test suite**

Run:
```bash
make build && make test
```
Expected: build succeeds; all tests pass (includes `ButtonTests`, `RenderingTests`, `StateModelTests`, `ControlStyleTests`, and `SharpVision.Showcase.Tests` — all use focusable controls or call `SetHovered` directly, so they are unaffected).

- [ ] **Step 13: Lint**

Run:
```bash
make lint
```
Expected: "✅ All lint checks passed." (format verify, C# type lint, markdown lint, doc link check, doc tests all green).

- [ ] **Step 14: Commit**

```bash
git add src/SharpVision/Controls/Control.cs \
        src/SharpVision/Input/CaptureManager.cs \
        src/SharpVision/Controls/Pressable.cs \
        src/SharpVision/Runtime/PointerDevice.cs \
        tests/SharpVision.Tests/Input/PointerTests.cs \
        docs/controls/control.md \
        docs/concepts/styling.md
git commit -m "fix(input): scope hover highlight to interactive controls

Hover resolves to the nearest interactive (CanFocus) control or null, so
static content such as Text, Table, and Grid no longer highlights on hover.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 2 (optional): Manual confirmation in the showcase

Not required for correctness (Task 1's automated tests cover the behavior), but useful because the bug was first observed visually.

**Steps:**

- [ ] Run `make run` to launch the showcase.
- [ ] Switch to the `White` theme.
- [ ] Hover a plain text label and a table's grid cells — confirm no foreground highlight.
- [ ] Hover a button, a text input (edit), a list row, and a scrollbar — confirm the hover foreground still appears.
- [ ] Exit the showcase.

---

## Self-Review

**Spec coverage:**
- Change 1 (`OwnsHover => CanFocus`) → Step 4. ✓
- Change 2 (`ResolveHover` returns null) → Step 5. ✓
- Change 3 (remove `Pressable` override) → Step 6. ✓
- "Deliberately unchanged" (`GetVisualState`, theme, routing) → no steps touch them; Step 8/12 verify unaffected suites. ✓
- Public API semantic change (`PointerDevice.Hovered`, `IsHovered` docs) → Steps 9 and 4. ✓
- Test updates (2 existing) → Step 7; additions (2) → Steps 1–2. ✓
- Docs (control.md, styling.md, PointerDevice prose) → Steps 9–10. ✓
- Accepted `List`-chrome edge case → no action required by design; not a task. ✓

**Placeholder scan:** No TBD/TODO/"handle edge cases"/vague steps. Every code step shows exact old/new text. ✓

**Type consistency:** `OwnsHover` (internal virtual bool), `CanFocus` (public bool), `ResolveHover(Control?) : Control?`, `CaptureManager.Hovered : Control?`, `Control.IsHovered : bool`, `CreatePointer(Point, PointerAction)` test helper, and probe helper names (`ProbeContainer`, `ProbeControl`, `ProbePressable`) match the codebase as read. ✓
