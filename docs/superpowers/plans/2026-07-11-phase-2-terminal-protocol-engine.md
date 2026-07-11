# Phase 2 Terminal Protocol Engine Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use
> superpowers:subagent-driven-development (recommended) or
> superpowers:executing-plans to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a bounded, allocation-conscious ECMA-48 protocol engine with
typed terminal commands, OSC 52 and Kitty OSC 5522 clipboard transactions, and
immutable conservative capability profiles.

**Architecture:** `SharpVision.Terminal.Protocols` owns a streaming byte parser
and a synchronous `IBufferWriter<byte>` encoder. The parser preserves raw
grammar at its boundary, lends spans only during callbacks, bounds every
retained field, and recovers after malformed or hostile input. Typed command,
clipboard, and capability layers sit above that grammar and degrade safely when
features are absent or replies are malformed, late, or contradictory.

**Tech Stack:** .NET 10, C# 14, `System.Buffers`, `System.Text`, xUnit v3,
Shouldly, Moq

---

## File map

### Protocol grammar and encoding

- `src/SharpVision.Terminal/Protocols/Limits.cs`: immutable parser and
  transaction bounds.
- `src/SharpVision.Terminal/Protocols/Diagnostic.cs`: non-sensitive structured
  diagnostics.
- `src/SharpVision.Terminal/Protocols/SequenceKind.cs`: recognized ECMA-48
  sequence families.
- `src/SharpVision.Terminal/Protocols/StringTerminator.cs`: BEL and ST string
  endings.
- `src/SharpVision.Terminal/Protocols/ISequenceSink.cs`: synchronous borrowed
  span callbacks.
- `src/SharpVision.Terminal/Protocols/Parser.cs`: bounded streaming state
  machine and pooled payload storage.
- `src/SharpVision.Terminal/Protocols/Writer.cs`: validated byte-level encoder.
- `src/SharpVision.Terminal/Protocols/Parameters.cs`: allocation-free CSI
  parameter enumeration and numeric validation.
- `src/SharpVision.Terminal/Protocols/Csi.cs`: typed cursor, erase, scrolling,
  insertion, deletion, and query commands.
- `src/SharpVision.Terminal/Protocols/Sgr.cs`: typed attributes and colors.
- `src/SharpVision.Terminal/Protocols/Osc.cs`: titles, hyperlinks, and color
  queries.
- `src/SharpVision.Terminal/Protocols/Modes.cs`: DEC modes required by the
  runtime.
- `src/SharpVision.Terminal/Protocols/Responses.cs`: typed DA, DSR, DECRPM, and
  OSC query response decoding.

### Clipboard and capabilities

- `src/SharpVision.Terminal/Clipboard/Selection.cs`: clipboard selection model.
- `src/SharpVision.Terminal/Clipboard/Osc52.cs`: plain-text clipboard encoding
  and decoding.
- `src/SharpVision.Terminal/Clipboard/KittyPacket.cs`: typed OSC 5522 packet
  metadata and status parsing.
- `src/SharpVision.Terminal/Clipboard/KittyWriter.cs`: MIME write/read/list and
  alias encoders with exact 4096-byte chunking.
- `src/SharpVision.Terminal/Clipboard/KittyTransaction.cs`: bounded correlated
  read/write state machine.
- `src/SharpVision.Terminal/Capabilities/Capabilities.cs`: immutable feature
  profile.
- `src/SharpVision.Terminal/Capabilities/Overrides.cs`: explicit nullable caller
  overrides.
- `src/SharpVision.Terminal/Capabilities/Detector.cs`: conservative environment
  hints and response refinement.
- `src/SharpVision.Terminal/Capabilities/QueryTracker.cs`: correlation,
  duplicate handling, and fake-clock-testable deadlines.

### Tests and specifications

- `tests/SharpVision.Terminal.Tests/Protocols/*Tests.cs`: exact-byte, parameter,
  parser, fragmentation, recovery, and allocation tests.
