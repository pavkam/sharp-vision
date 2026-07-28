// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;


/// <summary>Provides semantic-color-equivalent resolution helpers for tests migrated from the v3 Color API.</summary>
internal static class ThemeColorHelper
{
    internal static Color Foreground(Theme theme) => theme.ResolveColor(ThemeColor.ControlText);

    internal static Color Background(Theme theme) => theme.ResolveColor(ThemeColor.Window);

    internal static Color Surface(Theme theme) => theme.ResolveColor(ThemeColor.Surface);

    internal static Color WindowBackground(Theme theme) => theme.ResolveColor(ThemeColor.Surface);

    internal static Color Border(Theme theme) => theme.ResolveColor(ThemeColor.ControlBorder);

    internal static Color InactiveBorder(Theme theme) => theme.ResolveColor(ThemeColor.ControlBorder);

    internal static Color DisabledBorder(Theme theme) => theme.ResolveColor(ThemeColor.DisabledBorder);

    internal static Color FocusedBorder(Theme theme) => theme.ResolveColor(ThemeColor.FocusedBorder);

    internal static Color HoveredBorder(Theme theme) => theme.ResolveColor(ThemeColor.ActiveBorder);

    internal static Color PressedBorder(Theme theme) => theme.ResolveColor(ThemeColor.PressedBorder);

    internal static Color InactiveForeground(Theme theme) => theme.ResolveColor(ThemeColor.ControlText);

    internal static Color InactiveBackground(Theme theme) => theme.ResolveColor(ThemeColor.Control);

    internal static Color DisabledForeground(Theme theme) => theme.ResolveColor(ThemeColor.DisabledText);

    internal static Color DisabledBackground(Theme theme) => theme.ResolveColor(ThemeColor.DisabledControl);

    internal static Color FocusedForeground(Theme theme) => theme.ResolveColor(ThemeColor.FocusedText);

    internal static Color FocusedBackground(Theme theme) => theme.ResolveColor(ThemeColor.FocusedControl);

    internal static Color HoveredForeground(Theme theme) => theme.ResolveColor(ThemeColor.ActiveText);

    internal static Color HoveredBackground(Theme theme) => theme.ResolveColor(ThemeColor.ActiveControl);

    internal static Color PressedForeground(Theme theme) => theme.ResolveColor(ThemeColor.PressedText);

    internal static Color PressedBackground(Theme theme) => theme.ResolveColor(ThemeColor.PressedControl);

    internal static Color Shadow(Theme theme) => theme.ResolveColor(ThemeColor.ControlShadow);

    internal static Color Accent(Theme theme) => theme.ResolveColor(ThemeColor.Accent);

    internal static Color AccentForeground(Theme theme) => theme.ResolveColor(ThemeColor.ControlText);

    internal static Color SelectionForeground(Theme theme) => theme.ResolveColor(ThemeColor.SelectedText);

    internal static Color SelectionBackground(Theme theme) => theme.ResolveColor(ThemeColor.SelectedControl);

    internal static Color Focus(Theme theme) => theme.ResolveColor(ThemeColor.FocusedText);

    internal static Color AccessKey(Theme theme) => theme.Hotkey;

    internal static Color Error(Theme theme) => theme.Error;
    internal static Color Warning(Theme theme) => theme.Warning;
    internal static Color Success(Theme theme) => theme.Success;
    internal static Color Info(Theme theme) => theme.Info;
    internal static Color Muted(Theme theme) => theme.Muted;
    internal static Color Disabled(Theme theme) => theme.Muted;
}
