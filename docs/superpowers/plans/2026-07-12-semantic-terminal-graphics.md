# Semantic terminal graphics implementation plan

> Status: approved dependency-ordered continuation of the terminal protocol
> expansion. This plan is executable; support claims change only after the
> listed proof passes.

**Goal:** Add a bounded protocol-independent image model, semantic cell-grid
placements, deterministic Sixel output, Kitty graphics upload/placement/delete,
iTerm2 memory-only inline images, a mutable `Image` control, coherent cell
fallback, lifecycle tracking, documentation, showcase coverage, and live smoke
tests.

**Architecture:** Images are immutable owned values in the terminal layer.
Controls record semantic placements and render fallback cells; they never emit
escape bytes or select a terminal backend. Frames own a finite placement table
but only retain immutable image references. The renderer projects placements
through one immutable capability and multiplexer-route snapshot, then owns all
remote upload and placement state. Backend failure cannot corrupt the semantic
cell frame: it invalidates graphics state and leaves the already-rendered cell
fallback authoritative.

**Primary specifications:**

- DEC VT330/VT340 Programmer Reference Manual, Volume 2, Chapter 14, second
  edition (May 1988), for Sixel DCS parameters, raster attributes, RGB color
  registers, repeat, carriage return, and graphics newline.
- xterm control sequences, patch 410 (2026-04-19), for current Sixel DA1 and
  private-mode behavior.
- Kitty terminal graphics protocol, accessed 2026-07-12, for APC grammar, direct
  RGB/RGBA/PNG payloads, 4096-byte base64 chunks, queries, identifiers,
  placements, `C=1`, acknowledgements, deletion, and placeholders.
- iTerm2 Inline Images Protocol, accessed 2026-07-12, for OSC 1337 `File`,
  memory payload, dimensions, aspect policy, ST termination, and multipart
  limits.

## Non-negotiable invariants

1. Every pixel count, byte count, dimension, palette, command, placement,
   upload, pending reply, cache entry, and encoded batch is bounded before
   allocation or observable mutation.
2. Images copy caller memory exactly once at their public ownership boundary. No
   span, pooled array, mutable byte array, or terminal-supplied path escapes.
3. The library never opens a terminal-supplied path and does not implement Kitty
   file/shared-memory media or iTerm2 download mode.
4. A frame stores semantic placement intent, not backend commands or remote
   identifiers.
5. A control always produces a deterministic cell fallback. Unsupported,
   tentative, filtered, failed, or cancelled graphics never leave an empty
   unexplained region.
6. Image placement coordinates are cell rectangles. Pixel source rectangles use
   exact integer half-open geometry. Conversion uses the shared rational
   `Geometry.Metrics`; absent metrics prohibit pixel-exact placement rather than
   inventing a cell size.
7. Cell damage and graphics damage are separate but committed atomically after
   one complete write and flush.
8. Remote graphics state is renderer-owned, finite, and cleared on replacement,
   removal, capability loss, screen transition, invalidation, failure, and
   disposal.
9. Sixel does not claim arbitrary z-order or independent placement lifecycle.
   The renderer redraws it under its documented cursor/reservation policy.
10. Kitty/iTerm2 output is enabled only by `Feature.IsSupported` and an allowed
    direct or multiplexer route. Environment hints remain tentative.
11. One named C# type lives in one exactly named file; all public and internal
    APIs carry complete XML documentation and validation exceptions.
12. Every task follows red-green-refactor and updates protocol docs in the same
    commit as behavior.

## Fixed limits and public policy

Introduce `SharpVision.Terminal.Graphics.Limits` with validated defaults:

- maximum dimension: 16,384 pixels per axis;
- maximum pixels: 67,108,864;
- maximum owned source bytes: 256 MiB;
- maximum PNG metadata scan: 64 KiB;
- maximum placements per frame: 4,096;
- maximum alternate-text UTF-8 bytes: 4,096;
- maximum Sixel palette entries: 256;
- maximum Sixel output: renderer output limit, never a second unbounded limit;
- Kitty direct chunk: 4,096 base64 bytes, divisible by four except final;
- maximum active uploads and placements: 4,096 each;
- maximum pending graphics replies: existing query concurrency limit;
- maximum iTerm2 metadata: 4 KiB;
- maximum iTerm2 part: 1,048,576 bytes including OSC framing, with a
  conservative smaller part selected under tmux.

