# Theming v2: Semantic Colors Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use
> superpowers:subagent-driven-development (recommended) or
> superpowers:executing-plans to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make semantic theme colors first-class `Color` values
(`ThemeColors.Accent`) that resolve late through the one style cascade, retiring
`Control.TryGetThemeColor` and `RoleSwatch`.

**Architecture:** `Color` gains a deferred `ColorKind.Role` (opaque id)
mirroring `ColorKind.Default`. `ThemeColors` (Styling) exposes one `Color` per
`ColorRole`. Role colors flow through the existing style cascade and collapse to
concrete colors at a single point — `Control.ResolveProperty` (and the
design-time `ThemeResolver` overload) — using the active theme's palette.
`CellStyle` construction rejects unresolved role colors as a fail-fast guard.
The base control style becomes a fixed property→role mapping; a theme differs
only by its palette.

**Tech Stack:** .NET 10 / C# 14, xUnit v3 + Shouldly. No new dependencies.

Spec: `docs/superpowers/specs/2026-07-14-theming-v2-semantic-colors-design.md`.

## Global Constraints

- .NET 10 / C# 14. File-scoped namespaces; `using` after the namespace; `var`
  for locals where the type is apparent (repo `.editorconfig` enforces style;
  `TreatWarningsAsErrors` is on — expect analyzer-forced deviations from any
  literal snippet below, e.g. throw-ternaries for `IDE0046`, `<summary>` on test
  methods for the doc analyzer; these are acceptable when behavior/signatures
  match).
- One named type per file. No primary constructors; explicit constructors
  validating arguments first. XML docs on every public/internal type and member;
  document every thrown exception.
- 12 `ColorRole`s: Foreground, Background, Surface, Border, Accent, Muted,
  SelectionBackground, SelectionForeground, Error, Warning, Success, Info.
- Zero build warnings. Tests: xUnit v3, Shouldly, Arrange/Act/Assert,
  `MethodName_WhenThis_ThatIsExpected`, TDD (watch it fail first).

**Shared-branch discipline (critical):** work happens on
`codex/runtime-protocol-router`, shared with a concurrent session that actively
edits files under `src/SharpVision/Controls/` (esp. `Container.cs`,
`Control.cs`). For EVERY task:

- Stage ONLY the task's exact files; commit with an explicit **pathspec**
  (`git commit -- <files>`), never `git add -A`/`.`, `git restore`, `git stash`,
  `git checkout`, `git reset`.
- `Control.ThemeValues.cs` (Tasks 4, 6) is a `Control` partial in `Controls/`.
  Before editing, `git status` it; if the concurrent session has it
  dirty/changed, coordinate rather than clobber. Do not touch any other
  `Controls/` file.
- If a build/test fails on a file you didn't touch (concurrent WIP), verify in a
  disposable detached `git worktree` at committed HEAD with your files copied in
  — never stash/reset the shared tree.

**Setup:** record the base commit before each task
(`git rev-parse --short HEAD`); review each task by its own commit
(`SHA^..SHA`).

---

### Task 1: `Color.Role` / `ColorKind.Role` / `Color.RoleId`

**Files:**

- Modify: `src/SharpVision.Terminal/Protocols/ColorKind.cs`
- Modify: `src/SharpVision.Terminal/Protocols/Color.cs`
- Test: `tests/SharpVision.Terminal.Tests/Protocols/ColorRoleKindTests.cs`
  (create)

**Interfaces:**

- Produces: `ColorKind.Role`; `Color.Role(int id) : Color`;
  `Color.RoleId : int`.

- [ ] **Step 1: Write the failing test**

