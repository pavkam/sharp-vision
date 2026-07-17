# Theme Glyph Palette Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use
> superpowers:subagent-driven-development (recommended) or
> superpowers:executing-plans to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make every control-selected drawing glyph resolve from the active
schema-v2 theme while preserving explicit local overrides.

**Architecture:** `Theme` owns one complete immutable `ThemeGlyphs` composed
from focused value groups in `SharpVision.Styling`. The strict JSON loader
constructs that typed palette from a required schema-v2 `glyphs` object;
controls resolve local values before theme values and detached controls use
`Themes.Dark.Glyphs`.

**Tech Stack:** .NET 10, C# 14, `System.Text.Json`, xUnit v3, Shouldly,
Microsoft Testing Platform, Markdown/JSON theme resources.

---

## Task 1: Introduce the typed glyph palette and schema-v2 loader

**Files:**

- Create: `src/SharpVision/Styling/ThemedGlyph.cs`
- Create: `src/SharpVision/Styling/ChromeGlyphs.cs`
- Create: `src/SharpVision/Styling/ProgressGlyphs.cs`
- Create: `src/SharpVision/Styling/DisclosureGlyphs.cs`
- Create: `src/SharpVision/Styling/SelectionGlyphs.cs`
- Create: `src/SharpVision/Styling/NavigationGlyphs.cs`
- Create: `src/SharpVision/Styling/ScrollBarGlyphs.cs`
- Create: `src/SharpVision/Styling/SeparatorGlyphs.cs`
- Create: `src/SharpVision/Styling/TextGlyphs.cs`
- Create: `src/SharpVision/Styling/ThemeGlyphs.cs`
- Modify: `src/SharpVision/Styling/Theme.cs`
- Modify: `src/SharpVision/Styling/ThemeDefinition.cs`
- Modify: `src/SharpVision/Styling/ThemeBuilder.cs`
- Modify: `src/SharpVision/Styling/ThemeLoader.cs`
- Create: `tests/SharpVision.Tests/Styling/ThemeGlyphTests.cs`
- Create: `tests/SharpVision.Tests/Styling/ThemeGlyphLoaderTests.cs`
- Modify: `tests/SharpVision.Tests/Styling/ThemeDeserializeTests.cs`
- Modify: `tests/SharpVision.Tests/Styling/ThemeLoaderTests.cs`

- [ ] **Step 1: Write failing value and loader tests**

Add tests proving `ThemedGlyph` rejects control, zero-cell, and wide fallback
values; `ProgressGlyphs` copies exactly nine levels and rejects bad endpoints;
schema 1 is unsupported; schema 2 requires all glyph groups; and one complete
document exposes exact typed values.

```csharp
[Fact]
public void FromJson_WhenSchemaOneIsRead_ThrowsUnsupportedVersion()
{
    var error = Should.Throw<InvalidDataException>(
        () => ThemeLoader.FromJson("""{ "version": 1, "roles": {} }""", "legacy"));

    error.Message.ShouldContain("unsupported schema version 1");
}
```

- [ ] **Step 2: Run focused tests and confirm RED**

```bash
dotnet test --project tests/SharpVision.Tests/SharpVision.Tests.csproj \
  --filter-class "*ThemeGlyph*Tests" --timeout 60s
```

Expected: compile failures because the typed glyph model and schema-v2 surface
do not exist.

- [ ] **Step 3: Implement immutable glyph values and strict parsing**

`ThemedGlyph` owns primary and terminal-safe fallback Runes:

```csharp
public readonly record struct ThemedGlyph
{
    public ThemedGlyph(Rune value, Rune fallback)
    {
        Value = Validate(value, nameof(value));
        Fallback = Validate(fallback, nameof(fallback));
    }

    public Rune Value { get; }
    public Rune Fallback { get; }
}
```

Each group has an explicit constructor and readonly properties. `ProgressGlyphs`
copies its two nine-value inputs and exposes `ReadOnlyMemory<ThemedGlyph>`.
`ThemeGlyphs` requires every group. `Theme` receives a complete `ThemeGlyphs`
and publishes it through `Glyphs`.

