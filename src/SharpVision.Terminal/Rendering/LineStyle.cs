// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Rendering;

/// <summary>Describes an immutable Unicode or ASCII line family.</summary>
[PublicAPI]
public readonly record struct LineStyle
{
    /// <summary>Initializes a validated Unicode line style.</summary>
    /// <param name="weight">The stroke weight.</param>
    /// <param name="pattern">The straight-segment dash pattern.</param>
    /// <param name="rounded">Whether light solid corners use rounded glyphs.</param>
    /// <param name="ascii">Whether all segments use ASCII fallback glyphs.</param>
    /// <exception cref="ArgumentOutOfRangeException">An enum value is unknown.</exception>
    /// <exception cref="ArgumentException">
    /// Rounded corners are requested for a non-light or dashed style, or a
    /// double dashed style is requested when Unicode defines no such family.
    /// </exception>
    public LineStyle(
        LineWeight weight,
        LinePattern pattern = LinePattern.Solid,
        bool rounded = false,
        bool ascii = false)
    {
        ArgumentOutOfRangeException.ThrowIfNotDefined(weight, nameof(weight), "The line weight is unknown.");

        ArgumentOutOfRangeException.ThrowIfNotDefined(pattern, nameof(pattern), "The line pattern is unknown.");

        if (rounded && (weight != LineWeight.Light || pattern != LinePattern.Solid))
        {
            throw new ArgumentException(
                "Rounded corners require a light solid line.",
                nameof(rounded));
        }

        if (weight == LineWeight.Paired && pattern != LinePattern.Solid)
        {
            throw new ArgumentException(
                "Unicode does not define double-weight dashed box drawing.",
                nameof(pattern));
        }

        Weight = weight;
        Pattern = pattern;
        HasRoundedCorners = rounded;
        IsAscii = ascii;
    }

    /// <summary>Gets the standard light solid style.</summary>
    public static LineStyle Light { get; } = new(LineWeight.Light);

    /// <summary>Gets the standard heavy solid style.</summary>
    public static LineStyle Heavy { get; } = new(LineWeight.Heavy);

    /// <summary>Gets the standard double solid style.</summary>
    public static LineStyle Paired { get; } = new(LineWeight.Paired);

    /// <summary>Gets the light solid style with rounded corners.</summary>
    public static LineStyle Rounded { get; } = new(LineWeight.Light, rounded: true);

    /// <summary>Gets the portable ASCII fallback style.</summary>
    public static LineStyle Ascii { get; } = new(LineWeight.Light, ascii: true);

    /// <summary>Gets the stroke weight.</summary>
    public LineWeight Weight { get; }

    /// <summary>Gets the straight-segment dash pattern.</summary>
    public LinePattern Pattern { get; }

    /// <summary>Gets whether eligible corners use rounded glyphs.</summary>
    public bool HasRoundedCorners { get; }

    /// <summary>Gets whether the style uses portable ASCII glyphs.</summary>
    public bool IsAscii { get; }
}
