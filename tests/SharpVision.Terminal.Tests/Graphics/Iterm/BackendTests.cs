// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Graphics.Iterm;

using System.Buffers.Binary;

using SharpVision.Terminal.Capabilities;
using SharpVision.Terminal.Graphics;
using SharpVision.Terminal.Multiplexing;

using MultiplexingOperation = Terminal.Multiplexing.Operation;

/// <summary>Proves non-retained iTerm2 multipart frame transactions and route policy.</summary>
[Collection(PerformanceGroup.Name)]
public sealed class BackendTests
{
    /// <summary>Verifies a full-source PNG is anchored, emitted inline, and followed by cursor restoration.</summary>
    [Fact]
    public void Prepare_WhenInitialPngPlacementExists_EncodesCellsAndCursor()
    {
        using var backend = new NonRetainedGraphicsBackend(enableSixel: false, enableIterm: true);
        using var frame = Frame("ab", (Png(), new Rect(1, 0, 1, 1), PlacementMode.Contain));

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

    /// <summary>Verifies RGBA, cover, and partial PNG sources stay on the cell fallback.</summary>
    [Fact]
    public void Prepare_WhenPlacementCannotBeExpressed_DeclinesWithoutOutput()
    {
        using var backend = new NonRetainedGraphicsBackend(enableSixel: false, enableIterm: true);
        using var frame = new Frame(new Size(3, 1));
        frame.AddPlacement(new Placement(
            GraphicsImage.FromRgba(new Size(1, 1), [1, 2, 3, 255]),
            new Rect(0, 0, 1, 1),
            new Rect(0, 0, 1, 1),
            PlacementMode.Contain));
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
        WritePlacements(backend).ShouldBeEmpty();
    }

    /// <summary>Verifies movement and removal request complete cell repair without remote deletes.</summary>
    [Fact]
    public void Prepare_WhenPlacementMovesThenIsRemoved_RepairsStalePixels()
    {
        using var backend = new NonRetainedGraphicsBackend(enableSixel: false, enableIterm: true);
        var image = Png();
        using var first = Frame("ab", (image, new Rect(0, 0, 1, 1), PlacementMode.Stretch));
        using var moved = Frame("ab", (image, new Rect(1, 0, 1, 1), PlacementMode.Stretch));
        using var removed = Frame("ab");
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

    /// <summary>Verifies replacing emitted PNG with unsupported RGBA clears the prior inline image.</summary>
    [Fact]
    public void Prepare_WhenPngBecomesRgba_RequestsFullCellRedrawWithoutImageBytes()
    {
        using var backend = new NonRetainedGraphicsBackend(enableSixel: false, enableIterm: true);
        using var first = Frame("a", (Png(), new Rect(0, 0, 1, 1), PlacementMode.Contain));
        using var rgba = Frame(
            "a",
            (GraphicsImage.FromRgba(new Size(1, 1), [1, 2, 3, 255]),
             new Rect(0, 0, 1, 1),
             PlacementMode.Contain));
        _ = backend.Prepare(null, first, full: true, Context());
        backend.Commit();

        var result = backend.Prepare(first, rgba, full: false, Context());

        result.Changed.ShouldBeTrue();
        result.FullCellRedraw.ShouldBeTrue();
        result.Placements.ShouldBe(0);
        WritePlacements(backend).ShouldBeEmpty();
    }

    /// <summary>Verifies damage under an image repaints it while unrelated cell damage stays quiet.</summary>
    [Fact]
    public void Prepare_WhenCellsChange_RepaintsOnlyIntersectingPlacement()
    {
        using var backend = new NonRetainedGraphicsBackend(enableSixel: false, enableIterm: true);
        var image = Png();
        using var first = Frame("ab", (image, new Rect(0, 0, 1, 1), PlacementMode.Contain));
        using var changedUnder = Frame("xb", (image, new Rect(0, 0, 1, 1), PlacementMode.Contain));
        using var changedOutside = Frame("xc", (image, new Rect(0, 0, 1, 1), PlacementMode.Contain));
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
        using var first = Frame(
            "abcd",
            (firstImage, new Rect(0, 0, 2, 1), PlacementMode.Contain),
            (secondImage, new Rect(1, 0, 2, 1), PlacementMode.Contain),
            (thirdImage, new Rect(2, 0, 2, 1), PlacementMode.Contain));
        using var changed = Frame(
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
        using var frame = Frame(
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
        using var frame = Frame("a", (Png(), new Rect(0, 0, 1, 1), PlacementMode.Contain));

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
        using var frame = Frame("a", (image, new Rect(0, 0, 1, 1), PlacementMode.Contain));

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
        var route = new Route(new Policy(
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
    public void Prepare_WhenAuthorizedRouteIsTooSmall_DeclinesPlacementWithoutThrowing()
    {
        var route = new Route(new Policy(
            [MultiplexerKind.Tmux],
            TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative),
            PassthroughMode.All,
            paneVisible: true,
            MultiplexingOperation.Graphics,
            maxEnvelopeBytes: 64));
        using var backend = new NonRetainedGraphicsBackend(enableSixel: false, enableIterm: true, route: route);
        using var frame = Frame("a", (Png(), new Rect(0, 0, 1, 1), PlacementMode.Contain));

        var result = backend.Prepare(null, frame, full: true, Context());

        result.Changed.ShouldBeFalse();
        result.Placements.ShouldBe(0);
        WritePlacements(backend).ShouldBeEmpty();
    }

    /// <summary>Verifies Screen cannot carry the multipart image contract.</summary>
    [Fact]
    public void Constructor_WhenRouteContainsScreen_ThrowsNotSupportedException()
    {
        var route = new Route(new Policy(
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
        using var frame = Frame("a", (Png(), new Rect(0, 0, 1, 1), PlacementMode.Contain));
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

    private static GraphicsContext Context() => new(
        TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative with
        {
            ItermImages = new Feature(CapabilitySupport.Supported, Origin.Override)
        }),
        metrics: null);

    private static Frame Frame(
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

    private static Route TmuxRoute() => new(new Policy(
        [MultiplexerKind.Tmux],
        TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative),
        PassthroughMode.All,
        paneVisible: true,
        MultiplexingOperation.Graphics));

    private static byte[] WritePlacements(NonRetainedGraphicsBackend backend)
    {
        var output = new ArrayBufferWriter<byte>();
        backend.WritePlacements(output);
        return output.WrittenSpan.ToArray();
    }

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

    private static List<byte[]> ExtractTmuxEnvelopes(byte[] bytes, Route route)
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
}
