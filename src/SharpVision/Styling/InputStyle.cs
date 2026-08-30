// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

using System.Diagnostics.CodeAnalysis;

using ValueRange = JetBrains.Annotations.ValueRangeAttribute;

/// <summary>Defines the well-known editable-or-selectable input appearance - a heavy all-side
/// border by default - one of the sibling styles <see cref="ControlStyle"/> generalizes.</summary>
[PublicAPI]
public record InputStyle: ControlStyle
{
    /// <summary>Initializes a complete input appearance.</summary>
    /// <param name="face">The complete normal face.</param>
    /// <param name="border">The complete normal border.</param>
    /// <param name="shadow">The complete normal shadow.</param>
    /// <param name="dropDownGlyph">The printable one-cell disclosure chevron.</param>
    /// <exception cref="ArgumentException">The glyph is a control or is not one cell wide.</exception>
    [SetsRequiredMembers]
    public InputStyle(Face face, Border border, Shadow shadow, Rune dropDownGlyph)
        : base(face, border, shadow) => DropDownGlyph = dropDownGlyph;

    /// <summary>Gets the default input appearance: a heavy all-side border, no shadow.</summary>
    public static new InputStyle Default { get; } = new(
        DefaultFace,
        new Border(
            BorderSide.All,
            BorderGlyphStyle.Heavy,
            Color.Default,
            BorderRelief.Flat,
            Color.Transparent,
            TerminalAttributes.None),
        NoShadow,
        ControlGlyphs.Disclosure.DropDown.Value);

    /// <summary>Gets the disclosure chevron drawn by every drop-down input.</summary>
    /// <remarks>
    /// Deliberately on the shared input style rather than on each control. A terminal without
    /// dependable arrow coverage needs this replaced everywhere at once, and the three drop-down
    /// inputs previously each carried their own copy of the property, its validated setter, and its
    /// reset method - so the one detail that most wants a single application-wide answer was the
    /// one that had to be set per instance.
    /// </remarks>
    /// <exception cref="ArgumentException">The replacement value is a control or is not one cell wide.</exception>
    public required Rune DropDownGlyph
    {
        get;
        init => field = value.ValidateSingleCell(nameof(value));
    }

    /// <summary>Gets the cell gap between a present <see cref="Affix"/> and the caption it
    /// sits beside.</summary>
    /// <remarks>
    /// Not <c>required</c>, unlike this type's other structural members: every existing caller that
    /// constructs an <see cref="InputStyle"/> (or a derived style) predates affixes, and a uniform
    /// code-owned default of one cell needs no per-caller opt-in - the same reasoning
    /// <see cref="PopupStyle.AnchorGlyphs"/> already applies to a non-required style member with a
    /// code-owned default.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The replacement value is outside 0-4.</exception>
    [ValueRange(0, 4)]
    public int AffixGap
    {
        get;
        init => field = value is >= 0 and <= 4
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), value, "The affix gap must be between 0 and 4 cells.");
    } = 1;
}
