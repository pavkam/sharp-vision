// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Graphics.Backends;

using System.Buffers.Binary;

using SharpVision.Terminal.Capabilities;
using SharpVision.Terminal.Graphics;
using SharpVision.Terminal.Iterm;
using SharpVision.Terminal.Multiplexing;

/// <summary>
/// Proves the shared non-retained sixel/iTerm backend degrades deterministically instead of
/// crashing when placements combine to exceed the prepared-transaction byte budget; non-retained
/// iTerm2 multipart frame transactions and route policy; and non-retained sixel frame transactions
/// and route policy.
/// </summary>
[Collection(PerformanceGroup.Name)]
public sealed class NonRetainedGraphicsBackendTests
{
    /// <summary>Verifies placements that each individually fit the full budget in isolation, but
    /// whose combined encoded bytes exceed it once written into the shared frame buffer, are
    /// skipped instead of crashing Prepare() partway through the frame.</summary>
    [Fact]
    public void Prepare_WhenCombinedPlacementsExceedRemainingBudget_SkipsOverflowingPlacementsInstead()
    {
        var image = GradientImage();
        using var backend = new NonRetainedGraphicsBackend(enableSixel: true, enableIterm: false, maxPreparedBytes: 600);
        using var frame = new Frame(new Size(16, 1));

        for (var index = 0; index < 4; index++)
        {
            frame.Canvas.DrawImage(image, new Rect(index * 4, 0, 4, 1), PlacementMode.Stretch);
        }

        var profile = TerminalProfile.CreateAnsi(
            TerminalCapabilities.Conservative with
            {
                Sixel = new Feature(CapabilitySupport.Supported, Origin.Override)
            });
        var context = new GraphicsContext(profile, new CellMetrics(1, 24));

        var result = Should.NotThrow(() => backend.Prepare(null, frame, full: true, context));

        result.Placements.ShouldBeLessThan(4);
        result.Placements.ShouldBeGreaterThan(0);
        result.SkippedPlacements.Count.ShouldBe(4 - result.Placements);
        result.SkippedPlacements.ShouldAllBe(
            diagnostic => diagnostic.Reason == GraphicsPlacementSkipReason.OutputLimitExceeded);
    }

    /// <summary>Verifies that when a placement's sixel encoding fails outright (budget too small
    /// for even the smallest frame) and no fallback succeeds, WritePlacements produces exactly
    /// zero bytes rather than an orphaned cursor-move escape left over from the attempt that never
    /// actually wrote its sixel data.</summary>
    [Fact]
    public void Prepare_WhenSixelWriteFailsAndNoPlacementSucceeds_WritePlacementsProducesNoBytes()
    {
        var image = GradientImage();
        using var backend = new NonRetainedGraphicsBackend(enableSixel: true, enableIterm: false, maxPreparedBytes: 20);
        using var frame = new Frame(new Size(4, 1));
        frame.Canvas.DrawImage(image, new Rect(0, 0, 4, 1), PlacementMode.Stretch);

        var profile = TerminalProfile.CreateAnsi(
            TerminalCapabilities.Conservative with
            {
                Sixel = new Feature(CapabilitySupport.Supported, Origin.Override)
            });
        var context = new GraphicsContext(profile, new CellMetrics(1, 24));

        var result = Should.NotThrow(() => backend.Prepare(null, frame, full: true, context));

        result.Placements.ShouldBe(0);
        var destination = new ArrayBufferWriter<byte>();
        backend.WritePlacements(destination);
        destination.WrittenCount.ShouldBe(0);
    }

    /// <summary>Verifies a full-source PNG is anchored, emitted inline, and followed by cursor restoration.</summary>
    [Fact]
    public void Prepare_WhenInitialPngPlacementExists_EncodesCellsAndCursor()
    {
        using var backend = new NonRetainedGraphicsBackend(enableSixel: false, enableIterm: true);
        using var frame = ItermFrame("ab", (Png(), new Rect(1, 0, 1, 1), PlacementMode.Contain));

        var result = backend.Prepare(null, frame, full: true, Context());
        var bytes = WritePlacements(backend);

        result.ShouldBe(new GraphicsBackendResult(
            changed: true,
            uploads: 0,
            placements: 1,
            removals: 0,
            fullCellRedraw: true));
        bytes.AsSpan().StartsWith(
            "\u001b[1;2H\u001b]1337;MultipartFile=size=57;width=1;height=1;preserveAspectRatio=1;inline=1\u001b\\"u8)
            .ShouldBeTrue();
        bytes.AsSpan().EndsWith("\u001b]1337;FileEnd\u001b\\\u001b[1;1H"u8).ShouldBeTrue();
    }

