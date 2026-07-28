// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Graphics;

using System.Reflection;
using System.Runtime.CompilerServices;

using SharpVision.Terminal.Graphics;

/// <summary>Proves owned semantic placement geometry and frame lifetime.</summary>
public sealed class PlacementTests
{
    /// <summary>Verifies the public constructor rejects a null image.</summary>
    [Fact]
    public void Constructor_WhenImageIsNull_ThrowsArgumentNullException()
    {
        _ = Should.Throw<ArgumentNullException>(() => new Placement(
            null!,
            new Rect(0, 0, 1, 1),
            new Rect(0, 0, 1, 1),
            PlacementMode.Contain));
    }

    /// <summary>Verifies placement retains the exact immutable image identity.</summary>
    [Fact]
    public void Constructor_WhenImageIsProvided_OwnsStableIdentity()
    {
        var image = CreateImage(2, 2, 1);

        var placement = new Placement(
            image,
            new Rect(0, 0, 2, 2),
            new Rect(1, 2, 3, 4),
            PlacementMode.Contain);

        placement.Image.ShouldBeSameAs(image);
        placement.ImageIdentity.ShouldBe(image.Identity);
    }

    /// <summary>Verifies empty and out-of-image source rectangles are rejected.</summary>
    [Theory]
    [InlineData(0, 0, 0, 1)]
    [InlineData(0, 0, 1, 0)]
    [InlineData(-1, 0, 1, 1)]
    [InlineData(0, -1, 1, 1)]
    [InlineData(1, 1, 2, 2)]
    public void Constructor_WhenSourceIsInvalid_ThrowsArgumentOutOfRangeException(
        int x,
        int y,
        int width,
        int height)
    {
        var image = CreateImage(2, 2, 1);

        _ = Should.Throw<ArgumentOutOfRangeException>(() => new Placement(
            image,
            new Rect(x, y, width, height),
            new Rect(0, 0, 1, 1),
            PlacementMode.Stretch));
    }

    /// <summary>Verifies empty, negative-origin, and unknown destination state is rejected.</summary>
    [Theory]
    [InlineData(0, 0, 0, 1)]
    [InlineData(0, 0, 1, 0)]
    [InlineData(-1, 0, 1, 1)]
    [InlineData(0, -1, 1, 1)]
    public void Constructor_WhenDestinationIsInvalid_ThrowsArgumentOutOfRangeException(
        int x,
        int y,
        int width,
        int height)
    {
        _ = Should.Throw<ArgumentOutOfRangeException>(() => new Placement(
            CreateImage(1, 1, 1),
            new Rect(0, 0, 1, 1),
            new Rect(x, y, width, height),
            PlacementMode.Stretch));
    }

    /// <summary>Verifies undefined fitting mode values are rejected.</summary>
    [Fact]
    public void Constructor_WhenModeIsUndefined_ThrowsArgumentOutOfRangeException()
    {
        _ = Should.Throw<ArgumentOutOfRangeException>(() => new Placement(
            CreateImage(1, 1, 1),
            new Rect(0, 0, 1, 1),
            new Rect(0, 0, 1, 1),
            (PlacementMode) int.MaxValue));
    }

    /// <summary>Verifies the canvas records clipped destinations in stable paint order.</summary>
    [Theory]
    [InlineData(PlacementMode.Contain)]
    [InlineData(PlacementMode.Cover)]
    [InlineData(PlacementMode.Stretch)]
    public void DrawImage_WhenDestinationIntersectsClip_RecordsClippedPlacementInStableOrder(
        PlacementMode mode)
    {
        using var frame = new Frame(new Size(8, 5));
        var first = CreateImage(2, 2, 1);
        var second = CreateImage(3, 1, 2);
        var canvas = frame.Canvas.Clip(new Rect(2, 1, 4, 3));

        canvas.DrawImage(first, new Rect(0, 0, 4, 3), mode);
        canvas.DrawImage(second, new Rect(4, 2, 4, 3), mode);

        frame.PlacementCount.ShouldBe(2);
        var firstPlacement = frame.GetPlacement(0);
        firstPlacement.Image.ShouldBeSameAs(first);
        firstPlacement.Source.ShouldBe(new Rect(0, 0, 2, 2));
        firstPlacement.Destination.ShouldBe(new Rect(2, 1, 2, 2));
        firstPlacement.Mode.ShouldBe(mode);
        frame.GetPlacement(1).Image.ShouldBeSameAs(second);
        frame.GetPlacement(1).Destination.ShouldBe(new Rect(4, 2, 2, 2));
    }