All limits are injectable immutable policy values for tests and applications.
Changing a limit validates the complete proposal before publication.

## Phase 1: owned image values

### Task 1.1: source formats and ownership

#### Production files

- `src/SharpVision.Terminal/Graphics/Format.cs`
- `src/SharpVision.Terminal/Graphics/Image.cs`
- `src/SharpVision.Terminal/Graphics/Limits.cs`
- `src/SharpVision.Terminal/Graphics/Png.cs`

#### Test files

- `tests/SharpVision.Terminal.Tests/Graphics/ImageTests.cs`
- `tests/SharpVision.Terminal.Tests/Graphics/PngTests.cs`

Implement `Format.Rgba` and `Format.Png`. `Image.FromRgba` accepts positive
pixel dimensions and exactly `width * height * 4` sRGB RGBA bytes.
`Image.FromPng` copies a bounded PNG after validating the eight-byte signature,
first IHDR shape, positive 32-bit dimensions, supported
compression/filter/interlace header values, declared bounds, and presence of
IEND. It does not decode PNG. The value exposes dimensions, format, byte length,
stable process-local identity, and copy methods; it never exposes its owned
array.

Proof:

- every invalid dimension/length/format fails before copying;
- caller mutation after construction cannot change the image;
- copy destinations are validated atomically;
- overflow cases at every multiplication boundary are named tests;
- malformed/truncated/oversized PNG chunks recover without allocation spikes;
- identities are nonzero and stable for the value lifetime.

### Task 1.2: pixel and rectangle access

#### Production files

- `src/SharpVision.Terminal/Graphics/Pixel.cs`
- `src/SharpVision.Terminal/Graphics/PixelRect.cs`

#### Test files

- `tests/SharpVision.Terminal.Tests/Graphics/PixelTests.cs`
- `tests/SharpVision.Terminal.Tests/Graphics/PixelRectTests.cs`

Add an immutable four-byte `Pixel` and half-open `PixelRect`. Ranges validate
against image dimensions. Internal row-copy APIs use spans and checked offsets;
Sixel reads RGBA without creating `Pixel` objects in hot loops.

## Phase 2: deterministic Sixel backend

### Task 2.1: palette quantization

#### Production files

- `src/SharpVision.Terminal/Graphics/Sixel/Options.cs`
- `src/SharpVision.Terminal/Graphics/Sixel/Palette.cs`
- `src/SharpVision.Terminal/Graphics/Sixel/Quantizer.cs`

#### Test files

- `tests/SharpVision.Terminal.Tests/Graphics/Sixel/QuantizerTests.cs`
- `tests/SharpVision.Terminal.Tests/Graphics/Sixel/RandomizedQuantizerTests.cs`

Start with deterministic bounded quantization, not an adaptive science project:

1. alpha below the configured threshold is transparent;
2. exact opaque colors are collected until the palette limit;
3. overflow colors map into a fixed xterm-compatible 6x6x6 RGB cube plus a
   24-step grayscale ramp, truncated by the configured palette limit;
4. squared sRGB distance with lower-register tie break selects the register;
5. palette definitions use DEC RGB percentage components with deterministic
   integer rounding.

The default is private image palette semantics, transparent background, 256
registers, and alpha threshold 128. No dithering ships in this slice because it
would complicate exact damage and reference decoding; it remains a future
explicit option.

Proof:

- exact palettes remain exact;
- transparent pixels consume no register;
- every RGBA value maps deterministically;
- palette count and work arrays never exceed policy;
- fixed-seed randomized images produce stable hashes and bounded allocations.

### Task 2.2: Sixel command encoder

#### Production files

