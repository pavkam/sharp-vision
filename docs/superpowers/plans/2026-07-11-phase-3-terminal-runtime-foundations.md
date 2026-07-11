# Terminal Runtime Foundations Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use
> superpowers:subagent-driven-development (recommended) or
> superpowers:executing-plans to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build Unicode 17 cell geometry, grapheme-safe frame rendering, typed
terminal input, bounded transport, resize delivery, and reversible terminal
lifecycle behavior on top of the verified Phase 2 protocol engine.

**Architecture:** Keep Unicode, semantic frames, damage planning, encoding,
transport, input decoding, and lifecycle ownership as separate units inside
`SharpVision.Terminal`. Frames own pooled UTF-8 grapheme storage; the renderer
commits a front frame only after a complete transport write. Input is decoded
incrementally into immutable values before Phase 4's dispatcher receives it.

**Tech Stack:** .NET 10, C# 14, `Rune`, spans and memory, `ArrayPool<T>`,
`IBufferWriter<byte>`, `TimeProvider`, xUnit v3, Shouldly, Unicode 17.0.0, UAX
29 revision 47, UAX 11 revision 44, xterm patch 410, Kitty keyboard protocol

---

## Non-negotiable boundaries

- This phase owns terminal-side geometry, input, rendering, transport, resize,
  and terminal mode restoration. Phase 4 owns the UI dispatcher, control event
  routing, application `Idle`, timers, and control lifecycle.
- Unicode tables are generated from pinned official source files and checked in.
  Restore, build, and test never require network access.
- A cell never stores a `string` for its grapheme. A frame owns pooled UTF-8
  storage and cells store bounded slices into that arena.
- The renderer never commits its front frame before the transport confirms the
  full write. Failure leaves terminal state unknown and forces a full redraw.
- Environment-derived tentative capabilities never enable a terminal mode.
- Terminal callbacks receive owned immutable paste data and value-type key,
  pointer, focus, and resize records. Borrowed parser spans never cross the
  callback boundary.
- Direct awaited writes provide bounded backpressure; this phase does not add an
  unbounded output queue.

## File map

### Unicode and geometry

- `data/unicode/17.0.0/`: checked-in official Unicode property, emoji, and
  grapheme-conformance inputs used by offline generation and tests.
- `scripts/generate-unicode-data.mjs`: downloads pinned Unicode 17 files only
  under an explicit refresh flag, verifies versions/hashes, and
  deterministically emits C# ranges from checked-in data.
- `src/SharpVision.Terminal/Unicode/Data.g.cs`: generated non-overlapping ranges
  for grapheme break, Indic conjunct, East Asian width, emoji presentation, and
  extended pictographic properties.
- `src/SharpVision.Terminal/Unicode/Info.cs`: reports the pinned Unicode and
  annex revisions.
- `src/SharpVision.Terminal/Unicode/Grapheme.cs`: borrowed segment coordinates
  and the allocation-free UTF-16 enumerator.
- `src/SharpVision.Terminal/Unicode/Width.cs`: cluster width and whole-span
  measurement under explicit ambiguous-width policy.
- `src/SharpVision.Terminal/Geometry/{Point,Size,Rect,Metrics}.cs`: validated
  cell/pixel primitives shared by input, frames, resize, and the later UI layer.

### Frames and rendering

- `src/SharpVision.Terminal/Rendering/{Attributes,Style,Cell,CellInfo}.cs`:
  semantic style and internal cell ownership values.
- `src/SharpVision.Terminal/Rendering/{Edge,DrawResult,Frame,Canvas}.cs`: pooled
  semantic frame and grapheme-safe drawing API.
- `src/SharpVision.Terminal/Rendering/{DamageSpan,Damage}.cs`: row-hash fast
  path and allocation-free semantic damage enumeration.
- `src/SharpVision.Terminal/Rendering/{Encoder,Metrics,Renderer}.cs`: exact byte
  encoding, committed front-frame ownership, pooled output, synchronized-output
  wrapping, and failure invalidation.
- `tests/SharpVision.Terminal.Tests/Support/VirtualScreen.cs`: independent
  terminal model used to compare incremental and full renders.

### Input, transport, and runtime

- `src/SharpVision.Terminal/Input/{Action,Code,Modifiers,Stroke,Text,Pointer,Paste,Focus}.cs`:
  immutable typed input values with contextual names.
- `src/SharpVision.Terminal/Input/{Options,IInputSink,Decoder}.cs`: bounded
  streaming UTF-8, legacy key, Kitty keyboard, focus, paste, and mouse decoder.
- `src/SharpVision.Terminal/Protocols/Keyboard.cs`: exact Kitty keyboard
  query/push/pop encoders.
- `src/SharpVision.Terminal/Protocols/Modes.cs`: typed xterm mouse tracking and
  coordinate-mode encoders.
- `src/SharpVision.Terminal/Transport/{ITransport,StreamTransport}.cs`: direct
  memory-based async byte transport with serialized writes.
- `src/SharpVision.Terminal/Runtime/{Dimensions,IResizeSource,ConsoleResizeSource,ISink,Options,Session}.cs`:
  resize source, terminal event forwarding, startup, read loop, closure/fault
  reporting, and reverse-order mode cleanup.
- `src/SharpVision.Terminal/Runtime/{UnixResizeSource,Native}.cs`: Unix
  file-descriptor size queries plus coalesced SIGWINCH wakeups with cell/pixel
  dimensions; `ConsoleResizeSource` is the portable cell-only fallback.

