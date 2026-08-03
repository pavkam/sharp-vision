// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

/// <summary>Defines one complete immutable checkbox presentation.</summary>
[PublicAPI]
public readonly struct CheckBoxStyle: IEquatable<CheckBoxStyle>
{
    private static readonly ThemeProfile _standardAppearance = ControlStyleProfiles.Selection;
    private readonly CheckBoxMarkStyle? _markStyle;
    private readonly CheckBoxGlyphs? _glyphs;
    private readonly ThemeProfile? _appearance;

    /// <summary>Gets the primary checkbox-style definition. Reads the theme's registrable
    /// "checkBox" style section (see #155) for MarkStyle when the active theme authors one;
    /// falls back to the code-owned structural default otherwise.</summary>
    internal static StyleDefinition<CheckBoxStyle> Definition { get; } = new(
        ThemeRole.Input,
        ResolveComplete,
        static style => style.Appearance,
        Compare);

    private static CheckBoxStyle ResolveComplete(CheckBoxStyle? local, Theme? theme)
    {
        if (local.HasValue)
        {
            return local.Value;
        }

        var effectiveTheme = theme ?? Themes.Dark;
        var section = effectiveTheme.GetStyleSection<CheckBoxStyleSection>("checkBox");

        return new CheckBoxStyle(
            ParseMarkStyle(section?.MarkStyle, effectiveTheme.Slug) ?? Default.MarkStyle,
            Default.Glyphs,
            effectiveTheme.GetProfile(ThemeRole.Input).WithoutChrome());
    }

    private static CheckBoxMarkStyle? ParseMarkStyle(string? value, string themeSlug) => value is null
        ? null
        : Enum.TryParse<CheckBoxMarkStyle>(value, ignoreCase: true, out var markStyle) && Enum.IsDefined(markStyle)
            ? markStyle
            : throw new InvalidDataException(
                $"Theme '{themeSlug}' styles.checkBox.markStyle has unknown value '{value}'.");

    private static InvalidationImpact Compare(
        CheckBoxStyle previous,
        Theme? previousTheme,
        CheckBoxStyle current,
        Theme? currentTheme) =>
        previous.MarkWidth != current.MarkWidth
            ? InvalidationImpact.Measure
            : previous.MarkStyle != current.MarkStyle || previous.Glyphs != current.Glyphs
                ? InvalidationImpact.Render
                : InvalidationImpact.None;

    /// <summary>Gets the non-invalidating definition used by pure forwarding hosts.</summary>
    internal static StyleDefinition<CheckBoxStyle> ForwardingDefinition { get; } = StyleDefinitions.Part(
        static theme => Definition.Resolve(null, theme),
        static (_, _, _, _) => InvalidationImpact.None);

    /// <summary>Initializes a complete checkbox presentation.</summary>
    /// <param name="markStyle">The validated mark-layout family.</param>
    /// <param name="glyphs">The complete unchecked, checked, and indeterminate glyph family.</param>
    /// <param name="appearance">The complete normal and visual-state appearance profile.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="markStyle"/> is undefined.</exception>
    /// <exception cref="ArgumentException">A glyph is a control or is not one cell wide.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="appearance"/> is <see langword="null"/>.</exception>
    public CheckBoxStyle(CheckBoxMarkStyle markStyle, CheckBoxGlyphs glyphs, ThemeProfile appearance)
    {
        markStyle.ValidateDefined();
        var validatedGlyphs = new CheckBoxGlyphs(glyphs.Unchecked, glyphs.Checked, glyphs.Indeterminate);
        ArgumentNullException.ThrowIfNull(appearance);

        _markStyle = markStyle;
        _glyphs = validatedGlyphs;
        _appearance = appearance;
    }

    /// <summary>Gets the standard three-cell bracket presentation.</summary>
    public static CheckBoxStyle Default => default;

    /// <summary>Gets the three-cell bracket presentation.</summary>
    public static CheckBoxStyle Brackets { get; } = new(
        CheckBoxMarkStyle.Brackets,
        new CheckBoxGlyphs(new Rune(' '), new Rune('✓'), new Rune('─')),
        _standardAppearance);

    /// <summary>Gets the one-cell circle, tick, and indeterminate presentation.</summary>
    public static CheckBoxStyle Tick { get; } = new(
        CheckBoxMarkStyle.Tick,
        new CheckBoxGlyphs(new Rune('○'), new Rune('✓'), new Rune('−')),
        _standardAppearance);

    /// <summary>Gets the one-cell square-state presentation.</summary>
    public static CheckBoxStyle Square { get; } = new(
        CheckBoxMarkStyle.Square,
        CheckBoxGlyphs.Default,
        _standardAppearance);

    /// <summary>Gets the mark-layout family.</summary>
    public CheckBoxMarkStyle MarkStyle => _markStyle ?? CheckBoxMarkStyle.Brackets;

    /// <summary>Gets the complete state glyph family.</summary>
    public CheckBoxGlyphs Glyphs => _glyphs ?? Brackets.Glyphs;

    /// <summary>Gets the horizontal terminal-cell reservation for the mark.</summary>
    public int MarkWidth => MarkStyle == CheckBoxMarkStyle.Brackets ? 3 : 1;

    /// <summary>Gets the complete normal and visual-state appearance profile.</summary>
    public ThemeProfile Appearance => ResolveAppearance();

    /// <summary>Creates a validated copy with selected members replaced.</summary>
    /// <param name="markStyle">The optional replacement mark-layout family.</param>
    /// <param name="glyphs">The optional replacement glyph family.</param>
    /// <param name="appearance">The optional appearance-profile overlay.</param>
    /// <returns>A complete validated checkbox style.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="markStyle"/> is undefined.</exception>
    /// <exception cref="ArgumentException">A composed glyph is invalid.</exception>
    public CheckBoxStyle With(
        CheckBoxMarkStyle? markStyle = null,
        CheckBoxGlyphs? glyphs = null,
        AppearanceProfileSet? appearance = null) => new(
        markStyle ?? MarkStyle,
        glyphs ?? Glyphs,
        appearance is null ? Appearance : StyleResolution.Apply(Appearance, appearance.Value));

    /// <summary>Determines whether this value and another style resolve to the same presentation.</summary>
    /// <param name="other">The other style to compare.</param>
    /// <returns><see langword="true"/> when all resolved presentation members are equal.</returns>
    public bool Equals(CheckBoxStyle other) =>
        MarkStyle == other.MarkStyle && Glyphs == other.Glyphs && Appearance.Equals(other.Appearance);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is CheckBoxStyle other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(MarkStyle, Glyphs, Appearance);

    /// <summary>Determines whether two checkbox styles resolve to the same presentation.</summary>
    /// <param name="left">The first style.</param>
    /// <param name="right">The second style.</param>
    /// <returns><see langword="true"/> when the styles resolve equally.</returns>
    public static bool operator ==(CheckBoxStyle left, CheckBoxStyle right) => left.Equals(right);

    /// <summary>Determines whether two checkbox styles resolve to different presentations.</summary>
    /// <param name="left">The first style.</param>
    /// <param name="right">The second style.</param>
    /// <returns><see langword="true"/> when the styles resolve differently.</returns>
    public static bool operator !=(CheckBoxStyle left, CheckBoxStyle right) => !left.Equals(right);

    private ThemeProfile ResolveAppearance() => _appearance ?? _standardAppearance;
}
