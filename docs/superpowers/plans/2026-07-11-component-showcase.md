# Component Showcase Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use
> superpowers:subagent-driven-development (recommended) or
> superpowers:executing-plans to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship a running terminal-native catalog with one documentation page per
concrete SharpVision control, move every vendored resource into `extern/`, and
prove behavior through focused, integration, resize, and live-pane checks.

**Architecture:** `Gallery` navigates an immutable catalog of `Page` objects.
Each page builds a traditional mutable control tree with a shared RichText page
shell, live examples from `Examples`, and immutable `PropertyDescription`
metadata. Project files embed vendored payloads from `extern/` under stable
logical resource names; a repository validator prevents resource-path drift.

**Tech Stack:** .NET 10, C# 14, SharpVision public controls, xUnit v3, Shouldly,
Microsoft Testing Platform, Node.js validation scripts, Prettier,
markdownlint-cli2, and tmux.

---

## Task 1: Guard and relocate external resources

**Files:**

- Create: `extern/README.md`
- Create: `extern/figlet/NOTICE.md`
- Create: `extern/unicode/README.md`
- Create: `extern/unicode/LICENSE.txt`
- Move: `data/figlet/README.md` to `extern/figlet/README.md`
- Move: `data/figlet/fonts.manifest.json` to `extern/figlet/fonts.manifest.json`
- Move: `src/SharpVision/Fonts/figlet-fonts.zip` to
  `extern/figlet/figlet-fonts.zip`
- Move: `data/unicode/17.0.0/*` to `extern/unicode/17.0.0/`
- Create: `scripts/validate-extern.mjs`
- Create: `scripts/validate-extern.test.mjs`
- Modify: `scripts/generate-unicode-data.mjs`
- Modify: `package.json`
- Modify: `src/SharpVision/SharpVision.csproj`

- [ ] **Step 1: Write the failing external-layout tests**

  Export `validateExtern(root)` from `scripts/validate-extern.mjs`. In
  `scripts/validate-extern.test.mjs`, build temporary repositories and assert
  that validation rejects a tracked `data/` directory, a resource under `src/`,
  a missing adjacent README, and a missing license/notice. Assert that the
  intended `extern/figlet` and `extern/unicode/17.0.0` fixture passes.

- [ ] **Step 2: Verify the tests fail for the missing validator**

  Run: `node --test scripts/validate-extern.test.mjs`

  Expected: FAIL because `scripts/validate-extern.mjs` does not exist.

- [ ] **Step 3: Implement the validator and wire it into lint**

  The validator must walk repository files without following symlinks, reject
  `data/`, reject `.zip`, `.flf`, `.tlf`, and pinned Unicode `.txt` inputs
  outside `extern/`, require `extern/README.md`, and require README plus
  license/notice material for each immediate package directory. Add
  `lint:extern` and invoke it from `make lint` through `package.json`.

- [ ] **Step 4: Move resources and preserve runtime names**

  Update `SharpVision.csproj` to embed:

  ```xml
  <EmbeddedResource Include="../../extern/figlet/figlet-fonts.zip"
                    Link="Fonts/figlet-fonts.zip"
                    LogicalName="SharpVision.Fonts.figlet-fonts.zip" />
  <EmbeddedResource Include="../../extern/figlet/fonts.manifest.json"
                    Link="Fonts/fonts.manifest.json"
                    LogicalName="SharpVision.Fonts.fonts.manifest.json" />
  ```

  Change the Unicode generator source directory to `extern/unicode/17.0.0`.
  Update FIGlet package commands and provenance paths. Preserve the archive hash
  and manifest bytes.

- [ ] **Step 5: Add provenance and license material**

  `extern/README.md` defines the boundary and inventory.
  `extern/figlet/NOTICE.md` records the source repository, pinned commit,
  archive hash, manifest audit counts, and unresolved redistribution status.
  `extern/unicode/README.md` records version 17.0.0, upstream URLs, hashes, and
  generation command. `extern/unicode/LICENSE.txt` contains the official Unicode
  data license text obtained from Unicode.org.

