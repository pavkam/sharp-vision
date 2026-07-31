// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Graphics.Kitty;

using SharpVision.Terminal.Capabilities;
using SharpVision.Terminal.Graphics;
using SharpVision.Terminal.Multiplexing;

using KittyImage = GraphicsImage;
using MultiplexingOperation = Terminal.Multiplexing.Operation;

/// <summary>Proves the real Kitty backend through renderer commit, failure, and cleanup boundaries.</summary>
[Collection(PerformanceGroup.Name)]
public sealed class BackendRendererTests
{
    private static TerminalCapabilities KittyCapabilities { get; } = TerminalCapabilities.Conservative with
    {
        KittyGraphics = new Feature(CapabilitySupport.Supported, Origin.Override)
    };

    #region Frame transactions

    /// <summary>Verifies an unchanged prepared transaction commits even when it emits no bytes.</summary>
    [Fact]
    public async Task RenderAsync_WhenKittyFrameIsUnchanged_CommitsPreparedNoOpAsync()
    {
        using var renderer = new Renderer(new KittyGraphicsBackend());
        await using var transport = new FakeTransport();
        var image = Image(1);
        using var frame = Frame("a", (image, new Rect(0, 0, 1, 1)));
        _ = await renderer.RenderAsync(frame, transport, KittyCapabilities, CancellationToken.None);
        transport.Writes.Clear();

        var unchanged = await renderer.RenderAsync(
            frame,
            transport,
            KittyCapabilities,
            CancellationToken.None);
        var repeated = await renderer.RenderAsync(
            frame,
            transport,
            KittyCapabilities,
            CancellationToken.None);

        unchanged.Bytes.ShouldBe(0);
        repeated.Bytes.ShouldBe(0);
        transport.Writes.ShouldBeEmpty();
    }

    /// <summary>Verifies cell-only output commits a byte-quiet graphics transaction.</summary>
    [Fact]
    public async Task RenderAsync_WhenOnlyCellsChange_CommitsGraphicsNoOpAsync()
    {
        using var renderer = new Renderer(new KittyGraphicsBackend());
        await using var transport = new FakeTransport();
        var image = Image(1);
        using var first = Frame("a", (image, new Rect(0, 0, 1, 1)));
        using var changed = Frame("b", (image, new Rect(0, 0, 1, 1)));
        _ = await renderer.RenderAsync(first, transport, KittyCapabilities, CancellationToken.None);
        transport.Writes.Clear();

        _ = await renderer.RenderAsync(changed, transport, KittyCapabilities, CancellationToken.None);
        var repeated = await renderer.RenderAsync(
            changed,
            transport,
            KittyCapabilities,
            CancellationToken.None);

        var bytes = transport.Writes.ShouldHaveSingleItem();
        bytes.AsSpan().IndexOf("\u001b_G"u8).ShouldBe(-1);
        repeated.Bytes.ShouldBe(0);
    }

