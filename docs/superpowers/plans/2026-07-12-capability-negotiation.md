# Runtime Capability Negotiation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use
> superpowers:subagent-driven-development (recommended) or
> superpowers:executing-plans to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn routed terminal replies into one bounded immutable capability
profile before the first feature-dependent frame.

**Architecture:** A pure `Negotiator` batches safe DA1, Kitty keyboard-status,
and DECRQM probes within the configured concurrent-query limit and publishes
through the existing `Detector` precedence rules. `Session` intercepts replies
through a dedicated `NegotiationSink`, keeps delivering user input while the
shared deadline runs, withholds the first resize until the profile is published,
then enables only proven modes. `Application` applies profile updates on its
dispatcher before layout and renders with the active profile.

**Tech Stack:** .NET 10, C# 14, `TimeProvider`, `IBufferWriter<byte>`,
`ArrayBufferWriter<byte>`, xUnit v3, Shouldly, deterministic transports and
clocks.

---

## Delivery boundary

This plan completes vertical slice 1 from the approved
[terminal expansion design](../specs/2026-07-12-terminal-protocol-and-cell-geometry-expansion-design.md#vertical-slice-1-protocol-routing-and-capability-negotiation).
It negotiates only features with typed queries and responses:

- primary device attributes;
- Kitty keyboard status;
- synchronized output, focus reporting, bracketed paste, SGR cell mouse, and SGR
  pixel mouse through DECRQM/DECRPM.

Kitty graphics, Sixel, iTerm2 images, XTGETTCAP, XTWINOPS, clipboard reads, and
multiplexer passthrough remain in later plans. A timeout leaves query evidence
absent; it does not invent an unsupported result.

## Query batch and deadline

The default batch is exact and ordered:

```text
CSI ? u
CSI c
CSI ? 2026 $ p
CSI ? 1004 $ p
CSI ? 2004 $ p
CSI ? 1006 $ p
CSI ? 1016 $ p
```

The batch contains at most `Limits.MaxConcurrentQueries` entries. Capacity one
sends DA1 only. Capacity two or more sends Kitty keyboard status immediately
before DA1, then appends private-mode queries in the order shown. All emitted
queries share one finite deadline; negotiation never multiplies the timeout by
query count.

## File map

### Create

- `src/SharpVision.Terminal/Capabilities/NegotiationOptions.cs` — owned evidence
  and limit snapshot.
- `src/SharpVision.Terminal/Capabilities/Negotiator.cs` — query batch, matching,
  deadline, and publication.
- `tests/SharpVision.Terminal.Tests/Capabilities/NegotiationOptionsTests.cs`
- `tests/SharpVision.Terminal.Tests/Capabilities/NegotiatorTests.cs`
- `src/SharpVision.Terminal/Runtime/NegotiationSink.cs` — reply interception and
  ordinary-event forwarding.
- `src/SharpVision/Runtime/CapabilitiesChangedEventArgs.cs`
- `tests/SharpVision.Tests/Runtime/CapabilityNegotiationTests.cs`

### Modify

- `src/SharpVision.Terminal/Runtime/ISink.cs`
- `src/SharpVision.Terminal/Runtime/Options.cs`
- `src/SharpVision.Terminal/Runtime/Session.cs`
- `tests/SharpVision.Terminal.Tests/Runtime/RuntimeSink.cs`
- `tests/SharpVision.Terminal.Tests/Transport/RuntimeSink.cs`
- `tests/SharpVision.Terminal.Tests/Runtime/SessionTests.cs`
- `src/SharpVision/Runtime/Application.cs`
- `src/SharpVision.Showcase/StartupOptions.cs`
- `src/SharpVision.Showcase/Program.cs`
- `tests/SharpVision.Showcase.Tests/StartupOptionsTests.cs`
- `docs/architecture/capabilities.md`
- `docs/architecture/runtime-event-loop.md`
- `docs/protocols/device-attributes.md`
- `docs/protocols/coverage-matrix.md`
- `docs/testing/terminal-protocols.md`
- `docs/testing/pseudoterminals.md`

## Task 1: Specify bounded startup negotiation

**Files:**

- Modify: `docs/architecture/capabilities.md`
- Modify: `docs/architecture/runtime-event-loop.md`
- Modify: `docs/protocols/device-attributes.md`
- Modify: `docs/testing/terminal-protocols.md`

- [ ] **Step 1: Write the query-batch contract**

Add this normative contract:

```markdown
### Runtime negotiator

`Negotiator` snapshots caller-supplied environment values and emits one bounded
startup batch. DA1 is highest priority. Kitty keyboard status precedes DA1 only
when two query slots are available. Remaining slots query private modes 2026,
1004, 2004, 1006, and 1016 in that order.

All emitted queries share `Limits.QueryTimeout`. Validated responses may
complete negotiation early. At the deadline, absent replies remain absent query
evidence; they do not become unsupported values and therefore cannot erase
environment hints or explicit overrides.

The runtime publishes exactly one immutable startup profile before forwarding
the first resize. Explicit overrides are applied last. Matched, duplicate, late,
and unsolicited replies remain observable through runtime response events.
```

- [ ] **Step 2: Specify startup order**

Document this order:

```text
Starting -> base leases -> query batch -> input/reply/resize collection
-> profile publication -> optional leases -> first resize/layout/frame -> Started
```

User input continues during the query window. Only the newest pre-publication
resize is retained. Cancellation, EOF, and write failure use the ordinary
reverse-cleanup path.

- [ ] **Step 3: Specify test evidence**

Require exact query bytes, every response split, out-of-order replies,
fake-clock deadline, capacity truncation, input before publication, first-frame
profile use, late response observation, and PTY lifecycle proof.

- [ ] **Step 4: Validate and commit docs**

```bash
npx prettier --write docs/architecture/capabilities.md docs/architecture/runtime-event-loop.md docs/protocols/device-attributes.md docs/testing/terminal-protocols.md
npx markdownlint-cli2 docs/architecture/capabilities.md docs/architecture/runtime-event-loop.md docs/protocols/device-attributes.md docs/testing/terminal-protocols.md
npm run lint:links
git add docs/architecture/capabilities.md docs/architecture/runtime-event-loop.md docs/protocols/device-attributes.md docs/testing/terminal-protocols.md
git commit -m "docs(capabilities): specify runtime negotiation"
```

Expected: all document gates pass and only these four paths are committed.

## Task 2: Own inputs and encode one bounded batch

**Files:**

- Create: `src/SharpVision.Terminal/Capabilities/NegotiationOptions.cs`
- Create: `src/SharpVision.Terminal/Capabilities/Negotiator.cs`
- Create:
  `tests/SharpVision.Terminal.Tests/Capabilities/NegotiationOptionsTests.cs`
- Create: `tests/SharpVision.Terminal.Tests/Capabilities/NegotiatorTests.cs`

- [ ] **Step 1: Write failing ownership tests**

```csharp
[Fact]
public void Constructor_WhenEnvironmentChanges_RetainsOwnedSnapshot()
{
    // Arrange
    var environment = new Dictionary<string, string?>
    {
        ["TERM"] = "xterm-kitty",
    };
    var options = new NegotiationOptions(environment);

    // Act
    environment["TERM"] = "dumb";

    // Assert
    options.Environment["TERM"].ShouldBe("xterm-kitty");
}

[Fact]
public void Constructor_WhenEnvironmentIsNull_Throws()
{
    Should.Throw<ArgumentNullException>(
        () => new NegotiationOptions(null!));
}
```

Run the focused class. Expected: compile failure naming `NegotiationOptions`.

- [ ] **Step 2: Implement the owned policy**

```csharp
using System.Collections.ObjectModel;

using SharpVision.Terminal.Protocols;

namespace SharpVision.Terminal.Capabilities;

/// <summary>Owns finite policy and evidence inputs for one startup negotiation.</summary>
public sealed class NegotiationOptions
{
    /// <summary>Initializes one immutable negotiation policy.</summary>
    /// <param name="environment">Caller-supplied terminal environment values.</param>
    /// <param name="overrides">Optional explicit final overrides.</param>
    /// <param name="limits">Finite protocol limits, or null for defaults.</param>
    /// <exception cref="ArgumentNullException"><paramref name="environment"/> is null.</exception>
    public NegotiationOptions(
        IReadOnlyDictionary<string, string?> environment,
        Settings? overrides = null,
        Limits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(environment);
        var copy = new Dictionary<string, string?>(StringComparer.Ordinal);

        foreach (var pair in environment)
        {
            copy.Add(pair.Key, pair.Value);
        }

        Environment = new ReadOnlyDictionary<string, string?>(
            copy);
        Overrides = overrides;
        Limits = limits ?? Limits.Default;
    }

    /// <summary>Gets the owned environment snapshot.</summary>
    public IReadOnlyDictionary<string, string?> Environment { get; }

    /// <summary>Gets optional explicit final overrides.</summary>
    public Settings? Overrides { get; }

    /// <summary>Gets finite parser and query limits.</summary>
    public Limits Limits { get; }
}
```

- [ ] **Step 3: Write the failing exact-byte test**

```csharp
[Fact]
public void Start_WhenDefaultCapacityIsAvailable_WritesSafeQueriesInOrder()
{
    // Arrange
    var options = new NegotiationOptions(
        new Dictionary<string, string?>());
    var negotiator = new Negotiator(options, new ManualTimeProvider());
    var output = new ArrayBufferWriter<byte>();

    // Act
    negotiator.Start(output);

    // Assert
    Encoding.ASCII.GetString(output.WrittenSpan).ShouldBe(
        "\u001b[?u\u001b[c\u001b[?2026$p\u001b[?1004$p" +
        "\u001b[?2004$p\u001b[?1006$p\u001b[?1016$p");
    negotiator.IsComplete.ShouldBeFalse();
}
```

Expected: compile failure naming `Negotiator`.

- [ ] **Step 4: Implement start state and encoding**

The public surface is exact:

```csharp
public sealed class Negotiator
{
    public Negotiator(
        NegotiationOptions options,
        TimeProvider? timeProvider = null);

    public bool IsStarted { get; }
    public bool IsComplete { get; }
    public DateTimeOffset Deadline { get; }
    public Capabilities Capabilities { get; }
    public Diagnostic? LastDiagnostic { get; }

    public void Start(IBufferWriter<byte> destination);
    public QueryMatch Accept(in Response response);
    public bool Expire();
}
```

The implementation owns `NegotiationOptions`, `QueryTracker`, `TimeProvider`,
nullable query results, one deadline, and bounded pending/completed/expired mode
sets. `Start` validates destination and single use before mutation, selects no
more than `MaxConcurrentQueries`, registers DA/keyboard, and encodes via
`Writer`, `Keyboard.Query`, `Csi.PrimaryDeviceAttributes`, and
`Csi.QueryPrivateMode`.

Use one fixed mode array:

```csharp
private static readonly int[] _modes = [2026, 1004, 2004, 1006, 1016];
```

- [ ] **Step 5: Prove capacity and state validation**

Test capacities one through seven. Capacity one emits DA1; capacity two emits
keyboard plus DA1; each later slot appends one mode. Test null destination,
double start, `Accept` before start, `Expire` before start, and `Capabilities`
before completion. Invalid calls must not change state.

- [ ] **Step 6: Run and commit**

```bash
dotnet test --project tests/SharpVision.Terminal.Tests --filter-class "*NegotiationOptionsTests" "*NegotiatorTests" --timeout 60s
git add src/SharpVision.Terminal/Capabilities/NegotiationOptions.cs src/SharpVision.Terminal/Capabilities/Negotiator.cs tests/SharpVision.Terminal.Tests/Capabilities/NegotiationOptionsTests.cs tests/SharpVision.Terminal.Tests/Capabilities/NegotiatorTests.cs
git commit -m "feat(capabilities): encode bounded startup queries"
```

## Task 3: Match replies, expire once, and publish

**Files:**

- Modify: `src/SharpVision.Terminal/Capabilities/Negotiator.cs`
- Modify: `tests/SharpVision.Terminal.Tests/Capabilities/NegotiatorTests.cs`

- [ ] **Step 1: Write failing out-of-order tests**

Start with default capacity. Decode exact wire replies through
`Responses.TryCsi`, then accept private-mode replies in reverse order, keyboard
status, and DA1 last. Assert every selected query returns `Matched`, negotiation
completes early, and each proven feature has `Origin.Query`.

The mapping is exact:

```text
2026 -> Queries.SynchronizedOutput
1004 -> Queries.FocusReporting
2004 -> Queries.BracketedPaste
1006 -> Queries.CellMouse
1016 -> Queries.PixelMouse
```

- [ ] **Step 2: Implement response matching**

DA and keyboard use `QueryTracker.Match`. A keyboard response sets
`KittyKeyboard = true`. DA arriving while keyboard is pending sets it false
because the ordered Kitty probe closed unsupported.

Private-mode replies require two values and a mode in the pending set. Assign
`Response.IsSupported`, move the mode to completed, classify repeats as
duplicate, and leave unknown modes observable as `Unknown`. Complete early only
when tracker and mode pending counts both reach zero.

- [ ] **Step 3: Publish through existing precedence**

Create one `Queries` snapshot and publish:

```csharp
_capabilities = Detector.Detect(
    _options.Environment,
    queries,
    _options.Overrides);
```

The getter returns that instance only after completion. Later replies never
replace it.

- [ ] **Step 4: Write deadline and history tests**

With a manual clock prove: early `Expire` is false; the exact deadline completes
once; absent replies stay null; tentative hints survive; overrides win; late
responses return `Late`; repeats return `Duplicate`; unknown modes stay
`Unknown`; retained state remains bounded.

- [ ] **Step 5: Implement expiration**

Call `QueryTracker.Expire`, move pending modes into a bounded expired set,
publish once, and emit only structural duplicate/late diagnostics. Do not
include response values or environment strings.

- [ ] **Step 6: Run and commit**

```bash
dotnet test --project tests/SharpVision.Terminal.Tests --filter-class "*NegotiatorTests" "*QueryTrackerTests" "*DetectorTests" --timeout 60s
git add src/SharpVision.Terminal/Capabilities/Negotiator.cs tests/SharpVision.Terminal.Tests/Capabilities/NegotiatorTests.cs
git commit -m "feat(capabilities): publish negotiated profiles"
```

## Task 4: Integrate negotiation into `Session`

**Files:**

- Create: `src/SharpVision.Terminal/Runtime/NegotiationSink.cs`
- Modify: `src/SharpVision.Terminal/Runtime/ISink.cs`
- Modify: `src/SharpVision.Terminal/Runtime/Options.cs`
- Modify: `src/SharpVision.Terminal/Runtime/Session.cs`
- Modify: `tests/SharpVision.Terminal.Tests/Runtime/RuntimeSink.cs`
- Modify: `tests/SharpVision.Terminal.Tests/Transport/RuntimeSink.cs`
- Modify: `tests/SharpVision.Terminal.Tests/Runtime/SessionTests.cs`

- [ ] **Step 1: Write the failing startup-order test**

Configure negotiation with a manual clock. Queue text and a resize before
replies. Assert text arrives immediately but profile and resize do not. Feed
arbitrarily fragmented replies and assert:

```text
text -> response callbacks -> profile -> resize
```

Assert optional focus/paste/mouse/keyboard enable bytes follow query bytes and
precede reverse cleanup.

- [ ] **Step 2: Extend the sink contract**

```csharp
/// <summary>Publishes one immutable active capability profile.</summary>
/// <param name="value">The non-null immutable profile.</param>
public void Profile(Capabilities.Capabilities value);
```

`Capabilities` is a class, so do not use `in`. Every implementation validates
null before mutation.

- [ ] **Step 3: Add opt-in policy**

Add to runtime `Options`:

```csharp
/// <summary>Gets optional bounded startup negotiation policy.</summary>
public NegotiationOptions? Negotiation { get; init; }
```

Null preserves static-capability startup and publishes `Options.Capabilities`.
When present, explicit policy belongs in `NegotiationOptions.Overrides`.

- [ ] **Step 4: Add `NegotiationSink`**

It implements `IProtocolSink` and forwards every ordinary callback. Responses
update the negotiator before remaining observable:

```csharp
public void Response(in Response value)
{
    _ = _negotiator.Accept(in value);
    _destination.Response(in value);
}
```

The explicit constructor validates both dependencies.

- [ ] **Step 5: Split base and optional leases**

`StartAsync` enables only alternate screen and cursor. Add
`EnableOptionalAsync(Capabilities, CancellationToken)` for focus, paste, mouse,
and keyboard. `MouseSupported` accepts the active profile instead of reading
static options.

- [ ] **Step 6: Implement the shared-deadline loop**

When negotiation is enabled:

1. create negotiator and interception sink;
2. encode/write/flush the query batch once;
3. start transport read, resize read, and one injected-clock delay;
4. route input/replies while retaining only newest resize;
5. on early completion or deadline, publish profile, enable optional modes, then
   deliver retained resize;
6. continue the ordinary read/resize loop without a timer.

No lock spans callbacks or writes. EOF expires/publishes for observability but
does not enable new modes. Query-write, optional-mode, input-handler,
cancellation, and cleanup failures preserve the existing primary-exception
rules.

- [ ] **Step 7: Prove lifecycle cases**

Test complete replies, no replies, capacity one, resize storms, live input, EOF
before deadline, cancellation, query-write failure, optional-mode failure, and
exact reverse cleanup.

- [ ] **Step 8: Run and commit**

```bash
dotnet test --project tests/SharpVision.Terminal.Tests --filter-class "*NegotiatorTests" "*SessionTests" "*PseudoterminalTests" --timeout 60s
git add src/SharpVision.Terminal/Runtime/NegotiationSink.cs src/SharpVision.Terminal/Runtime/ISink.cs src/SharpVision.Terminal/Runtime/Options.cs src/SharpVision.Terminal/Runtime/Session.cs tests/SharpVision.Terminal.Tests/Runtime/RuntimeSink.cs tests/SharpVision.Terminal.Tests/Transport/RuntimeSink.cs tests/SharpVision.Terminal.Tests/Runtime/SessionTests.cs
git commit -m "feat(runtime): negotiate capabilities before first resize"
```

## Task 5: Apply profiles before layout and rendering

**Files:**

- Create: `src/SharpVision/Runtime/CapabilitiesChangedEventArgs.cs`
- Create: `tests/SharpVision.Tests/Runtime/CapabilityNegotiationTests.cs`
- Modify: `src/SharpVision/Runtime/Application.cs`

- [ ] **Step 1: Write the failing first-frame test**

With negotiation enabled, queue resize before replies and start asynchronously.
Assert no frame before publication. Feed replies and prove `CapabilitiesChanged`
occurs before `Resize`, `Application.Capabilities` is the published instance,
the first renderer write uses proven synchronized output, and overrides stay
final.

- [ ] **Step 2: Add event arguments**

```csharp
using TerminalCapabilities = SharpVision.Terminal.Capabilities.Capabilities;

namespace SharpVision.Runtime;

/// <summary>Provides one dispatcher-published capability profile change.</summary>
public sealed class CapabilitiesChangedEventArgs: EventArgs
{
    public CapabilitiesChangedEventArgs(
        TerminalCapabilities previous,
        TerminalCapabilities current)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(current);
        Previous = previous;
        Current = current;
    }

    public TerminalCapabilities Previous { get; }
    public TerminalCapabilities Current { get; }
}
```

Add complete XML parameter and property documentation.

- [ ] **Step 3: Store pending profiles outside input records**

`Application.Profile` stores the non-null reference under `_gate`, sets
`_profilePending`, and posts `DrainProfile`. Before root initialization,
`DrainProfile` leaves the value pending. `DrainResize` consumes and applies it
before attaching the root or laying out. This avoids the existing pre-resize
input-queue hold.

- [ ] **Step 4: Apply invalidation and renderer use**

Expose:

```csharp
public TerminalCapabilities Capabilities { get; private set; }
public event EventHandler<CapabilitiesChangedEventArgs>? CapabilitiesChanged;
```

Initialize from static options. After initialization, ambiguous-width changes
invalidate measure; other capability changes invalidate render. Raise the event
after assignment and before processing invalidation. Pass `Capabilities`, not
`_options.Capabilities`, to `Renderer.RenderAsync`. Frame geometry remains the
next slice.

- [ ] **Step 5: Prove refresh ordering**

Publish a second profile during a paused render. Assert dispatcher affinity,
immutable previous/current values, one coalesced invalidation, no mid-frame
swap, and the next frame using the new profile.

- [ ] **Step 6: Run and commit**

```bash
dotnet test --project tests/SharpVision.Tests --filter-class "*CapabilityNegotiationTests" "*ProtocolRoutingTests" "*OrderingTests" "*ApplicationTests" --timeout 60s
git add src/SharpVision/Runtime/CapabilitiesChangedEventArgs.cs src/SharpVision/Runtime/Application.cs tests/SharpVision.Tests/Runtime/CapabilityNegotiationTests.cs
git commit -m "feat(runtime): publish active terminal capabilities"
```

## Task 6: Opt the executable showcase into negotiation

**Files:**

- Modify: `src/SharpVision.Showcase/StartupOptions.cs`
- Modify: `src/SharpVision.Showcase/Program.cs`
- Modify: `tests/SharpVision.Showcase.Tests/StartupOptionsTests.cs`

- [ ] **Step 1: Write the failing policy test**

Assert `StartupOptions.Create(environment, negotiate: true)` snapshots the
environment, preserves the existing explicit `CellMouse = true` override, and
does not upgrade tentative hints before replies.

- [ ] **Step 2: Preserve deterministic showcase tests**

Implement:

```csharp
internal static RuntimeOptions Create(
    IReadOnlyDictionary<string, string?> environment,
    bool negotiate = false)
```

False retains static detection. True supplies `NegotiationOptions` with the
cell-mouse override and conservative static capabilities. Only executable
`Program.cs` passes true; screen tests keep the fast default.

- [ ] **Step 3: Prove executable bytes with a manual clock**

Observe the exact query batch, feed replies, and prove mouse enabling follows
publication. Use condition-based waits, never sleep.

- [ ] **Step 4: Run and commit**

```bash
dotnet test --project tests/SharpVision.Showcase.Tests --filter-class "*StartupOptionsTests" "*GalleryTests" --timeout 60s
git add src/SharpVision.Showcase/StartupOptions.cs src/SharpVision.Showcase/Program.cs tests/SharpVision.Showcase.Tests/StartupOptionsTests.cs
git commit -m "feat(showcase): negotiate terminal capabilities at startup"
```

## Task 7: Synchronize coverage and verify

**Files:**

- Modify: `docs/protocols/coverage-matrix.md`
- Modify: `docs/architecture/capabilities.md`
- Modify: `docs/architecture/runtime-event-loop.md`
- Modify: `docs/testing/pseudoterminals.md`

- [ ] **Step 1: Update only proven claims**

State that device attributes and selected DECRQM modes are actively negotiated,
timeout-bounded, and published before first layout. Keep geometry queries,
graphics probes, multiplexer reply routes, and graphics families unchanged.

- [ ] **Step 2: Add PTY and tmux smoke proof**

Run Unix PTY tests. If tmux is installed, record `tmux -V` and run a
condition-based disposable-server smoke proving negotiation reaches valid
replies or its finite conservative deadline and cleanup succeeds. Do not claim
outer passthrough.

- [ ] **Step 3: Run docs and focused Release proof**

```bash
npm run lint:markdown
npm run lint:links
npm run test:docs
dotnet test --project tests/SharpVision.Terminal.Tests --configuration Release --filter-class "*NegotiatorTests" "*SessionTests" "*PseudoterminalTests" --timeout 60s
dotnet test --project tests/SharpVision.Tests --configuration Release --filter-class "*CapabilityNegotiationTests" "*ProtocolRoutingTests" "*OrderingTests" --timeout 60s
dotnet test --project tests/SharpVision.Showcase.Tests --configuration Release --filter-class "*StartupOptionsTests" --timeout 60s
```

- [ ] **Step 4: Run repository gates**

```bash
make format
make lint
make build
make test
```

Expected: zero warnings, formatting differences, documentation failures, and
test failures. If unrelated concurrent work remains red, record exact failures
and keep this gate open.

- [ ] **Step 5: Audit and commit final docs**

```bash
git diff --check
git status --short
git diff --stat
git add docs/protocols/coverage-matrix.md docs/architecture/capabilities.md docs/architecture/runtime-event-loop.md docs/testing/pseudoterminals.md
git commit -m "docs(capabilities): record negotiated runtime coverage"
```

Stage only negotiation-owned hunks when documents overlap concurrent work.

## Self-review checklist

- Every capability-negotiation requirement in design slice 1 maps to a task.
- Query count, bytes, ordering, and one shared deadline are explicit.
- Capacity one through default is deterministic and bounded.
- Timeouts preserve hints; replies and overrides alone authorize use.
- User input remains live while first resize is withheld.
- Publication precedes optional modes, layout, and first frame.
- Profile updates are dispatcher-affine and never swap a frame mid-render.
- Static options remain compatible; the executable showcase opts in.
- Every named type has one exact file, explicit constructor, and XML docs.
- Full gates remain required even while unrelated concurrent work is temporarily
  red.
