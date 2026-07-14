# Hosting & Public API Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use
> superpowers:subagent-driven-development (recommended) or
> superpowers:executing-plans to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make SharpVision's console hosting portable (Unix + Windows), its
options comprehensive and fluent, its live input state and supported protocols
easily consumable, and expose bell/clipboard/title output behind interfaces —
without changing any existing runtime behavior.

**Architecture:** The Terminal layer gains one portable `ConsoleHost.Open` seam
returning a `ConsoleConnection`; the SharpVision layer gains a comprehensive
`ConsoleRunOptions`, a fluent `ConsoleApplicationBuilder`, a
`ConsoleApplication` entry point, a grouped `PointerDevice` read-model on
`Application`, a named `TerminalProtocol` discovery facade on `Capabilities`,
and an `ITerminalServices` output facade driven by a single-writer out-of-band
write path that shares the renderer's `_rendering` gate.

**Tech Stack:** .NET 10, C# 14, xUnit v3, Shouldly, Microsoft Testing Platform.

## Global Constraints

- Target .NET 10 and C# 14. File-scoped namespaces; `var` for locals; `using`
  directives after the namespace; shared imports in `GlobalUsings.cs`.
- One public/named type per file, file named exactly after the type (no generic
  arity). No nested named types; no two types per file.
- Never use primary constructors or positional records. Declare every
  constructor explicitly and validate all arguments before assigning state.
- Immutable value types are `readonly struct` / `readonly record struct`. Prefer
  readonly structs for small immutable values; use a class for reference
  identity, polymorphism, shared mutable state, or disposal.
- Add XML documentation to every public and internal type and member; document
  every thrown exception. Use `Debug.Assert` for internal invariants only.
- Prefer contextual identifiers (`Capabilities`, not `TerminalCapabilities`,
  inside the terminal namespace). Prefer
  `Rune`/`Span<byte>`/`ReadOnlySpan<byte>`/`IBufferWriter<byte>` in protocol
  paths; never allocate strings in hot loops.
- Controls never emit escape bytes; out-of-band protocol writes are runtime
  code, not control code.
- Restore terminal modes in `finally` paths; cleanup failures must not hide the
  primary exception. Degrade unsupported features safely by default.
- Tests use xUnit v3, Shouldly, Arrange/Act/Assert, and
  `MethodName_WhenThis_ThatIsExpected` naming. Watch each new test fail for the
  expected reason before implementing. Prefer deterministic fakes for
  transport/resize/clock over mocks.
- Zero build warnings and zero errors. Before declaring a phase complete run
  `make format`, `make lint`, `make build`, `make test`.

**Spec:** `docs/superpowers/specs/2026-07-14-hosting-public-api-design.md`.

---

## Phase 0 — Baseline

### Task 0: Verify the green baseline

- [ ] **Step 1: Confirm clean tree and green gates**

Run:

```bash
git -C /Users/alex/Development/sharp-vision status --short
make build
make test
```

Expected: clean working tree; build with zero warnings/errors; tests pass at or
above the configured minimum. If anything fails, stop and resolve before
Phase 1.

---

## Phase 1 — Portable console host (Terminal layer)

**File structure for this phase (all under
`src/SharpVision.Terminal/Runtime/`):**

- Create `ConsoleHostOptions.cs` — host policy record (resize interval,
  control-key capture).
- Create `ConsoleConnection.cs` — the opened bundle (`Transport`, `Resize`, owns
  the restore lease).
- Create `WindowsConsoleMode.cs` — internal Windows `SetConsoleMode`
  save/enter/restore lease.
- Rename `ConsoleInputMode.cs` → `UnixConsoleMode.cs`, make it `internal`.
- Create `UnixConsoleHost.cs` and `WindowsConsoleHost.cs` — internal per-OS host
  strategies.
- Modify `ConsoleHost.cs` — becomes the portable `Open` façade.
- Modify `Native.cs` — add Windows console-mode P/Invoke.
- Modify `src/SharpVision/Runtime/Application.cs` — own and dispose the host
  restore lease.

### Task 1: `ConsoleHostOptions`

**Files:**

- Create: `src/SharpVision.Terminal/Runtime/ConsoleHostOptions.cs`
- Test: `tests/SharpVision.Terminal.Tests/Runtime/ConsoleHostOptionsTests.cs`

**Interfaces:**

- Produces a `sealed record ConsoleHostOptions`:

```csharp
public sealed record ConsoleHostOptions
{
    public TimeSpan ResizeInterval { get; init; } // default 100 ms
    public bool CaptureControlKeys { get; init; } // default false
}
```

- [ ] **Step 1: Write the failing test**

```csharp
// tests/SharpVision.Terminal.Tests/Runtime/ConsoleHostOptionsTests.cs
namespace SharpVision.Terminal.Tests.Runtime;

using SharpVision.Terminal.Runtime;

public sealed class ConsoleHostOptionsTests
{
    [Fact]
    public void Defaults_WhenConstructed_MatchDocumentedPolicy()
    {
        var options = new ConsoleHostOptions();

        options.ResizeInterval.ShouldBe(TimeSpan.FromMilliseconds(100));
        options.CaptureControlKeys.ShouldBeFalse();
    }

    [Fact]
    public void ResizeInterval_WhenNotPositive_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(
            () => new ConsoleHostOptions { ResizeInterval = TimeSpan.Zero });
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run:

```bash
dotnet test --project tests/SharpVision.Terminal.Tests --filter-class "*ConsoleHostOptionsTests" --timeout 60s
```

Expected: FAIL — `ConsoleHostOptions` does not exist.

- [ ] **Step 3: Write the implementation**

```csharp
// src/SharpVision.Terminal/Runtime/ConsoleHostOptions.cs
// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Runtime;

/// <summary>Configures how <see cref="ConsoleHost.Open"/> prepares the interactive console.</summary>
public sealed record ConsoleHostOptions
{
    /// <summary>Gets the positive finite poll interval for the cell-only resize fallback.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not positive and finite.</exception>
    public TimeSpan ResizeInterval
    {
        get;
        init
        {
            if (value <= TimeSpan.Zero || value == Timeout.InfiniteTimeSpan)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "The resize interval must be positive and finite.");
            }

            field = value;
        }
    } = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Gets whether control keys such as Ctrl+C are delivered as input rather than
    /// raising the host signal. Default is <see langword="false"/>.
    /// </summary>
    public bool CaptureControlKeys { get; init; }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run:

```bash
dotnet test --project tests/SharpVision.Terminal.Tests --filter-class "*ConsoleHostOptionsTests" --timeout 60s
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/SharpVision.Terminal/Runtime/ConsoleHostOptions.cs tests/SharpVision.Terminal.Tests/Runtime/ConsoleHostOptionsTests.cs
git commit -m "feat(terminal): add ConsoleHostOptions"
```

### Task 2: `ConsoleConnection`

**Files:**

- Create: `src/SharpVision.Terminal/Runtime/ConsoleConnection.cs`
- Test: `tests/SharpVision.Terminal.Tests/Runtime/ConsoleConnectionTests.cs`

**Interfaces:**

- Consumes: `ITransport` (Transport), `IResizeSource` (Resize) from
  `SharpVision.Terminal.Transport`/`Runtime`.
- Produces: `sealed class ConsoleConnection : IAsyncDisposable`, ctor
  `(ITransport transport, IResizeSource resize, IDisposable restore)`;
  properties `ITransport Transport`, `IResizeSource Resize`; `DisposeAsync()`
  disposes **only** the restore lease, once.

- [ ] **Step 1: Write the failing test**

```csharp
// tests/SharpVision.Terminal.Tests/Runtime/ConsoleConnectionTests.cs
namespace SharpVision.Terminal.Tests.Runtime;

using SharpVision.Terminal.Runtime;
using SharpVision.Terminal.Tests.Fakes; // FakeTransport, FakeResizeSource (existing helpers)

public sealed class ConsoleConnectionTests
{
    private sealed class TrackingRestore : IDisposable
    {
        public int Disposals { get; private set; }
        public void Dispose() => Disposals++;
    }

    [Fact]
    public void DisposeAsync_WhenCalledTwice_RestoresExactlyOnce()
    {
        var restore = new TrackingRestore();
        var connection = new ConsoleConnection(new FakeTransport(), new FakeResizeSource(), restore);

        connection.DisposeAsync().AsTask().Wait();
        connection.DisposeAsync().AsTask().Wait();

        restore.Disposals.ShouldBe(1);
    }

    [Fact]
    public void Constructor_WhenRestoreNull_Throws()
    {
        Should.Throw<ArgumentNullException>(
            () => new ConsoleConnection(new FakeTransport(), new FakeResizeSource(), restore: null!));
    }
}
```

> If `FakeTransport`/`FakeResizeSource` do not yet exist under
> `tests/SharpVision.Terminal.Tests/Fakes/`, create minimal ones that implement
> `ITransport`/`IResizeSource` with no-op async methods (search the test project
> first — the terminal tests already use transport/resize fakes; reuse them and
> adjust the `using`).

- [ ] **Step 2: Run the test to verify it fails**

Run:

```bash
dotnet test --project tests/SharpVision.Terminal.Tests --filter-class "*ConsoleConnectionTests" --timeout 60s
```

Expected: FAIL — `ConsoleConnection` does not exist.

- [ ] **Step 3: Write the implementation**

```csharp
// src/SharpVision.Terminal/Runtime/ConsoleConnection.cs
// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Runtime;

using SharpVision.Terminal.Transport;

/// <summary>
/// Bundles the transport and resize source opened for one interactive console and
/// owns the platform terminal-mode restore lease.
/// </summary>
/// <remarks>
/// The running session disposes <see cref="Transport"/> and <see cref="Resize"/>;
/// this connection restores the platform terminal mode when disposed, which the
/// host performs after the session's reverse mode cleanup.
/// </remarks>
public sealed class ConsoleConnection: IAsyncDisposable
{
    private readonly IDisposable _restore;
    private int _disposed;

    /// <summary>Initializes a connection over opened console resources.</summary>
    /// <param name="transport">The non-null transport over the console streams.</param>
    /// <param name="resize">The non-null resize source.</param>
    /// <param name="restore">The non-null platform terminal-mode restore lease.</param>
    /// <exception cref="ArgumentNullException">A required dependency is null.</exception>
    public ConsoleConnection(ITransport transport, IResizeSource resize, IDisposable restore)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(resize);
        ArgumentNullException.ThrowIfNull(restore);

        Transport = transport;
        Resize = resize;
        _restore = restore;
    }

    /// <summary>Gets the transport over the interactive console streams.</summary>
    public ITransport Transport { get; }

    /// <summary>Gets the resize source for the interactive console.</summary>
    public IResizeSource Resize { get; }

    /// <summary>Restores the platform terminal mode once. Never disposes transport or resize.</summary>
    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _restore.Dispose();
        }

        return ValueTask.CompletedTask;
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run:

```bash
dotnet test --project tests/SharpVision.Terminal.Tests --filter-class "*ConsoleConnectionTests" --timeout 60s
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/SharpVision.Terminal/Runtime/ConsoleConnection.cs tests/SharpVision.Terminal.Tests/Runtime/ConsoleConnectionTests.cs
git commit -m "feat(terminal): add ConsoleConnection bundle"
```

### Task 3: Windows console-mode P/Invoke

**Files:**

- Modify: `src/SharpVision.Terminal/Runtime/Native.cs`
- Test: `tests/SharpVision.Terminal.Tests/Runtime/NativeConsoleModeTests.cs`

**Interfaces:**

- Produces (internal on `Native`): `nint GetStdHandle(int which)`,
  `bool TryGetConsoleMode(nint handle, out uint mode)`,
  `bool TrySetConsoleMode(nint handle, uint mode)`, plus the mode-flag constants
  `EnableProcessedInput`, `EnableLineInput`, `EnableEchoInput`,
  `EnableVirtualTerminalInput`, `EnableProcessedOutput`,
  `EnableVirtualTerminalProcessing`, `DisableNewlineAutoReturn`, and handle ids
  `StdInputHandle = -10`, `StdOutputHandle = -11`. The pure bit-math helper
  `uint ComputeInputMode(uint current, bool captureControlKeys)` and
  `uint ComputeOutputMode(uint current)` are `internal static` so they are
  unit-testable without a real console.

- [ ] **Step 1: Write the failing test**

```csharp
// tests/SharpVision.Terminal.Tests/Runtime/NativeConsoleModeTests.cs
namespace SharpVision.Terminal.Tests.Runtime;

using SharpVision.Terminal.Runtime;

public sealed class NativeConsoleModeTests
{
    [Fact]
    public void ComputeInputMode_WhenDefault_EnablesVtInputAndClearsLineAndEcho()
    {
        uint current = Native.EnableProcessedInput | Native.EnableLineInput | Native.EnableEchoInput;

        uint result = Native.ComputeInputMode(current, captureControlKeys: false);

        (result & Native.EnableVirtualTerminalInput).ShouldNotBe(0u);
        (result & Native.EnableLineInput).ShouldBe(0u);
        (result & Native.EnableEchoInput).ShouldBe(0u);
        (result & Native.EnableProcessedInput).ShouldNotBe(0u); // signals still processed
    }

