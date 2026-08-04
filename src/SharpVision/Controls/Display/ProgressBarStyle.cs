// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Display;

/// <summary>Defines one complete immutable progress-bar presentation.</summary>
[PublicAPI]
public readonly struct ProgressBarStyle: IEquatable<ProgressBarStyle>
{
    private static readonly ThemeProfile _standardAppearance = ControlStyleProfiles.Control;
    private readonly ColorValue? _fillColor;
    private readonly ColorValue? _trackColor;
    private readonly ColorValue? _indeterminateColor;
    private readonly ProgressBarGlyphs? _glyphs;
    private readonly ThemeProfile? _appearance;

    /// <summary>Gets the primary progress-bar-style definition. Reads the theme's registrable
    /// "progressBar" style section (see #155) for the three colors when the active theme authors
    /// one; falls back to the code-owned structural defaults otherwise.</summary>
    internal static StyleDefinition<ProgressBarStyle> Definition { get; } = new(
        ThemeRole.Control,
        ResolveComplete,
        static style => style.Appearance,
        static (previous, previousTheme, current, currentTheme) =>
            previous != current ||
            ControlBase.ResolveColor(previous.FillColor, previousTheme) != ControlBase.ResolveColor(current.FillColor, currentTheme) ||
            ControlBase.ResolveColor(previous.TrackColor, previousTheme) != ControlBase.ResolveColor(current.TrackColor, currentTheme) ||
            ControlBase.ResolveColor(previous.IndeterminateColor, previousTheme) != ControlBase.ResolveColor(current.IndeterminateColor, currentTheme)
                ? InvalidationImpact.Render
                : InvalidationImpact.None);

    private static ProgressBarStyle ResolveComplete(ProgressBarStyle? local, Theme? theme)
    {
        if (local.HasValue)
        {
            return local.Value;
        }

        var effectiveTheme = theme ?? Themes.Dark;
        var section = effectiveTheme.GetStyleSection<ProgressBarStyleSection>("progressBar");

        return new ProgressBarStyle(
            ResolveColor(effectiveTheme, section?.FillColor, "fillColor") ?? Default.FillColor,
            ResolveColor(effectiveTheme, section?.TrackColor, "trackColor") ?? Default.TrackColor,
            ResolveColor(effectiveTheme, section?.IndeterminateColor, "indeterminateColor") ?? Default.IndeterminateColor,
            ParseGlyphs(section?.Glyphs, effectiveTheme.Slug),
            effectiveTheme.GetProfile(ThemeRole.Control));
    }

    private static ColorValue? ResolveColor(Theme theme, string? value, string memberName) => value is null
        ? null
        : theme.ResolveSectionColorValue(value, $"styles.progressBar.{memberName}");

    private static ProgressBarGlyphs ParseGlyphs(ProgressBarGlyphsSection? section, string themeSlug) =>
        section is null
            ? Default.Glyphs
            : new ProgressBarGlyphs(
                ParseGlyph(section.Fill, themeSlug, "fill") ?? Default.Glyphs.Fill,
                ParseGlyph(section.Track, themeSlug, "track") ?? Default.Glyphs.Track,
                ParseGlyph(section.Indeterminate, themeSlug, "indeterminate") ?? Default.Glyphs.Indeterminate);

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
                $"Theme '{themeSlug}' styles.progressBar.glyphs.{memberName} must contain one Rune.");
        }

        var result = enumerator.Current;
        return enumerator.MoveNext()
            ? throw new InvalidDataException(
                $"Theme '{themeSlug}' styles.progressBar.glyphs.{memberName} must contain one Rune.")
            : result;
    }

    /// <summary>Initializes a complete progress-bar presentation.</summary>
    /// <param name="fillColor">The non-transparent completed-progress foreground.</param>
    /// <param name="trackColor">The non-transparent incomplete-track foreground.</param>
    /// <param name="indeterminateColor">The non-transparent indeterminate-state foreground.</param>
    /// <param name="glyphs">The complete fill, track, and indeterminate glyph family.</param>
    /// <param name="appearance">The complete normal and visual-state appearance profile.</param>
    /// <exception cref="ArgumentException">A part foreground is transparent.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="appearance"/> is <see langword="null"/>.</exception>
    public ProgressBarStyle(
        ColorValue fillColor,
        ColorValue trackColor,
        ColorValue indeterminateColor,
        ProgressBarGlyphs glyphs,
        ThemeProfile appearance)
    {
        ColorValue.ValidatePaint(fillColor, nameof(fillColor));
        ColorValue.ValidatePaint(trackColor, nameof(trackColor));
        ColorValue.ValidatePaint(indeterminateColor, nameof(indeterminateColor));
        ArgumentNullException.ThrowIfNull(appearance);

        _fillColor = fillColor;
        _trackColor = trackColor;
        _indeterminateColor = indeterminateColor;
        _glyphs = glyphs;
        _appearance = appearance;
    }

    /// <summary>Gets the standard progress-bar presentation.</summary>
    public static ProgressBarStyle Default => default;

    /// <summary>Gets the completed-progress foreground.</summary>
    public ColorValue FillColor => _fillColor ?? ThemeColor.Accent;

    /// <summary>Gets the incomplete-track foreground.</summary>
    public ColorValue TrackColor => _trackColor ?? ThemeColor.Muted;

    /// <summary>Gets the indeterminate-state foreground.</summary>
    public ColorValue IndeterminateColor => _indeterminateColor ?? ThemeColor.Accent;

    /// <summary>Gets the complete fill, track, and indeterminate glyph family.</summary>
    public ProgressBarGlyphs Glyphs => _glyphs ?? ProgressBarGlyphs.Default;

    /// <summary>Gets the complete normal and visual-state appearance profile.</summary>
    public ThemeProfile Appearance => ResolveAppearance();

    /// <summary>Creates a validated copy with selected members replaced.</summary>
    /// <param name="fillColor">The optional fill foreground.</param>
    /// <param name="trackColor">The optional track foreground.</param>
    /// <param name="indeterminateColor">The optional indeterminate foreground.</param>
    /// <param name="glyphs">The optional glyph family.</param>
    /// <param name="appearance">The optional appearance-profile overlay.</param>
    /// <returns>A complete validated progress-bar style.</returns>
    /// <exception cref="ArgumentException">A composed glyph or foreground is invalid.</exception>
    public ProgressBarStyle With(
        ColorValue? fillColor = null,
        ColorValue? trackColor = null,
        ColorValue? indeterminateColor = null,
        ProgressBarGlyphs? glyphs = null,
        AppearanceProfileSet? appearance = null) => new(
        fillColor ?? FillColor,
        trackColor ?? TrackColor,
        indeterminateColor ?? IndeterminateColor,
        glyphs ?? Glyphs,
        appearance is null ? Appearance : StyleResolution.Apply(Appearance, appearance.Value));

    /// <summary>Determines whether this value and another style resolve to the same presentation.</summary>
    /// <param name="other">The other style to compare.</param>
    /// <returns><see langword="true"/> when all resolved presentation members are equal.</returns>
    public bool Equals(ProgressBarStyle other) =>
        FillColor == other.FillColor &&
        TrackColor == other.TrackColor &&
        IndeterminateColor == other.IndeterminateColor &&
        Glyphs == other.Glyphs &&
        Appearance.Equals(other.Appearance);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is ProgressBarStyle other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(FillColor);
        hash.Add(TrackColor);
        hash.Add(IndeterminateColor);
        hash.Add(Glyphs);
        hash.Add(Appearance);
        return hash.ToHashCode();
    }

    /// <summary>Determines whether two progress-bar styles resolve to the same presentation.</summary>
    /// <param name="left">The first style.</param>
    /// <param name="right">The second style.</param>
    /// <returns><see langword="true"/> when the styles resolve equally.</returns>
    public static bool operator ==(ProgressBarStyle left, ProgressBarStyle right) => left.Equals(right);

    /// <summary>Determines whether two progress-bar styles resolve to different presentations.</summary>
    /// <param name="left">The first style.</param>
    /// <param name="right">The second style.</param>
    /// <returns><see langword="true"/> when the styles resolve differently.</returns>
    public static bool operator !=(ProgressBarStyle left, ProgressBarStyle right) => !left.Equals(right);

    private ThemeProfile ResolveAppearance() => _appearance ?? _standardAppearance;

}
