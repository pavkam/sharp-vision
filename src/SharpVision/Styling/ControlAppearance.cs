// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Provides shared resolved-appearance helpers for chrome renderers.</summary>
internal static class ControlAppearance
{
    public static TerminalStyle ResolveTerminalStyle(Control control, VisualState state) =>
        control.GetResolvedAppearance(state).Style;

    public static bool HasOpaqueFill(Control control, VisualState state) =>
        control.GetResolvedAppearance(state).BackgroundMode == BackgroundMode.Opaque;

    public static TerminalStyle ResolveBorderStyle(Control control, VisualState state) =>
        control.GetResolvedAppearance(state).BorderStyle;
}
