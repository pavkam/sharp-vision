// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

/// <summary>
/// Implements the shared hour/minute/second digit-entry clamp bounds used by every segmented
/// clock field control (<see cref="Controls.Input.TimeInput"/>, <see
/// cref="Controls.Input.DateTimeInput"/>) whenever a typed digit needs clamping to its segment's
/// valid range.
/// </summary>
/// <remarks>
/// Every member here is a pure function over a plain candidate value, so both <see
/// cref="TimeOnly"/>- and <see cref="DateTime"/>-typed controls convert at the call site instead
/// of this type depending on either concrete clock value type - the same stateless composition
/// <see cref="TemporalCalendarArithmetic"/> already uses. Unlike the calendar clamp, an hour or
/// minute/second bound never varies with another field's value, so this only needs to cover
/// digit entry; increment/decrement deliberately has no shared helper here - see <see
/// cref="Controls.Input.TimeInput"/>'s <c>AddWithoutWrap</c> and <see
/// cref="Controls.Input.DateTimeInput"/>'s <c>SafeAddTicks</c>, which solve genuinely different
/// overflow problems (a bounded same-day saturate versus a day-carrying add) on different
/// underlying types and are not a shared-code opportunity despite the superficial resemblance.
/// </remarks>
internal static class TemporalClockArithmetic
{
    /// <summary>Clamps a typed hour digit to its segment's valid range, 1-12 under a 12-hour
    /// AM/PM layout or 0-23 under a 24-hour layout.</summary>
    /// <param name="value">The candidate hour value.</param>
    /// <param name="hasAmPmDesignator">Whether the current layout has a 12-hour AM/PM
    /// designator segment.</param>
    public static int ClampHour(int value, bool hasAmPmDesignator) =>
        hasAmPmDesignator ? Math.Clamp(value, 1, 12) : Math.Clamp(value, 0, 23);

    /// <summary>Clamps a typed minute or second digit to its segment's valid 0-59 range.</summary>
    /// <param name="value">The candidate minute or second value.</param>
    public static int ClampMinuteOrSecond(int value) => Math.Clamp(value, 0, 59);
}
