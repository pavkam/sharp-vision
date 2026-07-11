using SharpVision.Terminal.Capabilities;
using SharpVision.Terminal.Geometry;
using SharpVision.Terminal.Rendering;
using SharpVision.Terminal.Tests.Support;

using Shouldly;

using CapabilitySupport = SharpVision.Terminal.Capabilities.Support;
using RenderMetrics = SharpVision.Terminal.Rendering.Metrics;
using TerminalCapabilities = SharpVision.Terminal.Capabilities.Capabilities;

namespace SharpVision.Terminal.Tests.Rendering;

/// <summary>
/// Verifies commit-on-success rendering, invalidation, cleanup, and backpressure.
/// </summary>
public sealed class RendererTests
{
    /// <summary>
    /// Verifies a successful frame commits and an identical frame becomes a no-op.
    /// </summary>
    [Fact]
    public async Task RenderAsync_WhenWriteSucceeds_CommitsOnlyOnceAsync()
    {
        using var renderer = new Renderer();
        await using var transport = new FakeTransport();
        using var frame = Create("ab");

        var first = await renderer.RenderAsync(
            frame,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken);
        var second = await renderer.RenderAsync(
            frame,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken);

        first.Full.ShouldBeTrue();
        first.Writes.ShouldBe(1);
        first.Bytes.ShouldBeGreaterThan(0);
        second.ShouldBe(new RenderMetrics(
            0,
            0,
            0,
            false,
            second.Elapsed));
        transport.Writes.Count.ShouldBe(1);
    }

    /// <summary>
    /// Verifies explicit and capability invalidation force a complete redraw.
    /// </summary>
    [Fact]
    public async Task RenderAsync_WhenStateIsInvalidated_ForcesFullRedrawAsync()
    {
        using var renderer = new Renderer();
        await using var transport = new FakeTransport();
        using var frame = Create("ab");
        _ = await renderer.RenderAsync(
            frame,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken);

        renderer.Invalidate();
        var explicitResult = await renderer.RenderAsync(
            frame,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken);
        var changedCapabilities = TerminalCapabilities.Conservative with
        {
            ColorDepth = ColorDepth.TrueColor,
        };
        var capabilityResult = await renderer.RenderAsync(
            frame,
            transport,
            changedCapabilities,
            TestContext.Current.CancellationToken);

        explicitResult.Full.ShouldBeTrue();
        capabilityResult.Full.ShouldBeTrue();
    }

    /// <summary>
    /// Verifies changed frame dimensions select a full redraw and replace front geometry.
    /// </summary>
    [Fact]
    public async Task RenderAsync_WhenFrameSizeChanges_ForcesFullRedrawAsync()
    {
        using var renderer = new Renderer();
        await using var transport = new FakeTransport();
        using var first = Create("a");
        using var resized = Create("ab");
        _ = await renderer.RenderAsync(
            first,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken);

        var result = await renderer.RenderAsync(
            resized,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken);
        var unchanged = await renderer.RenderAsync(
            resized,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken);

        result.Full.ShouldBeTrue();
        unchanged.Writes.ShouldBe(0);
    }

    /// <summary>
    /// Verifies synchronized output wraps only non-empty complete frame batches.
    /// </summary>
    [Fact]
    public async Task RenderAsync_WhenSynchronizedOutputIsSupported_WrapsExactBatchAsync()
    {
        using var renderer = new Renderer();
        await using var transport = new FakeTransport();
        using var frame = Create("x");
        var capabilities = TerminalCapabilities.Conservative with
        {
            SynchronizedOutput = new Feature(CapabilitySupport.Supported, Origin.Query),
        };

        _ = await renderer.RenderAsync(
            frame,
            transport,
            capabilities,
            TestContext.Current.CancellationToken);
        _ = await renderer.RenderAsync(
            frame,
            transport,
            capabilities,
            TestContext.Current.CancellationToken);

        transport.Writes.Count.ShouldBe(1);
        transport.Writes[0].AsSpan().StartsWith("\u001b[?2026h"u8).ShouldBeTrue();
        transport.Writes[0].AsSpan().EndsWith("\u001b[?2026l"u8).ShouldBeTrue();
    }