    /// <summary>Verifies invisible destinations are semantic no-ops.</summary>
    [Fact]
    public void DrawImage_WhenDestinationIsOutsideClip_RecordsNothing()
    {
        using var frame = new Frame(new Size(4, 2));

        frame.Canvas.Clip(new Rect(0, 0, 2, 2)).DrawImage(
            CreateImage(1, 1, 1),
            new Rect(3, 0, 1, 1),
            PlacementMode.Contain);

        frame.PlacementCount.ShouldBe(0);
    }

    /// <summary>Verifies an empty destination is a fully clipped semantic no-op.</summary>
    [Fact]
    public void DrawImage_WhenDestinationIsEmpty_RecordsNothing()
    {
        using var frame = new Frame(new Size(2, 1));

        frame.Canvas.DrawImage(
            CreateImage(1, 1, 1),
            new Rect(0, 0, 0, 1),
            PlacementMode.Stretch);

        frame.PlacementCount.ShouldBe(0);
    }

    /// <summary>Verifies Canvas validates a null image before frame mutation.</summary>
    [Fact]
    public void DrawImage_WhenImageIsNull_ThrowsArgumentNullException()
    {
        using var frame = new Frame(new Size(1, 1));

        _ = Should.Throw<ArgumentNullException>(() => frame.Canvas.DrawImage(
            null!,
            new Rect(0, 0, 1, 1),
            PlacementMode.Contain));

        frame.PlacementCount.ShouldBe(0);
    }

    /// <summary>Verifies Canvas validates unknown fitting modes before frame mutation.</summary>
    [Fact]
    public void DrawImage_WhenModeIsUndefined_ThrowsArgumentOutOfRangeException()
    {
        using var frame = new Frame(new Size(1, 1));

        _ = Should.Throw<ArgumentOutOfRangeException>(() => frame.Canvas.DrawImage(
            CreateImage(1, 1, 1),
            new Rect(0, 0, 1, 1),
            (PlacementMode) int.MaxValue));

        frame.PlacementCount.ShouldBe(0);
    }

    /// <summary>Verifies disposed frame and canvas placement access is rejected.</summary>
    [Fact]
    public void PlacementAccess_WhenFrameIsDisposed_ThrowsObjectDisposedException()
    {
        var frame = new Frame(new Size(1, 1));
        var canvas = frame.Canvas;
        frame.Canvas.DrawImage(
            CreateImage(1, 1, 1),
            new Rect(0, 0, 1, 1),
            PlacementMode.Contain);
        frame.Dispose();

        _ = Should.Throw<ObjectDisposedException>(() => frame.PlacementCount);
        _ = Should.Throw<ObjectDisposedException>(() => frame.GetPlacement(0));
        _ = Should.Throw<ObjectDisposedException>(() => canvas.Bounds);
        _ = Should.Throw<ObjectDisposedException>(() => canvas.DrawImage(
            CreateImage(1, 1, 1),
            new Rect(0, 0, 1, 1),
            PlacementMode.Contain));
    }

    /// <summary>Verifies clones own their ordered placement snapshot independently.</summary>
    [Fact]
    public void Clone_WhenSourceIsDisposed_RetainsOwnedPlacementSnapshot()
    {
        var source = new Frame(new Size(3, 2));
        var image = CreateImage(1, 1, 7);
        source.Canvas.DrawImage(image, new Rect(0, 0, 2, 2), PlacementMode.Cover);
        var clone = source.Clone();

        source.Clear();
        source.Dispose();

        clone.PlacementCount.ShouldBe(1);
        clone.GetPlacement(0).Image.ShouldBeSameAs(image);
        clone.GetPlacement(0).Destination.ShouldBe(new Rect(0, 0, 2, 2));
        clone.Dispose();
    }

