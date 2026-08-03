// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Runtime;

using System.Text.RegularExpressions;

/// <summary>
/// Verifies the Windows console host and console-mode lease against a real ConPTY-attached
/// process, closing the gap <c>docs/testing/pseudoterminals.md</c> flagged and issue #35 tracked:
/// until now, no test exercised <see cref="ConsoleHost.Open"/> against a genuine Windows console.
/// </summary>
/// <remarks>
/// These tests spawn <c>SharpVision.Terminal.Probe</c> attached to a real pseudo console (see
/// <see cref="WindowsPseudoterminal"/>) and assert on the plain-text facts and raw bytes it
/// reports back over the pseudo console's own output pipe — the same channel a real terminal
/// emulator would read. They therefore only run in the Windows CI lane; everywhere else they
/// skip via <see cref="Assert.SkipUnless(bool, string)"/>, matching
/// <c>Transport/PseudoterminalTests.cs</c>'s Unix convention.
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed partial class WindowsConsoleHostConPtyTests
{
    /// <summary>Verifies opening against a real ConPTY applies VT input and output modes.</summary>
    [Fact]
    public async Task Open_WhenAttachedToConPty_AppliesVtInputAndOutputModesAsync()
    {
        SkipWithoutConPty();
        await using var terminal = WindowsPseudoterminal.Open(["open-apply", "false"]);
        var saved = await ReadModesLineAsync(terminal, "saved");
        var applied = await ReadModesLineAsync(terminal, "applied");
        _ = await terminal.WaitForExitAsync(TestContext.Current.CancellationToken);

        (applied.Input & RuntimeInterop.EnableVirtualTerminalInput).ShouldNotBe(0u);
        (applied.Input & RuntimeInterop.EnableLineInput).ShouldBe(0u);
        (applied.Input & RuntimeInterop.EnableEchoInput).ShouldBe(0u);
        (applied.Input & RuntimeInterop.EnableProcessedInput).ShouldNotBe(0u);
        (applied.Output & RuntimeInterop.EnableVirtualTerminalProcessing).ShouldNotBe(0u);
        (applied.Output & RuntimeInterop.EnableWrapAtEolOutput).ShouldNotBe(0u);
        (applied.Output & RuntimeInterop.DisableNewlineAutoReturn).ShouldNotBe(0u);
        applied.ShouldNotBe(saved);
    }

    /// <summary>Verifies capturing control keys clears processed input in the applied mode.</summary>
    [Fact]
    public async Task Open_WhenCaptureControlKeysIsTrue_ClearsProcessedInputAsync()
    {
        SkipWithoutConPty();
        await using var terminal = WindowsPseudoterminal.Open(["open-apply", "true"]);
        _ = await ReadModesLineAsync(terminal, "saved");
        var applied = await ReadModesLineAsync(terminal, "applied");
        _ = await terminal.WaitForExitAsync(TestContext.Current.CancellationToken);

        (applied.Input & RuntimeInterop.EnableProcessedInput).ShouldBe(0u);
        (applied.Input & RuntimeInterop.EnableVirtualTerminalInput).ShouldNotBe(0u);
    }

    /// <summary>
    /// Verifies the production output-mode-set failure path — <c>WindowsConsoleMode.Enter</c>
    /// restoring the input mode before throwing when the output mode cannot be applied — because
    /// it needs a production injection seam that does not exist yet.
    /// </summary>
    /// <remarks>
    /// <c>WindowsConsoleMode.Enter</c> calls <c>Native.*</c> statics directly; there is no way to
    /// make a real, already-open ConPTY console handle reject <c>SetConsoleMode</c> for the
    /// output handle specifically without corrupting the handle in a way that also breaks the
    /// harness reading the result. Per issue #35's decided scope, that seam is shared with #27
    /// and is a real production change, not test-only work, so it is deliberately out of scope
    /// for this pass. This is left failing-fast via Skip rather than silently omitted.
    /// </remarks>
    [Fact(Skip = "Needs the WindowsConsoleMode.Enter native-call injection seam tracked by #27; " +
                 "see issue #35's decided open-scope note before implementing.")]
    public void Open_WhenOutputModeCannotBeSet_RestoresInputModeBeforeThrowing()
    {
    }

    /// <summary>Verifies a second Dispose is quiet and never re-touches the restored modes.</summary>
    [Fact]
    public async Task Dispose_WhenCalledTwice_RestoresBothModesExactlyOnceAsync()
    {
        SkipWithoutConPty();
        await using var terminal = WindowsPseudoterminal.Open(["dispose-twice"]);
        var first = await ReadModesLineAsync(terminal, "first");
        var second = await ReadModesLineAsync(terminal, "second");
        var exitCode = await terminal.WaitForExitAsync(TestContext.Current.CancellationToken);

        exitCode.ShouldBe(0);
        second.ShouldBe(first);
    }

    /// <summary>Verifies exact bytes cross a real ConPTY-attached console host transport.</summary>
    [Fact]
    public async Task ReadWriteAsync_WhenAttachedToConPty_TransfersExactBytesAsync()
    {
        SkipWithoutConPty();
        await using var terminal = WindowsPseudoterminal.Open(["echo"]);

        await terminal.Input.WriteAsync(
            "input"u8.ToArray(),
            TestContext.Current.CancellationToken);
        await terminal.Input.FlushAsync(TestContext.Current.CancellationToken);

        var output = new byte[6];
        var read = 0;

        while (read < output.Length)
        {
            var chunk = await terminal.Output.ReadAsync(
                output.AsMemory(read),
                TestContext.Current.CancellationToken);

            if (chunk == 0)
            {
                break;
            }

            read += chunk;
        }

        _ = await terminal.WaitForExitAsync(TestContext.Current.CancellationToken);

        read.ShouldBe(output.Length);
        output.ShouldBe("output"u8.ToArray());
    }

    /// <summary>Verifies a ConPTY resize reports only cells, never pixels.</summary>
    [Fact]
    public async Task Resize_WhenConPtyWindowChanges_ReportsCellsOnlyWithoutPixelsAsync()
    {
        SkipWithoutConPty();
        await using var terminal = WindowsPseudoterminal.Open(["resize"], new Size(80, 24));
        var first = await ReadLineAsync(terminal, "cells1");
        terminal.Resize(new Size(132, 43));
        var second = await ReadLineAsync(terminal, "cells2");
        _ = await terminal.WaitForExitAsync(TestContext.Current.CancellationToken);

        first.ShouldContain("pixels1=none");
        second.ShouldContain("cells2=132x43");
        second.ShouldContain("pixels2=none");
    }

    /// <summary>Verifies cancelling a pending read mid-lifecycle still restores the saved modes.</summary>
    [Fact]
    public async Task Open_WhenCancelledDuringLifecycle_RestoresSavedModesAsync()
    {
        SkipWithoutConPty();
        await using var terminal = WindowsPseudoterminal.Open(["cancelled"]);
        var saved = await ReadModesLineAsync(terminal, "saved");
        var ready = await terminal.ReadLineAsync(TestContext.Current.CancellationToken);
        ready.ShouldBe("ready");
        var cancelled = await terminal.ReadLineAsync(TestContext.Current.CancellationToken);
        var restored = await ReadModesLineAsync(terminal, "restored");
        var exitCode = await terminal.WaitForExitAsync(TestContext.Current.CancellationToken);

        exitCode.ShouldBe(0);
        cancelled.ShouldBe("cancelled=true");
        restored.ShouldBe(saved);
    }

    private static void SkipWithoutConPty() =>
        Assert.SkipUnless(OperatingSystem.IsWindows(), "The ConPTY fixture requires Windows.");

    private static async Task<string> ReadLineAsync(WindowsPseudoterminal terminal, string prefix)
    {
        while (true)
        {
            var line = await terminal.ReadLineAsync(TestContext.Current.CancellationToken) ?? throw new IOException($"The probe exited before reporting a '{prefix}' line.");

            // ConsoleHost.Open's VT-mode-entry escape sequences (hide cursor, clear screen, focus
            // reporting, etc.) share this same output stream and can land immediately before the
            // probe's next status line, so the expected prefix isn't necessarily at offset 0.
            var prefixIndex = line.IndexOf(prefix, StringComparison.Ordinal);

            if (prefixIndex >= 0)
            {
                return line[prefixIndex..];
            }
        }
    }

    private static async Task<(uint Input, uint Output)> ReadModesLineAsync(
        WindowsPseudoterminal terminal,
        string label)
    {
        var line = await ReadLineAsync(terminal, $"{label}-input=");
        var match = ModeLinePattern().Match(line);

        return !match.Success
            ? throw new IOException($"The probe reported an unparseable modes line: '{line}'.")
            : ((uint Input, uint Output)) (
            uint.Parse(match.Groups["input"].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            uint.Parse(match.Groups["output"].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture));
    }

    [GeneratedRegex(@"-input=(?<input>[0-9A-Fa-f]{8}) .*-output=(?<output>[0-9A-Fa-f]{8})")]
    private static partial Regex ModeLinePattern();
}
