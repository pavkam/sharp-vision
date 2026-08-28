// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies typed registration of Theme values resolved outside appearance profiles.</summary>
public sealed class ThemeValueDependencyTests
{
    /// <summary>Verifies repeated reads deduplicate one registration and a changed value requests
    /// its declared phase.</summary>
    [Fact]
    public void ResolveThemeValue_WhenReadRepeatedly_RegistersOnceAndInvalidatesDeclaredPhase()
    {
        var first = ThemeCatalog.Parse(ThemeJson.Create(hotkey: "#ff0000"));
        var second = ThemeCatalog.Parse(ThemeJson.Create(hotkey: "#00ff00"));
        var probe = new ThemeValueDependencyProbe();
        probe.SetTheme(first);

        probe.ResolveHotkey().ShouldBe(first.Hotkey);
        probe.ResolveHotkey().ShouldBe(first.Hotkey);
        probe.ThemeValueDependencyCount.ShouldBe(1);
        probe.Clear(Invalidation.All);
        probe.SetTheme(second);

        probe.Pending.ShouldBe(Invalidation.Render);
    }

    /// <summary>Verifies a warmed descriptor read performs no managed allocation while preserving
    /// the single registration.</summary>
    [Fact]
    public void ResolveThemeValue_WhenRegistrationIsWarm_AllocatesNothingPerRead()
    {
        var probe = new ThemeValueDependencyProbe();
        probe.SetTheme(ThemeCatalog.Dark);
        _ = probe.ResolveHotkey();

        for (var index = 0; index < 1_000; index++)
        {
            _ = probe.ResolveHotkey();
        }

        var minimum = long.MaxValue;

        for (var sample = 0; sample < 5; sample++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();

            for (var index = 0; index < 1_000; index++)
            {
                _ = probe.ResolveHotkey();
            }

            minimum = Math.Min(minimum, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        minimum.ShouldBe(0);
        probe.ThemeValueDependencyCount.ShouldBe(1);
    }

    /// <summary>Verifies a dependency cannot affect a Theme transition before its first read.</summary>
    [Fact]
    public void SetTheme_WhenDependencyHasNotBeenRead_DoesNotInvalidateForItsValue()
    {
        var first = ThemeCatalog.Parse(ThemeJson.Create(hotkey: "#ff0000"));
        var second = ThemeCatalog.Parse(ThemeJson.Create(hotkey: "#00ff00"));
        var probe = new ThemeValueDependencyProbe();
        probe.SetTheme(first);
        probe.Clear(Invalidation.All);

        probe.SetTheme(second);

        probe.Pending.ShouldBe(Invalidation.None);
        probe.ThemeValueDependencyCount.ShouldBe(0);
    }

    /// <summary>Verifies the strongest registered dependency wins when one Theme swap changes
    /// both layout and paint values.</summary>
    [Fact]
    public void SetTheme_WhenMeasureAndRenderDependenciesChange_UsesMeasure()
    {
        var first = ThemeCatalog.Parse(ThemeJson.Create(
            hotkey: "#ff0000",
            inputExtra: ", \"affixGap\": 1"));
        var second = ThemeCatalog.Parse(ThemeJson.Create(
            hotkey: "#00ff00",
            inputExtra: ", \"affixGap\": 3"));
        var probe = new ThemeValueDependencyProbe();
        probe.SetTheme(first);
        _ = probe.ResolveHotkey();
        _ = probe.ResolveTrackedAffixGap();
        probe.Clear(Invalidation.All);

        probe.SetTheme(second);

        probe.Pending.ShouldBe(Invalidation.All);
        probe.ThemeValueDependencyCount.ShouldBe(2);
    }

    /// <summary>Verifies equal concrete resolver output suppresses invalidation even when the two
    /// themes name different palette entries.</summary>
    [Fact]
    public void SetTheme_WhenSymbolicInputsResolveEqually_DoesNotInvalidate()
    {
        var first = ThemeCatalog.Parse(ThemeJson.Create(
            palette: "\"firstHotkey\":\"#ff0000\",\"bg\":\"#101010\",\"fg\":\"#e0e0e0\"",
            hotkey: "firstHotkey"));
        var second = ThemeCatalog.Parse(ThemeJson.Create(
            palette: "\"secondHotkey\":\"#ff0000\",\"bg\":\"#101010\",\"fg\":\"#e0e0e0\"",
            hotkey: "secondHotkey"));
        var probe = new ThemeValueDependencyProbe();
        probe.SetTheme(first);
        _ = probe.ResolveHotkey();
        probe.Clear(Invalidation.All);

        probe.SetTheme(second);

        probe.Pending.ShouldBe(Invalidation.None);
    }

    /// <summary>Verifies a conditional dependency can be removed after its consuming path becomes
    /// inactive.</summary>
    [Fact]
    public void SetThemeValueDependency_WhenDeactivated_RemovesFutureInvalidation()
    {
        var first = ThemeCatalog.Parse(ThemeJson.Create(hotkey: "#ff0000"));
        var second = ThemeCatalog.Parse(ThemeJson.Create(hotkey: "#00ff00"));
        var probe = new ThemeValueDependencyProbe();
        probe.SetTheme(first);
        _ = probe.ResolveHotkey();
        probe.SetHotkeyDependency(false);
        probe.Clear(Invalidation.All);

        probe.SetTheme(second);

        probe.Pending.ShouldBe(Invalidation.None);
        probe.ThemeValueDependencyCount.ShouldBe(0);
    }
}