```csharp
// tests/SharpVision.Terminal.Tests/Protocols/ColorRoleKindTests.cs
namespace SharpVision.Terminal.Tests.Protocols;

using SharpVision.Terminal.Protocols;

using Shouldly;

/// <summary>Verifies the deferred role color kind.</summary>
public sealed class ColorRoleKindTests
{
    [Fact]
    public void Role_StoresIdAndReportsRoleKind()
    {
        Color color = Color.Role(5);

        color.Kind.ShouldBe(ColorKind.Role);
        color.RoleId.ShouldBe(5);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(255)]
    public void Role_WhenIdInRange_RoundTrips(int id) => Color.Role(id).RoleId.ShouldBe(id);

    [Theory]
    [InlineData(-1)]
    [InlineData(256)]
    public void Role_WhenIdOutOfRange_Throws(int id) =>
        Should.Throw<ArgumentOutOfRangeException>(() => Color.Role(id));

    [Fact]
    public void RoleId_WhenNotRoleKind_Throws() =>
        Should.Throw<InvalidOperationException>(() => Color.Rgb(1, 2, 3).RoleId);

    [Fact]
    public void Role_EqualityById()
    {
        Color.Role(4).ShouldBe(Color.Role(4));
        Color.Role(4).ShouldNotBe(Color.Role(5));
        Color.Role(4).ShouldNotBe(Color.Indexed(4));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:
`dotnet test --project tests/SharpVision.Terminal.Tests --filter-class "*ColorRoleKindTests" --timeout 60s`
Expected: FAIL — `ColorKind.Role`/`Color.Role`/`RoleId` do not exist.

- [ ] **Step 3: Implement**

Add `Role` to `ColorKind` (after `Rgb`):

```csharp
    /// <summary>A theme-resolved semantic color slot; must be resolved to a concrete color before encoding.</summary>
    Role,
```

Add to `Color.cs` (after `Rgb`):

```csharp
    /// <summary>Creates a deferred semantic color resolved by the active theme before rendering.</summary>
    /// <param name="id">The opaque role id from 0 through 255.</param>
    /// <returns>A role color carrying <paramref name="id"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> is outside 0 through 255.</exception>
    /// <remarks>
    /// The terminal layer does not interpret the id; a higher layer (the styling theme) maps it to a
    /// concrete color during property resolution. A role color must never reach the SGR encoder.
    /// </remarks>
    public static Color Role(int id)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(id);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(id, byte.MaxValue);
        return new Color(ColorKind.Role, (byte) id, 0, 0);
    }

    /// <summary>Gets the opaque role id when this is a role color.</summary>
    /// <exception cref="InvalidOperationException">This color is not a role color.</exception>
    public int RoleId => Kind == ColorKind.Role
        ? Red
        : throw new InvalidOperationException("RoleId is only defined for a role color.");
```

- [ ] **Step 4: Run test — PASS.**
      `dotnet test --project tests/SharpVision.Terminal.Tests --filter-class "*ColorRoleKindTests" --timeout 60s`

- [ ] **Step 5: Commit**

```bash
git add src/SharpVision.Terminal/Protocols/ColorKind.cs src/SharpVision.Terminal/Protocols/Color.cs tests/SharpVision.Terminal.Tests/Protocols/ColorRoleKindTests.cs
git commit -- src/SharpVision.Terminal/Protocols/ColorKind.cs src/SharpVision.Terminal/Protocols/Color.cs tests/SharpVision.Terminal.Tests/Protocols/ColorRoleKindTests.cs -m "feat(terminal): add deferred role color kind to Color"
```

---

### Task 2: `CellStyle` rejects unresolved role colors (encode guard)

**Files:**

- Modify: `src/SharpVision.Terminal/Rendering/CellStyle.cs`
- Test: `tests/SharpVision.Terminal.Tests/Rendering/CellStyleRoleGuardTests.cs`
  (create — confirm the `Rendering` test folder path; match the sibling test
  layout)

**Interfaces:**

- Consumes: `ColorKind.Role` (Task 1).
- Produces: `CellStyle` constructor throws `ArgumentException` when any color
  argument has `Kind == Role`.

- [ ] **Step 1: Write the failing test**

```csharp
// tests/SharpVision.Terminal.Tests/Rendering/CellStyleRoleGuardTests.cs
namespace SharpVision.Terminal.Tests.Rendering;

using SharpVision.Terminal.Protocols;

using Shouldly;

using CellStyle = SharpVision.Terminal.Rendering.CellStyle;

/// <summary>Verifies a role color cannot enter a renderable cell style.</summary>
public sealed class CellStyleRoleGuardTests
{
    [Fact]
    public void Constructor_WhenForegroundIsRole_Throws() =>
        Should.Throw<ArgumentException>(() => new CellStyle(foreground: Color.Role(1)));

    [Fact]
    public void Constructor_WhenBackgroundIsRole_Throws() =>
        Should.Throw<ArgumentException>(() => new CellStyle(background: Color.Role(1)));

