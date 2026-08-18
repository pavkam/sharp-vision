// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Performance;

/// <summary>Gates <see cref="PointerManager"/> target resolution against a redundant per-record
/// focus-ancestry walk.</summary>
/// <remarks>
/// Target resolution runs for every dispatched record - Move, Wheel, Release, and Leave included -
/// but only a primary-button press ever consumes the resolved focus ancestor. A wall-clock
/// comparison would not isolate that specific cost cleanly (geometric hit testing and capture
/// eligibility are themselves ancestry walks that scale with depth regardless of this fix, and
/// Stopwatch-ratio assertions measure the host machine as much as the product - see
/// ButtonPerformanceTests for this repo's documented preference for deterministic checks under
/// CI/coverage load). Instead, <see cref="PointerManager.FocusResolutionCount"/> - incremented once
/// per <c>FindFocusTarget</c> ancestry walk - gives a bounded, load-independent assertion: a
/// non-press record must perform zero walks, and a primary-button press must perform exactly one.
/// </remarks>
[Collection(PerformanceGroup.Name)]
public sealed class PointerManagerPerformanceTests
{
    // Kept modest deliberately: geometric hit testing and IsMember/EffectiveIsEnabled ancestor
    // walks against a single-branch chain this deep scale far worse than linearly (empirically,
    // ~200 levels x 100 iterations here completes in low single-digit seconds; a prior 3_000 x
    // 1_000 combination pushed the whole class past ten minutes and blew CI's test-run timeout).
    // FocusResolutionCount is a bounded, depth-independent assertion - it proves the same "zero
    // walks for non-press, exactly one per press" invariant at any depth, so there is no need to
    // stress ResolveTargets's own scaling here to make the point this test exists to prove.
    private const int _chainDepth = 200;
    private const int _iterations = 100;

    /// <summary>Verifies dispatching a non-press record against a deep captured ancestry never
    /// walks parents to resolve a focus target, while a primary-button press against the same
    /// ancestry walks exactly once - regardless of how many records of each are dispatched.</summary>
    [Theory]
    [InlineData(PointerAction.Move)]
    [InlineData(PointerAction.Wheel)]
    [InlineData(PointerAction.Release)]
    [InlineData(PointerAction.Leave)]
    public async Task Dispatch_OnDeepCapturedAncestry_ResolvesFocusOnlyForPrimaryPressAsync(PointerAction action)
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var (root, leaf) = BuildChain(dispatcher, _chainDepth);
            using PointerManager manager = new(root);
            manager.Capture(leaf).ShouldBeTrue();

            var outside = new Point(9_999, 9_999);
            var recordPointer = CreatePointer(action, action == PointerAction.Leave ? null : outside);
            var pressPointer = CreatePointer(PointerAction.Press, outside);

            for (var index = 0; index < _iterations; index++)
            {
                _ = manager.Dispatch(recordPointer);
            }

            manager.FocusResolutionCount.ShouldBe(0);

            for (var index = 0; index < _iterations; index++)
            {
                _ = manager.Dispatch(pressPointer);
            }

            manager.FocusResolutionCount.ShouldBe(_iterations);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a primary-button press still resolves focus to the nearest eligible
    /// ancestor when the pressed leaf itself cannot accept focus, guarding the collapsed
    /// activation/delivery-boundary branch in <see cref="PointerManager.Dispatch"/>.</summary>
    [Fact]
    public async Task Dispatch_WhenPressedLeafIsNotFocusableButAncestorIs_FocusesNearestEligibleAncestorAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer { Bounds = new Rect(0, 0, 20, 10) };
            var focusableAncestor = new ProbeContainer { Bounds = new Rect(0, 0, 20, 10), IsFocusable = true };
            var leaf = new ProbeControl { Bounds = new Rect(4, 3, 8, 4) };
            root.Children.Add(focusableAncestor);
            focusableAncestor.Children.Add(leaf);
            root.Attach(dispatcher);
            using FocusManager focus = new(root);
            using PointerManager manager = new(root);

            manager.Dispatch(CreatePointer(PointerAction.Press, new Point(6, 5))).ShouldBeSameAs(leaf);

            focus.Focused.ShouldBeSameAs(focusableAncestor);
            leaf.IsFocused.ShouldBeFalse();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Builds a chain of depth simple containers ending in a hit-testable, non-focusable
    /// leaf, attaching the root first and appending one unattached node at a time so each addition
    /// only validates the single new node instead of recursively re-validating the whole growing
    /// chain.</summary>
    private static (ProbeContainer Root, ProbeControl Leaf) BuildChain(Dispatcher dispatcher, int depth)
    {
        var root = new ProbeContainer { Bounds = new Rect(0, 0, 5, 5) };
        root.Attach(dispatcher);
        var current = root;

        for (var index = 1; index < depth; index++)
        {
            var next = new ProbeContainer();
            current.Children.Add(next);
            current = next;
        }

        var leaf = new ProbeControl();
        current.Children.Add(leaf);
        return (root, leaf);
    }

    private static Pointer CreatePointer(PointerAction action, Point? cells) => new(
        cells,
        pixels: null,
        Buttons.Primary,
        action,
        wheelX: 0,
        wheelY: action == PointerAction.Wheel ? -1 : 0,
        Modifiers.None,
        isMotion: action == PointerAction.Move,
        isCellPositionInferred: false);

}
