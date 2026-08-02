// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

/// <summary>Defines an immutable, default-safe pair of printable one-cell radio-button glyphs.</summary>
[PublicAPI]
public readonly struct RadioButtonGlyphs: IEquatable<RadioButtonGlyphs>
{
    private readonly Rune? _unchecked;
    private readonly Rune? _checked;

    /// <summary>Initializes and validates unchecked and checked marks.</summary>
    /// <param name="uncheckedMark">The printable one-cell unchecked mark.</param>
    /// <param name="checkedMark">The printable one-cell checked mark.</param>
    /// <exception cref="ArgumentException">A mark is a control or is not one cell wide.</exception>
    public RadioButtonGlyphs(Rune uncheckedMark, Rune checkedMark)
    {
        var validatedUnchecked = uncheckedMark.ValidateSingleCell(nameof(uncheckedMark));
        var validatedChecked = checkedMark.ValidateSingleCell(nameof(checkedMark));

        _unchecked = validatedUnchecked;
        _checked = validatedChecked;
    }

    /// <summary>Gets the established one-cell circle marks.</summary>
    public static RadioButtonGlyphs Default => default;

    /// <summary>Gets the glyph rendered when the radio button is unchecked.</summary>
    public Rune Unchecked => _unchecked ?? new Rune('○');

    /// <summary>Gets the glyph rendered when the radio button is checked.</summary>
    public Rune Checked => _checked ?? new Rune('◉');

    /// <summary>Determines whether both resolved glyphs equal another pair.</summary>
    /// <param name="other">The other glyph pair to compare.</param>
    /// <returns><see langword="true"/> when both resolved glyphs are equal.</returns>
    public bool Equals(RadioButtonGlyphs other) => Unchecked == other.Unchecked && Checked == other.Checked;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is RadioButtonGlyphs other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Unchecked, Checked);

    /// <summary>Determines whether two glyph pairs resolve equally.</summary>
    /// <param name="left">The first glyph pair.</param>
    /// <param name="right">The second glyph pair.</param>
    /// <returns><see langword="true"/> when the pairs resolve equally.</returns>
    public static bool operator ==(RadioButtonGlyphs left, RadioButtonGlyphs right) => left.Equals(right);

    /// <summary>Determines whether two glyph pairs resolve differently.</summary>
    /// <param name="left">The first glyph pair.</param>
    /// <param name="right">The second glyph pair.</param>
    /// <returns><see langword="true"/> when the pairs resolve differently.</returns>
    public static bool operator !=(RadioButtonGlyphs left, RadioButtonGlyphs right) => !left.Equals(right);
}