    [Fact]
    public void Constructor_WhenConcreteColors_Succeeds() =>
        Should.NotThrow(() => new CellStyle(foreground: Color.Rgb(1, 2, 3), background: Color.Indexed(4)));
}
```

- [ ] **Step 2: Run — FAIL** (currently a role color is accepted).
      `dotnet test --project tests/SharpVision.Terminal.Tests --filter-class "*CellStyleRoleGuardTests" --timeout 60s`

- [ ] **Step 3: Implement** — in the `CellStyle` constructor validation block
      add (before assigning state):

```csharp
        if (foreground.Kind == ColorKind.Role || background.Kind == ColorKind.Role || underlineColor.Kind == ColorKind.Role)
        {
            throw new ArgumentException(
                "A role color must be resolved against a theme before it can be rendered.");
        }
```

- [ ] **Step 4: Run — PASS.**

- [ ] **Step 5: Commit**

```bash
git commit -- src/SharpVision.Terminal/Rendering/CellStyle.cs tests/SharpVision.Terminal.Tests/Rendering/CellStyleRoleGuardTests.cs -m "feat(terminal): reject unresolved role colors in CellStyle"
```

---

### Task 3: `ThemeColors` semantic-color accessor

**Files:**

- Create: `src/SharpVision/Styling/ThemeColors.cs`
- Test: `tests/SharpVision.Tests/Styling/ThemeColorsTests.cs`

**Interfaces:**

- Consumes: `Color.Role` (Task 1), `ColorRole`.
- Produces: `public static class ThemeColors` with 12 `Color` properties
  (`Foreground`, `Background`, `Surface`, `Border`, `Accent`, `Muted`,
  `SelectionBackground`, `SelectionForeground`, `Error`, `Warning`, `Success`,
  `Info`), each `Color.Role((int)ColorRole.X)`.

- [ ] **Step 1: Failing test**

```csharp
// tests/SharpVision.Tests/Styling/ThemeColorsTests.cs
namespace SharpVision.Tests.Styling;

using SharpVision.Styling;
using SharpVision.Terminal.Protocols;

using Shouldly;

/// <summary>Verifies the semantic-color accessor maps to color roles.</summary>
public sealed class ThemeColorsTests
{
    [Fact]
    public void Accent_IsRoleColorForAccent()
    {
        ThemeColors.Accent.Kind.ShouldBe(ColorKind.Role);
        ThemeColors.Accent.RoleId.ShouldBe((int) ColorRole.Accent);
    }

    [Fact]
    public void EveryRole_HasAMatchingAccessor()
    {
        // Each ThemeColors property is a role color whose id round-trips to a ColorRole.
        (Color color, ColorRole role)[] map =
        [
            (ThemeColors.Foreground, ColorRole.Foreground),
            (ThemeColors.Background, ColorRole.Background),
            (ThemeColors.Surface, ColorRole.Surface),
            (ThemeColors.Border, ColorRole.Border),
            (ThemeColors.Accent, ColorRole.Accent),
            (ThemeColors.Muted, ColorRole.Muted),
            (ThemeColors.SelectionBackground, ColorRole.SelectionBackground),
            (ThemeColors.SelectionForeground, ColorRole.SelectionForeground),
            (ThemeColors.Error, ColorRole.Error),
            (ThemeColors.Warning, ColorRole.Warning),
            (ThemeColors.Success, ColorRole.Success),
            (ThemeColors.Info, ColorRole.Info),
        ];

        foreach ((Color color, ColorRole role) in map)
        {
            color.RoleId.ShouldBe((int) role);
        }
    }
}
```

- [ ] **Step 2: Run — FAIL** (`ThemeColors` missing).

- [ ] **Step 3: Implement**

```csharp
// src/SharpVision/Styling/ThemeColors.cs
namespace SharpVision.Styling;

using SharpVision.Terminal.Protocols;

/// <summary>Semantic theme colors as first-class <see cref="Color"/> values resolved by the active theme.</summary>
/// <remarks>
/// Each property is a deferred role color; assign it to any color style property (for example
/// <c>Background = ThemeColors.Accent</c>) and it resolves to the active theme's palette value during
/// property resolution, tracking theme swaps automatically.
/// </remarks>
public static class ThemeColors
{
    /// <summary>The default text color.</summary>
    public static Color Foreground { get; } = Color.Role((int) ColorRole.Foreground);

    /// <summary>The default surface color behind content.</summary>
    public static Color Background { get; } = Color.Role((int) ColorRole.Background);

