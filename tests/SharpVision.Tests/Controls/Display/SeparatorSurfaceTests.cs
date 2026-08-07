// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Display;

/// <summary>Verifies Separator rendering and interaction through a mounted terminal surface.</summary>
public sealed class SeparatorSurfaceTests
{
    /// <summary>Verifies horizontal drawing, terminal-visible style, and excluded hit testing.</summary>
    [ComponentBehaviorEvidence(
        typeof(Separator),
        ComponentBehavior.Mounted |
        ComponentBehavior.HoverExcluded |
        ComponentBehavior.FocusExcluded |
        ComponentBehavior.TabExcluded |
        ComponentBehavior.DirectionalExcluded |
        ComponentBehavior.PressReleaseExcluded)]
    [Fact]
    public async Task Pointer_WhenMovedOverHorizontalSeparator_LeavesExactNonInteractiveLineAsync()
    {
        // Arrange
        var separator = new Separator
        {
            Face = AppearanceTestValues.Face(foreground: ReferenceColors.Get(3)),
            Width = Length.Percent(100)
        };
        await using var surface = await ComponentSurface.MountAsync(
            separator,
            new Size(5, 1),
            TestContext.Current.CancellationToken);

        // Act
        await surface.Pointer.MoveToAsync(separator);

        // Assert
        surface.ShouldHaveState(separator, VisualState.Normal);
        separator.PointerOver.ShouldBeFalse();
        separator.Focused.ShouldBeFalse();
        separator.Pressed.ShouldBeFalse();
        surface.ShouldRender("─────");
        surface.Cell(new Point(4, 0)).Style.Foreground.ShouldBe(ReferenceColors.Get(3));
    }

    /// <summary>Verifies orientation mutation redraws the complete final line.</summary>
    [Fact]
    public async Task UpdateAsync_WhenOrientationChanges_ReplacesHorizontalWithVerticalLineAsync()
    {
        // Arrange
        var separator = new Separator { Width = Length.Percent(100), Height = Length.Percent(100) };
        await using var surface = await ComponentSurface.MountAsync(
            separator,
            new Size(5, 3),
            TestContext.Current.CancellationToken);
        surface.ShouldRender("""
                             ─────


                             """);

        // Act
        await surface.UpdateAsync(
            () => separator.Orientation = Orientation.Vertical,
            "change Separator orientation");

        // Assert
        surface.ShouldRender("""
                             │
                             │
                             │
                             """);

        // Act and assert resized length
        await surface.ResizeAsync(new Size(1, 5));
        surface.ShouldRender("""
                             │
                             │
                             │
                             │
                             │
                             """);
    }

    /// <summary>Verifies zero arranged bounds emit no line cells.</summary>
    [Fact]
    public async Task Render_WhenSeparatorBoundsAreZero_DrawsNothingAsync()
    {
        // Arrange
        var separator = new Separator
        {
            Width = Length.Cells(0),
            Height = Length.Cells(0),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };
        // Act
        await using var surface = await ComponentSurface.MountAsync(
            separator,
            new Size(2, 1),
            TestContext.Current.CancellationToken);

        // Assert
        separator.Bounds.Width.ShouldBe(0);
        separator.Bounds.Height.ShouldBe(0);
        surface.ShouldRender(string.Empty);
    }
}
