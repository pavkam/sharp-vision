// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Provides shared resolved-appearance helpers for chrome renderers. Named for what it
/// holds rather than for the appearance itself, so <c>ControlAppearance</c> is free to name the
/// complete face/border/shadow value.</summary>
internal static class AppearanceExtensions
{
    extension(ControlBase control)
    {
        public TerminalStyle ResolveTerminalStyle(VisualState state) =>
            control.GetResolvedAppearance(state).Style;

        public bool HasOpaqueFill(VisualState state) =>
            control.GetResolvedAppearance(state).BackgroundMode == BackgroundMode.Opaque;

        public TerminalStyle ResolveBorderStyle(VisualState state) =>
            control.GetResolvedAppearance(state).BorderStyle;
    }
}
