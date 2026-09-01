// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Notifications;

/// <summary>Verifies complete immutable InfoBar presentations.</summary>
public sealed class InfoBarStyleTests
{
    /// <summary>Verifies the default aliases the informational preset and all presets are distinct.</summary>
    [Fact]
    public void Presets_WhenResolved_AreCompleteAndDefaultAliasesInfo()
    {
        InfoBarStyle.Default.ShouldBeSameAs(InfoBarStyle.Info);
        InfoBarStyle.Success.ShouldNotBe(InfoBarStyle.Info);
        InfoBarStyle.Warning.ShouldNotBe(InfoBarStyle.Info);
        InfoBarStyle.Error.ShouldNotBe(InfoBarStyle.Info);
    }

    /// <summary>Verifies negative geometry and transparent paint are rejected before construction completes.</summary>
    [Theory]
    [InlineData(-1, 0, 0)]
    [InlineData(0, -1, 0)]
    [InlineData(0, 0, 1)]
    [InlineData(0, 0, 2)]
    public void Constructor_WhenGeometryOrPaintIsInvalid_Throws(
        int contentGap,
        int adornmentGap,
        int transparentPart)
    {
        var baseline = InfoBarStyle.Info;

        _ = Should.Throw<ArgumentException>(() => new InfoBarStyle(
            baseline.Face,
            baseline.Border,
            baseline.Shadow,
            baseline.TitleFace,
            transparentPart == 1 ? Color.Transparent : baseline.AdornmentColor,
            baseline.DismissGlyph,
            transparentPart == 2 ? Color.Transparent : baseline.DismissColor,
            baseline.Padding,
            contentGap,
            adornmentGap));
    }

    /// <summary>Verifies immutable replacement accessors preserve validation.</summary>
    [Fact]
    public void With_WhenReplacementIsInvalid_Throws()
    {
        var baseline = InfoBarStyle.Info;

        _ = Should.Throw<ArgumentOutOfRangeException>(() => baseline with { ContentGap = -1 });
        _ = Should.Throw<ArgumentOutOfRangeException>(() => baseline with { AdornmentGap = -1 });
        _ = Should.Throw<ArgumentException>(() => baseline with { DismissColor = Color.Transparent });
        _ = Should.Throw<ArgumentException>(() => baseline with { AdornmentColor = Color.Transparent });
    }
}