    /// <summary>Verifies prepared copy storage owns placement order independently of its source frame.</summary>
    [Fact]
    public void CopyFrom_WhenSourceIsDisposed_RetainsIndependentPlacementSnapshot()
    {
        var source = new Frame(new Size(3, 2));
        using var destination = new Frame(new Size(3, 2));
        var first = CreateImage(1, 1, 3);
        var second = CreateImage(1, 1, 4);
        source.Canvas.DrawImage(first, new Rect(0, 0, 1, 1), PlacementMode.Contain);
        source.Canvas.DrawImage(second, new Rect(1, 0, 1, 1), PlacementMode.Cover);

        destination.PrepareCopyFrom(source);
        destination.CopyFrom(source);
        source.Clear();
        source.Dispose();

        destination.PlacementCount.ShouldBe(2);
        destination.GetPlacement(0).Image.ShouldBeSameAs(first);
        destination.GetPlacement(1).Image.ShouldBeSameAs(second);
    }

    /// <summary>Verifies clear releases image references while retaining the active frame.</summary>
    [Fact]
    public void Clear_WhenPlacementsExist_ReleasesRetainedImages()
    {
        var (frame, reference) = CreateClearedFrameReference();

        ForceCollection();

        reference.TryGetTarget(out _).ShouldBeFalse();
        frame.PlacementCount.ShouldBe(0);
        GC.KeepAlive(frame);
        frame.Dispose();
    }

    /// <summary>Verifies disposal clears pooled placement references.</summary>
    [Fact]
    public void Dispose_WhenPlacementsExist_ReleasesRetainedImages()
    {
        var reference = CreateDisposedFrameReference();

        ForceCollection();

        reference.TryGetTarget(out _).ShouldBeFalse();
    }

    /// <summary>Verifies finite placement capacity fails without changing the frame.</summary>
    [Fact]
    public void DrawImage_WhenPlacementLimitIsExceeded_LeavesExistingSnapshotUnchanged()
    {
        using var frame = new Frame(new Size(2, 1), 16, Ambiguous.Narrow, 1);
        var first = CreateImage(1, 1, 1);
        frame.Canvas.DrawImage(first, new Rect(0, 0, 1, 1), PlacementMode.Stretch);

        _ = Should.Throw<InvalidOperationException>(() => frame.Canvas.DrawImage(
            CreateImage(1, 1, 2),
            new Rect(1, 0, 1, 1),
            PlacementMode.Stretch));

        frame.PlacementCount.ShouldBe(1);
        frame.GetPlacement(0).Image.ShouldBeSameAs(first);
    }

    /// <summary>Verifies empty sentinel slots can never become active frame placements.</summary>
    [Fact]
    public void AddPlacement_WhenPlacementIsEmpty_ThrowsArgumentException()
    {
        using var frame = new Frame(new Size(1, 1));

        _ = Should.Throw<ArgumentException>(() => frame.AddPlacement(Placement.Empty));

        frame.PlacementCount.ShouldBe(0);
    }

    /// <summary>Verifies ordered source, destination, mode, and image changes are graphics damage.</summary>
    [Fact]
    public void PlacementsChanged_WhenSemanticPlacementChanges_ReturnsTrue()
    {
        using var front = new Frame(new Size(4, 2));
        using var back = new Frame(new Size(4, 2));
        var image = CreateImage(2, 2, 1);
        front.Canvas.DrawImage(image, new Rect(0, 0, 2, 2), PlacementMode.Contain);
        back.Canvas.DrawImage(image, new Rect(1, 0, 2, 2), PlacementMode.Contain);

        Damage.PlacementsChanged(front, back).ShouldBeTrue();
        Damage.PlacementsChanged(back, back).ShouldBeFalse();
        Damage.PlacementsChanged(back, back, full: true).ShouldBeTrue();
    }

