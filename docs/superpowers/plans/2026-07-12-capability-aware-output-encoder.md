# Capability-aware output encoder implementation plan

> Status: approved by the parent terminal-protocol expansion design and ready
> for test-driven execution in dependency order.

## Objective

Make the terminal output path consume the immutable capability snapshot it is
already given, project semantic styles to the highest representation the target
can safely display, and emit deterministic bytes for every supported color and
rendition tier.

This plan completes the remaining P0 capability-aware encoder boundary and the
style/color portion of vertical slice 3 in the terminal protocol expansion
design. It does not implement cursor modes, tab stops, terminal reports,
multiplexer passthrough, or graphics; those remain separate slices after this
one.

## Governing sources and explicit policy

- ECMA-48 5th edition, section 8.3.117, defines SGR 0 through 29 and the
  standard reset group semantics:
  <https://www.ecma-international.org/wp-content/uploads/ECMA-48_5th_edition_june_1991.pdf>
- Current xterm control sequences define the interoperable indexed/direct color
  forms, aixterm bright-color SGR values, underline variants, underline color,
  and OSC behavior: <https://invisible-island.net/xterm/ctlseqs/ctlseqs.html>
- xterm documents that the first sixteen palette entries are configurable and
  that no universal terminal color palette exists. SharpVision therefore uses a
  documented reference palette only for deterministic degradation; it does not
  claim exact physical RGB output on a 16-color terminal:
  <https://invisible-island.net/xterm/xterm.faq.html>

Output policy:

1. Semantic cells retain their requested colors and attributes. Capability
   projection affects only emitted terminal presentation.
2. `ColorDepth.TrueColor` preserves RGB and indexed colors.
3. `ColorDepth.Indexed256` preserves indices and maps RGB to the nearest entry
   in the documented xterm-compatible 256-color reference palette.
4. `ColorDepth.Basic16` maps RGB and indices above 15 to the nearest reference
   ANSI/aixterm color and emits classic 30-37/40-47/90-97/100-107 SGR, never
   `38;5` or `48;5`.
5. `ColorDepth.Monochrome` emits no foreground or background color selection.
6. Squared sRGB distance selects the nearest palette entry; equal distances
   select the lower index. The algorithm is allocation-free, culture-free, and
   bounded by 256 candidates.
7. Style transitions compare projected styles, not semantic styles. Two
   different RGB requests that collapse to the same indexed color emit no
   redundant reset or color transition.
8. Unsupported optional rendition extensions are omitted at projection time.
   Standard ECMA-48 rendition flags remain available unless a later verified
   capability explicitly narrows them.
9. Capability changes force the renderer's existing full-redraw path, so every
   cell is reprojected under one immutable profile. An in-flight frame never
   changes profile.

## Production and consumer map

| Area                               | Change                                                                          | Consumer benefit                                                          |
| ---------------------------------- | ------------------------------------------------------------------------------- | ------------------------------------------------------------------------- |
| `Capabilities`                     | Validate `ColorDepth`; later publish optional rendition evidence                | Invalid profiles fail before output; negotiated support has one owner     |
| `Protocols/BasicColor`             | Add typed 16-color names                                                        | Public SGR callers cannot confuse a basic color with an indexed extension |
| `Protocols/Sgr`                    | Add exact basic foreground/background and extended rendition encoders           | Encoder emits only representations allowed by the active tier             |
| `Rendering/Palette`                | Add deterministic color projection and reference palette lookup                 | Every control gets identical degradation without owning palette logic     |
| `Rendering/Encoder`                | Require capabilities, project target styles, and minimize projected transitions | Semantic frames stay rich while bytes become terminal-safe                |
| `Rendering/Renderer`               | Pass its immutable snapshot into every encode                                   | Capability negotiation finally affects actual terminal bytes              |
| `Rendering/Style` and `Attributes` | Add underline variants/color, rapid blink, and overline in a later task         | RichText, focus visuals, and themes gain typed modern styling             |
| `SharpVision` style resolver       | Preserve semantic requests into terminal styles                                 | Controls remain protocol-free and automatically benefit                   |
| Showcase capability/style page     | Display semantic request, active tier, and emitted fallback                     | Users can see why a terminal receives a degraded presentation             |

## Task 1: Pin capability-to-encoder behavior with failing tests

### Files

- Modify `tests/SharpVision.Terminal.Tests/Rendering/EncoderTests.cs`
- Modify `tests/SharpVision.Terminal.Tests/Rendering/RendererTests.cs`
- Add `tests/SharpVision.Terminal.Tests/Rendering/PaletteTests.cs`