Extend `ThemeLoader.ParseDefinition` with a required `glyphs` case and parse
every known member directly into typed values. A scalar has this exact shape:

```json
{ "value": "▶", "fallback": ">" }
```

Reject unknown, duplicate, missing, non-string, multi-scalar, control,
zero-cell, and invalid-width values with the full path. Accept only
`version: 2`.

- [ ] **Step 4: Run focused tests and confirm GREEN**

```bash
dotnet test --project tests/SharpVision.Tests/SharpVision.Tests.csproj \
  --filter-class "*ThemeGlyph*Tests" --timeout 60s
```

Expected: all theme glyph tests pass with zero warnings.

## Task 2: Migrate every shipped theme to the required glyph schema

**Files:**

- Modify: `src/SharpVision/Styling/Themes/*.theme.json`
- Modify: `tests/SharpVision.Tests/Styling/CuratedThemesTests.cs`
- Modify: `tests/SharpVision.Tests/Styling/ThemeCatalogTests.cs`

- [ ] **Step 1: Add failing catalog completeness tests**

```csharp
[Fact]
public void Load_WhenCatalogThemeIsRead_ProvidesCompleteSchemaTwoGlyphs()
{
    foreach (var entry in ThemeCatalog.Default.Entries)
    {
        var theme = ThemeCatalog.Default.Load(entry.Slug);
        theme.SchemaVersion.ShouldBe(2);
        theme.Glyphs.Progress.HorizontalFractions.Length.ShouldBe(9);
        theme.Glyphs.Progress.VerticalFractions.Length.ShouldBe(9);
    }
}
```

- [ ] **Step 2: Run catalog tests and confirm RED**

```bash
dotnet test --project tests/SharpVision.Tests/SharpVision.Tests.csproj \
  --filter-class "*ThemeCatalogTests|*CuratedThemesTests" --timeout 60s
```

Expected: embedded version-1 resources are rejected.

- [ ] **Step 3: Rewrite all embedded resources as schema 2**

Set every file to `"version": 2` and add every grouped glyph member. Dark and
curated themes use Unicode primary values with ASCII fallbacks; `default-light`
uses contrasting ASCII primary values for showcase proof. Do not add optional
members, loader defaults, or compatibility paths.

- [ ] **Step 4: Run catalog tests and confirm GREEN**

```bash
dotnet test --project tests/SharpVision.Tests/SharpVision.Tests.csproj \
  --filter-class "*ThemeCatalogTests|*CuratedThemesTests" --timeout 60s
```

Expected: every catalog theme loads and the contrasting palette differs
semantically.

## Task 3: Theme shared chrome and preserve existing local overrides

**Files:**

- Modify: `src/SharpVision/Controls/Control.ThemeValues.cs`
- Modify: `src/SharpVision/Controls/Control.StyleProperties.cs`
- Modify: `src/SharpVision/Controls/ControlChrome.cs`
- Modify: `src/SharpVision/Controls/Button.cs`
- Modify: `src/SharpVision/Controls/GroupBox.cs`
- Modify: `src/SharpVision/Controls/Popup.cs`
- Modify: `src/SharpVision/Controls/Window.cs`
- Modify: `tests/SharpVision.Tests/Styling/ControlChromeTests.cs`
- Modify: `tests/SharpVision.Tests/Controls/IntrinsicBorderTests.cs`
- Modify: `tests/SharpVision.Tests/Controls/IntrinsicShadowTests.cs`
- Modify: `tests/SharpVision.Tests/Controls/GroupBoxTests.cs`
- Modify: `tests/SharpVision.Tests/Controls/PopupTests.cs`
- Modify: `tests/SharpVision.Tests/Controls/WindowTests.cs`

- [ ] **Step 1: Write failing chrome precedence tests**

Prove a live theme changes border, shadow, repair, and close cells; assigning
the current effective value establishes a local override; and the focused reset
method returns to theme resolution.

- [ ] **Step 2: Run focused chrome tests and confirm RED**