    /// <summary>
    /// Verifies write failure leaves front state unknown and the next frame full.
    /// </summary>
    [Fact]
    public async Task RenderAsync_WhenWriteFails_InvalidatesNextFrameAsync()
    {
        using var renderer = new Renderer();
        await using var transport = new FakeTransport();
        using var frame = Create("ab");
        var failure = new IOException("write failed");
        transport.QueueFailure(failure, prefixBytes: 3);

        var thrown = await Should.ThrowAsync<IOException>(async () =>
            await renderer.RenderAsync(
                frame,
                transport,
                TerminalCapabilities.Conservative,
                TestContext.Current.CancellationToken));
        var recovered = await renderer.RenderAsync(
            frame,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken);

        thrown.ShouldBeSameAs(failure);
        recovered.Full.ShouldBeTrue();
    }

    /// <summary>
    /// Verifies a flush failure also makes terminal state unknown.
    /// </summary>
    [Fact]
    public async Task RenderAsync_WhenFlushFails_InvalidatesNextFrameAsync()
    {
        using var renderer = new Renderer();
        await using var transport = new FakeTransport();
        using var frame = Create("ab");
        transport.FlushFailure = new IOException("flush failed");

        _ = await Should.ThrowAsync<IOException>(async () =>
            await renderer.RenderAsync(
                frame,
                transport,
                TerminalCapabilities.Conservative,
                TestContext.Current.CancellationToken));
        var recovered = await renderer.RenderAsync(
            frame,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken);

        recovered.Full.ShouldBeTrue();
    }

    /// <summary>
    /// Verifies cancellation before output does not disturb known committed state.
    /// </summary>
    [Fact]
    public async Task RenderAsync_WhenCancelledBeforeWrite_PreservesCommittedFrameAsync()
    {
        using var renderer = new Renderer();
        await using var transport = new FakeTransport();
        using var frame = Create("a");
        _ = await renderer.RenderAsync(
            frame,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () =>
            await renderer.RenderAsync(
                frame,
                transport,
                TerminalCapabilities.Conservative,
                cancellation.Token));
        var unchanged = await renderer.RenderAsync(
            frame,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken);

        unchanged.Writes.ShouldBe(0);
    }

    /// <summary>
    /// Verifies cancellation during an uncertain write forces recovery redraw.
    /// </summary>
    [Fact]
    public async Task RenderAsync_WhenCancelledDuringWrite_InvalidatesNextFrameAsync()
    {
        using var renderer = new Renderer();
        await using var transport = new FakeTransport();
        using var frame = Create("a");
        transport.Block();
        using var cancellation = new CancellationTokenSource();
        var pending = renderer.RenderAsync(
            frame,
            transport,
            TerminalCapabilities.Conservative,
            cancellation.Token).AsTask();
        await transport.WriteStarted;

        cancellation.Cancel();
        _ = await Should.ThrowAsync<OperationCanceledException>(pending);
        transport.Release();
        var recovered = await renderer.RenderAsync(
            frame,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken);

        recovered.Full.ShouldBeTrue();
    }

    /// <summary>
    /// Verifies cleanup failure is observable without replacing the write failure.
    /// </summary>
    [Fact]
    public async Task RenderAsync_WhenWriteAndCleanupFail_PreservesOriginalExceptionAsync()
    {
        using var renderer = new Renderer();
        await using var transport = new FakeTransport();
        using var frame = Create("x");
        var capabilities = TerminalCapabilities.Conservative with
        {
            SynchronizedOutput = new Feature(CapabilitySupport.Supported, Origin.Query),
        };
        var original = new IOException("frame failed");
        var cleanup = new IOException("cleanup failed");
        transport.QueueFailure(original, prefixBytes: 2);
        transport.QueueFailure(cleanup);

        var thrown = await Should.ThrowAsync<IOException>(async () =>
            await renderer.RenderAsync(
                frame,
                transport,
                capabilities,
                TestContext.Current.CancellationToken));

        thrown.ShouldBeSameAs(original);
        renderer.LastCleanupException.ShouldBeSameAs(cleanup);
    }

