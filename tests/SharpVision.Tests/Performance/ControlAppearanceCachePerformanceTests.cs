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
}
