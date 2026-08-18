// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Input;

/// <summary>Verifies the shared hour/minute/second digit-entry clamp bounds composed into
/// TimeInput and DateTimeInput - directly against <see cref="TemporalClockArithmetic"/>,
/// mirroring <see cref="TemporalCalendarArithmeticTests"/>'s direct-call style. Both concrete
/// controls now call this shared type unchanged, so a regression here is a regression for
/// both.</summary>
public sealed class TemporalClockArithmeticTests
{
    #region ClampHour

    /// <summary>Verifies an in-range 24-hour value passes through unchanged.</summary>
    [Fact]
    public void ClampHour_WhenTwentyFourHourAndValueInRange_LeavesValueUnchanged()
    {
        var result = TemporalClockArithmetic.ClampHour(17, hasAmPmDesignator: false);

        result.ShouldBe(17);
    }

    /// <summary>Verifies a 24-hour value above 23 clamps down to 23.</summary>
    [Fact]
    public void ClampHour_WhenTwentyFourHourAndValueExceedsTwentyThree_ClampsToTwentyThree()
    {
        var result = TemporalClockArithmetic.ClampHour(30, hasAmPmDesignator: false);

        result.ShouldBe(23);
    }

    /// <summary>Verifies a 24-hour value below 0 clamps up to 0.</summary>
    [Fact]
    public void ClampHour_WhenTwentyFourHourAndValueIsNegative_ClampsToZero()
    {
        var result = TemporalClockArithmetic.ClampHour(-4, hasAmPmDesignator: false);

        result.ShouldBe(0);
    }

    /// <summary>Verifies an in-range 12-hour value passes through unchanged.</summary>
    [Fact]
    public void ClampHour_WhenTwelveHourAndValueInRange_LeavesValueUnchanged()
    {
        var result = TemporalClockArithmetic.ClampHour(9, hasAmPmDesignator: true);

        result.ShouldBe(9);
    }

    /// <summary>Verifies a 12-hour value of 0 clamps up to 1, since a 12-hour clock face has no zero hour.</summary>
    [Fact]
    public void ClampHour_WhenTwelveHourAndValueIsZero_ClampsToOne()
    {
        var result = TemporalClockArithmetic.ClampHour(0, hasAmPmDesignator: true);

        result.ShouldBe(1);
    }

    /// <summary>Verifies a 12-hour value above 12 clamps down to 12.</summary>
    [Fact]
    public void ClampHour_WhenTwelveHourAndValueExceedsTwelve_ClampsToTwelve()
    {
        var result = TemporalClockArithmetic.ClampHour(23, hasAmPmDesignator: true);

        result.ShouldBe(12);
    }

    #endregion

    #region ClampMinuteOrSecond

    /// <summary>Verifies an in-range value passes through unchanged.</summary>
    [Fact]
    public void ClampMinuteOrSecond_WhenValueInRange_LeavesValueUnchanged()
    {
        var result = TemporalClockArithmetic.ClampMinuteOrSecond(42);

        result.ShouldBe(42);
    }

    /// <summary>Verifies a value above 59 clamps down to 59.</summary>
    [Fact]
    public void ClampMinuteOrSecond_WhenValueExceedsFiftyNine_ClampsToFiftyNine()
    {
        var result = TemporalClockArithmetic.ClampMinuteOrSecond(90);

        result.ShouldBe(59);
    }

    /// <summary>Verifies a negative value clamps up to 0.</summary>
    [Fact]
    public void ClampMinuteOrSecond_WhenValueIsNegative_ClampsToZero()
    {
        var result = TemporalClockArithmetic.ClampMinuteOrSecond(-1);

        result.ShouldBe(0);
    }

    #endregion
}