    /// <summary>A raised or inset surface color.</summary>
    public static Color Surface { get; } = Color.Role((int) ColorRole.Surface);

    /// <summary>The default border and separator color.</summary>
    public static Color Border { get; } = Color.Role((int) ColorRole.Border);

    /// <summary>The primary emphasis color.</summary>
    public static Color Accent { get; } = Color.Role((int) ColorRole.Accent);

    /// <summary>A low-emphasis foreground for secondary content.</summary>
    public static Color Muted { get; } = Color.Role((int) ColorRole.Muted);

    /// <summary>The background color of a selected item.</summary>
    public static Color SelectionBackground { get; } = Color.Role((int) ColorRole.SelectionBackground);

    /// <summary>The text color of a selected item.</summary>
    public static Color SelectionForeground { get; } = Color.Role((int) ColorRole.SelectionForeground);

    /// <summary>The color signaling an error state.</summary>
    public static Color Error { get; } = Color.Role((int) ColorRole.Error);

    /// <summary>The color signaling a caution state.</summary>
    public static Color Warning { get; } = Color.Role((int) ColorRole.Warning);

    /// <summary>The color signaling a successful state.</summary>
    public static Color Success { get; } = Color.Role((int) ColorRole.Success);

    /// <summary>The color signaling neutral informational emphasis.</summary>
    public static Color Info { get; } = Color.Role((int) ColorRole.Info);
}
```

- [ ] **Step 4: Run — PASS.**

- [ ] **Step 5: Commit**

```bash
git commit -- src/SharpVision/Styling/ThemeColors.cs tests/SharpVision.Tests/Styling/ThemeColorsTests.cs -m "feat(styling): add ThemeColors semantic-color accessor"
```

---

### Task 4: Central late resolution of role colors

**Files:**

- Create: `src/SharpVision/Styling/SemanticColor.cs` (internal resolution
  helper)
- Modify: `src/SharpVision/Controls/Control.ThemeValues.cs` (`ResolveProperty`)
- Modify: `src/SharpVision/Styling/ThemeResolver.cs` (design-time overload)
- Test: `tests/SharpVision.Tests/Styling/SemanticColorResolutionTests.cs`

**Interfaces:**

- Consumes: `Color.Role`/`RoleId`/`ColorKind.Role`, `ColorRole`,
  `ThemeContext.TryGetColor`, `Theme.TryGetColor`.
- Produces: `internal static class SemanticColor` with
  `static Color Resolve(Color color, Func<ColorRole, Color?> lookup)`;
  `Control.ResolveProperty` and `ThemeResolver.Resolve(Theme,...)` collapse role
  colors to concrete.

- [ ] **Step 1: Failing test**

```csharp
// tests/SharpVision.Tests/Styling/SemanticColorResolutionTests.cs
namespace SharpVision.Tests.Styling;

using SharpVision.Controls;
using SharpVision.Styling;
using SharpVision.Terminal.Protocols;

using Shouldly;

/// <summary>Verifies role colors resolve to the active theme's palette during property resolution.</summary>
public sealed class SemanticColorResolutionTests
{
    private static Theme ThemeWith(Color foreground, Color accent)
    {
        Theme theme = new();
        ControlStyle<Control> style = new();
        style.Set(Control.ForegroundProperty, State.Normal, ThemeColors.Foreground);
        theme.SetStyle(style);
        theme.SetColor(ColorRole.Foreground, foreground);
        theme.SetColor(ColorRole.Accent, accent);
        theme.Freeze();
        return theme;
    }

    [Fact]
    public void GetValue_WhenPropertyIsRoleColor_ResolvesToPaletteColor()
    {
        ProbeControl control = new();
        ThemeTestSupport.ApplyTheme(control, ThemeWith(Color.Indexed(15), Color.Rgb(10, 20, 30)));

        control.GetValue(Control.ForegroundProperty).ShouldBe(Color.Indexed(15));
    }

    [Fact]
    public void DesignTimeResolve_WhenRoleColor_ResolvesAgainstTheme()
    {
        Theme theme = ThemeWith(Color.Indexed(7), Color.Indexed(4));

        ThemeResolver.Resolve(theme, typeof(Control), Control.ForegroundProperty, State.Normal)
            .ShouldBe(Color.Indexed(7));
    }

