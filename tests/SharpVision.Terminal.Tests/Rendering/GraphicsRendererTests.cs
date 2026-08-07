// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Rendering;

using System.Runtime.CompilerServices;

using SharpVision.Terminal.Capabilities;
using SharpVision.Terminal.Graphics;

/// <summary>Proves atomic cell and semantic-graphics renderer transactions.</summary>
[Collection(PerformanceGroup.Name)]
public sealed class GraphicsRendererTests
{
    /// <summary>Verifies a missing backend preserves the ordinary text render as fallback.</summary>
    [Fact]
    public async Task RenderAsync_WhenNoBackendExists_EmitsOnlyTextFallbackAsync()
    {
        using Renderer expectedRenderer = new();
        using Renderer actualRenderer = new();
        await using FakeTransport expectedTransport = new();
        await using FakeTransport actualTransport = new();
        using var expected = CreateFrame(withImage: false);
        using var actual = CreateFrame(withImage: true);

        _ = await expectedRenderer.RenderAsync(
            expected,
            expectedTransport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken);
        _ = await actualRenderer.RenderAsync(
            actual,
            actualTransport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken);

        actualTransport.Writes.Single().ShouldBe(expectedTransport.Writes.Single());
        Encoding.UTF8.GetString(actualTransport.Writes.Single()).ShouldContain("text");
    }

    /// <summary>Verifies resize reconstructs unchanged graphics and commits the new exact front.</summary>
    [Fact]
    public async Task RenderAsync_WhenFrameSizeChanges_ReconstructsGraphicsAndCommitsExactFrontAsync()
    {
        var backend = new FakeGraphicsBackend();
        using Renderer renderer = new(backend);
        await using FakeTransport transport = new();
        var image = CreateImage(1);
        using var first = CreateFrame(new Size(4, 1), image, Ambiguous.Narrow);
        using var resized = CreateFrame(new Size(5, 1), image, Ambiguous.Narrow);
        _ = await renderer.RenderAsync(
            first,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken);

        var changed = await renderer.RenderAsync(
            resized,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken);
        var unchanged = await renderer.RenderAsync(
            resized,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken);

        changed.Full.ShouldBeTrue();
        unchanged.Bytes.ShouldBe(0);
        backend.FullPreparations.ShouldBe([true, true, false]);
        backend.UploadWriteCount.ShouldBe(2);
        backend.PlacementWriteCount.ShouldBe(2);
    }

    /// <summary>Verifies ambiguous-width policy changes reconstruct unchanged graphics.</summary>
    [Fact]
    public async Task RenderAsync_WhenAmbiguousWidthChanges_ReconstructsGraphicsAsync()
    {
        var backend = new FakeGraphicsBackend();
        using Renderer renderer = new(backend);
        await using FakeTransport transport = new();
        var image = CreateImage(1);
        using var narrow = CreateFrame(new Size(4, 1), image, Ambiguous.Narrow);
        using var wide = CreateFrame(new Size(4, 1), image, Ambiguous.Wide);
        _ = await renderer.RenderAsync(
            narrow,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken);

        var changed = await renderer.RenderAsync(
            wide,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken);

        changed.Full.ShouldBeTrue();
        backend.FullPreparations.ShouldBe([true, true]);
        backend.UploadWriteCount.ShouldBe(2);
        backend.PlacementWriteCount.ShouldBe(2);
    }

    /// <summary>Verifies uploads precede cells and placements while removals follow replacement output.</summary>
    [Fact]
    public async Task RenderAsync_WhenGraphicsChange_OrdersBackendLifecycleAroundCellOutputAsync()
    {
        var backend = new FakeGraphicsBackend();
        using Renderer renderer = new(backend);
        await using FakeTransport transport = new();
        using var first = CreateFrame(withImage: true, "old!");
        using var frame = CreateFrame(withImage: true);

        _ = await renderer.RenderAsync(
            first,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken);
        transport.Writes.Clear();

        _ = await renderer.RenderAsync(
            frame,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken);

        var output = Encoding.UTF8.GetString(transport.Writes.Single());
        var upload = output.IndexOf("<upload>", StringComparison.Ordinal);
        var cell = output.IndexOf("text", StringComparison.Ordinal);
        var placement = output.IndexOf("<place>", StringComparison.Ordinal);
        var removal = output.IndexOf("<remove>", StringComparison.Ordinal);
        upload.ShouldBeGreaterThanOrEqualTo(0);
        cell.ShouldBeGreaterThan(upload);
        placement.ShouldBeGreaterThan(cell);
        removal.ShouldBeGreaterThan(placement);
        backend.CommitCount.ShouldBe(2);
    }