### Tests and specifications

- `tests/SharpVision.Terminal.Tests/Unicode/`: generated-data, conformance,
  curated width, and allocation tests.
- `tests/SharpVision.Terminal.Tests/Rendering/`: canvas repair, damage, exact
  bytes, virtual-screen equivalence, randomized frames, and failure tests.
- `tests/SharpVision.Terminal.Tests/Input/`: legacy, Kitty, paste, focus, mouse,
  fragmentation, malformed, and limit tests.
- `tests/SharpVision.Terminal.Tests/Transport/`: memory ownership, backpressure,
  closure, fault, and pseudoterminal tests.
- `tests/SharpVision.Terminal.Tests/Runtime/`: resize and lifecycle ordering
  with fake transport, resize source, and clock.
- `docs/testing/rendering.md`: the concrete frame oracle required by the
  `terminal-rendering` skill.
- Existing Unicode, rendering, input, protocol, lifecycle, memory, performance,
  pseudoterminal, and coverage specs are updated with exact shipped APIs.

## Task 1: Pin and generate Unicode 17 source data

**Files:**

- Create: `scripts/generate-unicode-data.mjs`
- Create: `data/unicode/17.0.0/GraphemeBreakProperty.txt`
- Create: `data/unicode/17.0.0/GraphemeBreakTest.txt`
- Create: `data/unicode/17.0.0/DerivedCoreProperties.txt`
- Create: `data/unicode/17.0.0/EastAsianWidth.txt`
- Create: `data/unicode/17.0.0/emoji-data.txt`
- Create: `data/unicode/17.0.0/UnicodeData.txt`
- Create: `data/unicode/17.0.0/ReadMe.txt`
- Create: `src/SharpVision.Terminal/Unicode/Data.g.cs`
- Create: `src/SharpVision.Terminal/Unicode/Info.cs`
- Create: `tests/SharpVision.Terminal.Tests/Unicode/DataTests.cs`
- Modify: `tests/SharpVision.Terminal.Tests/SharpVision.Terminal.Tests.csproj`
- Modify: `package.json`
- Modify: `docs/concepts/unicode-cell-geometry.md`

- [x] **Step 1: Write the failing generated-data contract tests**

  Add `DataTests` proving `Info.Version == "17.0.0"`, annex revisions are 47 and
  44, every generated range is sorted and non-overlapping, boundary lookups
  return the official first/last values, and the conformance fixture header
  identifies Unicode 17.0.0. Expose generated range validation to the test
  assembly through `InternalsVisibleTo`, not a public testing shortcut.

  ```csharp
  [Fact]
  public void Version_WhenRead_ReportsPinnedUnicodeSources()
  {
      Info.Version.ShouldBe("17.0.0");
      Info.GraphemeRevision.ShouldBe(47);
      Info.WidthRevision.ShouldBe(44);
  }
  ```

- [x] **Step 2: Run the focused test and verify RED**

  Run:

  ```bash
  dotnet test --project tests/SharpVision.Terminal.Tests/SharpVision.Terminal.Tests.csproj --filter-class "*DataTests" --timeout 60s
  ```

  Expected: compile failure because `SharpVision.Terminal.Unicode.Info` and the
  generated table do not exist.

- [x] **Step 3: Add the deterministic generator and checked-in output**

  Pin these official inputs under `https://www.unicode.org/Public/17.0.0/ucd/`:

  - `auxiliary/GraphemeBreakProperty.txt`
  - `auxiliary/GraphemeBreakTest.txt`
  - `DerivedCoreProperties.txt` for `Indic_Conjunct_Break`
  - `EastAsianWidth.txt`
  - `emoji/emoji-data.txt`

  The script reads checked-in files by default and accepts `--check` to compare
  generated output without writing. An explicit `--refresh` fetches official
  inputs, verifies each header and SHA-256 recorded in the script, and replaces
  local sources before generation. Generation parses hexadecimal ranges, merges
  adjacent equal properties, writes stable UTF-8 with LF endings, and emits no
  timestamps. Add `generate:unicode`, `refresh:unicode`, and `check:unicode`
  package scripts. Link the checked-in conformance file into the test output
  through the test project. The generated lookup uses binary search over
  readonly value-type ranges and returns documented defaults when no range
  matches.

  ```csharp
  public static class Info
  {
      public const string Version = "17.0.0";
      public const int GraphemeRevision = 47;
      public const int WidthRevision = 44;
  }
  ```

- [x] **Step 4: Verify generation is reproducible and tests are GREEN**

  Run `npm run check:unicode`, then the focused command from Step 2. Expected:
  the generator reports no diff and every `DataTests` case passes.

- [x] **Step 5: Commit**

  Commit message: `feat: pin Unicode 17 terminal data`

## Task 2: Implement extended grapheme segmentation

**Files:**

- Create: `src/SharpVision.Terminal/Unicode/Grapheme.cs`
- Create: `tests/SharpVision.Terminal.Tests/Unicode/GraphemeTests.cs`
- Create: `tests/SharpVision.Terminal.Tests/Unicode/ConformanceTests.cs`
- Modify: `docs/concepts/unicode-cell-geometry.md`
- Modify: `docs/testing/unicode-rendering.md`