    [Fact]
    public void LocalRoleColor_ResolvesAndTracksThemeSwap()
    {
        ProbeControl control = new();
        ThemeTestSupport.ApplyTheme(control, ThemeWith(Color.Indexed(1), Color.Indexed(2)));
        control.SetValue(Control.BackgroundProperty, ThemeColors.Accent);

        control.GetValue(Control.BackgroundProperty).ShouldBe(Color.Indexed(2));

        ThemeTestSupport.ApplyTheme(control, ThemeWith(Color.Indexed(1), Color.Indexed(9)));
        control.GetValue(Control.BackgroundProperty).ShouldBe(Color.Indexed(9));
    }
}
```

> **Implementer note:** confirm `ProbeControl` and `ThemeTestSupport.ApplyTheme`
> exist in `tests/SharpVision.Tests/Support` (used by `StandardThemeTests`).
> Reuse them. If `ApplyTheme` publishes a fresh `ThemeContext` per call, the
> swap assertion validates cache invalidation.

- [ ] **Step 2: Run — FAIL** (role color returned as-is, `GetValue` yields
      `Color.Role(...)` not the palette color).

- [ ] **Step 3: Implement**

`SemanticColor.cs`:

```csharp
// src/SharpVision/Styling/SemanticColor.cs
namespace SharpVision.Styling;

using SharpVision.Terminal.Protocols;

/// <summary>Collapses a deferred role color to a concrete color against a theme palette.</summary>
internal static class SemanticColor
{
    /// <summary>Resolves a role color to its concrete palette value; passes other colors through.</summary>
    /// <param name="color">The candidate color.</param>
    /// <param name="lookup">The palette lookup for a role, returning null when undefined.</param>
    /// <returns>The concrete color; <see cref="Color.Default"/> when a role is undefined by the theme.</returns>
    public static Color Resolve(Color color, Func<ColorRole, Color?> lookup)
    {
        if (color.Kind != ColorKind.Role)
        {
            return color;
        }

        // The loader guarantees every role is defined; Default is a safe last resort if not.
        return lookup((ColorRole) color.RoleId) ?? Color.Default;
    }
}
```

In `Control.ThemeValues.cs`, change `ResolveProperty<T>` to collapse role colors
using the control's `ThemeContext`:

```csharp
    internal T ResolveProperty<T>(StyleProperty<T> property, State visualState)
    {
        EnsureThemeProperty(property);
        (IStyleProperty Property, State State) key = (Property: property, State: visualState);

        if (_resolvedPropertyCache.TryGetValue(key, out object? cached))
        {
            return (T) cached!;
        }

        T? value = ThemeResolver.Resolve(this, property, visualState);

        // A role color collapses to the active theme's concrete color at this single point, so every
        // consumer (appearance, chrome, public getters) sees a concrete color.
        if (value is Color { Kind: ColorKind.Role } role)
        {
            ThemeContext? context = ThemeContext;
            Color concrete = SemanticColor.Resolve(
                role,
                r => context is not null && context.TryGetColor(r, out Color c) ? c : null);
            value = (T) (object) concrete;
        }

        _resolvedPropertyCache[key] = value;
        return value;
    }
```

In `ThemeResolver.cs`, the design-time overload
`Resolve<T>(Theme theme, Type controlType, StyleProperty<T> property, State visualState)`
collapses at its return:

```csharp
        // (after computing `value` from the chain, before returning)
        if (value is Color { Kind: ColorKind.Role } role)
        {
            value = (T) (object) SemanticColor.Resolve(
                role,
                r => theme.TryGetColor(r, out Color c) ? c : null);
        }

        return value;
