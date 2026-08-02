// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

/// <summary>Defines immutable printable narrow glyphs for CheckBox states.</summary>
[PublicAPI]
public readonly record struct CheckBoxGlyphs
{
    /// <summary>Initializes and validates unchecked, checked, and indeterminate marks.</summary>
    /// <exception cref="ArgumentException">A mark is a control or is not one cell wide.</exception>
    public CheckBoxGlyphs(Rune uncheckedMark, Rune checkedMark, Rune indeterminateMark)
    {
        Unchecked = Validate(uncheckedMark, nameof(uncheckedMark));
        Checked = Validate(checkedMark, nameof(checkedMark));
        Indeterminate = Validate(indeterminateMark, nameof(indeterminateMark));
    }

    /// <summary>Gets the default Unicode checkbox marks.</summary>
    public static CheckBoxGlyphs Default { get; } = new(
        new Rune('☐'),
        new Rune('☑'),
        new Rune('◩'));

    /// <summary>Gets the single-cell glyph rendered when the CheckBox is unchecked (default ☐).</summary>
    public Rune Unchecked { get; }

    /// <summary>Gets the single-cell glyph rendered when the CheckBox is checked (default ☑).</summary>
    public Rune Checked { get; }

    /// <summary>Gets the single-cell glyph rendered when the CheckBox is in the indeterminate three-state (default ◩).</summary>
    public Rune Indeterminate { get; }

    private static Rune Validate(Rune value, string name) =>
        value.ValidateSingleCell(name);
}