- [ ] **Step 6: Verify resource behavior and repository layout**

  Run:

  ```bash
  node --test scripts/validate-extern.test.mjs
  npm run lint:extern
  npm run check:unicode
  dotnet test --project tests/SharpVision.Tests/SharpVision.Tests.csproj \
    --filter-class "*FigletCatalogTests" --timeout 60s
  ```

  Expected: all checks pass; the catalog still reports and parses 400 fonts.

- [ ] **Step 7: Commit the verified external-resource slice**

  ```bash
  git add extern scripts package.json src/SharpVision/SharpVision.csproj data \
    src/SharpVision/Fonts/figlet-fonts.zip
  git commit -m "chore: organize external resources"
  ```

## Task 2: Introduce the typed page catalog

**Files:**

- Create: `src/SharpVision.Showcase/PropertyDescription.cs`
- Create: `src/SharpVision.Showcase/Page.cs`
- Create: `src/SharpVision.Showcase/Catalog.cs`
- Test: `tests/SharpVision.Showcase.Tests/PropertyDescriptionTests.cs`
- Test: `tests/SharpVision.Showcase.Tests/PageTests.cs`
- Modify: `tests/SharpVision.Showcase.Tests/GalleryTests.cs`

- [ ] **Step 1: Write failing value and catalog tests**

  `PropertyDescriptionTests` requires non-empty `Name`, `Type`, `Default`, and
  `Description` values and verifies exact preservation. `PageTests` requires a
  non-empty name/summary/interaction, at least one property, a non-null example
  factory, fresh control instances from each call, and a built page containing
  typed RichText headings for Overview, Examples, Properties, and Interaction.

  Change the gallery inventory expectation to this exact order:

  ```csharp
  string[] expected =
  [
      "Border", "Button", "Canvas", "CheckBox", "Dock", "FigletText",
      "Grid", "List", "Overlay", "RadioButton", "RichText", "ScrollBar",
      "ScrollView", "Shadow", "Stack", "Text", "TextInput",
  ];
  ```

- [ ] **Step 2: Verify the tests fail for the missing types and inventory**

  Run:

  ```bash
  dotnet test --project tests/SharpVision.Showcase.Tests/SharpVision.Showcase.Tests.csproj \
    --filter-class "*PropertyDescriptionTests|*PageTests|*GalleryTests" --timeout 60s
  ```

  Expected: FAIL because the new types are absent and the gallery still has five
  family pages.

- [ ] **Step 3: Implement validated immutable metadata**

  `PropertyDescription` is a `readonly struct` with explicit validating
  constructor and get-only string properties. `Page` is a sealed reference type
  because it owns a factory delegate and catalog identity. It exposes `Name`,
  `Summary`, `Interaction`, `Properties`, `CreateExamples()`, and
  `CreateContent()`; all public and internal members receive XML docs.

- [ ] **Step 4: Implement the reusable RichText page shell**

  `Page.CreateContent()` returns a vertical `Stack` with padding and spacing. It
  uses RichText for the title/summary, section headings, property name/type/
  default/description rows, and interaction text. Live controls returned by the
  factory appear under Examples. A `Debug.Assert` verifies catalog metadata
  remains non-empty after constructor validation.

- [ ] **Step 5: Create the exact catalog surface**

  `Catalog.Pages` is a stable read-only array containing 17 `Page` instances in
  the expected order. Every entry has at least three control-specific property
  descriptions and delegates live example creation to `Examples` methods
  introduced in Task 3.

- [ ] **Step 6: Verify metadata and page-shell tests pass**

  Run the focused command from Step 2.

  Expected: all selected tests pass with the new inventory.

## Task 3: Build live examples for every concrete control

**Files:**

- Create: `src/SharpVision.Showcase/Examples.cs`
- Modify: `src/SharpVision.Showcase/CanvasSample.cs`
- Modify: `src/SharpVision.Showcase/Gallery.cs`
- Test: `tests/SharpVision.Showcase.Tests/GalleryTests.cs`

