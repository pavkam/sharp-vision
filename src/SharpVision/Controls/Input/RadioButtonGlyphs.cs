// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

/// <summary>Defines an immutable, default-safe pair of printable one-cell radio-button glyphs.</summary>
[PublicAPI]
public readonly record struct RadioButtonGlyphs: IAppearanceFragment
{
    /// <summary>Initializes and validates unchecked and checked marks.</summary>
    /// <param name="uncheckedMark">The printable one-cell unchecked mark.</param>
    /// <param name="checkedMark">The printable one-cell checked mark.</param>
    /// <exception cref="ArgumentException">A mark is a control or is not one cell wide.</exception>
    public RadioButtonGlyphs(Rune uncheckedMark, Rune checkedMark)
    {
        // Validated here as well as in each init accessor, and deliberately so: an accessor cannot
        // know which constructor argument it came from, so its ArgumentException names "value".
        // For a family this wide that identifies nothing. Checking by name first means a caller
        // passing one bad glyph among ten is told which one.
        Unchecked = uncheckedMark.ValidateSingleCell(nameof(uncheckedMark));
        Checked = checkedMark.ValidateSingleCell(nameof(checkedMark));
    }

    /// <summary>Gets the established one-cell circle marks.</summary>
    public static RadioButtonGlyphs Default { get; } = new(new Rune('○'), new Rune('◉'));

    /// <summary>Gets the glyph rendered when the radio button is unchecked.</summary>
    /// <exception cref="ArgumentException">The replacement value is a control or is not one cell wide.</exception>
    public Rune Unchecked
    {
        get;
        init => field = value.ValidateSingleCell(nameof(value));
    }

    /// <summary>Gets the glyph rendered when the radio button is checked.</summary>
    /// <exception cref="ArgumentException">The replacement value is a control or is not one cell wide.</exception>
    public Rune Checked
    {
        get;
        init => field = value.ValidateSingleCell(nameof(value));
    }

    IAppearanceFragment IAppearanceFragment.Clone() => this with { };
}
