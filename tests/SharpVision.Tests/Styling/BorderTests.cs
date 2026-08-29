// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;

/// <summary>Verifies complete border semantic-relief validation.</summary>
public sealed class BorderTests
{
    /// <summary>Verifies constructor input rejects an undefined semantic relief.</summary>
    [Fact]
    public void Constructor_WhenReliefIsUnknown_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => _ = new Border(
            BorderSide.All,
            BorderGlyphStyle.Light,
            SemanticColor.ControlBorder,
            (BorderRelief) 99,
            Color.Transparent,
            TerminalAttributes.None));

        exception.ParamName.ShouldBe("relief");
    }

    /// <summary>Verifies record replacement rejects an undefined semantic relief.</summary>
    [Fact]
    public void Relief_WhenReplacementIsUnknown_ThrowsArgumentOutOfRangeException()
    {
        var border = AppearanceTestValues.Border(BorderSide.All);

        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => _ = border with { Relief = (BorderRelief) 99 });

        exception.ParamName.ShouldBe("value");
    }
}
