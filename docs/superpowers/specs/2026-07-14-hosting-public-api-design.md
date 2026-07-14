# Hosting & public API: portable console host, fluent builder, input surface, protocol consumption

## Status

Design approved 2026-07-14. Ready for an implementation plan.

## Problem

SharpVision's runtime is correct but its **public hosting surface is thin and
Unix-shaped**, and several capabilities the library already implements are not
reachable by a consumer:

1. **The console host is not portable.** `ConsoleInputMode.Enter()` shells out
   to `/bin/stty` and is a no-op on Windows: there is no `SetConsoleMode` call,
   so Windows never enters VT input / VT processing mode. The console path
   always uses the polling
   [`ConsoleResizeSource`](../../../src/SharpVision.Terminal/Runtime/ConsoleResizeSource.cs)
   (cell-only) even on Unix, so
   [`UnixResizeSource`](../../../src/SharpVision.Terminal/Runtime/UnixResizeSource.cs)
   (SIGWINCH plus pixel dimensions) is unused and **pixel mouse never works** in
   a console run.

2. **Console options are a stub.**
   [`ConsoleRunOptions`](../../../src/SharpVision/Runtime/ConsoleRunOptions.cs)
   has a single `RedirectedMessage` property. Every real knob — mouse, alternate
   screen, cursor, focus/paste, keyboard enhancement, cleanup timeout,
   capabilities, negotiation, resize interval, theme — is hardcoded in
   `ConsoleRun.CreateTerminalOptions()` and cannot be configured or passed
   through to the `Application`.

3. **`Application` exposes no live input read-model.** It surfaces `Focus`,
   `Capture` (hovered / pressed / captured), `Size`, and `Capabilities`, but no
   current pointer position (cell or pixel), held buttons, or modifiers.
   [`CaptureManager`](../../../src/SharpVision/Input/CaptureManager.cs) sees
   every pointer in `Dispatch` but retains none of it, so "where is the mouse"
   is unanswerable.

4. **Protocol support is present but not ergonomic to consume.**
   [`Capabilities.OptionalFeatures`](../../../src/SharpVision.Terminal/Capabilities/Capabilities.cs)
   is an anonymous `IReadOnlyList<Feature>` — the caller cannot tell which
   feature is which. Typed protocol responses flow through `ResponseReceived`
   and capability changes through `CapabilitiesChanged`, but there is no
   discoverable "what is supported and how do I use it" surface, and no
   application-facing way to invoke implemented **output** protocols (bell,
   window title, clipboard).

5. **One layering seam is muddy.** `Application.Console.cs` plus `ConsoleRun`
   hand-wire five Terminal-layer primitives (`ConsoleHost`, `ConsoleInputMode`,
   `ConsoleResizeSource`, `StreamTransport`, capability detection, negotiation).
   That orchestration belongs behind one Terminal-layer seam the higher layer
   simply consumes. `ConsoleInputMode` is public but is a Unix-only
   implementation detail — an abstraction that leaks out of its domain.

6. **There is no fluent entry point.** The only door is the static
   `Application.RunConsoleAsync(screen, options?)`; advanced hosts construct
   `Application` by hand.

## Goals

- Make the console host portable across Unix and Windows behind one clean seam.
- Make console options comprehensive, defaulted, and fully passed through to the
  `Application`.
- Provide a fluent public entry point in the shape of ASP.NET's
  `WebApplicationBuilder` and EF Core's configure-callback — both first-class.
- Expose live input state (pointer location, buttons, modifiers, terminal focus)
  as a grouped read-model on `Application`.
- Make the supported protocol set discoverable, and make implemented output
  protocols (bell, title, clipboard) consumable behind small interfaces.
- Remove the concrete abstraction leaks above without gratuitous restructuring.
- Preserve every existing behavior, invariant, dispatcher-affinity, capability
  gate, and safe-degradation rule.

## Non-goals

- No new rendering model, virtual tree, reconciliation, or hook-style state.
- No backward-compatibility shims. The library is pre-1.0 and spec-first;
  `Application.RunConsoleAsync` is replaced, not deprecated in place.