- [x] **Step 1: Write curated and full-conformance failing tests**

  Curated cases cover CR/LF, controls, combining marks, Hangul, prepend and
  spacing marks, Indic conjuncts, regional-indicator parity, emoji modifiers,
  ZWJ families, keycaps, tag flags, and lone UTF-16 surrogates. Parse every
  non-comment line of the checked-in GraphemeBreakTest file and compare expected
  UTF-16 boundaries. Failure output includes source line and scalar list.

  ```csharp
  [Theory]
  [InlineData("e\u0301", 1)]
  [InlineData("👩‍👩‍👧‍👦", 1)]
  [InlineData("🇵🇹🇬🇧", 2)]
  public void Enumerate_WhenTextContainsExtendedClusters_ReturnsExpectedCount(
      string value,
      int expected)
  {
      Count(Graphemes.Enumerate(value.AsSpan())).ShouldBe(expected);
  }
  ```

- [x] **Step 2: Run segmentation tests and verify RED**

  Run with `--filter-class "*GraphemeTests" "*ConformanceTests"`. Expected:
  compile failure because `Graphemes` and its enumerator do not exist.

- [x] **Step 3: Implement the allocation-free enumerator**

  Add a `ref struct GraphemeEnumerator` over `ReadOnlySpan<char>` and a readonly
  `Grapheme` containing `Offset`, `Length`, and `HasInvalidData`. `MoveNext`
  decodes with `Rune.DecodeFromUtf16`, consumes one UTF-16 code unit as U+FFFD
  on invalid data, and applies UAX 29 extended rules GB3 through GB13 and GB999.
  Track regional-indicator parity, extended-pictographic/Extend/ZWJ history, and
  Indic consonant/linker state without allocating or normalizing.

  ```csharp
  public static GraphemeEnumerator Enumerate(ReadOnlySpan<char> value) =>
      new(value);
  ```

  Debug assertions prove every returned segment is non-empty, ordered, and
  inside the original span.

- [x] **Step 4: Run curated, conformance, and allocation tests**

  Add a warmed 10,000-iteration enumeration test requiring zero managed bytes.
  Run the focused tests; expected: all Unicode 17 boundary lines and curated
  cases pass.

- [x] **Step 5: Commit**

  Commit message: `feat: segment Unicode grapheme clusters`

## Task 3: Implement terminal cell-width policy

**Files:**

- Create: `src/SharpVision.Terminal/Unicode/Width.cs`
- Create: `tests/SharpVision.Terminal.Tests/Unicode/WidthTests.cs`
- Create: `tests/SharpVision.Terminal.Tests/Unicode/MeasurementTests.cs`
- Modify: `src/SharpVision.Terminal/Capabilities/Capabilities.cs`
- Modify: `docs/concepts/unicode-cell-geometry.md`

- [x] **Step 1: Write width-policy tests and verify RED**

  Test ASCII, CJK, supplementary ideographs, half/full/ambiguous values under
  both policies, precomposed/decomposed equivalence, VS15/VS16, emoji modifiers,
  keycaps, flags, tag flags, ZWJ emoji, orphan marks, controls, private use,
  unassigned scalars, and invalid UTF-16 replacement. Require total width,
  cluster count, and control count from one pass.

  ```csharp
  Width.Measure("A界👩‍💻".AsSpan(), Ambiguous.Narrow).Cells.ShouldBe(5);
  Width.Measure("é".AsSpan(), Ambiguous.Narrow).Cells.ShouldBe(
      Width.Measure("e\u0301".AsSpan(), Ambiguous.Narrow).Cells);
  ```

- [x] **Step 2: Implement explicit cluster-width rules**

  Add `Ambiguous` (`Narrow`, `Wide`), `CellWidth` (`Control`, `Narrow`, `Wide`),
  and `Measurement`. Width is assigned to a complete grapheme: W/F East Asian
  width, emoji-presentation clusters, keycaps, flags, tag sequences, and emoji
  ZWJ sequences are wide; VS15 requests text presentation; VS16 requests emoji
  presentation; A follows the explicit policy; combining-only printable clusters
  occupy one repairable cell; C0/C1/CR/LF/TAB report `Control`.

  Add `AmbiguousWidth` and `UnicodeVersion` to immutable `Capabilities`, with
  caller overrides applied last. Never infer wide ambiguous characters from a
  locale environment variable.

- [x] **Step 3: Run focused tests and allocation proof**

  Run `--filter-class "*WidthTests" "*MeasurementTests"`. Add warmed mixed-text
  measurement requiring zero managed bytes per Rune/cluster. Expected: all tests
  pass.

- [x] **Step 4: Commit**

  Commit message: `feat: measure terminal cell widths`

## Task 4: Build pooled semantic frames and grapheme-safe canvas drawing

**Files:**

- Create: `src/SharpVision.Terminal/Geometry/Point.cs`
- Create: `src/SharpVision.Terminal/Geometry/Size.cs`
- Create: `src/SharpVision.Terminal/Geometry/Rect.cs`
- Create: `src/SharpVision.Terminal/Geometry/Metrics.cs`
- Create: `src/SharpVision.Terminal/Rendering/Attributes.cs`
- Create: `src/SharpVision.Terminal/Rendering/Style.cs`
- Create: `src/SharpVision.Terminal/Rendering/Cell.cs`
- Create: `src/SharpVision.Terminal/Rendering/CellInfo.cs`
- Create: `src/SharpVision.Terminal/Rendering/Edge.cs`
- Create: `src/SharpVision.Terminal/Rendering/DrawResult.cs`
- Create: `src/SharpVision.Terminal/Rendering/Frame.cs`
- Create: `src/SharpVision.Terminal/Rendering/Canvas.cs`
- Create: `tests/SharpVision.Terminal.Tests/Rendering/FrameTests.cs`
- Create: `tests/SharpVision.Terminal.Tests/Rendering/CanvasTests.cs`
- Modify: `docs/architecture/memory-ownership.md`
- Modify: `docs/architecture/rendering-pipeline.md`