    [Fact]
    public void ComputeInputMode_WhenCapturingControlKeys_ClearsProcessedInput()
    {
        uint current = Native.EnableProcessedInput | Native.EnableLineInput | Native.EnableEchoInput;

        uint result = Native.ComputeInputMode(current, captureControlKeys: true);

        (result & Native.EnableProcessedInput).ShouldBe(0u);
        (result & Native.EnableVirtualTerminalInput).ShouldNotBe(0u);
    }

    [Fact]
    public void ComputeOutputMode_WhenDefault_EnablesVtProcessingAndDisablesAutoReturn()
    {
        uint result = Native.ComputeOutputMode(Native.EnableProcessedOutput);

        (result & Native.EnableVirtualTerminalProcessing).ShouldNotBe(0u);
        (result & Native.DisableNewlineAutoReturn).ShouldNotBe(0u);
        (result & Native.EnableProcessedOutput).ShouldNotBe(0u);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run:

```bash
dotnet test --project tests/SharpVision.Terminal.Tests --filter-class "*NativeConsoleModeTests" --timeout 60s
```

Expected: FAIL — the constants/helpers do not exist.

- [ ] **Step 3: Add the constants, bit-math helpers, and P/Invoke to `Native`**

Add to the `Native` partial class (after the existing `Ioctl` import). The
bit-math is deterministic and testable on any OS; the imports are only called on
Windows.

```csharp
    // Windows console-mode boundary. Bit-math is factored out so it is unit
    // testable without a real console handle.
    internal const int StdInputHandle = -10;
    internal const int StdOutputHandle = -11;

    internal const uint EnableProcessedInput = 0x0001;
    internal const uint EnableLineInput = 0x0002;
    internal const uint EnableEchoInput = 0x0004;
    internal const uint EnableVirtualTerminalInput = 0x0200;
    internal const uint EnableProcessedOutput = 0x0001;
    internal const uint EnableVirtualTerminalProcessing = 0x0004;
    internal const uint DisableNewlineAutoReturn = 0x0008;

    /// <summary>Computes the raw-input console mode from the saved mode.</summary>
    /// <param name="current">The saved console input mode.</param>
    /// <param name="captureControlKeys">Whether Ctrl+C is delivered as input.</param>
    /// <returns>The mode enabling VT input without canonical line editing or echo.</returns>
    internal static uint ComputeInputMode(uint current, bool captureControlKeys)
    {
        uint mode = current;
        mode &= ~(EnableLineInput | EnableEchoInput);
        mode |= EnableVirtualTerminalInput;

        if (captureControlKeys)
        {
            mode &= ~EnableProcessedInput;
        }
        else
        {
            mode |= EnableProcessedInput;
        }

        return mode;
    }

    /// <summary>Computes the VT-processing console output mode from the saved mode.</summary>
    /// <param name="current">The saved console output mode.</param>
    /// <returns>The mode enabling VT processing and disabling newline auto-return.</returns>
    internal static uint ComputeOutputMode(uint current) =>
        current | EnableProcessedOutput | EnableVirtualTerminalProcessing | DisableNewlineAutoReturn;

    /// <summary>Gets a standard console handle.</summary>
    /// <param name="which">The <see cref="StdInputHandle"/> or <see cref="StdOutputHandle"/> id.</param>
    /// <returns>The native handle.</returns>
    [SupportedOSPlatform("windows")]
    internal static nint GetStandardHandle(int which) => GetStdHandle(which);

    /// <summary>Reads a console mode.</summary>
    /// <param name="handle">The console handle.</param>
    /// <param name="mode">Receives the current mode on success.</param>
    /// <returns>True when the mode was read.</returns>
    [SupportedOSPlatform("windows")]
    internal static bool TryGetConsoleMode(nint handle, out uint mode) => GetConsoleMode(handle, out mode);

    /// <summary>Writes a console mode.</summary>
    /// <param name="handle">The console handle.</param>
    /// <param name="mode">The mode to apply.</param>
    /// <returns>True when the mode was applied.</returns>
    [SupportedOSPlatform("windows")]
    internal static bool TrySetConsoleMode(nint handle, uint mode) => SetConsoleMode(handle, mode);

    [LibraryImport("kernel32", EntryPoint = "GetStdHandle", SetLastError = true)]
    private static partial nint GetStdHandle(int which);

    [LibraryImport("kernel32", EntryPoint = "GetConsoleMode", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetConsoleMode(nint handle, out uint mode);

    [LibraryImport("kernel32", EntryPoint = "SetConsoleMode", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetConsoleMode(nint handle, uint mode);
```

Add `using System.Runtime.Versioning;` after the namespace in `Native.cs` if not
already present (it is used by `[SupportedOSPlatform]`).

- [ ] **Step 4: Run the test to verify it passes**

Run:

```bash
dotnet test --project tests/SharpVision.Terminal.Tests --filter-class "*NativeConsoleModeTests" --timeout 60s
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/SharpVision.Terminal/Runtime/Native.cs tests/SharpVision.Terminal.Tests/Runtime/NativeConsoleModeTests.cs
git commit -m "feat(terminal): add Windows console-mode native boundary"
```

### Task 4: `WindowsConsoleMode` (internal)

**Files:**

- Create: `src/SharpVision.Terminal/Runtime/WindowsConsoleMode.cs`
- Test: covered indirectly (Windows-only behavior); no macOS-runnable unit test.
  The mode math is tested in Task 3.

**Interfaces:**

- Consumes: `Native.GetStandardHandle`, `Native.TryGetConsoleMode`,
  `Native.TrySetConsoleMode`, `Native.ComputeInputMode`,
  `Native.ComputeOutputMode`.
- Produces: `internal sealed class WindowsConsoleMode : IDisposable`;
  `internal static WindowsConsoleMode Enter(bool captureControlKeys)`;
  `Dispose()` restores both saved modes once.

- [ ] **Step 1: Write the implementation**

```csharp
// src/SharpVision.Terminal/Runtime/WindowsConsoleMode.cs
// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Runtime;

using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

/// <summary>Owns one Windows console raw/VT mode lease with guaranteed restoration.</summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsConsoleMode: IDisposable
{
    private readonly nint _input;
    private readonly nint _output;
    private readonly uint _savedInput;
    private readonly uint _savedOutput;
    private int _disposed;

    private WindowsConsoleMode(nint input, nint output, uint savedInput, uint savedOutput)
    {
        _input = input;
        _output = output;
        _savedInput = savedInput;
        _savedOutput = savedOutput;
    }

    /// <summary>Saves the current console modes and enters VT input and VT processing.</summary>
    /// <param name="captureControlKeys">Whether Ctrl+C is delivered as input.</param>
    /// <returns>A lease that restores both saved modes when disposed.</returns>
    /// <exception cref="IOException">A console mode cannot be read or written.</exception>
    internal static WindowsConsoleMode Enter(bool captureControlKeys)
    {
        nint input = Native.GetStandardHandle(Native.StdInputHandle);
        nint output = Native.GetStandardHandle(Native.StdOutputHandle);

        if (!Native.TryGetConsoleMode(input, out uint savedInput) ||
            !Native.TryGetConsoleMode(output, out uint savedOutput))
        {
            throw Failure();
        }

        if (!Native.TrySetConsoleMode(input, Native.ComputeInputMode(savedInput, captureControlKeys)))
        {
            throw Failure();
        }

        if (!Native.TrySetConsoleMode(output, Native.ComputeOutputMode(savedOutput)))
        {
            _ = Native.TrySetConsoleMode(input, savedInput);
            throw Failure();
        }

        return new WindowsConsoleMode(input, output, savedInput, savedOutput);
    }

    /// <summary>Restores the saved input and output console modes once, best effort.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _ = Native.TrySetConsoleMode(_input, _savedInput);
            _ = Native.TrySetConsoleMode(_output, _savedOutput);
        }
    }

    private static IOException Failure() =>
        new("The Windows console mode could not be configured.",
            new Win32Exception(Marshal.GetLastPInvokeError()));
}
```

- [ ] **Step 2: Build to verify it compiles**

Run: `make build` Expected: zero warnings, zero errors.

- [ ] **Step 3: Commit**

```bash
git add src/SharpVision.Terminal/Runtime/WindowsConsoleMode.cs
git commit -m "feat(terminal): add Windows console raw/VT mode lease"
```

### Task 5: `UnixConsoleMode` (internalize `ConsoleInputMode`)

**Files:**

- Rename: `src/SharpVision.Terminal/Runtime/ConsoleInputMode.cs` →
  `src/SharpVision.Terminal/Runtime/UnixConsoleMode.cs`
- Modify: any references to `ConsoleInputMode` (grep below).
- Test: existing `ConsoleInputMode` tests, if present, are renamed with it.

**Interfaces:**

- Produces: `internal sealed class UnixConsoleMode : IDisposable`;
  `internal static UnixConsoleMode Enter(bool captureControlKeys)` (drops `isig`
  from the `stty` arguments when `captureControlKeys` is true); `Dispose()`
  restores.

- [ ] **Step 1: Find references**

Run:

```bash
grep -rn "ConsoleInputMode" --include='*.cs' src tests | grep -v -E "obj/|bin/"
```

Expected: `Application.Console.cs` (removed in Task 11), the type file, and
possibly a test file.

- [ ] **Step 2: Rename the file and type, make it internal, add the parameter**

Rename the file to `UnixConsoleMode.cs`. Change the class to
`internal sealed class UnixConsoleMode`, rename the private constructor, and
change `Enter()` to
`internal static UnixConsoleMode Enter(bool captureControlKeys)`. In `Enter`,
build the `stty` argument list so `isig` is included only when control keys are
**not** captured:

```csharp
    internal static UnixConsoleMode Enter(bool captureControlKeys)
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return new UnixConsoleMode(restore: null);
        }

        string restore = Run("-g").Trim();

        try
        {
            _ = captureControlKeys
                ? Run("raw", "-echo")
                : Run("raw", "-echo", "isig");
            return new UnixConsoleMode(restore);
        }
        catch
        {
            TryRestore(restore);
            throw;
        }
    }
```

Keep the rest of the type (the `Run`/`TryRestore` process boundary and
`Dispose`) unchanged except the type name.

- [ ] **Step 3: Update the sole current caller only enough to compile**

In `src/SharpVision/Runtime/Application.Console.cs`, replace
`ConsoleInputMode.Enter()` with
`UnixConsoleMode.Enter(captureControlKeys: false)` **temporarily** (this file is
deleted in Task 11). If a test references `ConsoleInputMode`, rename it to
`UnixConsoleMode` and adjust for the new parameter.

- [ ] **Step 4: Build and run the affected tests**

Run:

```bash
make build
dotnet test --project tests/SharpVision.Terminal.Tests --filter-class "*ConsoleMode*" --timeout 60s
```

Expected: builds clean; any renamed mode tests pass.

- [ ] **Step 5: Commit**

```bash
git add -A src/SharpVision.Terminal/Runtime tests/SharpVision.Terminal.Tests src/SharpVision/Runtime/Application.Console.cs
git commit -m "refactor(terminal): internalize Unix console mode as UnixConsoleMode"
```

### Task 6: Per-OS hosts and the `ConsoleHost.Open` façade

**Files:**

- Create: `src/SharpVision.Terminal/Runtime/UnixConsoleHost.cs`
- Create: `src/SharpVision.Terminal/Runtime/WindowsConsoleHost.cs`
- Modify: `src/SharpVision.Terminal/Runtime/ConsoleHost.cs`
- Test: `tests/SharpVision.Terminal.Tests/Runtime/ConsoleHostTests.cs`

**Interfaces:**

- Consumes: `ConsoleHostOptions`, `ConsoleConnection`, `UnixConsoleMode`,
  `WindowsConsoleMode`, `StreamTransport`, `UnixResizeSource`,
  `ConsoleResizeSource`.
- Produces: `ConsoleHost.IsInteractive` (unchanged),
  `static ConsoleConnection ConsoleHost.Open(ConsoleHostOptions options)`.
  Internal `UnixConsoleHost.Open(options)` and
  `WindowsConsoleHost.Open(options)` each return a `ConsoleConnection`.

- [ ] **Step 1: Write the failing test**

```csharp
// tests/SharpVision.Terminal.Tests/Runtime/ConsoleHostTests.cs
namespace SharpVision.Terminal.Tests.Runtime;

using SharpVision.Terminal.Runtime;

public sealed class ConsoleHostTests
{
    [Fact]
    public void Open_WhenOptionsNull_Throws()
    {
        Should.Throw<ArgumentNullException>(() => ConsoleHost.Open(options: null!));
    }
}
```

> Opening a real console is environment-dependent, so the runnable unit test is
> limited to argument validation. The Unix open/restore path is exercised by the
> end-to-end showcase test that drives `ConsoleApplication` (Task 11) in
> interactive CI; the Windows path is validated on Windows hardware/CI per the
> spec.

- [ ] **Step 2: Run the test to verify it fails**

Run:

```bash
dotnet test --project tests/SharpVision.Terminal.Tests --filter-class "*ConsoleHostTests" --timeout 60s
```

Expected: FAIL — `Open` does not exist.

- [ ] **Step 3: Write `UnixConsoleHost`**

```csharp
// src/SharpVision.Terminal/Runtime/UnixConsoleHost.cs
// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Runtime;

using System.Runtime.Versioning;

using SharpVision.Terminal.Transport;

/// <summary>Opens an interactive console on Linux and macOS with SIGWINCH pixel resize.</summary>
[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
internal static class UnixConsoleHost
{
    /// <summary>Enters raw mode and opens tty streams, a SIGWINCH resize source, and a restore lease.</summary>
    /// <param name="options">The validated host policy.</param>
    /// <returns>A connection whose disposal restores the terminal input mode.</returns>
    /// <exception cref="IOException">Raw mode or the tty streams cannot be prepared.</exception>
    internal static ConsoleConnection Open(ConsoleHostOptions options)
    {
        UnixConsoleMode mode = UnixConsoleMode.Enter(options.CaptureControlKeys);

        try
        {
            var input = new FileStream(
                "/dev/tty",
                new FileStreamOptions
                {
                    Access = FileAccess.Read,
                    Mode = FileMode.Open,
                    Options = FileOptions.Asynchronous,
                    Share = FileShare.ReadWrite,
                    BufferSize = 1,
                });
            Stream output = Console.OpenStandardOutput();
            var transport = new StreamTransport(input, output, leaveOpen: true);

            // The tty read descriptor answers TIOCGWINSZ, giving cell and pixel
            // dimensions and SIGWINCH-driven resize rather than cell-only polling.
            int descriptor = (int) input.SafeFileHandle.DangerousGetHandle();
            var resize = new UnixResizeSource(descriptor);

            return new ConsoleConnection(transport, resize, mode);
        }
        catch
        {
            mode.Dispose();
            throw;
        }
    }
}
```

- [ ] **Step 4: Write `WindowsConsoleHost`**

```csharp
// src/SharpVision.Terminal/Runtime/WindowsConsoleHost.cs
// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Runtime;

using System.Runtime.Versioning;

using SharpVision.Terminal.Transport;

/// <summary>Opens an interactive console on Windows using VT input and VT processing.</summary>
[SupportedOSPlatform("windows")]
internal static class WindowsConsoleHost
{
    /// <summary>Enters VT console mode and opens standard streams and a polling resize source.</summary>
    /// <param name="options">The validated host policy.</param>
    /// <returns>A connection whose disposal restores the saved console modes.</returns>
    /// <exception cref="IOException">The console mode cannot be configured.</exception>
    internal static ConsoleConnection Open(ConsoleHostOptions options)
    {
        WindowsConsoleMode mode = WindowsConsoleMode.Enter(options.CaptureControlKeys);

        try
        {
            Stream input = Console.OpenStandardInput(bufferSize: 1);
            Stream output = Console.OpenStandardOutput();
            var transport = new StreamTransport(input, output, leaveOpen: true);

            // The standard Windows console does not report pixel dimensions, so
            // resize is cell-only polling.
            var resize = new ConsoleResizeSource(options.ResizeInterval);

            return new ConsoleConnection(transport, resize, mode);
        }
        catch
        {
            mode.Dispose();
            throw;
        }
    }
}
```

- [ ] **Step 5: Rewrite `ConsoleHost` as the portable façade**

```csharp
// src/SharpVision.Terminal/Runtime/ConsoleHost.cs
// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Runtime;

/// <summary>Opens interactive console streams for a SharpVision application host.</summary>
public static class ConsoleHost
{
    /// <summary>Gets whether standard input and output are attached to an interactive console.</summary>
    public static bool IsInteractive =>
        !Console.IsInputRedirected && !Console.IsOutputRedirected;

    /// <summary>Opens the interactive console for the current platform.</summary>
    /// <param name="options">The non-null host policy.</param>
    /// <returns>A connection exposing the transport and resize source and owning the restore lease.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    /// <exception cref="PlatformNotSupportedException">The current platform is not supported.</exception>
    /// <exception cref="IOException">The console cannot enter raw or VT mode.</exception>
    public static ConsoleConnection Open(ConsoleHostOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            return UnixConsoleHost.Open(options);
        }

        if (OperatingSystem.IsWindows())
        {
            return WindowsConsoleHost.Open(options);
        }

        throw new PlatformNotSupportedException(
            "Interactive console hosting is supported only on Linux, macOS, and Windows.");
    }
}
```

- [ ] **Step 6: Run the test and build**

Run:

```bash
dotnet test --project tests/SharpVision.Terminal.Tests --filter-class "*ConsoleHostTests" --timeout 60s
make build
```

Expected: PASS; clean build.

- [ ] **Step 7: Commit**

```bash
git add src/SharpVision.Terminal/Runtime/UnixConsoleHost.cs src/SharpVision.Terminal/Runtime/WindowsConsoleHost.cs src/SharpVision.Terminal/Runtime/ConsoleHost.cs tests/SharpVision.Terminal.Tests/Runtime/ConsoleHostTests.cs
git commit -m "feat(terminal): portable ConsoleHost.Open with per-OS strategies"
```

### Task 7: `Application` owns the host restore lease

**Files:**

- Modify: `src/SharpVision/Runtime/Application.cs`
- Test: `tests/SharpVision.Tests/Runtime/ApplicationHostLeaseTests.cs`

**Interfaces:**

- Produces: new `Application` constructor parameter
  `IAsyncDisposable? hostLease = null`, disposed exactly once after session
  cleanup.

- [ ] **Step 1: Write the failing test**

```csharp
// tests/SharpVision.Tests/Runtime/ApplicationHostLeaseTests.cs
namespace SharpVision.Tests.Runtime;

using SharpVision.Controls;
using SharpVision.Runtime;
using SharpVision.Tests.Fakes; // FakeTransport, FakeResizeSource used elsewhere in this project

public sealed class ApplicationHostLeaseTests
{
    private sealed class TrackingLease : IAsyncDisposable
    {
        public int Disposals { get; private set; }
        public ValueTask DisposeAsync() { Disposals++; return ValueTask.CompletedTask; }
    }

    [Fact]
    public async Task DisposeAsync_WhenNeverStarted_DisposesHostLeaseOnce()
    {
        var lease = new TrackingLease();
        var app = new Application(
            new Border(), new FakeTransport(), new FakeResizeSource(), options: null, hostLease: lease);

        await app.DisposeAsync();

        lease.Disposals.ShouldBe(1);
    }
}
```

> Reuse the fakes the `SharpVision.Tests` project already uses to construct an
> `Application` in its runtime tests. If none exist, create minimal
> `FakeTransport : ITransport` and `FakeResizeSource : IResizeSource` that block
> on read/resize until cancelled.

- [ ] **Step 2: Run the test to verify it fails**

Run:

```bash
dotnet test --project tests/SharpVision.Tests --filter-class "*ApplicationHostLeaseTests" --timeout 60s
```

Expected: FAIL — the constructor has no `hostLease` parameter.

- [ ] **Step 3: Add the field and constructor parameter**

In `Application.cs`, add a field near the other readonly runtime fields (around
line 37):

```csharp
    private readonly IAsyncDisposable? _hostLease;
    private int _hostLeaseDisposed;
```

Change the constructor signature (line 73) and assign the field after `_options`
is set (after line 95):

```csharp
    public Application(
        Control root,
        ITransport transport,
        IResizeSource resize,
        TerminalOptions? options = null,
        IAsyncDisposable? hostLease = null)
    {
        // ... existing validation unchanged ...
        _options = options ?? new TerminalOptions();
        _hostLease = hostLease;
        // ... rest unchanged ...
    }
```

Update the constructor XML doc to add a `hostLease` `<param>` entry: "An
optional host resource disposed last after cleanup, or null."

- [ ] **Step 4: Dispose the lease after session cleanup**

Add a helper and call it from both shutdown paths. Add the method near
`FinishWithoutSessionAsync` (around line 692):

```csharp
    private async ValueTask DisposeHostLeaseAsync()
    {
        if (_hostLease is not null && Interlocked.Exchange(ref _hostLeaseDisposed, 1) == 0)
        {
            await _hostLease.DisposeAsync().ConfigureAwait(false);
        }
    }
```

In `ObserveSessionAsync`, after `await _session.DisposeAsync();` (line 853) and
before `await Dispatcher.InvokeAsync(FinalizeStopped);`:

```csharp
        await _session.DisposeAsync();
        await DisposeHostLeaseAsync();
        await Dispatcher.InvokeAsync(FinalizeStopped);
```

In `FinishWithoutSessionAsync` (line 692), after
`await _session.DisposeAsync();`:

```csharp
    private async Task FinishWithoutSessionAsync()
    {
        await _session.DisposeAsync();
        await DisposeHostLeaseAsync();
        await Dispatcher.InvokeAsync(FinalizeStopped);
    }
```

- [ ] **Step 5: Run the test and the full runtime suite**

Run:

```bash
dotnet test --project tests/SharpVision.Tests --filter-class "*ApplicationHostLeaseTests" --timeout 60s
dotnet test --project tests/SharpVision.Tests --filter-class "*ApplicationTests" --timeout 120s
```

Expected: PASS; existing `Application` behavior unchanged.

- [ ] **Step 6: Commit**

```bash
git add src/SharpVision/Runtime/Application.cs tests/SharpVision.Tests/Runtime/ApplicationHostLeaseTests.cs
git commit -m "feat(runtime): Application owns and disposes the console host lease"
```

---

## Phase 2 — Comprehensive options, fluent builder, entry point

**File structure for this phase:**

- Modify `src/SharpVision/Runtime/ConsoleRunOptions.cs` — comprehensive record +
  `ToTerminalOptions`.
- Create `src/SharpVision/Runtime/ConsoleApplicationBuilder.cs` — fluent
  builder.
- Create `src/SharpVision/Runtime/ConsoleApplication.cs` — static entry point.
- Modify `src/SharpVision/Runtime/Application.cs` — add instance `RunAsync`.
- Delete `src/SharpVision/Runtime/ConsoleRun.cs` and
  `src/SharpVision/Runtime/Application.Console.cs`.
- Modify `src/SharpVision.Showcase/Program.cs` — use `ConsoleApplication`.

### Task 8: Comprehensive `ConsoleRunOptions` + `ToTerminalOptions`

**Files:**

- Modify: `src/SharpVision/Runtime/ConsoleRunOptions.cs`
- Test: `tests/SharpVision.Tests/Runtime/ConsoleRunOptionsTests.cs`

**Interfaces:**

- Consumes: `Theme`, `Themes`, `MouseTracking`, `MouseCoordinates`,
  `Enhancement`, `Capabilities`, `ColorDepth`, `NegotiationOptions`, `Detector`,
  `Settings`.
- Produces: `sealed record ConsoleRunOptions` with the property set from the
  spec; `TerminalOptions ToTerminalOptions()`;
  `ConsoleHostOptions ToHostOptions()`; `Theme ResolveTheme()`.

- [ ] **Step 1: Write the failing test**

```csharp
// tests/SharpVision.Tests/Runtime/ConsoleRunOptionsTests.cs
namespace SharpVision.Tests.Runtime;

using SharpVision.Runtime;
using SharpVision.Styling;
using SharpVision.Terminal.Protocols;

public sealed class ConsoleRunOptionsTests
{
    [Fact]
    public void Defaults_WhenConstructed_ReproduceInteractiveConsolePolicy()
    {
        var options = new ConsoleRunOptions();

        options.AlternateScreen.ShouldBeTrue();
        options.ShowCursor.ShouldBeFalse();
        options.MouseTracking.ShouldBe(MouseTracking.Any);
        options.MouseCoordinates.ShouldBe(MouseCoordinates.Sgr);
        options.BracketedPaste.ShouldBeTrue();
        options.FocusReporting.ShouldBeTrue();
        options.TreatControlCAsInput.ShouldBeFalse();
    }

    [Fact]
    public void ToTerminalOptions_WhenMouseDisabled_LeavesTrackingNull()
    {
        var options = new ConsoleRunOptions { MouseTracking = null };

        TerminalOptions terminal = options.ToTerminalOptions();

        terminal.Tracking.ShouldBeNull();
    }

    [Fact]
    public void ToHostOptions_WhenControlCAsInput_CapturesControlKeys()
    {
        var options = new ConsoleRunOptions { TreatControlCAsInput = true };

        options.ToHostOptions().CaptureControlKeys.ShouldBeTrue();
    }

    [Fact]
    public void ResolveTheme_WhenThemeNull_ReturnsDark()
    {
        new ConsoleRunOptions().ResolveTheme().ShouldBe(Themes.Dark);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run:

```bash
dotnet test --project tests/SharpVision.Tests --filter-class "*ConsoleRunOptionsTests" --timeout 60s
```

Expected: FAIL — properties/methods do not exist.

- [ ] **Step 3: Rewrite `ConsoleRunOptions`**

```csharp
// src/SharpVision/Runtime/ConsoleRunOptions.cs
// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Runtime;

using SharpVision.Styling;
using SharpVision.Terminal.Capabilities;
using SharpVision.Terminal.Protocols;

using TerminalCapabilities = Terminal.Capabilities.Capabilities;
using TerminalOptions = Terminal.Runtime.Options;

/// <summary>Configures one interactive console screen run and the terminal policy it produces.</summary>
public sealed record ConsoleRunOptions
{
    /// <summary>Gets the theme published to the tree, or null for <see cref="Themes.Dark"/>.</summary>
    public Theme? Theme { get; init; }

    /// <summary>Gets whether to enter the alternate screen. Default is true.</summary>
    public bool AlternateScreen { get; init; } = true;

    /// <summary>Gets whether the cursor stays visible. Default is false.</summary>
    public bool ShowCursor { get; init; }

    /// <summary>Gets the mouse tracking level, or null to disable mouse input. Default is <see cref="MouseTracking.Any"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    public MouseTracking? MouseTracking
    {
        get;
        init
        {
            if (value.HasValue && !Enum.IsDefined(value.Value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The mouse tracking level is unknown.");
            }

            field = value;
        }
    } = Protocols.MouseTracking.Any;

    /// <summary>Gets the mouse coordinate encoding. Default is <see cref="MouseCoordinates.Sgr"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    public MouseCoordinates MouseCoordinates
    {
        get;
        init
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The mouse coordinate encoding is unknown.");
            }

            field = value;
        }
    } = MouseCoordinates.Sgr;

    /// <summary>Gets whether bracketed paste is enabled. Default is true.</summary>
    public bool BracketedPaste { get; init; } = true;

    /// <summary>Gets whether focus reporting is enabled. Default is true.</summary>
    public bool FocusReporting { get; init; } = true;

    /// <summary>Gets the Kitty keyboard flags to push when supported, or null to disable.</summary>
    public Enhancement? KeyboardEnhancement { get; init; } =
        Enhancement.Disambiguate | Enhancement.EventTypes;

    /// <summary>Gets an explicit capability profile, or null to detect and negotiate.</summary>
    public TerminalCapabilities? Capabilities { get; init; }

    /// <summary>Gets an explicit color depth override, or null for the detected depth.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    public ColorDepth? ColorDepth
    {
        get;
        init
        {
            if (value.HasValue && !Enum.IsDefined(value.Value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The color depth is unknown.");
            }

            field = value;
        }
    }

    /// <summary>Gets a negotiation override, or null for default startup negotiation.</summary>
    public NegotiationOptions? Negotiation { get; init; }

    /// <summary>Gets the positive finite reverse-cleanup timeout. Default is one second.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not positive and finite.</exception>
    public TimeSpan CleanupTimeout
    {
        get;
        init
        {
            if (value <= TimeSpan.Zero || value == Timeout.InfiniteTimeSpan)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The cleanup timeout must be positive and finite.");
            }

            field = value;
        }
    } = TimeSpan.FromSeconds(1);

    /// <summary>Gets the positive transport read-buffer size. Default is 16 KiB.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not positive.</exception>
    public int ReadBufferSize
    {
        get;
        init => field = value > 0
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), value, "The read buffer size must be positive.");
    } = 16 * 1024;

    /// <summary>Gets the positive finite resize poll interval. Default is 100 ms.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not positive and finite.</exception>
    public TimeSpan ResizeInterval
    {
        get;
        init
        {
            if (value <= TimeSpan.Zero || value == Timeout.InfiniteTimeSpan)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The resize interval must be positive and finite.");
            }

            field = value;
        }
    } = TimeSpan.FromMilliseconds(100);

    /// <summary>Gets whether Ctrl+C is delivered as input rather than requesting shutdown. Default is false.</summary>
    public bool TreatControlCAsInput { get; init; }

    /// <summary>Gets whether the LINES and COLUMNS environment variables override the initial size. Default is false.</summary>
    public bool UseEnvironmentSizeOverrides { get; init; }

    /// <summary>Gets the optional message written when standard input or output is redirected.</summary>
    public string? RedirectedMessage { get; init; }

    /// <summary>Resolves the theme, defaulting to <see cref="Themes.Dark"/>.</summary>
    /// <returns>The theme to publish.</returns>
    public Theme ResolveTheme() => Theme ?? Themes.Dark;

    /// <summary>Builds the host policy for <see cref="Terminal.Runtime.ConsoleHost.Open"/>.</summary>
    /// <returns>The validated host options.</returns>
    public Terminal.Runtime.ConsoleHostOptions ToHostOptions() => new()
    {
        ResizeInterval = ResizeInterval,
        CaptureControlKeys = TreatControlCAsInput,
    };

    /// <summary>Builds the terminal session policy from these options.</summary>
    /// <returns>The validated terminal options.</returns>
    public TerminalOptions ToTerminalOptions()
    {
        TerminalCapabilities capabilities = ResolveCapabilities();

        return new TerminalOptions
        {
            Capabilities = capabilities,
            Negotiation = Capabilities is null ? Negotiation ?? DefaultNegotiation() : null,
            AlternateScreen = AlternateScreen,
            HideCursor = !ShowCursor,
            Focus = FocusReporting,
            Paste = BracketedPaste,
            Tracking = MouseTracking,
            Coordinates = MouseCoordinates,
            Keyboard = KeyboardEnhancement,
            CleanupTimeout = CleanupTimeout,
            ReadBufferSize = ReadBufferSize,
        };
    }

    private TerminalCapabilities ResolveCapabilities()
    {
        if (Capabilities is { } profile)
        {
            return ColorDepth is { } depth ? profile with { ColorDepth = depth } : profile;
        }

        var overrides = new Settings { CellMouse = true };
        TerminalCapabilities detected = Detector.Detect(new Dictionary<string, string?>(), overrides: overrides);
        return ColorDepth is { } forced ? detected with { ColorDepth = forced } : detected;
    }

    private static NegotiationOptions DefaultNegotiation()
    {
        var environment = new Dictionary<string, string?>(StringComparer.Ordinal);

        foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            if (entry.Key is string key)
            {
                environment[key] = entry.Value?.ToString();
            }
        }

        return new NegotiationOptions(environment, new Settings { CellMouse = true });
    }
}
```

> Note on the `MouseTracking` property: the property name and the enum type
> collide, so the initializer qualifies the enum as
> `Protocols.MouseTracking.Any`. Confirm the enum's namespace alias — the file
> imports `SharpVision.Terminal.Protocols`, where `MouseTracking` lives. If the
> build reports ambiguity, use the fully qualified
> `SharpVision.Terminal.Protocols.MouseTracking.Any`.

- [ ] **Step 4: Run the test to verify it passes**

Run:

```bash
dotnet test --project tests/SharpVision.Tests --filter-class "*ConsoleRunOptionsTests" --timeout 60s
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/SharpVision/Runtime/ConsoleRunOptions.cs tests/SharpVision.Tests/Runtime/ConsoleRunOptionsTests.cs
git commit -m "feat(runtime): comprehensive ConsoleRunOptions with terminal/host mapping"
```

### Task 9: `ConsoleApplicationBuilder`

**Files:**

- Create: `src/SharpVision/Runtime/ConsoleApplicationBuilder.cs`
- Test: `tests/SharpVision.Tests/Runtime/ConsoleApplicationBuilderTests.cs`

**Interfaces:**

- Consumes: `ConsoleRunOptions`, `Screen`, `Application`, `ConsoleHost.Open`,
  `ConsoleHost.IsInteractive`.
- Produces: `sealed class ConsoleApplicationBuilder` with fluent setters (each
  returns `this`), `Application Build()`, and
  `ValueTask<ConsoleRunStatus> RunAsync(CancellationToken)`. A read-only
  `Options` property exposes the accumulated `ConsoleRunOptions` for testing.

- [ ] **Step 1: Write the failing test**

```csharp
// tests/SharpVision.Tests/Runtime/ConsoleApplicationBuilderTests.cs
namespace SharpVision.Tests.Runtime;

