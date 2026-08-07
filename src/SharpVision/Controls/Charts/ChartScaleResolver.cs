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

        if (values.IsEmpty)
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

    private static ChartScaleRange ResolveEmpty(ChartScale scale)
    {
        return scale.Minimum is { } minimum && scale.Maximum is { } maximum
            ? new ChartScaleRange(minimum, maximum)
            : scale.Minimum is { } lower
                ? new ChartScaleRange(lower, Math.Max(lower + 1, scale.IncludeZero ? 0 : lower + 1))
                : scale.Maximum is { } upper
                    ? new ChartScaleRange(Math.Min(upper - 1, scale.IncludeZero ? 0 : upper - 1), upper)
                    : new ChartScaleRange(0, 1);
    }

    private static ChartScaleRange ExpandCollapsed(ChartScale scale, double value)
    {
        var margin = value == 0 ? 1 : Math.Max(Math.Abs(value) * 0.1, 1);

        return scale.Minimum.HasValue
            ? new ChartScaleRange(value, value + margin)
            : scale.Maximum.HasValue
                ? new ChartScaleRange(value - margin, value)
                : new ChartScaleRange(value - margin, value + margin);
    }
}
