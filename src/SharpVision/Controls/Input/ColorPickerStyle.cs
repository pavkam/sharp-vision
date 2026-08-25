// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

using System.Diagnostics.CodeAnalysis;

/// <summary>Describes the presentation ColorPicker applies to its heterogeneous retained parts.
/// This is a secondary (part) style, not a primary themed control style. It owns no
/// <c>styles.*</c> key - the theme catalog skips a style type whose own Definition is secondary,
/// so a document naming this type's derived key is rejected rather than accepted and ignored. Its
/// inherited Face/Border/Shadow (satisfying the shared <see cref="ControlStyle"/> constraint every
/// style type needs) are vestigial and never consulted; the real content is
/// <see cref="SliderStyle"/>/<see cref="StatusFace"/>/<see cref="SelectedMarker"/>.</summary>
[PublicAPI]
public sealed record ColorPickerStyle: ControlStyle
{
    /// <summary>Gets the secondary aggregate definition.</summary>
    internal static StyleDefinition<ColorPickerStyle> Definition { get; } = StyleDefinitions.Aggregate(
        static theme => new ColorPickerStyle(
            DefaultFace,
            NoBorder,
            NoShadow,
            SliderStyle.Definition.Resolve(null, theme),
            DefaultStatusFace,
            null),
        static (previous, _, current, _) => previous == current
            ? InvalidationImpact.None
            : InvalidationImpact.Measure);

    /// <summary>Gets the default status-face mechanics before value-dependent contrast is applied.</summary>
    internal static Face DefaultStatusFace { get; } = new(
        SemanticColor.ControlText,
        Color.Transparent,
        SemanticDecoration.NormalText,
        Underline.None,
        Color.Default);

    /// <summary>Initializes an aggregate style with optional per-part contributions.</summary>
    /// <param name="face">The vestigial normal face (this type owns no themed appearance of its own).</param>
    /// <param name="border">The vestigial normal border.</param>
    /// <param name="shadow">The vestigial normal shadow.</param>
    /// <param name="sliderStyle">The style forwarded to every retained Slider, or null for semantic ownership.</param>
    /// <param name="statusFace">The status mechanics, or null for the library fallback.</param>
    /// <param name="selectedMarker">The marker forwarded to the retained ColorPlane's currently-selected-
    /// coordinate indicator, or null for the library default.</param>
    [SetsRequiredMembers]
    public ColorPickerStyle(Face face, Border border, Shadow shadow, SliderStyle? sliderStyle, Face? statusFace, Rune? selectedMarker) : base(face, border, shadow)
    {
        SliderStyle = sliderStyle;
        StatusFace = statusFace;
        SelectedMarker = selectedMarker;
    }

    /// <summary>Gets the marker forwarded to the retained ColorPlane's currently-selected-coordinate
    /// indicator, or null for the library default.</summary>
    public Rune? SelectedMarker { get; init; }

    /// <summary>Gets the Slider contribution, or null when the retained Sliders own semantic resolution.</summary>
    public SliderStyle? SliderStyle { get; init; }

    /// <summary>Gets the status face contribution, or null for the library fallback.</summary>
    /// <remarks>The foreground is ignored because ColorPicker recomputes contrast from its value.</remarks>
    public Face? StatusFace { get; init; }
}
