// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

/// <summary>Describes the presentation ColorPicker applies to its heterogeneous retained parts.</summary>
[PublicAPI]
public readonly struct ColorPickerStyle: IEquatable<ColorPickerStyle>
{
    /// <summary>Gets the secondary aggregate definition.</summary>
    internal static StyleDefinition<ColorPickerStyle> Definition { get; } = StyleDefinitions.Part(
        static theme => new ColorPickerStyle(
            Input.SliderStyle.Definition.Resolve(null, theme),
            DefaultStatusFace),
        static (previous, _, current, _) => previous == current
            ? InvalidationImpact.None
            : InvalidationImpact.Measure);

    /// <summary>Gets the default status-face mechanics before value-dependent contrast is applied.</summary>
    internal static Face DefaultStatusFace { get; } = new(
        ThemeColor.ControlText,
        Color.Transparent,
        ThemeDecoration.NormalText,
        Underline.None,
        Color.Default);

    /// <summary>Initializes an aggregate style with optional per-part contributions.</summary>
    /// <param name="sliderStyle">The style forwarded to every retained Slider, or null for semantic ownership.</param>
    /// <param name="statusFace">The status mechanics, or null for the library fallback.</param>
    public ColorPickerStyle(SliderStyle? sliderStyle, Face? statusFace)
    {
        SliderStyle = sliderStyle;
        StatusFace = statusFace;
    }

    /// <summary>Gets the Slider contribution, or null when the retained Sliders own semantic resolution.</summary>
    public SliderStyle? SliderStyle { get; }

    /// <summary>Gets the status face contribution, or null for the library fallback.</summary>
    /// <remarks>The foreground is ignored because ColorPicker recomputes contrast from its value.</remarks>
    public Face? StatusFace { get; }

    /// <summary>Determines whether two aggregate styles have equal contributions.</summary>
    /// <param name="other">The other style.</param>
    /// <returns>True when both contributions are equal.</returns>
    public bool Equals(ColorPickerStyle other) =>
        SliderStyle == other.SliderStyle && StatusFace == other.StatusFace;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is ColorPickerStyle other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(SliderStyle, StatusFace);

    /// <summary>Determines whether two aggregate styles are equal.</summary>
    public static bool operator ==(ColorPickerStyle left, ColorPickerStyle right) => left.Equals(right);

    /// <summary>Determines whether two aggregate styles differ.</summary>
    public static bool operator !=(ColorPickerStyle left, ColorPickerStyle right) => !left.Equals(right);
}
