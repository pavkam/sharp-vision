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
    /// <param name="appearance">The optional partial normal and visual-state appearance profile.</param>
    /// <exception cref="ArgumentException">A supplied glyph is a control or is not one cell wide.</exception>
    public ChaseIndicatorStyleSet(
        Rune? active = null,
        Rune? inactive = null,
        AppearanceProfileSet? appearance = null)
    {
        var validatedActive = active is { } activeValue
            ? CellGlyphResolver.ValidateSingleCell(activeValue, nameof(active))
            : (Rune?) null;
        var validatedInactive = inactive is { } inactiveValue
            ? CellGlyphResolver.ValidateSingleCell(inactiveValue, nameof(inactive))
            : (Rune?) null;

        Active = validatedActive;
        Inactive = validatedInactive;
        Appearance = appearance;
    }

    /// <summary>Gets the optional replacement active-position glyph.</summary>
    public Rune? Active { get; }

    /// <summary>Gets the optional replacement inactive-position glyph.</summary>
    public Rune? Inactive { get; }

    /// <summary>Gets the optional partial normal and visual-state appearance profile.</summary>
    public AppearanceProfileSet? Appearance { get; }

    /// <summary>Applies this partial contribution to a complete chase-indicator presentation.</summary>
    /// <param name="baseline">The complete presentation that supplies omitted members.</param>
    /// <returns>The validated complete composed presentation.</returns>
    /// <exception cref="ArgumentException">A composed glyph is a control or is not one cell wide.</exception>
    public ChaseIndicatorStyle Apply(ChaseIndicatorStyle baseline)
    {
        var appearance = Appearance is null
            ? baseline.Appearance
            : StyleResolution.Apply(baseline.Appearance, Appearance.Value);

        return new ChaseIndicatorStyle(Active ?? baseline.Active, Inactive ?? baseline.Inactive, appearance);
    }
}

