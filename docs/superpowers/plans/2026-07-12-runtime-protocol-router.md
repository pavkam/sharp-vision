# Runtime Protocol Router Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use
> superpowers:subagent-driven-development (recommended) or
> superpowers:executing-plans to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking.

**Goal:** Preserve and route typed terminal replies and bounded OSC/DCS/string
events through the normal runtime without breaking the existing input decoder.

**Architecture:** Add an owned protocol-sequence value and a protocol sink that
extends the existing input sink. `ProtocolRouter` is the full-stream entry point
and uses the proven incremental `Decoder`; the decoder recognizes query replies
at the parser callback boundary and routes other bounded terminal strings as
owned values. `Session` requires the full sink, while `Application` publishes
typed replies on its dispatcher and converts unregistered raw extensions into
redacted diagnostics.

**Tech Stack:** .NET 10, C# 14, xUnit v3, Shouldly, Microsoft Testing Platform,
ECMA-48 parser callbacks, `ReadOnlySpan<byte>`, and owned
`ReadOnlyMemory<byte>`.

---

## Delivery boundary

This plan implements the protocol-router half of vertical slice 1 in the
approved
[terminal expansion design](../specs/2026-07-12-terminal-protocol-and-cell-geometry-expansion-design.md#vertical-slice-1-protocol-routing-and-capability-negotiation).
Capability negotiation is the next independent plan because it adds startup
query ordering, deadlines, profile publication, and mode activation on top of
the routed `Response` stream.

Completion requires:

- DA, DSR, DECRPM, Kitty keyboard status, and OSC color replies reach a typed
  runtime sink;
- OSC, DCS, APC, PM, and SOS reach a bounded owned callback;
- legacy `Decoder` consumers receive an `Unsupported` diagnostic instead of
  silent loss;
- `Session` uses `ProtocolRouter` and preserves transport order;
- `Application` raises typed response events on its dispatcher;
- unregistered raw extension payloads are redacted before application queuing;
- representative replies and string families pass every read-fragment boundary;
- malformed and oversized strings recover into known following input;
- normative docs and coverage claims match the implementation.

## File map

### Create

- `src/SharpVision.Terminal/Protocols/IProtocolSink.cs` — full synchronous
  terminal event contract.
- `src/SharpVision.Terminal/Protocols/ProtocolSequence.cs` — immutable owned
  OSC/DCS/APC/PM/SOS value.
- `src/SharpVision.Terminal/Protocols/ProtocolRouter.cs` — public full-stream
  facade.
- `tests/SharpVision.Terminal.Tests/Support/RecordingProtocolSink.cs` —
  deterministic routed-event sink.
- `tests/SharpVision.Terminal.Tests/Protocols/RouterTests.cs` — replies,
  strings, ownership, fragmentation, and recovery.
- `src/SharpVision/Runtime/ProtocolResponseEventArgs.cs` — dispatcher event
  payload.
- `tests/SharpVision.Tests/Runtime/ProtocolRoutingTests.cs` — application-path
  proof.
- `docs/protocols/runtime-routing.md` — normative routing contract.

### Modify

- `src/SharpVision.Terminal/Input/Adapter.cs`
- `src/SharpVision.Terminal/Input/Decoder.cs`
- `src/SharpVision.Terminal/Runtime/ISink.cs`
- `src/SharpVision.Terminal/Runtime/Session.cs`
- `src/SharpVision/Runtime/Application.cs`
- `src/SharpVision/Runtime/Record.cs`
- `src/SharpVision/Runtime/RecordKind.cs`
- `tests/SharpVision.Terminal.Tests/Runtime/RuntimeSink.cs`
- `tests/SharpVision.Terminal.Tests/Transport/RuntimeSink.cs`
- `tests/SharpVision.Terminal.Tests/Runtime/SessionTests.cs`
- `docs/index.md`
- `docs/protocols/index.md`
- `docs/protocols/coverage-matrix.md`
- `docs/protocols/dcs-strings.md`
- `docs/protocols/device-attributes.md`
- `docs/architecture/runtime-event-loop.md`
- `docs/testing/terminal-protocols.md`

## Task 1: Specify runtime routing before code

**Files:**

- Create: `docs/protocols/runtime-routing.md`
- Modify: `docs/index.md`
- Modify: `docs/protocols/index.md`
- Modify: `docs/protocols/dcs-strings.md`
- Modify: `docs/protocols/device-attributes.md`
- Modify: `docs/architecture/runtime-event-loop.md`

- [ ] **Step 1: Write the normative contract**

Create `runtime-routing.md` with this contract:

```markdown
# Runtime protocol routing

## Runtime routing contract

`Parser` owns bounded ECMA-48 framing. `ProtocolRouter` owns the decision
between typed input, typed terminal responses, and bounded raw extension
strings. Parser callback spans are borrowed; every `ProtocolSequence` copies its
header and payload before the callback returns.

`IProtocolSink.Response` receives recognized DA, DSR, DECRPM, Kitty keyboard,
and OSC color replies. `IProtocolSink.Sequence` receives completed OSC, DCS,
APC, PM, and SOS values without a registered typed consumer. A recognized
response is never emitted again as input or a raw sequence.

## Ordering and ownership

Callbacks are synchronous and remain in transport order. `Session` invokes one
sink callback at a time. The application may enqueue immutable numeric
responses; it redacts unregistered string payloads before queuing diagnostics.

## Recovery and fallback

Malformed, interrupted, truncated, and oversized sequences retain parser
recovery. A legacy `Decoder` sink without `IProtocolSink` receives
`DiagnosticCode.Unsupported` for a valid reply or string instead of silently
losing it.

## Test obligations

Test each recognized reply and each string family whole and at every split.
Mutate the source after routing to prove ownership. Follow hostile strings with
known input to prove recovery.
```

- [ ] **Step 2: Link the normative owner**

Add this protocol-index entry and link it from the affected architecture and
protocol sections:

```markdown
- [Runtime protocol routing](runtime-routing.md#runtime-routing-contract)
  defines typed dispatch, owned extension values, and runtime fallback.
```

- [ ] **Step 3: Validate the documents**

Run:

```bash
npx prettier --write docs/protocols/runtime-routing.md docs/index.md docs/protocols/index.md docs/protocols/dcs-strings.md docs/protocols/device-attributes.md docs/architecture/runtime-event-loop.md
npx markdownlint-cli2 docs/protocols/runtime-routing.md docs/index.md docs/protocols/index.md docs/protocols/dcs-strings.md docs/protocols/device-attributes.md docs/architecture/runtime-event-loop.md
npm run lint:links
```

Expected: zero Markdown and local-link errors.

- [ ] **Step 4: Commit the contract**

```bash
git add docs/protocols/runtime-routing.md docs/index.md docs/protocols/index.md docs/protocols/dcs-strings.md docs/protocols/device-attributes.md docs/architecture/runtime-event-loop.md
git commit -m "docs(protocols): specify runtime protocol routing"
```

Expected: only these documentation paths are committed.

## Task 2: Define the owned value and sink API

**Files:**

- Create: `src/SharpVision.Terminal/Protocols/IProtocolSink.cs`
- Create: `src/SharpVision.Terminal/Protocols/ProtocolSequence.cs`
- Create: `tests/SharpVision.Terminal.Tests/Support/RecordingProtocolSink.cs`
- Create: `tests/SharpVision.Terminal.Tests/Protocols/RouterTests.cs`

- [ ] **Step 1: Write the failing ownership test**

```csharp
[Fact]
public void Route_WhenDcsCompletes_OwnsHeaderAndPayload()
{
    // Arrange
    var sink = new RecordingProtocolSink();
    using var router = new ProtocolRouter(sink);
    var input = "\u001bP1;2$qpayload\u001b\\"u8.ToArray();

    // Act
    router.Route(input);
    input.AsSpan().Fill((byte) 'x');

    // Assert
    var sequence = sink.Sequences.ShouldHaveSingleItem();
    sequence.Kind.ShouldBe(SequenceKind.Dcs);
    sequence.Parameters.Span.SequenceEqual("1;2"u8).ShouldBeTrue();
    sequence.Intermediates.Span.SequenceEqual("$"u8).ShouldBeTrue();
    sequence.Final.ShouldBe((byte) 'q');
    sequence.Payload.Span.SequenceEqual("payload"u8).ShouldBeTrue();
    sequence.Terminator.ShouldBe(StringTerminator.EscapeBackslash);
}
```

- [ ] **Step 2: Witness the expected failure**

Run:

```bash
dotnet test --project tests/SharpVision.Terminal.Tests --filter-class "*RouterTests" --timeout 60s
```

Expected: build failure naming `ProtocolRouter`, `IProtocolSink`, and
`ProtocolSequence`.

- [ ] **Step 3: Add `IProtocolSink`**

```csharp
using SharpVision.Terminal.Input;

namespace SharpVision.Terminal.Protocols;

/// <summary>Receives typed input, terminal replies, and owned extension strings.</summary>
public interface IProtocolSink: IInputSink
{
    /// <summary>Receives one recognized immutable terminal response.</summary>
    /// <param name="value">The owned numeric response.</param>
    public void Response(in Response value);

    /// <summary>Receives one completed owned terminal string.</summary>
    /// <param name="value">The non-null copied sequence.</param>
    public void Sequence(ProtocolSequence value);
}
```

- [ ] **Step 4: Add `ProtocolSequence`**

```csharp
namespace SharpVision.Terminal.Protocols;

/// <summary>Owns one completed OSC, DCS, APC, PM, or SOS sequence.</summary>
public sealed class ProtocolSequence
{
    /// <summary>Initializes an owned copy after parser validation.</summary>
    /// <param name="kind">The terminal string family.</param>
    /// <param name="parameters">DCS parameters, otherwise empty.</param>
    /// <param name="intermediates">DCS intermediates, otherwise empty.</param>
    /// <param name="final">The DCS final, otherwise zero.</param>
    /// <param name="payload">The bounded payload.</param>
    /// <param name="terminator">The observed terminator.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="kind"/> is not a terminal string family.
    /// </exception>
    internal ProtocolSequence(
        SequenceKind kind,
        ReadOnlySpan<byte> parameters,
        ReadOnlySpan<byte> intermediates,
        byte final,
        ReadOnlySpan<byte> payload,
        StringTerminator terminator)
    {
        if (kind is not SequenceKind.Osc and not SequenceKind.Dcs and not SequenceKind.Apc and not SequenceKind.Pm and not SequenceKind.Sos)
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "The value is not a terminal string family.");
        }

        Kind = kind;
        Parameters = parameters.ToArray();
        Intermediates = intermediates.ToArray();
        Final = final;
        Payload = payload.ToArray();
        Terminator = terminator;
    }

    /// <summary>Gets the terminal string family.</summary>
    public SequenceKind Kind { get; }

    /// <summary>Gets owned DCS parameter bytes, or empty memory.</summary>
    public ReadOnlyMemory<byte> Parameters { get; }

    /// <summary>Gets owned DCS intermediate bytes, or empty memory.</summary>
    public ReadOnlyMemory<byte> Intermediates { get; }

    /// <summary>Gets the DCS final byte, or zero for another string family.</summary>
    public byte Final { get; }

    /// <summary>Gets the owned bounded payload.</summary>
    public ReadOnlyMemory<byte> Payload { get; }

    /// <summary>Gets the observed terminator.</summary>
    public StringTerminator Terminator { get; }
}
```

- [ ] **Step 5: Add `RecordingProtocolSink`**

Implement every `IInputSink` callback and these full-protocol members. Each
callback appends its stable family name to `Order`.

```csharp
internal List<Response> Responses { get; } = [];
internal List<ProtocolSequence> Sequences { get; } = [];
internal List<Diagnostic> Diagnostics { get; } = [];
internal List<Text> Text { get; } = [];
internal List<string> Order { get; } = [];

public void Response(in Response value)
{
    Responses.Add(value);
    Order.Add("response");
}

public void Sequence(ProtocolSequence value)
{
    ArgumentNullException.ThrowIfNull(value);
    Sequences.Add(value);
    Order.Add("sequence");
}
```

Expected: the owned types compile; the test still waits for `ProtocolRouter`.

## Task 3: Route replies and strings at the decoder boundary

**Files:**

- Create: `src/SharpVision.Terminal/Protocols/ProtocolRouter.cs`
- Modify: `src/SharpVision.Terminal/Input/Adapter.cs`
- Modify: `src/SharpVision.Terminal/Input/Decoder.cs`
- Modify: `tests/SharpVision.Terminal.Tests/Protocols/RouterTests.cs`

- [ ] **Step 1: Add failing typed-reply tests**

```csharp
[Theory]
[InlineData("\u001b[?1;2c", ResponseKind.PrimaryAttributes)]
[InlineData("\u001b[>41;410;0c", ResponseKind.SecondaryAttributes)]
[InlineData("\u001b[12;34R", ResponseKind.CursorPosition)]
[InlineData("\u001b[?2026;1$y", ResponseKind.PrivateMode)]
[InlineData("\u001b[?3u", ResponseKind.Keyboard)]
[InlineData("\u001b]10;rgb:ffff/0000/8080\u001b\\", ResponseKind.ForegroundColor)]
public void Route_WhenReplyIsRecognized_DeliversTypedResponse(string input, ResponseKind expected)
{
    // Arrange
    var sink = new RecordingProtocolSink();
    using var router = new ProtocolRouter(sink);

    // Act
    router.Route(System.Text.Encoding.UTF8.GetBytes(input));

    // Assert
    sink.Responses.ShouldHaveSingleItem().Kind.ShouldBe(expected);
    sink.Sequences.ShouldBeEmpty();
}
```

Add `Route_WhenReplyIsSplit_DeliversOnce` with a loop from zero through the
length of `ESC [ ? 2026 ; 1 $ y`. Create a fresh router per split, route the two
slices in order, and assert one response and no text. Also route the sequence
byte by byte in a fresh router.

- [ ] **Step 2: Add the legacy fallback test**

```csharp
[Fact]
public void Decode_WhenSinkHandlesOnlyInput_ReportsUnsupportedReply()
{
    // Arrange
    var sink = new RecordingInputSink();
    using var decoder = new SharpVision.Terminal.Input.Decoder(sink);

    // Act
    decoder.Decode("\u001b[?1;2c"u8);

    // Assert
    var diagnostic = sink.Diagnostics.ShouldHaveSingleItem();
    diagnostic.Code.ShouldBe(DiagnosticCode.Unsupported);
    diagnostic.Kind.ShouldBe(SequenceKind.Csi);
}
```

Run the focused test and expect failures because replies and strings are still
swallowed.

- [ ] **Step 3: Preserve parser callback data in `Adapter`**

```csharp
public void Sequence(SequenceKind kind, ReadOnlySpan<byte> value, StringTerminator terminator) =>
    _owner.AcceptSequence(kind, value, terminator);

public void Dcs(
    ReadOnlySpan<byte> parameters,
    ReadOnlySpan<byte> intermediates,
    byte final,
    ReadOnlySpan<byte> value,
    StringTerminator terminator) =>
    _owner.AcceptDcs(parameters, intermediates, final, value, terminator);
```

- [ ] **Step 4: Route typed CSI before key interpretation**

Add `private readonly IProtocolSink? _protocolSink;`, assign
`_protocolSink = sink as IProtocolSink;`, and put this after pending
UTF-8/legacy cleanup at the start of `HandleCsi`:

```csharp
if (Responses.TryCsi(parameters, intermediates, final, out var response))
{
    RouteResponse(in response, SequenceKind.Csi);
    return;
}
```

Remove the existing `Responses.TryCsi(..., out _)` branch from Kitty input
handling. Add:

```csharp
private void RouteResponse(in Response value, SequenceKind kind)
{
    if (_protocolSink is not null)
    {
        _protocolSink.Response(in value);
        return;
    }

    Report(DiagnosticCode.Unsupported, kind);
}
```

- [ ] **Step 5: Route OSC replies and raw strings**

```csharp
private void HandleSequence(SequenceKind kind, ReadOnlySpan<byte> payload, StringTerminator terminator)
{
    FlushUtf8();
    EndX10IfPending();
    EndSs3IfPending();

    if (kind == SequenceKind.Osc && Responses.TryOsc(payload, out var response))
    {
        RouteResponse(in response, kind);
        return;
    }

    if (_protocolSink is null)
    {
        Report(DiagnosticCode.Unsupported, kind);
        return;
    }

    _protocolSink.Sequence(new ProtocolSequence(kind, [], [], 0, payload, terminator));
}

private void HandleDcs(
    ReadOnlySpan<byte> parameters,
    ReadOnlySpan<byte> intermediates,
    byte final,
    ReadOnlySpan<byte> payload,
    StringTerminator terminator)
{
    FlushUtf8();
    EndX10IfPending();
    EndSs3IfPending();

    if (_protocolSink is null)
    {
        Report(DiagnosticCode.Unsupported, SequenceKind.Dcs);
        return;
    }

    _protocolSink.Sequence(new ProtocolSequence(SequenceKind.Dcs, parameters, intermediates, final, payload, terminator));
}
```

Update `AcceptSequence` and `AcceptDcs` to pass every borrowed field into these
handlers.

- [ ] **Step 6: Add the public full-stream facade**

```csharp
using SharpVision.Terminal.Input;

namespace SharpVision.Terminal.Protocols;

/// <summary>Routes one terminal byte stream into typed input and protocol events.</summary>
public sealed class ProtocolRouter: IDisposable
{
    private readonly Decoder _decoder;

    /// <summary>Initializes a router with bounded decoder policy.</summary>
    /// <param name="sink">The non-null synchronous protocol sink.</param>
    /// <param name="options">Finite input policy, or null for defaults.</param>
    /// <param name="timeProvider">The Escape deadline clock, or null.</param>
    /// <exception cref="ArgumentNullException"><paramref name="sink"/> is null.</exception>
    public ProtocolRouter(IProtocolSink sink, Options? options = null, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(sink);
        _decoder = new Decoder(sink, options, timeProvider);
    }

    /// <summary>Routes one borrowed transport fragment synchronously.</summary>
    /// <param name="input">The borrowed transport bytes.</param>
    public void Route(ReadOnlySpan<byte> input) => _decoder.Decode(input);

    /// <summary>Expires a pending lone Escape.</summary>
    public bool ExpireEscape() => _decoder.ExpireEscape();

    /// <summary>Completes pending input and framing.</summary>
    public void Complete() => _decoder.Complete();

    /// <summary>Releases parser and decoder storage.</summary>
    public void Dispose() => _decoder.Dispose();

    /// <summary>Updates ordered pixel-to-cell inference.</summary>
    /// <param name="value">Positive cell metrics, or null.</param>
    internal void SetCellMetrics(Geometry.Metrics? value) => _decoder.SetCellMetrics(value);
}
```

- [ ] **Step 7: Run router and regression tests**

```bash
dotnet test --project tests/SharpVision.Terminal.Tests --filter-class "*RouterTests" "*LegacyKeyTests" "*KittyKeyboardTests" "*PasteTests" "*MouseTests" --timeout 60s
```

Expected: all selected tests pass and only recognized replies are reclassified.

- [ ] **Step 8: Commit the low-level router**

```bash
git add src/SharpVision.Terminal/Protocols/IProtocolSink.cs src/SharpVision.Terminal/Protocols/ProtocolSequence.cs src/SharpVision.Terminal/Protocols/ProtocolRouter.cs src/SharpVision.Terminal/Input/Adapter.cs src/SharpVision.Terminal/Input/Decoder.cs tests/SharpVision.Terminal.Tests/Support/RecordingProtocolSink.cs tests/SharpVision.Terminal.Tests/Protocols/RouterTests.cs
git commit -m "feat(protocols): route replies and terminal strings"
```

## Task 4: Prove fragmentation and bounded recovery

**Files:**

- Modify: `tests/SharpVision.Terminal.Tests/Protocols/RouterTests.cs`
- Modify: `docs/testing/terminal-protocols.md`

- [ ] **Step 1: Add representative string cases**

```csharp
public static TheoryData<byte[], SequenceKind> StringCases => new()
{
    { "\u001b]777;payload\u001b\\"u8.ToArray(), SequenceKind.Osc },
    { "\u001bP1;2$qpayload\u001b\\"u8.ToArray(), SequenceKind.Dcs },
    { "\u001b_Gpayload\u001b\\"u8.ToArray(), SequenceKind.Apc },
    { "\u001b^payload\u001b\\"u8.ToArray(), SequenceKind.Pm },
    { "\u001bXpayload\u001b\\"u8.ToArray(), SequenceKind.Sos },
};
```

Route every case whole and through every two-fragment split. Assert the same
family, header, payload, terminator, and callback count. Add a BEL-terminated
OSC row.

- [ ] **Step 2: Add hostile recovery tests**

With `Limits.Default with { MaxStringBytes = 8 }`, route oversized OSC plus
`known`. Assert:

```csharp
sink.Diagnostics.ShouldContain(value => value.Code == DiagnosticCode.StringLimit && value.Kind == SequenceKind.Osc);
sink.Text.Select(value => value.Value.ToString()).ShouldBe(["k", "n", "o", "w", "n"]);
sink.Sequences.ShouldBeEmpty();
```

Add CAN cancellation, truncation through `Complete`, malformed OSC color, and a
known following key.

- [ ] **Step 3: Run focused proof**

```bash
dotnet test --project tests/SharpVision.Terminal.Tests --filter-class "*RouterTests" "*ParserFragmentationTests" "*ResponsesTests" --timeout 60s
```

Expected: all selected cases pass, including every generated split.

- [ ] **Step 4: Document and commit proof**

Document source-buffer mutation, whole-versus-split equivalence, and
known-trailing-input recovery in `docs/testing/terminal-protocols.md`.

```bash
git add tests/SharpVision.Terminal.Tests/Protocols/RouterTests.cs docs/testing/terminal-protocols.md
git commit -m "test(protocols): prove routed sequence recovery"
```

## Task 5: Replace the runtime decoder with `ProtocolRouter`

**Files:**

- Modify: `src/SharpVision.Terminal/Runtime/ISink.cs`
- Modify: `src/SharpVision.Terminal/Runtime/Session.cs`
- Modify: `tests/SharpVision.Terminal.Tests/Runtime/RuntimeSink.cs`
- Modify: `tests/SharpVision.Terminal.Tests/Transport/RuntimeSink.cs`
- Modify: `tests/SharpVision.Terminal.Tests/Runtime/SessionTests.cs`

- [ ] **Step 1: Write the failing runtime-path test**

```csharp
[Fact]
public async Task RunAsync_WhenReplyPrecedesText_RoutesBothInOrderAsync()
{
    // Arrange
    await using var transport = new SessionTransport();
    await using var resize = new FakeResizeSource();
    var sink = new RuntimeSink();
    transport.Input("\u001b[?1;2cx"u8.ToArray());
    transport.Close();
    await using var session = new Session(transport, resize, sink, RuntimeOptions.Minimal);

    // Act
    await session.RunAsync(TestContext.Current.CancellationToken);

    // Assert
    sink.Responses.ShouldHaveSingleItem().Kind.ShouldBe(ResponseKind.PrimaryAttributes);
    sink.Order.ShouldBe(["response", "text", "closed"]);
}
```

Expected failure: `Runtime.ISink` has no response callback and `Session` still
constructs `Decoder`.

- [ ] **Step 2: Expand the runtime sink**

```csharp
public interface ISink: IProtocolSink
```

Update its summary and all deterministic sink implementations. Each test sink
stores `Responses`, `Sequences`, and `Order`.

- [ ] **Step 3: Use `ProtocolRouter` in `Session`**

Replace the local decoder and corresponding calls:

```csharp
using var router = new ProtocolRouter(_sink, inputOptions, _timeProvider);
router.SetCellMetrics(dimensions.CellMetrics);
router.Route(buffer.AsSpan(0, count));
router.Complete();
```

Keep the current serialized read/resize loop, buffer ownership, closure, and
cleanup unchanged.

- [ ] **Step 4: Run runtime and PTY tests**

```bash
dotnet test --project tests/SharpVision.Terminal.Tests --filter-class "*SessionTests" "*PseudoterminalTests" --timeout 60s
```

Expected: routed reply order passes and existing mode/PTY bytes remain
unchanged.

- [ ] **Step 5: Commit runtime integration**

```bash
git add src/SharpVision.Terminal/Runtime/ISink.cs src/SharpVision.Terminal/Runtime/Session.cs tests/SharpVision.Terminal.Tests/Runtime/RuntimeSink.cs tests/SharpVision.Terminal.Tests/Transport/RuntimeSink.cs tests/SharpVision.Terminal.Tests/Runtime/SessionTests.cs
git commit -m "feat(runtime): preserve protocol replies in sessions"
```

## Task 6: Publish responses on the application dispatcher

**Files:**

- Create: `src/SharpVision/Runtime/ProtocolResponseEventArgs.cs`
- Create: `tests/SharpVision.Tests/Runtime/ProtocolRoutingTests.cs`
- Modify: `src/SharpVision/Runtime/Application.cs`
- Modify: `src/SharpVision/Runtime/Record.cs`
- Modify: `src/SharpVision/Runtime/RecordKind.cs`

- [ ] **Step 1: Write the failing application test**

Feed DA followed by text before the first resize. Subscribe to
`ProtocolResponse`, queue the resize, start, and assert:

```csharp
application.ProtocolResponse += (_, eventArgs) =>
{
    application.Dispatcher.CheckAccess().ShouldBeTrue();
    eventArgs.Response.Kind.ShouldBe(ResponseKind.PrimaryAttributes);
    eventArgs.Response.Values.ToArray().ShouldBe([1, 2]);
    order.Add("response");
};
```

Expected: compile failure naming the missing event and event args.

- [ ] **Step 2: Add event args and response records**

```csharp
using SharpVision.Terminal.Protocols;

namespace SharpVision.Runtime;

/// <summary>Provides one owned typed terminal response.</summary>
public sealed class ProtocolResponseEventArgs: EventArgs
{
    /// <summary>Initializes event arguments for one response.</summary>
    public ProtocolResponseEventArgs(Response response) => Response = response;

    /// <summary>Gets the recognized response.</summary>
    public Response Response { get; }
}
```

Add `Response` to `RecordKind`, plus:

```csharp
internal Response Response { get; private init; }

internal static Record From(Response value) => new(RecordKind.Response) { Response = value };
```

- [ ] **Step 3: Implement typed application delivery**

```csharp
/// <summary>Raised for one recognized terminal response on the dispatcher.</summary>
public event EventHandler<ProtocolResponseEventArgs>? ProtocolResponse;

/// <inheritdoc/>
public void Response(in Response value) => Enqueue(Record.From(value));
```

Add the dispatch case:

```csharp
case RecordKind.Response:
    ProtocolResponse?.Invoke(this, new ProtocolResponseEventArgs(record.Response));
    break;
```

- [ ] **Step 4: Redact unregistered extension strings**

```csharp
/// <inheritdoc/>
public void Sequence(ProtocolSequence value)
{
    ArgumentNullException.ThrowIfNull(value);
    var discarded = checked(value.Parameters.Length + value.Intermediates.Length + value.Payload.Length + (value.Kind == SequenceKind.Dcs ? 1 : 0));
    var diagnostic = new TerminalDiagnostic(DiagnosticCode.Unsupported, value.Kind, offset: 0, discardedBytes: discarded);
    Enqueue(Record.From(diagnostic));
}
```

Test `ESC ] 777 ; secret ST`: the application raises `Unsupported`/`Osc` with a
positive discarded count, and neither the event args nor `Diagnostic.ToString()`
contains `secret`.

- [ ] **Step 5: Run application integration tests**

```bash
dotnet test --project tests/SharpVision.Tests --filter-class "*ProtocolRoutingTests" "*OrderingTests" "*ApplicationTests" "*TerminalInputTests" --timeout 60s
```

Expected: all selected tests pass and response delivery is dispatcher-affine.

- [ ] **Step 6: Commit application observability**

```bash
git add src/SharpVision/Runtime/ProtocolResponseEventArgs.cs src/SharpVision/Runtime/Application.cs src/SharpVision/Runtime/Record.cs src/SharpVision/Runtime/RecordKind.cs tests/SharpVision.Tests/Runtime/ProtocolRoutingTests.cs
git commit -m "feat(runtime): publish typed terminal responses"
```

## Task 7: Synchronize coverage and run gates

**Files:**

- Modify: `docs/protocols/coverage-matrix.md`
- Modify: `docs/protocols/runtime-routing.md`
- Modify: `docs/testing/terminal-protocols.md`

- [ ] **Step 1: Update only proven claims**

State that DA1, DA2, CPR, DECRPM, Kitty keyboard status, and OSC color replies
are observable through `ProtocolRouter`, `Session`, and dispatcher events. State
that bounded raw string families are observable through
`IProtocolSink.Sequence`. Keep capability negotiation partial and keep Kitty
graphics, Sixel, and iTerm2 unsupported.

- [ ] **Step 2: Validate docs**

```bash
npx prettier --write docs/protocols/coverage-matrix.md docs/protocols/runtime-routing.md docs/testing/terminal-protocols.md
npm run lint:markdown
npm run lint:links
npm run test:docs
```

Expected: zero Markdown, link, anchor, and documentation-test failures.

- [ ] **Step 3: Run focused suites**

```bash
dotnet test --project tests/SharpVision.Terminal.Tests --filter-class "*RouterTests" "*ResponsesTests" "*SessionTests" "*PseudoterminalTests" --timeout 60s
dotnet test --project tests/SharpVision.Tests --filter-class "*ProtocolRoutingTests" "*OrderingTests" "*TerminalInputTests" --timeout 60s
```

Expected: every selected test passes with nonzero discovery.

- [ ] **Step 4: Run repository gates**

```bash
make format
make lint
make build
make test
```

Expected: zero formatting differences, warnings, build errors, analyzer errors,
Markdown failures, link failures, and test failures; discovered tests meet the
configured minimum.

- [ ] **Step 5: Audit and commit final docs**

```bash
git diff --check
git status --short
git diff --stat
git add docs/protocols/coverage-matrix.md docs/protocols/runtime-routing.md docs/testing/terminal-protocols.md
git commit -m "docs(protocols): record runtime router coverage"
```

Skip the final commit when there is no remaining documentation diff. Stage only
files from this plan and preserve unrelated user work.

## Self-review checklist

- Every protocol-router requirement in design slice 1 maps to a task.
- `Response`, `ProtocolSequence`, `IProtocolSink`, and `ProtocolRouter`
  signatures are consistent.
- Typed replies cannot fall through into keyboard handling.
- Raw payloads are copied only for opted-in protocol sinks and redacted before
  application queuing.
- All five terminal string families and all currently typed response families
  have fragmentation proof.
- Session and application tests exercise the real path.
- Capability negotiation remains explicitly outside this plan and starts from
  the routed response stream.
