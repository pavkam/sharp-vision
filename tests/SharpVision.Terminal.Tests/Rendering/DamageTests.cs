// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Rendering;



/// <summary>
/// Verifies semantic damage spans and wide-owner expansion.
/// </summary>
public sealed class DamageTests
{
    /// <summary>
    /// Verifies equal frames produce no damage.
    /// </summary>
    [Fact]
    public void Enumerate_WhenFramesAreEqual_ReturnsNoSpans()
    {
        using Frame front = Create("abcd");
        using Frame back = Create("abcd");

        GetSpans(front, back).ShouldBeEmpty();
    }

    /// <summary>
    /// Verifies separated changes remain separate deterministic spans.
    /// </summary>
    [Fact]
    public void Enumerate_WhenChangesAreSparse_ReturnsMergedAdjacentRuns()
    {
        using Frame front = Create("abcdef");
        using Frame back = Create("aXYdeZ");

        GetSpans(front, back).ShouldBe(
        [
            new DamageSpan(0, 1, 2),
            new DamageSpan(0, 5, 1),
        ]);
    }

    /// <summary>
    /// Verifies a changed wide lead damages its complete ownership range.
    /// </summary>
    [Fact]
    public void Enumerate_WhenWideGraphemeChanges_ExpandsThroughContinuation()
    {
        using Frame front = Create("界x", width: 3);
        using Frame back = Create("語x", width: 3);

        GetSpans(front, back).ShouldBe([new DamageSpan(0, 0, 2)]);
    }

    /// <summary>
    /// Verifies narrow/wide replacement includes stale and new ownership cells.
    /// </summary>
    [Fact]
    public void Enumerate_WhenWidthChanges_IncludesRepairedRange()
    {
        using Frame front = Create("界x", width: 3);
        using Frame back = Create("abx", width: 3);

        GetSpans(front, back).ShouldBe([new DamageSpan(0, 0, 2)]);
    }

    /// <summary>
    /// Verifies style-only changes remain observable damage.
    /// </summary>
    [Fact]
    public void Enumerate_WhenOnlyStyleChanges_ReturnsChangedCell()
    {
        using Frame front = Create("x");
        using Frame back = new(new Size(1, 1));
        _ = back.Canvas.Draw(
            "x".AsSpan(),
            new Point(0, 0),
            new CellStyle(attributes: Attributes.Bold));

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
            new DamageSpan(1, 0, 2),
        ]);
        GetSpans(back, back, full: true).Count.ShouldBe(2);
    }

    internal static List<DamageSpan> GetSpans(Frame? front, Frame back, bool full = false)
    {
        List<DamageSpan> result = [.. Damage.Enumerate(front, back, full)];

        return result;
    }

    private static Frame Create(string value, int? width = null)
    {
        Frame frame = new(new Size(width ?? value.Length, 1));
        _ = frame.Canvas.Draw(value.AsSpan(), new Point(0, 0));
        return frame;
    }
}
