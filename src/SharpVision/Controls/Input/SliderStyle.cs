// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

using System.Diagnostics.CodeAnalysis;

/// <summary>Defines one complete immutable slider presentation. This style's own
/// "slider" theme key falls back to the standard borderless interactive appearance.</summary>
[PublicAPI]
public sealed record SliderStyle: ControlStyle
{
    /// <summary>Gets the primary slider-style definition. A theme's own "slider" key can restyle
    /// the three colors or the glyph family directly - the standalone registrable "slider" style
    /// section this used to read separately is retired; the reflective Overlay mechanism
    /// reaches every member of this type uniformly.</summary>
    internal static StyleDefinition<SliderStyle> Definition { get; } = StyleDefinitions.Control(
        static theme => theme.GetInteractiveControlStyleSet(),
        Complete,
        static (previous, previousTheme, current, currentTheme) =>
            previous != current ||
            ControlBase.ResolveColor(previous.FillColor, previousTheme) != ControlBase.ResolveColor(current.FillColor, currentTheme) ||
            ControlBase.ResolveColor(previous.TrackColor, previousTheme) != ControlBase.ResolveColor(current.TrackColor, currentTheme) ||
            ControlBase.ResolveColor(previous.ThumbColor, previousTheme) != ControlBase.ResolveColor(current.ThumbColor, currentTheme)
                ? InvalidationImpact.Render
                : InvalidationImpact.None);

    private static SliderStyle Complete(ControlStyle control, VisualState state, Theme theme) =>
        new(control.Face, control.Border, control.Shadow, SemanticColor.Accent, SemanticColor.Muted, SemanticColor.Accent, SliderGlyphs.Default);

    /// <summary>Initializes a complete slider presentation.</summary>
    /// <param name="face">The complete normal face.</param>
    /// <param name="border">The complete normal border.</param>
    /// <param name="shadow">The complete normal shadow.</param>
    /// <param name="fillColor">The non-transparent filled-rail foreground.</param>
    /// <param name="trackColor">The non-transparent unfilled-rail foreground.</param>
    /// <param name="thumbColor">The non-transparent thumb foreground.</param>
    /// <param name="glyphs">The complete track, fill, and thumb glyph family.</param>
    /// <exception cref="ArgumentException">A part foreground is transparent.</exception>
    [SetsRequiredMembers]
    public SliderStyle(
        Face face,
        Border border,
        Shadow shadow,
        ControlColor fillColor,
        ControlColor trackColor,
        ControlColor thumbColor,
        SliderGlyphs glyphs) : base(face, border, shadow)
    {
        FillColor = fillColor;
        TrackColor = trackColor;
        ThumbColor = thumbColor;
        Glyphs = glyphs;
    }

    /// <summary>Gets the standard slider presentation.</summary>
    public static new SliderStyle Default { get; } = Complete(ControlStyle.Default, VisualState.Normal, Theme.Unthemed);

    /// <summary>Gets the filled-rail foreground.</summary>
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

    /// <summary>Gets the unfilled-rail foreground.</summary>
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

    /// <summary>Gets the thumb foreground.</summary>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public required ControlColor ThumbColor
    {
        get;
        init
        {
            ControlColor.ValidatePaint(value, nameof(value));
            field = value;
        }
    }

    /// <summary>Gets the complete track, fill, and thumb glyph family.</summary>
    public required SliderGlyphs Glyphs { get; init; }
}