- `tests/SharpVision.Terminal.Tests/Clipboard/*Tests.cs`: OSC 52 and Kitty OSC
  5522 byte/state/limit tests.
- `tests/SharpVision.Terminal.Tests/Capabilities/*Tests.cs`: precedence,
  timeout, and safe fallback tests.
- `tests/SharpVision.Terminal.Tests/Support/RecordingSink.cs`: copies borrowed
  parser callbacks into stable test observations.
- `tests/SharpVision.Terminal.Tests/Support/Fragmentation.cs`: whole, every
  two-part split, and byte-at-a-time parser oracle.
- `docs/protocols/{ecma-48,csi,osc,sgr,dec-private-modes,device-attributes,kitty-clipboard,coverage-matrix}.md`:
  normative implemented behavior and coverage.
- `docs/architecture/{capabilities,memory-ownership,error-handling}.md`:
  ownership, degradation, and diagnostic guarantees.

## Public contract locked by this plan

```csharp
namespace SharpVision.Terminal.Protocols;

public sealed record Limits
{
    public static Limits Default { get; } = new();
    public int MaxParameterBytes { get; init; } = 256;
    public int MaxIntermediateBytes { get; init; } = 16;
    public int MaxStringBytes { get; init; } = 1_048_576;
    public int MaxClipboardBytes { get; init; } = 16_777_216;
    public int MaxMetadataBytes { get; init; } = 8_192;
    public int MaxConcurrentQueries { get; init; } = 32;
    public TimeSpan QueryTimeout { get; init; } = TimeSpan.FromMilliseconds(750);
    public bool AcceptBellTerminatedOsc { get; init; } = true;
    public bool AcceptEightBitControls { get; init; }
}

public interface ISequenceSink
{
    void Text(ReadOnlySpan<byte> value);
    void Control(byte value);
    void Escape(ReadOnlySpan<byte> intermediates, byte final);
    void Csi(ReadOnlySpan<byte> parameters, ReadOnlySpan<byte> intermediates, byte final);
    void String(SequenceKind kind, ReadOnlySpan<byte> value, StringTerminator terminator);
    void Dcs(ReadOnlySpan<byte> parameters, ReadOnlySpan<byte> intermediates,
        byte final, ReadOnlySpan<byte> value, StringTerminator terminator);
    void Diagnostic(in Diagnostic value);
}

public sealed class Parser : IDisposable
{
    public Parser(Limits? limits = null);
    public long Offset { get; }
    public void Parse<TSink>(ReadOnlySpan<byte> input, ref TSink sink)
        where TSink : ISequenceSink;
    public void Complete<TSink>(ref TSink sink) where TSink : ISequenceSink;
    public void Reset();
    public void Dispose();
}

public readonly struct Writer
{
    public Writer(IBufferWriter<byte> destination);
    public void Escape(ReadOnlySpan<byte> intermediates, byte final);
    public void Csi(ReadOnlySpan<byte> parameters, ReadOnlySpan<byte> intermediates, byte final);
    public void Osc(int selector, ReadOnlySpan<byte> payload);
    public void Command(SequenceKind kind, ReadOnlySpan<byte> payload);
    public void Dcs(ReadOnlySpan<byte> parameters, ReadOnlySpan<byte> intermediates,
        byte final, ReadOnlySpan<byte> payload);
}
```

All span arguments are borrowed only for the synchronous call. Sink spans are
valid only until the callback returns. Public members validate before writing or
changing state; disposed objects throw `ObjectDisposedException`. Numeric text
is ASCII and culture-independent. The canonical emitted string terminator is ST
(`ESC \\`).

## Task 1: Bounds and diagnostics

**Files:**

- Create: `src/SharpVision.Terminal/Protocols/Limits.cs`
- Create: `src/SharpVision.Terminal/Protocols/Diagnostic.cs`
- Create: `src/SharpVision.Terminal/Protocols/SequenceKind.cs`
- Create: `src/SharpVision.Terminal/Protocols/StringTerminator.cs`
- Test: `tests/SharpVision.Terminal.Tests/Protocols/LimitsTests.cs`
- Test: `tests/SharpVision.Terminal.Tests/Protocols/DiagnosticTests.cs`

