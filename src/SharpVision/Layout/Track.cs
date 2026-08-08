// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Layout;

/// <summary>Defines one immutable Grid track length and cell limits.</summary>
/// <remarks>
/// This is a struct. <c>new Track()</c> now matches <see cref="Auto(int, int)"/> via the
/// explicit parameterless constructor below, but <c>default(Track)</c> and any
/// unassigned <c>Track[]</c> slot still bypass every declared constructor and
/// zero-initialize every field — the CLR guarantees this for <c>default</c> and
/// array allocation regardless of what constructors a struct declares, and no
/// library-side fix can intercept it. Both give <see cref="Minimum"/> == 0 and
/// <see cref="Maximum"/> == 0, not <see cref="Auto(int, int)"/>'s int.MaxValue maximum,
/// so <see cref="Tracks"/> resolution forces that slot to exactly 0 cells — a
/// permanently invisible track with no diagnostic. Always explicitly initialize
/// every element of a caller-managed <c>Track[]</c>.
/// </remarks>
[DebuggerDisplay("{ToString(),nq}")]
[PublicAPI]
public readonly record struct Track
{
    /// <summary>Initializes an automatic track, matching <see cref="Auto(int, int)"/>'s defaults.</summary>
    /// <remarks>
    /// Unlike <c>default(Track)</c> or an unassigned <c>Track[]</c> slot, which the CLR
    /// zero-initializes regardless of this constructor, <c>new Track()</c> reaches this
    /// and resolves the same way <see cref="Auto(int, int)"/> does.
    /// </remarks>
    public Track()
        : this(Length.Auto)
    {
    }

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

        maximum.ThrowIfBelowMinimum(minimum, nameof(maximum), "A track maximum cannot be below its minimum.");

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
