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

    /// <summary>Verifies LocalBounds reports parent-relative position.</summary>
    [Fact]
    public void LocalBounds_WhenParentHasOffset_ReportsRelativePosition()
    {
        var canvas = new Overlay { Padding = new Thickness(2) };
        var child = new ProbeControl(new Size(3, 2));
        Overlay.SetLeft(child, Length.Cells(1));
        Overlay.SetTop(child, Length.Cells(1));
        canvas.Children.Add(child);

        new LayoutEngine().Layout(canvas, new Size(20, 10));

        child.Bounds.X.ShouldBeGreaterThan(1);
        child.LocalBounds.X.ShouldBe(1);
        child.LocalBounds.Y.ShouldBe(1);
        child.LocalBounds.Width.ShouldBe(3);
        child.LocalBounds.Height.ShouldBe(2);
    }

    /// <summary>Verifies LocalBounds equals Bounds when there is no parent.</summary>
    [Fact]
    public void LocalBounds_WhenNoParent_EqualsBounds()
    {
        var control = new ProbeControl(new Size(5, 3));
        new LayoutEngine().Layout(control, new Size(10, 8));

        control.LocalBounds.ShouldBe(control.Bounds);
    }

    /// <summary>Verifies LocalBounds inside a bordered container accounts for border inset.</summary>
    [Fact]
    public void LocalBounds_WhenParentHasBorder_AccountsForBorderInset()
    {
        var dock = new Dock { Border = AppearanceTestValues.Border(BorderSide.All) };
        var child = new ProbeControl(new Size(3, 2));
        dock.Children.Add(child);

        new LayoutEngine().Layout(dock, new Size(10, 6));

        child.Bounds.X.ShouldBe(1);
        child.LocalBounds.X.ShouldBe(0);
        child.LocalBounds.Y.ShouldBe(0);
    }

    /// <summary>Verifies ContentBounds is publicly accessible.</summary>
    [Fact]
    public void ContentBounds_WhenAccessed_DeflatesBorderAndPadding()
    {
        var dock = new Dock { Border = AppearanceTestValues.Border(BorderSide.All), Padding = new Thickness(1) };
        new LayoutEngine().Layout(dock, new Size(10, 8));

        dock.ContentBounds.X.ShouldBe(2);
        dock.ContentBounds.Y.ShouldBe(2);
        dock.ContentBounds.Width.ShouldBe(6);
        dock.ContentBounds.Height.ShouldBe(4);
    }
}
