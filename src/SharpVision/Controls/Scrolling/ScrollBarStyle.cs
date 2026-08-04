// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Scrolling;

/// <summary>Defines one complete immutable scrollbar presentation.</summary>
[PublicAPI]
public readonly struct ScrollBarStyle: IEquatable<ScrollBarStyle>
{
    private static readonly ThemeProfile _standardAppearance = ControlStyleProfiles.Control;
    private readonly ScrollBarChrome? _chrome;
    private readonly ScrollBarFill? _fill;
    private readonly ScrollBarGlyphs? _glyphs;
    private readonly ColorValue? _trackColor;
    private readonly ColorValue? _thumbColor;
    private readonly ColorValue? _buttonColor;
    private readonly ThemeProfile? _appearance;

    /// <summary>Gets the primary scrollbar-style definition. Reads the theme's registrable
    /// "scrollBar" style section (see #155) for Chrome/Fill when the active theme authors one;
    /// falls back to the code-owned structural defaults otherwise.</summary>
    internal static StyleDefinition<ScrollBarStyle> Definition { get; } = new(
        ThemeRole.Control,
        ResolveComplete,
        static style => style.Appearance,
        Compare);

    private static ScrollBarStyle ResolveComplete(ScrollBarStyle? local, Theme? theme)
    {
        if (local.HasValue)
        {
            return local.Value;
        }

        var effectiveTheme = theme ?? Themes.Dark;
        var section = effectiveTheme.GetStyleSection<ScrollBarStyleSection>("scrollBar");

        return new ScrollBarStyle(
            ParseChrome(section?.Chrome, effectiveTheme.Slug) ?? Default.Chrome,
            ParseFill(section?.Fill, effectiveTheme.Slug) ?? Default.Fill,
            ParseGlyphs(section?.Glyphs, effectiveTheme.Slug),
            Default.TrackColor,
            Default.ThumbColor,
            Default.ButtonColor,
            effectiveTheme.GetProfile(ThemeRole.Control));
    }

    private static ScrollBarChrome? ParseChrome(string? value, string themeSlug) => value is null
        ? null
        : Enum.TryParse<ScrollBarChrome>(value, ignoreCase: true, out var chrome) && Enum.IsDefined(chrome)
            ? chrome
            : throw new InvalidDataException(
                $"Theme '{themeSlug}' styles.scrollBar.chrome has unknown value '{value}'.");

    private static ScrollBarFill? ParseFill(string? value, string themeSlug) => value is null
        ? null
        : Enum.TryParse<ScrollBarFill>(value, ignoreCase: true, out var fill) && Enum.IsDefined(fill)
            ? fill
            : throw new InvalidDataException(
                $"Theme '{themeSlug}' styles.scrollBar.fill has unknown value '{value}'.");

    private static ScrollBarGlyphs ParseGlyphs(ScrollBarGlyphsSection? section, string themeSlug) =>
        section is null
            ? Default.Glyphs
            : new ScrollBarGlyphs(
                ParseGlyph(section.VerticalDecrement, themeSlug, "verticalDecrement") ?? Default.Glyphs.VerticalDecrement,
                ParseGlyph(section.VerticalIncrement, themeSlug, "verticalIncrement") ?? Default.Glyphs.VerticalIncrement,
                ParseGlyph(section.HorizontalDecrement, themeSlug, "horizontalDecrement") ?? Default.Glyphs.HorizontalDecrement,
                ParseGlyph(section.HorizontalIncrement, themeSlug, "horizontalIncrement") ?? Default.Glyphs.HorizontalIncrement,
                ParseGlyph(section.BlockTrack, themeSlug, "blockTrack") ?? Default.Glyphs.BlockTrack,
                ParseGlyph(section.BlockThumb, themeSlug, "blockThumb") ?? Default.Glyphs.BlockThumb,
                ParseGlyph(section.HorizontalLineTrack, themeSlug, "horizontalLineTrack") ?? Default.Glyphs.HorizontalLineTrack,
                ParseGlyph(section.HorizontalLineThumb, themeSlug, "horizontalLineThumb") ?? Default.Glyphs.HorizontalLineThumb,
                ParseGlyph(section.VerticalLineTrack, themeSlug, "verticalLineTrack") ?? Default.Glyphs.VerticalLineTrack,
                ParseGlyph(section.VerticalLineThumb, themeSlug, "verticalLineThumb") ?? Default.Glyphs.VerticalLineThumb);

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
                $"Theme '{themeSlug}' styles.scrollBar.glyphs.{memberName} must contain one Rune.");
        }

        var result = enumerator.Current;
        return enumerator.MoveNext()
            ? throw new InvalidDataException(
                $"Theme '{themeSlug}' styles.scrollBar.glyphs.{memberName} must contain one Rune.")
            : result;
    }

    /// <summary>Gets the secondary definition used by generated-scrollbar hosts.</summary>
    internal static StyleDefinition<ScrollBarStyle> PartDefinition { get; } = StyleDefinitions.Part(
        static theme => Definition.Resolve(null, theme),
        Compare);

    /// <summary>Gets the non-invalidating definition used by pure forwarding hosts.</summary>
    internal static StyleDefinition<ScrollBarStyle> ForwardingDefinition { get; } = StyleDefinitions.Part(
        static theme => Definition.Resolve(null, theme),
        static (_, _, _, _) => InvalidationImpact.None);

    /// <summary>Gets the definition used by an editor whose rail chrome changes arrangement.</summary>
    internal static StyleDefinition<ScrollBarStyle> ArrangePartDefinition { get; } = StyleDefinitions.Part(
        static theme => Definition.Resolve(null, theme),
        static (previous, previousTheme, current, currentTheme) =>
            previous.Chrome != current.Chrome
                ? InvalidationImpact.Arrange
                : Compare(previous, previousTheme, current, currentTheme));

    /// <summary>Initializes a complete scrollbar presentation.</summary>
    /// <param name="chrome">The compact or full scrollbar chrome.</param>
    /// <param name="fill">The line or block glyph treatment.</param>
    /// <param name="glyphs">The complete button, track, and thumb glyph family.</param>
    /// <param name="trackColor">The non-transparent unoccupied-track foreground.</param>
    /// <param name="thumbColor">The non-transparent thumb foreground.</param>
    /// <param name="buttonColor">The non-transparent directional-button foreground.</param>
    /// <param name="appearance">The complete normal and visual-state appearance profile.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="chrome"/> or <paramref name="fill"/> is undefined.</exception>
    /// <exception cref="ArgumentException">A part foreground is transparent.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="appearance"/> is <see langword="null"/>.</exception>
    public ScrollBarStyle(
        ScrollBarChrome chrome,
        ScrollBarFill fill,
        ScrollBarGlyphs glyphs,
        ColorValue trackColor,
        ColorValue thumbColor,
        ColorValue buttonColor,
        ThemeProfile appearance)
    {
        chrome.ValidateDefined();
        fill.ValidateDefined();
        ColorValue.ValidatePaint(trackColor, nameof(trackColor));
        ColorValue.ValidatePaint(thumbColor, nameof(thumbColor));
        ColorValue.ValidatePaint(buttonColor, nameof(buttonColor));
        ArgumentNullException.ThrowIfNull(appearance);

        _chrome = chrome;
        _fill = fill;
        _glyphs = glyphs;
        _trackColor = trackColor;
        _thumbColor = thumbColor;
        _buttonColor = buttonColor;
        _appearance = appearance;
    }

    /// <summary>Gets the standard full block presentation.</summary>
    public static ScrollBarStyle Default => default;

    /// <summary>Gets the full button-and-track block presentation.</summary>
    public static ScrollBarStyle FullBlock => default;

    /// <summary>Gets the full button-and-track line presentation.</summary>
    public static ScrollBarStyle FullLine { get; } = Create(ScrollBarChrome.Full, ScrollBarFill.Line);

    /// <summary>Gets the compact track-only block presentation.</summary>
    public static ScrollBarStyle ThinBlock { get; } = Create(ScrollBarChrome.Thin, ScrollBarFill.Block);

    /// <summary>Gets the compact track-only line presentation.</summary>
    public static ScrollBarStyle ThinLine { get; } = Create(ScrollBarChrome.Thin, ScrollBarFill.Line);

    /// <summary>Gets the compact or full scrollbar chrome.</summary>
    public ScrollBarChrome Chrome => _chrome ?? ScrollBarChrome.Full;

    /// <summary>Gets the line or block glyph treatment.</summary>
    public ScrollBarFill Fill => _fill ?? ScrollBarFill.Block;

    /// <summary>Gets the complete button, track, and thumb glyph family.</summary>
    public ScrollBarGlyphs Glyphs => _glyphs ?? ScrollBarGlyphs.Default;

    /// <summary>Gets the unoccupied-track foreground.</summary>
    public ColorValue TrackColor => _trackColor ?? ThemeColor.Muted;

    /// <summary>Gets the thumb foreground.</summary>
    public ColorValue ThumbColor => _thumbColor ?? ThemeColor.Accent;

    /// <summary>Gets the directional-button foreground.</summary>
    public ColorValue ButtonColor => _buttonColor ?? ThemeColor.ControlText;

    /// <summary>Gets the complete normal and visual-state appearance profile.</summary>
    public ThemeProfile Appearance => ResolveAppearance();

    /// <summary>Creates a validated copy with selected members replaced.</summary>
    /// <param name="chrome">The optional replacement chrome.</param>
    /// <param name="fill">The optional replacement fill treatment.</param>
    /// <param name="glyphs">The optional replacement glyph family.</param>
    /// <param name="trackColor">The optional replacement track foreground.</param>
    /// <param name="thumbColor">The optional replacement thumb foreground.</param>
    /// <param name="buttonColor">The optional replacement button foreground.</param>
    /// <param name="appearance">The optional appearance-profile overlay.</param>
    /// <returns>A complete validated scrollbar style.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A composed enum value is undefined.</exception>
    /// <exception cref="ArgumentException">A composed glyph or foreground is invalid.</exception>
    public ScrollBarStyle With(
        ScrollBarChrome? chrome = null,
        ScrollBarFill? fill = null,
        ScrollBarGlyphs? glyphs = null,
        ColorValue? trackColor = null,
        ColorValue? thumbColor = null,
        ColorValue? buttonColor = null,
        AppearanceProfileSet? appearance = null) => new(
        chrome ?? Chrome,
        fill ?? Fill,
        glyphs ?? Glyphs,
        trackColor ?? TrackColor,
        thumbColor ?? ThumbColor,
        buttonColor ?? ButtonColor,
        appearance is null ? Appearance : StyleResolution.Apply(Appearance, appearance.Value));

    /// <summary>Determines whether this value and another style resolve to the same presentation.</summary>
    /// <param name="other">The other style to compare.</param>
    /// <returns><see langword="true"/> when all resolved presentation members are equal.</returns>
    public bool Equals(ScrollBarStyle other) =>
        Chrome == other.Chrome &&
        Fill == other.Fill &&
        Glyphs == other.Glyphs &&
        TrackColor == other.TrackColor &&
        ThumbColor == other.ThumbColor &&
        ButtonColor == other.ButtonColor &&
        Appearance.Equals(other.Appearance);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is ScrollBarStyle other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Chrome);
        hash.Add(Fill);
        hash.Add(Glyphs);
        hash.Add(TrackColor);
        hash.Add(ThumbColor);
        hash.Add(ButtonColor);
        hash.Add(Appearance);
        return hash.ToHashCode();
    }

    /// <summary>Determines whether two scrollbar styles resolve to the same presentation.</summary>
    /// <param name="left">The first style.</param>
    /// <param name="right">The second style.</param>
    /// <returns><see langword="true"/> when the styles resolve equally.</returns>
    public static bool operator ==(ScrollBarStyle left, ScrollBarStyle right) => left.Equals(right);

    /// <summary>Determines whether two scrollbar styles resolve to different presentations.</summary>
    /// <param name="left">The first style.</param>
    /// <param name="right">The second style.</param>
    /// <returns><see langword="true"/> when the styles resolve differently.</returns>
    public static bool operator !=(ScrollBarStyle left, ScrollBarStyle right) => !left.Equals(right);

    private static ScrollBarStyle Create(ScrollBarChrome chrome, ScrollBarFill fill) => new(
        chrome,
        fill,
        ScrollBarGlyphs.Default,
        ThemeColor.Muted,
        ThemeColor.Accent,
        ThemeColor.ControlText,
        _standardAppearance);

    private ThemeProfile ResolveAppearance() => _appearance ?? _standardAppearance;

    private static InvalidationImpact Compare(
        ScrollBarStyle previous,
        Theme? previousTheme,
        ScrollBarStyle current,
        Theme? currentTheme) =>
        previous.Chrome != current.Chrome
            ? InvalidationImpact.Measure
            : previous != current ||
              ControlBase.ResolveColor(previous.TrackColor, previousTheme) !=
              ControlBase.ResolveColor(current.TrackColor, currentTheme) ||
              ControlBase.ResolveColor(previous.ThumbColor, previousTheme) !=
              ControlBase.ResolveColor(current.ThumbColor, currentTheme) ||
              ControlBase.ResolveColor(previous.ButtonColor, previousTheme) !=
              ControlBase.ResolveColor(current.ButtonColor, currentTheme)
                ? InvalidationImpact.Render
                : InvalidationImpact.None;

}
