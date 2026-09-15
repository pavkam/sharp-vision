// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

using System.Diagnostics.CodeAnalysis;

/// <summary>Defines the well-known selectable-row appearance - a list, tree, table, tab, or
/// navigation row whose selection rather than pointer membership owns the highlighted fill - one
/// of the sibling styles <see cref="ControlStyle"/> generalizes and the owner of the theme's
/// <c>styles.item</c> section.</summary>
/// <remarks>
/// An item's Normal cascades from <c>control</c> (rows lie on the passive face, borderless) and
/// its interaction states cascade from <c>input</c> with the row rule applied: pointer hover keeps
/// the row's own background and changes only its text, while focus, press, and selection follow
/// the input deltas. That is exactly what <see cref="Theme.GetInteractiveRowStyleSet"/> always
/// produced, so a theme that never authors <c>item</c> looks exactly as it did before the section
/// existed. Authoring it lets a theme give rows their own plane - Turbo Vision's black-on-cyan list
/// viewers - or a hover fill of their own without touching buttons and text fields.
/// </remarks>
[PublicAPI]
public record ItemStyle: ControlStyle
{
    /// <summary>Initializes a complete item appearance.</summary>
    /// <param name="face">The complete normal face.</param>
    /// <param name="border">The complete normal border.</param>
    /// <param name="shadow">The complete normal shadow.</param>
    [SetsRequiredMembers]
    public ItemStyle(Face face, Border border, Shadow shadow) : base(face, border, shadow)
    {
    }

    /// <summary>Gets the default item appearance: the passive control's own borderless default,
    /// so an unauthored section diffs to nothing and resolves exactly as a row always has.</summary>
    public static new ItemStyle Default { get; } = new(DefaultFace, NoBorder, NoShadow);
}
