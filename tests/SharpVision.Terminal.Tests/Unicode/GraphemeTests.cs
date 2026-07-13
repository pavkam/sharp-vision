namespace SharpVision.Terminal.Tests.Unicode;

using SharpVision.Terminal.Unicode;

using Shouldly;

/// <summary>
/// Verifies curated extended-grapheme behavior and allocation bounds.
/// </summary>
public sealed class GraphemeTests
{
    /// <summary>
    /// Verifies user-perceived characters remain whole.
    /// </summary>
    /// <param name="value">The UTF-16 text to segment.</param>
    /// <param name="expected">The expected segment count.</param>
    [Theory]
    [InlineData("e\u0301", 1)]
    [InlineData("\r\n", 1)]
    [InlineData("각", 1)]
    [InlineData("🇵🇹🇬🇧", 2)]
    [InlineData("👩🏽‍💻", 1)]
    [InlineData("👩‍👩‍👧‍👦", 1)]
    [InlineData("1️⃣", 1)]
    public void Enumerate_WhenTextContainsExtendedClusters_ReturnsExpectedCount(
        string value,
        int expected) => Count(Graphemes.Enumerate(value.AsSpan())).ShouldBe(expected);

    /// <summary>
    /// Verifies consecutive regional indicators pair from the start.
    /// </summary>
    [Fact]
    public void Enumerate_WhenRegionalIndicatorCountIsOdd_PairsByParity()
    {
        var value = "🇵🇹🇬";
        var segments = GetSegments(value);

        segments.ShouldBe([(0, 4, false), (4, 2, false)]);
    }

    /// <summary>
    /// Verifies malformed UTF-16 consumes one replacement segment at a time.
    /// </summary>
    [Fact]
    public void Enumerate_WhenUtf16IsInvalid_ReportsReplacementSegment()
    {
        var value = "\ud800a\udc00";
        var segments = GetSegments(value);

        segments.ShouldBe(
        [
            (0, 1, true),
            (1, 1, false),
            (2, 1, true),
        ]);
    }

    /// <summary>
    /// Verifies warmed segmentation does not allocate per scalar or cluster.
    /// </summary>
    [Fact]
    public void Enumerate_WhenWarm_AllocatesNoManagedBytes()
    {
        var value = "ASCII e\u0301 界 👩🏽‍💻 🇵🇹";

        for (var index = 0; index < 100; index++)
        {
            _ = Count(Graphemes.Enumerate(value.AsSpan()));
        }

        var before = GC.GetAllocatedBytesForCurrentThread();

        for (var index = 0; index < 10_000; index++)
        {
            _ = Count(Graphemes.Enumerate(value.AsSpan()));
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        allocated.ShouldBe(0);
    }

    private static int Count(GraphemeEnumerable value)
    {
        var count = 0;

        foreach (var unused in value)
        {
            _ = unused;
            count++;
        }

        return count;
    }

    private static (int Offset, int Length, bool Invalid)[] GetSegments(string value)
    {
        var result = new List<(int, int, bool)>();

        foreach (var segment in Graphemes.Enumerate(value.AsSpan()))
        {
            result.Add((segment.Offset, segment.Length, segment.HasInvalidData));
        }

        return [.. result];
    }
}