    /// <summary>Verifies source cropping and stable paint order participate in damage.</summary>
    [Fact]
    public void PlacementsChanged_WhenSourceOrOrderChanges_ReturnsTrue()
    {
        using var front = new Frame(new Size(2, 1));
        using var back = new Frame(new Size(2, 1));
        var first = CreateImage(2, 1, 1);
        var second = CreateImage(2, 1, 2);
        front.AddPlacement(new Placement(
            first,
            new Rect(0, 0, 1, 1),
            new Rect(0, 0, 1, 1),
            PlacementMode.Stretch));
        front.AddPlacement(new Placement(
            second,
            new Rect(0, 0, 2, 1),
            new Rect(1, 0, 1, 1),
            PlacementMode.Stretch));
        back.AddPlacement(new Placement(
            second,
            new Rect(0, 0, 2, 1),
            new Rect(1, 0, 1, 1),
            PlacementMode.Stretch));
        back.AddPlacement(new Placement(
            first,
            new Rect(1, 0, 1, 1),
            new Rect(0, 0, 1, 1),
            PlacementMode.Stretch));

        Damage.PlacementsChanged(front, back).ShouldBeTrue();
    }

    /// <summary>Verifies destination movement independently damages graphics.</summary>
    [Fact]
    public void PlacementsChanged_WhenDestinationMoves_ReturnsTrue()
    {
        var image = CreateImage(1, 1, 1);
        using var front = CreatePlacementFrame(image, new Rect(0, 0, 1, 1), PlacementMode.Contain);
        using var back = CreatePlacementFrame(image, new Rect(1, 0, 1, 1), PlacementMode.Contain);

        Damage.PlacementsChanged(front, back).ShouldBeTrue();
    }

    /// <summary>Verifies destination resize independently damages graphics.</summary>
    [Fact]
    public void PlacementsChanged_WhenDestinationResizes_ReturnsTrue()
    {
        var image = CreateImage(1, 1, 1);
        using var front = CreatePlacementFrame(image, new Rect(0, 0, 1, 1), PlacementMode.Contain);
        using var back = CreatePlacementFrame(image, new Rect(0, 0, 2, 1), PlacementMode.Contain);

        Damage.PlacementsChanged(front, back).ShouldBeTrue();
    }

    /// <summary>Verifies source rectangle changes independently damage graphics.</summary>
    [Fact]
    public void PlacementsChanged_WhenSourceChanges_ReturnsTrue()
    {
        var image = CreateImage(2, 1, 1);
        using var front = new Frame(new Size(2, 1));
        using var back = new Frame(new Size(2, 1));
        front.AddPlacement(new Placement(
            image,
            new Rect(0, 0, 1, 1),
            new Rect(0, 0, 1, 1),
            PlacementMode.Contain));
        back.AddPlacement(new Placement(
            image,
            new Rect(1, 0, 1, 1),
            new Rect(0, 0, 1, 1),
            PlacementMode.Contain));

        Damage.PlacementsChanged(front, back).ShouldBeTrue();
    }

    /// <summary>Verifies fitting mode changes independently damage graphics.</summary>
    [Fact]
    public void PlacementsChanged_WhenModeChanges_ReturnsTrue()
    {
        var image = CreateImage(1, 1, 1);
        using var front = CreatePlacementFrame(image, new Rect(0, 0, 1, 1), PlacementMode.Contain);
        using var back = CreatePlacementFrame(image, new Rect(0, 0, 1, 1), PlacementMode.Cover);

        Damage.PlacementsChanged(front, back).ShouldBeTrue();
    }

    /// <summary>Verifies immutable image identity independently damages graphics.</summary>
    [Fact]
    public void PlacementsChanged_WhenImageIdentityChanges_ReturnsTrue()
    {
        using var front = CreatePlacementFrame(
            CreateImage(1, 1, 1),
            new Rect(0, 0, 1, 1),
            PlacementMode.Contain);
        using var back = CreatePlacementFrame(
            CreateImage(1, 1, 1),
            new Rect(0, 0, 1, 1),
            PlacementMode.Contain);

        Damage.PlacementsChanged(front, back).ShouldBeTrue();
    }

    /// <summary>Verifies stable equal order is undamaged while reversed z-order is damaged.</summary>
    [Fact]
    public void PlacementsChanged_WhenZOrderChanges_ReturnsTrue()
    {
        var first = CreateImage(1, 1, 1);
        var second = CreateImage(1, 1, 2);
        using var front = new Frame(new Size(2, 1));
        using var equal = new Frame(new Size(2, 1));
        using var reversed = new Frame(new Size(2, 1));

        AddPair(front, first, second);
        AddPair(equal, first, second);
        AddPair(reversed, second, first);

        Damage.PlacementsChanged(front, equal).ShouldBeFalse();
        Damage.PlacementsChanged(front, reversed).ShouldBeTrue();
    }