- [x] **Step 1: Write failing validation and redaction tests**

  Add tests named
  `Constructor_WhenLimitIsNotPositive_ThrowsArgumentOutOfRangeException`,
  `Default_WhenRead_HasFiniteInteractiveBounds`, and
  `ToString_WhenDiagnosticIsSensitive_DoesNotExposePayload`. Assert every
  numeric limit is positive, `QueryTimeout` is finite and positive, and the
  diagnostic string contains code/kind/offset/discard count but no payload.

- [x] **Step 2: Run the focused tests and verify RED**

  Run:

  ```bash
  dotnet test --project tests/SharpVision.Terminal.Tests/SharpVision.Terminal.Tests.csproj --filter-class "*LimitsTests|*DiagnosticTests"
  ```

  Expected: compilation fails because the protocol types do not exist.

- [x] **Step 3: Implement immutable validated values**

  Implement `Limits` as the public record above with a validating primary
  constructor or property initialization path that rejects zero, negatives,
  `Timeout.InfiniteTimeSpan`, and values above `int.MaxValue`. Add
  `DiagnosticCode` values for `Malformed`, `Cancelled`, `Truncated`,
  `ParameterLimit`, `IntermediateLimit`, `StringLimit`, `InvalidBase64`,
  `InvalidMetadata`, `UnexpectedPacket`, `DuplicateResponse`, `LateResponse`,
  `QueryLimit`, and `Unsupported`. `Diagnostic` contains only code, kind,
  offset, and discarded-byte count.

- [x] **Step 4: Run focused tests and verify GREEN**

  Expected: all focused tests pass with zero warnings.

- [x] **Step 5: Commit**

  Commit message: `feat: define bounded protocol contracts`

## Task 2: Byte writer and raw grammar

**Files:**

- Create: `src/SharpVision.Terminal/Protocols/Writer.cs`
- Test: `tests/SharpVision.Terminal.Tests/Protocols/WriterTests.cs`

- [x] **Step 1: Write exact-byte and validation tests**

  Use `ArrayBufferWriter<byte>` and literal expected bytes. Cover `ESC 7`,
  `CSI 12;4 H`, `OSC 2;title ST`, APC, PM, SOS, and DCS. Verify final bytes lie
  in `0x30..0x7e`, intermediates in `0x20..0x2f`, CSI parameters in
  `0x30..0x3f`, payloads reject ESC/C0 terminators, and a failed call writes
  nothing.

- [x] **Step 2: Run focused test and verify RED**

  Run:

  ```bash
  dotnet test --project tests/SharpVision.Terminal.Tests/SharpVision.Terminal.Tests.csproj --filter-class "*WriterTests"
  ```

  Expected: compilation fails because `Writer` does not exist.

- [x] **Step 3: Implement transactional writes**

  Validate the complete command first, calculate its exact length with checked
  arithmetic, request one destination span, write introducer/body/ST, and call
  `Advance` once. Use short private helpers for byte-class checks and
  `Debug.Assert` for the post-validation length invariant.

- [x] **Step 4: Run focused and assembly tests and verify GREEN**

  Run the focused command, then the complete terminal test project.

- [x] **Step 5: Commit**

  Commit message: `feat: add bounded protocol writer`

## Task 3: CSI parameters and typed commands

**Files:**

- Create: `src/SharpVision.Terminal/Protocols/Parameters.cs`
- Create: `src/SharpVision.Terminal/Protocols/Csi.cs`
- Create: `src/SharpVision.Terminal/Protocols/Sgr.cs`
- Create: `src/SharpVision.Terminal/Protocols/Osc.cs`
- Create: `src/SharpVision.Terminal/Protocols/Modes.cs`
- Test: `tests/SharpVision.Terminal.Tests/Protocols/ParametersTests.cs`
- Test: `tests/SharpVision.Terminal.Tests/Protocols/CsiTests.cs`
- Test: `tests/SharpVision.Terminal.Tests/Protocols/SgrTests.cs`
- Test: `tests/SharpVision.Terminal.Tests/Protocols/OscTests.cs`
- Test: `tests/SharpVision.Terminal.Tests/Protocols/ModesTests.cs`