    /// <summary>Verifies an iTerm transaction that exactly consumes the advertised remainder is
    /// declined because the complete prepared budget must also hold its anchor and final cursor.</summary>
    [Fact]
    public void Prepare_WhenItermTransactionLeavesNoCursorBudget_SkipsWithoutThrowing()
    {
        var image = Png();
        var itermImage = GraphicsImage.FromPng(image.Source);
        var transaction = new ArrayBufferWriter<byte>();
        ItermWriter.Write(itermImage, new Size(1, 1), PlacementMode.Contain, transaction);
        using var backend = new NonRetainedGraphicsBackend(
            enableSixel: false,
            enableIterm: true,
            maxPreparedBytes: transaction.WrittenCount);
        using var frame = ItermFrame("a", (image, new Rect(0, 0, 1, 1), PlacementMode.Contain));

        var result = Should.NotThrow(() => backend.Prepare(null, frame, full: true, Context()));

        result.Placements.ShouldBe(0);
        result.SkippedPlacements.ShouldHaveSingleItem().Reason.ShouldBe(
            GraphicsPlacementSkipReason.OutputLimitExceeded);
        WritePlacements(backend).ShouldBeEmpty();
    }

    /// <summary>Verifies direct and routed multi-placement output reserves multi-digit anchor and
    /// final cursor bytes as part of the exact complete prepared-output boundary.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Prepare_WhenCompleteItermOutputMeetsExactBudget_WritesAllWithoutOverflow(bool routed)
    {
        var route = routed ? TmuxRoute() : null;
        using var frame = new Frame(new Size(12, 12));
        frame.Canvas.DrawImage(Png(), new Rect(0, 0, 1, 1), PlacementMode.Contain);
        frame.Canvas.DrawImage(Png(), new Rect(10, 10, 1, 1), PlacementMode.Contain);
        frame.SetCursor(new Point(11, 11), visible: true);
        using var reference = new NonRetainedGraphicsBackend(
            enableSixel: false,
            enableIterm: true,
            route: route);
        _ = reference.Prepare(null, frame, full: true, Context());
        var exactBytes = WritePlacements(reference).Length;
        using var exact = new NonRetainedGraphicsBackend(
            enableSixel: false,
            enableIterm: true,
            maxPreparedBytes: exactBytes,
            route: route);

        var result = Should.NotThrow(() => exact.Prepare(null, frame, full: true, Context()));
        var output = WritePlacements(exact);

        result.Placements.ShouldBe(2);
        result.SkippedPlacements.ShouldBeEmpty();
        output.Length.ShouldBe(exactBytes);
        output.AsSpan().EndsWith("\u001b[12;12H"u8).ShouldBeTrue();
    }

    /// <summary>Verifies a full-source RGBA placement is PNG-encoded on demand and reaches the
    /// same iTerm2 multipart transaction shape as an owned PNG source.</summary>
    [Fact]
    public void Prepare_WhenInitialRgbaPlacementExists_EncodesPngOnDemand()
    {
        using var backend = new NonRetainedGraphicsBackend(enableSixel: false, enableIterm: true);
        using var frame = ItermFrame("ab", (Red(), new Rect(1, 0, 1, 1), PlacementMode.Contain));

        var result = backend.Prepare(null, frame, full: true, Context());
        var bytes = WritePlacements(backend);

        result.Placements.ShouldBe(1);
        result.SkippedPlacements.ShouldBeEmpty();
        bytes.AsSpan().StartsWith("\u001b[1;2H\u001b]1337;MultipartFile=size="u8).ShouldBeTrue();
        bytes.AsSpan().IndexOf(";width=1;height=1;preserveAspectRatio=1;inline=1\u001b\\"u8)
            .ShouldBeGreaterThanOrEqualTo(0);
        bytes.AsSpan().EndsWith("\u001b]1337;FileEnd\u001b\\\u001b[1;1H"u8).ShouldBeTrue();
    }