using SharpVision.Runtime;
using SharpVision.Showcase;
using SharpVision.Terminal.Protocols;

public sealed class ConsoleApplicationBuilderTests
{
    [Fact]
    public void FluentSetters_WhenChained_AccumulateOntoOptions()
    {
        var builder = ConsoleApplication.CreateBuilder(new Gallery())
            .UseAlternateScreen(false)
            .WithoutMouse()
            .TreatControlCAsInput();

        builder.Options.AlternateScreen.ShouldBeFalse();
        builder.Options.MouseTracking.ShouldBeNull();
        builder.Options.TreatControlCAsInput.ShouldBeTrue();
    }

    [Fact]
    public void UseMouse_WhenGivenLevel_SetsTrackingAndCoordinates()
    {
        var builder = ConsoleApplication.CreateBuilder(new Gallery())
            .UseMouse(MouseTracking.Press, MouseCoordinates.Pixel);

        builder.Options.MouseTracking.ShouldBe(MouseTracking.Press);
        builder.Options.MouseCoordinates.ShouldBe(MouseCoordinates.Pixel);
    }
}
```

> `Gallery` is the showcase `Screen`. If it is not yet a `Screen` subtype at
> this point, substitute a minimal local `sealed class TestScreen : Screen` with
> a trivial `Build()` returning `new Border()`.

- [ ] **Step 2: Run the test to verify it fails**

Run:

```bash
dotnet test --project tests/SharpVision.Tests --filter-class "*ConsoleApplicationBuilderTests" --timeout 60s
```

Expected: FAIL — the builder does not exist.

- [ ] **Step 3: Write the builder**

```csharp
// src/SharpVision/Runtime/ConsoleApplicationBuilder.cs
// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Runtime;

