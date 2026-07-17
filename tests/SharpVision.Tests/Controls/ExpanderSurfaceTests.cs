// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies Expander appearance, activation, focus, content exclusion, replacement, and resize through mounted surfaces.</summary>
public sealed class ExpanderSurfaceTests
{
    /// <summary>Verifies expanded and collapsed states draw exact header/content cells and remove stale rows.</summary>
    [Fact]
    public async Task UpdateAsync_WhenExpansionChanges_DrawsExactStateAndClearsContentAsync()
    {
        // Arrange
        var expander = new Expander
        {
            Header = "Details",
            Content = new ControlText("Body\nMore"),
            Width = Length.Cells(12),
            Height = Length.Cells(3),
        };
        await using var surface = await ComponentSurface.MountAsync(
            expander,
            new Size(12, 3),
            TestContext.Current.CancellationToken);
        surface.ShouldRender("""
            ▼ Details
            Body
            More
            """);

        // Act
        await surface.UpdateAsync(() => expander.IsExpanded = false, "collapse Expander");

        // Assert
        expander.Content.Bounds.ShouldBe(default);
        expander.Content.Parent.ShouldBeSameAs(expander);
        surface.ShouldRender("▶ Details");
    }

    /// <summary>Verifies pointer, Space, and Enter activation share focus and changed-event behavior.</summary>
    [Fact]
    public async Task Input_WhenHeaderIsActivated_TogglesThroughEverySupportedPathAsync()
    {
        // Arrange
        var changes = 0;
        var expander = new Expander
        {
            Header = "Input",
            Content = new ControlText("Content"),
            Width = Length.Cells(12),
            Height = Length.Cells(2),
        };
        expander.ExpandedChanged += (_, _) => changes++;
        await using var surface = await ComponentSurface.MountAsync(
            expander,
            new Size(12, 2),
            TestContext.Current.CancellationToken);

        // Act pointer
        await surface.Pointer.ClickAsync(expander, new Point(1, 0));

        // Assert pointer
        expander.IsExpanded.ShouldBeFalse();
        changes.ShouldBe(1);
        surface.ShouldHaveState(expander, VisualState.PointerOver | VisualState.Focused);

        // Act Space then Enter
        await surface.Keyboard.CompleteCharacterAsync(new Rune(' '));
        expander.IsExpanded.ShouldBeTrue();
        await surface.Keyboard.PressAsync(Code.Enter);

        // Assert keyboard
        expander.IsExpanded.ShouldBeFalse();
        changes.ShouldBe(3);
        surface.ShouldRender("▶ Input");
    }

    /// <summary>Verifies disabled input refuses toggles and collapsed replacement appears only after expansion.</summary>
    [Fact]
    public async Task Input_WhenDisabledOrContentReplaced_PreservesAvailabilityAndOwnershipPolicyAsync()
    {
        // Arrange disabled collapsed Expander
        var first = new ControlText("First");
        var second = new ControlText("Second");
        var expander = new Expander
        {
            Header = "Policy",
            IsExpanded = false,
            IsEnabled = false,
            Content = first,
            Width = Length.Cells(12),
            Height = Length.Cells(2),
        };
        await using var surface = await ComponentSurface.MountAsync(
            expander,
            new Size(12, 2),
            TestContext.Current.CancellationToken);

        // Act disabled input
        await surface.Pointer.ClickAsync(expander, new Point(1, 0));
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Enter);

        // Assert disabled refusal
        expander.IsExpanded.ShouldBeFalse();
        surface.ShouldHaveState(expander, VisualState.Disabled);
        surface.ShouldRender("▶ Policy");

        // Act replace while collapsed, enable, and expand
        await surface.UpdateAsync(() => expander.Content = second, "replace collapsed Expander content");
        await surface.UpdateAsync(() => expander.IsEnabled = true, "enable Expander");
        await surface.Pointer.ClickAsync(expander, new Point(1, 0));

        // Assert replacement reveal
        first.Parent.ShouldBeNull();
        second.Parent.ShouldBeSameAs(expander);
        expander.IsExpanded.ShouldBeTrue();
        surface.ShouldRender("""
            ▼ Policy
            Second
            """);
    }

    /// <summary>Verifies Unicode header geometry and content reflow remain correct after resize.</summary>
    [Fact]
    public async Task ResizeAsync_WhenUnicodeHeaderGrows_PreservesWideCellsAndReflowsContentAsync()
    {
        // Arrange
        var expander = new Expander
        {
            Header = "界 Tools",
            Content = new ControlText("abcdefgh") { Overflow = Overflow.WrapAnywhere },
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        await using var surface = await ComponentSurface.MountAsync(
            expander,
            new Size(6, 3),
            TestContext.Current.CancellationToken);
        surface.Cell(new Point(2, 0)).Text.ShouldBe("界");
        surface.Cell(new Point(3, 0)).IsContinuation.ShouldBeTrue();

        // Act
        await surface.ResizeAsync(new Size(12, 2));

        // Assert
        expander.Bounds.ShouldBe(new Rect(0, 0, 12, 2));
        expander.Content.Bounds.ShouldBe(new Rect(0, 1, 12, 1));
        surface.ShouldRender("""
            ▼ 界 Tools
            abcdefgh
            """);
        surface.Cell(new Point(2, 0)).Text.ShouldBe("界");
        surface.Cell(new Point(3, 0)).IsContinuation.ShouldBeTrue();
    }

    /// <summary>Verifies a one-cell collapsed header clips safely and resize reveals the retained label.</summary>
    [Fact]
    public async Task ResizeAsync_WhenCollapsedHeaderStartsTiny_RevealsLabelWithoutExpandingAsync()
    {
        // Arrange
        var expander = new Expander
        {
            Header = "Details",
            IsExpanded = false,
            Content = new ControlText("Hidden"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        await using var surface = await ComponentSurface.MountAsync(
            expander,
            new Size(1, 1),
            TestContext.Current.CancellationToken);
        surface.ShouldRender("▶");

        // Act
        await surface.ResizeAsync(new Size(9, 1));

        // Assert
        expander.IsExpanded.ShouldBeFalse();
        expander.Content.Bounds.ShouldBe(default);
        surface.ShouldRender("▶ Details");
    }
}
