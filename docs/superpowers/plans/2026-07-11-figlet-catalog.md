# FIGlet Engine and Font Catalog Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use
> superpowers:subagent-driven-development (recommended) or
> superpowers:executing-plans to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship a bounded FIGfont renderer, audited compressed 400-font catalog,
FIGlet display control, RichText descriptions, and showcase page.

**Architecture:** The SharpVision library parses and renders fonts through
immutable public values. One deterministic embedded ZIP and manifest supply lazy
named catalog access without filesystem extraction.

**Tech Stack:** .NET 10, C# 14, System.IO.Compression, System.Text.Json, xUnit
v3, Shouldly, FIGfont 2

---

## Task 1: Bounded FIGfont parser and renderer

**Files:**

- Create: `src/SharpVision/Fonts/FigletLimits.cs`
- Create: `src/SharpVision/Fonts/FigletLayout.cs`
- Create: `src/SharpVision/Fonts/FigletDirection.cs`
- Create: `src/SharpVision/Fonts/FigletOptions.cs`
- Create: `src/SharpVision/Fonts/FigletGlyph.cs`
- Create: `src/SharpVision/Fonts/FigletFont.cs`
- Create: `src/SharpVision/Fonts/FigletParser.cs`
- Create: `src/SharpVision/Fonts/FigletRenderer.cs`
- Test: `tests/SharpVision.Tests/Fonts/FigletParserTests.cs`
- Test: `tests/SharpVision.Tests/Fonts/FigletRendererTests.cs`

- [ ] Write failing tests for minimal and code-tagged fonts, layout modes,
      hardblanks, RTL, missing glyphs, malformed input, and every resource
      limit.
- [ ] Run focused tests and confirm failures arise from absent font APIs.
- [ ] Implement validated immutable models, bounded parsing, and row composition
      with FIGfont fitting and smushing rules.
- [ ] Compare exact representative output with official FIGlet and rerun tests.
- [ ] Commit the verified engine slice.

## Task 2: Audit and reproducible archive

**Files:**

- Create: `scripts/audit-figlet-fonts.mjs`
- Create: `scripts/package-figlet-fonts.mjs`
- Create: `scripts/audit-figlet-fonts.test.mjs`
- Create: `data/figlet/fonts.manifest.json`
- Create: `data/figlet/README.md`
- Create: `src/SharpVision/Fonts/figlet-fonts.zip`
- Modify: `package.json`

- [ ] Add failing script tests for missing entries, hash drift, duplicate names,
      absent license classification, unsafe paths, and unstable archive
      metadata.
- [ ] Implement audit and deterministic packaging scripts.
- [ ] Record provenance, SHA-256, notices, classification, and attribution for
      every one of the 400 source fonts.
- [ ] Build twice and assert byte-identical archive hashes.
- [ ] Commit the verified archive, manifest, scripts, and audit evidence.

## Task 3: Embedded catalog API

**Files:**

- Create: `src/SharpVision/Fonts/FigletFontInfo.cs`
- Create: `src/SharpVision/Fonts/FigletCatalog.cs`
- Modify: `src/SharpVision/SharpVision.csproj`
- Test: `tests/SharpVision.Tests/Fonts/FigletCatalogTests.cs`

- [ ] Write failing tests for 400 sorted names, exact metadata, parsing every
      entry, case-sensitive lookup, unsafe names, hash agreement, and
      concurrency.
- [ ] Confirm failures are caused by absent catalog APIs.
- [ ] Embed the ZIP and manifest and implement indexed lazy entry loading.
- [ ] Run all catalog and allocation tests, then commit the verified slice.

## Task 4: FIGletText and RichText controls

**Files:**

- Create: `src/SharpVision/Controls/FigletText.cs`
- Create: `src/SharpVision/Controls/RichText.cs`
- Create: `src/SharpVision/Controls/Inline.cs`
- Create: `src/SharpVision/Controls/Inlines.cs`
- Create: `src/SharpVision/Controls/Run.cs`
- Create: `src/SharpVision/Controls/LineBreak.cs`
- Create: `src/SharpVision/Controls/Hyperlink.cs`
- Test: `tests/SharpVision.Tests/Controls/FigletTextTests.cs`
- Test: `tests/SharpVision.Tests/Controls/RichTextTests.cs`

- [ ] Write failing ownership, validation, layout, style-run, link, clipping,
      Unicode, and resize tests for both controls.
- [ ] Implement traditional mutable controls with cached semantic output.
- [ ] Run focused exact-frame tests.
- [ ] Update the RichText and FigletText API docs and commit the verified slice.

## Task 5: Showcase and full verification

**Files:**

- Modify: `src/SharpVision.Showcase/`
- Modify: `tests/SharpVision.Showcase.Tests/`
- Modify: `docs/architecture/showcase.md`

- [ ] Add failing navigation and screen tests for FIGlet and RichText pages.
- [ ] Implement sidebar registration, font selection, audit metadata, typed
      descriptions, and representative output.
- [ ] Run showcase tests, all 400 catalog parses, and reference comparisons.
- [ ] Run `make format`, `make lint`, `make build`, and `make test`.
- [ ] Commit only after all gates report zero warnings and failures.
