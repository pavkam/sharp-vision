// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

internal readonly struct ResolvedAppearance
{
    internal ResolvedAppearance(
        TerminalStyle style,
        BackgroundMode backgroundMode,
        TerminalStyle borderStyle,
        TerminalStyle shadowStyle)
    {
        Style = style;
        BackgroundMode = backgroundMode;
        BorderStyle = borderStyle;
        ShadowStyle = shadowStyle;
    }

    internal TerminalStyle Style { get; }

    internal BackgroundMode BackgroundMode { get; }

    internal TerminalStyle BorderStyle { get; }

    internal TerminalStyle ShadowStyle { get; }
}
