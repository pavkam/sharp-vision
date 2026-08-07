// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

internal readonly struct ResolvedAppearance
{
    internal ResolvedAppearance(
        Face face,
        Border border,
        Shadow shadow,
        TerminalStyle style,
        BackgroundMode backgroundMode,
        TerminalStyle borderStyle,
        BackgroundMode borderBackgroundMode,
        TerminalStyle shadowStyle,
        BackgroundMode shadowBackgroundMode)
    {
        Face = face;
        Border = border;
        Shadow = shadow;
        Style = style;
        BackgroundMode = backgroundMode;
        BorderStyle = borderStyle;
        BorderBackgroundMode = borderBackgroundMode;
        ShadowStyle = shadowStyle;
        ShadowBackgroundMode = shadowBackgroundMode;
    }

    internal Face Face { get; }

    internal Border Border { get; }

    internal Shadow Shadow { get; }

    internal TerminalStyle Style { get; }

    internal BackgroundMode BackgroundMode { get; }

    internal TerminalStyle BorderStyle { get; }

    internal BackgroundMode BorderBackgroundMode { get; }

    internal TerminalStyle ShadowStyle { get; }

    internal BackgroundMode ShadowBackgroundMode { get; }
}
