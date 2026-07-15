// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies cross-cutting traversal over every central ownership slot.</summary>
public sealed class OwnedTraversalTests
{
    /// <summary>Verifies Tab navigation descends eligible slots on an owner that is not a Container.</summary>
    [Fact]
    public async Task MoveNext_WhenNonContainerOwnsFocusableControls_UsesNavigationMetadataAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new TraversalOwner();
            var first = new ProbeControl { CanFocus = true };
            var firstSlotSecond = new ProbeControl { CanFocus = true };
            var excluded = new ProbeControl { CanFocus = true };
            var second = new ProbeControl { CanFocus = true };
            root.AddNormal(first);
            root.AddNormal(firstSlotSecond);
            root.AddExcluded(excluded);
            root.AddSecondary(second);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);

            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(first);
            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(firstSlotSecond);
            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(second);
            focus.MoveNext(reverse: true).ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(firstSlotSecond);
            focus.Focus(first).ShouldBeTrue();
            focus.MoveNext(reverse: true).ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(second);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies default render and hit traversal apply slot order, layer, and eligibility metadata.</summary>
    [Fact]
    public void RenderAndHitTest_WhenNonContainerOwnsMultipleLayers_UseRegistryOrderAndFilters()
    {
        var root = new TraversalOwner { Bounds = new Rect(0, 0, 3, 1) };
        var first = new ProbeControl
        {
            Bounds = root.Bounds,
            Content = "A".AsMemory(),
        };
        var second = new ProbeControl
        {
            Bounds = root.Bounds,
            Content = "B".AsMemory(),
        };
        var excluded = new ProbeControl
        {
            Bounds = root.Bounds,
            Content = "X".AsMemory(),
        };
        var secondary = new ProbeControl
        {
            Bounds = root.Bounds,
            Content = "S".AsMemory(),
        };
        var popupFirst = new ProbeControl
        {
            Bounds = root.Bounds,
            Content = "Q".AsMemory(),
        };
        var popupLast = new ProbeControl
        {
            Bounds = root.Bounds,
            Content = "P".AsMemory(),
        };
        root.AddNormal(first);
        root.AddNormal(second);
        root.AddExcluded(excluded);
        root.AddSecondary(secondary);
        root.AddPopup(popupFirst);
        root.AddPopup(popupLast);
        using Frame frame = new(new Size(3, 1));

        root.Render(frame.Canvas);

        FrameOracle.Get(frame, default).ShouldBe("P");
        first.RenderCalls.ShouldBe(1);
        second.RenderCalls.ShouldBe(1);
        excluded.RenderCalls.ShouldBe(1);
        secondary.RenderCalls.ShouldBe(1);
        popupFirst.RenderCalls.ShouldBe(1);
        popupLast.RenderCalls.ShouldBe(1);
        root.HitTest(default).ShouldBeSameAs(popupLast);
        popupLast.IsHitTestVisible = false;
        popupFirst.IsHitTestVisible = false;
        root.HitTest(default).ShouldBeSameAs(secondary);
        secondary.IsHitTestVisible = false;
        root.HitTest(default).ShouldBeSameAs(second);
    }

    /// <summary>Verifies the generic owner can intentionally expose ordinary descendants outside its box.</summary>
    [Fact]
    public void HitTest_WhenNonContainerDoesNotClipChildren_ReachesOutsideNormalChild()
    {
        var root = new TraversalOwner
        {
            Bounds = new Rect(0, 0, 1, 1),
            ClipChildren = false,
        };
        var child = new ProbeControl { Bounds = new Rect(1, 0, 1, 1) };
        root.AddNormal(child);

        root.HitTest(new Point(1, 0)).ShouldBeSameAs(child);
    }

    /// <summary>Verifies an ineligible intermediate owner suppresses elevated descendants without clipping them by bounds.</summary>
    [Fact]
    public void HitTestPopup_WhenIntermediateOwnerIsNotHitTestVisible_SuppressesPopupSubtree()
    {
        var root = new TraversalOwner { Bounds = new Rect(0, 0, 4, 1) };
        var intermediate = new TraversalOwner
        {
            Bounds = root.Bounds,
            IsHitTestVisible = false,
        };
        var popup = new ProbeControl { Bounds = new Rect(4, 0, 1, 1) };
        intermediate.AddPopup(popup);
        root.AddNormal(intermediate);

        root.HitTest(new Point(4, 0)).ShouldNotBeSameAs(popup);
    }
}
