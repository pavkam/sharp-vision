// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Identifies one semantic color role a theme resolves independently of control type.</summary>
/// <remarks>
/// Semantic roles let a control ask the active theme for "the accent color" or "the border color"
/// rather than hardcoding palette indices, so a third-party control tracks theme swaps consistently
/// with the built-in controls.
/// </remarks>
public enum ColorRole
{
    /// <summary>The default text color.</summary>
    Foreground,

    /// <summary>The default surface color behind content.</summary>
    Background,

    /// <summary>A raised or inset surface color distinct from the base background.</summary>
    Surface,

    /// <summary>The default border and separator color.</summary>
    Border,

    /// <summary>The primary emphasis color for focus and active affordances.</summary>
    Accent,

    /// <summary>A low-emphasis foreground for secondary text.</summary>
    Muted,

    /// <summary>The background color of a selected item.</summary>
    Selection,
}