using SharpVision.Controls;
using SharpVision.Styling;
using SharpVision.Terminal.Capabilities;
using SharpVision.Terminal.Protocols;
using SharpVision.Terminal.Runtime;

using TerminalCapabilities = Terminal.Capabilities.Capabilities;

/// <summary>Configures and builds one interactive console <see cref="Application"/> fluently.</summary>
public sealed class ConsoleApplicationBuilder
{
    private readonly Screen _screen;
    private ConsoleRunOptions _options = new();

    /// <summary>Initializes a builder for one detached screen.</summary>
    /// <param name="screen">The non-null detached screen.</param>
    /// <exception cref="ArgumentNullException"><paramref name="screen"/> is null.</exception>
    public ConsoleApplicationBuilder(Screen screen)
    {
        ArgumentNullException.ThrowIfNull(screen);
        _screen = screen;
    }

    /// <summary>Gets the accumulated run options.</summary>
    public ConsoleRunOptions Options => _options;

    /// <summary>Publishes a theme to the tree.</summary>
    /// <param name="theme">The non-null theme.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="theme"/> is null.</exception>
    public ConsoleApplicationBuilder UseTheme(Theme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);
        _options = _options with { Theme = theme };
        return this;
    }

    /// <summary>Sets whether to enter the alternate screen.</summary>
    public ConsoleApplicationBuilder UseAlternateScreen(bool enabled = true)
    {
        _options = _options with { AlternateScreen = enabled };
        return this;
    }

    /// <summary>Sets whether the cursor stays visible.</summary>
    public ConsoleApplicationBuilder ShowCursor(bool visible = true)
    {
        _options = _options with { ShowCursor = visible };
        return this;
    }

    /// <summary>Enables mouse input at the given level and coordinate encoding.</summary>
    public ConsoleApplicationBuilder UseMouse(
        MouseTracking tracking = MouseTracking.Any,
        MouseCoordinates coordinates = MouseCoordinates.Sgr)
    {
        _options = _options with { MouseTracking = tracking, MouseCoordinates = coordinates };
        return this;
    }

    /// <summary>Disables mouse input.</summary>
    public ConsoleApplicationBuilder WithoutMouse()
    {
        _options = _options with { MouseTracking = null };
        return this;
    }

    /// <summary>Sets whether bracketed paste is enabled.</summary>
    public ConsoleApplicationBuilder UseBracketedPaste(bool enabled = true)
    {
        _options = _options with { BracketedPaste = enabled };
        return this;
    }

    /// <summary>Sets whether focus reporting is enabled.</summary>
    public ConsoleApplicationBuilder UseFocusReporting(bool enabled = true)
    {
        _options = _options with { FocusReporting = enabled };
        return this;
    }

    /// <summary>Sets the Kitty keyboard enhancement flags, or null to disable.</summary>
    public ConsoleApplicationBuilder UseKeyboardEnhancement(Enhancement? enhancement)
    {
        _options = _options with { KeyboardEnhancement = enhancement };
        return this;
    }

    /// <summary>Overrides the color depth.</summary>
    public ConsoleApplicationBuilder UseColorDepth(ColorDepth depth)
    {
        _options = _options with { ColorDepth = depth };
        return this;
    }

    /// <summary>Forces the minimal monochrome color depth.</summary>
    public ConsoleApplicationBuilder WithoutColors()
    {
        _options = _options with { ColorDepth = SharpVision.Terminal.Capabilities.ColorDepth.NoColor };
        return this;
    }

    /// <summary>Overrides the capability profile, bypassing detection.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="capabilities"/> is null.</exception>
    public ConsoleApplicationBuilder UseCapabilities(TerminalCapabilities capabilities)
    {
        ArgumentNullException.ThrowIfNull(capabilities);
        _options = _options with { Capabilities = capabilities };
        return this;
    }

    /// <summary>Overrides startup negotiation.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="negotiation"/> is null.</exception>
    public ConsoleApplicationBuilder UseNegotiation(NegotiationOptions negotiation)
    {
        ArgumentNullException.ThrowIfNull(negotiation);
        _options = _options with { Negotiation = negotiation };
        return this;
    }

    /// <summary>Disables startup negotiation.</summary>
    public ConsoleApplicationBuilder WithoutNegotiation()
    {
        _options = _options with { Negotiation = null, Capabilities = _options.Capabilities ?? TerminalCapabilities.Conservative };
        return this;
    }

    /// <summary>Sets the reverse-cleanup timeout.</summary>
    public ConsoleApplicationBuilder WithCleanupTimeout(TimeSpan timeout)
    {
        _options = _options with { CleanupTimeout = timeout };
        return this;
    }

    /// <summary>Sets the transport read-buffer size.</summary>
    public ConsoleApplicationBuilder WithReadBufferSize(int size)
    {
        _options = _options with { ReadBufferSize = size };
        return this;
    }

    /// <summary>Sets the resize poll interval.</summary>
    public ConsoleApplicationBuilder WithResizeInterval(TimeSpan interval)
    {
        _options = _options with { ResizeInterval = interval };
        return this;
    }

    /// <summary>Delivers Ctrl+C as input instead of requesting shutdown.</summary>
    public ConsoleApplicationBuilder TreatControlCAsInput(bool enabled = true)
    {
        _options = _options with { TreatControlCAsInput = enabled };
        return this;
    }

    /// <summary>Honors the LINES and COLUMNS environment variables for the initial size.</summary>
    public ConsoleApplicationBuilder UseEnvironmentSizeOverrides(bool enabled = true)
    {
        _options = _options with { UseEnvironmentSizeOverrides = enabled };
        return this;
    }

    /// <summary>Sets the message written when the console is redirected.</summary>
    public ConsoleApplicationBuilder WithRedirectedMessage(string? message)
    {
        _options = _options with { RedirectedMessage = message };
        return this;
    }

    /// <summary>Replaces the accumulated options wholesale.</summary>
    /// <param name="configure">The non-null transform over the current options.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="configure"/> is null.</exception>
    public ConsoleApplicationBuilder ConfigureOptions(Func<ConsoleRunOptions, ConsoleRunOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        _options = configure(_options) ?? throw new InvalidOperationException("ConfigureOptions returned null.");
        return this;
    }

    /// <summary>Opens the console and builds a wired application for advanced lifecycle control.</summary>
    /// <returns>The application; the caller runs and disposes it.</returns>
    /// <exception cref="IOException">The console is not interactive or cannot enter raw mode.</exception>
    public Application Build()
    {
        if (!ConsoleHost.IsInteractive)
        {
            throw new IOException("The console host is not interactive.");
        }

        ConsoleConnection connection = ConsoleHost.Open(_options.ToHostOptions());

        try
        {
            var application = new Application(
                _screen,
                connection.Transport,
                connection.Resize,
                _options.ToTerminalOptions(),
                hostLease: connection);
            _screen.Attach(application);
            application.Theme = _options.ResolveTheme();
            return application;
        }
        catch
        {
            connection.DisposeAsync().AsTask().GetAwaiter().GetResult();
            connection.Transport.DisposeAsync().AsTask().GetAwaiter().GetResult();
            connection.Resize.DisposeAsync().AsTask().GetAwaiter().GetResult();
            throw;
        }
    }

    /// <summary>Runs the console lifecycle to completion and reports the outcome.</summary>
    /// <param name="cancellationToken">Cancels the caller's wait.</param>
    /// <returns>The run status.</returns>
    public ValueTask<ConsoleRunStatus> RunAsync(CancellationToken cancellationToken = default) =>
        ConsoleApplication.RunCoreAsync(this, cancellationToken);
}
```

> `ColorDepth.NoColor` and the `Screen.Attach(Application)` signature: confirm
> the exact member names against
> `src/SharpVision.Terminal/Capabilities/ColorDepth.cs` and
> `src/SharpVision/Controls/Screen.cs`. If `NoColor` is named differently (e.g.,
> `None`/`Monochrome`), use the actual minimal member. The prior
> `Application.Console.cs` called `screen.Attach(application)`, confirming that
> signature.

- [ ] **Step 4: Run the test to verify it passes**

Run:

```bash
dotnet test --project tests/SharpVision.Tests --filter-class "*ConsoleApplicationBuilderTests" --timeout 60s
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/SharpVision/Runtime/ConsoleApplicationBuilder.cs tests/SharpVision.Tests/Runtime/ConsoleApplicationBuilderTests.cs
git commit -m "feat(runtime): add fluent ConsoleApplicationBuilder"
```

### Task 10: `ConsoleApplication` entry point + `Application.RunAsync`

**Files:**

- Create: `src/SharpVision/Runtime/ConsoleApplication.cs`
- Modify: `src/SharpVision/Runtime/Application.cs` (add instance `RunAsync`)
- Test: `tests/SharpVision.Tests/Runtime/ConsoleApplicationTests.cs`

**Interfaces:**

- Consumes: `ConsoleApplicationBuilder`, `ConsoleHost.IsInteractive`,
  `Application.StartAsync`, `Application.StopAsync`, `Application.Completion`,
  `Application.Failure`.
- Produces:

```csharp
public static class ConsoleApplication
{
    public static ConsoleApplicationBuilder CreateBuilder(Screen screen);
    public static ValueTask<ConsoleRunStatus> RunAsync(
        Screen screen, Action<ConsoleApplicationBuilder>? configure = null);
    public static ValueTask<ConsoleRunStatus> RunAsync(Screen screen, ConsoleRunOptions options);
    internal static ValueTask<ConsoleRunStatus> RunCoreAsync(
        ConsoleApplicationBuilder builder, CancellationToken cancellationToken);
}
// plus: Task Application.RunAsync(CancellationToken cancellationToken = default)
```

- [ ] **Step 1: Write the failing test**

```csharp
// tests/SharpVision.Tests/Runtime/ConsoleApplicationTests.cs
namespace SharpVision.Tests.Runtime;

