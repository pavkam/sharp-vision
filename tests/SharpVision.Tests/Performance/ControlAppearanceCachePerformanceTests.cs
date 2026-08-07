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
    /// resolution, regardless of how many states a control ever actually reaches.</summary>
    [Fact]
    public void GetActualFace_WhenFirstResolved_AllocatesFarLessThanTheFullStateSpace()
    {
        var control = new ProbeControl();
        control.SetTheme(ThemeCatalog.Dark);

        var before = GC.GetAllocatedBytesForCurrentThread();
        _ = control.ActualFace;
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        allocated.ShouldBeLessThan(4_096);
    }

    /// <summary>Verifies repeated CheckBox.ActualStyle reads against an unchanged Style/Theme pair
    /// reuse the memoized style instead of rebuilding a fresh 2.5 KB AppearanceStates on every read.</summary>
    [Fact]
    public void CheckBoxActualStyle_WhenReadRepeatedlyWithoutChange_AllocatesOnlyOnce()
    {
        var checkBox = new CheckBox();
        checkBox.SetTheme(ThemeCatalog.Dark);
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
    /// reuse the memoized style instead of rebuilding two fresh AppearanceStates instances on every read.</summary>
    [Fact]
    public void RadioButtonActualStyle_WhenReadRepeatedlyWithoutChange_AllocatesOnlyOnce()
    {
        var radioButton = new RadioButton();
        radioButton.SetTheme(ThemeCatalog.Dark);
        _ = radioButton.ActualStyle;

        var before = GC.GetAllocatedBytesForCurrentThread();

        for (var index = 0; index < 50; index++)
        {
            _ = radioButton.ActualStyle;
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        allocated.ShouldBeLessThan(512);
    }

    // Button.ActualStyle's cache-reuse cost has no dedicated regression test here: unlike
    // CheckBox/RadioButton above, its revalidation path does not necessarily allocate, so proving
    // it is skipped would require either a wall-clock budget or a mutable instance counter on
    // StyleSlot<TStyle> solely for test observability, and neither belongs in that stateless-by-
    // design cache. The fix's correctness is covered by ordinary ButtonTests coverage; its cost is
    // documented on StyleSlot<TStyle>.Actual and enforced by code review.
}
