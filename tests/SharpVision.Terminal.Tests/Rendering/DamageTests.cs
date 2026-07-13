namespace SharpVision.Terminal.Tests.Rendering;

using SharpVision.Terminal.Geometry;
using SharpVision.Terminal.Rendering;

using Shouldly;

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
            new DamageSpan(0, 5, 1),
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
        using var back = new Frame(new Size(1, 1));
        _ = back.Canvas.Draw(
            "x".AsSpan(),
            new Point(0, 0),
            new Style(attributes: Attributes.Bold));

        GetSpans(front, back).ShouldBe([new DamageSpan(0, 0, 1)]);
    }

    /// <summary>
    /// Verifies full invalidation and size changes cover every target row.
    /// </summary>
    [Fact]
    public void Enumerate_WhenFullOrResized_ReturnsEveryBackCell()
    {
        using var front = new Frame(new Size(1, 1));
        using var back = new Frame(new Size(2, 2));

        GetSpans(front, back).ShouldBe(
        [
            new DamageSpan(0, 0, 2),
            new DamageSpan(1, 0, 2),
        ]);
        GetSpans(back, back, full: true).Count.ShouldBe(2);
    }

    internal static List<DamageSpan> GetSpans(Frame? front, Frame back, bool full = false)
    {
        var result = new List<DamageSpan>();

        foreach (var span in Damage.Enumerate(front, back, full))
        {
            result.Add(span);
        }

        return result;
    }

    private static Frame Create(string value, int? width = null)
    {
        var frame = new Frame(new Size(width ?? value.Length, 1));
        _ = frame.Canvas.Draw(value.AsSpan(), new Point(0, 0));
        return frame;
    }
}