    /// <summary>Verifies cover-mode RGBA and cover- or partial-source PNG placements stay on the
    /// cell fallback and each reports why iTerm cannot express the placement, even though iTerm
    /// now accepts RGBA sources whose geometry does qualify.</summary>
    [Fact]
    public void Prepare_WhenPlacementCannotBeExpressed_DeclinesWithoutOutput()
    {
        using var backend = new NonRetainedGraphicsBackend(enableSixel: false, enableIterm: true);
        using var frame = new Frame(new Size(3, 1));
        frame.AddPlacement(new Placement(
            GraphicsImage.FromRgba(new Size(1, 1), [1, 2, 3, 255]),
            new Rect(0, 0, 1, 1),
            new Rect(0, 0, 1, 1),
            PlacementMode.Cover));
        var png = Png(width: 2);
        frame.AddPlacement(new Placement(
            png,
            new Rect(0, 0, 2, 1),
            new Rect(1, 0, 1, 1),
            PlacementMode.Cover));
        frame.AddPlacement(new Placement(
            png,
            new Rect(0, 0, 1, 1),
            new Rect(2, 0, 1, 1),
            PlacementMode.Contain));

        var result = backend.Prepare(null, frame, full: true, Context());

        result.Changed.ShouldBeFalse();
        result.Placements.ShouldBe(0);
        result.SkippedPlacements.Count.ShouldBe(3);
        result.SkippedPlacements[0].Reason.ShouldBe(GraphicsPlacementSkipReason.PlacementNotEncodable);
        result.SkippedPlacements[1].Reason.ShouldBe(GraphicsPlacementSkipReason.PlacementNotEncodable);
        result.SkippedPlacements[2].Reason.ShouldBe(GraphicsPlacementSkipReason.PlacementNotEncodable);
        WritePlacements(backend).ShouldBeEmpty();
    }

    /// <summary>Verifies movement and removal request complete cell repair without remote deletes.</summary>
    [Fact]
    public void Prepare_WhenPlacementMovesThenIsRemoved_RepairsStalePixels()
    {
        using var backend = new NonRetainedGraphicsBackend(enableSixel: false, enableIterm: true);
        var image = Png();
        using var first = ItermFrame("ab", (image, new Rect(0, 0, 1, 1), PlacementMode.Stretch));
        using var moved = ItermFrame("ab", (image, new Rect(1, 0, 1, 1), PlacementMode.Stretch));
        using var removed = ItermFrame("ab");
        _ = backend.Prepare(null, first, full: true, Context());
        backend.Commit();

        var movement = backend.Prepare(first, moved, full: false, Context());
        backend.Commit();
        var removal = backend.Prepare(moved, removed, full: false, Context());

        movement.FullCellRedraw.ShouldBeTrue();
        movement.Placements.ShouldBe(1);
        removal.Changed.ShouldBeTrue();
        removal.FullCellRedraw.ShouldBeTrue();
        removal.Placements.ShouldBe(0);
        WritePlacements(backend).ShouldBeEmpty();
    }

    /// <summary>Verifies replacing an emitted PNG with an RGBA source keeps the placement live by
    /// PNG-encoding the RGBA source on demand, instead of falling back (RGBA is no longer an
    /// unsupported format for the iTerm2 backend).</summary>
    [Fact]
    public void Prepare_WhenPngBecomesRgba_KeepsPlacementByEncodingRgbaOnDemand()
    {
        using var backend = new NonRetainedGraphicsBackend(enableSixel: false, enableIterm: true);
        using var first = ItermFrame("a", (Png(), new Rect(0, 0, 1, 1), PlacementMode.Contain));
        using var rgba = ItermFrame(
            "a",
            (GraphicsImage.FromRgba(new Size(1, 1), [1, 2, 3, 255]),
             new Rect(0, 0, 1, 1),
             PlacementMode.Contain));
        _ = backend.Prepare(null, first, full: true, Context());
        backend.Commit();

        var result = backend.Prepare(first, rgba, full: false, Context());
        var bytes = WritePlacements(backend);

        result.Changed.ShouldBeTrue();
        result.Placements.ShouldBe(1);
        bytes.AsSpan().IndexOf("]1337;MultipartFile="u8).ShouldBeGreaterThanOrEqualTo(0);
    }

