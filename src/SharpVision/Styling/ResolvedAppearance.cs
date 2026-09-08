// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>
/// Holds one control's fully composed face, border, and shadow, resolved from theme, local
/// values, and the active visual state into concrete literal colors and attributes. Only the
/// framework produces instances, through <c>ControlBase.GetResolvedAppearance</c>; a control
/// override receives one as a parameter and reads it, but never constructs its own.
/// </summary>
public readonly struct ResolvedAppearance
{
    /// <summary>Initializes one fully composed appearance snapshot.</summary>
    /// <param name="face">The resolved local face.</param>
    /// <param name="border">The resolved local border.</param>
    /// <param name="borderStyles">The concrete per-edge border terminal style.</param>
    /// <param name="shadow">The resolved local shadow.</param>
    /// <param name="style">The literal terminal style painted for body content.</param>
    /// <param name="backgroundMode">Whether the face background is opaque or transparent.</param>
    /// <param name="borderBackgroundMode">Whether the border background is opaque or transparent.</param>
    /// <param name="shadowStyle">The literal terminal style painted for shadow cells.</param>
    /// <param name="shadowBackgroundMode">Whether the shadow background is opaque or transparent.</param>
    internal ResolvedAppearance(
        Face face,
        Border border,
        ResolvedBorderStyles borderStyles,
        Shadow shadow,
        TerminalStyle style,
        BackgroundMode backgroundMode,
        BackgroundMode borderBackgroundMode,
        TerminalStyle shadowStyle,
        BackgroundMode shadowBackgroundMode)
    {
        Face = face;
        Border = border;
        BorderStyles = borderStyles;
        Shadow = shadow;
        Style = style;
        BackgroundMode = backgroundMode;
        BorderBackgroundMode = borderBackgroundMode;
        ShadowStyle = shadowStyle;
        ShadowBackgroundMode = shadowBackgroundMode;
    }

    /// <summary>Gets the resolved local face.</summary>
    public Face Face { get; }

    /// <summary>Gets the resolved local border.</summary>
    public Border Border { get; }

    /// <summary>Gets the concrete terminal style resolved for every physical border edge.</summary>
    public ResolvedBorderStyles BorderStyles { get; }

    /// <summary>Gets the resolved local shadow.</summary>
    public Shadow Shadow { get; }

    /// <summary>Gets the literal terminal style painted for this control's own body content.</summary>
    public TerminalStyle Style { get; }

    /// <summary>Gets whether the resolved face background is opaque or transparent.</summary>
    public BackgroundMode BackgroundMode { get; }

    /// <summary>Gets whether the resolved border background is opaque or transparent.</summary>
    public BackgroundMode BorderBackgroundMode { get; }

    /// <summary>Gets the literal terminal style painted for this control's shadow cells.</summary>
    public TerminalStyle ShadowStyle { get; }

    /// <summary>Gets whether the resolved shadow background is opaque or transparent.</summary>
    public BackgroundMode ShadowBackgroundMode { get; }
}
