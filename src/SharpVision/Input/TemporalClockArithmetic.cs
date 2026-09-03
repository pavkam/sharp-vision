// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

using ValueRange = JetBrains.Annotations.ValueRangeAttribute;

/// <summary>
/// Implements the shared hour/minute/second digit-entry clamp bounds and fractional-second tick
/// conversions used by every segmented
/// clock field control (<see cref="Controls.Input.TimeInput"/>, <see
/// cref="Controls.Input.DateTimeInput"/>) whenever a typed digit needs clamping to its segment's
/// valid range.
/// </summary>
/// <remarks>
/// Every member here is a pure function over a plain candidate value, so both <see
/// cref="TimeOnly"/>- and <see cref="DateTime"/>-typed controls convert at the call site instead
/// of this type depending on either concrete clock value type - the same stateless composition
/// <see cref="TemporalCalendarArithmetic"/> already uses. Unlike the calendar clamp, an hour or
/// minute/second bound never varies with another field's value. Fractional-second helpers convert
/// the declared one-to-seven-digit precision without losing ticks. Whole clock-field
/// increment/decrement deliberately has no shared helper here - see <see
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
    [Pure]
    public static int ClampHour(int value, bool hasAmPmDesignator) =>
        hasAmPmDesignator ? Math.Clamp(value, 1, 12) : Math.Clamp(value, 0, 23);

    /// <summary>Clamps a typed minute or second digit to its segment's valid 0-59 range.</summary>
    /// <param name="value">The candidate minute or second value.</param>
    [Pure]
    [ValueRange(0, 59)]
    public static int ClampMinuteOrSecond(int value) => Math.Clamp(value, 0, 59);

    /// <summary>Converts a fractional-second value at a declared decimal precision into ticks.</summary>
    /// <param name="value">The zero-based fractional value.</param>
    /// <param name="digitCapacity">The number of decimal places represented, from 1 through 7.</param>
    /// <returns>The fractional value expressed as 100-nanosecond ticks.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The precision is outside 1-7, or the value cannot fit it.</exception>
    [Pure]
    public static long FractionalSecondTicks(int value, int digitCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(digitCapacity, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(digitCapacity, 7);
        var scale = PowerOfTen(digitCapacity);
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(value, scale);
        return value * (TimeSpan.TicksPerSecond / scale);
    }

    /// <summary>Gets the tick delta represented by one unit at a declared fractional precision.</summary>
    /// <param name="digitCapacity">The number of decimal places represented, from 1 through 7.</param>
    /// <returns>The positive tick delta for one displayed unit.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The precision is outside 1-7.</exception>
    [Pure]
    public static long FractionalSecondUnitTicks(int digitCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(digitCapacity, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(digitCapacity, 7);
        return TimeSpan.TicksPerSecond / PowerOfTen(digitCapacity);
    }

    /// <summary>Gets the largest decimal value representable by a fractional-second run.</summary>
    /// <param name="digitCapacity">The number of decimal places represented, from 1 through 7.</param>
    /// <returns>A value consisting of <paramref name="digitCapacity"/> nines.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The precision is outside 1-7.</exception>
    [Pure]
    public static int FractionalSecondMaxValue(int digitCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(digitCapacity, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(digitCapacity, 7);
        return PowerOfTen(digitCapacity) - 1;
    }

    [Pure]
    private static int PowerOfTen(int exponent)
    {
        var result = 1;

        for (var index = 0; index < exponent; index++)
        {
            result *= 10;
        }

        return result;
    }
}