- [x] **Step 1: Write parameter grammar tests**

  Prove empty/default fields, semicolon parameters, colon subparameters, private
  prefixes, max count, max magnitude, and numeric overflow. Parsing returns an
  enum result (`Value`, `Default`, `Invalid`, `Overflow`, `End`) and never
  allocates a string.

- [x] **Step 2: Run parameter tests and verify RED**

  Expected: compilation fails because `Parameters` does not exist.

- [x] **Step 3: Implement allocation-free parameter enumeration**

  Store the raw `ReadOnlySpan<byte>` in a `ref struct`; advance by indexes;
  parse checked decimal digits; preserve `:` boundaries instead of flattening
  subparameters. Validate configured count and magnitude at the typed boundary.

- [x] **Step 4: Run parameter tests and verify GREEN**

- [x] **Step 5: Write exact-byte tests for the typed API**

  Cover cursor up/down/forward/back/position, erase display/line, insert/delete
  character/line, scroll up/down, save/restore cursor, visibility, alternate
  screen, bracketed paste, focus reporting, synchronized output, SGR reset,
  bold/dim/italic/underline/blink/reverse/hidden/strike, indexed/RGB colors,
  title, hyperlink open/close, default color query, DA/DSR, DECRQM, and Kitty
  mode 5522. Assert rejected zero coordinates, invalid RGB/index values,
  forbidden hyperlink controls, and unsupported sequence kinds write nothing.

- [x] **Step 6: Run typed command tests and verify RED**

  Expected: compilation fails because the typed command classes do not exist.

- [x] **Step 7: Implement typed command classes**

  Expose static methods that accept the immutable `Writer` value; use
  `stackalloc` plus `Utf8Formatter.TryFormat` for small numeric payloads. Use
  contextual enums (`EraseArea`, `Color`, `Rendition`, `Movement`) and avoid
  repeated `Terminal` prefixes. XML docs state examples, argument exceptions,
  ownership, and safe fallback behavior.

- [x] **Step 8: Run all typed command tests and verify GREEN**

- [x] **Step 9: Commit**

  Commit message: `feat: add typed control sequence encoders`

## Task 4: Streaming C0, ESC, and CSI parser

**Files:**

- Create: `src/SharpVision.Terminal/Protocols/ISequenceSink.cs`
- Create: `src/SharpVision.Terminal/Protocols/Parser.cs`
- Create: `tests/SharpVision.Terminal.Tests/Support/RecordingSink.cs`
- Create: `tests/SharpVision.Terminal.Tests/Support/Fragmentation.cs`
- Test: `tests/SharpVision.Terminal.Tests/Protocols/ParserControlTests.cs`
- Test: `tests/SharpVision.Terminal.Tests/Protocols/ParserCsiTests.cs`

- [ ] **Step 1: Write whole-input parser tests**

  Record UTF-8 text bytes, C0 controls, two-character ESC functions,
  intermediate ESC functions, empty CSI, private CSI, colon subparameters,
  multiple adjacent events, CAN/SUB cancellation, and eight-bit CSI with the
  option disabled/enabled. Assert UTF-8 continuation byte `0x9b` remains text by
  default.

- [ ] **Step 2: Run parser tests and verify RED**

  Expected: compilation fails because `Parser` and `ISequenceSink` do not exist.

- [ ] **Step 3: Implement ground, ESC, and CSI states**

  Parse by ECMA-48 byte classes. Keep one rented parameter buffer bounded by
  `MaxParameterBytes` and a fixed/rented intermediate buffer bounded by
  `MaxIntermediateBytes`. On overflow enter the matching ignore state, count
  discarded bytes, emit one diagnostic at recovery, and do not retain caller
  memory. Text callbacks may borrow directly from the current input span.

