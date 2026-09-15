// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

using System.Diagnostics.CodeAnalysis;

/// <summary>Defines the well-known layout-panel appearance - the face a <c>Dock</c>, <c>Grid</c>,
/// <c>Stack</c>, <c>Wrap</c>, <c>Overlay</c>, or <c>SplitPane</c> paints behind the children it
/// arranges - one of the sibling styles <see cref="ControlStyle"/> generalizes and the owner of the
/// theme's <c>styles.panel</c> section.</summary>
/// <remarks>
/// A panel cascades its Normal from <c>control</c> and, like every other passive chrome, answers
/// no interaction state, so a theme that never authors <c>panel</c> paints its layout panels with
/// the passive control face exactly as before. Authoring it lets a theme decide whether layout is
/// a plane of its own or pure arrangement: Turbo Vision sets a transparent panel background so the
/// blue desktop shows through an application's root <c>Dock</c> and a dialog's <c>Stack</c> shows
/// the gray dialog face beneath it, which is how Borland's <c>TGroup</c> behaves - a group
/// arranges its views and paints nothing of its own.
/// </remarks>
[PublicAPI]
public record PanelStyle: ControlStyle
{
    /// <summary>Initializes a complete panel appearance.</summary>
    /// <param name="face">The complete normal face.</param>
    /// <param name="border">The complete normal border.</param>
    /// <param name="shadow">The complete normal shadow.</param>
    [SetsRequiredMembers]
    public PanelStyle(Face face, Border border, Shadow shadow) : base(face, border, shadow)
    {
    }

    /// <summary>Gets the default panel appearance: the passive control's own borderless default,
    /// so an unauthored section diffs to nothing and resolves exactly as <c>control</c> does.</summary>
    public static new PanelStyle Default { get; } = new(DefaultFace, NoBorder, NoShadow);
}
