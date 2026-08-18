// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Layout;

/// <summary>Gates track resolution allocation for the caller-owned span overload.</summary>
public sealed class TrackPerformanceTests
{
    /// <summary>Verifies the caller-owned span overload allocates no managed memory.</summary>
    [Fact]
    public void Resolve_WhenCallerOwnsStorage_AllocatesNoManagedMemoryAfterWarmup()
    {
        ReadOnlySpan<Length> lengths =
            [Length.Cells(3), Length.Percent(25), Length.Star(1), Length.Star(2)];
        ReadOnlySpan<int> automatic = [0, 0, 0, 0];
        ReadOnlySpan<int> minimum = [0, 0, 0, 0];
        ReadOnlySpan<int> maximum = [10, 20, 100, 100];
        Span<int> destination = stackalloc int[4];
        Tracks.Resolve(80, lengths, automatic, minimum, maximum, destination);
        var before = GC.GetAllocatedBytesForCurrentThread();

        for (var iteration = 0; iteration < 1_000; iteration++)
        {
            Tracks.Resolve(80, lengths, automatic, minimum, maximum, destination);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        allocated.ShouldBe(0);
    }
}
