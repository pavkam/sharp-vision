// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies the shared Minimum/Maximum endpoint guards controls delegate to.</summary>
public sealed class RangeValidationTests
{
    /// <summary>Verifies a strictly ordered pair passes the inclusive "at or above" guard.</summary>
    [Fact]
    public void ThrowIfAtOrAboveMaximum_WhenMinimumIsBelowMaximum_DoesNotThrow() =>
        Should.NotThrow(() => ArgumentException.ThrowIfAtOrAboveMaximum(1.0, 2.0, "value", "message"));

    /// <summary>Verifies an ordinary equal pair is rejected.</summary>
    [Fact]
    public void ThrowIfAtOrAboveMaximum_WhenEndpointsAreEqual_Throws() =>
        Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfAtOrAboveMaximum(1.0, 1.0, "value", "message"));

    /// <summary>Verifies -0.0 and +0.0 - numerically equal but distinct bit patterns - are rejected,
    /// since <see cref="double.CompareTo(double)"/> reports them as equal.</summary>
    [Fact]
    public void ThrowIfAtOrAboveMaximum_WhenEndpointsAreNegativeAndPositiveZero_Throws() =>
        Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfAtOrAboveMaximum(-0.0, 0.0, "value", "message"));

    /// <summary>Verifies a minimum above the maximum is rejected.</summary>
    [Fact]
    public void ThrowIfAtOrAboveMaximum_WhenMinimumExceedsMaximum_Throws() =>
        Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfAtOrAboveMaximum(3.0, 2.0, "value", "message"));

    /// <summary>Verifies a strictly ordered pair passes the inclusive "at or below" guard.</summary>
    [Fact]
    public void ThrowIfAtOrBelowMinimum_WhenMaximumIsAboveMinimum_DoesNotThrow() =>
        Should.NotThrow(() => ArgumentException.ThrowIfAtOrBelowMinimum(2.0, 1.0, "value", "message"));

    /// <summary>Verifies an ordinary equal pair is rejected.</summary>
    [Fact]
    public void ThrowIfAtOrBelowMinimum_WhenEndpointsAreEqual_Throws() =>
        Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfAtOrBelowMinimum(1.0, 1.0, "value", "message"));

    /// <summary>Verifies -0.0 and +0.0 - numerically equal but distinct bit patterns - are rejected,
    /// since <see cref="double.CompareTo(double)"/> reports them as equal.</summary>
    [Fact]
    public void ThrowIfAtOrBelowMinimum_WhenEndpointsArePositiveAndNegativeZero_Throws() =>
        Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfAtOrBelowMinimum(0.0, -0.0, "value", "message"));

    /// <summary>Verifies a maximum below the minimum is rejected.</summary>
    [Fact]
    public void ThrowIfAtOrBelowMinimum_WhenMaximumIsBelowMinimum_Throws() =>
        Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfAtOrBelowMinimum(1.0, 2.0, "value", "message"));

    /// <summary>Verifies a minimum equal to the maximum passes the non-strict "above maximum" guard.</summary>
    [Fact]
    public void ThrowIfAboveMaximum_WhenMinimumEqualsMaximum_DoesNotThrow() =>
        Should.NotThrow(() => ArgumentException.ThrowIfAboveMaximum(1.0, 1.0, "value", "message"));

    /// <summary>Verifies a minimum above the maximum is rejected.</summary>
    [Fact]
    public void ThrowIfAboveMaximum_WhenMinimumExceedsMaximum_Throws() =>
        Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfAboveMaximum(2.0, 1.0, "value", "message"));

    /// <summary>Verifies a maximum equal to the minimum passes the non-strict "below minimum" guard.</summary>
    [Fact]
    public void ThrowIfBelowMinimum_WhenMaximumEqualsMinimum_DoesNotThrow() =>
        Should.NotThrow(() => ArgumentException.ThrowIfBelowMinimum(1.0, 1.0, "value", "message"));

    /// <summary>Verifies a maximum below the minimum is rejected.</summary>
    [Fact]
    public void ThrowIfBelowMinimum_WhenMaximumIsBelowMinimum_Throws() =>
        Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfBelowMinimum(1.0, 2.0, "value", "message"));

    /// <summary>Verifies a value inside the inclusive range passes.</summary>
    [Fact]
    public void ThrowIfOutsideInclusiveRange_WhenValueIsInsideRange_DoesNotThrow() =>
        Should.NotThrow(() => ArgumentOutOfRangeException.ThrowIfOutsideInclusiveRange(5.0, 1.0, 10.0, "value", "message"));

    /// <summary>Verifies a value at either inclusive endpoint passes.</summary>
    [Fact]
    public void ThrowIfOutsideInclusiveRange_WhenValueIsAtAnEndpoint_DoesNotThrow()
    {
        Should.NotThrow(() => ArgumentOutOfRangeException.ThrowIfOutsideInclusiveRange(1.0, 1.0, 10.0, "value", "message"));
        Should.NotThrow(() => ArgumentOutOfRangeException.ThrowIfOutsideInclusiveRange(10.0, 1.0, 10.0, "value", "message"));
    }

    /// <summary>Verifies a value below the range is rejected.</summary>
    [Fact]
    public void ThrowIfOutsideInclusiveRange_WhenValueIsBelowMinimum_Throws() =>
        Should.Throw<ArgumentOutOfRangeException>(() => ArgumentOutOfRangeException.ThrowIfOutsideInclusiveRange(0.0, 1.0, 10.0, "value", "message"));

    /// <summary>Verifies a value above the range is rejected.</summary>
    [Fact]
    public void ThrowIfOutsideInclusiveRange_WhenValueIsAboveMaximum_Throws() =>
        Should.Throw<ArgumentOutOfRangeException>(() => ArgumentOutOfRangeException.ThrowIfOutsideInclusiveRange(11.0, 1.0, 10.0, "value", "message"));

    /// <summary>Verifies a value inside the inclusive range is returned unchanged.</summary>
    [Fact]
    public void Clamp_WhenValueIsInsideRange_ReturnsValueUnchanged() =>
        5.0.Clamp(1.0, 10.0).ShouldBe(5.0);

    /// <summary>Verifies a value below the minimum is constrained to the minimum.</summary>
    [Fact]
    public void Clamp_WhenValueIsBelowMinimum_ReturnsMinimum() =>
        0.0.Clamp(1.0, 10.0).ShouldBe(1.0);

    /// <summary>Verifies a value above the maximum is constrained to the maximum.</summary>
    [Fact]
    public void Clamp_WhenValueIsAboveMaximum_ReturnsMaximum() =>
        11.0.Clamp(1.0, 10.0).ShouldBe(10.0);

    /// <summary>Verifies a value at either inclusive endpoint is returned unchanged.</summary>
    [Fact]
    public void Clamp_WhenValueIsAtAnEndpoint_ReturnsThatEndpoint()
    {
        1.0.Clamp(1.0, 10.0).ShouldBe(1.0);
        10.0.Clamp(1.0, 10.0).ShouldBe(10.0);
    }

    /// <summary>Verifies a positive whole-minute step passes.</summary>
    [Fact]
    public void ThrowIfNotAPositiveWholeMinuteStep_WhenValueIsAPositiveWholeMinute_DoesNotThrow() =>
        Should.NotThrow(() => ArgumentOutOfRangeException.ThrowIfNotAPositiveWholeMinuteStep(TimeSpan.FromMinutes(5), "value"));

    /// <summary>Verifies a zero step is rejected.</summary>
    [Fact]
    public void ThrowIfNotAPositiveWholeMinuteStep_WhenValueIsZero_Throws() =>
        Should.Throw<ArgumentOutOfRangeException>(() => ArgumentOutOfRangeException.ThrowIfNotAPositiveWholeMinuteStep(TimeSpan.Zero, "value"));

    /// <summary>Verifies a negative step is rejected.</summary>
    [Fact]
    public void ThrowIfNotAPositiveWholeMinuteStep_WhenValueIsNegative_Throws() =>
        Should.Throw<ArgumentOutOfRangeException>(() => ArgumentOutOfRangeException.ThrowIfNotAPositiveWholeMinuteStep(TimeSpan.FromMinutes(-1), "value"));

    /// <summary>Verifies a fractional-minute step is rejected.</summary>
    [Fact]
    public void ThrowIfNotAPositiveWholeMinuteStep_WhenValueIsFractionalMinute_Throws() =>
        Should.Throw<ArgumentOutOfRangeException>(() => ArgumentOutOfRangeException.ThrowIfNotAPositiveWholeMinuteStep(TimeSpan.FromSeconds(90), "value"));

    /// <summary>Verifies a positive whole step passes.</summary>
    [Fact]
    public void ThrowIfNotAPositiveStep_WhenValueIsPositive_DoesNotThrow() =>
        Should.NotThrow(() => ArgumentOutOfRangeException.ThrowIfNotAPositiveStep(1m, "value"));

    /// <summary>Verifies a positive fractional step passes.</summary>
    [Fact]
    public void ThrowIfNotAPositiveStep_WhenValueIsPositiveFraction_DoesNotThrow() =>
        Should.NotThrow(() => ArgumentOutOfRangeException.ThrowIfNotAPositiveStep(0.5m, "value"));

    /// <summary>Verifies a zero step is rejected, and that the exception carries the parameter
    /// name and offending value.</summary>
    [Fact]
    public void ThrowIfNotAPositiveStep_WhenValueIsZero_Throws()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => ArgumentOutOfRangeException.ThrowIfNotAPositiveStep(0m, "value"));

        exception.ParamName.ShouldBe("value");
        exception.ActualValue.ShouldBe(0m);
    }

    /// <summary>Verifies a negative step is rejected, and that the exception carries the parameter
    /// name and offending value.</summary>
    [Fact]
    public void ThrowIfNotAPositiveStep_WhenValueIsNegative_Throws()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => ArgumentOutOfRangeException.ThrowIfNotAPositiveStep(-1m, "value"));

        exception.ParamName.ShouldBe("value");
        exception.ActualValue.ShouldBe(-1m);
    }

    /// <summary>Verifies an exact division returns the unrounded quotient.</summary>
    [Fact]
    public void RoundHalfUp_WhenDivisionIsExact_ReturnsExactQuotient() =>
        RangeValidation.RoundHalfUp(10, 5).ShouldBe(2);

    /// <summary>Verifies a remainder of exactly one half rounds up.</summary>
    [Fact]
    public void RoundHalfUp_WhenRemainderIsExactlyHalf_RoundsUp() =>
        RangeValidation.RoundHalfUp(5, 2).ShouldBe(3);

    /// <summary>Verifies a remainder below one half rounds down.</summary>
    [Fact]
    public void RoundHalfUp_WhenRemainderIsBelowHalf_RoundsDown() =>
        RangeValidation.RoundHalfUp(4, 3).ShouldBe(1);

    /// <summary>Verifies a unit denominator returns the numerator unchanged.</summary>
    [Fact]
    public void RoundHalfUp_WhenDenominatorIsOne_ReturnsNumerator() =>
        RangeValidation.RoundHalfUp(7, 1).ShouldBe(7);

    /// <summary>Verifies a zero numerator returns zero regardless of denominator.</summary>
    [Fact]
    public void RoundHalfUp_WhenNumeratorIsZero_ReturnsZero() =>
        RangeValidation.RoundHalfUp(0, 5).ShouldBe(0);

    /// <summary>Verifies finite values, including zero and negative values, pass.</summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.5)]
    [InlineData(100.0)]
    public void ThrowIfNotFinite_Double_WhenValueIsFinite_DoesNotThrow(double value) =>
        Should.NotThrow(() => ArgumentOutOfRangeException.ThrowIfNotFinite(value, nameof(value), "message"));

    /// <summary>Verifies NaN is rejected.</summary>
    [Fact]
    public void ThrowIfNotFinite_Double_WhenValueIsNaN_Throws() =>
        Should.Throw<ArgumentOutOfRangeException>(() => ArgumentOutOfRangeException.ThrowIfNotFinite(double.NaN, "value", "message"));

    /// <summary>Verifies positive infinity is rejected.</summary>
    [Fact]
    public void ThrowIfNotFinite_Double_WhenValueIsPositiveInfinity_Throws() =>
        Should.Throw<ArgumentOutOfRangeException>(() => ArgumentOutOfRangeException.ThrowIfNotFinite(double.PositiveInfinity, "value", "message"));

    /// <summary>Verifies negative infinity is rejected.</summary>
    [Fact]
    public void ThrowIfNotFinite_Double_WhenValueIsNegativeInfinity_Throws() =>
        Should.Throw<ArgumentOutOfRangeException>(() => ArgumentOutOfRangeException.ThrowIfNotFinite(double.NegativeInfinity, "value", "message"));

    /// <summary>Verifies the supplied parameter name and message propagate onto the thrown exception.</summary>
    [Fact]
    public void ThrowIfNotFinite_Double_WhenValueIsNotFinite_ThrowsWithSuppliedParamNameAndMessage()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => ArgumentOutOfRangeException.ThrowIfNotFinite(double.NaN, "paramName", "custom message"));

        exception.ParamName.ShouldBe("paramName");
        exception.Message.ShouldStartWith("custom message");
    }

    /// <summary>Verifies finite wrapped values pass.</summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.5)]
    [InlineData(100.0)]
    public void ThrowIfNotFinite_NullableDouble_WhenValueIsFinite_DoesNotThrow(double value) =>
        Should.NotThrow(() => ArgumentOutOfRangeException.ThrowIfNotFinite((double?) value, nameof(value), "message"));

    /// <summary>Verifies a null value is accepted.</summary>
    [Fact]
    public void ThrowIfNotFinite_NullableDouble_WhenValueIsNull_DoesNotThrow() =>
        Should.NotThrow(() => ArgumentOutOfRangeException.ThrowIfNotFinite(null, "value", "message"));

    /// <summary>Verifies a wrapped NaN is rejected.</summary>
    [Fact]
    public void ThrowIfNotFinite_NullableDouble_WhenValueIsNaN_Throws() =>
        Should.Throw<ArgumentOutOfRangeException>(() => ArgumentOutOfRangeException.ThrowIfNotFinite((double?) double.NaN, "value", "message"));

    /// <summary>Verifies a wrapped positive infinity is rejected.</summary>
    [Fact]
    public void ThrowIfNotFinite_NullableDouble_WhenValueIsPositiveInfinity_Throws() =>
        Should.Throw<ArgumentOutOfRangeException>(
            () => ArgumentOutOfRangeException.ThrowIfNotFinite((double?) double.PositiveInfinity, "value", "message"));

    /// <summary>Verifies a wrapped negative infinity is rejected.</summary>
    [Fact]
    public void ThrowIfNotFinite_NullableDouble_WhenValueIsNegativeInfinity_Throws() =>
        Should.Throw<ArgumentOutOfRangeException>(
            () => ArgumentOutOfRangeException.ThrowIfNotFinite((double?) double.NegativeInfinity, "value", "message"));

    /// <summary>Verifies the supplied parameter name and message propagate onto the thrown exception.</summary>
    [Fact]
    public void ThrowIfNotFinite_NullableDouble_WhenValueIsNotFinite_ThrowsWithSuppliedParamNameAndMessage()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => ArgumentOutOfRangeException.ThrowIfNotFinite((double?) double.NaN, "paramName", "custom message"));

        exception.ParamName.ShouldBe("paramName");
        exception.Message.ShouldStartWith("custom message");
    }
}