- [x] **Step 1: Write geometry, ownership, and repair tests**

  Cover negative validation, zero-sized frames, bounds, clipping rectangles,
  style equality, UTF-8 copy sizing, narrow/wide drawing, combining and ZWJ
  clusters, wrap/clip/replace at the right edge, bottom overflow, overwrite of a
  wide lead, overwrite of a continuation, clearing either half, and disposal.

  ```csharp
  frame.Canvas.Draw("界".AsSpan(), new Point(0, 0), Style.Default, Edge.Clip);
  frame.Canvas.Draw("x".AsSpan(), new Point(1, 0), Style.Default, Edge.Clip);

  frame.GetCell(new Point(0, 0)).IsContinuation.ShouldBeFalse();
  frame.GetCell(new Point(1, 0)).IsContinuation.ShouldBeFalse();
  ```

- [x] **Step 2: Run the focused tests and verify RED**

  Run with `--filter-class "*FrameTests" "*CanvasTests"`. Expected: compile
  failure because geometry, `Frame`, and `Canvas` are absent.

- [x] **Step 3: Implement frame and canvas ownership**

  `Frame` rents a cleared cell array and a bounded growable UTF-8 arena. Lead
  cells store arena offset/length/hash, width, and `Style`; continuation cells
  store the absolute lead index. `CellInfo` exposes semantic metadata only, and
  `CopyGrapheme(Point, Span<byte>)` is the public payload boundary. Disposal
  clears references, UTF-8 bytes, and cells before pool return.

  ```csharp
  public sealed class Frame: IDisposable
  {
      public Frame(Size size, int maxTextBytes = 16 * 1024 * 1024);
      public Size Size { get; }
      public Canvas Canvas { get; }
      public CellInfo GetCell(Point point);
      public int GetGraphemeByteCount(Point point);
      public int CopyGrapheme(Point point, Span<byte> destination);
      public void Clear(Style style = default);
  }
  ```

  Before any write or clear, expand repair to the complete old ownership range.
  Before a wide write, repair both destination cells. `Edge.Wrap`, `Clip`, and
  `Replace` are explicit; no path writes half a cluster. Row hashes are dirtied
  by every semantic mutation and sealed lazily.

- [x] **Step 4: Run focused and randomized ownership tests**

  Seed random narrow/wide writes and clears, then assert every continuation
  points to a valid lead whose width covers it. Run focused tests; expected: all
  pass with the seed printed on failure.

- [x] **Step 5: Commit**

  Commit message: `feat: add grapheme-safe cell frames`

## Task 5: Implement semantic damage and exact frame encoding

**Files:**

- Create: `src/SharpVision.Terminal/Rendering/DamageSpan.cs`
- Create: `src/SharpVision.Terminal/Rendering/Damage.cs`
- Create: `src/SharpVision.Terminal/Rendering/Encoder.cs`
- Create: `tests/SharpVision.Terminal.Tests/Support/VirtualScreen.cs`
- Create: `tests/SharpVision.Terminal.Tests/Rendering/DamageTests.cs`
- Create: `tests/SharpVision.Terminal.Tests/Rendering/EncoderTests.cs`
- Create: `tests/SharpVision.Terminal.Tests/Rendering/EquivalenceTests.cs`
- Create: `docs/testing/rendering.md`
- Modify: `docs/architecture/rendering-pipeline.md`
- Modify: `docs/testing/unicode-rendering.md`

- [x] **Step 1: Write damage and terminal-model tests**

  Cover no change, sparse/dense rows, style-only change, deletion, hyperlink
  change, narrow-to-wide, wide-to-narrow, changed combining sequence, right and
  bottom edges, cursor visibility/position, and full invalidation. Exact-byte
  tests use literal expected bytes, while equivalence tests apply production
  output to an independent `VirtualScreen` and compare it with a clean full
  render of the same target frame.

- [x] **Step 2: Run the focused tests and verify RED**

  Run `--filter-class "*DamageTests" "*EncoderTests" "*EquivalenceTests"`.
  Expected: compile failure because damage and encoding types are absent.

- [x] **Step 3: Implement allocation-free damage enumeration**

  `Damage.Enumerate(front, back, full)` returns a `ref struct` enumerator.
  Changed cells expand through lead and continuation ownership in both frames;
  adjacent spans merge. Hashes reject mismatched graphemes quickly, but equal
  hashes still compare complete bytes so collisions cannot hide damage. Semantic
  equality includes width, attributes, colors, hyperlink text, and renderer
  metadata rather than raw struct bytes.

- [x] **Step 4: Implement deterministic encoding**

  `Encoder` writes absolute cursor positions, minimal required style and OSC 8
  transitions, grapheme UTF-8, explicit blanks, and the target cursor state to
  an `IBufferWriter<byte>`. A full render starts from reset state; an
  incremental render receives the committed cursor/style state. End state is
  always known. A changed continuation is never emitted independently.

