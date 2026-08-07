// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies Overlay's attached Left/Top/Right/Bottom positions, LocalBounds, and ContentBounds accessibility.</summary>
public sealed class ControlPositionTests
{
    /// <summary>Verifies attached positions default to null.</summary>
    [Fact]
    public void Position_WhenDefaulted_IsNull()
    {
        var control = new ProbeControl();

        Overlay.GetLeft(control).ShouldBeNull();
        Overlay.GetTop(control).ShouldBeNull();
        Overlay.GetRight(control).ShouldBeNull();
        Overlay.GetBottom(control).ShouldBeNull();
    }

    /// <summary>Verifies cells and percent values are accepted.</summary>
    [Fact]
    public void Position_WhenCellsOrPercent_Accepted()
    {
        var control = new ProbeControl();
        Overlay.SetLeft(control, Length.Cells(5));
        Overlay.SetTop(control, Length.Percent(50));
        Overlay.SetRight(control, Length.Cells(3));

        Overlay.GetLeft(control).ShouldBe(Length.Cells(5));
        Overlay.GetTop(control).ShouldBe(Length.Percent(50));
        Overlay.GetRight(control).ShouldBe(Length.Cells(3));

        Overlay.SetBottom(control, null);
        Overlay.GetBottom(control).ShouldBeNull();
    }

    /// <summary>Verifies Auto values are rejected on all four attached positions.</summary>
    [Fact]
    public void Position_WhenAuto_Throws()
    {
        var control = new ProbeControl();

        _ = Should.Throw<ArgumentException>(() => Overlay.SetLeft(control, Length.Auto));
        _ = Should.Throw<ArgumentException>(() => Overlay.SetTop(control, Length.Auto));
        _ = Should.Throw<ArgumentException>(() => Overlay.SetRight(control, Length.Auto));
        _ = Should.Throw<ArgumentException>(() => Overlay.SetBottom(control, Length.Auto));
    }

    /// <summary>Verifies Star values are rejected on all four attached positions.</summary>
    [Fact]
    public void Position_WhenStar_Throws()
    {
        var control = new ProbeControl();

        _ = Should.Throw<ArgumentException>(() => Overlay.SetLeft(control, Length.Star(1)));
        _ = Should.Throw<ArgumentException>(() => Overlay.SetTop(control, Length.Star(2)));
        _ = Should.Throw<ArgumentException>(() => Overlay.SetRight(control, Length.Star(1)));
        _ = Should.Throw<ArgumentException>(() => Overlay.SetBottom(control, Length.Star(1)));
    }

    /// <summary>Verifies rejected values do not mutate the attached value.</summary>
    [Fact]
    public void Position_WhenRejected_PreservesOldValue()
    {
        var control = new ProbeControl();
        Overlay.SetLeft(control, Length.Cells(10));

        _ = Should.Throw<ArgumentException>(() => Overlay.SetLeft(control, Length.Auto));

        Overlay.GetLeft(control).ShouldBe(Length.Cells(10));
    }

    /// <summary>Verifies changing an attached position invalidates the parent Overlay's measure pass.</summary>
    [Fact]
    public void Position_WhenChanged_InvalidatesParentMeasure()
    {
        var overlay = new Overlay();
        var child = new ProbeControl();
        overlay.Children.Add(child);
        overlay.Clear(Invalidation.All);

        Overlay.SetLeft(child, Length.Cells(1));

        overlay.Pending.ShouldBe(Invalidation.All);
    }

    /// <summary>Verifies setting the same value is a no-op.</summary>
    [Fact]
    public void Position_WhenSameValue_DoesNotInvalidate()
    {
        var overlay = new Overlay();
        var child = new ProbeControl();
        overlay.Children.Add(child);
        Overlay.SetLeft(child, Length.Cells(5));
        overlay.Clear(Invalidation.All);

        Overlay.SetLeft(child, Length.Cells(5));

        overlay.Pending.ShouldBe(Invalidation.None);
    }

    /// <summary>Verifies position requires dispatcher affinity when attached.</summary>
    [Fact]
    public async Task Position_WhenAttached_RequiresDispatcherAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var root = new ProbeContainer();
        var child = new ProbeControl();
        root.Children.Add(child);

        await dispatcher.InvokeAsync(
            () => root.Attach(dispatcher),
            TestContext.Current.CancellationToken);

        _ = Should.Throw<InvalidOperationException>(() => Overlay.SetLeft(child, Length.Cells(5)));
    }

    /// <summary>Verifies Overlay layout reads position from attached storage.</summary>
    [Fact]
    public void OverlayLayout_WhenControlHasPosition_ArrangesCorrectly()
    {
        var canvas = new Overlay();
        var child = new ProbeControl(new Size(3, 2));
        Overlay.SetLeft(child, Length.Cells(4));
        Overlay.SetTop(child, Length.Cells(2));
        canvas.Children.Add(child);

        new LayoutEngine().Layout(canvas, new Size(12, 8));

        child.Bounds.ShouldBe(new Rect(4, 2, 3, 2));
    }

}
