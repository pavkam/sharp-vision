// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

using System.Diagnostics.CodeAnalysis;

/// <summary>Defines the well-known passive, non-interactive hint appearance - deliberately
/// borderless, unlike every other style except the base <see cref="ControlStyle"/> itself, so
/// a transient hint is visually distinct from <see cref="PopupStyle"/>'s rounded frame - one
/// of the sibling styles <see cref="ControlStyle"/> generalizes.</summary>
[PublicAPI]
public record TooltipStyle: ControlStyle
{
    /// <summary>Initializes a complete tooltip appearance.</summary>
    [SetsRequiredMembers]
    public TooltipStyle(Face face, Border border, Shadow shadow) : base(face, border, shadow)
    {
    }

    /// <summary>Gets the default tooltip appearance: no border, no shadow.</summary>
    public static new TooltipStyle Default { get; } = new(DefaultFace, NoBorder, NoShadow);
}