- `src/SharpVision.Terminal/Graphics/Sixel/Encoder.cs`
- `src/SharpVision.Terminal/Graphics/Sixel/Result.cs`
- `src/SharpVision.Terminal/Graphics/Sixel/Run.cs`

#### Test files

- `tests/SharpVision.Terminal.Tests/Graphics/Sixel/EncoderTests.cs`
- `tests/SharpVision.Terminal.Tests/Graphics/Sixel/BoundaryTests.cs`

Encode seven-bit `DCS 0;1;0 q ... ST` for transparent background. Emit raster
attributes `"1;1;<width>;<height>`, DEC RGB register definitions `#n;2;r;g;b`,
and six-row bands. Within a band, emit one color plane at a time, graphics
carriage return `$` between planes, and graphics newline `-` between bands.
Sixel bytes are `? + mask`, least-significant bit at the top. Omit trailing zero
columns and use `!<count><sixel>` only when its exact byte count is smaller than
literal repetition. The encoder writes no cursor or mode controls.

Preflight the conservative maximum and use finite scratch memory. If the exact
result exceeds the caller limit, throw before touching the destination writer.
Cancellation is checked between bands, never inside a partially published DCS.

Proof:

- hand-authored 1x1, 1x6, 2x7, transparent, multicolor, and repeat golden bytes;
- dimensions/repeats at numeric boundaries;
- output limit one byte below/equal/above exact size;
- destination remains unchanged on validation or capacity failure;
- no control bytes appear inside Sixel data except defined commands;
- exact ST cleanup on success.

### Task 2.3: independent Sixel reference decoder

#### Test files

- `tests/SharpVision.Terminal.Tests/Support/SixelScreen.cs`
- `tests/SharpVision.Terminal.Tests/Support/SixelPixel.cs`
- `tests/SharpVision.Terminal.Tests/Graphics/Sixel/EquivalenceTests.cs`

Build a test-only decoder from the DEC grammar, not production helpers. Parse
raster attributes, RGB/HLS-compatible color selection, repeats, `$`, `-`, and
Sixel bytes into a finite reference pixel grid. Compare exact nontransparent
pixels for procedural fixtures and at least 256 fixed-seed randomized RGBA
images after applying the documented quantizer reference policy. Fragment every
representative DCS at every read boundary through the production parser before
feeding the reference model.

### Task 2.4: Sixel capability and output wrapper

#### Production files

- `src/SharpVision.Terminal/Graphics/Sixel/Command.cs`
- updates to `Capabilities/Negotiator.cs`, `Capabilities/Queries.cs`,
  `Protocols/DeviceAttributes.cs`, and routing policy.

#### Test files

- capability, negotiation, routing, exact-byte, and pseudoterminal tests.

DA1 parameter 4 is positive query evidence. Explicit overrides remain final.
Unknown/tentative support cannot emit Sixel. Direct output is preferred; tmux or
screen requires an allowed bounded passthrough route. The wrapper saves cursor,
positions to the placement origin, encodes one Sixel DCS, restores cursor, and
applies documented reserved-cell behavior. A failed write invalidates all Sixel
graphics state because it has no reliable remote placement identity.

## Phase 3: Kitty graphics backend

### Task 3.1: typed APC commands

#### Production files

- `src/SharpVision.Terminal/Graphics/Kitty/Action.cs`
- `src/SharpVision.Terminal/Graphics/Kitty/Format.cs`
- `src/SharpVision.Terminal/Graphics/Kitty/Command.cs`
- `src/SharpVision.Terminal/Graphics/Kitty/Encoder.cs`
- `src/SharpVision.Terminal/Graphics/Kitty/Delete.cs`

#### Test files

- exact-byte and validation tests under
  `tests/SharpVision.Terminal.Tests/Graphics/Kitty/`.

