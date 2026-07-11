using SharpVision.Terminal.Unicode;

using Shouldly;

namespace SharpVision.Terminal.Tests.Unicode;

/// <summary>
/// Verifies the pinned Unicode source identity and generated property tables.
/// </summary>
public sealed class DataTests
{
    /// <summary>
    /// Verifies callers can identify the exact Unicode behavior shipped by the
    /// terminal geometry engine.
    /// </summary>
    [Fact]
    public void Version_WhenRead_ReportsPinnedUnicodeSources()
    {
        Info.Version.ShouldBe("17.0.0");
        Info.GraphemeRevision.ShouldBe(47);
        Info.WidthRevision.ShouldBe(44);
    }

    /// <summary>
    /// Verifies generated ranges are safe for binary search.
    /// </summary>
    [Fact]
    public void Ranges_WhenGenerated_AreSortedAndNonOverlapping()
    {
        AssertOrdered(Data.GraphemeBreakRanges);
        AssertOrdered(Data.IndicConjunctRanges);
        AssertOrdered(Data.EastAsianWidthRanges);
        AssertOrdered(Data.EmojiPresentationRanges);
        AssertOrdered(Data.ExtendedPictographicRanges);
        AssertOrdered(Data.CanonicalBaseRanges);
        AssertOrdered(Data.AssignedRanges);
    }

    /// <summary>
    /// Verifies representative official boundaries survive generation.
    /// </summary>
    [Fact]
    public void Lookup_WhenKnownScalarsAreRead_ReturnsOfficialProperties()
    {
        Data.GetGraphemeBreak(0x000d).ShouldBe(GraphemeBreak.Cr);
        Data.GetGraphemeBreak(0x200d).ShouldBe(GraphemeBreak.Zwj);
        Data.GetIndicConjunct(0x094d).ShouldBe(IndicConjunct.Linker);
        Data.GetEastAsianWidth(0x00a1).ShouldBe(EastAsianWidth.Ambiguous);
        Data.GetEastAsianWidth(0x1100).ShouldBe(EastAsianWidth.Wide);
        Data.IsEmojiPresentation(0x1f600).ShouldBeTrue();
        Data.IsExtendedPictographic(0x1f469).ShouldBeTrue();
        Data.GetCanonicalBase(0x00e9).ShouldBe(0x0065);
        Data.IsAssigned(0x0061).ShouldBeTrue();
        Data.IsAssigned(0x0378).ShouldBeFalse();
    }

    private static void AssertOrdered(ReadOnlySpan<PropertyRange> ranges)
    {
        ranges.IsEmpty.ShouldBeFalse();

        for (var index = 0; index < ranges.Length; index++)
        {
            var current = ranges[index];

            current.Start.ShouldBeLessThanOrEqualTo(current.End);

            if (index > 0)
            {
                ranges[index - 1].End.ShouldBeLessThan(current.Start);
            }
        }
    }
}
