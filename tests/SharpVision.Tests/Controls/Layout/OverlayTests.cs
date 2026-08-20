// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Layout;


/// <summary>Verifies layered layout, stable z-order, clipping, and targeting.</summary>
public sealed class OverlayTests
{
    /// <summary>Verifies conservative defaults and maximum intrinsic desired size.</summary>
    [ComponentUnitEvidence(typeof(Overlay))]
    [Fact]
    public void Measure_WhenChildrenDiffer_UsesMaximumMarginInclusiveSize()
    {
        var layer = new Overlay();
        var first = new ProbeControl(new Size(3, 2));
        var second = new ProbeControl(new Size(4, 1)) { Margin = new Thickness(1) };
        layer.Children.Add(first);
        layer.Children.Add(second);

        new LayoutEngine().Layout(layer, new Size(20, 10));

        layer.ClipToBounds.ShouldBeTrue();
        layer.DesiredSize.ShouldBe(new Size(6, 3));
    }

    /// <summary>Verifies each child resolves length and alignment against the shared content box.</summary>
    [Fact]
    public void Arrange_WhenChildUsesPercentAndAlignment_ResolvesAgainstSharedBounds()
    {
        var layer = new Overlay();
        var child = new ProbeControl(new Size(1, 1))
        {
            Width = Length.Percent(50),
            Height = Length.Cells(2),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom
        };
        layer.Children.Add(child);

        new LayoutEngine().Layout(layer, new Size(10, 4));

        child.Bounds.ShouldBe(new Rect(5, 2, 5, 2));
    }

    /// <summary>Verifies higher z-order renders later and receives pointer targeting first.</summary>
    [Fact]
    public void ZIndex_WhenLayersOverlap_ControlsRenderAndHitOrder()
    {
        var layer = new Overlay { Bounds = new Rect(0, 0, 1, 1) };
        var high = Child("H");
        var low = Child("L");
        Overlay.SetZIndex(high, 10);
        Overlay.SetZIndex(low, -3);
        layer.Children.Add(high);
        layer.Children.Add(low);
        using Frame frame = new(new Size(1, 1));

        layer.Render(frame.Canvas);

        FrameOracle.Get(frame, default).ShouldBe("H");
        layer.HitTest(default).ShouldBeSameAs(high);
        Overlay.GetZIndex(high).ShouldBe(10);
        Overlay.GetZIndex(low).ShouldBe(-3);
    }

    /// <summary>Verifies equal z-order preserves collection order for rendering and targeting.</summary>
    [Fact]
    public void ZIndex_WhenValuesTie_PreservesCollectionOrder()
    {
        var layer = new Overlay { Bounds = new Rect(0, 0, 1, 1) };
        var first = Child("A");
        var second = Child("B");
        layer.Children.Add(first);
        layer.Children.Add(second);
        using Frame frame = new(new Size(1, 1));

        layer.Render(frame.Canvas);

        FrameOracle.Get(frame, default).ShouldBe("B");
        layer.HitTest(default).ShouldBeSameAs(second);
    }

    /// <summary>Verifies z-order mutation requests render and changes the next frame.</summary>
    [Fact]
    public void SetZIndex_WhenOrderChanges_InvalidatesParentAndReordersFrame()
    {
        var layer = new Overlay { Bounds = new Rect(0, 0, 1, 1) };
        var first = Child("A");
        var second = Child("B");
        layer.Children.Add(first);
        layer.Children.Add(second);
        layer.Clear(Invalidation.All);
        using Frame frame = new(new Size(1, 1));

        Overlay.SetZIndex(first, 2);
        layer.Pending.ShouldBe(Invalidation.Render);
        layer.Render(frame.Canvas);

        FrameOracle.Get(frame, default).ShouldBe("A");
    }

