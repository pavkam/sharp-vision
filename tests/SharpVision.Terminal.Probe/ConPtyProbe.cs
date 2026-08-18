// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Probe;

using System.Globalization;
using System.Runtime.Versioning;

using Terminal.Geometry;
using Terminal.Runtime;

/// <summary>
/// Runs one process-isolated Windows console-host lifecycle scenario while this process is
/// itself attached to a real ConPTY (see <c>tests/SharpVision.Terminal.Tests/Support/
/// WindowsPseudoterminal.cs</c>, which spawns this process as the ConPTY's client).
/// </summary>
/// <remarks>
/// A pseudo console can only host modes and streams for a process that was created attached to
/// it — the hosting test process itself cannot self-attach to a ConPTY it creates. This probe
/// is that attached process: it exercises the public
/// <see cref="ConsoleHost"/>/<see cref="ConsoleConnection"/> lifecycle from inside the real
/// pseudo console and reports plain-text facts back over the same channel the fixture already
/// reads as the ConPTY's output.
/// </remarks>
[SupportedOSPlatform("windows")]
internal static class ConPtyProbe
{
    /// <summary>Dispatches to the named scenario.</summary>
    /// <param name="arguments">The scenario name followed by its scenario-specific arguments.</param>
    /// <returns>Zero on a recognized scenario name, non-zero otherwise.</returns>
    internal static async Task<int> RunAsync(string[] arguments)
    {
        if (arguments.Length == 0)
        {
            await WriteLineAsync("error=missing-scenario").ConfigureAwait(false);
            return 1;
        }

        switch (arguments[0])
        {
            case "open-apply":
                await RunOpenApplyAsync(captureControlKeys: arguments.Length > 1 && arguments[1] == "true")
                    .ConfigureAwait(false);
                return 0;

            case "dispose-twice":
                await RunDisposeTwiceAsync().ConfigureAwait(false);
                return 0;

            case "echo":
                await RunEchoAsync().ConfigureAwait(false);
                return 0;

            case "resize":
                await RunResizeAsync().ConfigureAwait(false);
                return 0;

            case "cancelled":
                await RunCancelledAsync().ConfigureAwait(false);
                return 0;

            default:
                await WriteLineAsync($"error=unknown-scenario:{arguments[0]}").ConfigureAwait(false);
                return 1;
        }
    }

    private static async Task RunOpenApplyAsync(bool captureControlKeys)
    {
        var (savedInput, savedOutput) = ReadModes();
        await WriteLineAsync(FormatModes("saved", savedInput, savedOutput)).ConfigureAwait(false);

        await using var connection = ConsoleHost.Open(
            new ConsoleHostOptions { CaptureControlKeys = captureControlKeys });
        var (appliedInput, appliedOutput) = ReadModes();
        await WriteLineAsync(FormatModes("applied", appliedInput, appliedOutput)).ConfigureAwait(false);

        await connection.DisposeAsync().ConfigureAwait(false);
        var (restoredInput, restoredOutput) = ReadModes();
        await WriteLineAsync(FormatModes("restored", restoredInput, restoredOutput)).ConfigureAwait(false);
    }

    private static async Task RunDisposeTwiceAsync()
    {
        var connection = ConsoleHost.Open(new ConsoleHostOptions());

        await connection.DisposeAsync().ConfigureAwait(false);
        var (firstInput, firstOutput) = ReadModes();
        await WriteLineAsync(FormatModes("first", firstInput, firstOutput)).ConfigureAwait(false);

        // The connection guards a second Dispose as a no-op, so this must not touch the
        // console modes again — the restoration in RunOpenApplyAsync's terms happens exactly
        // once (see ConsoleConnection.DisposeAsync).
        await connection.DisposeAsync().ConfigureAwait(false);
        var (secondInput, secondOutput) = ReadModes();
        await WriteLineAsync(FormatModes("second", secondInput, secondOutput)).ConfigureAwait(false);
    }

