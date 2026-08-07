// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Rendering;

using SharpVision.Terminal.Capabilities;
using SharpVision.Terminal.Graphics;

/// <summary>Proves explicit borrowed-transport graphics shutdown and unconditional local release.</summary>
public sealed class RendererShutdownTests
{
    /// <summary>Verifies cleanup writes and flushes once before local backend disposal.</summary>
    [Fact]
    public async Task ShutdownAsync_WhenGraphicsWereCommitted_FlushesCleanupAndIsIdempotentAsync()
    {
        var backend = new FakeGraphicsBackend();
        var renderer = new Renderer(backend);
        await using var transport = new FakeTransport();
        using var frame = CreateFrame();
        _ = await renderer.RenderAsync(frame, transport, TerminalCapabilities.Conservative, CancellationToken.None);

        await renderer.ShutdownAsync(transport, CancellationToken.None);
        await renderer.ShutdownAsync(transport, CancellationToken.None);

        Encoding.ASCII.GetString(transport.Writes[^1]).ShouldBe("<cleanup>");
        backend.CleanupPrepareCount.ShouldBe(1);
        backend.CleanupCommitCount.ShouldBe(1);
        backend.DisposeCount.ShouldBe(1);
        transport.Flushes.ShouldBe(2);
    }

    /// <summary>Verifies a renderer without a graphics backend performs byte-quiet local shutdown.</summary>
    [Fact]
    public async Task ShutdownAsync_WhenNoBackendExists_WritesNothingAsync()
    {
        var renderer = new Renderer();
        await using var transport = new FakeTransport();

        await renderer.ShutdownAsync(transport, CancellationToken.None);

        transport.Writes.ShouldBeEmpty();
        transport.Flushes.ShouldBe(0);
        _ = Should.Throw<ObjectDisposedException>(renderer.Invalidate);
    }

    /// <summary>Verifies shutdown cannot overlap an in-flight frame render.</summary>
    [Fact]
    public async Task ShutdownAsync_WhenRenderIsInFlight_RejectsConcurrencyAsync()
    {
        var backend = new FakeGraphicsBackend();
        var renderer = new Renderer(backend);
        await using var transport = new FakeTransport();
        using var frame = CreateFrame();
        transport.Block();
        var render = renderer.RenderAsync(
            frame,
            transport,
            TerminalCapabilities.Conservative,
            CancellationToken.None).AsTask();
        await transport.WriteStarted;

        _ = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await renderer.ShutdownAsync(transport, CancellationToken.None));
        transport.Release();
        _ = await render;
        await renderer.ShutdownAsync(transport, CancellationToken.None);
    }

    /// <summary>Verifies cancellation invalidates remote state while still disposing local ownership.</summary>
    [Fact]
    public async Task ShutdownAsync_WhenCleanupIsCancelled_ReleasesLocalStateAsync()
    {
        var backend = new FakeGraphicsBackend();
        var renderer = new Renderer(backend);
        await using var transport = new FakeTransport();
        using var frame = CreateFrame();
        _ = await renderer.RenderAsync(frame, transport, TerminalCapabilities.Conservative, CancellationToken.None);
        transport.Block();
        using var cancellation = new CancellationTokenSource();
        var shutdown = renderer.ShutdownAsync(transport, cancellation.Token).AsTask();
        await transport.WriteStarted;

        cancellation.Cancel();
        _ = await Should.ThrowAsync<OperationCanceledException>(shutdown);

        backend.InvalidateCount.ShouldBe(1);
        backend.CleanupCommitCount.ShouldBe(0);
        backend.DisposeCount.ShouldBe(1);
        renderer.Dispose();
    }

    /// <summary>Verifies cleanup write failure remains the observed exception after local disposal.</summary>
    [Fact]
    public async Task ShutdownAsync_WhenCleanupWriteFails_PreservesOriginalAndReleasesLocalStateAsync()
    {
        var backend = new FakeGraphicsBackend();
        var renderer = new Renderer(backend);
        await using var transport = new FakeTransport();
        using var frame = CreateFrame();
        _ = await renderer.RenderAsync(frame, transport, TerminalCapabilities.Conservative, CancellationToken.None);
        var failure = new IOException("cleanup write failure");
        transport.QueueFailure(failure);

        var thrown = await Should.ThrowAsync<IOException>(async () =>
            await renderer.ShutdownAsync(transport, CancellationToken.None));

        thrown.ShouldBeSameAs(failure);
        backend.InvalidateCount.ShouldBe(1);
        backend.DisposeCount.ShouldBe(1);
        renderer.Dispose();
    }

    /// <summary>Verifies cleanup flush failure remains the observed exception after local disposal.</summary>
    [Fact]
    public async Task ShutdownAsync_WhenCleanupFlushFails_PreservesOriginalAndReleasesLocalStateAsync()
    {
        var backend = new FakeGraphicsBackend();
        var renderer = new Renderer(backend);
        await using var transport = new FakeTransport();
        using var frame = CreateFrame();
        _ = await renderer.RenderAsync(frame, transport, TerminalCapabilities.Conservative, CancellationToken.None);
        var failure = new IOException("cleanup flush failure");
        transport.QueueFlushFailure(failure);

        var thrown = await Should.ThrowAsync<IOException>(async () =>
            await renderer.ShutdownAsync(transport, CancellationToken.None));

        thrown.ShouldBeSameAs(failure);
        backend.InvalidateCount.ShouldBe(1);
        backend.DisposeCount.ShouldBe(1);
    }

    /// <summary>Verifies the first cleanup diagnostic survives later renders and secondary shutdown failure.</summary>
    [Fact]
    public async Task ShutdownAsync_WhenCleanupDiagnosticAlreadyExists_PreservesFirstFailureAsync()
    {
        var disposeFailure = new IOException("backend dispose failure");
        var backend = new FakeGraphicsBackend { DisposeFailure = disposeFailure };
        var renderer = new Renderer(backend);
        await using var transport = new FakeTransport();
        using var frame = CreateFrame();
        var capabilities = TerminalCapabilities.Conservative with
        {
            SynchronizedOutput = new Feature(CapabilitySupport.Supported, Origin.Query)
        };
        var renderFailure = new IOException("frame write failure");
        var firstCleanupFailure = new IOException("synchronized cleanup failure");
        transport.QueueFailure(renderFailure, prefixBytes: 1);
        transport.QueueFailure(firstCleanupFailure);
        _ = await Should.ThrowAsync<IOException>(async () => await renderer.RenderAsync(
            frame,
            transport,
            capabilities,
            CancellationToken.None));
        renderer.LastCleanupException.ShouldBeSameAs(firstCleanupFailure);
        _ = await renderer.RenderAsync(frame, transport, capabilities, CancellationToken.None);
        var shutdownFailure = new IOException("shutdown write failure");
        transport.QueueFailure(shutdownFailure);

        var thrown = await Should.ThrowAsync<IOException>(async () =>
            await renderer.ShutdownAsync(transport, CancellationToken.None));

        thrown.ShouldBeSameAs(shutdownFailure);
        renderer.LastCleanupException.ShouldBeSameAs(firstCleanupFailure);
    }

    private static Frame CreateFrame()
    {
        var frame = new Frame(new Size(1, 1));
        frame.Canvas.DrawImage(
            GraphicsImage.FromRgba(new Size(1, 1), [1, 2, 3, 255]),
            new Rect(0, 0, 1, 1),
            PlacementMode.Stretch);
        return frame;
    }
}
