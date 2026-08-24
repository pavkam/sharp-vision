// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Display;

/// <summary>Defines one <see cref="GlyphFamily"/>'s complete ChaseIndicator contribution: the
/// active and inactive position glyph pair.</summary>
[PublicAPI]
public readonly record struct ChaseIndicatorGlyphs: IAppearanceFragment
{
    /// <summary>Initializes and validates active and inactive position glyphs.</summary>
    /// <param name="active">The printable one-cell active-position glyph.</param>
    /// <param name="inactive">The printable one-cell inactive-position glyph.</param>
    /// <exception cref="ArgumentException">A glyph is a control or is not one cell wide.</exception>
    public ChaseIndicatorGlyphs(Rune active, Rune inactive)
    {
        // Validated here as well as in each init accessor, and deliberately so: an accessor cannot
        // know which constructor argument it came from, so its ArgumentException names "value".
        // Checking by name first means a caller passing one bad glyph among two is told which one.
        Active = active.ValidateSingleCell(nameof(active));
        Inactive = inactive.ValidateSingleCell(nameof(inactive));
    }

    /// <summary>Gets the active-position glyph.</summary>
    /// <exception cref="ArgumentException">The replacement value is a control or is not one cell wide.</exception>
    public Rune Active
    {
        get;
        init => field = value.ValidateSingleCell(nameof(value));
    }

    /// <summary>Gets the inactive-position glyph.</summary>
    /// <exception cref="ArgumentException">The replacement value is a control or is not one cell wide.</exception>
    public Rune Inactive
    {
        get;
        init => field = value.ValidateSingleCell(nameof(value));
    }

    IAppearanceFragment IAppearanceFragment.Clone() => this with { };
}
