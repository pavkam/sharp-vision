// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Layout;

/// <summary>Verifies the shared layout arithmetic helpers.</summary>
public sealed class LayoutMathTests
{
    /// <summary>Verifies normal addition returns the arithmetic sum.</summary>
    [Fact]
    public void Add_WhenBothPositive_ReturnsSummed() =>
        3.Add(7).ShouldBe(10);

    /// <summary>Verifies overflow saturates to <see cref="int.MaxValue"/>.</summary>
    [Fact]
    public void Add_WhenSumOverflows_ReturnsMaxValue() =>
        (int.MaxValue - 1).Add(2).ShouldBe(int.MaxValue);

    /// <summary>Verifies zero plus zero is zero.</summary>
    [Fact]
    public void Add_WhenBothZero_ReturnsZero() =>
        0.Add(0).ShouldBe(0);

    /// <summary>Verifies underflow saturates to <see cref="int.MinValue"/> just like the positive
    /// side saturates to <see cref="int.MaxValue"/> above - Add's saturation must be symmetric
    /// like its sibling SaturatingAdd, since callers such as
    /// <c>Container.ScrollBy(int, int)</c> pass an arbitrary signed delta against a non-negative
    /// offset.</summary>
    [Fact]
    public void Add_WhenSumUnderflows_ReturnsMinValue() =>
        (int.MinValue + 1).Add(-2).ShouldBe(int.MinValue);

    /// <summary>Verifies subtraction with a present value reduces correctly.</summary>
    [Fact]
    public void Subtract_WhenValuePresent_ReturnsReducedValue() =>
        ((int?) 10).Subtract(3).ShouldBe(7);

    /// <summary>Verifies null input propagates as null.</summary>
    [Fact]
    public void Subtract_WhenValueIsNull_ReturnsNull() =>
        ((int?) null).Subtract(5).ShouldBeNull();

    /// <summary>Verifies subtraction clamps to zero when the extent exceeds the value.</summary>
    [Fact]
    public void Subtract_WhenExtentExceedsValue_ClampsToZero() =>
        ((int?) 3).Subtract(10).ShouldBe(0);

    /// <summary>Verifies positive overflow clamps to <see cref="int.MaxValue"/>.</summary>
    [Fact]
    public void SaturatingAdd_WhenPositiveOverflow_ClampsToMaxValue() =>
        int.MaxValue.SaturatingAdd(1).ShouldBe(int.MaxValue);

    /// <summary>Verifies negative overflow clamps to <see cref="int.MinValue"/>.</summary>
    [Fact]
    public void SaturatingAdd_WhenNegativeOverflow_ClampsToMinValue() =>
        int.MinValue.SaturatingAdd(-1).ShouldBe(int.MinValue);

    /// <summary>Verifies normal saturating addition returns the arithmetic sum.</summary>
    [Fact]
    public void SaturatingAdd_WhenNormal_ReturnsSummed() =>
        5.SaturatingAdd(-3).ShouldBe(2);

    /// <summary>Verifies normal negation returns the sign-flipped value.</summary>
    [Fact]
    public void Negate_WhenNormal_ReturnsNegated() =>
        5.Negate().ShouldBe(-5);

    /// <summary>Verifies <see cref="int.MinValue"/> maps to <see cref="int.MaxValue"/>.</summary>
    [Fact]
    public void Negate_WhenMinValue_ReturnsMaxValue() =>
        int.MinValue.Negate().ShouldBe(int.MaxValue);

    /// <summary>Verifies signed subtraction saturates symmetrically at both integer boundaries.</summary>
    [Theory]
    [InlineData(int.MinValue, 1, int.MinValue)]
    [InlineData(int.MaxValue, -1, int.MaxValue)]
    [InlineData(-5, -3, -2)]
    public void Subtract_WhenSignedOperandsReachBoundaries_Saturates(
        int left,
        int right,
        int expected) =>
        left.SaturatingSubtract(right).ShouldBe(expected);

    /// <summary>Verifies sequence sums apply the documented left-to-right saturation policy.</summary>
    [Fact]
    public void SaturatingSum_WhenIntermediateTotalSaturates_PreservesSequentialPolicy()
    {
        int[] values = [int.MaxValue, 1, -1];

        values.AsSpan().SaturatingSum().ShouldBe(int.MaxValue - 1);
    }

    /// <summary>Verifies fixed-seed arbitrary values match a widened sequential reference without
    /// assuming associativity after any intermediate saturation.</summary>
    [Fact]
    public void SaturatingSum_WhenValuesAreRandom_MatchesSequentialReference()
    {
        var random = new Random(803804);

        for (var iteration = 0; iteration < 500; iteration++)
        {
            var values = Enumerable.Range(0, random.Next(0, 32))
                .Select(_ => random.Next(int.MinValue, int.MaxValue))
                .ToArray();
            var expected = 0;

            foreach (var value in values)
            {
                expected = (int) Math.Clamp((long) expected + value, int.MinValue, int.MaxValue);
            }

            values.AsSpan().SaturatingSum().ShouldBe(expected);
        }
    }

    /// <summary>Verifies gap extent handles empty and singleton sequences, saturates multiplication,
    /// and honors an optional non-negative bound.</summary>
    [Theory]
    [InlineData(5, 0, null, 0)]
    [InlineData(5, 1, null, 0)]
    [InlineData(int.MaxValue, int.MaxValue, null, int.MaxValue)]
    [InlineData(10, 4, 12, 12)]
    [InlineData(10, 4, 50, 30)]
    public void GapExtent_WhenGivenSpacingCountAndLimit_ReturnsBoundedSaturatedExtent(
        int spacing,
        int count,
        int? limit,
        int expected) =>
        LayoutMath.GapExtent(spacing, count, limit).ShouldBe(expected);
}
