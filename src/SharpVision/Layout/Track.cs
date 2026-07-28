// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Layout;

/// <summary>Defines one immutable Grid track length and cell limits.</summary>
[DebuggerDisplay("{ToString(),nq}")]
[PublicAPI]
public readonly record struct Track
{
    /// <summary>Initializes one validated track.</summary>
    /// <param name="length">The fixed, percentage, automatic, or proportional length.</param>
    /// <param name="minimum">The non-negative minimum cells.</param>
    /// <param name="maximum">The maximum cells, not below the minimum.</param>
    /// <exception cref="ArgumentOutOfRangeException">A limit is negative.</exception>
    /// <exception cref="ArgumentException"><paramref name="maximum"/> is below the minimum.</exception>
    public Track(Length length, int minimum = 0, int maximum = int.MaxValue)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(minimum);
        ArgumentOutOfRangeException.ThrowIfNegative(maximum);

        if (maximum < minimum)
        {
            throw new ArgumentException("A track maximum cannot be below its minimum.", nameof(maximum));
        }

        Length = length;
        Minimum = minimum;
        Maximum = maximum;
    }

    /// <summary>Gets the track sizing strategy.</summary>
    public Length Length { get; }

    /// <summary>Gets the minimum cells.</summary>
    public int Minimum { get; }

    /// <summary>Gets the maximum cells.</summary>
    public int Maximum { get; }

    /// <summary>Creates an automatic track.</summary>
    [Pure]
    public static Track Auto(int minimum = 0, int maximum = int.MaxValue) =>
        new(Length.Auto, minimum, maximum);

    /// <summary>Creates a fixed-cell track.</summary>
    [Pure]
    public static Track Cells(int value, int minimum = 0, int maximum = int.MaxValue) =>
        new(Length.Cells(value), minimum, maximum);

    /// <summary>Creates a percentage track.</summary>
    [Pure]
    public static Track Percent(double value, int minimum = 0, int maximum = int.MaxValue) =>
        new(Length.Percent(value), minimum, maximum);

    /// <summary>Creates a proportional track.</summary>
    [Pure]
    public static Track Star(double value, int minimum = 0, int maximum = int.MaxValue) =>
        new(Length.Star(value), minimum, maximum);

    /// <inheritdoc />
    public override string ToString() =>
        Minimum == 0 && Maximum == int.MaxValue
            ? Length.ToString()
            : Maximum == int.MaxValue
                ? $"{Length} [{Minimum}..∞]"
                : $"{Length} [{Minimum}..{Maximum}]";
}
