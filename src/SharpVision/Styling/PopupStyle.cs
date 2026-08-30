// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

using System.Diagnostics.CodeAnalysis;

/// <summary>Defines the well-known transient popup appearance - a rounded all-side border by
/// default - one of the sibling styles <see cref="ControlStyle"/> generalizes.</summary>
[PublicAPI]
public record PopupStyle: ControlStyle
{
    /// <summary>Initializes a complete popup appearance.</summary>
    [SetsRequiredMembers]
    public PopupStyle(Face face, Border border, Shadow shadow) : base(face, border, shadow)
    {
    }

    /// <summary>Gets the anchor-indicator arrows drawn into the frame edge facing the anchor.</summary>
    /// <remarks>
    /// Defaulted rather than required, so the three-argument constructor every well-known sibling
    /// style shares keeps working and no existing construction site changes.
    /// </remarks>
    public PopupAnchorGlyphs AnchorGlyphs { get; init; } = PopupAnchorGlyphs.Default;

    /// <summary>Gets the default popup appearance: a rounded all-side border, no shadow.</summary>
    public static new PopupStyle Default { get; } = new(
        DefaultFace,
        new Border(
            BorderSide.All,
            BorderGlyphStyle.Rounded,
            Color.Default,
            BorderRelief.Flat,
            Color.Transparent,
            TerminalAttributes.None),
        NoShadow);
}
