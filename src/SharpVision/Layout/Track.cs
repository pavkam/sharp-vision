// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Layout;

/// <summary>Defines one immutable shared layout track length and responsive limits.</summary>
/// <remarks>
/// This is a struct. <c>new Track()</c> matches <see cref="Auto"/> via the
/// explicit parameterless constructor below, but <c>default(Track)</c> and any
/// unassigned <c>Track[]</c> slot still bypass every declared constructor and
/// zero-initialize every field — the CLR guarantees this for <c>default</c> and
/// array allocation regardless of what constructors a struct declares, and no
/// library-side fix can intercept it. Both give an invalid automatic <see cref="Minimum"/>,
/// so typed <see cref="Tracks"/> resolution rejects the uninitialized slot rather than silently
/// collapsing it. Always explicitly initialize every element of a caller-managed
/// <c>Track[]</c>.
/// </remarks>
[DebuggerDisplay("{ToString(),nq}")]
[PublicAPI]
public readonly record struct Track
{
    /// <summary>Initializes an automatic track, matching <see cref="Auto"/>'s defaults.</summary>
    /// <remarks>
    /// Unlike <c>default(Track)</c> or an unassigned <c>Track[]</c> slot, which the CLR
    /// zero-initializes regardless of this constructor, <c>new Track()</c> reaches this
    /// and resolves the same way <see cref="Auto"/> does.
    /// </remarks>
    public Track()
        : this(Length.Auto, Length.Cells(0))
    {
    }

    /// <summary>Initializes one validated track.</summary>
    /// <param name="length">The fixed, percentage, automatic, or proportional length.</param>
    /// <param name="minimum">The fixed or percentage minimum.</param>
    /// <param name="maximum">The fixed or percentage maximum, or null when unbounded.</param>
    /// <exception cref="ArgumentException">A limit uses Auto or Star, or comparable limits are inverted.</exception>
    public Track(Length length, Length? minimum = null, Length? maximum = null)
    {
        var authoredMinimum = minimum ?? Length.Cells(0);
        ValidateLimit(authoredMinimum, nameof(minimum));

        if (maximum is { } authoredMaximum)
        {
            ValidateLimit(authoredMaximum, nameof(maximum));

            if (authoredMinimum.Kind == authoredMaximum.Kind && authoredMinimum.Value > authoredMaximum.Value)
            {
                throw new ArgumentException("A track maximum cannot be below its comparable minimum.", nameof(maximum));
            }
        }

        Length = length;
        Minimum = authoredMinimum;
        Maximum = maximum;
    }

    /// <summary>Gets the track sizing strategy.</summary>
    public Length Length { get; }

    /// <summary>Gets the fixed or percentage minimum.</summary>
    public Length Minimum { get; }

    /// <summary>Gets the fixed or percentage maximum, or null when unbounded.</summary>
    public Length? Maximum { get; }

    /// <summary>Creates an automatic track.</summary>
    /// <param name="minimum">The fixed or percentage minimum, or null for zero cells.</param>
    /// <param name="maximum">The fixed or percentage maximum, or null when unbounded.</param>
    /// <returns>The validated automatic track.</returns>
    /// <exception cref="ArgumentException">A limit uses Auto or Star, or comparable limits are inverted.</exception>
    [Pure]
    public static Track Auto(Length? minimum = null, Length? maximum = null) =>
        new(Length.Auto, minimum, maximum);

    /// <summary>Creates a fixed-cell track.</summary>
    /// <param name="value">The non-negative fixed cell request.</param>
    /// <param name="minimum">The fixed or percentage minimum, or null for zero cells.</param>
    /// <param name="maximum">The fixed or percentage maximum, or null when unbounded.</param>
    /// <returns>The validated fixed-cell track.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is negative.</exception>
    /// <exception cref="ArgumentException">A limit uses Auto or Star, or comparable limits are inverted.</exception>
    [Pure]
    public static Track Cells(int value, Length? minimum = null, Length? maximum = null) =>
        new(Length.Cells(value), minimum, maximum);

    /// <summary>Creates a percentage track.</summary>
    /// <param name="value">The finite percentage from zero through one hundred.</param>
    /// <param name="minimum">The fixed or percentage minimum, or null for zero cells.</param>
    /// <param name="maximum">The fixed or percentage maximum, or null when unbounded.</param>
    /// <returns>The validated percentage track.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is outside zero through one hundred.</exception>
    /// <exception cref="ArgumentException">A limit uses Auto or Star, or comparable limits are inverted.</exception>
    [Pure]
    public static Track Percent(double value, Length? minimum = null, Length? maximum = null) =>
        new(Length.Percent(value), minimum, maximum);

    /// <summary>Creates a proportional track.</summary>
    /// <param name="value">The finite positive proportional weight.</param>
    /// <param name="minimum">The fixed or percentage minimum, or null for zero cells.</param>
    /// <param name="maximum">The fixed or percentage maximum, or null when unbounded.</param>
    /// <returns>The validated proportional track.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is not positive and finite.</exception>
    /// <exception cref="ArgumentException">A limit uses Auto or Star, or comparable limits are inverted.</exception>
    [Pure]
    public static Track Star(double value, Length? minimum = null, Length? maximum = null) =>
        new(Length.Star(value), minimum, maximum);

    /// <inheritdoc />
    public override string ToString() =>
        Minimum == Length.Cells(0) && Maximum is null
            ? Length.ToString()
            : Maximum is null
                ? $"{Length} [{Minimum}..∞]"
                : $"{Length} [{Minimum}..{Maximum.Value}]";

    private static void ValidateLimit(Length limit, string paramName)
    {
        if (limit.Kind is LengthKind.Auto or LengthKind.Star)
        {
            throw new ArgumentException("A track limit must use Cells or Percent.", paramName);
        }
    }
}
