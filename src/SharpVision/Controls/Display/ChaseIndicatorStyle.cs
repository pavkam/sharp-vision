// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Display;

using System.Diagnostics.CodeAnalysis;

/// <summary>Defines one complete immutable chase-indicator presentation. This style's
/// own "chaseIndicator" theme key falls back to <see cref="ControlStyle"/>'s "control"
/// key.</summary>
[PublicAPI]
public sealed record ChaseIndicatorStyle: ControlStyle
{
    /// <summary>Gets the primary chase-indicator-style definition. A theme's own "chaseIndicator"
    /// key can restyle the three colors or the glyph family directly - the standalone registrable
    /// "chaseIndicator" style section this used to read separately is retired; the reflective
    /// Overlay reaches every member of this type uniformly.</summary>
    internal static StyleDefinition<ChaseIndicatorStyle> Definition { get; } = StyleDefinitions.Control(
        static theme => theme.GetStyleSet(ControlStyle.Default),
        Complete,
        static (previous, previousTheme, current, currentTheme) =>
            previous != current ||
            ControlBase.ResolveColor(previous.HeadColor, previousTheme) != ControlBase.ResolveColor(current.HeadColor, currentTheme) ||
            ControlBase.ResolveColor(previous.TrailColor, previousTheme) != ControlBase.ResolveColor(current.TrailColor, currentTheme) ||
            ControlBase.ResolveColor(previous.TrackColor, previousTheme) != ControlBase.ResolveColor(current.TrackColor, currentTheme)
                ? InvalidationImpact.Render
                : InvalidationImpact.None);

    // The active/inactive glyph pair comes from the active theme's glyph family (see
    // themes.md#glyph-families) rather than a literal hardcoded here - GlyphFamily.Default carries
    // the exact filled/hollow circle pair this style used to hardcode directly, so an unthemed
    // resolution is unchanged.
    private static ChaseIndicatorStyle Complete(ControlStyle control, VisualState state, Theme theme) =>
        new(
            control.Face,
            control.Border,
            control.Shadow,
            SemanticColor.Accent,
            SemanticColor.Muted,
            SemanticColor.Muted,
            theme.Glyphs.ChaseIndicator);

    /// <summary>Initializes a complete chase-indicator presentation.</summary>
    /// <param name="face">The complete normal face.</param>
    /// <param name="border">The complete normal border.</param>
    /// <param name="shadow">The complete normal shadow.</param>
    /// <param name="headColor">The non-transparent foreground of every current head position.</param>
    /// <param name="trailColor">The non-transparent foreground endpoint of the oldest retained trail frame.</param>
    /// <param name="trackColor">The non-transparent foreground of inactive positions.</param>
    /// <param name="glyphs">The complete active and inactive position glyph pair.</param>
    /// <exception cref="ArgumentException">A glyph is a control or is not one cell wide, or a color is transparent.</exception>
    [SetsRequiredMembers]
    public ChaseIndicatorStyle(
        Face face,
        Border border,
        Shadow shadow,
        ControlColor headColor,
        ControlColor trailColor,
        ControlColor trackColor,
        ChaseIndicatorGlyphs glyphs) : base(face, border, shadow)
    {
        HeadColor = headColor;
        TrailColor = trailColor;
        TrackColor = trackColor;
        Glyphs = glyphs;
    }

    /// <summary>Gets the standard circle presentation.</summary>
    public static new ChaseIndicatorStyle Default => Circle;

    /// <summary>Gets the filled and hollow circle presentation.</summary>
    public static ChaseIndicatorStyle Circle { get; } = Complete(ControlStyle.Default, VisualState.Normal, Theme.Unthemed);

    /// <summary>Gets the filled and hollow diamond presentation.</summary>
    public static ChaseIndicatorStyle Diamond { get; } = Circle with { Glyphs = new ChaseIndicatorGlyphs(new Rune('◆'), new Rune('◇')) };

    /// <summary>Gets the filled and hollow square presentation.</summary>
    public static ChaseIndicatorStyle Square { get; } = Circle with { Glyphs = new ChaseIndicatorGlyphs(new Rune('■'), new Rune('□')) };

    /// <summary>Gets the filled and hollow upward-triangle presentation.</summary>
    public static ChaseIndicatorStyle Up { get; } = Circle with { Glyphs = new ChaseIndicatorGlyphs(new Rune('▲'), new Rune('△')) };

    /// <summary>Gets the filled and hollow downward-triangle presentation.</summary>
    public static ChaseIndicatorStyle Down { get; } = Circle with { Glyphs = new ChaseIndicatorGlyphs(new Rune('▼'), new Rune('▽')) };

    /// <summary>Gets the filled and hollow left-triangle presentation.</summary>
    public static ChaseIndicatorStyle Left { get; } = Circle with { Glyphs = new ChaseIndicatorGlyphs(new Rune('◀'), new Rune('◁')) };

    /// <summary>Gets the filled and hollow right-triangle presentation.</summary>
    public static ChaseIndicatorStyle Right { get; } = Circle with { Glyphs = new ChaseIndicatorGlyphs(new Rune('▶'), new Rune('▷')) };

    /// <summary>Gets the foreground of every current head position.</summary>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public required ControlColor HeadColor
    {
        get;
        init
        {
            ControlColor.ValidatePaint(value, nameof(value));
            field = value;
        }
    }

    /// <summary>Gets the foreground endpoint of the oldest retained trail frame.</summary>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public required ControlColor TrailColor
    {
        get;
        init
        {
            ControlColor.ValidatePaint(value, nameof(value));
            field = value;
        }
    }

    /// <summary>Gets the foreground of inactive positions.</summary>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public required ControlColor TrackColor
    {
        get;
        init
        {
            ControlColor.ValidatePaint(value, nameof(value));
            field = value;
        }
    }

    /// <summary>Gets the complete active and inactive position glyph pair.</summary>
    public required ChaseIndicatorGlyphs Glyphs { get; init; }
}
