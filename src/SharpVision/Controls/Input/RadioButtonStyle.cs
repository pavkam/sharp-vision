// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

/// <summary>Defines one complete immutable radio-button presentation.</summary>
[PublicAPI]
public readonly struct RadioButtonStyle: IEquatable<RadioButtonStyle>
{
    private static readonly ThemeProfile _standardAppearance = WithCheckedAccent(ControlStyleProfiles.Selection);
    private readonly RadioButtonMarkStyle? _markStyle;
    private readonly RadioButtonGlyphs? _glyphs;
    private readonly ThemeProfile? _appearance;

    /// <summary>Initializes a complete radio-button presentation.</summary>
    /// <param name="markStyle">The validated mark-layout family.</param>
    /// <param name="glyphs">The complete unchecked and checked glyph pair.</param>
    /// <param name="appearance">The complete normal and visual-state appearance profile.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="markStyle"/> is undefined.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="appearance"/> is <see langword="null"/>.</exception>
    public RadioButtonStyle(RadioButtonMarkStyle markStyle, RadioButtonGlyphs glyphs, ThemeProfile appearance)
    {
        EnumValidation.ValidateDefined(markStyle);
        ArgumentNullException.ThrowIfNull(appearance);

        _markStyle = markStyle;
        _glyphs = glyphs;
        _appearance = appearance;
    }

    /// <summary>Gets the standard parenthesized presentation.</summary>
    public static RadioButtonStyle Default => default;

    /// <summary>Gets the exact three-cell <c>( )</c> and <c>(•)</c> presentation.</summary>
    public static RadioButtonStyle Parentheses { get; } = new(
        RadioButtonMarkStyle.Parentheses,
        new RadioButtonGlyphs(new Rune(' '), new Rune('•')),
        _standardAppearance);

    /// <summary>Gets the compact one-cell circle-glyph presentation.</summary>
    public static RadioButtonStyle Glyph { get; } = new(
        RadioButtonMarkStyle.Circle,
        RadioButtonGlyphs.Default,
        _standardAppearance);

    /// <summary>Gets the mark-layout family.</summary>
    public RadioButtonMarkStyle MarkStyle => _markStyle ?? RadioButtonMarkStyle.Parentheses;

    /// <summary>Gets the complete unchecked and checked glyph pair.</summary>
    public RadioButtonGlyphs Glyphs => _glyphs ?? Parentheses.Glyphs;

    /// <summary>Gets the fully formatted unchecked mark.</summary>
    public string UncheckedText => Format(Glyphs.Unchecked);

    /// <summary>Gets the fully formatted checked mark.</summary>
    public string CheckedText => Format(Glyphs.Checked);

    /// <summary>Gets the horizontal terminal-cell reservation for the mark.</summary>
    public int MarkWidth => MarkStyle == RadioButtonMarkStyle.Parentheses ? 3 : 1;

    /// <summary>Gets the complete normal and visual-state appearance profile.</summary>
    public ThemeProfile Appearance => ResolveAppearance();

    /// <summary>Determines whether this value and another style resolve to the same presentation.</summary>
    /// <param name="other">The other style to compare.</param>
    /// <returns><see langword="true"/> when all resolved presentation members are equal.</returns>
    public bool Equals(RadioButtonStyle other) =>
        MarkStyle == other.MarkStyle && Glyphs == other.Glyphs && ProfilesEqual(Appearance, other.Appearance);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is RadioButtonStyle other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var profile = Appearance;
        var hash = new HashCode();
        hash.Add(MarkStyle);
        hash.Add(Glyphs);
        AddProfile(ref hash, profile);
        return hash.ToHashCode();
    }

    /// <summary>Determines whether two radio-button styles resolve to the same presentation.</summary>
    /// <param name="left">The first style.</param>
    /// <param name="right">The second style.</param>
    /// <returns><see langword="true"/> when the styles resolve equally.</returns>
    public static bool operator ==(RadioButtonStyle left, RadioButtonStyle right) => left.Equals(right);

    /// <summary>Determines whether two radio-button styles resolve to different presentations.</summary>
    /// <param name="left">The first style.</param>
    /// <param name="right">The second style.</param>
    /// <returns><see langword="true"/> when the styles resolve differently.</returns>
    public static bool operator !=(RadioButtonStyle left, RadioButtonStyle right) => !left.Equals(right);

    private string Format(Rune glyph) => MarkStyle == RadioButtonMarkStyle.Parentheses
        ? string.Concat("(", glyph.ToString(), ")")
        : glyph.ToString();

    private ThemeProfile ResolveAppearance() => _appearance ?? _standardAppearance;

    /// <summary>Adds the standard semantic checked foreground to one complete profile.</summary>
    /// <param name="profile">The complete profile inherited by a radio-button style.</param>
    /// <returns>A complete profile whose checked state uses the accent foreground.</returns>
    internal static ThemeProfile WithCheckedAccent(ThemeProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var accent = new AppearanceSet(new FaceSet(foreground: ThemeColor.Accent));
        return new ThemeProfile(
            profile.Normal,
            profile.PointerOver,
            profile.FocusWithin,
            profile.Focused,
            profile.Current,
            profile.Selected,
            profile.Checked.Overlay(accent),
            profile.Indeterminate,
            profile.Pressed,
            profile.Disabled);
    }

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
