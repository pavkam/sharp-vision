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
}