### Steps

1. Change every direct `Encoder.Encode` call to provide an immutable capability
   profile. Use `TrueColor` in legacy exact-byte and equivalence tests so those
   tests continue proving the same rich output instead of silently changing
   their oracle.
2. Add exact-byte theories for foreground and background at all four depths: RGB
   preserved at true color, RGB converted to `38;5`/`48;5` at indexed 256, RGB
   converted to classic SGR at basic 16, and color omitted at monochrome.
3. Add indexed input cases for indices 0, 7, 8, 15, 16, 231, 232, and 255. Prove
   0-15 remain stable where supported and upper entries degrade
   deterministically.
4. Add tie-breaking and exact-palette-point tests. Include black, white, primary
   colors, one cube point, one grayscale point, and an RGB value equidistant
   between candidates.
5. Add a projected-transition test where adjacent semantic RGB colors collapse
   to one basic color. Assert that only one SGR color sequence is emitted.
6. Add renderer integration tests proving the same frame emitted under
   `TrueColor`, then `Basic16`, triggers a full redraw and different bytes.
7. Run the new tests before implementation and record failures caused by the
   missing encoder capability parameter and unconditional rich-color output.

## Task 2: Add typed basic-color SGR encoding

### Files

- Add `src/SharpVision.Terminal/Protocols/BasicColor.cs`
- Modify `src/SharpVision.Terminal/Protocols/Sgr.cs`
- Modify `tests/SharpVision.Terminal.Tests/Protocols/SgrTests.cs`
- Modify `docs/protocols/sgr.md`

### Steps

1. Add a public `BasicColor` enum with exactly sixteen validated semantic values
   ordered by palette index: eight normal and eight bright colors.
2. Add `Sgr.Foreground(Writer, BasicColor)` and
   `Sgr.Background(Writer, BasicColor)` overloads.
3. Validate unknown enum values before writing. Encode normal foreground as
   30-37, normal background as 40-47, bright foreground as 90-97, and bright
   background as 100-107.
4. Keep `Color.Indexed` behavior unchanged: it explicitly means the indexed
   extension and continues to encode through 38/48 mode 5.
5. Add exact-byte tests for all sixteen values in both roles and invalid-value
   tests that prove the destination remains unchanged.
6. Update the SGR protocol document with source version, exact forms, and the
   distinction between typed basic colors and indexed extension colors.

## Task 3: Implement deterministic palette projection

### Files

- Add `src/SharpVision.Terminal/Rendering/Palette.cs`
- Add `tests/SharpVision.Terminal.Tests/Rendering/PaletteTests.cs`
- Modify `docs/architecture/rendering-pipeline.md`
- Modify `docs/architecture/capabilities.md`

### Steps

1. Implement allocation-free conversion from a `Color` to an effective `Color`
   for `Monochrome`, `Basic16`, `Indexed256`, and `TrueColor`.
2. Store the reference 16-color palette as packed constants and derive the xterm
   cube and grayscale entries arithmetically. Do not allocate arrays per call
   and do not expose mutable palette storage.
3. Resolve an indexed source to reference RGB only when degradation requires a
   lower tier. Preserve default color at every tier.
4. Use `int` squared-distance arithmetic; the maximum three-channel sum fits
   safely. Iterate candidates in ascending index order and replace the current
   winner only for a strictly smaller distance, proving lower-index tie breaks.
5. Validate the `ColorDepth` enum at the public capability assignment boundary;
   internal projection treats unknown values as an impossible invariant.
6. Add exhaustive tests that every source index maps inside the allowed target
   range and randomized tests that projection is deterministic and idempotent.
7. Add a test proving monochrome always produces `Color.Default` and that
   projecting a target-tier value twice is unchanged.

## Task 4: Thread capabilities through Encoder and Renderer

### Files

- Modify `src/SharpVision.Terminal/Rendering/Encoder.cs`
- Modify `src/SharpVision.Terminal/Rendering/Renderer.cs`
- Modify all direct encoder tests and virtual-screen helpers
- Modify `docs/architecture/rendering-pipeline.md`

### Steps

1. Add a required `Capabilities` parameter to `Encoder.Encode`; validate it
   before inspecting frames or mutating the destination.
2. Project each target cell style before transition comparison. Preserve the
   semantic hyperlink and standard attributes for this task; project foreground
   and background through `Palette`.
