// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Proves Prism through mounted terminal surfaces.</summary>
public sealed class PrismSurfaceTests
{
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
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        await using var surface = await ComponentSurface.MountAsync(
            prism,
            new Size(4, 1),
            TestContext.Current.CancellationToken);

        // Act
        await surface.Pointer.MoveToAsync(child);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Assert
        prism.IsPointerOver.ShouldBeTrue();
        prism.IsPointerDirectlyOver.ShouldBeFalse();
        child.IsPointerDirectlyOver.ShouldBeTrue();
        prism.IsFocused.ShouldBeFalse();
        prism.IsPressed.ShouldBeFalse();
        surface.Cell(default).Text.ShouldBe("界");
        surface.Cell(default).Style.Foreground.ShouldBe(Color.Indexed(9));
        surface.Cell(new Point(1, 0)).IsContinuation.ShouldBeTrue();
    }
}