- [ ] **Step 4: Run whole-input tests and verify GREEN**

- [ ] **Step 5: Add fragmentation and recovery tests**

  For every representative sequence compare observations from whole input, every
  two-part split, byte-at-a-time input, and adjacent known text. Cover split
  introducers, excess parameters/intermediates, invalid bytes, cancellation,
  `Complete` while truncated, `Reset`, disposal, and a known CSI following
  malformed input.

- [ ] **Step 6: Run fragmentation tests and verify RED where behavior is
      absent**

- [ ] **Step 7: Complete state preservation, recovery, and lifecycle**

  Preserve only copied bounded state across calls. `Complete` emits exactly one
  truncation diagnostic for an incomplete sequence and returns to ground.
  `Reset` clears state without emitting. Return pooled arrays once and assert
  ownership in debug builds.

- [ ] **Step 8: Run parser tests and verify GREEN**

- [ ] **Step 9: Commit**

  Commit message: `feat: parse streaming escape and CSI sequences`

## Task 5: Bounded OSC, DCS, APC, PM, and SOS parsing

**Files:**

- Modify: `src/SharpVision.Terminal/Protocols/Parser.cs`
- Test: `tests/SharpVision.Terminal.Tests/Protocols/ParserStringTests.cs`
- Test: `tests/SharpVision.Terminal.Tests/Protocols/ParserDcsTests.cs`

- [ ] **Step 1: Write string-family tests**

  Cover OSC terminated by ST and permitted BEL, DCS with parameters,
  intermediates and final byte, APC, PM, SOS, split `ESC \\`, ESC followed by a
  non-ST byte inside a string, CAN/SUB abort, adjacent strings, and ST across
  reads. Assert BEL terminates only OSC and only when enabled.

- [ ] **Step 2: Run focused tests and verify RED**

- [ ] **Step 3: Implement bounded string and DCS states**

  Rent a payload buffer lazily. Grow only up to `MaxStringBytes`; on overflow
  clear sensitive data, enter string-ignore, and scan without further growth
  until ST (or permitted OSC BEL). Preserve a pending ESC at a read boundary; if
  its next byte is not `\\`, append both bytes if within bounds.

- [ ] **Step 4: Run focused tests and verify GREEN**

- [ ] **Step 5: Add hostile-input and allocation tests**

  Feed a multi-megabyte unterminated OSC with a 1 KiB configured limit and
  assert bounded retained capacity, one redacted diagnostic, recovery into text,
  and no payload in `Diagnostic.ToString()`. Warm the parser, parse 10,000 short
  CSI sequences, and assert zero thread allocations for the parse loop and a
  struct sink.

- [ ] **Step 6: Run hostile/allocation tests and verify RED then GREEN**

  Fix only measured allocation and bound failures; do not weaken assertions.

- [ ] **Step 7: Commit**

  Commit message: `feat: parse bounded terminal strings`

## Task 6: OSC 52 clipboard

**Files:**

- Create: `src/SharpVision.Terminal/Clipboard/Selection.cs`
- Create: `src/SharpVision.Terminal/Clipboard/Osc52.cs`
- Test: `tests/SharpVision.Terminal.Tests/Clipboard/Osc52Tests.cs`

- [ ] **Step 1: Write exact-byte and decode tests**

  Cover clipboard, primary, selection, query (`?`), empty text, UTF-8 text,
  Base64 padding, configured maximum, ST/BEL replies, invalid selector,
  forbidden controls, invalid Base64, oversize decoded data, and recovery. Use
  literal bytes and `Convert.ToBase64String` only for randomized oracle data,
  never the production writer.

- [ ] **Step 2: Run focused tests and verify RED**

