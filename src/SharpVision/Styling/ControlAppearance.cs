// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Provides shared resolved-appearance helpers for chrome renderers.</summary>
internal static class ControlAppearance
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