    /// <summary>Verifies backend preparation failure occurs before transport I/O.</summary>
    [Fact]
    public async Task RenderAsync_WhenBackendEncodingFails_WritesNothingAndForcesFullRepairAsync()
    {
        var backend = new FakeGraphicsBackend { FailPlacements = true };
        using Renderer renderer = new(backend);
        await using FakeTransport transport = new();
        using var frame = CreateFrame(withImage: true);

        _ = await Should.ThrowAsync<InvalidOperationException>(async () => await renderer.RenderAsync(
            frame,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken));

        transport.Writes.ShouldBeEmpty();
        backend.CommitCount.ShouldBe(0);
        backend.InvalidateCount.ShouldBe(1);
        backend.FailPlacements = false;
        _ = await renderer.RenderAsync(
            frame,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken);
        backend.FullPreparations.ShouldBe([true, true]);
    }

    /// <summary>Verifies backend allocation/preparation failure occurs before transport I/O.</summary>
    [Fact]
    public async Task RenderAsync_WhenBackendPrepareFails_WritesNothingAsync()
    {
        var backend = new FakeGraphicsBackend { FailPrepare = true };
        using Renderer renderer = new(backend);
        await using FakeTransport transport = new();
        using var frame = CreateFrame(withImage: true);

        _ = await Should.ThrowAsync<InvalidOperationException>(async () => await renderer.RenderAsync(
            frame,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken));

        transport.Writes.ShouldBeEmpty();
        backend.CommitCount.ShouldBe(0);
        backend.InvalidateCount.ShouldBe(1);
    }

    /// <summary>Verifies prepare failure after a commit schedules a complete repair.</summary>
    [Fact]
    public async Task RenderAsync_WhenBackendPrepareFailsAfterCommit_RetriesFullAsync()
    {
        var backend = new FakeGraphicsBackend();
        using Renderer renderer = new(backend);
        await using FakeTransport transport = new();
        using var first = CreateFrame(withImage: true, "old!");
        using var second = CreateFrame(withImage: true);
        _ = await renderer.RenderAsync(
            first,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken);
        backend.FailPrepare = true;

        _ = await Should.ThrowAsync<InvalidOperationException>(async () => await renderer.RenderAsync(
            second,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken));

        backend.FailPrepare = false;
        _ = await renderer.RenderAsync(
            second,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken);
        backend.FullPreparations.ShouldBe([true, false, true]);
    }

    /// <summary>Verifies failed terminal output invalidates graphics and does not commit backend state.</summary>
    [Fact]
    public async Task RenderAsync_WhenTransportFails_InvalidatesBackendAndRetriesFullAsync()
    {
        var backend = new FakeGraphicsBackend();
        using Renderer renderer = new(backend);
        await using FakeTransport transport = new();
        using var frame = CreateFrame(withImage: true);
        transport.QueueFailure(new IOException("write failure"));

        _ = await Should.ThrowAsync<IOException>(async () => await renderer.RenderAsync(
            frame,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken));

        backend.CommitCount.ShouldBe(0);
        backend.InvalidateCount.ShouldBe(1);
        _ = await renderer.RenderAsync(
            frame,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken);
        backend.FullPreparations.ShouldBe([true, true]);
        backend.CommitCount.ShouldBe(1);
    }

    /// <summary>Verifies flush failure invalidates backend and semantic front before full repair.</summary>
    [Fact]
    public async Task RenderAsync_WhenGraphicsFlushFails_InvalidatesBackendAndRetriesFullAsync()
    {
        var backend = new FakeGraphicsBackend();
        using Renderer renderer = new(backend);
        await using FakeTransport transport = new();
        using var first = CreateFrame(withImage: true, "old!");
        using var second = CreateFrame(withImage: true);
        _ = await renderer.RenderAsync(
            first,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken);
        var failure = new IOException("flush failure");
        transport.FlushFailure = failure;

        var thrown = await Should.ThrowAsync<IOException>(async () => await renderer.RenderAsync(
            second,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken));
        var recovered = await renderer.RenderAsync(
            second,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken);

        thrown.ShouldBeSameAs(failure);
        backend.InvalidateCount.ShouldBe(1);
        backend.FullPreparations.ShouldBe([true, false, true]);
        recovered.Full.ShouldBeTrue();
    }