- [ ] **Step 3: Implement OSC 52 text protocol**

  Encode UTF-8 into pooled bytes, validate decoded length before publishing,
  clear sensitive buffers before return, and expose a result union with
  `Success`, `Unavailable`, `Denied`, and `Malformed`. Diagnostics report only
  lengths and codes.

- [ ] **Step 4: Run focused tests and verify GREEN**

- [ ] **Step 5: Commit**

  Commit message: `feat: add OSC 52 clipboard support`

## Task 7: Kitty OSC 5522 packet grammar and encoder

**Files:**

- Create: `src/SharpVision.Terminal/Clipboard/KittyPacket.cs`
- Create: `src/SharpVision.Terminal/Clipboard/KittyWriter.cs`
- Test: `tests/SharpVision.Terminal.Tests/Clipboard/KittyPacketTests.cs`
- Test: `tests/SharpVision.Terminal.Tests/Clipboard/KittyWriterTests.cs`

- [ ] **Step 1: Write metadata parser tests**

  Parse colon-separated `key=value` fields for `type`, `status`, `mime`, `loc`,
  `id`, `pw`, and `name`. Cover unknown keys preserved as observations,
  duplicate required keys rejected, invalid Base64, invalid UTF-8 text fields,
  metadata limit, and IDs restricted to `[A-Za-z0-9-_+.]`. Assert password and
  payload bytes never appear in diagnostics or `ToString()`.

- [ ] **Step 2: Run packet tests and verify RED**

- [ ] **Step 3: Implement packet parsing**

  Split the selector/body without allocating substrings, validate ASCII keys,
  decode Base64 to owned bounded memory only when the caller requests it, and
  represent statuses `OK`, `DATA`, `DONE`, `EIO`, `EINVAL`, `ENOSYS`, `EPERM`,
  and `EBUSY` as enums. Preserve the optional sanitized correlation ID.

- [ ] **Step 4: Run packet tests and verify GREEN**

- [ ] **Step 5: Write exact-byte encoder and chunk tests**

  Cover read, MIME-list (`.`), primary selection, password/name, write start,
  write end, `wdata`, `walias`, and paste mode/query. For MIME data sizes 0, 1,
  4095, 4096, 4097, 8192, and randomized binary input assert each raw chunk is
  at most 4096 bytes, every chunk has independent required Base64 padding, MIME
  chunks are contiguous, and the final packet is exactly
  `OSC 5522;type=wdata ST`.

- [ ] **Step 6: Run encoder tests and verify RED**

- [ ] **Step 7: Implement canonical OSC 5522 encoding**

  Emit ST only. Encode metadata values exactly once. Use 4096-byte raw slices,
  `Base64.EncodeToUtf8`, and bounded stack/rented scratch storage. Validate all
  inputs before the first output write so invalid requests are atomic.

- [ ] **Step 8: Run encoder tests and verify GREEN**

- [ ] **Step 9: Commit**

  Commit message: `feat: encode Kitty clipboard packets`

## Task 8: Kitty clipboard transactions

**Files:**

- Create: `src/SharpVision.Terminal/Clipboard/KittyTransaction.cs`
- Test: `tests/SharpVision.Terminal.Tests/Clipboard/KittyTransactionTests.cs`
- Test:
  `tests/SharpVision.Terminal.Tests/Clipboard/ClipboardIntegrationTests.cs`

- [ ] **Step 1: Write transaction state tests**

  Cover read `OK -> DATA* -> DONE`, list response, write `DONE`, every error
  status, same-MIME contiguous chunks, MIME transition, duplicate `OK`, data
  before `OK`, data after `DONE`, mismatched/missing ID, invalid Base64, size
  limit, cancellation, timeout, and ignored late response. Use a manual
  `TimeProvider` test double; do not wait on wall-clock time.

- [ ] **Step 2: Run transaction tests and verify RED**

- [ ] **Step 3: Implement bounded correlated state machines**

  Make states explicit (`Created`, `Accepted`, `Receiving`, `Completed`,
  `Failed`, `Cancelled`, `TimedOut`). Validate state before mutation. Accumulate
  each MIME value in owned pooled memory capped by `MaxClipboardBytes`; clear
  sensitive storage on all terminal states and disposal. An invalid packet fails
  only its transaction and never the outer parser.

