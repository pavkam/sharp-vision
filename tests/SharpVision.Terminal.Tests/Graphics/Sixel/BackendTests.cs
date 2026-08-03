// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Graphics.Sixel;

using SharpVision.Terminal.Capabilities;
using SharpVision.Terminal.Graphics;
using SharpVision.Terminal.Multiplexing;

using MultiplexingOperation = Terminal.Multiplexing.Operation;

/// <summary>Proves non-retained sixel frame transactions and route policy.</summary>
[Collection(PerformanceGroup.Name)]
public sealed class BackendTests
{
    /// <summary>Verifies exact cell pixels anchor a sixel DCS and restore the semantic cursor.</summary>
    [Fact]
    public void Prepare_WhenInitialRgbaPlacementExists_EncodesExactPixelsAndCursor()
    {
        using var backend = new NonRetainedGraphicsBackend(enableSixel: true, enableIterm: false);
        using var frame = Frame("ab", (Red(), new Rect(1, 0, 1, 1)));

        var result = backend.Prepare(null, frame, full: true, Context(new CellMetrics(1, 6)));
        var bytes = WritePlacements(backend);

        result.ShouldBe(new GraphicsBackendResult(
            changed: true,
            uploads: 0,
            placements: 1,
            removals: 0,
            fullCellRedraw: true));
        bytes.ShouldBe(
            "\u001b[1;2H\u001bP0;1;0q\"1;1;1;6#0;2;100;0;0#0~\u001b\\\u001b[1;1H"u8.ToArray());
    }

    /// <summary>Verifies missing exact metrics stay on the ordinary cell fallback.</summary>
    [Fact]
    public void Prepare_WhenMetricsAreMissing_DeclinesWithoutRemoteState()
    {
        using var backend = new NonRetainedGraphicsBackend(enableSixel: true, enableIterm: false);
        using var rgba = Frame("a", (Red(), new Rect(0, 0, 1, 1)));

        var missingMetrics = backend.Prepare(null, rgba, full: true, Context(metrics: null));

        missingMetrics.Changed.ShouldBeFalse();
        WritePlacements(backend).ShouldBeEmpty();
    }

    /// <summary>Verifies a PNG this decoder cannot decode emits no sixel bytes, even though its
    /// format is in principle sixel-encodable and so is not declined at classification.</summary>
    [Fact]
    public void Prepare_WhenPngCannotBeDecoded_EmitsNoSixelBytes()
    {
        using var backend = new NonRetainedGraphicsBackend(enableSixel: true, enableIterm: false);
        using var rgba = Frame("a", (Red(), new Rect(0, 0, 1, 1)));
        using var png = Frame("a", (Png(), new Rect(0, 0, 1, 1)));
        _ = backend.Prepare(null, rgba, full: true, Context(new CellMetrics(1, 6)));
        backend.Commit();

        _ = backend.Prepare(rgba, png, full: false, Context(new CellMetrics(1, 6)));

        WritePlacements(backend).ShouldBeEmpty();
    }

    /// <summary>Verifies placement mutation clears stale pixels by asking the renderer for full cells.</summary>
    [Fact]
    public void Prepare_WhenPlacementMoves_RequestsFullCellRedrawAndReconstructsTarget()
    {
        using var backend = new NonRetainedGraphicsBackend(enableSixel: true, enableIterm: false);
        var image = Red();
        using var first = Frame("ab", (image, new Rect(0, 0, 1, 1)));
        using var moved = Frame("ab", (image, new Rect(1, 0, 1, 1)));
        var context = Context(new CellMetrics(1, 6));
        _ = backend.Prepare(null, first, full: true, context);
        backend.Commit();

        var result = backend.Prepare(first, moved, full: false, context);
        var bytes = WritePlacements(backend);

        result.FullCellRedraw.ShouldBeTrue();
        result.Placements.ShouldBe(1);
        bytes.AsSpan().StartsWith("\u001b[1;2H"u8).ShouldBeTrue();
    }

    /// <summary>Verifies removing an emitted placement requests cells that erase its old pixels.</summary>
    [Fact]
    public void Prepare_WhenRgbaPlacementIsRemoved_RequestsFullCellRedrawWithoutImageBytes()
    {
        using var backend = new NonRetainedGraphicsBackend(enableSixel: true, enableIterm: false);
        using var first = Frame("a", (Red(), new Rect(0, 0, 1, 1)));
        using var removed = Frame("a");
        var context = Context(new CellMetrics(1, 6));
        _ = backend.Prepare(null, first, full: true, context);
        backend.Commit();

        var result = backend.Prepare(first, removed, full: false, context);

        result.Changed.ShouldBeTrue();
        result.FullCellRedraw.ShouldBeTrue();
        result.Placements.ShouldBe(0);
        WritePlacements(backend).ShouldBeEmpty();
    }