    private static async Task RunEchoAsync()
    {
        await using var connection = ConsoleHost.Open(new ConsoleHostOptions());
        var buffer = new byte[5];
        var read = 0;

        while (read < buffer.Length)
        {
            var chunk = await connection.Transport
                .ReadAsync(buffer.AsMemory(read), CancellationToken.None)
                .ConfigureAwait(false);

            if (chunk == 0)
            {
                break;
            }

            read += chunk;
        }

        await connection.Transport.WriteAsync("output"u8.ToArray(), CancellationToken.None)
            .ConfigureAwait(false);
        await connection.Transport.FlushAsync(CancellationToken.None).ConfigureAwait(false);
    }

    private static async Task RunResizeAsync()
    {
        await using var connection = ConsoleHost.Open(
            new ConsoleHostOptions { ResizeInterval = TimeSpan.FromMilliseconds(20) });

        var first = await connection.Resize.ReadAsync(CancellationToken.None).ConfigureAwait(false);
        await WriteLineAsync(FormatDimensions("cells1", "pixels1", first)).ConfigureAwait(false);

        var second = await connection.Resize.ReadAsync(CancellationToken.None).ConfigureAwait(false);
        await WriteLineAsync(FormatDimensions("cells2", "pixels2", second)).ConfigureAwait(false);
    }

    private static async Task RunCancelledAsync()
    {
        var (savedInput, savedOutput) = ReadModes();
        await WriteLineAsync(FormatModes("saved", savedInput, savedOutput)).ConfigureAwait(false);

        var connection = ConsoleHost.Open(new ConsoleHostOptions());
        using var cancellation = new CancellationTokenSource();
        var pending = connection.Transport.ReadAsync(new byte[1], cancellation.Token).AsTask();
        await WriteLineAsync("ready").ConfigureAwait(false);

        // Nothing else writes to this ConPTY, so the pending read only ever completes by
        // cancellation — proving the lifecycle can be cancelled mid-flight and still restore
        // the saved modes (see the approved test
        // Open_WhenCancelledDuringLifecycle_RestoresSavedModes).
        await Task.Delay(TimeSpan.FromMilliseconds(200)).ConfigureAwait(false);
        await cancellation.CancelAsync().ConfigureAwait(false);

        try
        {
            _ = await pending.ConfigureAwait(false);
            await WriteLineAsync("cancelled=false").ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await WriteLineAsync("cancelled=true").ConfigureAwait(false);
        }

        await connection.DisposeAsync().ConfigureAwait(false);
        var (restoredInput, restoredOutput) = ReadModes();
        await WriteLineAsync(FormatModes("restored", restoredInput, restoredOutput)).ConfigureAwait(false);
    }

    private static (uint Input, uint Output) ReadModes()
    {
        var input = RuntimeInterop.GetStandardHandle(RuntimeInterop.StdInputHandle);
        var output = RuntimeInterop.GetStandardHandle(RuntimeInterop.StdOutputHandle);
        _ = RuntimeInterop.TryGetConsoleMode(input, out var inputMode);
        _ = RuntimeInterop.TryGetConsoleMode(output, out var outputMode);
        return (inputMode, outputMode);
    }

    private static string FormatModes(string label, uint input, uint output) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{label}-input={input:X8} {label}-output={output:X8}");

    private static string FormatDimensions(string cellsLabel, string pixelsLabel, Dimensions value) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{cellsLabel}={value.Cells.Width}x{value.Cells.Height} {pixelsLabel}={(value.Pixels is { } pixels ? $"{pixels.Width}x{pixels.Height}" : "none")}");

    private static async Task WriteLineAsync(string line)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(line + "\n");
        var stdout = Console.OpenStandardOutput();
        await stdout.WriteAsync(bytes).ConfigureAwait(false);
        await stdout.FlushAsync().ConfigureAwait(false);
    }
}
