// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Provides shared resolved-appearance helpers for chrome renderers.</summary>
internal static class ControlAppearance
{
    internal static TerminalStyle ResolveTerminalStyle(Control control, VisualState state) =>
        control.GetResolvedAppearance(state).Style;

    internal static bool HasOpaqueFill(Control control, VisualState state) =>
        control.GetResolvedAppearance(state).BackgroundMode == BackgroundMode.Opaque;

    internal static TerminalStyle ResolveBorderStyle(Control control, VisualState state) =>
        control.GetResolvedAppearance(state).BorderStyle;
}
