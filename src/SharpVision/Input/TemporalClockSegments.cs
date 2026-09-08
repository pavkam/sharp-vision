// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

/// <summary>Implements the shared default-pattern construction, clock-segment maximum-value
/// policy, and sub-second tick preservation used by the two clock-capable segmented fields
/// (<see cref="Controls.Input.TimeInput"/> and <see cref="Controls.Input.DateTimeInput"/>).
/// </summary>
/// <remarks>
/// <see cref="Controls.Input.TimeInput"/> and <see cref="Controls.Input.DateTimeInput"/> keep
/// their own <c>Use24HourFormat</c>/<c>ShowSeconds</c>/<c>Format</c>/<c>TimeStep</c> public
/// properties - they differ in the underlying value type
/// (<see cref="TimeOnly"/> versus <see cref="DateTime"/>) and, for <see
/// cref="Controls.Input.DateTimeInput"/>, compose a leading date pattern the time-only field never
/// has - but the algorithms these properties feed were duplicated near-verbatim before this type
/// existed. Every member here is a pure function over plain values, matching the same stateless
/// composition <see cref="TemporalCalendarArithmetic"/> and <see cref="TemporalClockArithmetic"/>
/// already use.
/// </remarks>
internal static class TemporalClockSegments
{
    /// <summary>Builds the fixed hour:minute[:second][ designator] custom format pattern
    /// <see cref="Controls.Input.TimeInput"/> and <see cref="Controls.Input.DateTimeInput"/> derive
    /// from their own structural flags when no explicit <c>Format</c> override is set.</summary>
    /// <param name="use24HourFormat">Whether the hour run is <c>"HH"</c> (24-hour) or <c>"hh"</c>
    /// (12-hour, paired with a trailing AM/PM designator run).</param>
    /// <param name="showSeconds">Whether a <c>":ss"</c> run follows the minute run.</param>
    /// <returns>The composed time-only pattern, with no leading date portion.</returns>
    [Pure]
    public static string BuildDefaultTimePattern(bool use24HourFormat, bool showSeconds)
    {
        var pattern = new StringBuilder(use24HourFormat ? "HH" : "hh").Append(':').Append("mm");

        if (showSeconds)
        {
            _ = pattern.Append(':').Append("ss");
        }

        if (!use24HourFormat)
        {
            _ = pattern.Append(' ').Append("tt");
        }

        return pattern.ToString();
    }

    /// <summary>Resolves the largest numeric value an editable clock segment can hold, for the
    /// four segment kinds every clock-capable field shares.</summary>
    /// <param name="kind">The segment's semantic kind.</param>
    /// <param name="hasAmPmDesignator">Whether the current layout has a 12-hour AM/PM designator
    /// segment, governing the hour segment's own maximum.</param>
    /// <param name="runLength">The pattern run length backing a fractional-second segment.</param>
    /// <returns>The segment's maximum value, or zero for a segment kind this helper does not
    /// own (a calendar kind, resolved instead by <see cref="Controls.Input.DateTimeInput"/>'s own
    /// seam).</returns>
    [Pure]
#pragma warning disable IDE0072 // Month, Day, and Year are calendar kinds owned by DateTimeInput's own seam, not this clock-only helper.
    public static int MaxValueFor(TemporalSegmentKind kind, bool hasAmPmDesignator, int runLength) =>
        kind switch
        {
            TemporalSegmentKind.Hour => hasAmPmDesignator ? 12 : 23,
            TemporalSegmentKind.Minute or TemporalSegmentKind.Second => 59,
            TemporalSegmentKind.FractionalSecond => TemporalClockArithmetic.FractionalSecondMaxValue(runLength),
            _ => 0
        };
#pragma warning restore IDE0072

    /// <summary>Gets the sub-second (tick-resolution) remainder of a tick count, the fragment a
    /// whole-component reconstruction (new hour/minute/second, or a replaced calendar component)
    /// would otherwise silently drop.</summary>
    /// <param name="ticks">The original value's tick count.</param>
    /// <returns>The remainder after the largest whole number of seconds is removed.</returns>
    [Pure]
    public static long SubSecondRemainderTicks(long ticks) => ticks % TimeSpan.TicksPerSecond;
}
