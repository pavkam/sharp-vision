// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Rendering;

using SharpVision.Terminal.Capabilities;
using SharpVision.Terminal.Geometry;
using SharpVision.Terminal.Protocols;
using SharpVision.Terminal.Rendering;
using SharpVision.Terminal.Tests.Support;

using Shouldly;

using CapabilitySupport = Terminal.Capabilities.Support;
using RenderMetrics = Terminal.Rendering.Metrics;
using TerminalCapabilities = Terminal.Capabilities.Capabilities;

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
        using Renderer renderer = new Renderer();
        await using FakeTransport transport = new FakeTransport();
        using Frame frame = Create("ab");

        RenderMetrics first = await renderer.RenderAsync(
            frame,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken);
        RenderMetrics second = await renderer.RenderAsync(
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
        using Renderer renderer = new Renderer();
        await using FakeTransport transport = new FakeTransport();
        using Frame frame = Create("ab");
        _ = await renderer.RenderAsync(
            frame,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken);

        renderer.Invalidate();
        RenderMetrics explicitResult = await renderer.RenderAsync(
            frame,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken);
        TerminalCapabilities changedCapabilities = TerminalCapabilities.Conservative with
        {
            ColorDepth = ColorDepth.TrueColor,
        };
        RenderMetrics capabilityResult = await renderer.RenderAsync(
            frame,
            transport,
            changedCapabilities,
            TestContext.Current.CancellationToken);

        explicitResult.Full.ShouldBeTrue();
        capabilityResult.Full.ShouldBeTrue();
    }

    /// <summary>Verifies a color-tier change reprojects the complete semantic frame.</summary>
    [Fact]
    public async Task RenderAsync_WhenColorDepthChanges_WritesDifferentFullRepresentationAsync()
    {
        using Renderer renderer = new Renderer();
        await using FakeTransport transport = new FakeTransport();
        using Frame frame = new Frame(new Size(1, 1));
        _ = frame.Canvas.Draw("x", default, new Style(Color.Rgb(255, 0, 0)));
        TerminalCapabilities trueColor = TerminalCapabilities.Conservative with
        {
            ColorDepth = ColorDepth.TrueColor,
        };

        RenderMetrics first = await renderer.RenderAsync(
            frame,
            transport,
            trueColor,
            TestContext.Current.CancellationToken);
        RenderMetrics second = await renderer.RenderAsync(
            frame,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken);

        first.Full.ShouldBeTrue();
        second.Full.ShouldBeTrue();
        transport.Writes.Count.ShouldBe(2);
        transport.Writes[0].AsSpan().IndexOf("\u001b[38;2;255;0;0m"u8).ShouldBeGreaterThanOrEqualTo(0);
        transport.Writes[1].AsSpan().IndexOf("\u001b[91m"u8).ShouldBeGreaterThanOrEqualTo(0);
        transport.Writes[1].AsSpan().IndexOf("38;2"u8).ShouldBe(-1);
    }

    /// <summary>
    /// Verifies changed frame dimensions select a full redraw and replace front geometry.
    /// </summary>
    [Fact]
    public async Task RenderAsync_WhenFrameSizeChanges_ForcesFullRedrawAsync()
    {
        using Renderer renderer = new Renderer();
        await using FakeTransport transport = new FakeTransport();
        using Frame first = Create("a");
        using Frame resized = Create("ab");
        _ = await renderer.RenderAsync(
            first,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken);

        RenderMetrics result = await renderer.RenderAsync(
            resized,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken);
        RenderMetrics unchanged = await renderer.RenderAsync(
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
        using Renderer renderer = new Renderer();
        await using FakeTransport transport = new FakeTransport();
        using Frame frame = Create("x");
        TerminalCapabilities capabilities = TerminalCapabilities.Conservative with
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
        using Renderer renderer = new Renderer();
        await using FakeTransport transport = new FakeTransport();
        using Frame frame = Create("ab");
        IOException failure = new IOException("write failed");
        transport.QueueFailure(failure, prefixBytes: 3);

        IOException thrown = await Should.ThrowAsync<IOException>(async () =>
            await renderer.RenderAsync(
                frame,
                transport,
                TerminalCapabilities.Conservative,
                TestContext.Current.CancellationToken));
        RenderMetrics recovered = await renderer.RenderAsync(
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
        using Renderer renderer = new Renderer();
        await using FakeTransport transport = new FakeTransport();
        using Frame frame = Create("ab");
        transport.FlushFailure = new IOException("flush failed");

        _ = await Should.ThrowAsync<IOException>(async () =>
            await renderer.RenderAsync(
                frame,
                transport,
                TerminalCapabilities.Conservative,
                TestContext.Current.CancellationToken));
        RenderMetrics recovered = await renderer.RenderAsync(
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
        using Renderer renderer = new Renderer();
        await using FakeTransport transport = new FakeTransport();
        using Frame frame = Create("a");
        _ = await renderer.RenderAsync(
            frame,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken);
        using CancellationTokenSource cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () =>
            await renderer.RenderAsync(
                frame,
                transport,
                TerminalCapabilities.Conservative,
                cancellation.Token));
        RenderMetrics unchanged = await renderer.RenderAsync(
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
        using Renderer renderer = new Renderer();
        await using FakeTransport transport = new FakeTransport();
        using Frame frame = Create("a");
        transport.Block();
        using CancellationTokenSource cancellation = new CancellationTokenSource();
        Task<RenderMetrics> pending = renderer.RenderAsync(
            frame,
            transport,
            TerminalCapabilities.Conservative,
            cancellation.Token).AsTask();
        await transport.WriteStarted;

        cancellation.Cancel();
        _ = await Should.ThrowAsync<OperationCanceledException>(pending);
        transport.Release();
        RenderMetrics recovered = await renderer.RenderAsync(
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
        using Renderer renderer = new Renderer();
        await using FakeTransport transport = new FakeTransport();
        using Frame frame = Create("x");
        TerminalCapabilities capabilities = TerminalCapabilities.Conservative with
        {
            SynchronizedOutput = new Feature(CapabilitySupport.Supported, Origin.Query),
        };
        IOException original = new IOException("frame failed");
        IOException cleanup = new IOException("cleanup failed");
        transport.QueueFailure(original, prefixBytes: 2);
        transport.QueueFailure(cleanup);

        IOException thrown = await Should.ThrowAsync<IOException>(async () =>
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
        using Renderer renderer = new Renderer();
        await using FakeTransport transport = new FakeTransport();
        using Frame frame = Create("a");
        transport.Block();

        Task<RenderMetrics> pending = renderer.RenderAsync(
            frame,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken).AsTask();
        await transport.WriteStarted;

        pending.IsCompleted.ShouldBeFalse();
        transport.Release();
        RenderMetrics result = await pending;
        result.Writes.ShouldBe(1);
    }

    /// <summary>
    /// Verifies concurrent render attempts are rejected before shared buffer mutation.
    /// </summary>
    [Fact]
    public async Task RenderAsync_WhenCalledConcurrently_ThrowsInvalidOperationExceptionAsync()
    {
        using Renderer renderer = new Renderer();
        await using FakeTransport transport = new FakeTransport();
        using Frame frame = Create("a");
        transport.Block();
        Task<RenderMetrics> first = renderer.RenderAsync(
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
        using Renderer renderer = new Renderer(maxOutputBytes: 1);
        await using FakeTransport transport = new FakeTransport();
        using Frame frame = Create("a");

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
        using Renderer renderer = new Renderer();
        await using FakeTransport transport = new FakeTransport();
        using Frame frame = Create("unchanged");
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
        Renderer renderer = new Renderer();
        await using FakeTransport transport = new FakeTransport();
        using Frame frame = Create("a");
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
        Frame frame = new Frame(new Size(value.Length, 1));
        _ = frame.Canvas.Draw(value.AsSpan(), new Point(0, 0));
        return frame;
    }
}
