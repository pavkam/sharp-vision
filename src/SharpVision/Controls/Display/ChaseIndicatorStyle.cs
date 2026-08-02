// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Display;

/// <summary>Defines one complete immutable chase-indicator presentation.</summary>
[PublicAPI]
public readonly struct ChaseIndicatorStyle: IEquatable<ChaseIndicatorStyle>
{
    private static readonly ThemeProfile _standardAppearance = ControlStyleProfiles.Control;
    private readonly Rune? _active;
    private readonly Rune? _inactive;
    private readonly ColorValue? _headColor;
    private readonly ColorValue? _trailColor;
    private readonly ColorValue? _trackColor;
    private readonly ThemeProfile? _appearance;

    /// <summary>Initializes a complete chase-indicator presentation.</summary>
    /// <param name="active">The printable one-cell active-position glyph.</param>
    /// <param name="inactive">The printable one-cell inactive-position glyph.</param>
    /// <param name="headColor">The non-transparent foreground of every current head position.</param>
    /// <param name="trailColor">The non-transparent foreground endpoint of the oldest retained trail frame.</param>
    /// <param name="trackColor">The non-transparent foreground of inactive positions.</param>
    /// <param name="appearance">The complete normal and visual-state appearance profile.</param>
    /// <exception cref="ArgumentException">A glyph is a control or is not one cell wide, or a color is transparent.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="appearance"/> is <see langword="null"/>.</exception>
    public ChaseIndicatorStyle(
        Rune active,
        Rune inactive,
        ColorValue headColor,
        ColorValue trailColor,
        ColorValue trackColor,
        ThemeProfile appearance)
    {
        var validatedActive = CellGlyphResolver.ValidateSingleCell(active, nameof(active));
        var validatedInactive = CellGlyphResolver.ValidateSingleCell(inactive, nameof(inactive));
        ColorValue.ValidatePaint(headColor, nameof(headColor));
        ColorValue.ValidatePaint(trailColor, nameof(trailColor));
        ColorValue.ValidatePaint(trackColor, nameof(trackColor));
        ArgumentNullException.ThrowIfNull(appearance);

        _active = validatedActive;
        _inactive = validatedInactive;
        _headColor = headColor;
        _trailColor = trailColor;
        _trackColor = trackColor;
        _appearance = appearance;
    }

    /// <summary>Gets the standard circle presentation.</summary>
    public static ChaseIndicatorStyle Default => default;

    /// <summary>Gets the filled and hollow circle presentation.</summary>
    public static ChaseIndicatorStyle Circle => default;

    /// <summary>Gets the filled and hollow diamond presentation.</summary>
    public static ChaseIndicatorStyle Diamond { get; } = Create('◆', '◇');

    /// <summary>Gets the filled and hollow square presentation.</summary>
    public static ChaseIndicatorStyle Square { get; } = Create('■', '□');

    /// <summary>Gets the filled and hollow upward-triangle presentation.</summary>
    public static ChaseIndicatorStyle Up { get; } = Create('▲', '△');

    /// <summary>Gets the filled and hollow downward-triangle presentation.</summary>
    public static ChaseIndicatorStyle Down { get; } = Create('▼', '▽');

    /// <summary>Gets the filled and hollow left-triangle presentation.</summary>
    public static ChaseIndicatorStyle Left { get; } = Create('◀', '◁');

    /// <summary>Gets the filled and hollow right-triangle presentation.</summary>
    public static ChaseIndicatorStyle Right { get; } = Create('▶', '▷');

    /// <summary>Gets the active-position glyph.</summary>
    public Rune Active => _active ?? new Rune('●');

    /// <summary>Gets the inactive-position glyph.</summary>
    public Rune Inactive => _inactive ?? new Rune('◯');

    /// <summary>Gets the foreground of every current head position.</summary>
    public ColorValue HeadColor => _headColor ?? ThemeColor.Accent;

    /// <summary>Gets the foreground endpoint of the oldest retained trail frame.</summary>
    public ColorValue TrailColor => _trailColor ?? ThemeColor.Muted;

    /// <summary>Gets the foreground of inactive positions.</summary>
    public ColorValue TrackColor => _trackColor ?? ThemeColor.Muted;

    /// <summary>Gets the complete normal and visual-state appearance profile.</summary>
    public ThemeProfile Appearance => ResolveAppearance();

    /// <summary>Determines whether this value and another style resolve to the same presentation.</summary>
    /// <param name="other">The other style to compare.</param>
    /// <returns><see langword="true"/> when all resolved presentation members are equal.</returns>
    public bool Equals(ChaseIndicatorStyle other) =>
        Active == other.Active &&
        Inactive == other.Inactive &&
        HeadColor == other.HeadColor &&
        TrailColor == other.TrailColor &&
        TrackColor == other.TrackColor &&
        Appearance.Equals(other.Appearance);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is ChaseIndicatorStyle other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Active);
        hash.Add(Inactive);
        hash.Add(HeadColor);
        hash.Add(TrailColor);
        hash.Add(TrackColor);
        hash.Add(Appearance);
        return hash.ToHashCode();
    }

    /// <summary>Determines whether two chase-indicator styles resolve to the same presentation.</summary>
    /// <param name="left">The first style.</param>
    /// <param name="right">The second style.</param>
    /// <returns><see langword="true"/> when the styles resolve equally.</returns>
    public static bool operator ==(ChaseIndicatorStyle left, ChaseIndicatorStyle right) => left.Equals(right);

    /// <summary>Determines whether two chase-indicator styles resolve to different presentations.</summary>
    /// <param name="left">The first style.</param>
    /// <param name="right">The second style.</param>
    /// <returns><see langword="true"/> when the styles resolve differently.</returns>
    public static bool operator !=(ChaseIndicatorStyle left, ChaseIndicatorStyle right) => !left.Equals(right);

    private static ChaseIndicatorStyle Create(char active, char inactive) =>
        new(
            new Rune(active),
            new Rune(inactive),
            Default.HeadColor,
            Default.TrailColor,
            Default.TrackColor,
            _standardAppearance);

    private ThemeProfile ResolveAppearance() => _appearance ?? _standardAppearance;

}