    /// <summary>Verifies damage under an image repaints it while unrelated cell damage stays quiet.</summary>
    [Fact]
    public void Prepare_WhenCellsChange_RepaintsOnlyIntersectingPlacement()
    {
        using var backend = new NonRetainedGraphicsBackend(enableSixel: false, enableIterm: true);
        var image = Png();
        using var first = ItermFrame("ab", (image, new Rect(0, 0, 1, 1), PlacementMode.Contain));
        using var changedUnder = ItermFrame("xb", (image, new Rect(0, 0, 1, 1), PlacementMode.Contain));
        using var changedOutside = ItermFrame("xc", (image, new Rect(0, 0, 1, 1), PlacementMode.Contain));
        _ = backend.Prepare(null, first, full: true, Context());
        backend.Commit();

        var intersecting = backend.Prepare(first, changedUnder, full: false, Context());
        backend.Commit();
        var outside = backend.Prepare(changedUnder, changedOutside, full: false, Context());

        intersecting.Placements.ShouldBe(1);
        intersecting.FullCellRedraw.ShouldBeFalse();
        outside.Changed.ShouldBeFalse();
    }

    /// <summary>Verifies repaint selection follows later overlapping PNGs transitively in paint order.</summary>
    [Fact]
    public void Prepare_WhenDamageRepaintsLowerOverlappingPng_RepaintsEveryLaterOverlap()
    {
        using var backend = new NonRetainedGraphicsBackend(enableSixel: false, enableIterm: true);
        var firstImage = Png();
        var secondImage = Png();
        var thirdImage = Png();
        using var first = ItermFrame(
            "abcd",
            (firstImage, new Rect(0, 0, 2, 1), PlacementMode.Contain),
            (secondImage, new Rect(1, 0, 2, 1), PlacementMode.Contain),
            (thirdImage, new Rect(2, 0, 2, 1), PlacementMode.Contain));
        using var changed = ItermFrame(
            "xbcd",
            (firstImage, new Rect(0, 0, 2, 1), PlacementMode.Contain),
            (secondImage, new Rect(1, 0, 2, 1), PlacementMode.Contain),
            (thirdImage, new Rect(2, 0, 2, 1), PlacementMode.Contain));
        _ = backend.Prepare(null, first, full: true, Context());
        backend.Commit();

        var result = backend.Prepare(first, changed, full: false, Context());

        result.Placements.ShouldBe(3);
        Count(WritePlacements(backend), "\u001b]1337;MultipartFile="u8).ShouldBe(3);
    }

    /// <summary>Verifies a later fallback suppresses every transitively overlapping lower PNG.</summary>
    [Fact]
    public void Prepare_WhenLaterOverlapIsUnsupported_SuppressesAffectedLowerPlacements()
    {
        using var backend = new NonRetainedGraphicsBackend(enableSixel: false, enableIterm: true);
        using var frame = ItermFrame(
            "abcd",
            (Png(), new Rect(0, 0, 2, 1), PlacementMode.Contain),
            (Png(), new Rect(1, 0, 2, 1), PlacementMode.Contain),
            (Png(), new Rect(2, 0, 2, 1), PlacementMode.Cover));

        var result = backend.Prepare(null, frame, full: true, Context());

        result.Placements.ShouldBe(0);
        result.FullCellRedraw.ShouldBeFalse();
        WritePlacements(backend).ShouldBeEmpty();
    }

    /// <summary>Verifies paint-only occlusion transitions repair non-retained pixels and later reveal the image.</summary>
    [Fact]
    public void Prepare_WhenCellPaintOccludesThenRevealsPlacement_ReconstructsEffectiveSet()
    {
        using var backend = new NonRetainedGraphicsBackend(enableSixel: false, enableIterm: true);
        var image = Png();
        using var visible = PaintedFrame(image, imageLast: true);
        using var occluded = PaintedFrame(image, imageLast: false);
        using var revealed = PaintedFrame(image, imageLast: true);
        _ = backend.Prepare(null, visible, full: true, Context());
        backend.Commit();

        var hidden = backend.Prepare(visible, occluded, full: false, Context());
        backend.Commit();
        var shown = backend.Prepare(occluded, revealed, full: false, Context());

        hidden.FullCellRedraw.ShouldBeTrue();
        hidden.Placements.ShouldBe(0);
        shown.FullCellRedraw.ShouldBeTrue();
        shown.Placements.ShouldBe(1);
    }