- [x] **Step 5: Prove targeted and randomized equivalence**

  Add fixed-seed random frame pairs with random styles, hyperlinks, ASCII, CJK,
  combining, and emoji clusters. For each pair compare incremental application
  with full-render application. Print seed, dimensions, and both semantic frames
  on failure. Expected: all focused tests pass.

- [x] **Step 6: Commit**

  Commit message: `feat: encode equivalent frame diffs`

## Task 6: Add bounded transport and commit-on-success rendering

**Files:**

- Create: `src/SharpVision.Terminal/Transport/ITransport.cs`
- Create: `src/SharpVision.Terminal/Transport/StreamTransport.cs`
- Create: `src/SharpVision.Terminal/Rendering/Metrics.cs`
- Create: `src/SharpVision.Terminal/Rendering/Renderer.cs`
- Create: `tests/SharpVision.Terminal.Tests/Support/FakeTransport.cs`
- Create: `tests/SharpVision.Terminal.Tests/Transport/StreamTransportTests.cs`
- Create: `tests/SharpVision.Terminal.Tests/Rendering/RendererTests.cs`
- Modify: `docs/architecture/rendering-pipeline.md`
- Modify: `docs/architecture/memory-ownership.md`
- Modify: `docs/testing/performance.md`

- [x] **Step 1: Write transport and renderer failure tests**

  Test async read/write, caller cancellation, serialized concurrent writes,
  leave-open ownership, disposal, a deliberately blocked write, successful
  commit, no-op frame, resize invalidation, capability invalidation, partial/
  failed write, cancellation, synchronized wrapping, cleanup failure, and
  original-exception preservation.

- [x] **Step 2: Run focused tests and verify RED**

  Run `--filter-class "*StreamTransportTests" "*RendererTests"`. Expected:
  compile failure because transport and renderer contracts are absent.

- [x] **Step 3: Implement direct awaited transport**

  ```csharp
  public interface ITransport: IAsyncDisposable
  {
      ValueTask<int> ReadAsync(Memory<byte> destination, CancellationToken cancellationToken);
      ValueTask WriteAsync(ReadOnlyMemory<byte> source, CancellationToken cancellationToken);
      ValueTask FlushAsync(CancellationToken cancellationToken);
  }
  ```

  `StreamTransport` validates readable/writable streams, uses memory overloads,
  serializes complete writes with a `SemaphoreSlim`, invokes no callback under
  the gate, and honors `leaveOpen`. Awaiting the underlying stream is the
  bounded-backpressure mechanism.

- [x] **Step 4: Implement renderer ownership and recovery**

  `Renderer` owns the committed front frame and a cleared pooled byte writer.
  `RenderAsync` encodes against front state, awaits one complete transport write
  and flush, then copies/switches target state into front. On failure it marks
  state unknown, attempts a separate synchronized-output reset when needed,
  preserves the original exception, and requires a full redraw next time.
  `Metrics` reports bytes, writes, spans, full/incremental, and elapsed time
  only for completed frames.

- [x] **Step 5: Run focused tests and allocation checks**

  Require a warmed no-change frame to allocate zero and sparse/dense frames to
  allocate only within the reusable renderer buffer. Expected: focused tests
  pass and the blocked fake proves backpressure without queue growth.

- [x] **Step 6: Commit**

  Commit message: `feat: render frames through bounded transport`

## Task 7: Define typed input and decode UTF-8 plus legacy keys

**Files:**

- Create: `src/SharpVision.Terminal/Input/Action.cs`
- Create: `src/SharpVision.Terminal/Input/Code.cs`
- Create: `src/SharpVision.Terminal/Input/Modifiers.cs`
- Create: `src/SharpVision.Terminal/Input/Stroke.cs`
- Create: `src/SharpVision.Terminal/Input/Text.cs`
- Create: `src/SharpVision.Terminal/Input/Pointer.cs`
- Create: `src/SharpVision.Terminal/Input/Paste.cs`
- Create: `src/SharpVision.Terminal/Input/Focus.cs`
- Create: `src/SharpVision.Terminal/Input/Options.cs`
- Create: `src/SharpVision.Terminal/Input/IInputSink.cs`
- Create: `src/SharpVision.Terminal/Input/Decoder.cs`
- Modify: `src/SharpVision.Terminal/Protocols/Parser.cs`
- Create: `tests/SharpVision.Terminal.Tests/Support/RecordingInputSink.cs`
- Create: `tests/SharpVision.Terminal.Tests/Input/TextTests.cs`
- Create: `tests/SharpVision.Terminal.Tests/Input/LegacyKeyTests.cs`
- Modify: `docs/concepts/input-routing.md`
- Modify: `docs/protocols/ansi-vt.md`

- [x] **Step 1: Write typed text and legacy-key tests**

  Cover fragmented UTF-8, invalid UTF-8 replacement, plain/Alt text, Enter, Tab,
  Backspace, Escape expiration, arrows, Home/End, Insert/Delete, Page keys,
  F1-F12, Shift-Tab, CSI modifiers, SS3, adjacent events, unknown valid keys,
  malformed sequences, and end-of-stream. Repeat representative inputs at every
  byte split.

- [x] **Step 2: Run focused tests and verify RED**

  Run `--filter-class "*TextTests" "*LegacyKeyTests"`. Expected: compile failure
  because input values and `Input.Decoder` do not exist.

