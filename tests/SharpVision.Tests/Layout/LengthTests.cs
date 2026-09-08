// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Layout;

/// <summary>Verifies immutable length values and their factories.</summary>
public sealed class LengthTests
{
    /// <summary>Verifies diagnostic lengths retain invariant decimal separators and unit
    /// suffixes under both dot-decimal and comma-decimal cultures.</summary>
    [Theory]
    [InlineData("en-US")]
    [InlineData("pt-PT")]
    [InlineData("fr-FR")]
    public void ToString_WhenCultureVaries_UsesInvariantUnits(string culture)
    {
        // Arrange
        var previous = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);

            // Act and assert
            Length.Auto.ToString().ShouldBe("Auto");
            Length.Cells(3).ToString().ShouldBe("3cells");
            Length.Percent(12.5).ToString().ShouldBe("12.5%");
            Length.Percent(50).ToString().ShouldBe("50%");
            Length.Star(2).ToString().ShouldBe("2*");
            Length.Star(1.25).ToString().ShouldBe("1.25*");
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    /// <summary>Verifies every supported length factory preserves its exact value.</summary>
    [Fact]
    public void Factory_WhenLengthIsValid_PreservesKindAndValue()
    {
        Length.Auto.ShouldBe(default);
        Length.Cells(0).ShouldBe(new Length(LengthKind.Cells, 0));
        Length.Cells(14).ShouldBe(new Length(LengthKind.Cells, 14));
        Length.Percent(37.5).ShouldBe(new Length(LengthKind.Percent, 37.5));
        Length.Star(2.5).ShouldBe(new Length(LengthKind.Star, 2.5));
    }

    /// <summary>Verifies invalid fixed, percentage, and proportional values are rejected.</summary>
    [Theory]
    [InlineData(LengthKind.Cells, -1)]
    [InlineData(LengthKind.Cells, 1.5)]
    [InlineData(LengthKind.Percent, -0.1)]
    [InlineData(LengthKind.Percent, 100.1)]
    [InlineData(LengthKind.Percent, double.NaN)]
    [InlineData(LengthKind.Star, 0)]
    [InlineData(LengthKind.Star, double.PositiveInfinity)]
    public void Constructor_WhenLengthIsInvalid_ThrowsArgumentOutOfRangeException(
        LengthKind kind,
        double value) =>
        _ = Should.Throw<ArgumentOutOfRangeException>(() => new Length(kind, value));

    /// <summary>Verifies automatic lengths reject a meaningless numeric payload.</summary>
    [Fact]
    public void Constructor_WhenAutomaticLengthHasValue_ThrowsArgumentException()
    {
        _ = Should.Throw<ArgumentException>(() => new Length(LengthKind.Auto, 1));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => new Length((LengthKind) int.MaxValue, 0));
    }

    /// <summary>Verifies a percentage that resolves to an exact half-cell rounds away from zero:
    /// 3 * 50 / 100 == 1.5, the midpoint between 1 and 2.</summary>
    [Fact]
    public void ResolvePercent_WhenHalfway_RoundsAwayFromZero() =>
        Length.ResolvePercent(3, 50).ShouldBe(2);

    /// <summary>Verifies a percentage whose exact result exceeds <see cref="int.MaxValue"/>
    /// saturates instead of overflowing.</summary>
    [Fact]
    public void ResolvePercent_WhenOverflowing_SaturatesAtMaxValue() =>
        Length.ResolvePercent(int.MaxValue, 200).ShouldBe(int.MaxValue);
}
