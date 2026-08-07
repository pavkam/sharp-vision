// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.


namespace SharpVision.Terminal.Tests.Transport;


/// <summary>
/// Verifies real Unix pseudoterminal transport, EOF, and resize behavior.
/// </summary>
[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
public sealed class PseudoterminalTests
{
    /// <summary>Verifies exact bytes cross a real PTY in both directions.</summary>
    [Fact]
    public async Task ReadWriteAsync_WhenUsingUnixPseudoterminal_TransfersExactBytesAsync()
    {
        Assert.SkipUnless(
            OperatingSystem.IsLinux() || OperatingSystem.IsMacOS(),
            "Unix pseudoterminals require Linux or macOS.");
        await using var terminal = UnixPseudoterminal.Open();
        await using StreamTransport transport = new(
            terminal.Slave,
            terminal.Slave,
            leaveOpen: true);
        var destination = new byte[5];

        await terminal.Master.WriteAsync(
            "input"u8.ToArray(),
            TestContext.Current.CancellationToken);
        await terminal.Master.FlushAsync(TestContext.Current.CancellationToken);
        var read = await transport.ReadAsync(
            destination,
            TestContext.Current.CancellationToken);
        await transport.WriteAsync(
            "output"u8.ToArray(),
            TestContext.Current.CancellationToken);
        await transport.FlushAsync(TestContext.Current.CancellationToken);
        var output = new byte[6];
        var written = await terminal.Master.ReadAsync(
            output,
            TestContext.Current.CancellationToken);

        read.ShouldBe(destination.Length);
        destination.ShouldBe("input"u8.ToArray());
        written.ShouldBe(output.Length);
        output.ShouldBe("output"u8.ToArray());
    }

    /// <summary>Verifies closing the PTY master becomes transport EOF.</summary>
    /// <remarks>
    /// The outcome the kernel reports here is not stable. Measured on Linux x64 over 400 closes,
    /// 391 reads returned zero and 9 raised EIO, while macOS returned zero every time; the split
    /// depends on how the close and the read interleave. This asserts the single behaviour callers
    /// are promised, which StreamTransport now guarantees by translating the hang-up errno rather
    /// than letting it reach the session as a fault.
    /// </remarks>
    [Fact]
    public async Task ReadAsync_WhenPseudoterminalMasterCloses_ReturnsEndOfFileAsync()
    {
        Assert.SkipUnless(
            OperatingSystem.IsLinux() || OperatingSystem.IsMacOS(),
            "Unix pseudoterminals require Linux or macOS.");
        await using var terminal = UnixPseudoterminal.Open();
        await using StreamTransport transport = new(
            terminal.Slave,
            terminal.Slave,
            leaveOpen: true);
        await terminal.CloseMasterAsync();

        var read = await transport.ReadAsync(
            new byte[1],
            TestContext.Current.CancellationToken);

        read.ShouldBe(0);
    }

    /// <summary>Verifies SIGWINCH makes the newest cell and pixel size observable.</summary>
    [Fact]
    public async Task ReadAsync_WhenPseudoterminalResizes_ReturnsNewestDimensionsAsync()
    {
        Assert.SkipUnless(
            OperatingSystem.IsLinux() || OperatingSystem.IsMacOS(),
            "Unix pseudoterminals require Linux or macOS.");
        var initial = new Dimensions(new Size(80, 24), new Size(800, 480));
        var expected = new Dimensions(new Size(132, 43), initial.Pixels);
        await using var terminal = UnixPseudoterminal.Open(initial);
        await using UnixResizeSource source = new(terminal.SlaveDescriptor);

        var first = await source.ReadAsync(TestContext.Current.CancellationToken);
        terminal.SetWindowSize(expected);
        terminal.SignalWindowChange();
        var resized = await source.ReadAsync(TestContext.Current.CancellationToken);

        first.ShouldBe(initial);
        resized.ShouldBe(expected);
    }

    /// <summary>Verifies the synchronous snapshot consumes the construction wakeup, so the startup
    /// size is published once rather than twice.
    ///
    /// <para>The constructor seeds one wakeup so a ReadAsync-only consumer still gets an initial
    /// observation. <c>TryReadCurrent</c> is that initial observation for every consumer that has
    /// one - the runtime session routes it through the same startup readiness path - but it left
    /// the seed buffered, so the very next ReadAsync returned immediately with the identical
    /// dimensions and the session published the same geometry a second time.</para>
    /// </summary>
    [Fact]
    public async Task ReadAsync_AfterTryReadCurrent_DoesNotRepublishTheSameDimensionsAsync()
    {
        Assert.SkipUnless(
            OperatingSystem.IsLinux() || OperatingSystem.IsMacOS(),
            "Unix pseudoterminals require Linux or macOS.");
        var initial = new Dimensions(new Size(80, 24), new Size(800, 480));
        var expected = new Dimensions(new Size(132, 43), initial.Pixels);
        await using var terminal = UnixPseudoterminal.Open(initial);
        await using UnixResizeSource source = new(terminal.SlaveDescriptor);

        source.TryReadCurrent(out var snapshot).ShouldBeTrue();
        var pending = source.ReadAsync(TestContext.Current.CancellationToken).AsTask();

        // The next read must be waiting on a genuine SIGWINCH, not on a leftover seed. Observed
        // by giving it time to complete spuriously and then proving only the real resize satisfies
        // it - a duplicate would have completed with `initial` long before this point.
        await Task.Delay(100, TestContext.Current.CancellationToken);
        pending.IsCompleted.ShouldBeFalse(
            "the construction wakeup belongs to the snapshot already taken");

        terminal.SetWindowSize(expected);
        terminal.SignalWindowChange();

        snapshot.ShouldBe(initial);
        (await pending).ShouldBe(expected);
    }

    /// <summary>The counter-case that keeps the drain honest: a consumer that never takes the
    /// snapshot must still get its initial observation from ReadAsync alone, which is the whole
    /// reason the constructor seeds a wakeup.</summary>
    [Fact]
    public async Task ReadAsync_WhenTryReadCurrentIsNeverCalled_StillDeliversTheInitialSizeAsync()
    {
        Assert.SkipUnless(
            OperatingSystem.IsLinux() || OperatingSystem.IsMacOS(),
            "Unix pseudoterminals require Linux or macOS.");
        var initial = new Dimensions(new Size(80, 24), new Size(800, 480));
        await using var terminal = UnixPseudoterminal.Open(initial);
        await using UnixResizeSource source = new(terminal.SlaveDescriptor);

        var first = await source.ReadAsync(TestContext.Current.CancellationToken);

        first.ShouldBe(initial);
    }

    /// <summary>Verifies the native size boundary without signal delivery.</summary>
    [Fact]
    public async Task GetDimensions_WhenPseudoterminalHasSize_ReturnsExactDimensionsAsync()
    {
        Assert.SkipUnless(
            OperatingSystem.IsLinux() || OperatingSystem.IsMacOS(),
            "Unix pseudoterminals require Linux or macOS.");
        var expected = new Dimensions(new Size(91, 31), new Size(1092, 620));
        await using var terminal = UnixPseudoterminal.Open(expected);

        var actual = RuntimeInterop.GetDimensions(terminal.SlaveDescriptor);

        actual.ShouldBe(expected);
    }

    /// <summary>Verifies a real SIGWINCH resize crosses the serialized runtime.</summary>
    [Fact]
    public async Task RunAsync_WhenPseudoterminalResizes_DeliversNewestDimensionsAsync()
    {
        Assert.SkipUnless(
            OperatingSystem.IsLinux() || OperatingSystem.IsMacOS(),
            "Unix pseudoterminals require Linux or macOS.");
        var initial = new Dimensions(new Size(80, 24), new Size(800, 480));
        var expected = new Dimensions(new Size(144, 48), initial.Pixels);
        await using var terminal = UnixPseudoterminal.Open(initial);
        await using StreamTransport transport = new(
            terminal.Slave,
            terminal.Slave,
            leaveOpen: true);
        await using UnixResizeSource source = new(terminal.SlaveDescriptor);
        var sink = new RuntimeSink(expected);
        await using Session session = new(
            transport,
            source,
            sink,
            TerminalOptions.Minimal);
        using var cancellation = new CancellationTokenSource();
        var running = session.RunAsync(cancellation.Token).AsTask();

        terminal.SetWindowSize(expected);
        terminal.SignalWindowChange();
        var resized = await sink.Expected.Task.WaitAsync(TestContext.Current.CancellationToken);
        cancellation.Cancel();
        _ = await Should.ThrowAsync<OperationCanceledException>(running);

        resized.ShouldBe(expected);
        sink.Faults.ShouldBeEmpty();
    }

    /// <summary>
    /// Verifies disposing a <see cref="UnixResizeSource"/> while a <c>ReadAsync</c> call is
    /// blocked waiting for the next signal completes that pending read with
    /// <see cref="ObjectDisposedException"/> - the same exception every entry-point check on this
    /// type already throws once disposed - rather than an unmapped
    /// <see cref="ChannelClosedException"/> that no documented contract
    /// on this type promises or that any caller is prepared to handle.
    /// </summary>
    [Fact]
    public async Task DisposeAsync_WhenReadAsyncIsPending_CompletesItWithObjectDisposedExceptionAsync()
    {
        Assert.SkipUnless(
            OperatingSystem.IsLinux() || OperatingSystem.IsMacOS(),
            "Unix pseudoterminals require Linux or macOS.");
        await using var terminal = UnixPseudoterminal.Open();
        var source = new UnixResizeSource(terminal.SlaveDescriptor);

        // Drain the construction-time wakeup so the next ReadAsync genuinely blocks.
        _ = await source.ReadAsync(TestContext.Current.CancellationToken);
        var pending = source.ReadAsync(TestContext.Current.CancellationToken).AsTask();

        await source.DisposeAsync();

        _ = await Should.ThrowAsync<ObjectDisposedException>(pending);
    }
}
