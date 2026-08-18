// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Performance;

/// <summary>Gates <see cref="ControlBase.HitTest"/> against a redundant per-level ancestry walk
/// while resolving <see cref="ControlBase.EffectiveIsVisible"/>/<see cref="ControlBase.EffectiveIsEnabled"/>.</summary>
/// <remarks>
/// <see cref="ControlBase.CanHitTestSelf"/> reads both effective-state properties at every level of
/// <see cref="ControlBase.HitTest"/>'s recursive descent. Before memoization, each of those reads
/// walked to the root, making one full hit test against a chain of depth N cost O(N^2) rather than
/// O(N). A wall-clock comparison would not isolate that specific cost cleanly - Stopwatch-ratio
/// assertions measure the host machine as much as the product (see
/// <c>PointerManagerPerformanceTests</c> and <c>ButtonPerformanceTests</c> for this repo's
/// documented preference for deterministic checks under CI/coverage load). Instead,
/// <see cref="ControlBase.EffectiveStateComputationCount"/> - incremented only on a genuine cache
/// miss - gives a bounded, load-independent assertion. Every assertion here measures the DELTA this
/// count grows by across one <see cref="ControlBase.HitTest"/> call, rather than an absolute count,
/// since attaching a control to a parent (<c>Children.Add</c>) legitimately performs its own,
/// unrelated effective-state read for initial appearance resolution before any hit test runs.
/// </remarks>
[Collection(PerformanceGroup.Name)]
public sealed class ControlBasePerformanceTests
{
    // Kept modest deliberately, mirroring PointerManagerPerformanceTests's own right-sizing note:
    // a single-branch chain this deep already demonstrates the O(N) vs O(N^2) distinction clearly
    // (a quadratic regression here would need ~500x the computations a linear one does), without
    // resurrecting the multi-minute runtime a much deeper chain caused before that class was
    // right-sized in 145bdb64.
    private const int _chainDepth = 500;

    /// <summary>A point contained by every level's bounds, so the descent reaches the leaf.</summary>
    private static readonly Point _probePoint = new(2, 2);

    /// <summary>Verifies one hit test against a deep chain grows the whole chain's total effective-
    /// state computation count only linearly with depth, and a following hit test against the same,
    /// unchanged chain recomputes nothing at all.</summary>
    [Fact]
    public void HitTest_OnDeepChain_GrowsComputationCountLinearlyWithDepthThenNotAtAll()
    {
        var (root, _, chain) = BuildChain(_chainDepth);
        var beforeFirstHitTest = TotalComputations(chain);

        _ = root.HitTest(_probePoint);

        var firstHitTestDelta = TotalComputations(chain) - beforeFirstHitTest;

        // A quadratic descent recomputes O(depth) work at every one of the depth levels examined,
        // for O(depth^2) total; a linear one recomputes each node's two effective-state properties
        // at most once, for at most 2 * depth. At depth 500, quadratic is ~125,000 while linear is
        // at most 1,000 - asserting well under the quadratic bound cleanly separates the two shapes.
        firstHitTestDelta.ShouldBeGreaterThan(0);
        firstHitTestDelta.ShouldBeLessThan(_chainDepth * 4);

        _ = root.HitTest(_probePoint);

        TotalComputations(chain).ShouldBe(beforeFirstHitTest + firstHitTestDelta);
    }

    /// <summary>Verifies that once the root is disabled - a change whose OWN cascade already walks
    /// every descendant to repair its chrome geometry, an unrelated, pre-existing O(depth) cost this
    /// test does not touch - a following hit test observes the change without any further per-
    /// descendant recomputation of its own, since <see cref="ControlBase.CanHitTestSelf"/> now
    /// short-circuits at the (already-disabled) root before ever descending into children.</summary>
    [Fact]
    public void HitTest_AfterRootIsEnabledToggles_AddsNoFurtherRecomputationOfItsOwn()
    {
        var (root, leaf, chain) = BuildChain(_chainDepth);
        _ = root.HitTest(_probePoint);

        root.IsEnabled = false;

        // Captured AFTER the setter's own subtree-wide chrome-repair cascade completes, so this
        // isolates exactly what the FOLLOWING hit test's own descent adds on top of that.
        var baselineAfterDisable = TotalComputations(chain);
        var blocked = root.HitTest(_probePoint);

        blocked.ShouldBeNull();
        (TotalComputations(chain) - baselineAfterDisable).ShouldBeLessThanOrEqualTo(2);

        root.IsEnabled = true;
        var resolved = root.HitTest(_probePoint);

        resolved.ShouldBeSameAs(leaf);
    }

    private static int TotalComputations(List<ControlBase> chain) =>
        chain.Sum(control => control.EffectiveStateComputationCount);

    /// <summary>Builds a chain of depth simple containers ending in a hit-testable leaf, all sharing
    /// the same absolute bounds so every level contains the probe point.</summary>
    private static (ProbeContainer Root, ProbeControl Leaf, List<ControlBase> Chain) BuildChain(int depth)
    {
        var bounds = new Rect(0, 0, 5, 5);
        var root = new ProbeContainer { Bounds = bounds };
        var chain = new List<ControlBase> { root };
        var current = root;

        for (var index = 1; index < depth; index++)
        {
            var next = new ProbeContainer { Bounds = bounds };
            current.Children.Add(next);
            chain.Add(next);
            current = next;
        }

        var leaf = new ProbeControl { Bounds = bounds };
        current.Children.Add(leaf);
        chain.Add(leaf);
        return (root, leaf, chain);
    }
}