    /// <summary>Verifies cancellation during graphics output invalidates and fully repairs state.</summary>
    [Fact]
    public async Task RenderAsync_WhenGraphicsOutputIsCancelled_InvalidatesBackendAndRetriesFullAsync()
    {
        var backend = new FakeGraphicsBackend();
        using Renderer renderer = new(backend);
        await using FakeTransport transport = new();
        using var first = CreateFrame(withImage: true, "old!");
        using var second = CreateFrame(withImage: true);
        _ = await renderer.RenderAsync(
            first,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken);
        transport.Writes.Clear();
        transport.Block();
        using var cancellation = new CancellationTokenSource();
        var pending = renderer.RenderAsync(
            second,
            transport,
            TerminalCapabilities.Conservative,
            cancellation.Token).AsTask();
        await transport.WriteStarted;

        cancellation.Cancel();
        _ = await Should.ThrowAsync<OperationCanceledException>(pending);
        transport.Release();
        var recovered = await renderer.RenderAsync(
            second,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken);

        backend.InvalidateCount.ShouldBe(1);
        backend.CommitCount.ShouldBe(2);
        backend.FullPreparations.ShouldBe([true, false, true]);
        recovered.Full.ShouldBeTrue();
        AssertGraphicsOrder(transport.Writes[^1]);
    }

    /// <summary>Verifies synchronized graphics write and cleanup failures preserve the frame failure.</summary>
    [Fact]
    public async Task RenderAsync_WhenSynchronizedGraphicsWriteAndCleanupFail_PreservesOriginalAsync()
    {
        var backend = new FakeGraphicsBackend();
        using Renderer renderer = new(backend);
        await using FakeTransport transport = new();
        using var frame = CreateFrame(withImage: true);
        var profile = CreateSynchronizedProfile(ColorDepth.Basic16);
        var original = new IOException("graphics write failure");
        var cleanup = new IOException("graphics cleanup failure");
        transport.QueueFailure(original, prefixBytes: 2);
        transport.QueueFailure(cleanup);

        var thrown = await Should.ThrowAsync<IOException>(async () => await renderer.RenderAsync(
            frame,
            transport,
            profile,
            TestContext.Current.CancellationToken));
        var observedCleanup = renderer.LastCleanupException;
        var recovered = await renderer.RenderAsync(
            frame,
            transport,
            profile,
            TestContext.Current.CancellationToken);

        thrown.ShouldBeSameAs(original);
        observedCleanup.ShouldBeSameAs(cleanup);
        backend.InvalidateCount.ShouldBe(1);
        backend.CommitCount.ShouldBe(1);
        backend.FullPreparations.ShouldBe([true, true]);
        recovered.Full.ShouldBeTrue();
        AssertGraphicsOrder(transport.Writes[^1]);
    }

    /// <summary>Verifies synchronized graphics flush and cleanup failures preserve the flush failure.</summary>
    [Fact]
    public async Task RenderAsync_WhenSynchronizedGraphicsFlushAndCleanupFail_PreservesOriginalAsync()
    {
        var backend = new FakeGraphicsBackend();
        using Renderer renderer = new(backend);
        await using FakeTransport transport = new();
        using var frame = CreateFrame(withImage: true);
        var profile = CreateSynchronizedProfile(ColorDepth.Basic16);
        var original = new IOException("graphics flush failure");
        var cleanup = new IOException("graphics cleanup flush failure");
        transport.QueueFlushFailure(original);
        transport.QueueFlushFailure(cleanup);

        var thrown = await Should.ThrowAsync<IOException>(async () => await renderer.RenderAsync(
            frame,
            transport,
            profile,
            TestContext.Current.CancellationToken));
        var observedCleanup = renderer.LastCleanupException;
        var recovered = await renderer.RenderAsync(
            frame,
            transport,
            profile,
            TestContext.Current.CancellationToken);

        thrown.ShouldBeSameAs(original);
        observedCleanup.ShouldBeSameAs(cleanup);
        backend.InvalidateCount.ShouldBe(1);
        backend.CommitCount.ShouldBe(1);
        backend.FullPreparations.ShouldBe([true, true]);
        recovered.Full.ShouldBeTrue();
        AssertGraphicsOrder(transport.Writes[^1]);
    }

