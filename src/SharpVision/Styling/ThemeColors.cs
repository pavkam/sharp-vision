// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

using SharpVision.Terminal.Protocols;

/// <summary>Semantic theme colors as first-class <see cref="Color"/> values resolved by the active theme.</summary>
/// <remarks>
/// Each property is a deferred role color; assign it to any color style property (for example
/// <c>Background = ThemeColors.Accent</c>) and it resolves to the active theme's palette value during
/// property resolution, tracking theme swaps automatically.
/// </remarks>
public static class ThemeColors
{
    /// <summary>The default text color.</summary>
    public static Color Foreground { get; } = Color.Role((int) ColorRole.Foreground);

    /// <summary>The default surface color behind content.</summary>
    public static Color Background { get; } = Color.Role((int) ColorRole.Background);

    /// <summary>A raised or inset surface color.</summary>
    public static Color Surface { get; } = Color.Role((int) ColorRole.Surface);

    /// <summary>The default border and separator color.</summary>
    public static Color Border { get; } = Color.Role((int) ColorRole.Border);

    /// <summary>The primary emphasis color.</summary>
    public static Color Accent { get; } = Color.Role((int) ColorRole.Accent);

    /// <summary>A low-emphasis foreground for secondary content.</summary>
    public static Color Muted { get; } = Color.Role((int) ColorRole.Muted);

    /// <summary>The background color of a selected item.</summary>
    public static Color SelectionBackground { get; } = Color.Role((int) ColorRole.SelectionBackground);

    /// <summary>The text color of a selected item.</summary>
    public static Color SelectionForeground { get; } = Color.Role((int) ColorRole.SelectionForeground);

    /// <summary>The color signaling an error state.</summary>
    public static Color Error { get; } = Color.Role((int) ColorRole.Error);

    /// <summary>The color signaling a caution state.</summary>
    public static Color Warning { get; } = Color.Role((int) ColorRole.Warning);

    /// <summary>The color signaling a successful state.</summary>
    public static Color Success { get; } = Color.Role((int) ColorRole.Success);

    /// <summary>The color signaling neutral informational emphasis.</summary>
    public static Color Info { get; } = Color.Role((int) ColorRole.Info);
}
