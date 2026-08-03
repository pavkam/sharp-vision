// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

/// <summary>Defines one complete immutable slider presentation.</summary>
[PublicAPI]
public readonly struct SliderStyle: IEquatable<SliderStyle>
{
    private static readonly ThemeProfile _standardAppearance = ControlStyleProfiles.Control;
    private readonly ColorValue? _fillColor;
    private readonly ColorValue? _trackColor;
    private readonly ColorValue? _thumbColor;
    private readonly SliderGlyphs? _glyphs;
    private readonly ThemeProfile? _appearance;

    /// <summary>Gets the primary slider-style definition.</summary>
    internal static StyleDefinition<SliderStyle> Definition { get; } = StyleDefinitions.Control(
        ThemeRole.Control,
        static profile => new SliderStyle(
            Default.FillColor,
            Default.TrackColor,
            Default.ThumbColor,
            Default.Glyphs,
            profile),
        static style => style.Appearance,
        static (previous, previousTheme, current, currentTheme) =>
            previous != current ||
            ControlBase.ResolveColor(previous.FillColor, previousTheme) != ControlBase.ResolveColor(current.FillColor, currentTheme) ||
            ControlBase.ResolveColor(previous.TrackColor, previousTheme) != ControlBase.ResolveColor(current.TrackColor, currentTheme) ||
            ControlBase.ResolveColor(previous.ThumbColor, previousTheme) != ControlBase.ResolveColor(current.ThumbColor, currentTheme)
                ? InvalidationImpact.Render
                : InvalidationImpact.None);

    /// <summary>Initializes a complete slider presentation.</summary>
    /// <param name="fillColor">The non-transparent filled-rail foreground.</param>
    /// <param name="trackColor">The non-transparent unfilled-rail foreground.</param>
    /// <param name="thumbColor">The non-transparent thumb foreground.</param>
    /// <param name="glyphs">The complete track, fill, and thumb glyph family.</param>
    /// <param name="appearance">The complete normal and visual-state appearance profile.</param>
    /// <exception cref="ArgumentException">A part foreground is transparent.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="appearance"/> is <see langword="null"/>.</exception>
    public SliderStyle(
        ColorValue fillColor,
        ColorValue trackColor,
        ColorValue thumbColor,
        SliderGlyphs glyphs,
        ThemeProfile appearance)
    {
        ColorValue.ValidatePaint(fillColor, nameof(fillColor));
        ColorValue.ValidatePaint(trackColor, nameof(trackColor));
        ColorValue.ValidatePaint(thumbColor, nameof(thumbColor));
        ArgumentNullException.ThrowIfNull(appearance);

        _fillColor = fillColor;
        _trackColor = trackColor;
        _thumbColor = thumbColor;
        _glyphs = glyphs;
        _appearance = appearance;
    }

    /// <summary>Gets the standard slider presentation.</summary>
    public static SliderStyle Default => default;

    /// <summary>Gets the filled-rail foreground.</summary>
    public ColorValue FillColor => _fillColor ?? ThemeColor.Accent;

    /// <summary>Gets the unfilled-rail foreground.</summary>
    public ColorValue TrackColor => _trackColor ?? ThemeColor.Muted;

    /// <summary>Gets the thumb foreground.</summary>
    public ColorValue ThumbColor => _thumbColor ?? ThemeColor.Accent;

    /// <summary>Gets the complete track, fill, and thumb glyph family.</summary>
    public SliderGlyphs Glyphs => _glyphs ?? SliderGlyphs.Default;

    /// <summary>Gets the complete normal and visual-state appearance profile.</summary>
    public ThemeProfile Appearance => ResolveAppearance();

    /// <summary>Creates a validated copy with selected members replaced.</summary>
    /// <param name="fillColor">The optional replacement fill foreground.</param>
    /// <param name="trackColor">The optional replacement track foreground.</param>
    /// <param name="thumbColor">The optional replacement thumb foreground.</param>
    /// <param name="glyphs">The optional replacement glyph family.</param>
    /// <param name="appearance">The optional appearance-profile overlay.</param>
    /// <returns>A complete validated slider style.</returns>
    /// <exception cref="ArgumentException">A composed glyph or foreground is invalid.</exception>
    public SliderStyle With(
        ColorValue? fillColor = null,
        ColorValue? trackColor = null,
        ColorValue? thumbColor = null,
        SliderGlyphs? glyphs = null,
        AppearanceProfileSet? appearance = null) => new(
        fillColor ?? FillColor,
        trackColor ?? TrackColor,
        thumbColor ?? ThumbColor,
        glyphs ?? Glyphs,
        appearance is null ? Appearance : StyleResolution.Apply(Appearance, appearance.Value));

    /// <summary>Determines whether this value and another style resolve to the same presentation.</summary>
    /// <param name="other">The other style to compare.</param>
    /// <returns><see langword="true"/> when all resolved presentation members are equal.</returns>
    public bool Equals(SliderStyle other) =>
        FillColor == other.FillColor &&
        TrackColor == other.TrackColor &&
        ThumbColor == other.ThumbColor &&
        Glyphs == other.Glyphs &&
        Appearance.Equals(other.Appearance);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is SliderStyle other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(FillColor);
        hash.Add(TrackColor);
        hash.Add(ThumbColor);
        hash.Add(Glyphs);
        hash.Add(Appearance);
        return hash.ToHashCode();
    }

    /// <summary>Determines whether two slider styles resolve to the same presentation.</summary>
    /// <param name="left">The first style.</param>
    /// <param name="right">The second style.</param>
    /// <returns><see langword="true"/> when the styles resolve equally.</returns>
    public static bool operator ==(SliderStyle left, SliderStyle right) => left.Equals(right);

    /// <summary>Determines whether two slider styles resolve to different presentations.</summary>
    /// <param name="left">The first style.</param>
    /// <param name="right">The second style.</param>
    /// <returns><see langword="true"/> when the styles resolve differently.</returns>
    public static bool operator !=(SliderStyle left, SliderStyle right) => !left.Equals(right);

    private ThemeProfile ResolveAppearance() => _appearance ?? _standardAppearance;

}
