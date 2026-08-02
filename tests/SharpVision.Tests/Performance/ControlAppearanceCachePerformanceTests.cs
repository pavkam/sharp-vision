// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Performance;

/// <summary>Gates the per-control resolved-appearance cache footprint.</summary>
[Collection(PerformanceGroup.Name)]
public sealed class ControlAppearanceCachePerformanceTests
{
    /// <summary>Verifies resolving one visual state allocates a small inline cache rather than the
    /// full 512-slot combinatorial VisualState space — the prior dense array allocated roughly
    /// 148 KB (296 bytes per Nullable&lt;ResolvedAppearance&gt; slot times 512) on the very first
    /// resolution, regardless of how many states a control ever actually reaches (see #114).</summary>
    [Fact]
    public void GetActualFace_WhenFirstResolved_AllocatesFarLessThanTheFullStateSpace()
    {
        var control = new ProbeControl();
        control.SetTheme(Themes.Dark);

        var before = GC.GetAllocatedBytesForCurrentThread();
        _ = control.ActualFace;
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        allocated.ShouldBeLessThan(4_096);
    }

    /// <summary>Verifies repeated CheckBox.ActualStyle reads against an unchanged Style/Theme pair
    /// reuse the memoized style instead of rebuilding a fresh 2.5 KB ThemeProfile on every read
    /// (see #179).</summary>
    [Fact]
    public void CheckBoxActualStyle_WhenReadRepeatedlyWithoutChange_AllocatesOnlyOnce()
    {
        var checkBox = new CheckBox();
        checkBox.SetTheme(Themes.Dark);
        _ = checkBox.ActualStyle;

        var before = GC.GetAllocatedBytesForCurrentThread();

        for (var index = 0; index < 50; index++)
        {
            _ = checkBox.ActualStyle;
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        allocated.ShouldBeLessThan(512);
    }

    /// <summary>Verifies repeated RadioButton.ActualStyle reads against an unchanged Style/Theme pair
    /// reuse the memoized style instead of rebuilding two fresh ThemeProfile instances on every read
    /// (see #179).</summary>
    [Fact]
    public void RadioButtonActualStyle_WhenReadRepeatedlyWithoutChange_AllocatesOnlyOnce()
    {
        var radioButton = new RadioButton();
        radioButton.SetTheme(Themes.Dark);
        _ = radioButton.ActualStyle;

        var before = GC.GetAllocatedBytesForCurrentThread();

        for (var index = 0; index < 50; index++)
        {
            _ = radioButton.ActualStyle;
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        allocated.ShouldBeLessThan(512);
    }

    /// <summary>Verifies repeated Button.ActualStyle reads against an unchanged Style/Theme pair reuse
    /// the memoized style instead of re-validating all 512 VisualState combinations on every read
    /// (see #179).</summary>
    [Fact]
    public void ButtonActualStyle_WhenReadRepeatedlyWithoutChange_DoesNotRevalidateEveryRead()
    {
        var button = new Button();
        button.SetTheme(Themes.Dark);
        _ = button.ActualStyle;

        var stopwatch = Stopwatch.StartNew();

        for (var index = 0; index < 50; index++)
        {
            _ = button.ActualStyle;
        }

        stopwatch.Stop();

        // A single uncached read costs roughly 200 microseconds (512 VisualState resolutions); 50
        // cached reads completing in under 5 ms rules out even one full re-validation slipping in,
        // with generous headroom for scheduling noise.
        stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromMilliseconds(5));
    }
}