```

Add `using SharpVision.Terminal.Protocols;` where needed. Note
`value is Color { Kind: ColorKind.Role }` matches both `Color` and a non-null
`Color?`; `(T)(object)concrete` round-trips for both (unboxing a boxed `Color`
to `Color?` is valid).

- [ ] **Step 4: Run the new test AND the full styling suite** (regression:
      `StandardThemeTests`/`ColorRoleTests` must still pass, because they read
      through `ResolveProperty`):

Run:
`dotnet test --project tests/SharpVision.Tests --filter-class "*SemanticColorResolutionTests" --timeout 60s`
Run:
`dotnet test --project tests/SharpVision.Tests --filter-class "*Styling*" --timeout 120s`
Expected: PASS. If `StandardThemeTests` fails, inspect whether
`ThemeTestSupport.Resolve` bypasses `ResolveProperty`; route it through the
collapsing path.

- [ ] **Step 5: Commit** (pathspec: `SemanticColor.cs`,
      `Control.ThemeValues.cs`, `ThemeResolver.cs`, the new test)

```bash
git commit -- src/SharpVision/Styling/SemanticColor.cs src/SharpVision/Controls/Control.ThemeValues.cs src/SharpVision/Styling/ThemeResolver.cs tests/SharpVision.Tests/Styling/SemanticColorResolutionTests.cs -m "feat(styling): resolve role colors to palette during property resolution"
```

---

### Task 5: Recipe uses role colors; base style becomes theme-independent

**Files:**

- Modify: `src/SharpVision/Styling/ThemeBuilder.cs`
- Test: `tests/SharpVision.Tests/Styling/ThemeBuilderTests.cs` (update)

**Interfaces:**

- Consumes: `ThemeColors` (Task 3), resolution (Task 4).
- Produces: `ThemeBuilder.Build` base `ControlStyle<Control>` holds
  `ThemeColors.*` values; palette (`SetColor`) unchanged.

- [ ] **Step 1: Update the test** — replace the base-style assertions in
      `ThemeBuilderTests` to expect role colors in the style AND concrete
      resolution through the theme:

```csharp
    [Fact]
    public void Build_BaseStyleStoresRoleColors()
    {
        Dictionary<ColorRole, Color> roles = Roles();
        Theme theme = ThemeBuilder.Build(roles);

        // The base style now carries the semantic (role) value, not a pre-resolved concrete.
        ControlStyle<Control> style = theme.GetStyle<Control>()!;
        style.TryGet(Control.ForegroundProperty, State.Normal, out Color? fg).ShouldBeTrue();
        fg!.Value.Kind.ShouldBe(ColorKind.Role);
        fg.Value.RoleId.ShouldBe((int) ColorRole.Foreground);
    }

    [Fact]
    public void Build_ResolvesRoleColorsToSeededPalette()
    {
        Dictionary<ColorRole, Color> roles = Roles();
        Theme theme = ThemeBuilder.Build(roles);

        ThemeResolver.Resolve(theme, typeof(Control), Control.ForegroundProperty, State.Normal)
            .ShouldBe(roles[ColorRole.Foreground]);
        ThemeResolver.Resolve(theme, typeof(Control), Control.BackgroundProperty, State.Selected)
            .ShouldBe(roles[ColorRole.SelectionBackground]);
        ThemeResolver.Resolve(theme, typeof(Control), Control.ForegroundProperty, State.Disabled)
            .ShouldBe(roles[ColorRole.Muted]);
    }
```

(Keep the existing `Build_ProducesFrozenThemeWithRoles` test — the palette still
carries every role.)

- [ ] **Step 2: Run — the new `Build_BaseStyleStoresRoleColors` FAILS** (base
      style currently stores resolved concretes).

- [ ] **Step 3: Implement** — in `ThemeBuilder.BuildBaseStyle`, replace the
      resolved-color locals with `ThemeColors.*` and drop the `roles` parameter
      usage for the style (the palette loop in `Build` still uses `roles`):

```csharp
    private static ControlStyle<Control> BuildBaseStyle()
    {
        ControlStyle<Control> style = new();

        style.Set(Control.ForegroundProperty, State.Normal, ThemeColors.Foreground);
        style.Set(Control.BackgroundProperty, State.Normal, ThemeColors.Background);
        style.Set(Control.BorderColorProperty, State.Normal, ThemeColors.Border);
        style.Set(Control.ForegroundProperty, State.Hovered, ThemeColors.Accent);
        style.Set(Control.AttributesProperty, State.Focused, TerminalAttributes.Underline);
        style.Set(Control.ForegroundProperty, State.Checked, ThemeColors.SelectionForeground);
        style.Set(Control.BackgroundProperty, State.Checked, ThemeColors.SelectionBackground);
        style.Set(Control.ForegroundProperty, State.Selected, ThemeColors.SelectionForeground);
        style.Set(Control.BackgroundProperty, State.Selected, ThemeColors.SelectionBackground);
        style.Set(Control.ForegroundProperty, State.Disabled, ThemeColors.Muted);
        style.Set(Control.ShadowForegroundProperty, State.Normal, ThemeColors.Border);

        return style;
    }
