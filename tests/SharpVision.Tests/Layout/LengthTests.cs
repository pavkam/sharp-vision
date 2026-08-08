// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Layout;

/// <summary>Verifies immutable length values and their factories.</summary>
public sealed class LengthTests
{
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
}