Encode only direct transmission (`t=d`) for RGBA and PNG. For RGBA, include
`f=32,s=<width>,v=<height>`; for PNG use `f=100`. Use nonzero renderer-owned
image/placement identifiers, `q=1` for ordinary commands, `C=1` for stable
cursor behavior, and explicit `c/r` placement cells. Base64 chunks are at most
4096 bytes and all non-final chunks are divisible by four. Only the first chunk
carries full metadata; `m=1` continues and `m=0` completes.

Support transmit, transmit-and-place, place-existing, delete placement, delete
image, and delete all owned graphics. File, temporary-file, shared-memory,
compression, animation, and terminal paths are rejected as unsupported typed
options rather than accepted raw strings.

### Task 3.2: query and reply routing

#### Production files

- `src/SharpVision.Terminal/Graphics/Kitty/Reply.cs`
- `src/SharpVision.Terminal/Graphics/Kitty/ReplyDecoder.cs`
- router and negotiator integration.

Decode bounded APC `G` replies with printable messages, image id, optional image
number, and placement id. Correlate the non-mutating 1x1 RGB query with the DA1
ordering rule from the Kitty specification. `OK` establishes support; an error
establishes protocol support but rejects that command; DA1 arriving first with
no Kitty reply leaves support unverified. Duplicate, late, malformed, and
unsolicited replies are diagnostics and never input text.

Fragment every reply at every byte boundary. Add timeout, duplicate, wrong-id,
oversize, malformed base64, route-wrapper, and end-to-end router tests.

### Task 3.3: retained upload and placement state

#### Production files

- `src/SharpVision.Terminal/Graphics/Kitty/State.cs`
- `src/SharpVision.Terminal/Graphics/Kitty/Upload.cs`
- `src/SharpVision.Terminal/Graphics/Kitty/Placement.cs`

Map semantic image identity to a finite nonzero protocol image id and semantic
placement identity to a finite placement id. Reuse unchanged uploads, issue
place/update for moved rectangles, delete removed placements, and delete image
data only when no retained placement needs it. Capability/route loss deletes
when safe and always clears local state. A failed batch discards local certainty
and forces reupload on the next frame.

Defer Unicode placeholders until direct placement lifecycle is proven. Then add
them as a separately gated route for tmux, using U+10EEEE, official row/column
diacritics, foreground image-id encoding, and underline-color placement-id
encoding. This must reuse the semantic underline-color encoder already shipped.

## Phase 4: iTerm2 memory-only backend

### Task 4.1: OSC 1337 inline encoder

#### Production files

- `src/SharpVision.Terminal/Graphics/Iterm/Dimension.cs`
- `src/SharpVision.Terminal/Graphics/Iterm/Options.cs`
- `src/SharpVision.Terminal/Graphics/Iterm/Encoder.cs`

#### Test files

- `tests/SharpVision.Terminal.Tests/Graphics/Iterm/EncoderTests.cs`
- `tests/SharpVision.Terminal.Tests/Graphics/Iterm/BoundaryTests.cs`

Encode only `inline=1` from owned PNG bytes. Do not send a filename. Include
declared byte size, cell or pixel width/height, and explicit
`preserveAspectRatio`. Use ST, not BEL. Use single OSC `File=` when bounded by
route policy; otherwise use `MultipartFile`, bounded `FilePart`, and `FileEnd`
only where proved supported. Never emit download mode.

Placement is cursor-based and not independently deletable, so any moved/removed
iTerm2 image forces the documented redraw/clear strategy. Exact-byte tests cover
auto/cell/pixel dimensions, aspect modes, empty/maximum parts, metadata
escaping, and tmux part limits.

### Task 4.2: feature reporting and route policy

Add typed iTerm2 feature-report parsing where the terminal advertises inline
images. Environment identity remains tentative. tmux integration and ordinary
tmux passthrough are distinct routes. Unsupported route means fallback cells.

## Phase 5: semantic placements and renderer lifecycle

### Task 5.1: frame placement arena

#### Production files

