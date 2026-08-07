// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Unicode;

/// <summary>
/// Verifies the pinned Unicode source identity and generated property tables.
/// </summary>
public sealed class UnicodeDataTests
{
    /// <summary>
    /// Verifies callers can identify the exact Unicode behavior shipped by the
    /// terminal geometry engine.
    /// </summary>
    [Fact]
    public void Version_WhenRead_ReportsPinnedUnicodeSources()
    {
        UnicodeInfo.Version.ShouldBe("17.0.0");
        UnicodeInfo.GraphemeRevision.ShouldBe(47);
        UnicodeInfo.WidthRevision.ShouldBe(44);
    }

    /// <summary>
    /// Verifies representative official boundaries survive generation.
    /// </summary>
    [Fact]
    public void Lookup_WhenKnownScalarsAreRead_ReturnsOfficialProperties()
    {
        0x000d.GetGraphemeBreak().ShouldBe(GraphemeBreak.Cr);
        0x200d.GetGraphemeBreak().ShouldBe(GraphemeBreak.Zwj);
        0x094d.GetIndicConjunct().ShouldBe(IndicConjunct.Linker);
        0x00a1.GetEastAsianWidth().ShouldBe(EastAsianWidth.Ambiguous);
        0x1100.GetEastAsianWidth().ShouldBe(EastAsianWidth.Wide);
        0x1f600.IsEmojiPresentation().ShouldBeTrue();
        0x1f469.IsExtendedPictographic().ShouldBeTrue();
        0x00e9.GetCanonicalBase().ShouldBe(0x0065);
        0x0061.IsAssigned().ShouldBeTrue();
        0x0378.IsAssigned().ShouldBeFalse();
    }
}
