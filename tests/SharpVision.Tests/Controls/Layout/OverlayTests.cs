// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Layout;

using Layer = Overlay;

/// <summary>Verifies layered layout, stable z-order, clipping, and targeting.</summary>
public sealed class OverlayTests
{
    /// <summary>Verifies conservative defaults and maximum intrinsic desired size.</summary>
    [ComponentUnitEvidence(typeof(Layer))]
    [Fact]
    public void Measure_WhenChildrenDiffer_UsesMaximumMarginInclusiveSize()
    {
        var layer = new Layer();
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
        var layer = new Layer();
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
        var layer = new Layer { Bounds = new Rect(0, 0, 1, 1) };
        var high = Child("H");
        var low = Child("L");
        Layer.SetZIndex(high, 10);
        Layer.SetZIndex(low, -3);
        layer.Children.Add(high);
        layer.Children.Add(low);
        using Frame frame = new(new Size(1, 1));

        layer.Render(frame.Canvas);

        FrameOracle.Get(frame, default).ShouldBe("H");
        layer.HitTest(default).ShouldBeSameAs(high);
        Layer.GetZIndex(high).ShouldBe(10);
        Layer.GetZIndex(low).ShouldBe(-3);
    }

    /// <summary>Verifies equal z-order preserves collection order for rendering and targeting.</summary>
    [Fact]
    public void ZIndex_WhenValuesTie_PreservesCollectionOrder()
    {
        var layer = new Layer { Bounds = new Rect(0, 0, 1, 1) };
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
        var layer = new Layer { Bounds = new Rect(0, 0, 1, 1) };
        var first = Child("A");
        var second = Child("B");
        layer.Children.Add(first);
        layer.Children.Add(second);
        layer.Clear(Invalidation.All);
        using Frame frame = new(new Size(1, 1));

        Layer.SetZIndex(first, 2);
        layer.Pending.ShouldBe(Invalidation.Render);
        layer.Render(frame.Canvas);

        FrameOracle.Get(frame, default).ShouldBe("A");
    }

    /// <summary>Verifies disabled clipping allows descendant drawing and targeting outside bounds.</summary>
    [Fact]
    public void ClipToBounds_WhenFalse_AllowsChildrenInsideAncestorCanvas()
    {
        var layer = new Layer { Bounds = new Rect(0, 0, 1, 1), ClipToBounds = false };
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
        var layer = new Layer { Bounds = new Rect(0, 0, 1, 1) };
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
        var layer = new Layer { Bounds = new Rect(0, 0, 1, 1) };
        var low = Child("L");
        var high = Child("H");
        high.IsHitTestVisible = false;
        Layer.SetZIndex(high, 1);
        layer.Children.Add(low);
        layer.Children.Add(high);

        layer.HitTest(default).ShouldBeSameAs(low);
    }

    /// <summary>Verifies intrinsic scrolling clips layered content to its viewport even when ordinary overlay clipping is disabled.</summary>
    [Fact]
    public void Render_WhenAutoScrollIsBounded_ClipsZOrderedContentToViewport()
    {
        var layer = new Layer
        {
            AutoScroll = true,
            ClipToBounds = false,
            ScrollBars = ScrollBars.Both,
            ShowScrollBars = ShowScrollBars.Never
        };
        var high = new ProbeControl(new Size(4, 1)) { Content = "HHHH".AsMemory() };
        var low = new ProbeControl(new Size(4, 1)) { Content = "LLLL".AsMemory() };
        Layer.SetZIndex(high, 10);
        Layer.SetZIndex(low, -3);
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
        var layer = new Layer
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Both,
            ShowScrollBars = ShowScrollBars.Always
        };
        var high = new ProbeControl(new Size(8, 6)) { Content = "HHHHHHHH".AsMemory() };
        var low = new ProbeControl(new Size(8, 6)) { Content = "LLLLLLLL".AsMemory() };
        Layer.SetZIndex(high, 10);
        Layer.SetZIndex(low, -3);
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
        Layer.SetZIndex(cover, 100);
        var layer = new Layer
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
        Layer.SetZIndex(high, 10);
        Layer.SetZIndex(low, -10);
        var layer = new Layer();
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
        var layer = new Layer();
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
        var layer = new Layer();
        var first = new ProbeControl { Focusable = true };
        var second = new ProbeControl { Focusable = true };
        Layer.SetZIndex(first, 20);
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

        _ = Should.Throw<ArgumentNullException>(() => Layer.GetZIndex(null!));
        _ = Should.Throw<ArgumentNullException>(() => Layer.SetZIndex(null!, 1));
        _ = Should.Throw<InvalidOperationException>(() => Layer.SetZIndex(child, 1));

        Layer.GetZIndex(child).ShouldBe(0);
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
}
