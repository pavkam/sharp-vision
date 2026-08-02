// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Display;

/// <summary>Defines optional member-wise contributions to a complete chase-indicator presentation.</summary>
[PublicAPI]
public readonly record struct ChaseIndicatorStyleSet
{
    /// <summary>Initializes a partial chase-indicator presentation contribution.</summary>
    /// <param name="active">The optional replacement active-position glyph.</param>
    /// <param name="inactive">The optional replacement inactive-position glyph.</param>
    /// <param name="headColor">The optional replacement foreground of every current head position.</param>
    /// <param name="trailColor">The optional replacement foreground endpoint of the oldest retained trail frame.</param>
    /// <param name="trackColor">The optional replacement foreground of inactive positions.</param>
    /// <param name="appearance">The optional partial normal and visual-state appearance profile.</param>
    /// <exception cref="ArgumentException">A supplied glyph is a control or is not one cell wide, or a supplied color is transparent.</exception>
    public ChaseIndicatorStyleSet(
        Rune? active = null,
        Rune? inactive = null,
        ColorValue? headColor = null,
        ColorValue? trailColor = null,
        ColorValue? trackColor = null,
        AppearanceProfileSet? appearance = null)
    {
        var validatedActive = active is { } activeValue
            ? activeValue.ValidateSingleCell(nameof(active))
            : (Rune?) null;
        var validatedInactive = inactive is { } inactiveValue
            ? inactiveValue.ValidateSingleCell(nameof(inactive))
            : (Rune?) null;

        if (headColor is { } headColorValue)
        {
            ColorValue.ValidatePaint(headColorValue, nameof(headColor));
        }

        if (trailColor is { } trailColorValue)
        {
            ColorValue.ValidatePaint(trailColorValue, nameof(trailColor));
        }

        if (trackColor is { } trackColorValue)
        {
            ColorValue.ValidatePaint(trackColorValue, nameof(trackColor));
        }

        Active = validatedActive;
        Inactive = validatedInactive;
        HeadColor = headColor;
        TrailColor = trailColor;
        TrackColor = trackColor;
        Appearance = appearance;
    }

    /// <summary>Gets the optional replacement active-position glyph.</summary>
    public Rune? Active { get; }

    /// <summary>Gets the optional replacement inactive-position glyph.</summary>
    public Rune? Inactive { get; }

    /// <summary>Gets the optional replacement foreground of every current head position.</summary>
    public ColorValue? HeadColor { get; }

    /// <summary>Gets the optional replacement foreground endpoint of the oldest retained trail frame.</summary>
    public ColorValue? TrailColor { get; }

    /// <summary>Gets the optional replacement foreground of inactive positions.</summary>
    public ColorValue? TrackColor { get; }

    /// <summary>Gets the optional partial normal and visual-state appearance profile.</summary>
    public AppearanceProfileSet? Appearance { get; }

    /// <summary>Applies this partial contribution to a complete chase-indicator presentation.</summary>
    /// <param name="baseline">The complete presentation that supplies omitted members.</param>
    /// <returns>The validated complete composed presentation.</returns>
    /// <exception cref="ArgumentException">A composed glyph is a control or is not one cell wide, or a composed color is transparent.</exception>
    public ChaseIndicatorStyle Apply(ChaseIndicatorStyle baseline)
    {
        var appearance = Appearance is null
            ? baseline.Appearance
            : StyleResolution.Apply(baseline.Appearance, Appearance.Value);

        return new ChaseIndicatorStyle(
            Active ?? baseline.Active,
            Inactive ?? baseline.Inactive,
            HeadColor ?? baseline.HeadColor,
            TrailColor ?? baseline.TrailColor,
            TrackColor ?? baseline.TrackColor,
            appearance);
    }
}