```bash
dotnet test --project tests/SharpVision.Tests/SharpVision.Tests.csproj \
  --filter-class "*ControlChromeTests|*IntrinsicBorderTests|*IntrinsicShadowTests|*GroupBoxTests|*PopupTests|*WindowTests" \
  --timeout 60s
```

- [ ] **Step 3: Implement render-time glyph resolution**

```csharp
internal ThemeGlyphs ResolveThemeGlyphs() => Theme?.Glyphs ?? Themes.Dark.Glyphs;
```

Refactor existing `BorderGlyphs`, `ShadowGlyph`, `Button.Glyphs`,
`GroupBox.Glyphs`, `Popup.Glyphs`, and `Window.Glyphs` to record local
assignment without constructor literals. Add focused reset methods. Change
`ControlChrome` to use theme-owned repair values and window-close glyphs.

- [ ] **Step 4: Run focused chrome tests and confirm GREEN**

```bash
dotnet test --project tests/SharpVision.Tests/SharpVision.Tests.csproj \
  --filter-class "*ControlChromeTests|*IntrinsicBorderTests|*IntrinsicShadowTests|*GroupBoxTests|*PopupTests|*WindowTests" \
  --timeout 60s
```

## Task 4: Theme progress, disclosure, and selection controls

**Files:**

- Modify: `src/SharpVision/Controls/ProgressBar.cs`
- Modify: `src/SharpVision/Controls/ComboBox.cs`
- Modify: `src/SharpVision/Controls/Expander.cs`
- Modify: `src/SharpVision/Controls/CheckBox.cs`
- Modify: `src/SharpVision/Controls/RadioButton.cs`
- Modify: `src/SharpVision/Controls/MenuItem.cs`
- Modify: matching control tests under `tests/SharpVision.Tests/Controls/`

- [ ] **Step 1: Add failing exact-cell tests for every semantic family**

Use a distinctive theme and assert progress empty/full/indeterminate/fractions,
combo drop-down, both expander states, every checkbox style, both radio states,
and check/radio menu states. Prove existing progress and checkbox local values
win until reset.

- [ ] **Step 2: Run focused tests and confirm RED**

```bash
dotnet test --project tests/SharpVision.Tests/SharpVision.Tests.csproj \
  --filter-class "*ProgressBarTests|*ComboBoxTests|*ExpanderTests|*CheckBoxTests|*RadioButtonTests|*MenuTests" \
  --timeout 60s
```

- [ ] **Step 3: Replace literal selection with palette lookups**

Draw Runes with `DrawRune`, index immutable progress sequences, and compose
ASCII brackets/spaces separately from themed marks. Preserve the user's current
`Expander` content-bound edits. Remove implicit `Marks.Default` selection from
control construction; retain it only as an explicit caller preset.

- [ ] **Step 4: Run focused tests and confirm GREEN**

```bash
dotnet test --project tests/SharpVision.Tests/SharpVision.Tests.csproj \
  --filter-class "*ProgressBarTests|*ComboBoxTests|*ExpanderTests|*CheckBoxTests|*RadioButtonTests|*MenuTests" \
  --timeout 60s
```

## Task 5: Theme navigation, scrollbars, separators, tables, tabs, and text

**Files:**

- Modify: `src/SharpVision/Controls/NavigationViewGroup.cs`
- Modify: `src/SharpVision/Controls/NavigationViewItem.cs`
- Modify: `src/SharpVision/Controls/NavigationViewSeparator.cs`
- Modify: `src/SharpVision/Controls/ScrollBar.cs`
- Modify: `src/SharpVision/Controls/Separator.cs`
- Modify: `src/SharpVision/Controls/MenuSeparator.cs`
- Modify: `src/SharpVision/Controls/TablePresenter.cs`
- Modify: `src/SharpVision/Controls/TabControl.cs`
- Modify: `src/SharpVision/Controls/Text.cs`
- Modify: `src/SharpVision/Text/Layout.cs`
- Modify: matching tests under `tests/SharpVision.Tests/`

- [ ] **Step 1: Add failing exact-cell tests for the remaining audit**

