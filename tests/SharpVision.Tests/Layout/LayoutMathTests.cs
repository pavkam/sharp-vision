// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Layout;

/// <summary>Verifies the shared layout arithmetic helpers.</summary>
public sealed class LayoutMathTests
{
    /// <summary>Verifies normal addition returns the arithmetic sum.</summary>
    [Fact]
    public void Add_WhenBothPositive_ReturnsSummed() =>
        LayoutMath.Add(3, 7).ShouldBe(10);

    /// <summary>Verifies overflow saturates to <see cref="int.MaxValue"/>.</summary>
    [Fact]
    public void Add_WhenSumOverflows_ReturnsMaxValue() =>
        LayoutMath.Add(int.MaxValue - 1, 2).ShouldBe(int.MaxValue);

    /// <summary>Verifies zero plus zero is zero.</summary>
    [Fact]
    public void Add_WhenBothZero_ReturnsZero() =>
        LayoutMath.Add(0, 0).ShouldBe(0);

    /// <summary>Verifies subtraction with a present value reduces correctly.</summary>
    [Fact]
    public void Subtract_WhenValuePresent_ReturnsReducedValue() =>
        LayoutMath.Subtract(10, 3).ShouldBe(7);

    /// <summary>Verifies null input propagates as null.</summary>
    [Fact]
    public void Subtract_WhenValueIsNull_ReturnsNull() =>
        LayoutMath.Subtract(null, 5).ShouldBeNull();

    /// <summary>Verifies subtraction clamps to zero when the extent exceeds the value.</summary>
    [Fact]
    public void Subtract_WhenExtentExceedsValue_ClampsToZero() =>
        LayoutMath.Subtract(3, 10).ShouldBe(0);

    /// <summary>Verifies positive overflow clamps to <see cref="int.MaxValue"/>.</summary>
    [Fact]
    public void SaturatingAdd_WhenPositiveOverflow_ClampsToMaxValue() =>
        LayoutMath.SaturatingAdd(int.MaxValue, 1).ShouldBe(int.MaxValue);

    /// <summary>Verifies negative overflow clamps to <see cref="int.MinValue"/>.</summary>
    [Fact]
    public void SaturatingAdd_WhenNegativeOverflow_ClampsToMinValue() =>
        LayoutMath.SaturatingAdd(int.MinValue, -1).ShouldBe(int.MinValue);

    /// <summary>Verifies normal saturating addition returns the arithmetic sum.</summary>
    [Fact]
    public void SaturatingAdd_WhenNormal_ReturnsSummed() =>
        LayoutMath.SaturatingAdd(5, -3).ShouldBe(2);

    /// <summary>Verifies normal negation returns the sign-flipped value.</summary>
    [Fact]
    public void Negate_WhenNormal_ReturnsNegated() =>
        LayoutMath.Negate(5).ShouldBe(-5);

    /// <summary>Verifies <see cref="int.MinValue"/> maps to <see cref="int.MaxValue"/>.</summary>
    [Fact]
    public void Negate_WhenMinValue_ReturnsMaxValue() =>
        LayoutMath.Negate(int.MinValue).ShouldBe(int.MaxValue);
}