    /// <summary>Verifies replacing emitted RGBA with a PNG this decoder cannot decode clears the
    /// prior sixel instead of leaving stale pixels or emitting a partial cursor move.</summary>
    [Fact]
    public void Prepare_WhenRgbaBecomesUndecodablePng_RequestsFullCellRedrawAndEmitsNothing()
    {
        using var backend = new NonRetainedGraphicsBackend(enableSixel: true, enableIterm: false);
        using var first = Frame("a", (Red(), new Rect(0, 0, 1, 1)));
        using var png = Frame("a", (Png(), new Rect(0, 0, 1, 1)));
        var context = Context(new CellMetrics(1, 6));
        _ = backend.Prepare(null, first, full: true, context);
        backend.Commit();

        var result = backend.Prepare(first, png, full: false, context);

        result.Changed.ShouldBeTrue();
        result.FullCellRedraw.ShouldBeTrue();
        result.Placements.ShouldBe(0);
        WritePlacements(backend).ShouldBeEmpty();
    }

    /// <summary>Verifies changed or missing metrics reconstruct or clear prior sixel pixels.</summary>
    [Fact]
    public void Prepare_WhenMetricsChangeOrDisappear_ReconstructsThenClears()
    {
        using var backend = new NonRetainedGraphicsBackend(enableSixel: true, enableIterm: false);
        using var frame = Frame("a", (Red(), new Rect(0, 0, 1, 1)));
        _ = backend.Prepare(null, frame, full: true, Context(new CellMetrics(1, 6)));
        backend.Commit();

        var changed = backend.Prepare(
            frame,
            frame,
            full: false,
            Context(new CellMetrics(new Size(1, 1), new Size(2, 7))));
        var changedBytes = WritePlacements(backend);
        backend.Commit();
        var missing = backend.Prepare(frame, frame, full: false, Context(metrics: null));

        changed.FullCellRedraw.ShouldBeTrue();
        changed.Placements.ShouldBe(1);
        changedBytes.AsSpan().IndexOf("\"1;1;2;7"u8).ShouldBeGreaterThanOrEqualTo(0);
        missing.FullCellRedraw.ShouldBeTrue();
        missing.Placements.ShouldBe(0);
    }

    /// <summary>Verifies cell damage under an unchanged image repaints only the intersecting placement.</summary>
    [Fact]
    public void Prepare_WhenCellDamageIntersectsPlacement_RepaintsWithoutFullCellRedraw()
    {
        using var backend = new NonRetainedGraphicsBackend(enableSixel: true, enableIterm: false);
        var image = Red();
        using var first = Frame("ab", (image, new Rect(0, 0, 1, 1)));
        using var changedUnder = Frame("xb", (image, new Rect(0, 0, 1, 1)));
        using var changedOutside = Frame("xc", (image, new Rect(0, 0, 1, 1)));
        var context = Context(new CellMetrics(1, 6));
        _ = backend.Prepare(null, first, full: true, context);
        backend.Commit();

        var intersecting = backend.Prepare(first, changedUnder, full: false, context);
        backend.Commit();
        var outside = backend.Prepare(changedUnder, changedOutside, full: false, context);

        intersecting.Placements.ShouldBe(1);
        intersecting.FullCellRedraw.ShouldBeFalse();
        outside.Changed.ShouldBeFalse();
    }

    /// <summary>Verifies each DCS is independently tmux-routed while cursor motion remains pane-local.</summary>
    [Fact]
    public void Prepare_WhenTmuxRouteIsAuthorized_RoutesDcsOnly()
    {
        var route = new MultiplexerRoute(new Policy(
            [MultiplexerKind.Tmux],
            TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative),
            PassthroughMode.All,
            paneVisible: true,
            MultiplexingOperation.Graphics));
        using var backend = new NonRetainedGraphicsBackend(enableSixel: true, enableIterm: false, route: route);
        using var frame = Frame("a", (Red(), new Rect(0, 0, 1, 1)));

        _ = backend.Prepare(null, frame, full: true, Context(new CellMetrics(1, 6)));
        var bytes = WritePlacements(backend);
        var sixel = "\u001bP0;1;0q\"1;1;1;6#0;2;100;0;0#0~\u001b\\";