    /// <summary>Verifies a failed new image is deleted before its IDs become reusable.</summary>
    [Fact]
    public async Task RenderAsync_WhenFailedBatchAddedImage_DeletesUncertainImageBeforeIdReuseAsync()
    {
        using var renderer = new Renderer(new KittyGraphicsBackend());
        await using var transport = new FakeTransport();
        var firstImage = Image(1);
        var uncertainImage = Image(2);
        using var committed = Frame("a", (firstImage, new Rect(0, 0, 1, 1)));
        using var failed = Frame(
            "a",
            (firstImage, new Rect(0, 0, 1, 1)),
            (uncertainImage, new Rect(2, 0, 1, 1)));
        _ = await renderer.RenderAsync(committed, transport, KittyCapabilities, CancellationToken.None);
        transport.QueueFailure(new IOException("partial image batch"), prefixBytes: 1);
        _ = await Should.ThrowAsync<IOException>(async () => await renderer.RenderAsync(
            failed,
            transport,
            KittyCapabilities,
            CancellationToken.None));
        transport.Writes.Clear();

        _ = await renderer.RenderAsync(committed, transport, KittyCapabilities, CancellationToken.None);
        var recovery = transport.Writes.ShouldHaveSingleItem();
        transport.Writes.Clear();
        _ = await renderer.RenderAsync(failed, transport, KittyCapabilities, CancellationToken.None);
        var reused = transport.Writes.ShouldHaveSingleItem();

        recovery.AsSpan().IndexOf("\u001b_Ga=d,d=I,i=2,q=2\u001b\\"u8).ShouldBeGreaterThanOrEqualTo(0);
        recovery.AsSpan().IndexOf("d=i,i=2,p=2"u8).ShouldBe(-1);
        reused.AsSpan().IndexOf("a=t,f=32,t=d,s=1,v=1,i=2,q=2"u8).ShouldBeGreaterThanOrEqualTo(0);
        reused.AsSpan().IndexOf("a=p,i=2,p=2"u8).ShouldBeGreaterThanOrEqualTo(0);
    }

    /// <summary>Verifies a failed shared-image placement is deleted before its ID becomes reusable.</summary>
    [Fact]
    public async Task RenderAsync_WhenFailedBatchAddedPlacement_DeletesUncertainPairBeforeIdReuseAsync()
    {
        using var renderer = new Renderer(new KittyGraphicsBackend());
        await using var transport = new FakeTransport();
        var image = Image(1);
        using var committed = Frame("a", (image, new Rect(0, 0, 1, 1)));
        using var failed = Frame(
            "a",
            (image, new Rect(0, 0, 1, 1)),
            (image, new Rect(2, 0, 1, 1)));
        _ = await renderer.RenderAsync(committed, transport, KittyCapabilities, CancellationToken.None);
        transport.QueueFailure(new IOException("partial placement batch"), prefixBytes: 1);
        _ = await Should.ThrowAsync<IOException>(async () => await renderer.RenderAsync(
            failed,
            transport,
            KittyCapabilities,
            CancellationToken.None));
        transport.Writes.Clear();

        _ = await renderer.RenderAsync(committed, transport, KittyCapabilities, CancellationToken.None);
        var recovery = transport.Writes.ShouldHaveSingleItem();
        transport.Writes.Clear();
        _ = await renderer.RenderAsync(failed, transport, KittyCapabilities, CancellationToken.None);
        var reused = transport.Writes.ShouldHaveSingleItem();

        recovery.AsSpan().IndexOf("\u001b_Ga=d,d=i,i=1,p=2,q=2\u001b\\"u8).ShouldBeGreaterThanOrEqualTo(0);
        recovery.AsSpan().IndexOf("d=I,i=1"u8).ShouldBe(-1);
        reused.AsSpan().IndexOf("a=p,i=1,p=2"u8).ShouldBeGreaterThanOrEqualTo(0);
    }

    #endregion

    #region Cleanup and lifetime

    /// <summary>Verifies failure invalidation quarantines newly rented IDs without allocation.</summary>
    [Fact]
    public void Invalidate_WhenPreparedHasRentedIds_QuarantinesWithoutAllocation()
    {
        using var backend = new KittyGraphicsBackend();
        var committedImage = Image(1);
        using var committed = Frame("a", (committedImage, new Rect(0, 0, 1, 1)));
        using var prepared = Frame(
            "a",
            (committedImage, new Rect(0, 0, 1, 1)),
            (Image(2), new Rect(2, 0, 1, 1)));
        _ = backend.Prepare(null, committed, full: true);
        backend.Commit();
        _ = backend.Prepare(committed, prepared, full: false);

        var before = GC.GetAllocatedBytesForCurrentThread();
        backend.Invalidate();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        allocated.ShouldBe(0);
    }

