// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Charts;

/// <summary>Resolves authored and automatic chart bounds deterministically.</summary>
internal static class ChartScaleResolver
{
    /// <summary>Resolves one non-empty finite range from authored policy and finite values.</summary>
    /// <param name="scale">The validated authored policy.</param>
    /// <param name="values">The finite values to inspect.</param>
    /// <returns>A finite range whose maximum is above its minimum.</returns>
    [Pure]
    internal static ChartScaleRange Resolve(ChartScale scale, ReadOnlySpan<double> values)
    {
        var observedMinimum = double.PositiveInfinity;
        var observedMaximum = double.NegativeInfinity;

        foreach (var value in values)
        {
            Debug.Assert(double.IsFinite(value));
            observedMinimum = Math.Min(observedMinimum, value);
            observedMaximum = Math.Max(observedMaximum, value);
        }

        return Resolve(scale, observedMinimum, observedMaximum, hasValues: !values.IsEmpty);
    }

    /// <summary>Resolves one non-empty finite range from authored policy and a chart's series,
    /// scanning each series' points directly for their minimum and maximum instead of requiring
    /// them to be materialized into an array first.</summary>
    /// <param name="scale">The validated authored policy.</param>
    /// <param name="series">The series whose point values are inspected.</param>
    /// <returns>A finite range whose maximum is above its minimum.</returns>
    [Pure]
    internal static ChartScaleRange Resolve(ChartScale scale, IReadOnlyList<ChartSeries> series)
    {
        var observedMinimum = double.PositiveInfinity;
        var observedMaximum = double.NegativeInfinity;
        var hasValues = false;

        foreach (var item in series)
        {
            foreach (var point in item.Points)
            {
                Debug.Assert(double.IsFinite(point.Value));
                observedMinimum = Math.Min(observedMinimum, point.Value);
                observedMaximum = Math.Max(observedMaximum, point.Value);
                hasValues = true;
            }
        }

        return Resolve(scale, observedMinimum, observedMaximum, hasValues);
    }

    [Pure]
    private static ChartScaleRange Resolve(
        ChartScale scale,
        double observedMinimum,
        double observedMaximum,
        bool hasValues)
    {
        if (!hasValues)
        {
            return ResolveEmpty(scale);
        }

        var minimum = scale.Minimum ?? observedMinimum;
        var maximum = scale.Maximum ?? observedMaximum;

        if (scale.IncludeZero)
        {
            if (!scale.Minimum.HasValue)
            {
                minimum = Math.Min(minimum, 0);
            }

            if (!scale.Maximum.HasValue)
            {
                maximum = Math.Max(maximum, 0);
            }
        }

        return minimum < maximum
            ? new ChartScaleRange(minimum, maximum)
            : ExpandCollapsed(scale, minimum);
    }

    [Pure]
    private static ChartScaleRange ResolveEmpty(ChartScale scale)
    {
        return scale.Minimum is { } minimum && scale.Maximum is { } maximum
            ? new ChartScaleRange(minimum, maximum)
            : scale.Minimum is { } lower
                ? ExpandFromMinimum(lower, scale.IncludeZero)
                : scale.Maximum is { } upper
                    ? ExpandFromMaximum(upper, scale.IncludeZero)
                    : new ChartScaleRange(0, 1);
    }

    [Pure]
    private static ChartScaleRange ExpandCollapsed(ChartScale scale, double value)
    {
        var margin = value == 0 ? 1 : Math.Max(Math.Abs(value) * 0.1, 1);

        if (scale.Minimum.HasValue)
        {
            return ExpandFromMinimum(value, includeZero: false, margin);
        }

        if (scale.Maximum.HasValue)
        {
            return ExpandFromMaximum(value, includeZero: false, margin);
        }

        var minimum = value - margin;
        var maximum = value + margin;

        return double.IsFinite(minimum) && double.IsFinite(maximum) && minimum < maximum
            ? new ChartScaleRange(minimum, maximum)
            : value == double.MaxValue
                ? new ChartScaleRange(double.BitDecrement(value), value)
                : new ChartScaleRange(value, double.BitIncrement(value));
    }

    [Pure]
    private static ChartScaleRange ExpandFromMinimum(double minimum, bool includeZero, double distance = 1)
    {
        var expanded = minimum + distance;
        var maximum = Math.Max(expanded, includeZero ? 0 : expanded);

        return double.IsFinite(maximum) && minimum < maximum
            ? new ChartScaleRange(minimum, maximum)
            : minimum == double.MaxValue
                ? new ChartScaleRange(double.BitDecrement(minimum), minimum)
                : new ChartScaleRange(minimum, double.BitIncrement(minimum));
    }

    [Pure]
    private static ChartScaleRange ExpandFromMaximum(double maximum, bool includeZero, double distance = 1)
    {
        var expanded = maximum - distance;
        var minimum = Math.Min(expanded, includeZero ? 0 : expanded);

        return double.IsFinite(minimum) && minimum < maximum
            ? new ChartScaleRange(minimum, maximum)
            : maximum == double.MinValue
                ? new ChartScaleRange(maximum, double.BitIncrement(maximum))
                : new ChartScaleRange(double.BitDecrement(maximum), maximum);
    }
}
