// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

using System.Diagnostics.CodeAnalysis;

/// <summary>Defines the universal themeable appearance every control style composes - the one
/// concrete, non-abstract base every control's own style type derives from, directly or through
/// one of the sibling well-known styles. Ordinary and concrete, not privileged: a control with
/// nothing to add can use this (or a sibling) directly as its style type, with zero new type
/// declared.</summary>
[PublicAPI]
public record ControlStyle: IAppearanceFragment
{
    /// <summary>Initializes a complete control appearance.</summary>
    [SetsRequiredMembers]
    public ControlStyle(Face face, Border border, Shadow shadow)
    {
        Face = face;
        Border = border;
        Shadow = shadow;
    }

    /// <summary>Gets the text, glyph, and body appearance.</summary>
    public required Face Face { get; init; }

    /// <summary>Gets the intrinsic border appearance.</summary>
    public required Border Border { get; init; }

    /// <summary>Gets the intrinsic shadow appearance.</summary>
    public required Shadow Shadow { get; init; }

    /// <summary>Gets the shared code-owned default face every well-known style starts from.</summary>
    internal static Face DefaultFace { get; } = new(
        Color.Default, Color.Transparent, TerminalAttributes.None, Underline.None, Color.Default);

    /// <summary>Gets the shared code-owned no-border default every borderless style starts from.</summary>
    internal static Border NoBorder { get; } = new(
        BorderSide.None, BorderGlyphStyle.Default, Color.Default, Color.Transparent, TerminalAttributes.None);

    /// <summary>Gets the shared code-owned no-shadow default every non-Window style starts from.</summary>
    internal static Shadow NoShadow { get; } = new(
        false, ShadowMode.Composite, default, ControlGlyphs.Chrome.Shadow.Value, Color.Default, Color.Transparent, TerminalAttributes.None);

    // Declared AFTER the three members it reads. Static property initializers run in textual order,
    // so while this sat above them it captured default(Face)/default(Border)/default(Shadow) - all
    // zeroes - instead of the code-owned values. That made the universal fallback for every
    // style-less control carry a Color.Default background rather than the declared Transparent one,
    // which silently closed the ambient-inheritance gate in AppearanceResolver.Resolve (it inherits
    // only when the resolved background is Transparent) and stopped transparent controls from
    // picking up their parent's face entirely.
    /// <summary>Gets the passive base-control default: no border, no shadow.</summary>
    public static ControlStyle Default { get; } = new(DefaultFace, NoBorder, NoShadow);

    IAppearanceFragment IAppearanceFragment.Clone() => this with { };
}