    /// <summary>Verifies real shutdown emits exact hard deletes and commits local identifier release.</summary>
    [Fact]
    public async Task ShutdownAsync_WhenRealBackendOwnsImages_WritesExactDeletesAndReleasesIdsAsync()
    {
        var backend = new KittyGraphicsBackend();
        var renderer = new Renderer(backend);
        await using var transport = new FakeTransport();
        using var frame = Frame(
            "a",
            (Image(1), new Rect(0, 0, 1, 1)),
            (Image(2), new Rect(2, 0, 1, 1)));
        _ = await renderer.RenderAsync(frame, transport, KittyCapabilities, CancellationToken.None);
        transport.Writes.Clear();

        await renderer.ShutdownAsync(transport, CancellationToken.None);

        transport.Writes.ShouldHaveSingleItem().ShouldBe(
            "\u001b_Ga=d,d=I,i=1,q=2\u001b\\\u001b_Ga=d,d=I,i=2,q=2\u001b\\"u8.ToArray());
    }

    /// <summary>Verifies tmux wraps each shutdown delete independently.</summary>
    [Fact]
    public async Task ShutdownAsync_WhenRealBackendUsesTmux_RoutesEachDeleteIndependentlyAsync()
    {
        var route = new Route(new Policy(
            [MultiplexerKind.Tmux],
            TerminalProfile.CreateAnsi(KittyCapabilities),
            PassthroughMode.All,
            paneVisible: true,
            MultiplexingOperation.Graphics));
        var renderer = new Renderer(new KittyGraphicsBackend(route: route));
        await using var transport = new FakeTransport();
        using var frame = Frame(
            "a",
            (Image(1), new Rect(0, 0, 1, 1)),
            (Image(2), new Rect(2, 0, 1, 1)));
        _ = await renderer.RenderAsync(frame, transport, KittyCapabilities, CancellationToken.None);
        transport.Writes.Clear();

        await renderer.ShutdownAsync(transport, CancellationToken.None);

        transport.Writes.ShouldHaveSingleItem().ShouldBe(
            [
                .. Tmux("\u001b_Ga=d,d=I,i=1,q=2\u001b\\"),
                .. Tmux("\u001b_Ga=d,d=I,i=2,q=2\u001b\\")
            ]);
    }

    /// <summary>Verifies shutdown after a partial batch deletes committed and uncertain remote image state.</summary>
    [Fact]
    public async Task ShutdownAsync_WhenRenderFailedWithUncertainImage_DeletesAllPossibleRemoteStateAsync()
    {
        var renderer = new Renderer(new KittyGraphicsBackend());
        await using var transport = new FakeTransport();
        var committedImage = Image(1);
        var uncertainImage = Image(2);
        using var committed = Frame("a", (committedImage, new Rect(0, 0, 1, 1)));
        using var failed = Frame(
            "a",
            (committedImage, new Rect(0, 0, 1, 1)),
            (uncertainImage, new Rect(2, 0, 1, 1)));
        _ = await renderer.RenderAsync(committed, transport, KittyCapabilities, CancellationToken.None);
        transport.QueueFailure(new IOException("partial image batch"), prefixBytes: 1);
        _ = await Should.ThrowAsync<IOException>(async () => await renderer.RenderAsync(
            failed,
            transport,
            KittyCapabilities,
            CancellationToken.None));
        transport.Writes.Clear();

        await renderer.ShutdownAsync(transport, CancellationToken.None);

        transport.Writes.ShouldHaveSingleItem().ShouldBe(
            "\u001b_Ga=d,d=I,i=1,q=2\u001b\\\u001b_Ga=d,d=I,i=2,q=2\u001b\\"u8.ToArray());
    }