- [x] **Step 3: Implement value contracts and streaming UTF-8**

  `Stroke` preserves logical `Code`, optional character `Rune`, native numeric
  code, `Modifiers`, and press/repeat/release `Action`. `Text` contains one
  valid `Rune`. `IInputSink` has overloads for `Stroke`, `Text`, `Pointer`,
  `Paste`, `Focus`, and redacted `Diagnostic`.

  `Decoder` owns a protocol `Parser`, at most three pending UTF-8 bytes, SS3/X10
  continuation state, and a configurable finite paste limit and Escape timeout.
  Add read-only `Parser.IsGround` so a trailing raw Escape can be held and
  expired without guessing private parser state. Decode with
  `Rune.DecodeFromUtf8`.

- [x] **Step 4: Implement legacy VT mapping and Escape policy**

  Map official CSI/SS3 functional forms and modifier value minus one. A lone
  Escape becomes `Code.Escape` only when `ExpireEscape` reaches the injected
  `TimeProvider` deadline or `Complete` is called. ESC plus printable input is
  one Alt-modified stroke/text pair. Unknown valid sequences are reported and do
  not desynchronize later input.

- [x] **Step 5: Run focused fragmentation and allocation tests**

  Expected: every legacy representative matches at all split points, malformed
  input recovers, and warmed ASCII/Rune decoding allocates zero per event.

- [x] **Step 6: Commit**

  Commit message: `feat: decode typed terminal keys and text`

## Task 8: Decode bounded paste, focus, and cell/pixel mouse input

**Files:**

- Modify: `src/SharpVision.Terminal/Input/Decoder.cs`
- Modify: `src/SharpVision.Terminal/Input/Options.cs`
- Create: `tests/SharpVision.Terminal.Tests/Input/PasteTests.cs`
- Create: `tests/SharpVision.Terminal.Tests/Input/FocusTests.cs`
- Create: `tests/SharpVision.Terminal.Tests/Input/MouseTests.cs`
- Modify: `docs/protocols/paste-focus.md`
- Modify: `docs/protocols/mouse.md`
- Modify: `docs/architecture/memory-ownership.md`

- [x] **Step 1: Write paste, focus, and mouse tests**

  Paste cases cover empty/multiline/Unicode data, embedded ESC, every proper
  prefix of the end marker, marker-like payload, all split points, invalid
  UTF-8, overflow discard, truncation, recovery, and owned-memory retention.
  Focus covers gained/lost and adjacency. Mouse covers X10/VT200, UTF-8 1005,
  SGR 1006, pixel 1016, urxvt 1015 compatibility, every button/modifier/action,
  wheel axes, motion, extra buttons, leave, zero/maximum coordinates, malformed
  values, metrics conversion, and fragmentation.

- [x] **Step 2: Run focused tests and verify RED**

  Run `--filter-class "*PasteTests" "*FocusTests" "*MouseTests"`. Expected:
  tests fail because the decoder reports these inputs only as raw sequences.

- [x] **Step 3: Implement raw paste mode with bounded ownership**

  Once CSI 200~ is decoded, feed subsequent bytes to a streaming exact-marker
  matcher rather than the protocol parser until CSI 201~. Prefix bytes are held
  in a six-byte scratch span; mismatches flush to a cleared pooled buffer.
  Overflow discards through the terminator and reports once. Success validates
  UTF-8, copies exact bytes into owned immutable memory, clears the pool, emits
  one `Paste`, and resumes protocol parsing.

- [x] **Step 4: Implement focus and pointer decoding**

  Decode CSI I/O, X10 three-field input, SGR and urxvt decimal input. Convert
  wire one-based coordinates once. In pixel mode preserve raw pixels and derive
  cells with validated positive `Geometry.Metrics`; set
  `IsCellPositionInferred`. Cell mode leaves `Pixels` absent. Preserve wheel
  deltas, buttons, modifiers, action, motion, and leave distinctly.

- [x] **Step 5: Run every-split and hostile-limit tests**

  Expected: all cases pass; a multi-megabyte unterminated paste remains within
  its configured allocation budget and a trailing arrow key is decoded.

- [x] **Step 6: Commit**

  Commit message: `feat: decode paste focus and pointer input`

## Task 9: Implement Kitty keyboard negotiation and decoding

**Files:**

- Create: `src/SharpVision.Terminal/Protocols/Keyboard.cs`
- Modify: `src/SharpVision.Terminal/Input/Decoder.cs`
- Modify: `src/SharpVision.Terminal/Capabilities/Overrides.cs`
- Modify: `src/SharpVision.Terminal/Capabilities/QueryTracker.cs`
- Create: `tests/SharpVision.Terminal.Tests/Protocols/KeyboardTests.cs`
- Create: `tests/SharpVision.Terminal.Tests/Input/KittyKeyboardTests.cs`
- Modify: `docs/protocols/kitty-keyboard.md`
- Modify: `docs/protocols/device-attributes.md`

- [ ] **Step 1: Write exact-byte and official-example tests**

  Encoder tests require query `CSI ? u`, push `CSI > flags u`, pop `CSI < u`,
  flag validation, and no mutation on invalid values. Decoder tests use official
  Kitty examples plus alternate keys, associated text, every modifier/event
  kind, Enter/Tab/Backspace, functional keys, Unicode, unknown codes, malformed
  fields, query replies, all split points, and fallback coexistence.

- [ ] **Step 2: Run focused tests and verify RED**

  Run `--filter-class "*KeyboardTests" "*KittyKeyboardTests"`. Expected:
  compile/test failure because keyboard commands and CSI-u decoding are absent.

