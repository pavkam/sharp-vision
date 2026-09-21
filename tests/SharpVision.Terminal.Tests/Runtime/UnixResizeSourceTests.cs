// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Runtime;

#pragma warning disable SYSLIB1054 // A raw self-signal import keeps this test class non-partial and explicit.

/// <summary>Verifies Unix SIGWINCH resize polling distinguishes a measurement failure from a
/// genuine suspend, mirroring <see cref="ConsoleResizeSourceTests"/>'s contract for the portable
/// sibling.</summary>
[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
public sealed class UnixResizeSourceTests
{
    private const int _sigwinch = 28;

    /// <summary>Verifies a transient measurement failure is never published as a resize, and that
    /// waiting for the next SIGWINCH wakeup recovers and reports the real dimensions once
    /// measurement succeeds again.</summary>
    [Fact]
    public async Task ReadAsync_WhenMeasurementFailsThenRecovers_NeverPublishesTheFailureAsync()
    {
        Assert.SkipUnless(
            OperatingSystem.IsLinux() || OperatingSystem.IsMacOS(),
            "SIGWINCH terminal resize requires Linux or macOS.");
        var calls = 0;
        using var measured = new SemaphoreSlim(0);
        var expected = new Dimensions(new Size(80, 24));
        await using var source = new UnixResizeSource(0, _ =>
        {
            var call = Interlocked.Increment(ref calls);
            _ = measured.Release();

            return call switch
            {
                1 or 2 => throw new IOException("The terminal dimensions could not be read."),
                _ => expected
            };
        });

        var reading = source.ReadAsync(TestContext.Current.CancellationToken).AsTask();

        // The construction-time seed satisfies the first (failing) measurement, after which the
        // loop blocks on the next channel wakeup; every later attempt needs a genuine SIGWINCH.
        // Wait for each measurement to have actually run before raising the next signal, so a
        // signal delivered too early cannot coalesce with one still pending in the bounded
        // channel and be lost.
        await measured.WaitAsync(TestContext.Current.CancellationToken);
        RaiseSigwinch();
        await measured.WaitAsync(TestContext.Current.CancellationToken);
        RaiseSigwinch();

        var dimensions = await reading;

        dimensions.ShouldBe(expected);
        calls.ShouldBe(3);
    }

    /// <summary>Verifies TryReadCurrent reports failure rather than a synthesized dimension when
    /// the measurement boundary throws.</summary>
    [Fact]
    public async Task TryReadCurrent_WhenMeasurementFails_ReturnsFalseAsync()
    {
        Assert.SkipUnless(
            OperatingSystem.IsLinux() || OperatingSystem.IsMacOS(),
            "SIGWINCH terminal resize requires Linux or macOS.");
        await using var source = new UnixResizeSource(
            0,
            static _ => throw new IOException("The terminal dimensions could not be read."));

        var result = source.TryReadCurrent(out var value);

        result.ShouldBeFalse();
        value.ShouldBe(default);
    }

    /// <summary>Verifies TryReadCurrent reports the measured dimensions when the measurement
    /// boundary succeeds.</summary>
    [Fact]
    public async Task TryReadCurrent_WhenMeasurementSucceeds_ReturnsTrueAsync()
    {
        Assert.SkipUnless(
            OperatingSystem.IsLinux() || OperatingSystem.IsMacOS(),
            "SIGWINCH terminal resize requires Linux or macOS.");
        var expected = new Dimensions(new Size(80, 24));
        await using var source = new UnixResizeSource(0, _ => expected);

        var result = source.TryReadCurrent(out var value);

        result.ShouldBeTrue();
        value.ShouldBe(expected);
    }

    /// <summary>
    /// Verifies disposing a <see cref="UnixResizeSource"/> while a <c>ReadAsync</c> call is
    /// blocked waiting for the next signal completes that pending read with
    /// <see cref="ObjectDisposedException"/>, as documented on the type, rather than letting the
    /// measurement failure path swallow it.
    /// </summary>
    [Fact]
    public async Task DisposeAsync_WhenReadAsyncIsPending_CompletesItWithObjectDisposedExceptionAsync()
    {
        Assert.SkipUnless(
            OperatingSystem.IsLinux() || OperatingSystem.IsMacOS(),
            "SIGWINCH terminal resize requires Linux or macOS.");
        var source = new UnixResizeSource(0, _ => new Dimensions(new Size(80, 24)));

        // Drain the construction-time wakeup so the next ReadAsync genuinely blocks.
        _ = await source.ReadAsync(TestContext.Current.CancellationToken);
        var pending = source.ReadAsync(TestContext.Current.CancellationToken).AsTask();

        await source.DisposeAsync();

        _ = await Should.ThrowAsync<ObjectDisposedException>(pending);
    }

    /// <summary>Delivers a real SIGWINCH to this process so every currently registered Unix resize
    /// source observes a wakeup, exactly as a terminal-driven resize would.</summary>
    /// <exception cref="IOException">The signal cannot be delivered.</exception>
    private static void RaiseSigwinch()
    {
        if (Kill(GetProcessId(), _sigwinch) != 0)
        {
            throw new IOException("SIGWINCH could not be delivered.");
        }
    }

    [DllImport("libc", EntryPoint = "kill", ExactSpelling = true, SetLastError = true)]
    private static extern int Kill(int processId, int signal);

    [DllImport("libc", EntryPoint = "getpid", ExactSpelling = true)]
    private static extern int GetProcessId();
}

#pragma warning restore SYSLIB1054
