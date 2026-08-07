// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Rendering;

using System.Runtime.CompilerServices;

using SharpVision.Terminal.Graphics;

/// <summary>
/// Verifies semantic frame geometry, ownership, validation, and disposal; seeded frame mutations
/// preserving wide-cell ownership invariants; and image placement lifetime and access.
/// </summary>
public sealed class FrameTests
{
    private const int _seed = 0xC311;

    /// <summary>Verifies cursor shape is immutable frame state and rejects unknown values.</summary>
    [Fact]
    public void SetCursor_WhenShapeIsSpecified_CommitsValidatedSemanticShape()
    {
        using Frame frame = new(new Size(2, 1));

        frame.SetCursor(new Point(1, 0), visible: true, CursorShape.Bar);

        frame.Cursor.ShouldBe(new Cursor(new Point(1, 0), true, CursorShape.Bar));
        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            frame.SetCursor(default, visible: true, (CursorShape) 99));
        frame.Cursor.ShouldBe(new Cursor(new Point(1, 0), true, CursorShape.Bar));
    }

    /// <summary>Verifies cloning grows its text arena before copying a large semantic frame.</summary>
    [Fact]
    public void Clone_WhenTextExceedsInitialArena_PreservesEveryGrapheme()
    {
        const int length = 300;
        using Frame frame = new(new Size(length, 1));
        _ = frame.Canvas.Draw(new string('x', length), default, CellStyle.Default);

        using var clone = frame.Clone();

        clone.Size.ShouldBe(frame.Size);
        clone.GetGraphemeByteCount(new Point(0, 0)).ShouldBe(1);
        clone.GetGraphemeByteCount(new Point(length - 1, 0)).ShouldBe(1);
        GetText(clone, new Point(length - 1, 0)).ShouldBe("x");
    }

    /// <summary>Verifies explicit rendering-value constructors reject impossible metrics.</summary>
    [Fact]
    public void Constructor_WhenRenderingValueIsInvalid_ThrowsDocumentedException()
    {
        _ = Should.Throw<ArgumentException>(() =>
            new CellInfo(CellStyle.Default, width: 1, isContinuation: true, lead: default));
        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            new DrawResult(default, graphemes: -1, cells: 0, clipped: 0, replaced: 0));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => new EncodeResult(-1, full: false));
        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            new RenderMetrics(0, 0, 0, full: false, elapsed: TimeSpan.FromTicks(-1)));
    }

    /// <summary>
    /// Verifies negative extents are rejected by geometry value constructors.
    /// </summary>
    [Fact]
    public void Constructor_WhenGeometryExtentIsNegative_ThrowsArgumentOutOfRangeException()
    {
        _ = Should.Throw<ArgumentOutOfRangeException>(() => new Size(-1, 0));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => new Rect(0, 0, -1, 0));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => new CellMetrics(0, 1));
    }

    /// <summary>
    /// Verifies zero-sized suspended screens are valid and address no cell.
    /// </summary>
    [Fact]
    public void Constructor_WhenSizeIsZero_CreatesSuspendedFrame()
    {
        using Frame frame = new(new Size(0, 0));

        frame.Size.ShouldBe(new Size(0, 0));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => frame.GetCell(new Point(0, 0)));
    }

    /// <summary>
    /// Verifies public coordinates are checked before frame state is read.
    /// </summary>
    [Fact]
    public void GetCell_WhenPointIsOutsideFrame_ThrowsArgumentOutOfRangeException()
    {
        using Frame frame = new(new Size(2, 1));

        _ = Should.Throw<ArgumentOutOfRangeException>(() => frame.GetCell(new Point(-1, 0)));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => frame.GetCell(new Point(2, 0)));
    }

    /// <summary>
    /// Verifies borrowed frame text is copied only into a sufficient destination.
    /// </summary>
    [Fact]
    public void CopyGrapheme_WhenDestinationIsTooSmall_ThrowsArgumentException()
    {
        using Frame frame = new(new Size(2, 1));
        _ = frame.Canvas.Draw("界".AsSpan(), new Point(0, 0));

        frame.GetGraphemeByteCount(new Point(0, 0)).ShouldBe(3);
        _ = Should.Throw<ArgumentException>(() => frame.CopyGrapheme(new Point(0, 0), new byte[2]));

        Span<byte> destination = stackalloc byte[3];
        frame.CopyGrapheme(new Point(1, 0), destination).ShouldBe(3);
        destination.SequenceEqual("界"u8).ShouldBeTrue();
    }

    /// <summary>
    /// Verifies clearing resets semantic cells, styles, and grapheme arena usage.
    /// </summary>
    [Fact]
    public void Clear_WhenFrameContainsText_ResetsEveryCell()
    {
        using Frame frame = new(new Size(2, 1));
        var style = new CellStyle(attributes: TerminalAttributes.Bold);
        _ = frame.Canvas.Draw("ab".AsSpan(), new Point(0, 0), style);

        frame.Clear();

        frame.GetCell(new Point(0, 0)).ShouldBe(CellInfo.Blank);
        frame.GetCell(new Point(1, 0)).ShouldBe(CellInfo.Blank);
        frame.GetGraphemeByteCount(new Point(0, 0)).ShouldBe(0);
    }

    /// <summary>
    /// Verifies disposed pooled state cannot be observed or mutated.
    /// </summary>
    [Fact]
    public void Dispose_WhenCalled_RejectsFurtherAccess()
    {
        var frame = new Frame(new Size(1, 1));

        frame.Dispose();
        frame.Dispose();

        _ = Should.Throw<ObjectDisposedException>(() => frame.Canvas);
        _ = Should.Throw<ObjectDisposedException>(() => frame.GetCell(new Point(0, 0)));
    }

    /// <summary>
    /// Verifies random draw and clear operations never orphan a continuation.
    /// </summary>
    /// <remarks>
    /// This test was previously skipped on .NET 10 x64 Linux because the process died with an
    /// AccessViolationException blamed on <c>Frame.get_Size()</c>, a plain auto-property that
    /// cannot fault. That crash is not a frame or pooling defect: it is the code-coverage
    /// instrumentation probe store, which is attributed to whichever managed method happens to be
    /// executing. The invariant itself is platform-independent, so the
    /// quarantine is removed and every platform executes it.
    /// </remarks>
    [Fact]
    public void Mutate_WhenOperationsAreRandomized_PreservesOwnership()
    {
        var random = new Random(_seed);
        (string Source, string Presentation)[] values =
        [
            (Source: "a", Presentation: "a"),
            (Source: "界", Presentation: "界"),
            (Source: "e\u0301", Presentation: "e\u0301"),
            (Source: "👩‍💻", Presentation: "👩‍💻"),
            (Source: "🇵🇹", Presentation: "🇵🇹"),
            (Source: "\u0301", Presentation: "�"),
            (Source: "\u200d", Presentation: "�"),
            (Source: "\ufe0f", Presentation: "�"),
            (Source: "🏽", Presentation: "�")
        ];
        using Frame frame = new(new Size(20, 5));

        for (var operation = 0; operation < 1_000; operation++)
        {
            var point = new Point(random.Next(frame.Size.Width), random.Next(frame.Size.Height));

            if (random.Next(4) == 0)
            {
                frame.Canvas.Clear(new Rect(point.X, point.Y, 1, 1));
            }
            else
            {
                var (source, presentation) = values[random.Next(values.Length)];
                var edge = (Edge) random.Next(3);
                _ = frame.Canvas.Draw(source.AsSpan(), point, edge: edge);

                if (presentation == "�")
                {
                    GetText(frame, point).ShouldBe(
                        presentation,
                        $"Seed {_seed}, operation {operation}, orphan presentation.");
                }
            }

            AssertOwnership(frame, operation);
        }
    }

    private static void AssertOwnership(Frame frame, int operation)
    {
        for (var y = 0; y < frame.Size.Height; y++)
        {
            for (var x = 0; x < frame.Size.Width; x++)
            {
                var point = new Point(x, y);
                var cell = frame.GetCell(point);
                var message = $"Seed {_seed}, operation {operation}, cell ({x},{y}).";

                if (cell.Continuation)
                {
                    cell.Lead.Y.ShouldBe(y, message);
                    cell.Lead.X.ShouldBe(x - 1, message);
                    var lead = frame.GetCell(cell.Lead);
                    lead.Continuation.ShouldBeFalse(message);
                    lead.Width.ShouldBe(2, message);
                }
                else if (cell.Width == 2)
                {
                    x.ShouldBeLessThan(frame.Size.Width - 1, message);
                    var continuation = frame.GetCell(new Point(x + 1, y));
                    continuation.Continuation.ShouldBeTrue(message);
                    continuation.Lead.ShouldBe(point, message);
                }
                else
                {
                    cell.Width.ShouldBe(1, message);
                }
            }
        }
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

    /// <summary>Verifies revision exhaustion cannot silently reorder active placement provenance.</summary>
    [Fact]
    public void Draw_WhenRevisionIsExhaustedWithPlacement_ThrowsBeforeCellMutation()
    {
        using var frame = new Frame(new Size(1, 1));
        frame.Canvas.DrawImage(
            CreateImage(1, 1, 1),
            new Rect(0, 0, 1, 1),
            PlacementMode.Stretch);
        frame.SeedMutationRevision(ulong.MaxValue);

        var exception = Should.Throw<InvalidOperationException>(() =>
            frame.Canvas.Draw("X", default));

        exception.Message.ShouldContain("image placements remained active");
        frame.IsPlacementEffective(0).ShouldBeTrue();
        Span<byte> grapheme = stackalloc byte[4];
        frame.CopyGrapheme(default, grapheme).ShouldBe(0);
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

    internal static string GetText(Frame frame, Point point)
    {
        var count = frame.GetGraphemeByteCount(point);
        var bytes = new byte[count];
        frame.CopyGrapheme(point, bytes).ShouldBe(count);

        return Encoding.UTF8.GetString(bytes);
    }
}