    /// <summary>Verifies profile changes reconstruct otherwise unchanged remote graphics.</summary>
    [Fact]
    public async Task RenderAsync_WhenProfileChanges_ReconstructsGraphicsAsync()
    {
        var backend = new FakeGraphicsBackend();
        using Renderer renderer = new(backend);
        await using FakeTransport transport = new();
        using var frame = CreateFrame(withImage: true);
        var first = TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative);
        var second = TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative with
        {
            ColorDepth = ColorDepth.Monochrome
        });
        _ = await renderer.RenderAsync(frame, transport, first, TestContext.Current.CancellationToken);

        var changed = await renderer.RenderAsync(
            frame,
            transport,
            second,
            TestContext.Current.CancellationToken);

        changed.Full.ShouldBeTrue();
        backend.FullPreparations.ShouldBe([true, true]);
        backend.UploadWriteCount.ShouldBe(2);
        backend.PlacementWriteCount.ShouldBe(2);
        backend.CommitCount.ShouldBe(2);
        backend.InvalidateCount.ShouldBe(0);
        AssertGraphicsOrder(transport.Writes[^1]);
    }

    /// <summary>Verifies explicit invalidation reconstructs otherwise unchanged remote graphics.</summary>
    [Fact]
    public async Task Invalidate_WhenGraphicsAreCommitted_ForcesCompleteReconstructionAsync()
    {
        var backend = new FakeGraphicsBackend();
        using Renderer renderer = new(backend);
        await using FakeTransport transport = new();
        using var frame = CreateFrame(withImage: true);
        _ = await renderer.RenderAsync(
            frame,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken);

        renderer.Invalidate();
        var changed = await renderer.RenderAsync(
            frame,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken);

        changed.Full.ShouldBeTrue();
        backend.InvalidateCount.ShouldBe(1);
        backend.FullPreparations.ShouldBe([true, true]);
        backend.CommitCount.ShouldBe(2);
        AssertGraphicsOrder(transport.Writes[^1]);
    }

    /// <summary>Verifies render and disposal are excluded while backend preparation owns the writer.</summary>
    [Fact]
    public async Task RenderAsync_WhenBackendPrepareIsInFlight_ExcludesRenderAndDisposeAsync()
    {
        var backend = new FakeGraphicsBackend();
        var renderer = new Renderer(backend);
        await using FakeTransport transport = new();
        using var frame = CreateFrame(withImage: true);
        backend.BlockPrepare();
        var pending = Task.Run(async () => await renderer.RenderAsync(
            frame,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken));
        await backend.PrepareStarted;

        try
        {
            _ = await Should.ThrowAsync<InvalidOperationException>(async () => await renderer.RenderAsync(
                frame,
                transport,
                TerminalCapabilities.Conservative,
                TestContext.Current.CancellationToken));
            _ = Should.Throw<InvalidOperationException>(renderer.Dispose);
        }
        finally
        {
            backend.ReleasePrepare();
        }

        var completed = await pending;
        var unchanged = await renderer.RenderAsync(
            frame,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken);
        renderer.Dispose();

        completed.Full.ShouldBeTrue();
        unchanged.Bytes.ShouldBe(0);
        backend.CommitCount.ShouldBe(2);
        backend.InvalidateCount.ShouldBe(0);
        backend.DisposeCount.ShouldBe(1);
    }

    /// <summary>Verifies render and disposal are excluded while backend output owns the writer.</summary>
    [Fact]
    public async Task RenderAsync_WhenBackendOutputIsInFlight_ExcludesRenderAndDisposeAsync()
    {
        var backend = new FakeGraphicsBackend();
        var renderer = new Renderer(backend);
        await using FakeTransport transport = new();
        using var frame = CreateFrame(withImage: true);
        transport.Block();
        var pending = renderer.RenderAsync(
            frame,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken).AsTask();
        await transport.WriteStarted;

        _ = await Should.ThrowAsync<InvalidOperationException>(async () => await renderer.RenderAsync(
            frame,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken));
        _ = Should.Throw<InvalidOperationException>(renderer.Dispose);
        transport.Release();
        var completed = await pending;
        var unchanged = await renderer.RenderAsync(
            frame,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken);
        renderer.Dispose();

        completed.Full.ShouldBeTrue();
        unchanged.Bytes.ShouldBe(0);
        backend.CommitCount.ShouldBe(2);
        backend.InvalidateCount.ShouldBe(0);
        backend.DisposeCount.ShouldBe(1);
        AssertGraphicsOrder(transport.Writes.Single());
    }

    /// <summary>Verifies renderer disposal releases its backend exactly once.</summary>
    [Fact]
    public void Dispose_WhenBackendExists_DisposesOwnedBackendOnce()
    {
        var backend = new FakeGraphicsBackend();
        var renderer = new Renderer(backend);

        renderer.Dispose();
        renderer.Dispose();

        backend.DisposeCount.ShouldBe(1);
    }

    /// <summary>Verifies backend cleanup failure cannot leak local ownership or the writer gate.</summary>
    [Fact]
    public async Task Dispose_WhenBackendThrows_ReleasesLocalOwnershipAndRemainsIdempotentAsync()
    {
        var failure = new IOException("backend cleanup failure");
        var backend = new FakeGraphicsBackend { DisposeFailure = failure };
        var renderer = new Renderer(backend);
        await using FakeTransport transport = new();
        var imageReference = CommitImageAndDisposeBack(renderer, transport);

        var thrown = Should.Throw<IOException>(renderer.Dispose);
        renderer.Dispose();
        ForceCollection();

        thrown.ShouldBeSameAs(failure);
        backend.DisposeCount.ShouldBe(1);
        imageReference.TryGetTarget(out _).ShouldBeFalse();
        _ = Should.Throw<ObjectDisposedException>(renderer.Invalidate);
        using var disposedProbe = new Frame(new Size(1, 1));
        _ = await Should.ThrowAsync<ObjectDisposedException>(async () => await renderer.RenderAsync(
            disposedProbe,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken));
    }

    /// <summary>Verifies warmed unchanged semantic placements allocate no managed memory.</summary>
    [Fact]
    public async Task RenderAsync_WhenPlacementIsUnchanged_AllocatesZeroBytesAsync()
    {
        using Renderer renderer = new();
        await using FakeTransport transport = new();
        using var frame = CreateFrame(withImage: true);
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

    private static Frame CreateFrame(bool withImage, string text = "text")
    {
        var frame = new Frame(new Size(4, 1));
        _ = frame.Canvas.Draw(text, default);

        if (withImage)
        {
            frame.Canvas.DrawImage(
                GraphicsImage.FromRgba(new Size(1, 1), [1, 2, 3, 255]),
                new Rect(0, 0, 2, 1),
                PlacementMode.Contain);
        }

        return frame;
    }

    private static Frame CreateFrame(Size size, GraphicsImage image, Ambiguous ambiguous)
    {
        var frame = new Frame(size, ambiguousWidth: ambiguous);
        _ = frame.Canvas.Draw("text", default);
        frame.Canvas.DrawImage(image, new Rect(0, 0, 2, 1), PlacementMode.Contain);
        return frame;
    }

    private static GraphicsImage CreateImage(byte value) =>
        GraphicsImage.FromRgba(new Size(1, 1), [value, value, value, 255]);

    private static TerminalProfile CreateSynchronizedProfile(ColorDepth depth) =>
        TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative with
        {
            ColorDepth = depth,
            SynchronizedOutput = new Feature(CapabilitySupport.Supported, Origin.Query)
        });

    private static void AssertGraphicsOrder(byte[] output)
    {
        var value = Encoding.UTF8.GetString(output);
        var upload = value.IndexOf("<upload>", StringComparison.Ordinal);
        var placement = value.IndexOf("<place>", StringComparison.Ordinal);
        var removal = value.IndexOf("<remove>", StringComparison.Ordinal);
        upload.ShouldBeGreaterThanOrEqualTo(0);
        placement.ShouldBeGreaterThan(upload);

        if (removal >= 0)
        {
            removal.ShouldBeGreaterThan(placement);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<GraphicsImage> CommitImageAndDisposeBack(
        Renderer renderer,
        FakeTransport transport)
    {
        using var frame = CreateFrame(withImage: true);
        var reference = new WeakReference<GraphicsImage>(frame.GetPlacement(0).Image!);
        _ = renderer.RenderAsync(
            frame,
            transport,
            TerminalCapabilities.Conservative,
            CancellationToken.None).AsTask().GetAwaiter().GetResult();
        return reference;
    }

    private static void ForceCollection()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }
}
