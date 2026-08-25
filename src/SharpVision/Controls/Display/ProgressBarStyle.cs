// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Display;

using System.Diagnostics.CodeAnalysis;

/// <summary>Defines one complete immutable progress-bar presentation. This style declares no
/// theme section of its own: it falls back to <see cref="ControlStyle"/>'s "control" role section
/// for its passive chrome, resolves its own colors from semantic colors and its glyph family from
/// <see cref="Theme.Glyphs"/>, and is themeable only through that fallback and a locally assigned
/// <see cref="ProgressBar.Style"/>.</summary>
[PublicAPI]
public sealed record ProgressBarStyle: ControlStyle
{
    /// <summary>Gets the primary progress-bar-style definition. Falls back to
    /// <see cref="ControlStyle"/>'s "control" role section; the three colors are code-owned from
    /// semantic colors and the glyph family is code-owned from <see cref="Theme.Glyphs"/>.</summary>
    internal static StyleDefinition<ProgressBarStyle> Definition { get; } = StyleDefinitions.Control(
        static theme => theme.GetStyleSet(ControlStyle.Default),
        Complete,
        static (previous, previousTheme, current, currentTheme) =>
            previous != current ||
            ControlBase.ResolveColor(previous.FillColor, previousTheme) != ControlBase.ResolveColor(current.FillColor, currentTheme) ||
            ControlBase.ResolveColor(previous.TrackColor, previousTheme) != ControlBase.ResolveColor(current.TrackColor, currentTheme) ||
            ControlBase.ResolveColor(previous.IndeterminateColor, previousTheme) != ControlBase.ResolveColor(current.IndeterminateColor, currentTheme)
                ? InvalidationImpact.Render
                : InvalidationImpact.None);

    // Colors are code-owned, not part of a glyph family: FillColor/TrackColor keep every cluster's
    // shared Accent/Muted, and IndeterminateColor is normalized to Info, the majority value across
    // the deleted "progressBar" sections' three-way accent/warning/info split. The glyph trio comes
    // from the active theme's glyph family (see themes.md#glyph-families) rather than a literal
    // hardcoded here - GlyphFamily.Default carries the exact ProgressBarGlyphs.Default this style
    // used to hardcode directly, so an unthemed resolution is unchanged.
    private static ProgressBarStyle Complete(ControlStyle control, VisualState state, Theme theme) =>
        new(control.Face, control.Border, control.Shadow, SemanticColor.Accent, SemanticColor.Muted, SemanticColor.Info, theme.Glyphs.ProgressBar);

    /// <summary>Initializes a complete progress-bar presentation.</summary>
    /// <param name="face">The complete normal face.</param>
    /// <param name="border">The complete normal border.</param>
    /// <param name="shadow">The complete normal shadow.</param>
    /// <param name="fillColor">The non-transparent completed-progress foreground.</param>
    /// <param name="trackColor">The non-transparent incomplete-track foreground.</param>
    /// <param name="indeterminateColor">The non-transparent indeterminate-state foreground.</param>
    /// <param name="glyphs">The complete fill, track, and indeterminate glyph family.</param>
    /// <exception cref="ArgumentException">A part foreground is transparent.</exception>
    [SetsRequiredMembers]
    public ProgressBarStyle(
        Face face,
        Border border,
        Shadow shadow,
        ControlColor fillColor,
        ControlColor trackColor,
        ControlColor indeterminateColor,
        ProgressBarGlyphs glyphs) : base(face, border, shadow)
    {
        FillColor = fillColor;
        TrackColor = trackColor;
        IndeterminateColor = indeterminateColor;
        Glyphs = glyphs;
    }

    /// <summary>Gets the standard progress-bar presentation.</summary>
    public static new ProgressBarStyle Default { get; } = Complete(ControlStyle.Default, VisualState.Normal, Theme.Unthemed);

    /// <summary>Gets the completed-progress foreground.</summary>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public required ControlColor FillColor
    {
        get;
        init
        {
            ControlColor.ValidatePaint(value, nameof(value));
            field = value;
        }
    }

    /// <summary>Gets the incomplete-track foreground.</summary>
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

    /// <summary>Gets the indeterminate-state foreground.</summary>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public required ControlColor IndeterminateColor
    {
        get;
        init
        {
            ControlColor.ValidatePaint(value, nameof(value));
            field = value;
        }
    }

    /// <summary>Gets the complete fill, track, and indeterminate glyph family.</summary>
    public required ProgressBarGlyphs Glyphs { get; init; }
}
