// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Input;

/// <summary>Verifies the shared month/day/year clamp-and-replace arithmetic composed into
/// DateInput and DateTimeInput - directly against <see cref="TemporalCalendarArithmetic"/>,
/// mirroring <see cref="TemporalSegmentClassificationTests"/>'s direct-call style. Both concrete
/// controls now call this shared type unchanged, so a regression here is a regression for
/// both.</summary>
public sealed class TemporalCalendarArithmeticTests
{
    #region ClampDayOfMonth

    /// <summary>Verifies a Feb 29 day survives unchanged for a leap year.</summary>
    [Fact]
    public void ClampDayOfMonth_WhenYearIsLeapAndDayIsFeb29_LeavesDayUnchanged()
    {
        var result = TemporalCalendarArithmetic.ClampDayOfMonth(2024, 2, 29);

        result.ShouldBe((2024, 2, 29));
    }

    /// <summary>Verifies a Feb 29 day clamps to Feb 28 for a non-leap year.</summary>
    [Fact]
    public void ClampDayOfMonth_WhenYearIsNotLeapAndDayIsFeb29_ClampsToFeb28()
    {
        var result = TemporalCalendarArithmetic.ClampDayOfMonth(2026, 2, 29);

        result.ShouldBe((2026, 2, 28));
    }

    /// <summary>Verifies day 31 clamps to day 30 when the month has only 30 days.</summary>
    [Fact]
    public void ClampDayOfMonth_WhenDayExceedsThirtyDayMonth_ClampsToThirty()
    {
        var result = TemporalCalendarArithmetic.ClampDayOfMonth(2026, 4, 31);

        result.ShouldBe((2026, 4, 30));
    }

    /// <summary>Verifies a day already within the month's range passes through unchanged.</summary>
    [Fact]
    public void ClampDayOfMonth_WhenDayIsAlreadyValid_LeavesDayUnchanged()
    {
        var result = TemporalCalendarArithmetic.ClampDayOfMonth(2026, 7, 19);

        result.ShouldBe((2026, 7, 19));
    }

    #endregion

    #region ReplaceMonth

    /// <summary>Verifies replacing the month keeps the year and clamps day 31 into a 30-day month.</summary>
    [Fact]
    public void ReplaceMonth_WhenNewMonthHasFewerDays_ClampsDay()
    {
        var result = TemporalCalendarArithmetic.ReplaceMonth(2026, 31, 4);

        result.ShouldBe((2026, 4, 30));
    }

    /// <summary>Verifies replacing the month onto one with enough days leaves the day unchanged.</summary>
    [Fact]
    public void ReplaceMonth_WhenNewMonthHasEnoughDays_LeavesDayUnchanged()
    {
        var result = TemporalCalendarArithmetic.ReplaceMonth(2026, 15, 8);

        result.ShouldBe((2026, 8, 15));
    }

    #endregion

    #region ReplaceYear

    /// <summary>Verifies a Feb 29 day clamps to Feb 28 when the new year is not a leap year.</summary>
    [Fact]
    public void ReplaceYear_WhenNewYearIsNotLeapAndDayIsFeb29_ClampsToFeb28()
    {
        var result = TemporalCalendarArithmetic.ReplaceYear(2, 29, 2026);

        result.ShouldBe((2026, 2, 28));
    }

    /// <summary>Verifies a Feb 29 day survives unchanged when the new year is also a leap year.</summary>
    [Fact]
    public void ReplaceYear_WhenNewYearIsAlsoLeap_LeavesDayUnchanged()
    {
        var result = TemporalCalendarArithmetic.ReplaceYear(2, 29, 2028);

        result.ShouldBe((2028, 2, 29));
    }

    /// <summary>Verifies a year of zero or below clamps up to the minimum supported year, 1.</summary>
    [Fact]
    public void ReplaceYear_WhenNewYearIsZero_ClampsToOne()
    {
        var result = TemporalCalendarArithmetic.ReplaceYear(6, 15, 0);

        result.ShouldBe((1, 6, 15));
    }

    /// <summary>Verifies a year of 10000 or above clamps down to the maximum supported year, 9999.</summary>
    [Fact]
    public void ReplaceYear_WhenNewYearIsTenThousand_ClampsToNineNineNineNine()
    {
        var result = TemporalCalendarArithmetic.ReplaceYear(6, 15, 10000);

        result.ShouldBe((9999, 6, 15));
    }

    #endregion
}
