// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Defines one complete intrinsic border appearance.</summary>
public readonly record struct Border: IAppearanceFragment
{
    /// <summary>Initializes a complete border appearance.</summary>
    /// <param name="sides">The enabled one-cell edges.</param>
    /// <param name="glyphStyle">The complete border glyph family.</param>
    /// <param name="foreground">The border foreground.</param>
    /// <param name="background">The border-cell background.</param>
    /// <param name="attributes">The border attributes.</param>
    /// <exception cref="ArgumentException"><paramref name="foreground"/> is transparent.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="sides"/> contains unknown flags.</exception>
    public Border(
        BorderSide sides,
        BorderGlyphStyle glyphStyle,
        ControlColor foreground,
        ControlColor background,
        ControlDecoration attributes) : this(
            sides,
            glyphStyle,
            foreground,
            default,
            background,
            attributes)
    {
    }

    /// <summary>Initializes a complete border appearance with optional per-edge foregrounds.</summary>
    /// <param name="sides">The enabled one-cell edges.</param>
    /// <param name="glyphStyle">The complete border glyph family.</param>
    /// <param name="foreground">The uniform foreground inherited by edges without overrides.</param>
    /// <param name="edgeColors">The optional physical-edge foreground overrides.</param>
    /// <param name="background">The border-cell background.</param>
    /// <param name="attributes">The border attributes.</param>
    /// <exception cref="ArgumentException">A foreground is transparent.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="sides"/> contains unknown flags.</exception>
    public Border(
        BorderSide sides,
        BorderGlyphStyle glyphStyle,
        ControlColor foreground,
        BorderEdgeColors edgeColors,
        ControlColor background,
        ControlDecoration attributes)
    {
        // Validated by parameter name here as well as in each init accessor. The accessor guards
        // the doors a constructor cannot see - a `with` expression and the theme overlay's
        // reflective write - but it only ever knows the value as "value", which for a constructor
        // taking several channels of the same type identifies nothing.
        //
        // Deliberately NOT ArgumentOutOfRangeException.ThrowIfUndefinedFlags here:
        // AppearanceResolver.ResolveBorder constructs a Border on every uncached appearance
        // resolution (see AppearanceStates.ApplyStates for the same reasoning), and the generic
        // helper's Convert.ToUInt64 boxes both operands on every call. The direct bitwise check
        // below costs nothing since BorderSide's operators never box.
        if ((sides & ~BorderSide.All) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sides), sides, "The border sides contain unknown flags.");
        }

        ControlColor.ValidatePaint(foreground, nameof(foreground));
        Sides = sides;
        GlyphStyle = glyphStyle;
        Foreground = foreground;
        EdgeColors = edgeColors;
        Background = background;
        Attributes = attributes;
    }

    /// <summary>Gets the enabled one-cell edges.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The replacement value contains unknown flags.</exception>
    public BorderSide Sides
    {
        get;
        init
        {
            // See the constructor's guard for why this is a direct bitwise check rather than
            // ArgumentOutOfRangeException.ThrowIfUndefinedFlags.
            if ((value & ~BorderSide.All) != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The border sides contain unknown flags.");
            }

            field = value;
        }
    }

    /// <summary>Gets the complete border glyph family.</summary>
    /// <remarks>A zeroed family - the one <c>default(BorderGlyphStyle)</c> produces - is normalized
    /// to <see cref="BorderGlyphStyle.Default"/> rather than rejected, so a partially-initialized
    /// value still draws.</remarks>
    public BorderGlyphStyle GlyphStyle
    {
        get;
        init => field = value.TopLeft.Value == 0 ? BorderGlyphStyle.Default : value;
    }

    /// <summary>Gets the border foreground.</summary>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public ControlColor Foreground
    {
        get;
        init
        {
            ControlColor.ValidatePaint(value, nameof(value));
            field = value;
        }
    }

    /// <summary>Gets optional physical-edge foreground overrides.</summary>
    public BorderEdgeColors EdgeColors { get; init; }

    /// <summary>Gets the border-cell background.</summary>
    public ControlColor Background { get; init; }

    /// <summary>Gets the border attributes.</summary>
    public ControlDecoration Attributes { get; init; }

    IAppearanceFragment IAppearanceFragment.Clone() => this with { };
}