        bytes.ShouldBe([
            .. "\u001b[1;1H"u8,
            .. Tmux(sixel),
            .. "\u001b[1;1H"u8
        ]);
    }

    /// <summary>Verifies GNU Screen routes are rejected before any frame state exists.</summary>
    [Fact]
    public void Constructor_WhenRouteContainsScreen_ThrowsNotSupportedException()
    {
        var route = new MultiplexerRoute(new Policy(
            [MultiplexerKind.Screen],
            TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative),
            PassthroughMode.All,
            paneVisible: true,
            MultiplexingOperation.Graphics));

        _ = Should.Throw<NotSupportedException>(() => new NonRetainedGraphicsBackend(enableSixel: true, enableIterm: false, route: route));
    }

    /// <summary>Verifies non-retained commit, invalidation, and shutdown own no remote identities.</summary>
    [Fact]
    public void Lifetime_WhenTransactionCompletes_IsAllocationFreeAndByteQuiet()
    {
        using var backend = new NonRetainedGraphicsBackend(enableSixel: true, enableIterm: false);
        using var frame = Frame("a", (Red(), new Rect(0, 0, 1, 1)));
        _ = backend.Prepare(null, frame, full: true, Context(new CellMetrics(1, 6)));

        var beforeCommit = GC.GetAllocatedBytesForCurrentThread();
        backend.Commit();
        var commitAllocated = GC.GetAllocatedBytesForCurrentThread() - beforeCommit;
        var beforeInvalidate = GC.GetAllocatedBytesForCurrentThread();
        backend.Invalidate();
        var invalidateAllocated = GC.GetAllocatedBytesForCurrentThread() - beforeInvalidate;

        commitAllocated.ShouldBe(0);
        invalidateAllocated.ShouldBe(0);
        backend.PrepareCleanup().ShouldBe(0);
        var cleanup = new ArrayBufferWriter<byte>();
        backend.WriteCleanup(cleanup);
        cleanup.WrittenCount.ShouldBe(0);
        backend.CommitCleanup();
    }

    /// <summary>Verifies prepared synchronous writers copy bytes without managed allocation.</summary>
    [Fact]
    public void WritePlacements_WhenPrepared_AllocatesNoManagedBytes()
    {
        using var backend = new NonRetainedGraphicsBackend(enableSixel: true, enableIterm: false);
        using var frame = Frame("a", (Red(), new Rect(0, 0, 1, 1)));
        _ = backend.Prepare(null, frame, full: true, Context(new CellMetrics(1, 6)));
        var output = new ArrayBufferWriter<byte>(256);
        backend.WritePlacements(output);
        output.ResetWrittenCount();

        var before = GC.GetAllocatedBytesForCurrentThread();
        backend.WritePlacements(output);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        allocated.ShouldBe(0);
    }

    private static GraphicsContext Context(CellMetrics? metrics) => new(
        TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative with
        {
            Sixel = new Feature(CapabilitySupport.Supported, Origin.Override)
        }),
        metrics);

    private static GraphicsImage Red() => GraphicsImage.FromRgba(
        new Size(1, 1),
        [255, 0, 0, 255]);

    private static GraphicsImage Png() => GraphicsImage.FromPng(
    [
        137, 80, 78, 71, 13, 10, 26, 10,
        0, 0, 0, 13, 73, 72, 68, 82,
        0, 0, 0, 1, 0, 0, 0, 1,
        8, 6, 0, 0, 0,
        0, 0, 0, 0,
        0, 0, 0, 0, 73, 68, 65, 84,
        0, 0, 0, 0,
        0, 0, 0, 0, 73, 69, 78, 68,
        0, 0, 0, 0
    ]);

    private static Frame Frame(
        string text,
        params (GraphicsImage Image, Rect Destination)[] placements)
    {
        var frame = new Frame(new Size(text.Length, 1));
        _ = frame.Canvas.Draw(text, default);

        foreach (var (image, destination) in placements)
        {
            frame.Canvas.DrawImage(image, destination, PlacementMode.Stretch);
        }

        return frame;
    }

    private static byte[] WritePlacements(NonRetainedGraphicsBackend backend)
    {
        var output = new ArrayBufferWriter<byte>();
        backend.WritePlacements(output);
        return output.WrittenSpan.ToArray();
    }

    private static byte[] Tmux(string command)
    {
        var output = new ArrayBufferWriter<byte>();
        TmuxWriter.WritePassthrough(output, Encoding.ASCII.GetBytes(command));
        return output.WrittenSpan.ToArray();
    }
}