using SharpVision.Runtime;
using SharpVision.Showcase;

public sealed class ConsoleApplicationTests
{
    [Fact]
    public void CreateBuilder_WhenScreenNull_Throws()
    {
        Should.Throw<ArgumentNullException>(() => ConsoleApplication.CreateBuilder(screen: null!));
    }

    [Fact]
    public async Task RunAsync_WhenConsoleRedirected_ReturnsRedirected()
    {
        // The test host runs with redirected standard streams, so IsInteractive is false.
        ConsoleRunStatus status = await ConsoleApplication.RunAsync(new Gallery());

        status.ShouldBe(ConsoleRunStatus.Redirected);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run:

```bash
dotnet test --project tests/SharpVision.Tests --filter-class "*ConsoleApplicationTests" --timeout 60s
```

Expected: FAIL — `ConsoleApplication` does not exist.

- [ ] **Step 3: Add the instance `Application.RunAsync`**

In `Application.cs`, add after `StopAsync` (around line 273):

```csharp
    /// <summary>Starts the application, waits for completion, and stops it.</summary>
    /// <param name="cancellationToken">Requests shutdown.</param>
    /// <returns>The complete run; faults with the primary failure when one occurred.</returns>
    /// <exception cref="InvalidOperationException">The application was already started.</exception>
    /// <exception cref="ObjectDisposedException">The application is disposed.</exception>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        await StartAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await Completion.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await StopAsync(CancellationToken.None).ConfigureAwait(false);
            return;
        }

        await StopAsync(CancellationToken.None).ConfigureAwait(false);
    }
```

- [ ] **Step 4: Write `ConsoleApplication`**

```csharp
// src/SharpVision/Runtime/ConsoleApplication.cs
// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Runtime;

using SharpVision.Controls;
using SharpVision.Terminal.Runtime;

/// <summary>Provides the fluent entry point for interactive console applications.</summary>
public static class ConsoleApplication
{
    /// <summary>Creates a builder for one detached screen.</summary>
    /// <param name="screen">The non-null detached screen.</param>
    /// <returns>A fluent builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="screen"/> is null.</exception>
    public static ConsoleApplicationBuilder CreateBuilder(Screen screen) => new(screen);

    /// <summary>Configures and runs an interactive console application.</summary>
    /// <param name="screen">The non-null detached screen.</param>
    /// <param name="configure">Optional fluent configuration.</param>
    /// <returns>The run status.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="screen"/> is null.</exception>
    public static ValueTask<ConsoleRunStatus> RunAsync(
        Screen screen,
        Action<ConsoleApplicationBuilder>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(screen);
        var builder = new ConsoleApplicationBuilder(screen);
        configure?.Invoke(builder);
        return RunCoreAsync(builder, CancellationToken.None);
    }

    /// <summary>Runs an interactive console application with prebuilt options.</summary>
    /// <param name="screen">The non-null detached screen.</param>
    /// <param name="options">The non-null run options.</param>
    /// <returns>The run status.</returns>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    public static ValueTask<ConsoleRunStatus> RunAsync(Screen screen, ConsoleRunOptions options)
    {
        ArgumentNullException.ThrowIfNull(screen);
        ArgumentNullException.ThrowIfNull(options);
        var builder = new ConsoleApplicationBuilder(screen).ConfigureOptions(_ => options);
        return RunCoreAsync(builder, CancellationToken.None);
    }

    internal static async ValueTask<ConsoleRunStatus> RunCoreAsync(
        ConsoleApplicationBuilder builder,
        CancellationToken cancellationToken)
    {
        if (!ConsoleHost.IsInteractive)
        {
            if (builder.Options.RedirectedMessage is { Length: > 0 } message)
            {
                Console.WriteLine(message);
            }

            return ConsoleRunStatus.Redirected;
        }

        await using Application application = builder.Build();

        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        void OnCancel(object? sender, ConsoleCancelEventArgs eventArgs)
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        }

        bool observeCtrlC = !builder.Options.TreatControlCAsInput;

        if (observeCtrlC)
        {
            Console.CancelKeyPress += OnCancel;
        }

        try
        {
            await application.StartAsync(cancellation.Token).ConfigureAwait(false);
            _ = await Task.WhenAny(
                application.Completion,
                Task.Delay(Timeout.InfiniteTimeSpan, cancellation.Token)).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            await application.StopAsync(CancellationToken.None).ConfigureAwait(false);
            return ConsoleRunStatus.Cancelled;
        }
        finally
        {
            if (observeCtrlC)
            {
                Console.CancelKeyPress -= OnCancel;
            }
        }

        await application.StopAsync(CancellationToken.None).ConfigureAwait(false);

        return application.Failure is not null
            ? ConsoleRunStatus.Failed
            : cancellation.IsCancellationRequested
                ? ConsoleRunStatus.Cancelled
                : ConsoleRunStatus.Completed;
    }
}
```

- [ ] **Step 5: Run the test to verify it passes**

Run:

```bash
dotnet test --project tests/SharpVision.Tests --filter-class "*ConsoleApplicationTests" --timeout 60s
```

Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/SharpVision/Runtime/ConsoleApplication.cs src/SharpVision/Runtime/Application.cs tests/SharpVision.Tests/Runtime/ConsoleApplicationTests.cs
git commit -m "feat(runtime): add ConsoleApplication entry point and Application.RunAsync"
```

### Task 11: Migrate the showcase and remove the old entry points

**Files:**

- Modify: `src/SharpVision.Showcase/Program.cs`
- Delete: `src/SharpVision/Runtime/ConsoleRun.cs`
- Delete: `src/SharpVision/Runtime/Application.Console.cs`
- Test: existing showcase tests.

**Interfaces:**

- Consumes: `ConsoleApplication.RunAsync`.

- [ ] **Step 1: Update `Program.cs`**

```csharp
// src/SharpVision.Showcase/Program.cs
// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using SharpVision.Runtime;
using SharpVision.Showcase;

ConsoleRunStatus status = await ConsoleApplication.RunAsync(new Gallery());

return status == ConsoleRunStatus.Failed ? 1 : 0;
```

- [ ] **Step 2: Delete the superseded files and find stragglers**

Run:

```bash
git rm src/SharpVision/Runtime/ConsoleRun.cs src/SharpVision/Runtime/Application.Console.cs
grep -rn "RunConsoleAsync\|ConsoleRun\b" --include='*.cs' src tests | grep -v -E "obj/|bin/"
```

Expected: no remaining references. Update any test that called
`Application.RunConsoleAsync` to `ConsoleApplication.RunAsync`.

- [ ] **Step 3: Build and run the showcase + runtime suites**

Run:

```bash
make build
dotnet test --project tests/SharpVision.Showcase.Tests --timeout 180s
dotnet test --project tests/SharpVision.Tests --filter-class "*Console*" --timeout 120s
```

Expected: clean build; showcase and console tests pass.

- [ ] **Step 4: Commit**

```bash
git add -A src/SharpVision.Showcase/Program.cs src/SharpVision/Runtime tests
git commit -m "refactor(runtime): migrate console entry to ConsoleApplication; remove ConsoleRun"
```

---

## Phase 3 — Application input read-model

**File structure:** Create `src/SharpVision/Runtime/PointerDevice.cs`; modify
`src/SharpVision/Runtime/Application.cs`.

### Task 12: `PointerDevice`

**Files:**

- Create: `src/SharpVision/Runtime/PointerDevice.cs`
- Test: `tests/SharpVision.Tests/Runtime/PointerDeviceTests.cs`

**Interfaces:**

- Consumes: `Point` (`SharpVision.Terminal.Geometry`), `Buttons`, `Modifiers`,
  `PointerAction`, `Pointer` (`SharpVision.Terminal.Input`), `CaptureManager`,
  `Control`.
- Produces: `sealed class PointerDevice`. Internal ctor
  `PointerDevice(Func<CaptureManager?> capture)`. Read-only properties
  `Position`, `PixelPosition`, `Buttons`, `Modifiers`, `LastAction`, `Hovered`,
  `Pressed`, `Captured`. Internal `void Observe(in Pointer pointer)`.

- [ ] **Step 1: Write the failing test**

```csharp
// tests/SharpVision.Tests/Runtime/PointerDeviceTests.cs
namespace SharpVision.Tests.Runtime;

using SharpVision.Runtime;
using SharpVision.Terminal.Geometry;
using SharpVision.Terminal.Input;

public sealed class PointerDeviceTests
{
    [Fact]
    public void Observe_WhenMove_UpdatesPositionAndButtons()
    {
        var device = new PointerDevice(() => null);
        var pointer = new Pointer(
            cells: new Point(4, 2),
            pixels: null,
            buttons: Buttons.Primary,
            action: PointerAction.Move,
            wheelX: 0,
            wheelY: 0,
            modifiers: Modifiers.Shift,
            isMotion: true,
            isCellPositionInferred: false);

        device.Observe(pointer);

        device.Position.ShouldBe(new Point(4, 2));
        device.Buttons.ShouldBe(Buttons.Primary);
        device.Modifiers.ShouldBe(Modifiers.Shift);
        device.LastAction.ShouldBe(PointerAction.Move);
    }

    [Fact]
    public void Observe_WhenLeave_ClearsPosition()
    {
        var device = new PointerDevice(() => null);
        device.Observe(new Pointer(new Point(1, 1), null, Buttons.None, PointerAction.Move, 0, 0, Modifiers.None, true, false));

        device.Observe(new Pointer(null, null, Buttons.None, PointerAction.Leave, 0, 0, Modifiers.None, false, false));

        device.Position.ShouldBeNull();
    }
}
```

> Confirm `Buttons.None`/`Modifiers.None` member names against
> `Buttons.cs`/`Modifiers.cs`; if the zero value is named differently, use
> `default`.

- [ ] **Step 2: Run the test to verify it fails**

Run:

```bash
dotnet test --project tests/SharpVision.Tests --filter-class "*PointerDeviceTests" --timeout 60s
```

Expected: FAIL — `PointerDevice` does not exist.

- [ ] **Step 3: Write `PointerDevice`**

```csharp
// src/SharpVision/Runtime/PointerDevice.cs
// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Runtime;

using SharpVision.Controls;
using SharpVision.Input;
using SharpVision.Terminal.Geometry;
using SharpVision.Terminal.Input;

/// <summary>Exposes the last observed pointer state and current pointer targets.</summary>
/// <remarks>
/// This is a pull-style snapshot updated on the dispatcher as pointer input is
/// dispatched. It never throws; positions are null before the first pointer and
/// after a leave. Targets read through the capture manager once the tree attaches.
/// </remarks>
public sealed class PointerDevice
{
    private readonly Func<CaptureManager?> _capture;

    internal PointerDevice(Func<CaptureManager?> capture)
    {
        Debug.Assert(capture is not null, "The capture accessor must be provided.");
        _capture = capture;
    }

    /// <summary>Gets the last observed zero-based cell position, or null.</summary>
    public Point? Position { get; private set; }

    /// <summary>Gets the last observed zero-based pixel position, or null.</summary>
    public Point? PixelPosition { get; private set; }

    /// <summary>Gets the buttons held as of the last pointer.</summary>
    public Buttons Buttons { get; private set; }

    /// <summary>Gets the modifiers active as of the last pointer.</summary>
    public Modifiers Modifiers { get; private set; }

    /// <summary>Gets the action of the last pointer.</summary>
    public PointerAction LastAction { get; private set; }

    /// <summary>Gets the current hover target, or null.</summary>
    public Control? Hovered => _capture()?.Hovered;

    /// <summary>Gets the control where the active press began, or null.</summary>
    public Control? Pressed => _capture()?.Pressed;

    /// <summary>Gets the exclusive capture target, or null.</summary>
    public Control? Captured => _capture()?.Captured;

    internal void Observe(in Pointer pointer)
    {
        Buttons = pointer.Buttons;
        Modifiers = pointer.Modifiers;
        LastAction = pointer.Action;

        if (pointer.Action == PointerAction.Leave)
        {
            Position = null;
            PixelPosition = null;
            return;
        }

        Position = pointer.Cells;
        PixelPosition = pointer.Pixels;
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run:

```bash
dotnet test --project tests/SharpVision.Tests --filter-class "*PointerDeviceTests" --timeout 60s
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/SharpVision/Runtime/PointerDevice.cs tests/SharpVision.Tests/Runtime/PointerDeviceTests.cs
git commit -m "feat(runtime): add PointerDevice read-model"
```

### Task 13: Wire `Application.Pointer` and `Application.HasFocus`

**Files:**

- Modify: `src/SharpVision/Runtime/Application.cs`
- Test: `tests/SharpVision.Tests/Runtime/ApplicationPointerTests.cs`

**Interfaces:**

- Produces: `Application.Pointer` (`PointerDevice`, always non-null),
  `Application.HasFocus` (`bool`, default true).

- [ ] **Step 1: Write the failing test**

```csharp
// tests/SharpVision.Tests/Runtime/ApplicationPointerTests.cs
namespace SharpVision.Tests.Runtime;

using SharpVision.Controls;
using SharpVision.Runtime;
using SharpVision.Tests.Fakes;

public sealed class ApplicationPointerTests
{
    [Fact]
    public void Pointer_WhenConstructed_IsNonNullSnapshot()
    {
        var app = new Application(new Border(), new FakeTransport(), new FakeResizeSource());

        app.Pointer.ShouldNotBeNull();
        app.Pointer.Position.ShouldBeNull();
        app.HasFocus.ShouldBeTrue();
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run:

```bash
dotnet test --project tests/SharpVision.Tests --filter-class "*ApplicationPointerTests" --timeout 60s
```

Expected: FAIL — `Pointer`/`HasFocus` do not exist.

- [ ] **Step 3: Add the members and wire dispatch**

In `Application.cs`:

Add a field and initialize it in the constructor (the accessor closes over
`CaptureValue`, which is set at init):

```csharp
    private readonly PointerDevice _pointer;
```

In the constructor, after `Dispatcher = Dispatcher.Start(...)` (line 98):

```csharp
        _pointer = new PointerDevice(() => CaptureValue);
```

Add the public members near `Size`/`Capabilities` (around line 203):

```csharp
    /// <summary>Gets the last observed pointer state and current pointer targets.</summary>
    public PointerDevice Pointer => _pointer;

    /// <summary>Gets whether the terminal window currently has focus.</summary>
    public bool HasFocus { get; private set; } = true;
```

In `Dispatch`, update the `RecordKind.Pointer` and `RecordKind.Focus` cases:

```csharp
            case RecordKind.Pointer:
                _pointer.Observe(record.Pointer);
                _ = Capture.Dispatch(record.Pointer);
                break;
```

```csharp
            case RecordKind.Focus:
                HasFocus = record.Focus.Gained;

                if (!record.Focus.Gained)
                {
                    Capture.TerminalFocusLost();
                }

                Router.Route(
                    Focus.Focused ?? Root,
                    Events.Focus,
                    new FocusEventArgs(record.Focus));
                break;
```

> Confirm the focus value member is `record.Focus.Gained` (used already at
> `Application.cs:498`).

- [ ] **Step 4: Run the test and the runtime suite**

Run:

```bash
dotnet test --project tests/SharpVision.Tests --filter-class "*ApplicationPointerTests" --timeout 60s
dotnet test --project tests/SharpVision.Tests --filter-class "*ApplicationTests" --timeout 120s
```

Expected: PASS; existing behavior unchanged.

- [ ] **Step 5: Commit**

```bash
git add src/SharpVision/Runtime/Application.cs tests/SharpVision.Tests/Runtime/ApplicationPointerTests.cs
git commit -m "feat(runtime): expose Application.Pointer and HasFocus"
```

---

## Phase 4 — Protocol discovery facade

**File structure:** Create
`src/SharpVision.Terminal/Capabilities/TerminalProtocol.cs` and
`ProtocolSupport.cs`; modify `Capabilities.cs`.

### Task 14: `TerminalProtocol` and `ProtocolSupport`

**Files:**

- Create: `src/SharpVision.Terminal/Capabilities/TerminalProtocol.cs`
- Create: `src/SharpVision.Terminal/Capabilities/ProtocolSupport.cs`
- Test: `tests/SharpVision.Terminal.Tests/Capabilities/ProtocolSupportTests.cs`

**Interfaces:**

- Produces: `enum TerminalProtocol` with one member per optional feature;
  `readonly record struct ProtocolSupport` ctor
  `(TerminalProtocol protocol, Feature feature)` with properties `Protocol`,
  `Feature`.

- [ ] **Step 1: Write the failing test**

```csharp
// tests/SharpVision.Terminal.Tests/Capabilities/ProtocolSupportTests.cs
namespace SharpVision.Terminal.Tests.Capabilities;

using SharpVision.Terminal.Capabilities;

public sealed class ProtocolSupportTests
{
    [Fact]
    public void Constructor_WhenGivenProtocolAndFeature_ExposesBoth()
    {
        var feature = new Feature(Support.Supported, Origin.Query);
        var pair = new ProtocolSupport(TerminalProtocol.Sixel, feature);

        pair.Protocol.ShouldBe(TerminalProtocol.Sixel);
        pair.Feature.ShouldBe(feature);
    }
}
```

> Confirm `Origin.Query` exists in `Origin.cs`; if not, use any defined member
> (the test only checks pass-through).

- [ ] **Step 2: Run the test to verify it fails**

Run:

```bash
dotnet test --project tests/SharpVision.Terminal.Tests --filter-class "*ProtocolSupportTests" --timeout 60s
```

Expected: FAIL — the types do not exist.

- [ ] **Step 3: Write the enum**

```csharp
// src/SharpVision.Terminal/Capabilities/TerminalProtocol.cs
// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Capabilities;

/// <summary>Names one optional terminal protocol or extension a profile can report.</summary>
public enum TerminalProtocol
{
    /// <summary>DEC private mode 2026 synchronized output.</summary>
    SynchronizedOutput,

    /// <summary>Focus in/out reporting.</summary>
    FocusReporting,

    /// <summary>Bracketed paste.</summary>
    BracketedPaste,

    /// <summary>SGR pixel-coordinate mouse reporting.</summary>
    PixelMouse,

    /// <summary>SGR cell-coordinate mouse reporting.</summary>
    CellMouse,

    /// <summary>The Kitty keyboard protocol.</summary>
    KittyKeyboard,

    /// <summary>OSC 52 clipboard access.</summary>
    Osc52,

    /// <summary>Kitty OSC 5522 clipboard access.</summary>
    KittyClipboard,

    /// <summary>The Kitty graphics extension.</summary>
    KittyGraphics,

    /// <summary>Sixel raster graphics.</summary>
    Sixel,

    /// <summary>iTerm2 inline images.</summary>
    ItermImages,

    /// <summary>Styled underline variants.</summary>
    StyledUnderlines,

    /// <summary>Independent underline color.</summary>
    UnderlineColor,

    /// <summary>Overline rendition.</summary>
    Overline,
}
```

- [ ] **Step 4: Write the pair struct**

```csharp
// src/SharpVision.Terminal/Capabilities/ProtocolSupport.cs
// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Capabilities;

/// <summary>Pairs one terminal protocol with its detected support evidence.</summary>
public readonly record struct ProtocolSupport
{
    /// <summary>Initializes a validated protocol/feature pair.</summary>
    /// <param name="protocol">The protocol.</param>
    /// <param name="feature">The support evidence.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="protocol"/> is unknown.</exception>
    public ProtocolSupport(TerminalProtocol protocol, Feature feature)
    {
        if (!Enum.IsDefined(protocol))
        {
            throw new ArgumentOutOfRangeException(nameof(protocol), protocol, "The terminal protocol is unknown.");
        }

        Protocol = protocol;
        Feature = feature;
    }

    /// <summary>Gets the protocol.</summary>
    public TerminalProtocol Protocol { get; }

    /// <summary>Gets the support evidence.</summary>
    public Feature Feature { get; }
}
```

- [ ] **Step 5: Run the test to verify it passes**

Run:

```bash
dotnet test --project tests/SharpVision.Terminal.Tests --filter-class "*ProtocolSupportTests" --timeout 60s
```

Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/SharpVision.Terminal/Capabilities/TerminalProtocol.cs src/SharpVision.Terminal/Capabilities/ProtocolSupport.cs tests/SharpVision.Terminal.Tests/Capabilities/ProtocolSupportTests.cs
git commit -m "feat(terminal): add TerminalProtocol and ProtocolSupport"
```

### Task 15: `Capabilities.Support` and `Capabilities.Features`

**Files:**

- Modify: `src/SharpVision.Terminal/Capabilities/Capabilities.cs`
- Test:
  `tests/SharpVision.Terminal.Tests/Capabilities/CapabilitiesDiscoveryTests.cs`

**Interfaces:**

- Consumes: `TerminalProtocol`, `ProtocolSupport`, `Feature`.
- Produces: `Feature Support(TerminalProtocol protocol)`;
  `IReadOnlyList<ProtocolSupport> Features`. Removes the anonymous
  `OptionalFeatures`.

- [ ] **Step 1: Write the failing test**

```csharp
// tests/SharpVision.Terminal.Tests/Capabilities/CapabilitiesDiscoveryTests.cs
namespace SharpVision.Terminal.Tests.Capabilities;

using SharpVision.Terminal.Capabilities;

using TerminalCapabilities = SharpVision.Terminal.Capabilities.Capabilities;

public sealed class CapabilitiesDiscoveryTests
{
    [Fact]
    public void Support_WhenSixelUnknownOnConservative_ReturnsSameStateAsProperty()
    {
        TerminalCapabilities capabilities = TerminalCapabilities.Conservative;

        capabilities.Support(TerminalProtocol.Sixel).State.ShouldBe(capabilities.Sixel.State);
    }

    [Fact]
    public void Features_WhenEnumerated_ListsEveryProtocolExactlyOnce()
    {
        IReadOnlyList<ProtocolSupport> features = TerminalCapabilities.Conservative.Features;

        int protocolCount = Enum.GetValues<TerminalProtocol>().Length;
        features.Count.ShouldBe(protocolCount);
        features.Select(f => f.Protocol).Distinct().Count().ShouldBe(protocolCount);
    }

    [Fact]
    public void Support_WhenProtocolUnknown_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(
            () => TerminalCapabilities.Conservative.Support((TerminalProtocol) 999));
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run:

```bash
dotnet test --project tests/SharpVision.Terminal.Tests --filter-class "*CapabilitiesDiscoveryTests" --timeout 60s
```

Expected: FAIL — `Support`/`Features` do not exist.

- [ ] **Step 3: Replace `OptionalFeatures` with `Support` and `Features`**

In `Capabilities.cs`, remove the `OptionalFeatures` property (lines 102-121) and
add:

```csharp
    /// <summary>Gets the support evidence for one optional protocol.</summary>
    /// <param name="protocol">The protocol to query.</param>
    /// <returns>The feature evidence.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="protocol"/> is unknown.</exception>
    public Feature Support(TerminalProtocol protocol) => protocol switch
    {
        TerminalProtocol.SynchronizedOutput => SynchronizedOutput,
        TerminalProtocol.FocusReporting => FocusReporting,
        TerminalProtocol.BracketedPaste => BracketedPaste,
        TerminalProtocol.PixelMouse => PixelMouse,
        TerminalProtocol.CellMouse => CellMouse,
        TerminalProtocol.KittyKeyboard => KittyKeyboard,
        TerminalProtocol.Osc52 => Osc52,
        TerminalProtocol.KittyClipboard => KittyClipboard,
        TerminalProtocol.KittyGraphics => KittyGraphics,
        TerminalProtocol.Sixel => Sixel,
        TerminalProtocol.ItermImages => ItermImages,
        TerminalProtocol.StyledUnderlines => StyledUnderlines,
        TerminalProtocol.UnderlineColor => UnderlineColor,
        TerminalProtocol.Overline => Overline,
        _ => throw new ArgumentOutOfRangeException(nameof(protocol), protocol, "The terminal protocol is unknown."),
    };

    /// <summary>Gets every optional protocol paired with its support evidence.</summary>
    public IReadOnlyList<ProtocolSupport> Features =>
    [
        new(TerminalProtocol.SynchronizedOutput, SynchronizedOutput),
        new(TerminalProtocol.FocusReporting, FocusReporting),
        new(TerminalProtocol.BracketedPaste, BracketedPaste),
        new(TerminalProtocol.PixelMouse, PixelMouse),
        new(TerminalProtocol.CellMouse, CellMouse),
        new(TerminalProtocol.KittyKeyboard, KittyKeyboard),
        new(TerminalProtocol.Osc52, Osc52),
        new(TerminalProtocol.KittyClipboard, KittyClipboard),
        new(TerminalProtocol.KittyGraphics, KittyGraphics),
        new(TerminalProtocol.Sixel, Sixel),
        new(TerminalProtocol.ItermImages, ItermImages),
        new(TerminalProtocol.StyledUnderlines, StyledUnderlines),
        new(TerminalProtocol.UnderlineColor, UnderlineColor),
        new(TerminalProtocol.Overline, Overline),
    ];
```

- [ ] **Step 4: Find and update `OptionalFeatures` callers**

Run:

```bash
grep -rn "OptionalFeatures" --include='*.cs' src tests | grep -v -E "obj/|bin/"
```

Expected: update any caller to iterate `Features` (each element has `.Protocol`
and `.Feature`) or call `Support(...)`.

- [ ] **Step 5: Run the test and build**

Run:

```bash
dotnet test --project tests/SharpVision.Terminal.Tests --filter-class "*CapabilitiesDiscoveryTests" --timeout 60s
make build
```

Expected: PASS; clean build.

- [ ] **Step 6: Commit**

```bash
git add src/SharpVision.Terminal/Capabilities/Capabilities.cs tests/SharpVision.Terminal.Tests/Capabilities/CapabilitiesDiscoveryTests.cs
git commit -m "feat(terminal): named protocol discovery on Capabilities"
```

---

## Phase 5 — Output services and ordered out-of-band writes

**File structure:** Create `src/SharpVision/Runtime/IBell.cs`, `IClipboard.cs`,
`ITerminalServices.cs`, `TerminalServices.cs`; modify
`src/SharpVision/Runtime/Application.cs`.

### Task 16: Ordered out-of-band write path

**Files:**

- Modify: `src/SharpVision/Runtime/Application.cs`
- Test: `tests/SharpVision.Tests/Runtime/ApplicationOutOfBandTests.cs`

**Interfaces:**

- Produces (internal on `Application`):
  `internal void PostOutOfBand(ReadOnlyMemory<byte> bytes)` — thread-safe;
  buffers bytes and drains them on the dispatcher, flushing only when no frame
  render is in flight.

**Design note.** The renderer already serializes frame writes through
`_rendering` plus a dispatcher `Hold`. Out-of-band bytes reuse that
single-writer discipline: they are buffered under `_gate`, and a dispatcher
drain flushes them as a mini write op that sets `_rendering`. `CompleteRender`
drains any pending out-of-band bytes before servicing a deferred render, so a
byte can never interleave a frame.

- [ ] **Step 1: Write the failing test**

```csharp
// tests/SharpVision.Tests/Runtime/ApplicationOutOfBandTests.cs
namespace SharpVision.Tests.Runtime;

using SharpVision.Controls;
using SharpVision.Runtime;
using SharpVision.Tests.Fakes; // RecordingTransport captures written bytes; FakeResizeSource

public sealed class ApplicationOutOfBandTests
{
    [Fact]
    public async Task PostOutOfBand_WhenRunning_WritesBytesToTransport()
    {
        var transport = new RecordingTransport();
        var resize = new FakeResizeSource();
        resize.Push(new Dimensions(new Size(80, 24)));
        await using var app = new Application(new Border(), transport, resize);
        await app.StartAsync();

        app.PostOutOfBand(new byte[] { 0x07 });
        await app.Dispatcher.InvokeAsync(() => { }); // drain the dispatcher
        await Task.Delay(50);

        transport.Written.ShouldContain((byte) 0x07);

        await app.StopAsync();
    }
}
```

> `RecordingTransport` is a fake `ITransport` that appends `WriteAsync` bytes to
> a `List<byte> Written`. If the `SharpVision.Tests` project already has a
> recording transport, reuse it; otherwise add a minimal one under
> `tests/SharpVision.Tests/Fakes/`. `FakeResizeSource.Push` supplies one resize
> then blocks; adapt to the project's existing resize fake API. This test is
> timing-tolerant by design (a small delay plus a dispatcher drain).

- [ ] **Step 2: Run the test to verify it fails**

Run:

```bash
dotnet test --project tests/SharpVision.Tests --filter-class "*ApplicationOutOfBandTests" --timeout 60s
```

Expected: FAIL — `PostOutOfBand` does not exist.

- [ ] **Step 3: Add the out-of-band state and methods**

In `Application.cs`, add fields near the render fields (around line 46):

```csharp
    private readonly System.Buffers.ArrayBufferWriter<byte> _outOfBand = new();
    private bool _outOfBandWake;
```

Add the public/internal entry and the drain, near `StartRender` (around line
887):

```csharp
    internal void PostOutOfBand(ReadOnlyMemory<byte> bytes)
    {
        if (bytes.IsEmpty)
        {
            return;
        }

        lock (_gate)
        {
            if (_stopping)
            {
                return;
            }

            _outOfBand.Write(bytes.Span);

            if (_outOfBandWake)
            {
                return;
            }

            _outOfBandWake = true;
        }

        Dispatcher.Post(DrainOutOfBand);
    }

    private void DrainOutOfBand()
    {
        Dispatcher.VerifyAccess();

        lock (_gate)
        {
            _outOfBandWake = false;
        }

        // A frame render owns the writer; CompleteRender re-drains afterward.
        if (_rendering || _stopping || IsSuspended())
        {
            return;
        }

        FlushOutOfBand();
    }

    private void FlushOutOfBand()
    {
        Dispatcher.VerifyAccess();
        Debug.Assert(!_rendering, "Out-of-band flush must not overlap a frame render.");

        byte[] payload;

        lock (_gate)
        {
            if (_outOfBand.WrittenCount == 0)
            {
                return;
            }

            payload = _outOfBand.WrittenSpan.ToArray();
            _outOfBand.Clear();
        }

        IDisposable hold = Dispatcher.Hold();
        TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _renderTask = completion.Task;
        _rendering = true;
        ValueTask operation = WriteOutOfBandAsync(payload);
        _ = ObserveOutOfBandAsync(operation, hold, completion);
    }

    private async ValueTask WriteOutOfBandAsync(byte[] payload)
    {
        await _transport.WriteAsync(payload, _lifetime.Token).ConfigureAwait(false);
        await _transport.FlushAsync(_lifetime.Token).ConfigureAwait(false);
    }

    private async Task ObserveOutOfBandAsync(ValueTask operation, IDisposable hold, TaskCompletionSource completion)
    {
        Exception? failure = null;

        try
        {
            await operation;
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        try
        {
            Dispatcher.Post(() => CompleteOutOfBand(hold, completion, failure));
        }
        catch
        {
            hold.Dispose();
            _ = completion.TrySetResult();
        }
    }

    private void CompleteOutOfBand(IDisposable hold, TaskCompletionSource completion, Exception? failure)
    {
        Dispatcher.VerifyAccess();

        try
        {
            _rendering = false;

            if (failure is not null &&
                (failure is not OperationCanceledException || !_lifetime.IsCancellationRequested))
            {
                Report(failure);
                return;
            }

            PumpAfterWrite();
        }
        finally
        {
            hold.Dispose();
            _ = completion.TrySetResult();
        }
    }

    private void PumpAfterWrite()
    {
        if (_stopping || IsSuspended())
        {
            return;
        }

        if (HasPendingOutOfBand())
        {
            FlushOutOfBand();
            return;
        }

        if (_renderRequested || Root.Pending != Invalidation.None)
        {
            _renderRequested = false;
            ProcessInvalidation();
        }
    }

    private bool HasPendingOutOfBand()
    {
        lock (_gate)
        {
            return _outOfBand.WrittenCount > 0;
        }
    }
```

- [ ] **Step 4: Drain out-of-band bytes when a frame completes**

In `CompleteRender` (around line 452), replace the deferred-render tail so
out-of-band bytes flush first:

```csharp
            FrameRendered?.Invoke(this, new FrameRenderedEventArgs(metrics!.Value));
            MarkStarted();

            if (HasPendingOutOfBand())
            {
                FlushOutOfBand();
            }
            else if (_renderRequested || Root.Pending != Invalidation.None)
            {
                _renderRequested = false;
                ProcessInvalidation();
            }
```

- [ ] **Step 5: Run the test and the runtime suite**

Run:

```bash
dotnet test --project tests/SharpVision.Tests --filter-class "*ApplicationOutOfBandTests" --timeout 60s
dotnet test --project tests/SharpVision.Tests --filter-class "*ApplicationTests" --timeout 120s
```

Expected: PASS; existing render behavior unchanged.

- [ ] **Step 6: Commit**

```bash
git add src/SharpVision/Runtime/Application.cs tests/SharpVision.Tests/Runtime/ApplicationOutOfBandTests.cs
git commit -m "feat(runtime): ordered out-of-band write path sharing the render gate"
```

### Task 17: `ITerminalServices`, `IBell`, `IClipboard`, and `Application.Terminal`

**Files:**

- Create: `src/SharpVision/Runtime/IBell.cs`
- Create: `src/SharpVision/Runtime/IClipboard.cs`
- Create: `src/SharpVision/Runtime/ITerminalServices.cs`
- Create: `src/SharpVision/Runtime/TerminalServices.cs`
- Modify: `src/SharpVision/Runtime/Application.cs`
- Test: `tests/SharpVision.Tests/Runtime/TerminalServicesTests.cs`

**Interfaces:**

- Consumes: `Application.PostOutOfBand`, `Application.Capabilities`, `Writer`,
  `Osc`, `Osc52`, `Selection`.
- Produces (full signatures in Step 3); `Application.Terminal` returns an
  `ITerminalServices`:

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

- [ ] **Step 1: Write the failing test**

```csharp
// tests/SharpVision.Tests/Runtime/TerminalServicesTests.cs
namespace SharpVision.Tests.Runtime;

using SharpVision.Controls;
using SharpVision.Runtime;
using SharpVision.Tests.Fakes;

public sealed class TerminalServicesTests
{
    [Fact]
    public async Task Bell_WhenRung_EmitsBelByte()
    {
        var transport = new RecordingTransport();
        var resize = new FakeResizeSource();
        resize.Push(new Dimensions(new Size(80, 24)));
        await using var app = new Application(new Border(), transport, resize);
        await app.StartAsync();

        app.Terminal.Bell.Ring();
        await app.Dispatcher.InvokeAsync(() => { });
        await Task.Delay(50);

        transport.Written.ShouldContain((byte) 0x07);

        await app.StopAsync();
    }

    [Fact]
    public void Terminal_WhenConstructed_IsNonNull()
    {
        var app = new Application(new Border(), new FakeTransport(), new FakeResizeSource());

        app.Terminal.ShouldNotBeNull();
        app.Terminal.Bell.ShouldNotBeNull();
        app.Terminal.Clipboard.ShouldNotBeNull();
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run:

```bash
dotnet test --project tests/SharpVision.Tests --filter-class "*TerminalServicesTests" --timeout 60s
```

Expected: FAIL — the types/members do not exist.

- [ ] **Step 3: Write the three interfaces**

```csharp
// src/SharpVision/Runtime/IBell.cs
// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Runtime;

/// <summary>Signals the terminal alert.</summary>
public interface IBell
{
    /// <summary>Requests an audible bell, ordered with frame output and never mid-frame.</summary>
    void Ring();
}
```

```csharp
// src/SharpVision/Runtime/IClipboard.cs
// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Runtime;

using SharpVision.Terminal.Clipboard;

/// <summary>Writes and requests terminal clipboard selections when supported.</summary>
public interface IClipboard
{
    /// <summary>Gets whether the active terminal advertises clipboard access.</summary>
    bool IsSupported { get; }

    /// <summary>Writes text to a selection; a no-op when unsupported.</summary>
    /// <param name="text">The text to copy.</param>
    /// <param name="selection">The target selection.</param>
    void Write(ReadOnlySpan<char> text, Selection selection = Selection.Clipboard);

    /// <summary>Requests a selection's text; replies arrive through ResponseReceived. A no-op when unsupported.</summary>
    /// <param name="selection">The target selection.</param>
    void Request(Selection selection = Selection.Clipboard);
}
```

```csharp
// src/SharpVision/Runtime/ITerminalServices.cs
// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Runtime;

/// <summary>Exposes implemented terminal output protocols to an application.</summary>
public interface ITerminalServices
{
    /// <summary>Gets the terminal alert.</summary>
    IBell Bell { get; }

    /// <summary>Gets clipboard access.</summary>
    IClipboard Clipboard { get; }

    /// <summary>Sets the window title using OSC 2.</summary>
    /// <param name="title">The non-null title.</param>
    /// <exception cref="ArgumentNullException"><paramref name="title"/> is null.</exception>
    void SetTitle(string title);
}
```

- [ ] **Step 4: Write the implementation**

```csharp
// src/SharpVision/Runtime/TerminalServices.cs
// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Runtime;

using System.Buffers;
using System.Text;

using SharpVision.Terminal.Clipboard;
using SharpVision.Terminal.Protocols;

using TerminalCapabilities = Terminal.Capabilities.Capabilities;

/// <summary>Encodes implemented output protocols and posts them through the ordered write path.</summary>
internal sealed class TerminalServices: ITerminalServices, IBell, IClipboard
{
    private readonly Application _application;

    internal TerminalServices(Application application)
    {
        Debug.Assert(application is not null, "The owning application must be provided.");
        _application = application;
    }

    /// <inheritdoc/>
    public IBell Bell => this;

    /// <inheritdoc/>
    public IClipboard Clipboard => this;

    /// <inheritdoc/>
    public bool IsSupported =>
        _application.Capabilities.Osc52.IsSupported || _application.Capabilities.KittyClipboard.IsSupported;

    /// <inheritdoc/>
    public void Ring() => _application.PostOutOfBand(new byte[] { 0x07 });

    /// <inheritdoc/>
    public void SetTitle(string title)
    {
        ArgumentNullException.ThrowIfNull(title);
        int byteCount = Encoding.UTF8.GetByteCount(title);
        var destination = new ArrayBufferWriter<byte>(byteCount + 8);
        byte[] rented = ArrayPool<byte>.Shared.Rent(byteCount);

        try
        {
            int written = Encoding.UTF8.GetBytes(title, rented);
            Osc.Title(new Writer(destination), rented.AsSpan(0, written));
            _application.PostOutOfBand(destination.WrittenMemory);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented, clearArray: true);
        }
    }

    /// <inheritdoc/>
    public void Write(ReadOnlySpan<char> text, Selection selection = Selection.Clipboard)
    {
        if (!IsSupported)
        {
            return;
        }

        int byteCount = Encoding.UTF8.GetByteCount(text);
        var destination = new ArrayBufferWriter<byte>(byteCount + 16);
        byte[] rented = ArrayPool<byte>.Shared.Rent(Math.Max(1, byteCount));

        try
        {
            int written = Encoding.UTF8.GetBytes(text, rented);
            Osc52.Write(new Writer(destination), selection, rented.AsSpan(0, written));
            _application.PostOutOfBand(destination.WrittenMemory);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented, clearArray: true);
        }
    }

    /// <inheritdoc/>
    public void Request(Selection selection = Selection.Clipboard)
    {
        if (!IsSupported)
        {
            return;
        }

        var destination = new ArrayBufferWriter<byte>(8);
        Osc52.Query(new Writer(destination), selection);
        _application.PostOutOfBand(destination.WrittenMemory);
    }
}
```

- [ ] **Step 5: Expose `Application.Terminal`**

In `Application.cs`, add a field near `_pointer`:

```csharp
    private readonly TerminalServices _terminal;
```

Initialize it in the constructor after `_pointer` is set:

```csharp
        _terminal = new TerminalServices(this);
```

Add the public member near `Pointer` (around line 205):

```csharp
    /// <summary>Gets the implemented terminal output services (bell, clipboard, title).</summary>
    public ITerminalServices Terminal => _terminal;
```

- [ ] **Step 6: Run the tests and full runtime suite**

Run:

```bash
dotnet test --project tests/SharpVision.Tests --filter-class "*TerminalServicesTests" --timeout 60s
dotnet test --project tests/SharpVision.Tests --filter-class "*ApplicationTests" --timeout 120s
```

Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add src/SharpVision/Runtime/IBell.cs src/SharpVision/Runtime/IClipboard.cs src/SharpVision/Runtime/ITerminalServices.cs src/SharpVision/Runtime/TerminalServices.cs src/SharpVision/Runtime/Application.cs tests/SharpVision.Tests/Runtime/TerminalServicesTests.cs
git commit -m "feat(runtime): expose ITerminalServices with IBell/IClipboard/SetTitle"
```

---

## Phase 6 — Documentation and quality gate

### Task 18: Documentation and `AGENTS.md` sync

**Files:**

- Create: `docs/concepts/hosting.md`
- Modify: `docs/architecture/runtime-event-loop.md`,
  `docs/concepts/input-routing.md`, `docs/protocols/index.md`,
  `docs/protocols/coverage-matrix.md`, `docs/protocols/runtime-routing.md`,
  `docs/architecture/showcase.md`, `AGENTS.md`, `docs/index.md` (link the new
  hosting page if it lists concepts).

- [ ] **Step 1: Write `docs/concepts/hosting.md`**

Cover: `ConsoleApplication.CreateBuilder`/`RunAsync`, the three usages
(one-liner, callback, builder), the full `ConsoleRunOptions` table with defaults
(copy from Task 8), `ConsoleHost.Open`/`ConsoleConnection`, Unix vs Windows
behavior (SIGWINCH + pixels vs. `SetConsoleMode` + cell-only polling), and
`TreatControlCAsInput`. State that the Windows path needs hardware/CI
validation.

- [ ] **Step 2: Update the other docs**

- `runtime-event-loop.md`: add `Application.RunAsync` and the out-of-band write
  path (buffered under the gate, flushed between frames via the `_rendering`
  gate).
- `input-routing.md`: add `Application.Pointer` (`PointerDevice`: position,
  pixel position, buttons, modifiers, hovered/pressed/captured) and
  `Application.HasFocus`.
- `protocols/index.md` and `coverage-matrix.md`: document
  `Capabilities.Support`/`Features` and `ITerminalServices`
  (bell/clipboard/title); keep the unsupported states honest
  (graphics/sixel/iTerm2 remain unsupported and are not on the output facade).
- `runtime-routing.md`: describe the inbound consumption surface
  (`ResponseReceived`, `CapabilitiesChanged`, `Diagnostic`).
- `showcase.md`: reflect the `ConsoleApplication` entry point.
- `AGENTS.md`: note the hosting pattern (`ConsoleApplication`/builder),
  `ITerminalServices`/`IBell`, and `TreatControlCAsInput`.

- [ ] **Step 3: Verify docs lint and links**

Run:

```bash
npm run format -- docs AGENTS.md
npm run lint:markdown
npm run lint:links
```

Expected: markdown lint passes; no new link failures beyond the two pre-existing
`showcase-dashboard.png` warnings.

- [ ] **Step 4: Commit**

```bash
git add docs AGENTS.md
git commit -m "docs: hosting, input, and protocol consumption for the new public API"
```

### Task 19: Full quality gate

- [ ] **Step 1: Run every gate**

Run:

```bash
make format
make lint
make build
make test
```

Expected: zero warnings, zero errors, discovered tests at or above the
configured minimum, no Markdown or link failures.

- [ ] **Step 2: Commit any format-only changes**

```bash
git add -A
git commit -m "chore: format and quality-gate pass for hosting/public API"
```

---

## Self-review notes (author checklist, resolved)

- **Spec coverage:** §1 portable host → Tasks 1–7; §2 options → Task 8; §3
  builder/entry → Tasks 9–11; §4 input read-model → Tasks 12–13; §5 protocol
  discovery → Tasks 14–15; §6 output services → Task 17; §7 ordered writes →
  Task 16; docs → Task 18; gate → Task 19.
- **Type consistency:** `ConsoleConnection(transport, resize, restore)`,
  `ConsoleHost.Open(ConsoleHostOptions)`,
  `ConsoleRunOptions.ToTerminalOptions()/ToHostOptions()/ResolveTheme()`,
  `ConsoleApplicationBuilder.Build()/RunAsync()/Options`,
  `ConsoleApplication.CreateBuilder/RunAsync/RunCoreAsync`,
  `Application(root, transport, resize, options, hostLease)` +
  `Application.RunAsync`/`Pointer`/`HasFocus`/`Terminal`/`PostOutOfBand`,
  `PointerDevice.Observe`, `Capabilities.Support/Features`,
  `ProtocolSupport(protocol, feature)`,
  `ITerminalServices.Bell/Clipboard/SetTitle` — used consistently across tasks.
- **Verification-required member names** (confirm against source while
  implementing, noted inline in the relevant tasks): `ColorDepth.NoColor`,
  `Screen.Attach(Application)`, `Buttons.None`/`Modifiers.None`, `Origin.Query`,
  focus `Gained`, and the exact
  `Detector.Detect`/`NegotiationOptions`/`Settings` shapes (taken from the
  deleted `ConsoleRun.cs`).