- [ ] **Step 3: Implement keyboard commands and typed replies**

  Add validated flags for disambiguation, event types, alternate keys, all-keys,
  and associated text. The first milestone pushes disambiguation plus event
  types and pops one stack entry during cleanup. Extend typed CSI responses and
  `QueryTracker` with the uncorrelated Kitty-keyboard family; a DA reply before
  keyboard status classifies the query as unsupported.

- [ ] **Step 4: Implement CSI-u decoding**

  Parse main/shift/base code alternatives, modifier/event subparameters, and
  associated Unicode code points with finite counts and scalar validation.
  Preserve unknown functional code numbers. Emit one `Stroke` followed by
  ordered `Text` values for associated text. Invalid fields report once and do
  not consume the next valid event.

- [ ] **Step 5: Run focused and integration tests**

  Add writer-to-parser-to-input integration for push/query/reply/key/pop.
  Expected: exact bytes, every-split decoding, correlation, and fallback pass.

- [ ] **Step 6: Commit**

  Commit message: `feat: support Kitty keyboard input`

## Task 10: Add mouse modes, resize sources, and reversible terminal session

**Files:**

- Modify: `src/SharpVision.Terminal/Protocols/Modes.cs`
- Modify: `src/SharpVision.Terminal/Capabilities/Capabilities.cs`
- Modify: `src/SharpVision.Terminal/Capabilities/Overrides.cs`
- Create: `src/SharpVision.Terminal/Runtime/Dimensions.cs`
- Create: `src/SharpVision.Terminal/Runtime/IResizeSource.cs`
- Create: `src/SharpVision.Terminal/Runtime/ConsoleResizeSource.cs`
- Create: `src/SharpVision.Terminal/Runtime/UnixResizeSource.cs`
- Create: `src/SharpVision.Terminal/Runtime/Native.cs`
- Create: `src/SharpVision.Terminal/Runtime/ISink.cs`
- Create: `src/SharpVision.Terminal/Runtime/Options.cs`
- Create: `src/SharpVision.Terminal/Runtime/Session.cs`
- Create: `tests/SharpVision.Terminal.Tests/Support/FakeResizeSource.cs`
- Create: `tests/SharpVision.Terminal.Tests/Runtime/ResizeTests.cs`
- Create: `tests/SharpVision.Terminal.Tests/Runtime/SessionTests.cs`
- Modify: `docs/architecture/runtime-event-loop.md`
- Modify: `docs/concepts/lifecycle-events.md`
- Modify: `docs/protocols/dec-private-modes.md`
- Modify: `docs/protocols/mouse.md`

- [ ] **Step 1: Write exact mode and lifecycle-order tests**

  Test modes 9/1000/1002/1003/1005/1006/1015/1016, invalid combinations, startup
  bytes, partial startup failure, reverse cleanup, repeated disposal, read
  closure, read fault, handler fault, cancellation, cleanup fault preserving the
  original, resize coalescing inputs, zero-cell suspension, pixel metrics, and
  no callback under the transport write gate.

- [ ] **Step 2: Run focused tests and verify RED**

  Run `--filter-class "*ModesTests" "*ResizeTests" "*SessionTests"`. Expected:
  failures because mouse mode and runtime session APIs are absent.

- [ ] **Step 3: Implement typed mode planning**

  Add `CellMouse` to `Capabilities`. `Modes` validates tracking and coordinate
  enums and emits exact private set/reset bytes. `Runtime.Options` selects
  alternate screen, cursor visibility, focus, paste, pointer tracking, Kitty
  keyboard flags, input limits, and cleanup timeout. Session planning includes
  only `Feature.IsSupported` optional modes and records every successful enable
  for reverse disable/pop.

- [ ] **Step 4: Implement resize and session loops**

  `Dimensions` contains non-negative cells plus optional non-negative pixels and
  derives positive metrics only when both axes permit it. `UnixResizeSource`
  reads `winsize` through OS-specific `TIOCGWINSZ` values and uses a
  capacity-one SIGWINCH channel to coalesce wakeups without invoking callers in
  the signal callback. `ConsoleResizeSource` uses an injected finite polling
  interval and `TimeProvider` as the portable cell-only fallback, returns only
  changes, and waits without spinning. `Session.RunAsync` owns one input read
  buffer, forwards immutable values through `Runtime.ISink`, reports resize,
  closed, faulted, and diagnostics, and restores modes in `finally`.

  Session events are terminal-side records. UI resize coalescing, committed root
  layout, application lifecycle, timers, and `Idle` remain explicit Phase 4
  responsibilities.

- [ ] **Step 5: Run lifecycle and no-spin tests**

  Use deterministic fake transport/resize source/clock. Expected: ordering and
  cleanup tests pass, waits remain blocked until fake input/resize/time
  advances, and terminal fault never hides restoration diagnostics.

- [ ] **Step 6: Commit**

  Commit message: `feat: add terminal runtime lifecycle`

## Task 11: Add pseudoterminal, randomized, performance, and documentation proof

**Files:**

- Create: `tests/SharpVision.Terminal.Tests/Support/UnixPseudoterminal.cs`
- Create: `tests/SharpVision.Terminal.Tests/Transport/PseudoterminalTests.cs`
- Create:
  `tests/SharpVision.Terminal.Tests/Rendering/RandomizedRenderingTests.cs`
