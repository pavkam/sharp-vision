// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

/// <summary>
/// Implements the shared month/day/year clamp-and-replace arithmetic used by every segmented
/// calendar field control (<see cref="Controls.Input.DateInput"/>, <see
/// cref="Controls.Input.DateTimeInput"/>) whenever a typed digit or a segment clear/replace would
/// otherwise land on a day the resulting month or year cannot represent - for example replacing
/// the year under a Feb 29 value with a non-leap year, or the month under a day-31 value with a
/// 30-day month.
/// </summary>
/// <remarks>
/// Every member here is a pure function over a plain year/month/day triple, so both <see
/// cref="DateOnly"/>- and <see cref="DateTime"/>-typed controls convert at the call site instead
/// of this type depending on either concrete calendar value type - the same stateless composition
/// <see cref="TemporalSegmentClassification"/> and <see cref="TemporalPatternSegmenter"/> already
/// use.
/// </remarks>
internal static class TemporalCalendarArithmetic
{
    /// <summary>Clamps a day to the last day the given year and month can represent, leaving the
    /// year and month unchanged.</summary>
    /// <param name="year">The year, already in the valid 1-9999 range.</param>
    /// <param name="month">The month, already in the valid 1-12 range.</param>
    /// <param name="day">The day to clamp.</param>
    [Pure]
    public static (int Year, int Month, int Day) ClampDayOfMonth(int year, int month, int day) =>
        (year, month, Math.Min(day, DateTime.DaysInMonth(year, month)));

    /// <summary>Replaces the month, clamping the existing day to the new month's day count -
    /// for instance day 31 replacing into April clamps to day 30.</summary>
    /// <param name="year">The year, already in the valid 1-9999 range.</param>
    /// <param name="day">The existing day.</param>
    /// <param name="newMonth">The replacement month, already in the valid 1-12 range.</param>
    [Pure]
    public static (int Year, int Month, int Day) ReplaceMonth(int year, int day, int newMonth) =>
        ClampDayOfMonth(year, newMonth, day);

    /// <summary>Replaces the year, clamping it to the 1-9999 range and clamping the existing day
    /// to the new year's day count for the given month - relevant only for a Feb 29 leap day
    /// replacing into a non-leap year.</summary>
    /// <param name="month">The month, already in the valid 1-12 range.</param>
    /// <param name="day">The existing day.</param>
    /// <param name="newYear">The replacement year.</param>
    [Pure]
    public static (int Year, int Month, int Day) ReplaceYear(int month, int day, int newYear) =>
        ClampDayOfMonth(Math.Clamp(newYear, 1, 9999), month, day);
}