```

Update `Build` to call `BuildBaseStyle()` (no argument). Keep the
`foreach (ColorRole role in ...) theme.SetColor(role, roles[role]);` palette
loop and `theme.Freeze()`. Note in an XML remark that the base style is now
theme-independent.

- [ ] **Step 4: Run** — `ThemeBuilderTests` and the full styling suite (incl.
      `StandardThemeTests` asserting `Themes.Dark` foreground resolves to
      `Indexed(15)`):

Run:
`dotnet test --project tests/SharpVision.Tests --filter-class "*Styling*" --timeout 120s`
Expected: PASS (palette values unchanged → identical concrete appearance).

- [ ] **Step 5: Commit**

```bash
git commit -- src/SharpVision/Styling/ThemeBuilder.cs tests/SharpVision.Tests/Styling/ThemeBuilderTests.cs -m "refactor(styling): base theme style references role colors, not concretes"
```

---

### Task 6: Retire `TryGetThemeColor` and `RoleSwatch`

**Files:**

- Modify: `src/SharpVision/Controls/Control.ThemeValues.cs` (remove
  `TryGetThemeColor`)
- Delete: `src/SharpVision.Showcase/Controls/RoleSwatch.cs`
- Modify: `src/SharpVision.Showcase/Panes/ThemingPane.cs`
- Modify/replace test:
  `tests/SharpVision.Showcase.Tests/RoleSwatchLiveThemeTests.cs`
- Modify: `docs/architecture/showcase.md` (only if wording references
  `RoleSwatch`/mechanism; the "active theme, live" claim stays true)

**Interfaces:**

- Consumes: `ThemeColors`, resolution (Task 4).
- Produces: Theming page swatches are plain fill controls with
  `Background = ThemeColors.<role>`.

- [ ] **Step 1: Remove `TryGetThemeColor`** from `Control.ThemeValues.cs` (the
      `protected bool TryGetThemeColor(ColorRole, out Color)` method and its
      doc). Confirm no remaining references (`git grep -n TryGetThemeColor` →
      only the deletion site + `RoleSwatch` which is also being deleted).

- [ ] **Step 2: Rework the Theming pane swatches.** In `ThemingPane.Build()`,
      replace `new RoleSwatch(role)` with a small fixed-size fill control whose
      background is the role color — a `Border` (or the project's minimal fill
      control) with `Background = <role color>`, `Width = Length.Cells(6)`,
      `Height = Length.Cells(1)`, and an opaque fill. Map each `ColorRole` to
      its `ThemeColors` value (a `switch` or a `ColorRole`→`Color` helper). The
      description already says the swatches track the active theme; keep it.
      Example per row:

```csharp
foreach (ColorRole role in Enum.GetValues<ColorRole>())
{
    Border chip = new()
    {
        Width = Length.Cells(6),
        Height = Length.Cells(1),
        FillMode = FillMode.Opaque,
        Background = ThemeColorFor(role), // local helper returning the matching ThemeColors.* value
    };
    swatches.Children.Add(Doc.Row(chip, new Text(role.ToString())));
}
```

Add a private
`static Color ThemeColorFor(ColorRole role) => role switch { ColorRole.Foreground => ThemeColors.Foreground, ... };`
(all 12). Because `Background` is a role color resolved on render (Task 4), the
chip tracks theme swaps with no custom control.

> **Implementer note:** confirm `Border` (or whichever container) paints its
> `Background` across its area with `FillMode.Opaque`. If a bare `Border` with
> no child does not fill, use the same fill approach the deleted `RoleSwatch`
> used, but driven by the `Background` property rather than an `OnRender`
> `TryGetThemeColor` call. The control must NOT read the theme itself — it only
> sets `Background = <role color>` and lets resolution handle it.

- [ ] **Step 3: Delete `RoleSwatch.cs`** (`git rm`).

- [ ] **Step 4: Replace the live-theme test.** Rewrite
      `RoleSwatchLiveThemeTests` (rename to e.g. `ThemeSwatchLiveThemeTests`) to
      assert the Theming page's swatch cell shows the active theme's role color
      and updates after
      `application.Theme = ThemeCatalog.Default.Load("dracula")` — same harness,
      now targeting the plain chip control instead of `RoleSwatch`. Assert the
      rendered cell background equals `Themes.Dark`'s role color initially and
      Dracula's after the swap, with a distinctness guard.

- [ ] **Step 5: Run** —
      `dotnet test --project tests/SharpVision.Showcase.Tests --filter-class "*ThemeSwatchLiveThemeTests" --timeout 120s`
      and `--filter-class "*ThemeGalleryTests"`. Build the showcase clean.

- [ ] **Step 6: Commit** (pathspec incl. the `git rm`'d file, the pane, the
      renamed test, and Control.ThemeValues.cs)

```bash
git commit -- src/SharpVision/Controls/Control.ThemeValues.cs src/SharpVision.Showcase/Controls/RoleSwatch.cs src/SharpVision.Showcase/Panes/ThemingPane.cs tests/SharpVision.Showcase.Tests/RoleSwatchLiveThemeTests.cs tests/SharpVision.Showcase.Tests/ThemeSwatchLiveThemeTests.cs -m "refactor: retire TryGetThemeColor and RoleSwatch for role-color swatches"
```

(Adjust the pathspec to the actual renamed test file(s); a `git rm` + new file
both appear.)

---

### Task 7: Documentation

**Files:**

- Modify: `docs/concepts/themes.md`
- Modify: the `ColorRole` reference (`docs/concepts/theming-new-controls.md`)

**Interfaces:** none.

- [ ] **Step 1: Update `docs/concepts/themes.md`.** In the roles/recipe
      sections, document that semantic colors are first-class `Color` values via
      `ThemeColors.*` (e.g. `Background = ThemeColors.Accent`), resolved late
      against the active theme's palette through the style cascade; that the
      base control style is a fixed property→role mapping (theme-independent)
      and a theme differs only by its palette; and that custom controls use
      `ThemeColors.*` directly (no `TryGetThemeColor`). Keep the JSON format /
      fallback / catalog / `ThemeFile` sections accurate (unchanged). No
      placeholders.

- [ ] **Step 2: Update the `ColorRole` reference** (`theming-new-controls.md`)
      to point custom-control authors at `ThemeColors.*` values instead of a
      query API.

- [ ] **Step 3: Validate** markdown lint on the touched files (scoped); confirm
      links resolve.

- [ ] **Step 4: Commit**
      (`git commit -- docs/concepts/themes.md docs/concepts/theming-new-controls.md -m "docs(styling): document semantic ThemeColors and late resolution"`)

---

### Final verification

- [ ] Build the three projects clean (0 warnings):
      `dotnet build src/SharpVision.Showcase/SharpVision.Showcase.csproj`.
- [ ] Run the styling + terminal + showcase theme suites; all green:
      `*Styling*`, `*ColorRoleKindTests`, `*CellStyleRoleGuardTests`,
      `*ThemeGalleryTests`, `*ThemeSwatchLiveThemeTests`.
- [ ] `git grep -n "TryGetThemeColor\|RoleSwatch"` returns nothing.
- [ ] Confirm no `Controls/` files other than `Control.ThemeValues.cs` were
      touched (concurrent session's territory).
- [ ] Full `make format/lint/build/test` gate — run once the concurrent
      session's tree settles; note if deferred.

## Self-Review notes (author)

- **Spec coverage:** §1 Color.Role→T1; encode guard→T2; §2 ThemeColors→T3; §3
  central resolution→T4; §4 recipe→T5; §5 retire TryGetThemeColor/RoleSwatch→T6;
  §6 API surface→T3/T6; docs→T7. All covered.
- **Type consistency:** `Color.Role`/`RoleId`/`ColorKind.Role`, `ThemeColors.*`,
  `SemanticColor.Resolve`, `Control.ResolveProperty`,
  `ThemeResolver.Resolve(Theme,…)` used identically across tasks.
- **Regression safety:** resolution lives inside `ResolveProperty`, so
  `StandardThemeTests`/`ColorRoleTests` and all render paths get concrete colors
  unchanged (Tasks 4/5 explicitly re-run `*Styling*`). The `CellStyle` guard
  (T2) is defense-in-depth; it should never trip in normal flow because
  resolution precedes composition.
- **Flagged for implementers:** verify
  `ProbeControl`/`ThemeTestSupport.ApplyTheme` (T4); confirm the `Rendering`
  test folder path (T2); confirm a `Border` fills its `Background` with no child
  (T6). Shared-branch: `Control.ThemeValues.cs` is in the concurrent session's
  `Controls/` dir — check before editing.
