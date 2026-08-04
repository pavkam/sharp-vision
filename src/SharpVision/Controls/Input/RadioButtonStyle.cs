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

    /// <summary>Gets the primary radio-button-style definition. Reads the theme's registrable
    /// "radioButton" style section (see #155) for MarkStyle when the active theme authors one;
    /// falls back to the code-owned structural default otherwise.</summary>
    internal static StyleDefinition<RadioButtonStyle> Definition { get; } = new(
        ThemeRole.Input,
        ResolveComplete,
        static style => style.Appearance,
        Compare);

    private static RadioButtonStyle ResolveComplete(RadioButtonStyle? local, Theme? theme)
    {
        if (local.HasValue)
        {
            return local.Value;
        }

        var effectiveTheme = theme ?? Themes.Dark;
        var section = effectiveTheme.GetStyleSection<RadioButtonStyleSection>("radioButton");

        return new RadioButtonStyle(
            ParseMarkStyle(section?.MarkStyle, effectiveTheme.Slug) ?? Default.MarkStyle,
            ParseGlyphs(section?.Glyphs, effectiveTheme.Slug),
            WithCheckedAccent(effectiveTheme.GetProfile(ThemeRole.Input).WithoutChrome()));
    }

    private static RadioButtonMarkStyle? ParseMarkStyle(string? value, string themeSlug) => value is null
        ? null
        : Enum.TryParse<RadioButtonMarkStyle>(value, ignoreCase: true, out var markStyle) && Enum.IsDefined(markStyle)
            ? markStyle
            : throw new InvalidDataException(
                $"Theme '{themeSlug}' styles.radioButton.markStyle has unknown value '{value}'.");

    private static RadioButtonGlyphs ParseGlyphs(RadioButtonGlyphsSection? section, string themeSlug) =>
        section is null
            ? Default.Glyphs
            : new RadioButtonGlyphs(
                ParseGlyph(section.Unchecked, themeSlug, "unchecked") ?? Default.Glyphs.Unchecked,
                ParseGlyph(section.Checked, themeSlug, "checked") ?? Default.Glyphs.Checked);

    private static Rune? ParseGlyph(string? value, string themeSlug, string memberName)
    {
        if (value is null)
        {
            return null;
        }

        var enumerator = value.EnumerateRunes();
        if (!enumerator.MoveNext())
        {
            throw new InvalidDataException(
                $"Theme '{themeSlug}' styles.radioButton.glyphs.{memberName} must contain one Rune.");
        }

        var result = enumerator.Current;
        return enumerator.MoveNext()
            ? throw new InvalidDataException(
                $"Theme '{themeSlug}' styles.radioButton.glyphs.{memberName} must contain one Rune.")
            : result;
    }

    private static InvalidationImpact Compare(
        RadioButtonStyle previous,
        Theme? previousTheme,
        RadioButtonStyle current,
        Theme? currentTheme) =>
        previous.MarkWidth != current.MarkWidth
            ? InvalidationImpact.Measure
            : previous.MarkStyle != current.MarkStyle || previous.Glyphs != current.Glyphs
                ? InvalidationImpact.Render
                : InvalidationImpact.None;

    /// <summary>Initializes a complete radio-button presentation.</summary>
    /// <param name="markStyle">The validated mark-layout family.</param>
    /// <param name="glyphs">The complete unchecked and checked glyph pair.</param>
    /// <param name="appearance">The complete normal and visual-state appearance profile.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="markStyle"/> is undefined.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="appearance"/> is <see langword="null"/>.</exception>
    public RadioButtonStyle(RadioButtonMarkStyle markStyle, RadioButtonGlyphs glyphs, ThemeProfile appearance)
    {
        markStyle.ValidateDefined();
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

    /// <summary>Creates a validated copy with selected members replaced.</summary>
    /// <param name="markStyle">The optional replacement mark-layout family.</param>
    /// <param name="glyphs">The optional replacement glyph pair.</param>
    /// <param name="appearance">The optional appearance-profile overlay.</param>
    /// <returns>A complete validated radio-button style.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="markStyle"/> is undefined.</exception>
    /// <exception cref="ArgumentException">A composed glyph is invalid.</exception>
    public RadioButtonStyle With(
        RadioButtonMarkStyle? markStyle = null,
        RadioButtonGlyphs? glyphs = null,
        AppearanceProfileSet? appearance = null) => new(
        markStyle ?? MarkStyle,
        glyphs ?? Glyphs,
        appearance is null ? Appearance : StyleResolution.Apply(Appearance, appearance.Value));

    /// <summary>Determines whether this value and another style resolve to the same presentation.</summary>
    /// <param name="other">The other style to compare.</param>
    /// <returns><see langword="true"/> when all resolved presentation members are equal.</returns>
    public bool Equals(RadioButtonStyle other) =>
        MarkStyle == other.MarkStyle && Glyphs == other.Glyphs && Appearance.Equals(other.Appearance);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is RadioButtonStyle other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var profile = Appearance;
        var hash = new HashCode();
        hash.Add(MarkStyle);
        hash.Add(Glyphs);
        hash.Add(profile);
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

}
