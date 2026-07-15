// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies intrinsic border geometry participates in the common control layout contract.</summary>
public sealed class ControlBorderReservationTests
{
    /// <summary>Verifies a complete border reserves one cell on every content edge.</summary>
    [Fact]
    public void Arrange_WhenContainerHasBorder_InsetsChildByBorder()
    {
        var child = StretchingChild();
        var container = StretchingContainer(child);
        container.BorderThickness = new Thickness(1);

        new Engine().Layout(container, new Size(20, 10));

        child.Bounds.ShouldBe(new Rect(1, 1, 18, 8));
    }

    /// <summary>Verifies border and padding reserve distinct, ordered content insets.</summary>
    [Fact]
    public void Arrange_WhenBorderAndPaddingAreSet_InsetsChildByBoth()
    {
        var child = StretchingChild();
        var container = StretchingContainer(child);
        container.BorderThickness = new Thickness(1);
        container.Padding = new Thickness(1);

        new Engine().Layout(container, new Size(20, 10));

        child.Bounds.ShouldBe(new Rect(2, 2, 16, 6));
    }

    /// <summary>Verifies a complete border contributes both physical edges to desired size.</summary>
    [Fact]
    public void Measure_WhenContainerHasBorder_DesiredSizeIncludesBorder()
    {
        var child = new ProbeControl(new Size(4, 2));
        var container = new LayoutProbe() { BorderThickness = new Thickness(1) };
        container.Children.Add(child);

        container.Measure(new Constraint(null, null));

        container.DesiredSize.ShouldBe(new Size(6, 4));
    }

    /// <summary>Verifies the zero-border default preserves the complete arranged slot.</summary>
    [Fact]
    public void Arrange_WhenNoBorder_LeavesChildAtFullSlot()
    {
        var child = StretchingChild();
        var container = StretchingContainer(child);

        new Engine().Layout(container, new Size(20, 10));

        child.Bounds.ShouldBe(new Rect(0, 0, 20, 10));
    }

    /// <summary>Verifies partial physical edges reserve only their active cells.</summary>
    [Fact]
    public void Arrange_WhenBorderEdgesArePartial_ReservesOnlyActiveEdges()
    {
        var child = StretchingChild();
        var container = StretchingContainer(child);
        container.BorderThickness = new Thickness(1, 1, 0, 0);

        new Engine().Layout(container, new Size(20, 10));

        child.Bounds.ShouldBe(new Rect(1, 1, 19, 9));
    }

    /// <summary>Verifies combined geometric insets saturate before constraint subtraction.</summary>
    [Fact]
    public void Measure_WhenCombinedInsetExceedsInteger_SaturatesWithoutThrowing()
    {
        var child = new ProbeControl();
        var container = new LayoutProbe()
        {
            Padding = new Thickness(int.MaxValue - 1, 0, 0, 0),
            BorderThickness = new Thickness(1, 0, 1, 0),
        };
        container.Children.Add(child);

        Should.NotThrow(() => container.Measure(new Constraint(10, 10)));

        child.MeasureConstraints.ShouldHaveSingleItem()
            .ShouldBe(new Constraint(0, 10));
    }

    private static ProbeControl StretchingChild() => new()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch,
        VerticalAlignment = VerticalAlignment.Stretch,
    };

    private static LayoutProbe StretchingContainer(Control child)
    {
        var container = new LayoutProbe()
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        container.Children.Add(child);
        return container;
    }
}