- [ ] **Step 4: Run transaction tests and verify GREEN**

- [ ] **Step 5: Write end-to-end packet tests**

  Traverse typed request -> `KittyWriter` -> bytes -> every parser split ->
  `KittyPacket` -> transaction result. Include arbitrary binary MIME data,
  aliases, permission denied, multiplexer correlation, and a malformed packet
  followed by a valid transaction.

- [ ] **Step 6: Run integration tests and verify RED then GREEN**

- [ ] **Step 7: Commit**

  Commit message: `feat: add Kitty clipboard transactions`

## Task 9: Immutable capability profiles and bounded queries

**Files:**

- Create: `src/SharpVision.Terminal/Capabilities/Capabilities.cs`
- Create: `src/SharpVision.Terminal/Capabilities/Overrides.cs`
- Create: `src/SharpVision.Terminal/Capabilities/Detector.cs`
- Create: `src/SharpVision.Terminal/Capabilities/QueryTracker.cs`
- Create: `src/SharpVision.Terminal/Protocols/Responses.cs`
- Test: `tests/SharpVision.Terminal.Tests/Capabilities/CapabilitiesTests.cs`
- Test: `tests/SharpVision.Terminal.Tests/Capabilities/DetectorTests.cs`
- Test: `tests/SharpVision.Terminal.Tests/Capabilities/QueryTrackerTests.cs`
- Test: `tests/SharpVision.Terminal.Tests/Protocols/ResponsesTests.cs`

- [ ] **Step 1: Write immutable profile and precedence tests**

  Assert defaults enable no optional feature. Cover TERM/COLORTERM/kitty/iTerm
  hints as tentative only, SSH/tmux/screen narrowing, valid query refinement,
  malformed/contradictory replies staying conservative, and explicit overrides
  winning last. Assert an existing profile never mutates when a new one is
  produced.

- [ ] **Step 2: Run capability tests and verify RED**

- [ ] **Step 3: Implement profiles and detector**

  Model `Support` as `Unknown`, `Unsupported`, `Tentative`, or `Supported` plus
  an origin enum. Include color depth, synchronized output, focus, bracketed
  paste, pixel mouse, Kitty keyboard, OSC 52, Kitty clipboard, and extension
  hooks for graphics/sixel/iTerm images. Environment inputs are an immutable
  dictionary supplied by the caller, never read globally inside detection.

- [ ] **Step 4: Run capability tests and verify GREEN**

- [ ] **Step 5: Write query tracker tests**

  Cover one active uncorrelated query per response family, unique Kitty ID
  registration, maximum total concurrency, matched response, duplicate response
  diagnostic, timeout using `TimeProvider`, cancellation, late reply diagnostic,
  and Kitty 5522 DECRPM values 0/4 mapping to unsupported. Cover typed DA1/DA2,
  cursor-position DSR, DECRPM, OSC default-color, and malformed replies without
  throwing.

- [ ] **Step 6: Run query tests and verify RED**

- [ ] **Step 7: Implement bounded query tracking**

  Store at most `MaxConcurrentQueries`; calculate deadlines from the injected
  `TimeProvider`; permit only one uncorrelated in-flight query for each response
  family; match Kitty clipboard replies by sanitized ID; remove
  completed/cancelled/timed-out entries; return typed outcomes rather than
  throwing for absent or malformed terminal replies. `Responses` validates raw
  parser callbacks and produces typed DA, DSR, DECRPM, and OSC color values.

- [ ] **Step 8: Run all capability tests and verify GREEN**

- [ ] **Step 9: Commit**

  Commit message: `feat: add conservative terminal capabilities`

## Task 10: Randomized parser invariants and protocol documentation

**Files:**