    /// <summary>Verifies each multipart OSC is independently routed while CUP remains pane-local.</summary>
    [Fact]
    public void Prepare_WhenTmuxRouteIsAuthorized_RoutesEveryOscIndependently()
    {
        var route = TmuxRoute();
        using var backend = new NonRetainedGraphicsBackend(enableSixel: false, enableIterm: true, route: route);
        using var frame = ItermFrame("a", (Png(), new Rect(0, 0, 1, 1), PlacementMode.Contain));

        _ = backend.Prepare(null, frame, full: true, Context());
        var bytes = WritePlacements(backend);

        Count(bytes, "\u001bPtmux;"u8).ShouldBe(3);
        bytes.AsSpan().StartsWith("\u001b[1;1H\u001bPtmux;"u8).ShouldBeTrue();
        bytes.AsSpan().EndsWith("\u001b[1;1H"u8).ShouldBeTrue();
    }

    /// <summary>Verifies route-aware chunking keeps large multipart envelopes within tmux policy.</summary>
    [Fact]
    public void Prepare_WhenPngCrossesDefaultRoutedBoundary_ReconstructsWithinEveryEnvelopeLimit()
    {
        var route = TmuxRoute();
        using var backend = new NonRetainedGraphicsBackend(enableSixel: false, enableIterm: true, route: route);
        var image = Png(dataBytes: 786_410);
        using var frame = ItermFrame("a", (image, new Rect(0, 0, 1, 1), PlacementMode.Contain));

        _ = backend.Prepare(null, frame, full: true, Context());
        var bytes = WritePlacements(backend);
        var envelopes = ExtractTmuxEnvelopes(bytes, route);
        var innerFrames = new List<byte[]>();

        foreach (var envelope in envelopes)
        {
            envelope.Length.ShouldBeLessThanOrEqualTo(route.Policy.MaxEnvelopeBytes);
            var inner = new ArrayBufferWriter<byte>();
            TmuxWriter.TryUnwrapEnvelope(envelope, inner).ShouldBeTrue();
            innerFrames.Add(inner.WrittenSpan.ToArray());
        }

        var reconstructed = innerFrames
            .Where(value => value.AsSpan().StartsWith("\u001b]1337;FilePart="u8))
            .SelectMany(DecodePart)
            .ToArray();
        var expected = new byte[image.ByteCount];
        _ = image.CopyTo(expected);

        route.GetMaximumGraphicsFrameBytes(escapeBytes: 2).ShouldBe(1_048_565);
        envelopes.Count.ShouldBe(4);
        reconstructed.ShouldBe(expected);
    }

    /// <summary>Verifies nested tmux ESC recurrence produces an exact accepted payload boundary.</summary>
    [Fact]
    public void GetMaximumGraphicsFrameBytes_WhenTmuxIsNested_AcceptsBoundAndRejectsBoundPlusOne()
    {
        var route = new MultiplexerRoute(new MultiplexingPolicy(
            [MultiplexerKind.Tmux, MultiplexerKind.Tmux],
            TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative),
            PassthroughMode.All,
            paneVisible: true,
            MultiplexingOperation.Graphics,
            maxEnvelopeBytes: 100));
        var maximum = route.GetMaximumGraphicsFrameBytes(escapeBytes: 2);
        var accepted = new byte[maximum];
        accepted[0] = 0x1b;
        accepted[^1] = 0x1b;
        var rejected = new byte[maximum + 1];
        rejected[0] = 0x1b;
        rejected[^1] = 0x1b;
        var output = new ArrayBufferWriter<byte>();

        route.TryWriteGraphics(output, accepted).ShouldBeTrue();
        output.WrittenCount.ShouldBe(100);
        output.ResetWrittenCount();
        route.TryWriteGraphics(output, rejected).ShouldBeFalse();

