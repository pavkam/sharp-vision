// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

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
        EnumValidation.ValidateDefined(chrome);
        EnumValidation.ValidateDefined(fill);
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
        ProfilesEqual(Appearance, other.Appearance);

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
        AddProfile(ref hash, Appearance);
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