- Create: `tests/SharpVision.Terminal.Tests/Input/RandomizedInputTests.cs`
- Modify: `docs/protocols/coverage-matrix.md`
- Modify:
  `docs/protocols/{ansi-vt,mouse,paste-focus,kitty-keyboard,synchronized-output}.md`
- Modify:
  `docs/architecture/{project-structure,rendering-pipeline,runtime-event-loop,memory-ownership,error-handling}.md`
- Modify:
  `docs/concepts/{unicode-cell-geometry,input-routing,lifecycle-events,safe-degradation}.md`
- Modify:
  `docs/testing/{unicode-rendering,rendering,performance,pseudoterminals,terminal-protocols}.md`

- [ ] **Step 1: Add actual Unix pseudoterminal transport proof**

  On Linux/macOS, the test helper opens a PTY pair, owns both descriptors, and
  exposes a deterministic cleanup point. Drive `StreamTransport` through the
  slave, verify exact bidirectional bytes through the master, propagate EOF,
  change the PTY window size, signal SIGWINCH, and prove `UnixResizeSource` plus
  the runtime receive the newest cell and pixel dimensions. On other platforms
  use xUnit's explicit runtime skip with a reason; CI's Windows job supplies the
  console-specific counterpart in Phase 6.

- [ ] **Step 2: Add fixed-seed hostile and equivalence suites**

  Random input must terminate within the test deadline, remain bounded, and
  recover to a known key. Random frame transitions must equal a full render and
  preserve every ownership invariant. Print seed/case/input/frame on failure and
  promote any discovered issue to a named regression before changing code.

- [ ] **Step 3: Add allocation and throughput regression checks**

  Measure warmed ASCII/mixed/emoji segmentation, frame no-op/sparse/dense scans,
  encoding, legacy text, mouse, and Kitty keys. Gate deterministic allocation
  budgets; record timing and architecture without making noisy wall-clock timing
  a local pass/fail criterion.

- [ ] **Step 4: Publish exact support and ownership documentation**

  Change coverage rows only where typed implementation and tests exist. Name
  unsupported graphics/image/passthrough boundaries. Document public type names,
  Unicode sources, width policy, cell repair, transport backpressure, input
  limits, pixel inference, resize order, session cleanup, borrowed/owned memory,
  safe degradation, and the fact that application `Idle` starts in Phase 4.

- [ ] **Step 5: Run focused documentation and Phase 3 tests**

  Run `npm run check:unicode`, `make lint`, and terminal tests with
  `--minimum-expected-tests 1`. Expected: generator, analyzers, Markdown,
  section links, docs tests, and all terminal tests pass.

- [ ] **Step 6: Commit**

  Commit message: `docs: publish terminal runtime guarantees`

## Task 12: Phase 3 verification and audit

**Files:**

- Modify only files required by verified failures.

- [ ] **Step 1: Run formatting**

  Run `make format`. Expected: exit 0 and no unintended diff.

- [ ] **Step 2: Run lint and generated-data checks**

  Run `npm run check:unicode` and `make lint`. Expected: generated output is
  current; analyzers, Prettier, Markdownlint, skills, links, and docs tests
  pass.

- [ ] **Step 3: Run release build**

  Run `make build`. Expected: all six projects build with 0 warnings and 0
  errors.

- [ ] **Step 4: Run all tests and inspect discovery**

  Run `make test`, then each test project independently with
  `--minimum-expected-tests 1`. Expected: every test passes and each of the
  terminal, UI, and showcase assemblies reports a non-zero count.

- [ ] **Step 5: Audit repository and public surface**

  Run:

  ```bash
  git diff --check
  git status --short
  rg -n "TODO|TBD|NotImplementedException" src tests docs scripts
  dotnet build SharpVision.slnx --configuration Release --no-restore
  ```

  Expected: clean whitespace, only the final intended plan update before commit,
  no implementation placeholders, and 0 warnings/errors.

- [ ] **Step 6: Commit the verified phase**

  Commit message: `chore: complete terminal runtime foundations`

## Self-review record

- **Spec coverage:** Tasks map every Phase 3 roadmap requirement: Unicode 17
  grapheme segmentation and width, cell/pixel geometry, wide-cell repair,
  semantic frames, damage, full/incremental equivalence, transport backpressure,
  synchronized output, typed key/text/paste/focus/cell/pixel mouse input, Kitty
  keyboard, resize events, terminal closure/fault, mode restoration,
  pseudoterminal proof, allocation, randomized invariants, and docs. Application
  `Idle` and dispatcher ordering remain assigned to Phase 4 and are named at the
  boundary rather than omitted.
- **Primary sources:** Unicode data is pinned to Unicode 17.0.0, UAX 29 revision
  47, and UAX 11 revision 44. Mouse behavior follows xterm patch 410. Keyboard
  behavior follows Kitty's current protocol, including query, stack push/pop,
  flags, modifiers, event types, and associated text.
- **Placeholder scan:** Every task has exact files, failing proof,
  implementation contract, green command, and commit. The final audit contains
  the literal search tokens intentionally; no product behavior is represented by
  a stub.
- **Type consistency:** `Geometry.Point/Size/Metrics`, `Unicode.Grapheme/Width`,
  `Rendering.Frame/Canvas/Renderer`, `Input.Decoder`, `Transport.ITransport`,
  and `Runtime.Session` are the canonical names used across all tasks. The
  namespace supplies context, so repeated `Terminal` prefixes and `Control`
  suffixes are avoided.