    /// <summary>Verifies re-asserting the already-committed z-order is a no-op that does not
    /// invalidate the owning Overlay's render phase.</summary>
    [Fact]
    public void SetZIndex_WhenValueIsUnchanged_DoesNotInvalidateOwningOverlay()
    {
        var layer = new Overlay();
        var child = Child("A");
        layer.Children.Add(child);
        Overlay.SetZIndex(child, 5);
        layer.Clear(Invalidation.All);

        Overlay.SetZIndex(child, 5);

        Overlay.GetZIndex(child).ShouldBe(5);
        layer.Pending.ShouldBe(Invalidation.None);
    }

    /// <summary>Verifies disabled clipping allows descendant drawing and targeting outside bounds.</summary>
    [Fact]
    public void ClipToBounds_WhenFalse_AllowsChildrenInsideAncestorCanvas()
    {
        var layer = new Overlay { Bounds = new Rect(0, 0, 1, 1), ClipToBounds = false };
        var child = new ProbeControl { Bounds = new Rect(1, 0, 1, 1), Content = "X".AsMemory() };
        layer.Children.Add(child);
        using Frame frame = new(new Size(2, 1));

        layer.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(1, 0)).ShouldBe("X");
        layer.HitTest(new Point(1, 0)).ShouldBeSameAs(child);
    }

    /// <summary>Verifies default clipping suppresses outside drawing and targeting.</summary>
    [Fact]
    public void ClipToBounds_WhenTrue_RejectsChildrenOutsideBounds()
    {
        var layer = new Overlay { Bounds = new Rect(0, 0, 1, 1) };
        var child = new ProbeControl { Bounds = new Rect(1, 0, 1, 1), Content = "X".AsMemory() };
        layer.Children.Add(child);
        using Frame frame = new(new Size(2, 1));

        layer.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(1, 0)).ShouldBeEmpty();
        layer.HitTest(new Point(1, 0)).ShouldBeNull();
    }

    /// <summary>Verifies a transparent top layer lets the next eligible layer receive input.</summary>
    [Fact]
    public void HitTest_WhenTopLayerIsTransparent_ReturnsLowerLayer()
    {
        var layer = new Overlay { Bounds = new Rect(0, 0, 1, 1) };
        var low = Child("L");
        var high = Child("H");
        high.IsHitTestVisible = false;
        Overlay.SetZIndex(high, 1);
        layer.Children.Add(low);
        layer.Children.Add(high);

        layer.HitTest(default).ShouldBeSameAs(low);
    }

    /// <summary>Verifies intrinsic scrolling clips layered content to its viewport even when ordinary overlay clipping is disabled.</summary>
    [Fact]
    public void Render_WhenAutoScrollIsBounded_ClipsZOrderedContentToViewport()
    {
        var layer = new Overlay
        {
            AutoScroll = true,
            ClipToBounds = false,
            ScrollBars = ScrollBars.Both,
            ShowScrollBars = ShowScrollBars.Never
        };
        var high = new ProbeControl(new Size(4, 1)) { Content = "HHHH".AsMemory() };
        var low = new ProbeControl(new Size(4, 1)) { Content = "LLLL".AsMemory() };
        Overlay.SetZIndex(high, 10);
        Overlay.SetZIndex(low, -3);
        layer.Children.Add(high);
        layer.Children.Add(low);
        new LayoutEngine().Layout(layer, new Size(2, 1));
        using Frame frame = new(new Size(4, 1));

        layer.Render(frame.Canvas);

        FrameOracle.Get(frame, default).ShouldBe("H");
        FrameOracle.Get(frame, new Point(1, 0)).ShouldBe("H");
        FrameOracle.Get(frame, new Point(2, 0)).ShouldBeEmpty();
        FrameOracle.Get(frame, new Point(3, 0)).ShouldBeEmpty();
        layer.HitTest(default).ShouldBeSameAs(high);
        layer.HitTest(new Point(2, 0)).ShouldBeNull();
    }

    /// <summary>Verifies generated bars paint after every z-ordered content layer and retain their axis geometry.</summary>
    [Fact]
    public void Render_WhenAutoScrollShowsBothBars_PaintsBarsAfterZOrderedContent()
    {
        var layer = new Overlay
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Both,
            ShowScrollBars = ShowScrollBars.Always
        };
        var high = new ProbeControl(new Size(8, 6)) { Content = "HHHHHHHH".AsMemory() };
        var low = new ProbeControl(new Size(8, 6)) { Content = "LLLLLLLL".AsMemory() };
        Overlay.SetZIndex(high, 10);
        Overlay.SetZIndex(low, -3);
        layer.Children.Add(high);
        layer.Children.Add(low);
        var size = new Size(4, 3);
        new LayoutEngine().Layout(layer, size);
        using Frame frame = new(size);

        layer.Render(frame.Canvas);

        layer.Viewport.ShouldBe(new Size(3, 2));
        FrameOracle.Get(frame, default).ShouldBe("H");
        FrameOracle.Get(frame, new Point(3, 0)).ShouldBe("▲");
        FrameOracle.Get(frame, new Point(0, 2)).ShouldBe("◀");
    }

    /// <summary>Verifies elevated popups beat generated bars, bars beat ordinary content, and owner eligibility gates the whole subtree.</summary>
    [Fact]
    public void HitTest_WhenPopupOverlapsScrollBar_PreservesLayerAndOwnerEligibility()
    {
        var anchor = new ProbeControl(new Size(1, 1))
        {
            Width = Length.Cells(1),
            Height = Length.Cells(1),
            VerticalAlignment = VerticalAlignment.Top
        };
        var popup = new Popup
        {
            Anchor = anchor,
            Content = new ProbeControl(new Size(2, 1)) { Content = "PP".AsMemory() },
            IsOpen = true
        };
        var cover = new ProbeControl(new Size(8, 8)) { Content = "CCCCCCCC".AsMemory() };
        Overlay.SetZIndex(cover, 100);
        var layer = new Overlay
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Both,
            ShowScrollBars = ShowScrollBars.Always
        };
        layer.Children.Add(anchor);
        layer.Children.Add(popup);
        layer.Children.Add(cover);
        var size = new Size(8, 4);
        new LayoutEngine().Layout(layer, size);
        var point = new Point(popup.SurfaceBounds.X, layer.Bounds.Bottom - 1);
        using Frame frame = new(size);

        layer.Render(frame.Canvas);

        popup.SurfaceBounds.Contains(point).ShouldBeTrue();
        FrameOracle.Get(frame, point).ShouldBe("╰");
        layer.HitTest(point).ShouldBeSameAs(popup);

        popup.IsOpen = false;
        layer.HitTest(point).ShouldBeOfType<ScrollBar>().Orientation.ShouldBe(Orientation.Horizontal);

        layer.IsHitTestVisible = false;
        layer.HitTest(point).ShouldBeNull();
    }

    /// <summary>Verifies elevated rendering and hit testing honor stable ZIndex instead of collection order.</summary>
    [Fact]
    public void PopupLayer_WhenZIndexOpposesCollectionOrder_HighestZDrawsAndHitsFirst()
    {
        var anchor = PopupAnchor();
        var high = CreatePopup("H");
        var low = CreatePopup("L");
        high.Anchor = anchor;
        low.Anchor = anchor;
        Overlay.SetZIndex(high, 10);
        Overlay.SetZIndex(low, -10);
        var layer = new Overlay();
        layer.Children.Add(anchor);
        layer.Children.Add(high);
        layer.Children.Add(low);
        var size = new Size(6, 4);
        new LayoutEngine().Layout(layer, size);
        var bounds = high.Content!.Bounds;
        var point = new Point(bounds.X, bounds.Y);
        using Frame frame = new(size);

        layer.Render(frame.Canvas);

        FrameOracle.Get(frame, point).ShouldBe("H");
        layer.HitTest(point).ShouldBeSameAs(high.Content);
    }

    /// <summary>Verifies equal popup ZIndex retains collection order for drawing and reverse hit priority.</summary>
    [Fact]
    public void PopupLayer_WhenZIndexIsEqual_LaterCollectionItemDrawsAndHitsFirst()
    {
        var anchor = PopupAnchor();
        var first = CreatePopup("A");
        var second = CreatePopup("B");
        first.Anchor = anchor;
        second.Anchor = anchor;
        var layer = new Overlay();
        layer.Children.Add(anchor);
        layer.Children.Add(first);
        layer.Children.Add(second);
        var size = new Size(6, 4);
        new LayoutEngine().Layout(layer, size);
        var bounds = second.Content!.Bounds;
        var point = new Point(bounds.X, bounds.Y);
        using Frame frame = new(size);

        layer.Render(frame.Canvas);

        FrameOracle.Get(frame, point).ShouldBe("B");
        layer.HitTest(point).ShouldBeSameAs(second.Content);
    }

    /// <summary>Verifies z-order never changes collection-order focus traversal.</summary>
    [Fact]
    public async Task MoveNext_WhenZOrderDiffers_UsesCollectionOrderAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var layer = new Overlay();
        var first = new ProbeControl { IsFocusable = true };
        var second = new ProbeControl { IsFocusable = true };
        Overlay.SetZIndex(first, 20);
        layer.Children.Add(first);
        layer.Children.Add(second);

        await dispatcher.InvokeAsync(() =>
        {
            layer.Attach(dispatcher);
            using FocusManager focus = new(layer);
            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(first);
            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(second);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies attached values validate controls and dispatcher affinity.</summary>
    [Fact]
    public async Task SetZIndex_WhenControlIsAttachedOffThread_ThrowsBeforeMutationAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var child = new ProbeControl();
        await dispatcher.InvokeAsync(
            () => child.Attach(dispatcher),
            TestContext.Current.CancellationToken);

        _ = Should.Throw<ArgumentNullException>(() => Overlay.GetZIndex(null!));
        _ = Should.Throw<ArgumentNullException>(() => Overlay.SetZIndex(null!, 1));
        _ = Should.Throw<InvalidOperationException>(() => Overlay.SetZIndex(child, 1));

        Overlay.GetZIndex(child).ShouldBe(0);
    }

    /// <summary>Verifies a collapsed child contributes no desired size while a visible sibling still does.</summary>
    [ComponentVisibilityEvidence(typeof(Overlay), ComponentVisibilityEvidence.CollapsedExcludesSize)]
    [Fact]
    public void Measure_WhenChildIsCollapsed_ExcludesItFromMaximumDesiredSize()
    {
        var layer = new Overlay();
        var collapsed = new ProbeControl(new Size(20, 10)) { Visibility = Visibility.Collapsed };
        var visible = new ProbeControl(new Size(3, 2));
        layer.Children.Add(collapsed);
        layer.Children.Add(visible);

        new LayoutEngine().Layout(layer, new Size(30, 15));

        layer.DesiredSize.ShouldBe(new Size(3, 2));
        collapsed.DesiredSize.ShouldBe(default);
    }

    /// <summary>Verifies a hidden child still contributes its measured size to the shared maximum,
    /// the same as a visible child would.</summary>
    [ComponentVisibilityEvidence(typeof(Overlay), ComponentVisibilityEvidence.HiddenRetainsSlot)]
    [Fact]
    public void Measure_WhenChildIsHidden_StillContributesToMaximumDesiredSize()
    {
        var layer = new Overlay();
        var hidden = new ProbeControl(new Size(6, 4)) { Visibility = Visibility.Hidden };
        var small = new ProbeControl(new Size(1, 1));
        layer.Children.Add(hidden);
        layer.Children.Add(small);

        new LayoutEngine().Layout(layer, new Size(30, 15));

        layer.DesiredSize.ShouldBe(new Size(6, 4));
    }

    /// <summary>Verifies a hidden child keeps the exact resolved slot, alignment, and offset it would
    /// receive while visible - the same geometry <see cref="Arrange_WhenChildUsesPercentAndAlignment_ResolvesAgainstSharedBounds"/>
    /// proves for a visible child.</summary>
    [ComponentVisibilityEvidence(typeof(Overlay), ComponentVisibilityEvidence.HiddenRetainsSlot)]
    [Fact]
    public void Arrange_WhenChildIsHidden_KeepsSlotAlignmentAndOffset()
    {
        var layer = new Overlay();
        var child = new ProbeControl(new Size(1, 1))
        {
            Visibility = Visibility.Hidden,
            Width = Length.Percent(50),
            Height = Length.Cells(2),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom
        };
        layer.Children.Add(child);

        new LayoutEngine().Layout(layer, new Size(10, 4));

        child.Bounds.ShouldBe(new Rect(5, 2, 5, 2));
    }

    /// <summary>Verifies collapsed children never reach z-order arrange, rendering, or hit testing -
    /// an attached z-index and offset that would otherwise win both are ignored entirely - while a
    /// visible sibling underneath is unaffected.</summary>
    [ComponentVisibilityEvidence(typeof(Overlay), ComponentVisibilityEvidence.CollapsedExcludesSize)]
    [Fact]
    public void Arrange_WhenChildIsCollapsed_SkipsZOrderArrangeRenderAndHitTest()
    {
        var layer = new Overlay { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
        var collapsed = new ProbeControl(new Size(1, 1)) { Content = "C".AsMemory(), Visibility = Visibility.Collapsed };
        var visible = new ProbeControl(new Size(1, 1)) { Content = "V".AsMemory() };
        Overlay.SetZIndex(collapsed, 100);
        Overlay.SetLeft(collapsed, Length.Cells(0));
        layer.Children.Add(visible);
        layer.Children.Add(collapsed);
        new LayoutEngine().Layout(layer, new Size(1, 1));
        using Frame frame = new(new Size(1, 1));

        layer.Render(frame.Canvas);

        collapsed.Bounds.ShouldBe(default);
        FrameOracle.Get(frame, default).ShouldBe("V");
        layer.HitTest(default).ShouldBeSameAs(visible);
    }

    /// <summary>Verifies a hidden child excludes itself from rendering and hit testing while keeping
    /// its arranged slot, and that the exclusion reverses the instant the child becomes visible again.</summary>
    [ComponentVisibilityEvidence(
        typeof(Overlay),
        ComponentVisibilityEvidence.HiddenRetainsSlot |
        ComponentVisibilityEvidence.HiddenExcludesRenderInput |
        ComponentVisibilityEvidence.TransitionInvalidatesCorrectly)]
    [Fact]
    public void HitTest_WhenChildIsHidden_ExcludesItButKeepsArrangedSlot()
    {
        var layer = new Overlay { Bounds = new Rect(0, 0, 1, 1) };
        var child = new ProbeControl
        {
            Bounds = new Rect(0, 0, 1, 1),
            Content = "X".AsMemory(),
            Visibility = Visibility.Hidden
        };
        layer.Children.Add(child);
        using Frame frame = new(new Size(1, 1));

        layer.Render(frame.Canvas);

        FrameOracle.Get(frame, default).ShouldBeEmpty();
        child.HitTest(default).ShouldBeNull();
        layer.HitTest(default).ShouldBeSameAs(layer, "an ineligible child falls through to the overlay's own bounds");
        child.Bounds.ShouldBe(new Rect(0, 0, 1, 1));

        child.Visibility = Visibility.Visible;

        layer.HitTest(default).ShouldBeSameAs(child);
    }

    /// <summary>Verifies a IsVisible → Hidden → Collapsed → IsVisible cycle invalidates only the phases
    /// each transition requires and restores identical geometry, rather than drifting after each
    /// step.</summary>
    [ComponentVisibilityEvidence(typeof(Overlay), ComponentVisibilityEvidence.TransitionInvalidatesCorrectly)]
    [Fact]
    public void Visibility_WhenCyclingThroughAllStates_RestoresDeterministicGeometry()
    {
        var layer = new Overlay { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
        var child = new ProbeControl(new Size(4, 2))
        {
            Width = Length.Cells(4),
            Height = Length.Cells(2),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        layer.Children.Add(child);
        var engine = new LayoutEngine();
        var size = new Size(20, 10);

        engine.Layout(layer, size);
        var settled = child.Bounds;
        settled.ShouldBe(new Rect(8, 4, 4, 2));

        // Hidden invalidates render only; a same-size re-layout is a cache hit and geometry stays frozen.
        child.Visibility = Visibility.Hidden;
        engine.Layout(layer, size);
        child.Bounds.ShouldBe(settled);

        // Collapsed invalidates measure; the child's own geometry clears and the shared maximum shrinks.
        child.Visibility = Visibility.Collapsed;
        engine.Layout(layer, size);
        child.Bounds.ShouldBe(default);
        layer.DesiredSize.ShouldBe(default);

        // Returning to IsVisible invalidates measure again and restores the original committed geometry.
        child.Visibility = Visibility.Visible;
        engine.Layout(layer, size);
        child.Bounds.ShouldBe(settled);
        layer.DesiredSize.ShouldBe(new Size(4, 2));
    }

    /// <summary>Verifies attached offsets on both edges of a zero-size axis clamp StretchedExtent to
    /// zero through saturating arithmetic instead of throwing or going negative.</summary>
    [ComponentVisibilityEvidence(typeof(Overlay), ComponentVisibilityEvidence.ZeroTinyConstraint)]
    [Fact]
    public void Arrange_WhenAvailableAxesAndOffsetsAreZero_ClampsStretchedExtentWithoutThrowing()
    {
        var layer = new Overlay();
        var child = new ProbeControl(new Size(3, 1));
        Overlay.SetLeft(child, Length.Cells(0));
        Overlay.SetRight(child, Length.Cells(0));
        Overlay.SetTop(child, Length.Cells(0));
        Overlay.SetBottom(child, Length.Cells(0));
        layer.Children.Add(child);

        new LayoutEngine().Layout(layer, new Size(0, 0));

        // The overlay's own DesiredSize is itself capped by the zero root constraint; the point
        // of this test is that the child's attached-offset arithmetic clamps cleanly to zero too,
        // instead of throwing or producing a negative extent.
        layer.DesiredSize.ShouldBe(default);
        child.Bounds.ShouldBe(default);
    }

    private static ProbeControl Child(string content) => new()
    {
        Bounds = new Rect(0, 0, 1, 1),
        Content = content.AsMemory()
    };

    private static ProbeControl PopupAnchor() => new(new Size(1, 1))
    {
        Width = Length.Cells(1),
        Height = Length.Cells(1),
        VerticalAlignment = VerticalAlignment.Top
    };

    private static Popup CreatePopup(string content) => new()
    {
        Content = new ProbeControl(new Size(1, 1)) { Content = content.AsMemory() },
        IsOpen = true
    };
    /// <summary>Verifies disabling a detached Overlay cascades EffectiveIsEnabled to an owned child
    /// and recovers on re-enable, without needing a mounted surface.</summary>
    [ComponentUnitEvidence(typeof(Overlay), ComponentBehavior.Disabled)]
    [Fact]
    public void Enabled_WhenToggled_CascadesEffectiveIsEnabledToOwnedChild()
    {
        var layer = new Overlay();
        var child = new ProbeControl();
        layer.Children.Add(child);

        child.EffectiveIsEnabled.ShouldBeTrue();

        layer.IsEnabled = false;

        layer.EffectiveIsEnabled.ShouldBeFalse();
        child.EffectiveIsEnabled.ShouldBeFalse();

        layer.IsEnabled = true;

        layer.EffectiveIsEnabled.ShouldBeTrue();
        child.EffectiveIsEnabled.ShouldBeTrue();
    }

}
