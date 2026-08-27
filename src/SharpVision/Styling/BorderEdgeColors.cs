// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Defines optional foreground overrides for the four physical edges of one border.</summary>
/// <remarks>Any missing edge inherits <see cref="Border.Foreground"/>. Horizontal edges own the
/// colors of the corner glyphs where two differently colored edges meet.</remarks>
public readonly record struct BorderEdgeColors: IAppearanceFragment
{
    /// <summary>Initializes optional physical-edge foreground overrides.</summary>
    /// <param name="top">The optional top-edge foreground.</param>
    /// <param name="right">The optional right-edge foreground.</param>
    /// <param name="bottom">The optional bottom-edge foreground.</param>
    /// <param name="left">The optional left-edge foreground.</param>
    /// <exception cref="ArgumentException">An override is transparent.</exception>
    public BorderEdgeColors(
        ControlColor? top = null,
        ControlColor? right = null,
        ControlColor? bottom = null,
        ControlColor? left = null)
    {
        Validate(top, nameof(top));
        Validate(right, nameof(right));
        Validate(bottom, nameof(bottom));
        Validate(left, nameof(left));
        Top = top;
        Right = right;
        Bottom = bottom;
        Left = left;
    }

    /// <summary>Gets the optional top-edge foreground.</summary>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public ControlColor? Top
    {
        get;
        init
        {
            Validate(value, nameof(value));
            field = value;
        }
    }

    /// <summary>Gets the optional right-edge foreground.</summary>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public ControlColor? Right
    {
        get;
        init
        {
            Validate(value, nameof(value));
            field = value;
        }
    }

    /// <summary>Gets the optional bottom-edge foreground.</summary>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public ControlColor? Bottom
    {
        get;
        init
        {
            Validate(value, nameof(value));
            field = value;
        }
    }

    /// <summary>Gets the optional left-edge foreground.</summary>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public ControlColor? Left
    {
        get;
        init
        {
            Validate(value, nameof(value));
            field = value;
        }
    }

    /// <summary>Creates a raised frame with highlighted top and left edges.</summary>
    /// <param name="highlight">The top and left foreground.</param>
    /// <param name="shade">The right and bottom foreground.</param>
    /// <returns>The complete raised-edge mapping.</returns>
    /// <exception cref="ArgumentException">A supplied foreground is transparent.</exception>
    public static BorderEdgeColors Raised(ControlColor highlight, ControlColor shade) =>
        new(highlight, shade, shade, highlight);

    /// <summary>Creates a sunken frame with shaded top and left edges.</summary>
    /// <param name="highlight">The right and bottom foreground.</param>
    /// <param name="shade">The top and left foreground.</param>
    /// <returns>The complete sunken-edge mapping.</returns>
    /// <exception cref="ArgumentException">A supplied foreground is transparent.</exception>
    public static BorderEdgeColors Sunken(ControlColor highlight, ControlColor shade) =>
        new(shade, highlight, highlight, shade);

    /// <summary>Resolves the top edge against the border's uniform fallback.</summary>
    internal ControlColor ResolveTop(ControlColor fallback) => Top ?? fallback;

    /// <summary>Resolves the right edge against the border's uniform fallback.</summary>
    internal ControlColor ResolveRight(ControlColor fallback) => Right ?? fallback;

    /// <summary>Resolves the bottom edge against the border's uniform fallback.</summary>
    internal ControlColor ResolveBottom(ControlColor fallback) => Bottom ?? fallback;

    /// <summary>Resolves the left edge against the border's uniform fallback.</summary>
    internal ControlColor ResolveLeft(ControlColor fallback) => Left ?? fallback;

    IAppearanceFragment IAppearanceFragment.Clone() => this with { };

    private static void Validate(ControlColor? value, string parameterName)
    {
        if (value is { } color)
        {
            ControlColor.ValidatePaint(color, parameterName);
        }
    }
}