- Test: `tests/SharpVision.Terminal.Tests/Protocols/ParserRandomizedTests.cs`
- Modify: `docs/protocols/ecma-48.md`
- Modify: `docs/protocols/csi.md`
- Modify: `docs/protocols/osc.md`
- Modify: `docs/protocols/sgr.md`
- Modify: `docs/protocols/dec-private-modes.md`
- Modify: `docs/protocols/device-attributes.md`
- Modify: `docs/protocols/kitty-clipboard.md`
- Modify: `docs/protocols/coverage-matrix.md`
- Modify: `docs/architecture/capabilities.md`
- Modify: `docs/architecture/memory-ownership.md`
- Modify: `docs/architecture/error-handling.md`

- [ ] **Step 1: Write deterministic randomized invariants**

  With fixed seeds, generate valid CSI/OSC/DCS sequences and hostile arbitrary
  bytes. Assert whole input equals every fragmentation for complete valid
  sequences, parser retained capacity stays within configured limits, no call
  hangs, `Complete` returns to ground, and a known trailing CSI is observed
  after every malformed prefix. Print the seed on failure.

- [ ] **Step 2: Run randomized tests and verify RED for uncovered cases**

- [ ] **Step 3: Fix parser invariants without loosening bounds**

  Minimize any failing seed into a named regression test before changing
  production code. Preserve the randomized case after the named test passes.

- [ ] **Step 4: Update normative docs and coverage states**

  Document exact public types, defaults, byte grammar, accepted terminators,
  eight-bit C1 policy, diagnostic recovery, memory lifetimes, query precedence,
  clipboard fallback, implemented typed commands, and known extension-only
  families. Change only genuinely implemented coverage rows to “typed” or
  “observed”; keep deferred graphics/image rows explicit.

- [ ] **Step 5: Run documentation validation**

  Run: `make lint`

  Expected: .NET analyzers, Prettier, Markdownlint, skill validation, and local
  file/section link checks all pass.

- [ ] **Step 6: Commit**

  Commit message: `docs: publish Phase 2 protocol guarantees`

## Task 11: Phase 2 verification and audit

**Files:**

- Modify only files required by verified failures.

- [ ] **Step 1: Run formatting**

  Run: `make format`

  Expected: command exits 0 and leaves no unintended formatting diff.

- [ ] **Step 2: Run lint**

  Run: `make lint`

  Expected: command exits 0 with no analyzer, Markdown, Prettier, skill, or link
  errors.

- [ ] **Step 3: Run release build**

  Run: `make build`

  Expected: all projects build in Release with 0 warnings and 0 errors.

- [ ] **Step 4: Run all tests**

  Run: `make test`

  Expected: every discovered test passes; the output reports non-zero test
  counts for all three test projects.

- [ ] **Step 5: Check repository hygiene and public docs**

  Run:

  ```bash
  git diff --check
  git status --short
  rg -n "TODO|TBD|NotImplementedException" src tests docs
  ```

  Expected: no whitespace errors, only intended Phase 2 changes before the final
  commit, and no placeholders in implemented Phase 2 scope.

- [ ] **Step 6: Commit the verified phase**

  Commit message: `chore: complete terminal protocol engine`

## Self-review record

- **Spec coverage:** The plan covers bounded ECMA-48 parsing, typed CSI/OSC/SGR
  encoding, lifecycle modes, typed query responses, OSC 52, full first-milestone
  Kitty OSC 5522 packet/state behavior, immutable capabilities, query
  timeout/correlation, conservative fallback, fragmentation, hostile input,
  allocation, randomized invariants, docs, and repository gates. Unicode, input
  event decoding, transport, rendering, UI, controls, and showcase remain
  assigned to Phases 3-6 by the approved roadmap.
- **Placeholder scan:** The plan contains no implementation placeholder. The
  final hygiene command deliberately searches for the literal placeholder
  markers in the repository.
- **Type consistency:** Parser, sink, writer, clipboard, diagnostic, limits, and
  capability names above are the canonical Phase 2 names used by every task.
