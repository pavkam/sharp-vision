// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>Exposes typed non-appearance Theme dependency registration for behavioral tests.</summary>
internal sealed class ThemeValueDependencyProbe: ControlBase
{
    private static readonly ThemeValueDependency<Color> _hotkeyDependency = new(
        static theme => theme.Hotkey,
        InvalidationImpact.Render);

    private static readonly ThemeValueDependency<int> _affixGapDependency = new(
        static theme => theme.GetStyleSet(InputStyle.Default).Normal.AffixGap,
        InvalidationImpact.Measure);

    /// <summary>Resolves and registers the root hotkey color.</summary>
    internal Color ResolveHotkey() => ResolveThemeValue(_hotkeyDependency);

    /// <summary>Resolves and registers the shared input affix gap.</summary>
    internal int ResolveTrackedAffixGap() => ResolveThemeValue(_affixGapDependency);

    /// <summary>Activates or removes the conditional hotkey dependency.</summary>
    internal void SetHotkeyDependency(bool active) =>
        SetThemeValueDependency(_hotkeyDependency, active);
}
