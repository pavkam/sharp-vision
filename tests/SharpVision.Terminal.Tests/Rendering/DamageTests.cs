// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Rendering;

using SharpVision.Terminal.Graphics;

/// <summary>
/// Verifies semantic damage spans and wide-owner expansion; and image-placement damage detection.
/// </summary>
public sealed class DamageTests
{
    /// <summary>
    /// Verifies equal frames produce no damage.
    /// </summary>
    [Fact]
    public void Enumerate_WhenFramesAreEqual_ReturnsNoSpans()
    {
        using var front = Create("abcd");
        using var back = Create("abcd");

        GetSpans(front, back).ShouldBeEmpty();
    }

    /// <summary>
    /// Verifies separated changes remain separate deterministic spans.
    /// </summary>
    [Fact]
    public void Enumerate_WhenChangesAreSparse_ReturnsMergedAdjacentRuns()
    {
        using var front = Create("abcdef");
        using var back = Create("aXYdeZ");

        GetSpans(front, back).ShouldBe(
        [
            new DamageSpan(0, 1, 2),
            new DamageSpan(0, 5, 1)
        ]);
    }

    /// <summary>
    /// Verifies a changed wide lead damages its complete ownership range.
    /// </summary>
    [Fact]
    public void Enumerate_WhenWideGraphemeChanges_ExpandsThroughContinuation()
    {
        using var front = Create("界x", width: 3);
        using var back = Create("語x", width: 3);

        GetSpans(front, back).ShouldBe([new DamageSpan(0, 0, 2)]);
    }

    /// <summary>
    /// Verifies narrow/wide replacement includes stale and new ownership cells.
    /// </summary>
    [Fact]
    public void Enumerate_WhenWidthChanges_IncludesRepairedRange()
    {
        using var front = Create("界x", width: 3);
        using var back = Create("abx", width: 3);

        GetSpans(front, back).ShouldBe([new DamageSpan(0, 0, 2)]);
    }

    /// <summary>
    /// Verifies style-only changes remain observable damage.
    /// </summary>
    [Fact]
    public void Enumerate_WhenOnlyStyleChanges_ReturnsChangedCell()
    {
        using var front = Create("x");
        using Frame back = new(new Size(1, 1));
        _ = back.Canvas.Draw(
            "x".AsSpan(),
            new Point(0, 0),
            new CellStyle(attributes: TerminalAttributes.Bold));

        GetSpans(front, back).ShouldBe([new DamageSpan(0, 0, 1)]);
    }

    /// <summary>
    /// Verifies full invalidation and size changes cover every target row.
    /// </summary>
    [Fact]
    public void Enumerate_WhenFullOrResized_ReturnsEveryBackCell()
    {
        using Frame front = new(new Size(1, 1));
        using Frame back = new(new Size(2, 2));

        GetSpans(front, back).ShouldBe(
        [
            new DamageSpan(0, 0, 2),
            new DamageSpan(1, 0, 2)
        ]);
        GetSpans(back, back, full: true).Count.ShouldBe(2);
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

    /// <summary>
    /// Verifies a present overlay with no active cell anywhere hashes identically to a null overlay,
    /// so pairing a null-overlay frame against an all-inactive-overlay frame still finds a scroll
    /// instead of every row probe mismatching purely from the overlay's presence.
    /// </summary>
    [Fact]
    public void TryFindVerticalScroll_WhenOneOverlayIsNullAndTheOtherHasNoActiveCells_DetectsScroll()
    {
        using var front = CreateRows("head", "1111", "2222", "3333", "4444");
        using var back = CreateRows("head", "2222", "3333", "4444", "5555");
        var backOverlay = new GraphicsCellOverlay(back);

        var found = Damage.TryFindVerticalScroll(front, back, frontOverlay: null, backOverlay, out var scroll);

        found.ShouldBeTrue();
        scroll.Top.ShouldBe(1);
        scroll.Bottom.ShouldBe(4);
        scroll.SourceOffset.ShouldBe(1);
    }

    internal static List<DamageSpan> GetSpans(Frame? front, Frame back, bool full = false)
    {
        List<DamageSpan> result = [.. Damage.Enumerate(front, back, full)];

        return result;
    }

    private static Frame Create(string value, int? width = null)
    {
        var frame = new Frame(new Size(width ?? value.Length, 1));
        _ = frame.Canvas.Draw(value.AsSpan(), new Point(0, 0));
        return frame;
    }

    private static Frame CreateRows(params string[] rows)
    {
        var width = rows.Max(static row => row.Length);
        var frame = new Frame(new Size(width, rows.Length));

        for (var row = 0; row < rows.Length; row++)
        {
            _ = frame.Canvas.Draw(rows[row], new Point(0, row));
        }

        return frame;
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

    private static GraphicsImage CreateImage(int width, int height, byte value)
    {
        var source = new byte[checked(width * height * 4)];
        source.AsSpan().Fill(value);
        return GraphicsImage.FromRgba(new Size(width, height), source);
    }
}