    /// <summary>Verifies shutdown hard deletion covers an uncertain placement of a committed image.</summary>
    [Fact]
    public async Task ShutdownAsync_WhenRenderFailedWithUncertainPlacement_AvoidsRedundantSoftDeleteAsync()
    {
        var renderer = new Renderer(new KittyGraphicsBackend());
        await using var transport = new FakeTransport();
        var image = Image(1);
        using var committed = Frame("a", (image, new Rect(0, 0, 1, 1)));
        using var failed = Frame(
            "a",
            (image, new Rect(0, 0, 1, 1)),
            (image, new Rect(2, 0, 1, 1)));
        _ = await renderer.RenderAsync(committed, transport, KittyCapabilities, CancellationToken.None);
        transport.QueueFailure(new IOException("partial placement batch"), prefixBytes: 1);
        _ = await Should.ThrowAsync<IOException>(async () => await renderer.RenderAsync(
            failed,
            transport,
            KittyCapabilities,
            CancellationToken.None));
        transport.Writes.Clear();

        await renderer.ShutdownAsync(transport, CancellationToken.None);

        transport.Writes.ShouldHaveSingleItem().ShouldBe(
            "\u001b_Ga=d,d=I,i=1,q=2\u001b\\"u8.ToArray());
    }

    /// <summary>Verifies valid cleanup commit releases IDs without allocation.</summary>
    [Fact]
    public void CommitCleanup_WhenPrepared_ReleasesIdentifiersWithoutAllocation()
    {
        using var backend = new KittyGraphicsBackend();
        var committedImage = Image(1);
        using var first = Frame("a", (committedImage, new Rect(0, 0, 1, 1)));
        using var uncertain = Frame(
            "a",
            (committedImage, new Rect(0, 0, 1, 1)),
            (Image(2), new Rect(2, 0, 1, 1)));
        using var second = Frame("a", (Image(3), new Rect(0, 0, 1, 1)));
        _ = backend.Prepare(null, first, full: true);
        backend.Commit();
        _ = backend.Prepare(first, uncertain, full: false);
        backend.Invalidate();
        var cleanupCount = backend.PrepareCleanup();
        var cleanup = new ArrayBufferWriter<byte>();
        backend.WriteCleanup(cleanup);

        var before = GC.GetAllocatedBytesForCurrentThread();
        backend.CommitCleanup();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        var result = backend.Prepare(null, second, full: true);
        var bytes = WritePrepared(backend);

        allocated.ShouldBe(0);
        cleanupCount.ShouldBe(2);
        cleanup.WrittenSpan.ToArray().ShouldBe(
            "\u001b_Ga=d,d=I,i=1,q=2\u001b\\\u001b_Ga=d,d=I,i=2,q=2\u001b\\"u8.ToArray());
        result.Uploads.ShouldBe(1);
        Encoding.ASCII.GetString(bytes.Uploads).ShouldContain("i=1,q=2");
    }

    #endregion

    #region Helpers

    private static KittyImage Image(byte value) => KittyImage.FromRgba(
        new Size(1, 1),
        [value, value, value, 255]);

    private static Frame Frame(
        string text,
        params (KittyImage Image, Rect Destination)[] placements)
    {
        var frame = new Frame(new Size(4, 1));
        _ = frame.Canvas.Draw(text, default);

        foreach (var placement in placements)
        {
            frame.Canvas.DrawImage(placement.Image, placement.Destination, PlacementMode.Stretch);
        }

        return frame;
    }

    private static PreparedBytes WritePrepared(KittyGraphicsBackend backend)
    {
        var uploads = new ArrayBufferWriter<byte>();
        var placements = new ArrayBufferWriter<byte>();
        var removals = new ArrayBufferWriter<byte>();
        backend.WriteUploads(uploads);
        backend.WritePlacements(placements);
        backend.WriteRemovals(removals);
        return new PreparedBytes(
            uploads.WrittenSpan.ToArray(),
            placements.WrittenSpan.ToArray(),
            removals.WrittenSpan.ToArray());
    }

    private static byte[] Tmux(string command)
    {
        var output = new ArrayBufferWriter<byte>();
        TmuxWriter.WritePassthrough(
            output,
            Encoding.ASCII.GetBytes(command));
        return output.WrittenSpan.ToArray();
    }

    #endregion
}