- [ ] **Step 1: Write failing completeness and freshness tests**

  For each catalog entry, call `CreateExamples()` twice and assert non-null,
  distinct, detached control trees. Recursively assert every page includes at
  least one concrete example control whose runtime type matches the sidebar
  name. Select every sidebar index and assert `SelectedPage`, content identity,
  RichText presence, property-row count, and sidebar/content ownership.

- [ ] **Step 2: Verify the new completeness tests fail**

  Run the showcase test project filtered to `GalleryTests` and confirm failure
  because per-control examples do not yet exist.

- [ ] **Step 3: Implement representative live variants**

  Add one factory method per control to `Examples`:

  - Border: light, heavy, paired, rounded, ASCII, solid, and shade glyph sets.
  - Button: enabled, disabled, default, cancel, and a click-updated status line.
  - Canvas: fixed and percentage positions plus the Unicode drawing sample.
  - CheckBox: unchecked, checked, indeterminate, and disabled states.
  - Dock: left/top/right/bottom/fill children with spacing.
  - FigletText: Standard font sample with catalog/audit note.
  - Grid: fixed, percent, auto, star, spacing, and span examples.
  - List: single selection, disabled state, enough items to scroll.
  - Overlay: stable z-order with overlapping styled labels.
  - RadioButton: a named group with one selected and one disabled option.
  - RichText: styled runs, line break, Unicode, and semantic hyperlink.
  - ScrollBar: horizontal and vertical values with visible thumbs.
  - ScrollView: content requiring both automatic bars.
  - Shadow: composite and block-glyph modes with multiple offsets.
  - Stack: horizontal/vertical, reverse order, spacing, and sized children.
  - Text: wrapping, trimming, alignment, styling, and wide graphemes.
  - TextInput: editable, read-only, password, maximum length, and multiline.

  Reuse ordinary public controls only. Event handlers may update existing
  example text but must be detached by tree disposal; no background tasks or
  private APIs are allowed.

- [ ] **Step 4: Replace family switching in Gallery**

  `Gallery` accepts the immutable `Catalog.Pages`, binds names to the sidebar,
  and assigns `Page.CreateContent()` to the scrolling main pane. Expose
  `IReadOnlyList<Page> Pages` and `Page Selected` for tests and diagnostics.
  Keep the sidebar at 24 cells and both main scrollbars on Auto.

- [ ] **Step 5: Verify every page is registered and fresh**

  Run:

  ```bash
  dotnet test --project tests/SharpVision.Showcase.Tests/SharpVision.Showcase.Tests.csproj \
    --timeout 60s
  ```

  Expected: all showcase metadata, navigation, and composition tests pass.

- [ ] **Step 6: Commit the catalog and examples**

  ```bash
  git add src/SharpVision.Showcase tests/SharpVision.Showcase.Tests
  git commit -m "feat(showcase): document every control"
  ```

## Task 4: Prove rendered layout, scrolling, resize, and interaction

**Files:**

- Create: `tests/SharpVision.Showcase.Tests/GalleryRenderingTests.cs`
- Create: `tests/SharpVision.Showcase.Tests/GalleryInteractionTests.cs`
- Create: `tests/SharpVision.Showcase.Tests/Screen.cs`
- Modify: `tests/SharpVision.Showcase.Tests/SharpVision.Showcase.Tests.csproj`

- [ ] **Step 1: Write failing semantic rendering tests**

  Lay out and render the gallery into a terminal `Frame` at 30x8, 80x24, and
  140x40. Convert cells to a deterministic `Screen` helper that preserves Rune,
  continuation-cell, and style semantics. Assert bounds containment, selected
  page title visibility at normal/large sizes, and automatic main-pane vertical
  scrollbar visibility for long pages. At 30x8 assert no exception, no invalid
  cell continuation, and preserved sidebar selection.

