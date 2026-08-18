// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Charts;

/// <summary>Defines optional authored chart bounds and automatic zero inclusion.</summary>
[PublicAPI]
public readonly struct ChartScale: IEquatable<ChartScale>
{
    /// <summary>Initializes a validated authored chart scale.</summary>
    /// <param name="minimum">The optional finite lower bound.</param>
    /// <param name="maximum">The optional finite upper bound.</param>
    /// <param name="includeZero">Whether automatic bounds expand to include zero.</param>
    /// <exception cref="ArgumentOutOfRangeException">A supplied bound is not finite.</exception>
    /// <exception cref="ArgumentException">The supplied minimum is not below the supplied maximum.</exception>
    public ChartScale(double? minimum, double? maximum, bool includeZero)
    {
        ArgumentOutOfRangeException.ThrowIfNotFinite(minimum, nameof(minimum), "Chart bounds must be finite.");
        ArgumentOutOfRangeException.ThrowIfNotFinite(maximum, nameof(maximum), "Chart bounds must be finite.");

        if (minimum.HasValue && maximum.HasValue)
        {
            ArgumentException.ThrowIfAtOrAboveMaximum(minimum.Value, maximum.Value, nameof(maximum), "The chart minimum must be lower than the chart maximum.");
        }

        Minimum = minimum;
        Maximum = maximum;
        IncludeZero = includeZero;
    }

    /// <summary>Gets the standard automatic scale, including zero.</summary>
    public static ChartScale Automatic => new(null, null, includeZero: true);

    /// <summary>Gets the optional authored lower bound.</summary>
    public double? Minimum { get; }

    /// <summary>Gets the optional authored upper bound.</summary>
    public double? Maximum { get; }

    /// <summary>Gets whether automatic bounds include zero.</summary>
    public bool IncludeZero { get; }

    /// <summary>Determines whether another scale has the same authored bounds and policy.</summary>
    /// <param name="other">The other scale.</param>
    /// <returns>True when every member is equal.</returns>
    public bool Equals(ChartScale other) =>
        Minimum == other.Minimum && Maximum == other.Maximum && IncludeZero == other.IncludeZero;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is ChartScale other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Minimum, Maximum, IncludeZero);

    /// <summary>Determines whether two scales are equal.</summary>
    public static bool operator ==(ChartScale left, ChartScale right) => left.Equals(right);

    /// <summary>Determines whether two scales differ.</summary>
    public static bool operator !=(ChartScale left, ChartScale right) => !left.Equals(right);
}
