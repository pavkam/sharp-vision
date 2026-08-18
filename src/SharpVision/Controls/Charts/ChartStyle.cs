// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Charts;

using System.Diagnostics.CodeAnalysis;

/// <summary>Defines one complete immutable chart presentation and fallback palette.</summary>
[PublicAPI]
public sealed record ChartStyle: ControlStyle
{
    /// <summary>Gets the primary chart-style definition whose <c>chart</c> theme key falls back
    /// to the passive <c>control</c> style.</summary>
    internal static StyleDefinition<ChartStyle> Definition { get; } = StyleDefinitions.Control(
        static theme => theme.GetStyleSet(ControlStyle.Default),
        Complete,
        static (previous, previousTheme, current, currentTheme) =>
            previous != current ||
            ControlBase.ResolveColor(previous.AxisColor, previousTheme) !=
            ControlBase.ResolveColor(current.AxisColor, currentTheme) ||
            ControlBase.ResolveColor(previous.LabelColor, previousTheme) !=
            ControlBase.ResolveColor(current.LabelColor, currentTheme) ||
            ControlBase.ResolveColor(previous.PrimaryColor, previousTheme) !=
            ControlBase.ResolveColor(current.PrimaryColor, currentTheme) ||
            ControlBase.ResolveColor(previous.SecondaryColor, previousTheme) !=
            ControlBase.ResolveColor(current.SecondaryColor, currentTheme) ||
            ControlBase.ResolveColor(previous.TertiaryColor, previousTheme) !=
            ControlBase.ResolveColor(current.TertiaryColor, currentTheme)
                ? InvalidationImpact.Render
                : InvalidationImpact.None);

    /// <summary>Initializes a complete chart presentation.</summary>
    /// <param name="face">The complete normal face.</param>
    /// <param name="border">The complete normal border.</param>
    /// <param name="shadow">The complete normal shadow.</param>
    /// <param name="axisColor">The non-transparent axis foreground.</param>
    /// <param name="labelColor">The non-transparent label foreground.</param>
    /// <param name="primaryColor">The first non-transparent fallback series foreground.</param>
    /// <param name="secondaryColor">The second non-transparent fallback series foreground.</param>
    /// <param name="tertiaryColor">The third non-transparent fallback series foreground.</param>
    /// <param name="glyphs">The complete narrow chart glyph family.</param>
    /// <exception cref="ArgumentException">A color is transparent.</exception>
    [SetsRequiredMembers]
    public ChartStyle(
        Face face,
        Border border,
        Shadow shadow,
        ControlColor axisColor,
        ControlColor labelColor,
        ControlColor primaryColor,
        ControlColor secondaryColor,
        ControlColor tertiaryColor,
        ChartGlyphs glyphs) : base(face, border, shadow)
    {
        AxisColor = axisColor;
        LabelColor = labelColor;
        PrimaryColor = primaryColor;
        SecondaryColor = secondaryColor;
        TertiaryColor = tertiaryColor;
        Glyphs = glyphs;
    }

    /// <summary>Gets the code-owned default presentation.</summary>
    public static new ChartStyle Default { get; } = Complete(ControlStyle.Default, VisualState.Normal);

    /// <summary>Gets the axis foreground.</summary>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public required ControlColor AxisColor
    {
        get;
        init
        {
            ControlColor.ValidatePaint(value, nameof(value));
            field = value;
        }
    }

    /// <summary>Gets the label foreground.</summary>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public required ControlColor LabelColor
    {
        get;
        init
        {
            ControlColor.ValidatePaint(value, nameof(value));
            field = value;
        }
    }

    /// <summary>Gets the first fallback series foreground.</summary>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public required ControlColor PrimaryColor
    {
        get;
        init
        {
            ControlColor.ValidatePaint(value, nameof(value));
            field = value;
        }
    }

    /// <summary>Gets the second fallback series foreground.</summary>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public required ControlColor SecondaryColor
    {
        get;
        init
        {
            ControlColor.ValidatePaint(value, nameof(value));
            field = value;
        }
    }

    /// <summary>Gets the third fallback series foreground.</summary>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public required ControlColor TertiaryColor
    {
        get;
        init
        {
            ControlColor.ValidatePaint(value, nameof(value));
            field = value;
        }
    }

    /// <summary>Gets the complete chart glyph family.</summary>
    public required ChartGlyphs Glyphs
    {
        get;
        init;
    }

    /// <summary>Gets how bar and area extents rasterize into cells.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The replacement value is unknown.</exception>
    public ChartFillMode FillMode
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNotDefined(value, nameof(value), "The fill mode is unknown.");

            field = value;
        }
    } = ChartFillMode.Fractional;

    /// <summary>Gets how line-series segments rasterize into cells.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The replacement value is unknown.</exception>
    public ChartLineMode LineMode
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNotDefined(value, nameof(value), "The line mode is unknown.");

            field = value;
        }
    } = ChartLineMode.Quadrant;

    /// <summary>Gets the default dash pattern a series without its own override draws with when
    /// the chart draws through the quadrant line renderer.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The replacement value is unknown.</exception>
    public LinePattern LinePattern
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNotDefined(value, nameof(value), "The line pattern is unknown.");

            field = value;
        }
    } = LinePattern.Solid;

    /// <summary>Gets one deterministic fallback series foreground by zero-based index.</summary>
    internal ControlColor GetSeriesColor(int index) => Math.Abs(index % 3) switch
    {
        0 => PrimaryColor,
        1 => SecondaryColor,
        _ => TertiaryColor
    };

    private static ChartStyle Complete(ControlStyle control, VisualState state)
    {
        _ = state;
        return new ChartStyle(
            control.Face,
            control.Border,
            control.Shadow,
            SemanticColor.Muted,
            SemanticColor.ControlText,
            SemanticColor.Accent,
            SemanticColor.Success,
            SemanticColor.Warning,
            ChartGlyphs.Default);
    }
}