- `src/SharpVision.Terminal/Graphics/Fit.cs`
- `src/SharpVision.Terminal/Graphics/Lifetime.cs`
- `src/SharpVision.Terminal/Graphics/Placement.cs`
- `src/SharpVision.Terminal/Graphics/PlacementInfo.cs`
- updates to `Rendering/Frame.cs`, `Rendering/Canvas.cs`, and damage logic.

`Canvas.DrawImage` validates the entire source rectangle, destination cell
rectangle, clip intersection, fit mode, alternate text, z-order, and placement
limit before mutation. A placement has stable semantic identity, image, source
pixel rectangle, destination cells, effective clip, fit, z-order, and lifetime.
Frame clone/copy owns an independent placement table while sharing only
immutable image values. Clear/dispose releases placement references.

Placement equality and damage are independent of fallback cells. Identical
placement identity plus image identity and geometry is unchanged. Move/change
damages old and new cell rectangles. A clip cannot create negative or overflowed
geometry.

### Task 5.2: backend selection

#### Production files

- `src/SharpVision.Terminal/Graphics/Backend.cs`
- `src/SharpVision.Terminal/Graphics/Route.cs`
- `src/SharpVision.Terminal/Graphics/Projector.cs`
- updates to capabilities and multiplexer routing.

Selection order is Kitty direct/placeholder, Sixel, iTerm2, fallback, subject to
source format and route. PNG-only images cannot use Sixel without a decoder;
RGBA can use Kitty or Sixel, while iTerm2 requires owned PNG. Applications may
set a preferred backend but cannot force an unproved feature. The selected
backend and reason are observable diagnostics for the showcase.

### Task 5.3: transactional renderer integration

#### Production files

- `src/SharpVision.Terminal/Graphics/State.cs`
- `src/SharpVision.Terminal/Graphics/Encoder.cs`
- updates to `Rendering/Renderer.cs`, `Rendering/Metrics.cs`, and output buffer.

Prepare cell bytes, graphics commands, cache mutations, and front-frame storage
before I/O. Write one bounded batch and flush once. Only then commit both frame
and graphics state. Cancellation or any write/flush error preserves the previous
front frame, marks terminal graphics unknown, clears proposed cache mutations,
and forces full cell plus graphics reconstruction next time. Cleanup errors are
diagnostic and never replace the original exception.

Metrics add uploads, placements, deletes, graphics bytes, fallback placements,
and selected backend without allocating on an unchanged frame.

Proof includes unchanged image no-op, move without reupload on Kitty,
replacement delete/upload, removal, capability loss, resize, alternate screen,
clear, failure at every write boundary, cancellation, disposal, and strict
finite cache eviction.

## Phase 6: mutable Image control

### Task 6.1: public control API

#### Production files

- `src/SharpVision/Controls/Image.cs`
- `src/SharpVision/Controls/ImageFallback.cs`
- `src/SharpVision/Controls/Stretch.cs`

#### Test files

- `tests/SharpVision.Tests/Controls/ImageTests.cs`
- `tests/SharpVision.Tests/Controls/ImageFallbackTests.cs`

The mutable control owns a non-null/nullable source, stretch (`None`, `Uniform`,
`UniformToFill`, `Fill`), alignment, alternate text, source rectangle, and
fallback policy. Source and intrinsic-size changes invalidate measure where
automatic dimensions depend on them; fit, alignment, alternate text, and visual
changes invalidate render only when layout is unchanged. Every setter validates
before dispatcher-affine mutation.

Measurement uses exact image pixels plus exact cell metrics when available.
Without metrics, explicit cell lengths remain authoritative and automatic size
uses a bounded documented fallback based on alternate-text cells. Arrange
computes one clipped destination rectangle. Render first paints the cell
fallback, then records one semantic placement. The control never asks which
backend is active.

Fallback modes:

- alternate text, grapheme-safe and clipped;
- deterministic half-block preview for RGBA when color fidelity permits;
- framed placeholder containing dimensions/backend reason;
- explicit blank only when the caller requests it.

### Task 6.2: integration behavior

