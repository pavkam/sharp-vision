// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.GeometryCases;

/// <summary>Verifies signed coordinate construction and diagnostic formatting.</summary>
public sealed class PointTests
{
    /// <summary>Verifies the deconstructed coordinate matches the constructed fields.</summary>
    [Fact]
    public void Deconstruct_WhenPointIsValid_ReturnsCoordinates()
    {
        var (x, y) = new Point(3, -5);

        x.ShouldBe(3);
        y.ShouldBe(-5);
    }

    /// <summary>
    /// Verifies a negative coordinate renders the ASCII hyphen-minus rather than the Unicode
    /// minus sign (U+2212) that <see cref="NumberFormatInfo.NegativeSign"/> uses under these ICU
    /// cultures.
    /// </summary>
    [Theory]
    [InlineData("sv-SE")]
    [InlineData("nb-NO")]
    [InlineData("fa-IR")]
    public void ToString_WhenCultureUsesUnicodeMinusSign_UsesAsciiHyphen(string culture)
    {
        // Arrange
        var previous = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);

            // Act and assert
            new Point(-1, 2).ToString().ShouldBe("(-1, 2)");
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
