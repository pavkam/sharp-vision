// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

using System.Diagnostics.CodeAnalysis;

/// <summary>Defines the well-known passive, non-interactive hint appearance - a light all-side
/// border by default so the hint stays visually contained when it floats over busy content,
/// while remaining distinct from <see cref="PopupStyle"/>'s rounded frame - one of the sibling
/// styles <see cref="ControlStyle"/> generalizes.</summary>
[PublicAPI]
public record TooltipStyle: ControlStyle
{
    /// <summary>Initializes a complete tooltip appearance.</summary>
    [SetsRequiredMembers]
    public TooltipStyle(Face face, Border border, Shadow shadow) : base(face, border, shadow)
    {
    }

    /// <summary>Gets the default tooltip appearance: a light all-side border, no shadow.</summary>
    public static new TooltipStyle Default { get; } = new(
        DefaultFace,
        new Border(
            BorderSide.All,
            BorderGlyphStyle.Light,
            Color.Default,
            BorderRelief.Raised,
            Color.Transparent,
            TerminalAttributes.None),
        NoShadow);
}