- No support claims for unsupported protocols. Kitty graphics, sixel, and iTerm2
  remain "unsupported with a specific reason" per the
  [coverage matrix](../../protocols/coverage-matrix.md#coverage); the discovery
  facade reports their real state and the output facade never exposes them.
- No changes to layout math, input routing, focus, styling, theming, or the
  dispatcher event loop, other than the additive read-model and out-of-band
  write path described here.
- This design composes with, and does not alter, the already-approved
  `Screen : View` / `Build()` model.

## Decisions (locked)

| #   | Decision                  | Choice                                                                                                                                                                                                                                                                    |
| --- | ------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1   | Fluent shape              | Both first-class: a mutable `ConsoleApplicationBuilder` **and** an `Action<ConsoleApplicationBuilder>` configure-callback; plus an immutable `ConsoleRunOptions` path. One config model (the callback configures the builder).                                            |
| 2   | Windows portability       | Portable seam plus a real Windows VT path (`SetConsoleMode`). Unix fully verified here; the Windows path is unit-tested for flag math and the P/Invoke boundary and flagged as needing hardware/CI validation.                                                            |
| 3   | Input surface             | A grouped read-only device object: `Application.Pointer` → `PointerDevice`; plus `Application.HasFocus`.                                                                                                                                                                  |
| 4   | Scope                     | One cohesive design, implemented across ordered phases.                                                                                                                                                                                                                   |
| 5   | Bell and output protocols | Exposed behind interfaces: `Application.Terminal` → `ITerminalServices` with `IBell`, `IClipboard`, and `SetTitle`. The bell is required, not optional.                                                                                                                   |
| 6   | Out-of-band writes        | A single-writer ordered mechanism shares the renderer's `_rendering` gate, so protocol bytes never interleave a frame.                                                                                                                                                    |
| 7   | Sharpie-inspired options  | Take `TreatControlCAsInput` (from `SuppressControlKeys`), a color-depth override, and environment size overrides. Drop echo / input-buffering / manual-flush / managed-windows / header-footer / SLK / mouse-click-interval as architecture-fighting or NCurses-specific. |

## Design

### 1. Portable console host (Terminal layer)

Consolidate console wiring behind one seam in `SharpVision.Terminal.Runtime`.

`ConsoleHost` (existing static class) becomes the portable façade:

```csharp
public static class ConsoleHost
{
    public static bool IsInteractive { get; }                 // unchanged intent
    public static ConsoleConnection Open(ConsoleHostOptions options);
}
```

`ConsoleHostOptions` (new `sealed record`) is the terminal-layer host policy:

- `TimeSpan ResizeInterval` — positive, finite; the poll interval used by the
  cell-only fallback resize source. Default `100 ms`.
- `bool CaptureControlKeys` — when `true`, control keys (Ctrl+C) are delivered
  as input rather than raising the host signal. Default `false`.

`ConsoleConnection` (new `sealed class : IAsyncDisposable`) is the opened
bundle:

- `ITransport Transport { get; }`
- `IResizeSource Resize { get; }`
- `DisposeAsync()` restores the platform terminal mode (stty restore on Unix,
  `SetConsoleMode` restore on Windows).

**Ownership is explicit.** The connection _constructs_ the transport and resize
source, but the running `Session` disposes those (unchanged from today). The
connection owns only the platform **restore lease**. `Application` disposes the
connection as the _final_ cleanup step — after the session's reverse DEC-mode
cleanup — so VT modes are undone before cooked/echo input is restored.

Internal platform strategies select behavior by OS:

- `UnixConsoleHost` — opens `/dev/tty` streams, enters `stty raw -echo`
  (retaining or dropping `isig` per `CaptureControlKeys`), and uses
  `UnixResizeSource` so SIGWINCH **and pixel dimensions** drive resize. This is
  what makes pixel mouse work in a console run.
- `WindowsConsoleHost` — resolves handles via `GetStdHandle`, saves the current
  input and output console modes, enables `ENABLE_VIRTUAL_TERMINAL_INPUT` on
  input and `ENABLE_VIRTUAL_TERMINAL_PROCESSING | DISABLE_NEWLINE_AUTO_RETURN`
  on output, clears `ENABLE_LINE_INPUT | ENABLE_ECHO_INPUT` (and
  `ENABLE_PROCESSED_INPUT` when `CaptureControlKeys` is `true`), and uses the
  polling `ConsoleResizeSource` (cell-only; pixel dimensions are documented as
  unavailable on the standard Windows console).

`ConsoleInputMode` moves from public to an internal Unix implementation detail
of `UnixConsoleHost`; a new internal `WindowsConsoleMode` is its peer. The
public surface shrinks: consumers see only `ConsoleHost`, `ConsoleHostOptions`,
and `ConsoleConnection`. New Windows P/Invoke (`GetStdHandle`, `GetConsoleMode`,
`SetConsoleMode`) is added to
[`Native`](../../../src/SharpVision.Terminal/Runtime/Native.cs) using
`LibraryImport` with `SetLastError`, mirroring `GetDimensions`.

`Application` gains one optional constructor parameter,
`IAsyncDisposable? hostLease = null`, which it owns and disposes last.

### 2. Comprehensive console options (SharpVision layer)

`ConsoleRunOptions` becomes a comprehensive immutable `record` with proper
defaults that fully drives the terminal session policy. Each property validates
on `init` (reusing the terminal `Options` validation rules). Fields and
defaults:

| Property                      | Type                  | Default                                     |
| ----------------------------- | --------------------- | ------------------------------------------- |
| `Theme`                       | `Theme?`              | `null` → `Themes.Dark`                      |
| `AlternateScreen`             | `bool`                | `true`                                      |
| `ShowCursor`                  | `bool`                | `false`                                     |
| `MouseTracking`               | `MouseTracking?`      | `MouseTracking.Any` (`null` disables mouse) |
| `MouseCoordinates`            | `MouseCoordinates`    | `MouseCoordinates.Sgr`                      |
| `BracketedPaste`              | `bool`                | `true`                                      |
| `FocusReporting`              | `bool`                | `true`                                      |
| `KeyboardEnhancement`         | `Enhancement?`        | `Disambiguate` + `EventTypes`               |
| `Capabilities`                | `Capabilities?`       | `null` → detect and negotiate               |
| `ColorDepth`                  | `ColorDepth?`         | `null` → detected                           |
| `Negotiation`                 | `NegotiationOptions?` | `null` → default startup negotiation        |
| `CleanupTimeout`              | `TimeSpan`            | `1 s`                                       |
| `ReadBufferSize`              | `int`                 | `16 * 1024`                                 |
| `ResizeInterval`              | `TimeSpan`            | `100 ms`                                    |
| `TreatControlCAsInput`        | `bool`                | `false`                                     |
| `UseEnvironmentSizeOverrides` | `bool`                | `false`                                     |
| `RedirectedMessage`           | `string?`             | `null`                                      |

It exposes `ToTerminalOptions(Capabilities detected)` that maps to the terminal
`Options` record, replacing `ConsoleRun.CreateTerminalOptions()`. Defaults
reproduce today's behavior: detect + negotiate, mouse `Any`/`Sgr`, alternate
screen on, cursor hidden, focus/paste on, Kitty keyboard enhancement on. When
`Capabilities` is set explicitly it bypasses detection; `ColorDepth` overrides
the detected depth. `UseEnvironmentSizeOverrides` honors `LINES`/`COLUMNS` for
the initial size (a testability aid); `TreatControlCAsInput` flows into
`ConsoleHostOptions.CaptureControlKeys` and suppresses the console-run cancel
wiring.

### 3. Fluent builder and configure-callback

`ConsoleApplicationBuilder` (new `sealed class`, mutable, fluent — every setter
returns `this`) is the single configuration surface. It holds the `Screen` and a
working `ConsoleRunOptions`. Methods:

`UseTheme`, `UseAlternateScreen(bool = true)`, `ShowCursor(bool = true)`,
`UseMouse(MouseTracking = Any, MouseCoordinates = Sgr)`, `WithoutMouse()`,
`UseBracketedPaste(bool = true)`, `UseFocusReporting(bool = true)`,
`UseKeyboardEnhancement(Enhancement?)`, `UseColorDepth(ColorDepth)`,
`WithoutColors()`, `UseNegotiation(NegotiationOptions)`, `WithoutNegotiation()`,
`UseCapabilities(Capabilities)`, `WithCleanupTimeout(TimeSpan)`,
`WithReadBufferSize(int)`, `WithResizeInterval(TimeSpan)`,
`TreatControlCAsInput(bool = true)`, `UseEnvironmentSizeOverrides(bool = true)`,
`WithRedirectedMessage(string?)`, and a `ConfigureTerminal(Action<...>)` escape
hatch. Argument validation is delegated to `ConsoleRunOptions` `init` accessors.

- `Build() : Application` — opens the console host and returns a fully wired
  `Application` (advanced control). Throws `IOException` when
  `!ConsoleHost.IsInteractive`.
- `RunAsync(CancellationToken = default) : ValueTask<ConsoleRunStatus>` — the
  managed console lifecycle: redirect check, build, start, wait for completion
  or Ctrl+C, stop, and status mapping.

`ConsoleApplication` (new `static class`) is the front door:

```csharp
public static class ConsoleApplication
{
    public static ConsoleApplicationBuilder CreateBuilder(Screen screen);
    public static ValueTask<ConsoleRunStatus> RunAsync(
        Screen screen, Action<ConsoleApplicationBuilder>? configure = null);
    public static ValueTask<ConsoleRunStatus> RunAsync(
        Screen screen, ConsoleRunOptions options);
}
```

The three usages:

```csharp
// One-liner
await ConsoleApplication.RunAsync(new Gallery());

// EF-Core-style configure-callback (configures the builder)
await ConsoleApplication.RunAsync(new Gallery(), b => b
    .UseTheme(Themes.Dark)
    .UseMouse(MouseTracking.Any, MouseCoordinates.Sgr)
    .WithoutNegotiation());

// ASP.NET-style builder
await ConsoleApplication.CreateBuilder(new Gallery())
    .UseAlternateScreen()
    .UseKeyboardEnhancement(Enhancement.Disambiguate | Enhancement.EventTypes)
    .RunAsync();

// Advanced: own the lifecycle
Application app = ConsoleApplication.CreateBuilder(new Gallery()).Build();
await app.RunAsync();
```

`Application` gains an instance convenience
`RunAsync(CancellationToken = default) : Task` = start → await `Completion` →
stop, surfacing `Failure`. This serves non-console hosts too. The console-status
mapping stays in the console types. `Application.RunConsoleAsync` and
`ConsoleRun` are removed; `Program.cs` moves to `ConsoleApplication`.

### 4. `Application` input read-model (grouped device)

`PointerDevice` (new `sealed class`) is a dispatcher-affine read model:

```csharp
public sealed class PointerDevice
{
    public Point? Position { get; }        // last cell position, null before first pointer / after leave
    public Point? PixelPosition { get; }   // last pixel position when the wire supplied it
    public Buttons Buttons { get; }        // buttons held as of the last pointer
    public Modifiers Modifiers { get; }    // modifiers as of the last pointer
    public PointerAction LastAction { get; }
    public Control? Hovered { get; }       // delegated to CaptureManager
    public Control? Pressed { get; }       // delegated to CaptureManager
    public Control? Captured { get; }      // delegated to CaptureManager
}
```

- `Application.Pointer` returns the device. Unlike `Focus`/`Capture` (which
  throw before the first resize because they own tree state), `Pointer` is
  **always readable**: hovered/pressed/captured read through the
  `CaptureManager` when it exists (otherwise `null`), and
  position/buttons/modifiers reflect the last dispatched pointer (null/none
  until the first arrives). This makes "read the mouse location" safe at any
  time.
- `Application.HasFocus` (`bool`) tracks terminal focus in/out; it defaults to
  `true` (assume focused until told otherwise) and updates on focus records.

Implementation: the device object is created once in the constructor and reads
`CaptureValue`. `Application.Dispatch` records the last `Pointer`
(position/pixel/buttons/modifiers/action) for `RecordKind.Pointer` — clearing
position on `PointerAction.Leave` — and toggles `HasFocus` for
`RecordKind.Focus`. No new event is added; consumers already have `Router`
pointer events for push-style needs, and this is the pull-style snapshot.

### 5. Protocol discovery (Terminal layer)

Make the supported set named and honest.

- New `TerminalProtocol` enum names each optional feature carried by
  `Capabilities` (SynchronizedOutput, FocusReporting, BracketedPaste,
  PixelMouse, CellMouse, KittyKeyboard, Osc52, KittyClipboard, KittyGraphics,
  Sixel, ItermImages, StyledUnderlines, UnderlineColor, Overline).
- New `ProtocolSupport`
  `readonly record struct { TerminalProtocol Protocol; Feature Feature; }`.
- `Capabilities.Support(TerminalProtocol) : Feature` maps the enum to the
  matching property.
- `Capabilities.Features : IReadOnlyList<ProtocolSupport>` replaces the
  anonymous `OptionalFeatures`, pairing each protocol with its `Feature`
  (state + origin).

This reports each protocol's real `Support` state — `Unsupported` for
graphics/sixel/iTerm2 — and never fabricates support. The inbound consumption
surface (`ResponseReceived` typed responses, `CapabilitiesChanged`,
`Diagnostic`) is unchanged in behavior and documented as the way to consume
protocol replies.

### 6. Output protocols behind interfaces (SharpVision layer)

New interfaces in `SharpVision.Runtime`:

```csharp
public interface IBell { void Ring(); }

public interface IClipboard
{
    bool IsSupported { get; }
    void Write(ReadOnlySpan<char> text, Selection selection = Selection.Clipboard);
    void Request(Selection selection = Selection.Clipboard);
}

public interface ITerminalServices
{
    IBell Bell { get; }
    IClipboard Clipboard { get; }
    void SetTitle(string title);
}
```

`Application.Terminal` returns an `ITerminalServices`;
`Application.Terminal.Bell` is an `IBell`. Encoding reuses the existing
[`Osc`](../../../src/SharpVision.Terminal/Protocols/Osc.cs),
[`Modes`](../../../src/SharpVision.Terminal/Protocols/Modes.cs), and clipboard
encoders plus a C0 BEL byte.

- **Bell** emits BEL (`0x07`); always available.
- **`SetTitle`** emits OSC 2; always available.
- **Clipboard** is capability-gated: `IsSupported` reflects `Osc52` /
  `KittyClipboard`; when unsupported, `Write`/`Request` are no-ops (safe
  degradation). `Request` results arrive through the existing `ResponseReceived`
  path.
- Graphics, sixel, and iTerm images are **not** exposed (unsupported per the
  matrix).

Interfaces make these testable against a fake transport and let a future visual
bell be a drop-in `IBell`.

### 7. Ordered out-of-band writes

Output-protocol bytes must never interleave a frame written by the async
renderer. The mechanism reuses the renderer's single-writer discipline:

- Each service call posts to the dispatcher and appends its encoded bytes to a
  pending out-of-band buffer guarded by the existing `_gate`.
- A drain runs on the dispatcher. If `_rendering` is `true`, the bytes stay
  buffered; `CompleteRender` drains pending out-of-band bytes before it services
  a deferred render request. If `_rendering` is `false`, the drain starts an
  out-of-band flush that sets `_rendering`, writes and flushes the buffer
  through the transport, and on completion clears `_rendering` and resumes
  normal invalidation — exactly the path frame rendering uses.

Because out-of-band flushes and frame renders share the `_rendering` gate and
the dispatcher `Hold`, there is a single writer at all times and byte ordering
is deterministic.

## Error handling

- `ConsoleApplicationBuilder.Build()` on a redirected console throws
  `IOException` ("the console host is not interactive").
  `ConsoleApplication.RunAsync` and `ConsoleApplicationBuilder.RunAsync` check
  `IsInteractive` first and return `ConsoleRunStatus.Redirected`, writing
  `RedirectedMessage` when present.
- All `ConsoleRunOptions` and builder arguments validate before mutating state,
  reusing the terminal `Options` bounds (positive/finite timeouts, positive
  buffer size, defined enum values); each throw is documented in XML.
- Windows P/Invoke failures throw `IOException` with a `Win32Exception` inner,
  mirroring `Native.GetDimensions`. `ConsoleConnection.DisposeAsync` restores
  the saved console mode on a best-effort basis without hiding a primary
  failure.
- Out-of-band write failures surface through the existing render-failure path
  (`Report` / `Failure`); a cancelled flush during shutdown is swallowed like a
  cancelled render.
- Clipboard on an unsupported terminal is a documented no-op with
  `IsSupported == false`; it never throws for lack of support.
- `PointerDevice` never throws; it is a pure snapshot.

## Testing

- **Console host:** the OS strategy selection and the `ConsoleConnection`
  ownership/disposal order (VT modes undone before input restore) are unit
  tested. The Windows mode-flag computation (which bits are set and cleared for
  each option) and the P/Invoke boundary shape are unit tested; the spec records
  that live Windows-console behavior needs hardware/CI validation. The Unix path
  keeps its existing integration coverage. Pixel dimensions reach the runtime on
  the Unix console path (regression for the pixel-mouse gap).
- **Options mapping:** exhaustive default values and each knob mapping into the
  terminal `Options` and `ConsoleHostOptions`; `TreatControlCAsInput` and the
  color-depth / environment-size overrides.
- **Builder:** each fluent method sets exactly its field; the configure-callback
  overload is equivalent to the explicit builder; the immutable
  `ConsoleRunOptions` overload; `Build()` throws when non-interactive;
  `RunAsync` returns `Redirected` when non-interactive and maps
  completion/cancel/failure to the right status.
- **Input read-model:** position/pixel/buttons/modifiers update on a dispatched
  pointer; `Leave` clears position; hovered/pressed/captured delegate to
  `CaptureManager`; `HasFocus` toggles on focus records and defaults `true`.
- **Protocol discovery:** `Support(protocol)` returns the matching `Feature` for
  every enum member; `Features` lists all protocols exactly once; unsupported
  protocols report their real `Support` state (honest against the matrix).
- **Output services:** `IBell.Ring()` emits exact BEL bytes to a fake transport;
  a bell requested mid-render flushes _after_ the frame with no interleaving
  (assert byte order); `SetTitle` emits exact OSC 2 bytes; clipboard emits exact
  OSC 52 bytes when supported and nothing when unsupported; off-dispatcher calls
  marshal correctly.
- Tests use xUnit v3, Shouldly, deterministic fakes for transport/resize/clock,
  and `MethodName_WhenThis_ThatIsExpected` naming. New tests watch-fail first.

## Documentation to update in the same change

- New `docs/concepts/hosting.md` — `ConsoleApplication`, the builder, the
  configure-callback, `ConsoleRunOptions`, and Unix/Windows portability.
- `docs/architecture/runtime-event-loop.md` — `Application.RunAsync`, the
  out-of-band write path and its ordering guarantee.
- `docs/concepts/input-routing.md` — `Application.Pointer` / `PointerDevice` and
  `Application.HasFocus`.
- `docs/protocols/index.md` and `docs/protocols/coverage-matrix.md` — the
  `TerminalProtocol` discovery facade, the `ITerminalServices` output surface,
  and the honest unsupported states.
- `docs/protocols/runtime-routing.md` — the inbound consumption surface.
- `docs/architecture/showcase.md` and `src/SharpVision.Showcase/Program.cs` —
  the `ConsoleApplication` entry point.
- `AGENTS.md` — the hosting pattern, `ITerminalServices`/`IBell`, and
  `TreatControlCAsInput`.

## Precondition (must hold before implementation starts)

A green `make build && make test` baseline. The working tree is currently clean
on `codex/runtime-protocol-router`; the implementation plan verifies the
baseline before phase 1.

## Proposed phasing (for the implementation plan)

0. Verify the green baseline above.
1. **Portable console host seam:** `ConsoleHost.Open` + `ConsoleConnection` +
   `ConsoleHostOptions`; `UnixConsoleHost` (SIGWINCH + pixels) and
   `WindowsConsoleHost` (`SetConsoleMode`);
   `ConsoleInputMode`/`WindowsConsoleMode` internalized; `Native` Windows
   P/Invoke; `Application` owns the host lease; `TreatControlCAsInput`.
2. **Options + builder:** comprehensive `ConsoleRunOptions` with
   `ToTerminalOptions`; `ConsoleApplicationBuilder`; `ConsoleApplication`;
   `Application.RunAsync`; color-depth and environment-size overrides;
   `Program.cs` migration; remove `ConsoleRun` and
   `Application.RunConsoleAsync`.
3. **Input read-model:** `PointerDevice`, `Application.Pointer`,
   `Application.HasFocus`.
4. **Protocol discovery:** `TerminalProtocol`, `ProtocolSupport`,
   `Capabilities.Support`/`Features`; documented inbound consumption.
5. **Output services + ordered writes:** the `_rendering`-gated out-of-band
   write path; `ITerminalServices`, `IBell`, `IClipboard`;
   `Application.Terminal`.
6. **Docs + `AGENTS.md` sync and a full quality-gate pass.**
