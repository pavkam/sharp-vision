# Canvas, Borders, and Shadows Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use
> superpowers:subagent-driven-development (recommended) or
> superpowers:executing-plans to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add topology-aware Unicode drawing, border presets, and composite and
block-glyph shadow controls.

**Architecture:** Drawing and grapheme-preserving style mutation live in the
terminal Canvas. UI controls consume those primitives through immutable values
and a capacity-one Shadow decorator with explicit visual overflow.

**Tech Stack:** .NET 10, C# 14, xUnit v3, Shouldly, Unicode 17 semantic cells

---

## Task 1: Canvas Rune, fill, and style primitives

**Files:**

- Modify: `src/SharpVision.Terminal/Rendering/Canvas.cs`
- Modify: `src/SharpVision.Terminal/Rendering/Frame.cs`
- Test: `tests/SharpVision.Terminal.Tests/Rendering/CanvasPrimitiveTests.cs`

- [ ] Write failing tests named `DrawRune_WhenRuneIsWide_ThrowsBeforeMutation`,
      `Fill_WhenRegionIsClipped_WritesOnlyIntersection`, and
      `ApplyStyle_WhenRegionTouchesWideGlyph_StylesCompleteOwner`.
- [ ] Run the focused class and confirm failures report missing Canvas members.
- [ ] Add validated `DrawRune`, `Fill`, and `ApplyStyle` methods and an internal
      Frame operation that updates a complete owner's style without rewriting
      text.
- [ ] Run the focused class and the existing Canvas tests; require zero
      failures.
- [ ] Commit the verified slice.

## Task 2: Topology-aware lines and boxes

**Files:**

- Create: `src/SharpVision.Terminal/Rendering/LineWeight.cs`
- Create: `src/SharpVision.Terminal/Rendering/LinePattern.cs`
- Create: `src/SharpVision.Terminal/Rendering/LineStyle.cs`
- Create: `src/SharpVision.Terminal/Rendering/Topology.cs`
- Create: `src/SharpVision.Terminal/Rendering/LineResolver.cs`
- Modify: `src/SharpVision.Terminal/Rendering/Canvas.cs`
- Test: `tests/SharpVision.Terminal.Tests/Rendering/LineTests.cs`

- [ ] Write failing tests for light, heavy, double, rounded, dashed, ASCII,
      clipped, tiny, and crossing lines, including reverse draw order.
- [ ] Confirm the focused class fails because line APIs do not exist.
- [ ] Implement immutable validated styles, topology decoding and merging, Rune
      resolution, `DrawHorizontalLine`, `DrawVerticalLine`, and `DrawBox`.
- [ ] Run topology and Canvas tests and verify exact glyphs and commutativity.
- [ ] Commit the verified slice.

## Task 3: Block primitives and borders

**Files:**

- Create: `src/SharpVision.Terminal/Rendering/Shade.cs`
- Create: `src/SharpVision.Terminal/Rendering/Quadrants.cs`
- Create: `src/SharpVision.Terminal/Rendering/BlockResolver.cs`
- Modify: `src/SharpVision.Terminal/Rendering/Canvas.cs`
- Modify: `src/SharpVision/Controls/Glyphs.cs`
- Modify: `src/SharpVision/Controls/Border.cs`
- Test: `tests/SharpVision.Terminal.Tests/Rendering/BlockTests.cs`
- Test: `tests/SharpVision.Tests/Controls/BorderTests.cs`

- [ ] Write failing tests for every shade, all sixteen quadrant masks, every
      border preset, and exact rendered corners.
- [ ] Confirm the focused tests fail on the absent APIs.
- [ ] Implement block resolution, Canvas operations, and light, heavy, double,
      rounded, ASCII, solid, and shade Glyphs presets.
- [ ] Run the focused rendering and Border tests.
- [ ] Update `docs/controls/display/border.md` and commit the verified slice.

## Task 4: Visual overflow and Shadow

**Files:**

- Create: `src/SharpVision/Controls/ShadowMode.cs`
- Create: `src/SharpVision/Controls/Shadow.cs`
- Modify: `src/SharpVision/Controls/Control.cs`
- Test: `tests/SharpVision.Tests/Controls/ShadowTests.cs`
- Create: `docs/controls/display/shadow.md`
- Modify: `docs/controls/index.md`

- [ ] Add failing tests for constructor defaults, validation, child ownership,
      both rendering modes, negative offsets, clipping, z-order, wide glyphs,
      hit-testing, tiny bounds, and resize rearrangement.
- [ ] Confirm focused failures are caused by missing Shadow and overflow APIs.
- [ ] Add `VisualBounds` to Control, preserving descendant clipping, and
      implement the capacity-one Shadow decorator with offset footprint
      rendering.
- [ ] Run focused controls, integration resize, and rendering tests.
- [ ] Commit implementation, tests, and docs together.

## Task 5: Showcase and full verification

**Files:**

- Modify: `src/SharpVision.Showcase/`
- Modify: `tests/SharpVision.Showcase.Tests/`
- Modify: `docs/architecture/showcase.md`

- [ ] Add a failing showcase screen test expecting all border presets, drawing
      primitives, and both shadow modes.
- [ ] Implement the visual page with traditional controls.
- [ ] Run showcase tests and inspect exact semantic screen output.
- [ ] Run `make format`, `make lint`, `make build`, and `make test`.
- [ ] Commit only after all gates report zero warnings and failures.
