// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;



using Layer = Overlay;

/// <summary>Verifies layered layout, stable z-order, clipping, and targeting.</summary>
public sealed class OverlayTests
{
    /// <summary>Verifies conservative defaults and maximum intrinsic desired size.</summary>
    [Fact]
    public void Measure_WhenChildrenDiffer_UsesMaximumMarginInclusiveSize()
    {
        var layer = new Layer();
        var first = new ProbeControl(new Size(3, 2));
        var second = new ProbeControl(new Size(4, 1)) { Margin = new Thickness(1) };
        layer.Children.Add(first);
        layer.Children.Add(second);

        new Engine().Layout(layer, new Size(20, 10));

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
            VerticalAlignment = VerticalAlignment.Bottom,
        };
        layer.Children.Add(child);

        new Engine().Layout(layer, new Size(10, 4));

        child.Bounds.ShouldBe(new Rect(5, 2, 5, 2));
    }

    /// <summary>Verifies higher z-order renders later and receives pointer targeting first.</summary>
    [Fact]
    public void ZIndex_WhenLayersOverlap_ControlsRenderAndHitOrder()
    {
        var layer = new Layer() { Bounds = new Rect(0, 0, 1, 1) };
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
        var layer = new Layer() { Bounds = new Rect(0, 0, 1, 1) };
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
        var layer = new Layer() { Bounds = new Rect(0, 0, 1, 1) };
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
        var layer = new Layer()
        {
            Bounds = new Rect(0, 0, 1, 1),
            ClipToBounds = false,
        };
        var child = new ProbeControl()
        {
            Bounds = new Rect(1, 0, 1, 1),
            Content = "X".AsMemory(),
        };
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
        var layer = new Layer() { Bounds = new Rect(0, 0, 1, 1) };
        var child = new ProbeControl()
        {
            Bounds = new Rect(1, 0, 1, 1),
            Content = "X".AsMemory(),
        };
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
        var layer = new Layer() { Bounds = new Rect(0, 0, 1, 1) };
        var low = Child("L");
        var high = Child("H");
        high.IsHitTestVisible = false;
        Layer.SetZIndex(high, 1);
        layer.Children.Add(low);
        layer.Children.Add(high);

        layer.HitTest(default).ShouldBeSameAs(low);
    }

    /// <summary>Verifies z-order never changes collection-order focus traversal.</summary>
    [Fact]
    public async Task MoveNext_WhenZOrderDiffers_UsesCollectionOrderAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var layer = new Layer();
        var first = new ProbeControl() { CanFocus = true };
        var second = new ProbeControl() { CanFocus = true };
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
        Content = content.AsMemory(),
    };
}
