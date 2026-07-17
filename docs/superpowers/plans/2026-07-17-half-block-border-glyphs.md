# Half-Block Border Glyphs Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use
> superpowers:subagent-driven-development (recommended) or
> superpowers:executing-plans to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a public `Glyphs.HalfBlock` border preset, prove its exact cells
through mounted surfaces, and demonstrate it in the Theming showcase.

**Architecture:** Keep border rasterization unchanged and express the style
entirely through the existing immutable `Glyphs` value. The preset maps upper,
lower, and side half-blocks plus matching corner blocks into the eight physical
border positions, so every control can opt in through `BorderGlyphs`.

**Tech Stack:** .NET 10, C# 14, xUnit v3, Shouldly, SharpVision retained
controls and component-surface harness.

---

## Task 1: Define the public preset contract test-first

**Files:**

- Modify: `tests/SharpVision.Tests/Controls/IntrinsicBorderTests.cs`
- Modify: `src/SharpVision/Controls/Glyphs.cs`

- [ ] **Step 1: Add a failing preset case**

Add `Glyphs.HalfBlock` to `PresetCases` with top-left `▛`, top `▀`, and left
`▌`.

- [ ] **Step 2: Verify the focused test fails**

Run:

```bash
dotnet test --project tests/SharpVision.Tests/SharpVision.Tests.csproj --filter-class "*IntrinsicBorderTests" --timeout 60s
```

Expected: compilation fails because `Glyphs.HalfBlock` does not exist.

- [ ] **Step 3: Implement the immutable preset**

Add a documented static `Glyphs` property with this physical mapping:

```csharp
public static Glyphs HalfBlock { get; } = new(
    new Rune('▛'),
    new Rune('▀'),
    new Rune('▜'),
    new Rune('▐'),
    new Rune('▟'),
    new Rune('▄'),
    new Rune('▙'),
    new Rune('▌'));
```

- [ ] **Step 4: Verify the focused contract test passes**

Run the same focused command and expect all `IntrinsicBorderTests` to pass.

## Task 2: Prove mounted and showcase surfaces

**Files:**

- Create: `tests/SharpVision.Tests/Controls/IntrinsicBorderSurfaceTests.cs`
- Modify: `src/SharpVision.Showcase/Panes/ThemingPane.cs`
- Modify: `tests/SharpVision.Showcase.Tests/ThemeGalleryTests.cs`

- [ ] **Step 1: Add a mounted whole-surface test**

Mount a stretched `Dock` with `BorderThickness = new Thickness(1)` and
`BorderGlyphs = Glyphs.HalfBlock` in a 7-by-3 surface and require:

```text
▛▀▀▀▀▀▜
▌Half ▐
▙▄▄▄▄▄▟
```

- [ ] **Step 2: Add a failing showcase specimen test**

Require the Theming page to contain a 32-by-3 `Dock` using `Glyphs.HalfBlock`,
and require its rendered top and bottom rows to contain the exact corner and
edge glyphs.

- [ ] **Step 3: Add the Theming showcase specimen**

Place the fixed-size half-block surface beside the existing intrinsic chrome
example and show the public assignment `BorderGlyphs = Glyphs.HalfBlock` in the
example source.

- [ ] **Step 4: Run both focused suites**

```bash
dotnet test --project tests/SharpVision.Tests/SharpVision.Tests.csproj --filter-class "*IntrinsicBorder*" --timeout 60s
dotnet test --project tests/SharpVision.Showcase.Tests/SharpVision.Showcase.Tests.csproj --filter-class "*ThemeGalleryTests" --timeout 60s
```

Expected: all focused tests pass.

## Task 3: Align the normative contract and verify the repository

**Files:**

- Modify: `docs/controls/control.md`

- [ ] **Step 1: Document the preset**

Specify that `Glyphs.HalfBlock` maps `▛▀▜`, `▌ ▐`, and `▙▄▟` to the physical
border segments and retains the same one-cell layout reservation as every other
glyph family.

- [ ] **Step 2: Run quality gates**

```bash
make format
make lint
make build
make test
```

Expected: formatting and linting pass, build reports zero warnings and errors,
and all configured tests pass.