Test Image inside Border, Grid, ScrollView, Popup, Window, Table cell, clipped
Overlay, resized roots, and nested scroll regions. Pointer hit testing remains
cell-based. Scrolling changes placement geometry and backend commands without
changing source ownership. Focus and input behavior remain ordinary Control
semantics.

## Phase 7: docs, showcase, and live proof

### Task 7.1: normative documentation

Update:

- `docs/protocols/sixel.md`
- `docs/protocols/kitty-graphics.md`
- `docs/protocols/iterm2.md`
- `docs/protocols/coverage-matrix.md`
- `docs/architecture/rendering-pipeline.md`
- `docs/architecture/capabilities.md`
- new `docs/concepts/images.md`
- new `docs/controls/display/image.md`
- rendering, protocol, pseudoterminal, randomized, and showcase test docs.

Each protocol page records source version/date, exact supported subset, bounds,
fallback, multiplexer behavior, unsupported commands, security policy, and
proof. The coverage matrix moves one row at a time only after that backend's
acceptance tests pass.

### Task 7.2: procedural showcase

Add an Image page built from an in-memory procedural RGBA test card and a tiny
embedded audited PNG generated deterministically by a test/tooling script. Show:

- current backend, support evidence, route, and fallback reason;
- None/Uniform/UniformToFill/Fill;
- source cropping, clipping, scrolling, and resize;
- transparency and palette pressure;
- replacement/removal lifecycle;
- alternate-text and half-block fallback.

Add representative semantic screen tests that remain stable on terminals without
graphics. Backend-specific golden bytes stay in terminal tests rather than
snapshots.

### Task 7.3: PTY and tmux smoke matrix

Extend the smoke harness to run:

1. direct host terminal query only when an interactive terminal exists;
2. tmux direct cell fallback always;
3. tmux passthrough query/output only when version and `allow-passthrough`
   evidence permit;
4. GNU screen fallback and bounded wrapper behavior when installed;
5. Sixel output captured to bytes and decoded by the independent reference;
6. Kitty/iTerm2 probes with finite timeout and no false support claim.

The smoke never writes an image when support is only tentative. It restores
modes and cursor in `finally`, records the installed tmux/screen versions, and
skips environmental absence with an explicit reason.

## Commit and verification sequence

Keep commits dependency-ordered and independently reviewable:

1. `docs(graphics): specify semantic image architecture`
2. `feat(graphics): own bounded rgba and png images`
3. `feat(sixel): quantize bounded rgba images`
4. `feat(sixel): encode deterministic graphics`
5. `test(sixel): add independent reference decoder`
6. `feat(protocols): negotiate and route sixel`
7. `feat(kitty): encode chunked graphics commands`
8. `feat(kitty): route replies and retain placements`
9. `feat(iterm): encode memory-only inline images`
10. `feat(rendering): record semantic image placements`
11. `feat(rendering): commit graphics lifecycle atomically`
12. `feat(controls): add image control and fallbacks`
13. `feat(showcase): demonstrate terminal graphics`
14. `test(graphics): add pty and tmux smoke coverage`
15. `docs(graphics): publish verified coverage`

For every commit:

1. add the focused failing public-behavior test;
2. confirm the expected failure;
3. implement the smallest complete behavior;
4. run focused tests;
5. run affected project tests;
6. update normative docs and source attribution;
7. stage only intentional files;
8. verify the staged patch in a disposable worktree;
9. run `make format`, `make lint`, `make build`, and `make test` before marking
   the phase complete.

## Explicitly deferred or excluded

- adaptive/dithered image quantization;
- decoding arbitrary PNG/JPEG/GIF/SVG in the library;
- Kitty file, temporary-file, and shared-memory transfer;
- Kitty animation/composition;
- iTerm2 download/file-transfer mode or terminal-selected uploads;
- opening any terminal-supplied file or URI;
- ReGIS, Tektronix, DRCS, and arbitrary rectangle protocols;
- graphics APIs that bypass semantic frames or dispatcher affinity.

These are not placeholders in the supported contract. They remain explicit
unsupported boundaries until a sourced consumer-driven plan replaces them.