    /// <summary>Verifies later cell paint changes effective placement state without changing public semantics.</summary>
    [Fact]
    public void PlacementsChanged_WhenLaterCellsOccludeImage_TracksPrivatePaintProvenance()
    {
        var image = CreateImage(1, 1, 1);
        using var visible = new Frame(new Size(1, 1));
        _ = visible.Canvas.Draw("x", default);
        visible.Canvas.DrawImage(image, new Rect(0, 0, 1, 1), PlacementMode.Stretch);
        using var occluded = new Frame(new Size(1, 1));
        occluded.Canvas.DrawImage(image, new Rect(0, 0, 1, 1), PlacementMode.Stretch);
        _ = occluded.Canvas.Draw("x", default);

        visible.GetPlacement(0).ShouldBe(occluded.GetPlacement(0));
        visible.GetPlacement(0).GetHashCode().ShouldBe(occluded.GetPlacement(0).GetHashCode());
        Damage.PlacementsChanged(visible, occluded).ShouldBeTrue();

        using var clone = occluded.Clone();
        using var copy = new Frame(new Size(1, 1));
        copy.PrepareCopyFrom(occluded);
        copy.CopyFrom(occluded);

        Damage.PlacementsChanged(occluded, clone).ShouldBeFalse();
        Damage.PlacementsChanged(occluded, copy).ShouldBeFalse();
    }

    /// <summary>Verifies revision exhaustion cannot silently reorder active placement provenance.</summary>
    [Fact]
    public void Draw_WhenRevisionIsExhaustedWithPlacement_ThrowsBeforeCellMutation()
    {
        using var frame = new Frame(new Size(1, 1));
        frame.Canvas.DrawImage(
            CreateImage(1, 1, 1),
            new Rect(0, 0, 1, 1),
            PlacementMode.Stretch);
        var revision = typeof(Frame).GetField(
            "_mutationRevision",
            BindingFlags.Instance | BindingFlags.NonPublic).ShouldNotBeNull();
        revision.SetValue(frame, ulong.MaxValue);

        var exception = Should.Throw<InvalidOperationException>(() =>
            frame.Canvas.Draw("X", default));

        exception.Message.ShouldContain("image placements remained active");
        frame.IsPlacementEffective(0).ShouldBeTrue();
        Span<byte> grapheme = stackalloc byte[4];
        frame.CopyGrapheme(default, grapheme).ShouldBe(0);
    }

    private static void AddPair(Frame frame, GraphicsImage first, GraphicsImage second)
    {
        frame.Canvas.DrawImage(first, new Rect(0, 0, 1, 1), PlacementMode.Contain);
        frame.Canvas.DrawImage(second, new Rect(1, 0, 1, 1), PlacementMode.Contain);
    }

    private static Frame CreatePlacementFrame(
        GraphicsImage image,
        Rect destination,
        PlacementMode mode)
    {
        var frame = new Frame(new Size(2, 1));
        frame.Canvas.DrawImage(image, destination, mode);
        return frame;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (Frame Frame, WeakReference<GraphicsImage> Reference) CreateClearedFrameReference()
    {
        var frame = new Frame(new Size(1, 1));
        var image = CreateImage(1, 1, 9);
        var reference = new WeakReference<GraphicsImage>(image);
        frame.Canvas.DrawImage(image, new Rect(0, 0, 1, 1), PlacementMode.Contain);
        frame.Clear();
        return (frame, reference);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<GraphicsImage> CreateDisposedFrameReference()
    {
        var frame = new Frame(new Size(1, 1));
        var image = CreateImage(1, 1, 9);
        var reference = new WeakReference<GraphicsImage>(image);
        frame.Canvas.DrawImage(image, new Rect(0, 0, 1, 1), PlacementMode.Contain);
        frame.Dispose();
        return reference;
    }

    private static void ForceCollection()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    private static GraphicsImage CreateImage(int width, int height, byte value)
    {
        var source = new byte[checked(width * height * 4)];
        source.AsSpan().Fill(value);
        return GraphicsImage.FromRgba(new Size(width, height), source);
    }
}
