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

    /// <summary>Determines whether this value and another style resolve to the same presentation.</summary>
    /// <param name="other">The other style to compare.</param>
    /// <returns><see langword="true"/> when all resolved presentation members are equal.</returns>
    public bool Equals(SliderStyle other) =>
        FillColor == other.FillColor &&
        TrackColor == other.TrackColor &&
        ThumbColor == other.ThumbColor &&
        Glyphs == other.Glyphs &&
        ProfilesEqual(Appearance, other.Appearance);

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
        AddProfile(ref hash, Appearance);
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

    private static bool ProfilesEqual(ThemeProfile left, ThemeProfile right) =>
        ReferenceEquals(left, right) ||
        (left.Normal.Equals(right.Normal) &&
         left.PointerOver.Equals(right.PointerOver) &&
         left.FocusWithin.Equals(right.FocusWithin) &&
         left.Focused.Equals(right.Focused) &&
         left.Current.Equals(right.Current) &&
         left.Selected.Equals(right.Selected) &&
         left.Checked.Equals(right.Checked) &&
         left.Indeterminate.Equals(right.Indeterminate) &&
         left.Pressed.Equals(right.Pressed) &&
         left.Disabled.Equals(right.Disabled));

    private static void AddProfile(ref HashCode hash, ThemeProfile profile)
    {
        hash.Add(profile.Normal);
        hash.Add(profile.PointerOver);
        hash.Add(profile.FocusWithin);
        hash.Add(profile.Focused);
        hash.Add(profile.Current);
        hash.Add(profile.Selected);
        hash.Add(profile.Checked);
        hash.Add(profile.Indeterminate);
        hash.Add(profile.Pressed);
        hash.Add(profile.Disabled);
    }
}