Assert navigation markers/separators, every scrollbar orientation and fill,
general/menu/table separators, tab divider/underline, and text ellipsis. Add a
source audit rejecting drawing literals in `src/SharpVision/Controls` except
explicit caller preset files `Glyphs.cs` and `Marks.cs`.

- [ ] **Step 2: Run focused tests and confirm RED**

```bash
dotnet test --project tests/SharpVision.Tests/SharpVision.Tests.csproj \
  --filter-class "*NavigationViewTests|*ScrollBarTests|*SeparatorTests|*TableTests|*TabControlTests|*TextTests|*ControlGlyphSourceTests" \
  --timeout 60s
```

- [ ] **Step 3: Migrate every remaining renderer**

Use themed Rune drawing. Replace table line primitives with bounded loops using
themed table-horizontal/table-vertical values. Remove the duplicate ellipsis
literal from `Text`; layout reserves one cell for the validated theme ellipsis.

- [ ] **Step 4: Run focused tests and confirm GREEN**

```bash
dotnet test --project tests/SharpVision.Tests/SharpVision.Tests.csproj \
  --filter-class "*NavigationViewTests|*ScrollBarTests|*SeparatorTests|*TableTests|*TabControlTests|*TextTests|*ControlGlyphSourceTests" \
  --timeout 60s
```

## Task 6: Align docs, showcase, and external API proof

**Files:**

- Modify: `docs/concepts/themes.md`
- Modify: `docs/concepts/styling.md`
- Modify: affected files under `docs/controls/`
- Modify: current theme showcase under `src/SharpVision.Showcase/`
- Modify: `tests/SharpVision.Showcase.Tests/ThemeGalleryTests.cs`
- Modify: `tests/SharpVision.Showcase.Tests/GalleryRenderingTests.cs`
- Modify: `tests/SharpVision.Consumer.Tests/ExternalContractTests.cs`

- [ ] **Step 1: Add failing showcase and consumer tests**

Prove a live Dark-to-White switch changes representative progress, drop-down,
disclosure, border, and navigation cells without rebuilding the tree. Compile
and run every new public glyph type from packed packages.

- [ ] **Step 2: Run focused tests and confirm RED**

```bash
dotnet test --project tests/SharpVision.Showcase.Tests/SharpVision.Showcase.Tests.csproj --timeout 60s
dotnet test --project tests/SharpVision.Consumer.Tests/SharpVision.Consumer.Tests.csproj --timeout 60s
```

- [ ] **Step 3: Update normative contracts and showcase content**

Document schema 2, required groups, validation, local precedence/reset, detached
Dark resolution, live replacement, and affected control surfaces. Update theme
showcase proof without overwriting concurrent Expander work.

- [ ] **Step 4: Run docs and focused suites**

```bash
npm run format:check
npm run lint:markdown
npm run lint:links
npm run test:docs
dotnet test --project tests/SharpVision.Showcase.Tests/SharpVision.Showcase.Tests.csproj --timeout 60s
dotnet test --project tests/SharpVision.Consumer.Tests/SharpVision.Consumer.Tests.csproj --timeout 60s
```

## Task 7: Verify the complete repository

**Files:**

- Modify only files required by formatter or failures attributable to this
  feature.

- [ ] **Step 1: Audit remaining literals and the worktree**

```bash
rg -n --pcre2 '[^\x00-\x7F]' src/SharpVision/Controls --glob '*.cs'
git status --short
git diff --check
```

Expected: only explicit public preset definitions and non-glyph prose remain;
unrelated user changes remain present and unstaged.

- [ ] **Step 2: Run repository gates**

```bash
make format
make lint
make build
make test
```

Expected: zero formatting or Markdown failures, zero warnings, zero build
errors, and all configured test minimums satisfied.

- [ ] **Step 3: Review the final scoped diff**

```bash
git diff --stat 6042c46..HEAD
git log --oneline 6042c46..HEAD
```

Confirm every design completion criterion has implementation, test, docs, or
showcase evidence and no unrelated user file was staged by feature commits.
