// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Layout;

/// <summary>Verifies Wrap participates in the ordinary mounted Container surface.</summary>
public sealed class WrapSurfaceTests
{
    /// <summary>Verifies hidden children retain their packed slot while collapsed children release
    /// it, leaving routed pointer activation with the newly leading sibling.</summary>
    [Fact]
    public async Task Visibility_WhenChildTransitionsThroughEveryState_ReflowsCellsAndPointerTargetsAsync()
    {
        // Arrange
        var first = CreateButton("A");
        var second = new ControlText("B")
        {
            Width = Length.Cells(2),
            Height = Length.Cells(1),
            Face = AppearanceTestValues.Face(background: ReferenceColors.Get(1))
        };
        var wrap = new Wrap { Spacing = 1, Children = { first, second } };
        await using var surface = await ComponentSurface.MountAsync(
            wrap,
            new Size(5, 1),
            TestContext.Current.CancellationToken);

        // Assert - visible state renders the opaque sibling and exposes both targets.
        second.Bounds.ShouldBe(new Rect(3, 0, 2, 1));
        wrap.HitTest(new Point(0, 0)).ShouldBeSameAs(first);
        await surface.Pointer.ClickAsync(first);

        // Act - hidden participates in layout but cannot receive input.
        await surface.UpdateAsync(() => first.Visibility = Visibility.Hidden, "hide first wrapped child");

        // Assert - the sibling's opaque cell and slot stay stable, while the hidden slot falls through.
        second.Bounds.ShouldBe(new Rect(3, 0, 2, 1));
        surface.Cell(new Point(3, 0)).Text.ShouldBe("B");
        wrap.HitTest(new Point(0, 0)).ShouldBeSameAs(wrap);

        // Act - collapsed no longer participates in packing.
        await surface.UpdateAsync(() => first.Visibility = Visibility.Collapsed, "collapse first wrapped child");

        // Assert
        first.Bounds.ShouldBe(default);
        second.Bounds.ShouldBe(new Rect(0, 0, 2, 1));
        surface.Cell(default).Text.ShouldBe("B");
        wrap.HitTest(default).ShouldBeSameAs(second);

        // Act - visible restoration returns the original packed slot and pointer target.
        await surface.UpdateAsync(() => first.Visibility = Visibility.Visible, "restore first wrapped child");

        // Assert
        first.Bounds.ShouldBe(new Rect(0, 0, 2, 1));
        second.Bounds.ShouldBe(new Rect(3, 0, 2, 1));
        surface.Cell(new Point(3, 0)).Text.ShouldBe("B");
        wrap.HitTest(new Point(0, 0)).ShouldBeSameAs(first);
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

    /// <summary>Verifies a popup owned by a wrapped child stays elevated and receives its normal
    /// hit route rather than being hidden behind sibling content.</summary>
    [Fact]
    public async Task Popup_WhenOwnedByWrappedChild_RemainsRoutedThroughContainerPopupLayerAsync()
    {
        // Arrange
        var anchor = new ProbeControl(new Size(1, 1)) { IsFocusable = true };
        var popup = new Popup
        {
            Anchor = anchor,
            Content = new ControlText("P")
        };
        var wrap = new Wrap { IsFocusable = true, Children = { anchor, popup } };
        await using var surface = await ComponentSurface.MountAsync(
            wrap,
            new Size(4, 2),
            TestContext.Current.CancellationToken);

        // Act
        await surface.UpdateAsync(() => popup.IsOpen = true, "open child popup");
        var point = new Point(popup.SurfaceBounds.X, popup.SurfaceBounds.Y);

        // Assert
        popup.SurfaceBounds.Contains(point).ShouldBeTrue();
        wrap.HitTest(point).ShouldBeSameAs(popup);
        surface.Cell(point).Text.ShouldNotBeEmpty();
    }

    /// <summary>Verifies disabling Wrap cascades unavailable state to normal children without
    /// changing their retained geometry.</summary>
    [Fact]
    public async Task Enabled_WhenWrapIsDisabled_CascadesToChildAndPreservesGeometryAsync()
    {
        // Arrange
        var child = new ControlText("A") { Width = Length.Cells(2), Height = Length.Cells(1) };
        var wrap = new Wrap { Children = { child } };
        await using var surface = await ComponentSurface.MountAsync(
            wrap,
            new Size(3, 1),
            TestContext.Current.CancellationToken);
        var bounds = child.Bounds;

        // Act
        await surface.UpdateAsync(() => wrap.IsEnabled = false, "disable Wrap");

        // Assert
        surface.ShouldHaveState(wrap, VisualState.Disabled);
        child.EffectiveIsEnabled.ShouldBeFalse();
        child.Bounds.ShouldBe(bounds);
    }

    /// <summary>Verifies a scrollable child owns wheel input before an ancestor Wrap can react to
    /// it, preserving ordinary nested scroll ownership.</summary>
    [Fact]
    public async Task Pointer_WhenScrollableChildReceivesWheel_DoesNotInterceptItAtWrapAsync()
    {
        // Arrange
        var child = new Stack
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.Never,
            Width = Length.Cells(3),
            Height = Length.Cells(2)
        };
        child.Children.Add(new ControlText("A") { Height = Length.Cells(1) });
        child.Children.Add(new ControlText("B") { Height = Length.Cells(1) });
        child.Children.Add(new ControlText("C") { Height = Length.Cells(1) });
        var wrap = new Wrap { Children = { child } };
        await using var surface = await ComponentSurface.MountAsync(
            wrap,
            new Size(3, 2),
            TestContext.Current.CancellationToken);

        // Act
        await surface.Pointer.WheelAsync(child, default, wheelY: -1);

        // Assert
        child.VerticalOffset.ShouldBe(1);
        wrap.VerticalOffset.ShouldBe(0);
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
