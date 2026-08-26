// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

/// <summary>Defines immutable printable narrow glyphs for CheckBox states.</summary>
[PublicAPI]
public readonly record struct CheckBoxGlyphs: IAppearanceFragment
{
    /// <summary>Initializes and validates unchecked, checked, and indeterminate marks.</summary>
    /// <exception cref="ArgumentException">A mark is a control or is not one cell wide.</exception>
    public CheckBoxGlyphs(Rune uncheckedMark, Rune checkedMark, Rune indeterminateMark)
    {
        // Validated here as well as in each init accessor, and deliberately so: an accessor cannot
        // know which constructor argument it came from, so its ArgumentException names "value".
        // For a family this wide that identifies nothing. Checking by name first means a caller
        // passing one bad glyph among ten is told which one.
        Unchecked = uncheckedMark.ValidateSingleCell(nameof(uncheckedMark));
        Checked = checkedMark.ValidateSingleCell(nameof(checkedMark));
        Indeterminate = indeterminateMark.ValidateSingleCell(nameof(indeterminateMark));
    }

    /// <summary>Gets the default Unicode checkbox marks.</summary>
    public static CheckBoxGlyphs Default { get; } = new(
        new Rune('☐'),
        new Rune('☑'),
        new Rune('◩'));

    /// <summary>Gets the single-cell glyph rendered when the CheckBox is unchecked (default ☐).</summary>
    /// <exception cref="ArgumentException">The replacement value is a control or is not one cell wide.</exception>
    public Rune Unchecked
    {
        get => field.Value == 0 ? Default.Unchecked : field;
        init => field = value.ValidateSingleCell(nameof(value));
    }

    /// <summary>Gets the single-cell glyph rendered when the CheckBox is checked (default ☑).</summary>
    /// <exception cref="ArgumentException">The replacement value is a control or is not one cell wide.</exception>
    public Rune Checked
    {
        get => field.Value == 0 ? Default.Checked : field;
        init => field = value.ValidateSingleCell(nameof(value));
    }

    /// <summary>Gets the single-cell glyph rendered when the CheckBox is in the indeterminate three-state (default ◩).</summary>
    /// <exception cref="ArgumentException">The replacement value is a control or is not one cell wide.</exception>
    public Rune Indeterminate
    {
        get => field.Value == 0 ? Default.Indeterminate : field;
        init => field = value.ValidateSingleCell(nameof(value));
    }

    IAppearanceFragment IAppearanceFragment.Clone() => this with { };
}
