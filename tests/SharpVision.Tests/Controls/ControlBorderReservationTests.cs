// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies the base layout pipeline reserves border thickness with padding.</summary>
public sealed class ControlBorderReservationTests
{
    /// <summary>Verifies a bordered container insets its child by the border on every edge.</summary>
    [Fact]
    public void Arrange_WhenContainerHasBorder_InsetsChildByBorder()
    {
        ProbeControl child = new(new Size(4, 2))
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        LayoutProbe container = new()
        {
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        container.Children.Add(child);

        new Engine().Layout(container, new Size(20, 10));

        child.Bounds.ShouldBe(new Rect(1, 1, 18, 8));
    }

    /// <summary>Verifies a bordered container's desired size includes the border.</summary>
    [Fact]
    public void Measure_WhenContainerHasBorder_DesiredSizeIncludesBorder()
    {
        ProbeControl child = new(new Size(4, 2));
        LayoutProbe container = new() { BorderThickness = new Thickness(1) };
        container.Children.Add(child);

        container.Measure(new Constraint(width: null, height: null));

        container.DesiredSize.ShouldBe(new Size(6, 4));
    }

    /// <summary>Verifies a zero-border container leaves its child in the full slot.</summary>
    [Fact]
    public void Arrange_WhenNoBorder_LeavesChildAtFullSlot()
    {
        ProbeControl child = new(new Size(4, 2))
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        LayoutProbe container = new()
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        container.Children.Add(child);

        new Engine().Layout(container, new Size(20, 10));

        child.Bounds.ShouldBe(new Rect(0, 0, 20, 10));
    }

    /// <summary>Verifies asymmetric border and padding edges compose before child arrangement.</summary>
    [Fact]
    public void Arrange_WhenBorderAndPaddingAreAsymmetric_ComposesEveryEdge()
    {
        ProbeControl child = new(new Size(4, 2))
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        LayoutProbe container = new()
        {
            BorderThickness = new Thickness(1, 0, 0, 1),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(2, 3, 4, 5),
        };
        container.Children.Add(child);

        new Engine().Layout(container, new Size(20, 15));

        child.Bounds.ShouldBe(new Rect(3, 3, 13, 6));
    }

    /// <summary>Verifies asymmetric border and padding totals contribute to intrinsic desired size.</summary>
    [Fact]
    public void Measure_WhenBorderAndPaddingAreAsymmetric_IncludesEveryEdge()
    {
        ProbeControl child = new(new Size(4, 2));
        LayoutProbe container = new()
        {
            BorderThickness = new Thickness(1, 0, 0, 1),
            Padding = new Thickness(2, 3, 4, 5),
        };
        container.Children.Add(child);

        container.Measure(new Constraint(width: null, height: null));

        container.DesiredSize.ShouldBe(new Size(11, 11));
    }

    /// <summary>Verifies combined padding and border reservation saturates instead of overflowing.</summary>
    [Fact]
    public void Measure_WhenPaddingAndBorderExceedIntegerRange_SaturatesDesiredSize()
    {
        ProbeControl child = new(new Size(1, 1));
        LayoutProbe container = new()
        {
            BorderThickness = new Thickness(1, 0, 0, 0),
            Padding = new Thickness(int.MaxValue, 0, 0, 0),
        };
        container.Children.Add(child);

        container.Measure(new Constraint(width: null, height: null));

        container.DesiredSize.ShouldBe(new Size(int.MaxValue, 1));
    }
}
