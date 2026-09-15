// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

using System.Diagnostics.CodeAnalysis;

/// <summary>Defines the well-known two-state option appearance - the face a <c>CheckBox</c> or
/// <c>RadioButton</c> caption sits on - one of the sibling styles <see cref="ControlStyle"/>
/// generalizes and the owner of the theme's <c>styles.toggle</c> section.</summary>
/// <remarks>
/// A toggle cascades from <see cref="InputStyle"/>'s <c>input</c> section exactly the way
/// <c>input</c> cascades from <c>control</c>: an unauthored <c>toggle</c> section resolves to the
/// input face and every input interaction state, so a theme that never mentions it looks exactly
/// as it did before the section existed. Authoring it lets a theme give options their own plane -
/// Turbo Vision's black-on-cyan clusters beside its white-on-blue input lines, or a modern theme
/// that lays check boxes flat on the dialog face while text fields keep a sunken surface. The
/// code-owned default carries input's bordered geometry so the cascade has identical chrome to
/// diff against; the option controls themselves strip that border in their own completion.
/// </remarks>
[PublicAPI]
public record ToggleStyle: InputStyle
{
    /// <summary>Initializes a complete toggle appearance.</summary>
    /// <param name="face">The complete normal face.</param>
    /// <param name="border">The complete normal border.</param>
    /// <param name="shadow">The complete normal shadow.</param>
    /// <param name="dropDownGlyph">The printable one-cell disclosure chevron inherited from the input family.</param>
    /// <exception cref="ArgumentException">The glyph is a control or is not one cell wide.</exception>
    [SetsRequiredMembers]
    public ToggleStyle(Face face, Border border, Shadow shadow, Rune dropDownGlyph)
        : base(face, border, shadow, dropDownGlyph)
    {
    }

    /// <summary>Gets the default toggle appearance: the input family's own default, so an
    /// unauthored section diffs to nothing and resolves exactly as <c>input</c> does.</summary>
    public static new ToggleStyle Default { get; } = new(
        InputStyle.Default.Face,
        InputStyle.Default.Border,
        InputStyle.Default.Shadow,
        InputStyle.Default.DropDownGlyph);
}