        maximum.ShouldBe(74);
        output.WrittenCount.ShouldBe(0);
    }

    /// <summary>Verifies an authorized route too small for metadata preserves cell fallback.</summary>
    [Fact]
    public void Prepare_WhenItermAuthorizedRouteIsTooSmall_DeclinesPlacementWithoutThrowing()
    {
        var route = new MultiplexerRoute(new MultiplexingPolicy(
            [MultiplexerKind.Tmux],
            TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative),
            PassthroughMode.All,
            paneVisible: true,
            MultiplexingOperation.Graphics,
            maxEnvelopeBytes: 64));
        using var backend = new NonRetainedGraphicsBackend(enableSixel: false, enableIterm: true, route: route);
        using var frame = ItermFrame("a", (Png(), new Rect(0, 0, 1, 1), PlacementMode.Contain));

        var result = backend.Prepare(null, frame, full: true, Context());

        result.Changed.ShouldBeFalse();
        result.Placements.ShouldBe(0);
        WritePlacements(backend).ShouldBeEmpty();
    }

    /// <summary>Verifies Screen cannot carry the multipart image contract.</summary>
    [Fact]
    public void Constructor_WhenItermRouteContainsScreen_ThrowsNotSupportedException()
    {
        var route = new MultiplexerRoute(new MultiplexingPolicy(
            [MultiplexerKind.Screen],
            TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative),
            PassthroughMode.All,
            paneVisible: true,
            MultiplexingOperation.Graphics));

        _ = Should.Throw<NotSupportedException>(() => new NonRetainedGraphicsBackend(enableSixel: false, enableIterm: true, route: route));
    }

    /// <summary>Verifies prepared writes and local lifecycle own no managed or remote cleanup state.</summary>
    [Fact]
    public void Lifetime_WhenPrepared_WritersAndStateTransitionsAreAllocationFree()
    {
        using var backend = new NonRetainedGraphicsBackend(enableSixel: false, enableIterm: true);
        using var frame = ItermFrame("a", (Png(), new Rect(0, 0, 1, 1), PlacementMode.Contain));
        _ = backend.Prepare(null, frame, full: true, Context());
        var output = new ArrayBufferWriter<byte>(512);
        backend.WritePlacements(output);
        output.ResetWrittenCount();

        var before = GC.GetAllocatedBytesForCurrentThread();
        backend.WritePlacements(output);
        backend.Commit();
        backend.Invalidate();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        allocated.ShouldBe(0);
        backend.PrepareCleanup().ShouldBe(0);
        var cleanup = new ArrayBufferWriter<byte>();
        backend.WriteCleanup(cleanup);
        cleanup.WrittenCount.ShouldBe(0);
        backend.CommitCleanup();
    }

    /// <summary>Verifies exact cell pixels anchor a sixel DCS and restore the semantic cursor.</summary>
    [Fact]
    public void Prepare_WhenInitialRgbaPlacementExists_EncodesExactPixelsAndCursor()
    {
        using var backend = new NonRetainedGraphicsBackend(enableSixel: true, enableIterm: false);
        using var frame = SixelFrame("ab", (Red(), new Rect(1, 0, 1, 1)));

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

    /// <summary>Verifies a partially transparent pixel is blended against the destination cell's
    /// explicit background color before quantization, instead of being thresholded to opaque.</summary>
    [Fact]
    public void Prepare_WhenDestinationCellHasExplicitBackground_BlendsPartialAlphaAgainstIt()
    {
        using var backend = new NonRetainedGraphicsBackend(enableSixel: true, enableIterm: false);
        using var frame = new Frame(new Size(1, 1));
        _ = frame.Canvas.Draw("a", default, new CellStyle(background: Color.Rgb(0, 0, 200)));
        var image = GraphicsImage.FromRgba(new Size(1, 1), [200, 0, 0, 128]);
        frame.Canvas.DrawImage(image, new Rect(0, 0, 1, 1), PlacementMode.Stretch);

        var result = backend.Prepare(null, frame, full: true, Context(new CellMetrics(1, 1)));
        var bytes = WritePlacements(backend);

        result.Placements.ShouldBe(1);
        bytes.ShouldBe(
            "[1;1HP0;1;0q\"1;1;1;1#0;2;40;0;40#0@\\[1;1H"u8.ToArray());
    }

    /// <summary>Verifies a destination cell with no explicit background (the default sentinel)
    /// keeps the existing threshold behavior instead of blending against a meaningless color.</summary>
    [Fact]
    public void Prepare_WhenDestinationCellBackgroundIsDefault_UsesThresholdInsteadOfBlending()
    {
        using var backend = new NonRetainedGraphicsBackend(enableSixel: true, enableIterm: false);
        using var frame = new Frame(new Size(1, 1));
        _ = frame.Canvas.Draw("a", default);
        var image = GraphicsImage.FromRgba(new Size(1, 1), [200, 0, 0, 128]);
        frame.Canvas.DrawImage(image, new Rect(0, 0, 1, 1), PlacementMode.Stretch);

        var result = backend.Prepare(null, frame, full: true, Context(new CellMetrics(1, 1)));
        var bytes = WritePlacements(backend);

        result.Placements.ShouldBe(1);
        bytes.ShouldBe(
            "[1;1HP0;1;0q\"1;1;1;1#0;2;80;0;0#0@\\[1;1H"u8.ToArray());
    }

    /// <summary>Verifies missing exact metrics stay on the ordinary cell fallback.</summary>
    [Fact]
    public void Prepare_WhenMetricsAreMissing_DeclinesWithoutRemoteState()
    {
        using var backend = new NonRetainedGraphicsBackend(enableSixel: true, enableIterm: false);
        using var rgba = SixelFrame("a", (Red(), new Rect(0, 0, 1, 1)));

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
        using var rgba = SixelFrame("a", (Red(), new Rect(0, 0, 1, 1)));
        using var png = SixelFrame("a", (Png(), new Rect(0, 0, 1, 1)));
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
        using var first = SixelFrame("ab", (image, new Rect(0, 0, 1, 1)));
        using var moved = SixelFrame("ab", (image, new Rect(1, 0, 1, 1)));
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
        using var first = SixelFrame("a", (Red(), new Rect(0, 0, 1, 1)));
        using var removed = SixelFrame("a");
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
        using var first = SixelFrame("a", (Red(), new Rect(0, 0, 1, 1)));
        using var png = SixelFrame("a", (Png(), new Rect(0, 0, 1, 1)));
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
        using var frame = SixelFrame("a", (Red(), new Rect(0, 0, 1, 1)));
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
        using var first = SixelFrame("ab", (image, new Rect(0, 0, 1, 1)));
        using var changedUnder = SixelFrame("xb", (image, new Rect(0, 0, 1, 1)));
        using var changedOutside = SixelFrame("xc", (image, new Rect(0, 0, 1, 1)));
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
        var route = new MultiplexerRoute(new MultiplexingPolicy(
            [MultiplexerKind.Tmux],
            TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative),
            PassthroughMode.All,
            paneVisible: true,
            MultiplexingOperation.Graphics));
        using var backend = new NonRetainedGraphicsBackend(enableSixel: true, enableIterm: false, route: route);
        using var frame = SixelFrame("a", (Red(), new Rect(0, 0, 1, 1)));

        _ = backend.Prepare(null, frame, full: true, Context(new CellMetrics(1, 6)));
        var bytes = WritePlacements(backend);
        var sixel = "\u001bP0;1;0q\"1;1;1;6#0;2;100;0;0#0~\u001b\\";

        bytes.ShouldBe([
            .. "\u001b[1;1H"u8,
            .. Tmux(sixel),
            .. "\u001b[1;1H"u8
        ]);
    }

    /// <summary>
    /// Verifies a sixel DCS exceeding an authorized route's envelope declines that placement to
    /// cell fallback deterministically, rather than fully encoding it and only then throwing out
    /// of <c>Prepare</c> and hanging the host — the sixel twin of the iTerm2 test with the same
    /// name.
    /// </summary>
    [Fact]
    public void Prepare_WhenSixelAuthorizedRouteIsTooSmall_DeclinesPlacementWithoutThrowing()
    {
        var route = new MultiplexerRoute(new MultiplexingPolicy(
            [MultiplexerKind.Tmux],
            TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative),
            PassthroughMode.All,
            paneVisible: true,
            MultiplexingOperation.Graphics,
            maxEnvelopeBytes: 10));
        using var backend = new NonRetainedGraphicsBackend(enableSixel: true, enableIterm: false, route: route);
        using var frame = SixelFrame("a", (Red(), new Rect(0, 0, 1, 1)));

        var result = backend.Prepare(null, frame, full: true, Context(new CellMetrics(1, 6)));

        result.Placements.ShouldBe(0);
        WritePlacements(backend).ShouldBeEmpty();
    }

    /// <summary>Verifies GNU Screen routes are rejected before any frame state exists.</summary>
    [Fact]
    public void Constructor_WhenSixelRouteContainsScreen_ThrowsNotSupportedException()
    {
        var route = new MultiplexerRoute(new MultiplexingPolicy(
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
        using var frame = SixelFrame("a", (Red(), new Rect(0, 0, 1, 1)));
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
        using var frame = SixelFrame("a", (Red(), new Rect(0, 0, 1, 1)));
        _ = backend.Prepare(null, frame, full: true, Context(new CellMetrics(1, 6)));
        var output = new ArrayBufferWriter<byte>(256);
        backend.WritePlacements(output);
        output.ResetWrittenCount();

        var before = GC.GetAllocatedBytesForCurrentThread();
        backend.WritePlacements(output);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        allocated.ShouldBe(0);
    }

    // A gradient (rather than solid) image keeps sixel run-length compression from collapsing
    // each placement to a handful of bytes, so a small handful of placements can reach a shared
    // budget that Writer.Write's own conservative worst-case check would clear individually.
    private static GraphicsImage GradientImage()
    {
        var bytes = new byte[4 * 4 * 4];

        for (var index = 0; index < 16; index++)
        {
            bytes[(index * 4) + 0] = (byte) (index * 17);
            bytes[(index * 4) + 1] = (byte) (index * 7);
            bytes[(index * 4) + 2] = (byte) (index * 3);
            bytes[(index * 4) + 3] = 255;
        }

        return GraphicsImage.FromRgba(new Size(4, 4), bytes);
    }

    private static GraphicsContext Context() => new(
        TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative with
        {
            ItermImages = new Feature(CapabilitySupport.Supported, Origin.Override)
        }),
        metrics: null);

    private static Frame ItermFrame(
        string text,
        params (GraphicsImage Image, Rect Destination, PlacementMode Mode)[] placements)
    {
        var frame = new Frame(new Size(text.Length, 1));
        _ = frame.Canvas.Draw(text, default);

        foreach (var (image, destination, mode) in placements)
        {
            frame.Canvas.DrawImage(image, destination, mode);
        }

        return frame;
    }

    private static GraphicsImage Png(int width = 1, int dataBytes = 0)
    {
        var payload = new byte[57 + dataBytes];
        ReadOnlySpan<byte> signature = [137, 80, 78, 71, 13, 10, 26, 10];
        signature.CopyTo(payload);
        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(8), 13);
        "IHDR"u8.CopyTo(payload.AsSpan(12));
        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(16), (uint) width);
        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(20), 1);
        payload[24] = 8;
        payload[25] = 6;
        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(33), (uint) dataBytes);
        "IDAT"u8.CopyTo(payload.AsSpan(37));
        var iend = 45 + dataBytes;
        "IEND"u8.CopyTo(payload.AsSpan(iend + 4));
        return GraphicsImage.FromPng(payload);
    }

    private static Frame PaintedFrame(GraphicsImage image, bool imageLast)
    {
        var frame = new Frame(new Size(1, 1));

        if (imageLast)
        {
            _ = frame.Canvas.Draw("x", default);
            frame.Canvas.DrawImage(image, new Rect(0, 0, 1, 1), PlacementMode.Stretch);
        }
        else
        {
            frame.Canvas.DrawImage(image, new Rect(0, 0, 1, 1), PlacementMode.Stretch);
            _ = frame.Canvas.Draw("x", default);
        }

        return frame;
    }

    private static MultiplexerRoute TmuxRoute() => new(new MultiplexingPolicy(
        [MultiplexerKind.Tmux],
        TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative),
        PassthroughMode.All,
        paneVisible: true,
        MultiplexingOperation.Graphics));

    private static int Count(ReadOnlySpan<byte> value, ReadOnlySpan<byte> candidate)
    {
        var count = 0;

        while (value.IndexOf(candidate) is var index and >= 0)
        {
            count++;
            value = value[(index + candidate.Length)..];
        }

        return count;
    }

    private static List<byte[]> ExtractTmuxEnvelopes(byte[] bytes, MultiplexerRoute route)
    {
        var result = new List<byte[]>();
        var remaining = bytes.AsSpan();

        while (remaining.IndexOf("\u001bPtmux;"u8) is var start and >= 0)
        {
            remaining = remaining[start..];
            var length = 2;

            while (length <= remaining.Length && !route.MayEnd(remaining[..length]))
            {
                length++;
            }

            length.ShouldBeLessThanOrEqualTo(remaining.Length);
            result.Add(remaining[..length].ToArray());
            remaining = remaining[length..];
        }

        return result;
    }

    private static byte[] DecodePart(byte[] frame)
    {
        var prefix = "\u001b]1337;FilePart="u8;
        return Convert.FromBase64String(
            Encoding.ASCII.GetString(frame.AsSpan(prefix.Length, frame.Length - prefix.Length - 2)));
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

    private static Frame SixelFrame(
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