    /// <summary>
    /// Verifies a slow transport naturally backpressures and cannot commit early.
    /// </summary>
    [Fact]
    public async Task RenderAsync_WhenTransportIsBlocked_WaitsBeforeCommitAsync()
    {
        using var renderer = new Renderer();
        await using var transport = new FakeTransport();
        using var frame = Create("a");
        transport.Block();

        var pending = renderer.RenderAsync(
            frame,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken).AsTask();
        await transport.WriteStarted;

        pending.IsCompleted.ShouldBeFalse();
        transport.Release();
        var result = await pending;
        result.Writes.ShouldBe(1);
    }

    /// <summary>
    /// Verifies concurrent render attempts are rejected before shared buffer mutation.
    /// </summary>
    [Fact]
    public async Task RenderAsync_WhenCalledConcurrently_ThrowsInvalidOperationExceptionAsync()
    {
        using var renderer = new Renderer();
        await using var transport = new FakeTransport();
        using var frame = Create("a");
        transport.Block();
        var first = renderer.RenderAsync(
            frame,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken).AsTask();
        await transport.WriteStarted;

        _ = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await renderer.RenderAsync(
                frame,
                transport,
                TerminalCapabilities.Conservative,
                TestContext.Current.CancellationToken));
        transport.Release();
        _ = await first;
    }

    /// <summary>
    /// Verifies a finite batch limit fails before any terminal bytes are attempted.
    /// </summary>
    [Fact]
    public async Task RenderAsync_WhenOutputExceedsLimit_ThrowsBeforeWriteAsync()
    {
        using var renderer = new Renderer(maxOutputBytes: 1);
        await using var transport = new FakeTransport();
        using var frame = Create("a");

        _ = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await renderer.RenderAsync(
                frame,
                transport,
                TerminalCapabilities.Conservative,
                TestContext.Current.CancellationToken));

        transport.Writes.ShouldBeEmpty();
    }

    /// <summary>
    /// Verifies a warmed unchanged frame allocates no managed memory.
    /// </summary>
    [Fact]
    public async Task RenderAsync_WhenFrameIsUnchanged_AllocatesZeroBytesAsync()
    {
        using var renderer = new Renderer();
        await using var transport = new FakeTransport();
        using var frame = Create("unchanged");
        _ = await renderer.RenderAsync(
            frame,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken);

        for (var index = 0; index < 10_000; index++)
        {
            _ = await renderer.RenderAsync(
                frame,
                transport,
                TerminalCapabilities.Conservative,
                CancellationToken.None);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();

        for (var index = 0; index < 10_000; index++)
        {
            _ = await renderer.RenderAsync(
                frame,
                transport,
                TerminalCapabilities.Conservative,
                CancellationToken.None);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        allocated.ShouldBe(0);
    }

    /// <summary>
    /// Verifies renderer disposal is idempotent and never assumes transport ownership.
    /// </summary>
    [Fact]
    public async Task Dispose_WhenCalledTwice_ReleasesOnlyRendererOwnershipAsync()
    {
        var renderer = new Renderer();
        await using var transport = new FakeTransport();
        using var frame = Create("a");
        _ = await renderer.RenderAsync(
            frame,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken);

        renderer.Dispose();
        renderer.Dispose();

        transport.DisposeCount.ShouldBe(0);
        _ = Should.Throw<ObjectDisposedException>(renderer.Invalidate);
    }

    private static Frame Create(string value)
    {
        var frame = new Frame(new Size(value.Length, 1));
        _ = frame.Canvas.Draw(value.AsSpan(), new Point(0, 0));
        return frame;
    }
}
