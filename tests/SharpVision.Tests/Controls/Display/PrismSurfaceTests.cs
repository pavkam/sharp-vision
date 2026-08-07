// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Display;

/// <summary>Proves Prism through mounted terminal surfaces.</summary>
public sealed class PrismSurfaceTests
{
    /// <summary>Verifies multi-cell text content renders with distinct color-shifted foregrounds across positions.</summary>
    [Fact]
    public async Task Render_WhenHorizontalPrismOwnsText_AppliesDistinctForegroundsAcrossCellsAsync()
    {
        // Arrange
        var child = new ControlText("ABCD");
        var prism = new Prism
        {
            Content = child,
            Direction = PrismDirection.Horizontal,
            CycleLength = 4,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        await using var surface = await ComponentSurface.MountAsync(
            prism,
            new Size(4, 1),
            TestContext.Current.CancellationToken);

        // Assert text content is visible
        surface.ShouldRender("ABCD");

        // Assert each cell has RGB foreground from the color-shift effect
        var colorAtZero = surface.Cell(new Point(0, 0)).Style.Foreground;
        var colorAtOne = surface.Cell(new Point(1, 0)).Style.Foreground;
        var colorAtTwo = surface.Cell(new Point(2, 0)).Style.Foreground;
        colorAtZero.ShouldBe(ReferenceColors.Get(9));
        colorAtZero.ShouldNotBe(colorAtTwo);
        colorAtOne.ShouldNotBe(colorAtZero);
    }

    /// <summary>Verifies changing Phase shifts the foreground color at every cell position.</summary>
    [Fact]
    public async Task Render_WhenPhaseChanges_ShiftsForegroundColorsAsync()
    {
        // Arrange
        var child = new ControlText("AB");
        var prism = new Prism
        {
            Content = child,
            Direction = PrismDirection.Horizontal,
            CycleLength = 4,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        await using var surface = await ComponentSurface.MountAsync(
            prism,
            new Size(2, 1),
            TestContext.Current.CancellationToken);
        var originalColorAtZero = surface.Cell(new Point(0, 0)).Style.Foreground;
        originalColorAtZero.ShouldBe(ReferenceColors.Get(9));

        // Act
        await surface.UpdateAsync(() => prism.Phase = 0.5, "advance Prism phase by half");

        // Assert the same cell now has a different foreground
        surface.ShouldRender("AB");
        var newColorAtZero = surface.Cell(new Point(0, 0)).Style.Foreground;
        newColorAtZero.ShouldNotBe(originalColorAtZero);
    }

    /// <summary>Verifies retained Unicode content receives the effect while pointer state follows ancestry.</summary>
    [ComponentBehaviorEvidence(
        typeof(Prism),
        ComponentBehavior.Mounted |
        ComponentBehavior.Hover |
        ComponentBehavior.FocusExcluded |
        ComponentBehavior.TabExcluded |
        ComponentBehavior.DirectionalExcluded |
        ComponentBehavior.PressReleaseExcluded |
        ComponentBehavior.Composition)]
    [Fact]
    public async Task Render_WhenPrismOwnsUnicodeContent_AppliesScopedColorAndRoutesHoverAsync()
    {
        // Arrange
        var child = new ControlText("界");
        var prism = new Prism
        {
            Content = child,
            Direction = PrismDirection.Horizontal,
            CycleLength = 4,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        await using var surface = await ComponentSurface.MountAsync(
            prism,
            new Size(4, 1),
            TestContext.Current.CancellationToken);

        // Act
        await surface.Pointer.MoveToAsync(child);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Assert
        prism.PointerOver.ShouldBeTrue();
        prism.PointerDirectlyOver.ShouldBeFalse();
        child.PointerDirectlyOver.ShouldBeTrue();
        prism.Focused.ShouldBeFalse();
        prism.Pressed.ShouldBeFalse();
        surface.Cell(default).Text.ShouldBe("界");
        surface.Cell(default).Style.Foreground.ShouldBe(ReferenceColors.Get(9));
        surface.Cell(new Point(1, 0)).Continuation.ShouldBeTrue();
    }
}