- [ ] **Step 2: Verify rendering tests fail for family pages or missing
      content**

  Run the test project filtered to `GalleryRenderingTests`; confirm expected
  failure before implementation adjustments.

- [ ] **Step 3: Write failing interaction and resize tests**

  Attach the real gallery to an `Application` using the existing deterministic
  transport and resize fakes. Drive Down/Enter or pointer selection through the
  public input sink, click the Button example, edit the TextInput example,
  scroll the main pane, then publish resize events. Assert selected page,
  example state, offsets, focus, emitted frame completion, and no runtime
  failure.

- [ ] **Step 4: Implement only the showcase corrections exposed by tests**

  Adjust example minimum sizes, page spacing, or shell composition without
  adding test-only paths. Keep every correction within public control behavior.

- [ ] **Step 5: Verify focused render and interaction suites pass**

  Run:

  ```bash
  dotnet test --project tests/SharpVision.Showcase.Tests/SharpVision.Showcase.Tests.csproj \
    --timeout 60s
  ```

  Expected: every catalog, rendering, interaction, resize, and scrolling test
  passes without warnings or runtime failure.

## Task 5: Update normative documentation and capture the running app

**Files:**

- Modify: `docs/architecture/showcase.md`
- Modify: `docs/testing/showcase.md`
- Modify: `docs/index.md`
- Create: `docs/images/showcase-button.png`
- Create: `scripts/capture-showcase.sh`

- [ ] **Step 1: Update the showcase contract**

  Replace family-page language with exact catalog requirements and link inline
  to the page-shell, properties, interaction, resize, and external-resource
  sections of the approved design. Document that screenshots supplement, never
  replace, semantic cell and event assertions.

- [ ] **Step 2: Add a deterministic live capture command**

  `scripts/capture-showcase.sh` validates `tmux` and image-conversion tools,
  starts an isolated 100x32 tmux session running the Release showcase, waits for
  the first rendered component page by polling pane content, captures ANSI or
  text output, converts it to `docs/images/showcase-button.png`, and tears down
  the session in a trap. It exits non-zero when the app terminates, renders no
  page title, or conversion fails.

- [ ] **Step 3: Run the actual showcase and inspect its image**

  Run:

  ```bash
  dotnet build SharpVision.slnx --configuration Release
  scripts/capture-showcase.sh
  ```

  Inspect the PNG at original resolution. Verify sidebar labels, Button title,
  summary, live variants, property documentation, interaction guidance, and
  scroll affordance are legible and not overwritten.

- [ ] **Step 4: Commit documentation and visual proof**

  ```bash
  git add docs scripts/capture-showcase.sh
  git commit -m "docs: capture the component showcase"
  ```

## Task 6: Full verification and completion audit

**Files:**

- Modify only files required by failures found during the audit.

- [ ] **Step 1: Run formatting and repository gates**

  ```bash
  make format
  make lint
  make build
  make test
  ```

  Expected: zero format, C# type, Markdown, link, documentation-test, build, and
  test failures; Release build reports zero warnings and zero errors.

- [ ] **Step 2: Audit every design requirement against evidence**

  Compare `docs/superpowers/specs/2026-07-11-component-showcase-design.md` line
  by line against the current catalog, examples, tests, `extern/` tree, loader
  paths, scripts, documentation, and captured image. Treat indirect or missing
  proof as incomplete and add the required implementation or test.

- [ ] **Step 3: Verify the final repository state**

  Run:

  ```bash
  git diff --check
  git status --short
  find extern -type f | sort
  rg -n "data/(figlet|unicode)|src/SharpVision/Fonts/figlet-fonts.zip" \
    --glob '!.git/**' .
  ```

  Expected: no whitespace errors, only intentional files, a tidy documented
  `extern/` inventory, and no stale active paths (historical plans may describe
  the old layout only as prior state).

- [ ] **Step 4: Commit any audit corrections and report exact evidence**

  Stage only intentional corrections, commit them with a scoped message, and
  report the exact passing commands, discovered test count, build warning/error
  count, image path, and any remaining upstream licensing blocker.