3. Track the last emitted effective style, not the last semantic style. Reset
   and final cleanup decisions use that effective state.
4. Emit basic colors through the typed `BasicColor` SGR overloads, indexed and
   RGB colors through the existing typed `Color` path, and nothing for default
   colors.
5. Pass the exact renderer snapshot to `Encoder.Encode`. Keep synchronized
   output wrapping outside the encoder because it is frame-transaction framing,
   not cell-style projection.
6. Update equivalence and randomized rendering tests to run at every color
   depth. The independent virtual terminal must interpret classic, indexed, and
   RGB SGR without using production projection code.
7. Prove unchanged frames still emit zero bytes and the warmed unchanged path
   retains zero allocation.

## Task 5: Complete typed modern rendition semantics

### Files

- Modify `src/SharpVision.Terminal/Rendering/Attributes.cs`
- Modify `src/SharpVision.Terminal/Rendering/Style.cs`
- Modify `src/SharpVision.Terminal/Protocols/Rendition.cs`
- Modify `src/SharpVision.Terminal/Protocols/Sgr.cs`
- Add focused capability types under `src/SharpVision.Terminal/Capabilities/`
- Modify style resolver, RichText, docs, and tests

### Steps

1. Extend semantic attributes with rapid blink and overline while preserving
   existing slow-blink behavior.
2. Add a typed underline variant with none, single, double, curly, dotted, and
   dashed values. Reject conflicting variants before style construction.
3. Add optional underline color to `Style`; default means terminal default and
   reset uses SGR 59.
4. Encode underline variants using `4:x`, underline color using 58/59, rapid
   blink using 6/25, and overline using 53/55, with exact bytes from current
   xterm documentation.
5. Add capability evidence for extended underline, underline color, overline,
   OSC 8 hyperlinks, and any other non-ECMA extension. Only `Supported` evidence
   authorizes active emission; unsupported/unknown values project to the
   documented fallback.
6. Project unsupported underline variants to single underline when ordinary
   underline is safe, omit unsupported underline color/overline/hyperlink, and
   keep source semantic styles unchanged.
7. Update RichText inline APIs and the style resolver without allowing controls
   to emit SGR or OSC bytes directly.

## Task 6: Add report-backed evidence without coupling Encoder to parsing

### Files

- Extend device-attribute, DECRQSS, and XTGETTCAP protocol types and tests
- Extend capability query aggregation and negotiator tests
- Modify protocol coverage documents

### Steps

1. Add bounded typed DECRQSS and XTGETTCAP request/response parsing in their
   dedicated protocol files.
2. Route every reply through the existing incremental router and query tracker.
3. Convert verified replies to immutable capability evidence during negotiation;
   never let Encoder inspect environment variables, terminal names, or raw
   replies.
4. Preserve one shared startup deadline and query-slot bounds. Missing or
   malformed evidence remains unknown rather than optimistic.
5. Add every-fragment-boundary tests, malformed hex/status recovery, oversized
   payload tests, duplicates/late replies, and end-to-end renderer output tests.

## Task 7: Showcase and verification

### Files

- Modify the capability/style showcase page after the concurrent showcase work
  is committed
- Modify representative showcase screen tests
- Update `docs/protocols/coverage-matrix.md`

### Steps

1. Add a user-visible style/color degradation panel showing requested RGB,
   active color tier, effective emitted representation, and optional rendition
   support.
2. Render the same semantic sample at monochrome, basic 16, indexed 256, and
   true color in deterministic screen tests.
3. Add exact transport-byte integration tests from `Application` frame creation
   through `Renderer`, not only direct encoder tests.
4. Run direct and tmux showcase smoke tests. tmux does not require passthrough
   for SGR, but the smoke must prove the outer terminal receives the selected
   tier and cleanup leaves no active style.
5. Change coverage claims only after typed implementation and all required tests
   exist.

## Verification sequence

During each task:

```bash
dotnet test --project tests/SharpVision.Terminal.Tests \
  --filter-class "*SgrTests" "*PaletteTests" "*EncoderTests" "*RendererTests" \
  --timeout 60s
```

Before each commit, verify the staged snapshot independently from the shared
dirty tree. Before declaring the slice complete, run:

```bash
make format
make lint
make build
make test
```

Acceptance requires zero warnings/errors, all exact-byte and randomized tests
green, no Markdown/link failures, no unrelated staged files, and a successful
tmux smoke where available.
