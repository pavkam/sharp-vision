// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies Left/Top/Right/Bottom position properties, LocalBounds, and ContentBounds accessibility.</summary>
public sealed class ControlPositionTests
{
    /// <summary>Verifies position properties default to null.</summary>
    [Fact]
    public void Position_WhenDefaulted_IsNull()
    {
        var control = new ProbeControl();

        control.Left.ShouldBeNull();
        control.Top.ShouldBeNull();
        control.Right.ShouldBeNull();
        control.Bottom.ShouldBeNull();
    }

    /// <summary>Verifies cells and percent values are accepted.</summary>
    [Fact]
    public void Position_WhenCellsOrPercent_Accepted()
    {
        var control = new ProbeControl { Left = Length.Cells(5), Top = Length.Percent(50), Right = Length.Cells(3) };

        control.Left.ShouldBe(Length.Cells(5));
        control.Top.ShouldBe(Length.Percent(50));
        control.Right.ShouldBe(Length.Cells(3));

        control.Bottom = null;
        control.Bottom.ShouldBeNull();
    }

    /// <summary>Verifies Auto values are rejected on all four position properties.</summary>
    [Fact]
    public void Position_WhenAuto_Throws()
    {
        var control = new ProbeControl();

        _ = Should.Throw<ArgumentException>(() => control.Left = Length.Auto);
        _ = Should.Throw<ArgumentException>(() => control.Top = Length.Auto);
        _ = Should.Throw<ArgumentException>(() => control.Right = Length.Auto);
        _ = Should.Throw<ArgumentException>(() => control.Bottom = Length.Auto);
    }

    /// <summary>Verifies Star values are rejected on all four position properties.</summary>
    [Fact]
    public void Position_WhenStar_Throws()
    {
        var control = new ProbeControl();

        _ = Should.Throw<ArgumentException>(() => control.Left = Length.Star(1));
        _ = Should.Throw<ArgumentException>(() => control.Top = Length.Star(2));
        _ = Should.Throw<ArgumentException>(() => control.Right = Length.Star(1));
        _ = Should.Throw<ArgumentException>(() => control.Bottom = Length.Star(1));
    }

    /// <summary>Verifies rejected values do not mutate the property.</summary>
    [Fact]
    public void Position_WhenRejected_PreservesOldValue()
    {
        var control = new ProbeControl { Left = Length.Cells(10) };

        _ = Should.Throw<ArgumentException>(() => control.Left = Length.Auto);

        control.Left.ShouldBe(Length.Cells(10));
    }

    /// <summary>Verifies position change raises PropertyChanged.</summary>
    [Fact]
    public void Position_WhenChanged_RaisesPropertyChanged()
    {
        var control = new ProbeControl { Left = Length.Cells(1) };
        List<string?> names = [];
        control.PropertyChanged += (_, eventArgs) => names.Add(eventArgs.PropertyName);

        control.Left = Length.Cells(5);
        control.Top = Length.Cells(3);

        names.ShouldContain(nameof(Control.Left));
        names.ShouldContain(nameof(Control.Top));
    }

    /// <summary>Verifies setting the same value is a no-op.</summary>
    [Fact]
    public void Position_WhenSameValue_DoesNotNotify()
    {
        var control = new ProbeControl { Left = Length.Cells(5) };
        var raised = 0;
        control.PropertyChanged += (_, _) => raised++;

        control.Left = Length.Cells(5);

        raised.ShouldBe(0);
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

        _ = Should.Throw<InvalidOperationException>(() => child.Left = Length.Cells(5));
    }

    /// <summary>Verifies SharpVision.Controls.Overlay.SetLeft delegates to Control.Left.</summary>
    [Fact]
    public void OverlaySetLeft_WhenCalled_DelegatesToControlProperty()
    {
        var control = new ProbeControl();

        Overlay.SetLeft(control, Length.Cells(7));

        control.Left.ShouldBe(Length.Cells(7));
        Overlay.GetLeft(control).ShouldBe(Length.Cells(7));
    }

    /// <summary>Verifies SharpVision.Controls.Overlay.SetTop delegates to Control.Top.</summary>
    [Fact]
    public void OverlaySetTop_WhenCalled_DelegatesToControlProperty()
    {
        var control = new ProbeControl();

        Overlay.SetTop(control, Length.Cells(3));

        control.Top.ShouldBe(Length.Cells(3));
        Overlay.GetTop(control).ShouldBe(Length.Cells(3));
    }

    /// <summary>Verifies Overlay layout reads position from Control properties.</summary>
    [Fact]
    public void OverlayLayout_WhenControlHasPosition_ArrangesCorrectly()
    {
        var canvas = new Overlay();
        var child = new ProbeControl(new Size(3, 2)) { Left = Length.Cells(4), Top = Length.Cells(2) };
        canvas.Children.Add(child);

        new Engine().Layout(canvas, new Size(12, 8));

        child.Bounds.ShouldBe(new Rect(4, 2, 3, 2));
    }

    /// <summary>Verifies LocalBounds reports parent-relative position.</summary>
    [Fact]
    public void LocalBounds_WhenParentHasOffset_ReportsRelativePosition()
    {
        var canvas = new Overlay { Padding = new Thickness(2) };
        var child = new ProbeControl(new Size(3, 2)) { Left = Length.Cells(1), Top = Length.Cells(1) };
        canvas.Children.Add(child);

        new Engine().Layout(canvas, new Size(20, 10));

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
        new Engine().Layout(control, new Size(10, 8));

        control.LocalBounds.ShouldBe(control.Bounds);
    }

    /// <summary>Verifies LocalBounds inside a bordered container accounts for border inset.</summary>
    [Fact]
    public void LocalBounds_WhenParentHasBorder_AccountsForBorderInset()
    {
        var dock = new Dock { Border = AppearanceTestValues.Border(BorderSide.All) };
        var child = new ProbeControl(new Size(3, 2));
        dock.Children.Add(child);

        new Engine().Layout(dock, new Size(10, 6));

        child.Bounds.X.ShouldBe(1);
        child.LocalBounds.X.ShouldBe(0);
        child.LocalBounds.Y.ShouldBe(0);
    }

    /// <summary>Verifies ContentBounds is publicly accessible.</summary>
    [Fact]
    public void ContentBounds_WhenAccessed_DeflatesBorderAndPadding()
    {
        var dock = new Dock { Border = AppearanceTestValues.Border(BorderSide.All), Padding = new Thickness(1) };
        new Engine().Layout(dock, new Size(10, 8));

        dock.ContentBounds.X.ShouldBe(2);
        dock.ContentBounds.Y.ShouldBe(2);
        dock.ContentBounds.Width.ShouldBe(6);
        dock.ContentBounds.Height.ShouldBe(4);
    }
}
