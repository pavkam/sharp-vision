// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

using System.Diagnostics.CodeAnalysis;

/// <summary>Defines the well-known framed grouping-or-collection appearance - a light all-side
/// border by default - one of the sibling styles <see cref="ControlStyle"/> generalizes.</summary>
[PublicAPI]
public record ContainerStyle: ControlStyle
{
    /// <summary>Initializes a complete container appearance.</summary>
    [SetsRequiredMembers]
    public ContainerStyle(Face face, Border border, Shadow shadow) : base(face, border, shadow)
    {
    }

    /// <summary>Gets the default container appearance: a light all-side border, no shadow.</summary>
    public static new ContainerStyle Default { get; } = new(
        DefaultFace,
        new Border(
            BorderSide.All,
            BorderGlyphStyle.Light,
            Color.Default,
            BorderRelief.Sunken,
            Color.Transparent,
            TerminalAttributes.None),
        NoShadow);
}
