// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Layout;

/// <summary>Verifies Wrap participates in the ordinary mounted Container surface.</summary>
public sealed class WrapSurfaceTests
{
    /// <summary>Verifies hidden children retain their packed slot while collapsed children release
    /// it, leaving routed pointer activation with the newly leading sibling.</summary>
    [Fact]
    public async Task Visibility_WhenChildChangesFromHiddenToCollapsed_ReflowsAndRoutesPointerToRemainingChildAsync()
    {
        // Arrange
        var activations = 0;
        var first = CreateButton("A");
        var second = CreateButton("B");
        second.Click += (_, _) => activations++;
        var wrap = new Wrap { Spacing = 1, Children = { first, second } };
        await using var surface = await ComponentSurface.MountAsync(
            wrap,
            new Size(5, 1),
            TestContext.Current.CancellationToken);

        // Act - hidden participates in layout but cannot receive input.
        await surface.UpdateAsync(() => first.Visibility = Visibility.Hidden, "hide first wrapped child");

        // Assert - the second child keeps its original packed slot.
        second.Bounds.ShouldBe(new Rect(3, 0, 2, 1));

        // Act - collapsed no longer participates in packing.
        await surface.UpdateAsync(() => first.Visibility = Visibility.Collapsed, "collapse first wrapped child");
        await surface.Pointer.ClickAsync(second);

        // Assert
        first.Bounds.ShouldBe(default);
        second.Bounds.ShouldBe(new Rect(0, 0, 2, 1));
        activations.ShouldBe(1);
    }

    /// <summary>Verifies final cells preserve a wide cluster's continuation ownership and resize
    /// repacks the next child before its pointer route is used.</summary>
    [Fact]
    public async Task ResizeAsync_WhenWideChildReflows_PreservesContinuationAndFinalPointerTargetAsync()
    {
        // Arrange
        var activations = 0;
        var wide = new ControlText("界") { Width = Length.Cells(2), Height = Length.Cells(1) };
        var button = CreateButton("B");
        button.Click += (_, _) => activations++;
        var wrap = new Wrap { Spacing = 1, Children = { wide, button } };
        await using var surface = await ComponentSurface.MountAsync(
            wrap,
            new Size(3, 2),
            TestContext.Current.CancellationToken);

        // Act
        await surface.ResizeAsync(new Size(5, 1));
        await surface.Pointer.ClickAsync(button);

        // Assert
        wide.Bounds.ShouldBe(new Rect(0, 0, 2, 1));
        button.Bounds.ShouldBe(new Rect(3, 0, 2, 1));
        surface.Cell(new Point(0, 0)).Text.ShouldBe("界");
        surface.Cell(new Point(1, 0)).Continuation.ShouldBeTrue();
        activations.ShouldBe(1);
    }

    /// <summary>Verifies ordinary tab navigation follows Wrap child source order, independent of
    /// the rows created by packing.</summary>
    [Fact]
    public async Task Keyboard_WhenChildrenWrap_TraversesFocusableDescendantsInSourceOrderAsync()
    {
        // Arrange
        var first = CreateButton("A");
        var second = CreateButton("B");
        var wrap = new Wrap { Spacing = 1, Children = { first, second } };
        await using var surface = await ComponentSurface.MountAsync(
            wrap,
            new Size(2, 2),
            TestContext.Current.CancellationToken);

        // Act and assert
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(first);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(second);
    }

    /// <summary>Verifies Wrap delegates wheel input to its inherited scrolling service and keeps
    /// the visible viewport's final cells in sync with the retained offset.</summary>
    [Fact]
    public async Task Pointer_WhenVerticalWrapAutoScrolls_UsesInheritedWheelHandlingAsync()
    {
        // Arrange
        var wrap = new Wrap
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.Never,
            Spacing = 1,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        wrap.Children.Add(new ControlText("A") { Width = Length.Cells(2), Height = Length.Cells(1) });
        wrap.Children.Add(new ControlText("B") { Width = Length.Cells(2), Height = Length.Cells(1) });
        wrap.Children.Add(new ControlText("C") { Width = Length.Cells(2), Height = Length.Cells(1) });
        await using var surface = await ComponentSurface.MountAsync(
            wrap,
            new Size(2, 2),
            TestContext.Current.CancellationToken);

        // Act
        await surface.Pointer.WheelAsync(wrap, default, wheelY: -1);

        // Assert
        wrap.VerticalOffset.ShouldBe(1);
        surface.ShouldRender("B\nC");
    }

    private static Button CreateButton(string text) => new()
    {
        Text = text,
        Style = TestButtonStyles.Flat,
        Padding = default,
        Width = Length.Cells(2),
        Height = Length.Cells(1),
        HorizontalAlignment = HorizontalAlignment.Stretch,
        VerticalAlignment = VerticalAlignment.Stretch
    };
}
