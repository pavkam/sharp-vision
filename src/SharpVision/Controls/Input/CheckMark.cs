// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

/// <summary>Defines one immutable check-mark layout and its state glyph family.</summary>
/// <remarks>
/// <para>
/// This is the presentation a check mark needs and nothing more. <see cref="CheckBoxStyle"/> also
/// carries a <see cref="AppearanceStates"/> that paints an entire control, which is correct for a
/// standalone CheckBox but wrong for a tree row: the row already paints itself from its own
/// resolved style, so applying a CheckBox appearance to it would recolour the whole line. Sharing
/// this narrower value lets both controls agree on mark geometry and glyphs without sharing
/// appearance.
/// </para>
/// <para>
/// The default value is the three-cell <see cref="Brackets"/> family, matching
/// <see cref="CheckBoxStyle.Default"/> so a check mark reads the same whether it appears in a
/// standalone CheckBox or a tree row.
/// </para>
/// </remarks>
[PublicAPI]
public readonly struct CheckMark: IEquatable<CheckMark>
{
    private const int _bracketWidthCells = 3;
    private const int _singleWidthCells = 1;

    private readonly CheckBoxMarkStyle? _markStyle;
    private readonly CheckBoxGlyphs? _glyphs;

    /// <summary>Initializes one validated mark layout and glyph family.</summary>
    /// <param name="markStyle">The validated mark-layout family.</param>
    /// <param name="glyphs">The complete unchecked, checked, and indeterminate glyph family.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="markStyle"/> is undefined.</exception>
    /// <exception cref="ArgumentException">A glyph is a control or is not one cell wide.</exception>
    public CheckMark(CheckBoxMarkStyle markStyle, CheckBoxGlyphs glyphs)
    {
        ArgumentOutOfRangeException.ThrowIfNotDefined(markStyle, nameof(markStyle), "The enum value is unknown.");

        // Reconstructing the family revalidates every glyph, so a default-constructed
        // CheckBoxGlyphs cannot smuggle an unvalidated rune through.
        _markStyle = markStyle;
        _glyphs = new CheckBoxGlyphs(glyphs.Unchecked, glyphs.Checked, glyphs.Indeterminate);
    }

    /// <summary>Gets the three-cell bracket mark, which is also the default value.</summary>
    public static CheckMark Brackets { get; } = new(
        CheckBoxMarkStyle.Brackets,
        CheckBoxStyle.Brackets.Glyphs);

    /// <summary>Gets the one-cell circle, tick, and indeterminate mark.</summary>
    public static CheckMark Tick { get; } = new(
        CheckBoxMarkStyle.Tick,
        CheckBoxStyle.Tick.Glyphs);

    /// <summary>Gets the one-cell square-state mark.</summary>
    public static CheckMark Square { get; } = new(
        CheckBoxMarkStyle.Square,
        CheckBoxGlyphs.Default);

    /// <summary>Gets the mark-layout family.</summary>
    public CheckBoxMarkStyle MarkStyle => _markStyle ?? CheckBoxMarkStyle.Brackets;

    /// <summary>Gets the complete state glyph family.</summary>
    public CheckBoxGlyphs Glyphs => _glyphs ?? CheckBoxStyle.Brackets.Glyphs;

    /// <summary>Gets the horizontal terminal-cell reservation for this mark.</summary>
    /// <remarks>
    /// Brackets wrap their glyph in a leading and trailing cell; every other family occupies one
    /// cell. A change to this value moves surrounding content and therefore requires a measure
    /// pass, not merely a repaint.
    /// </remarks>
    public int Width => MarkStyle == CheckBoxMarkStyle.Brackets
        ? _bracketWidthCells
        : _singleWidthCells;

    /// <summary>Compares two marks for presentation equality.</summary>
    /// <param name="left">The left mark.</param>
    /// <param name="right">The right mark.</param>
    /// <returns><see langword="true"/> when both resolve to the same presentation.</returns>
    public static bool operator ==(CheckMark left, CheckMark right) => left.Equals(right);

    /// <summary>Compares two marks for presentation inequality.</summary>
    /// <param name="left">The left mark.</param>
    /// <param name="right">The right mark.</param>
    /// <returns><see langword="true"/> when the marks resolve differently.</returns>
    public static bool operator !=(CheckMark left, CheckMark right) => !left.Equals(right);

    /// <summary>Creates a mark that keeps this layout and replaces its glyph family.</summary>
    /// <param name="glyphs">The replacement glyph family.</param>
    /// <returns>A mark with the same layout and the supplied glyphs.</returns>
    /// <exception cref="ArgumentException">A glyph is a control or is not one cell wide.</exception>
    public CheckMark WithGlyphs(CheckBoxGlyphs glyphs) => new(MarkStyle, glyphs);

    /// <summary>Selects the glyph this mark uses for one check state.</summary>
    /// <param name="state">The checked, unchecked, or indeterminate state.</param>
    /// <returns>The configured glyph for that state.</returns>
    public Rune GlyphFor(bool? state) => state switch
    {
        true => Glyphs.Checked,
        false => Glyphs.Unchecked,
        null => Glyphs.Indeterminate
    };

    /// <inheritdoc/>
    public bool Equals(CheckMark other) => MarkStyle == other.MarkStyle && Glyphs == other.Glyphs;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is CheckMark other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(MarkStyle, Glyphs);
}
