// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies ProgressBar states through a mounted terminal surface.</summary>
public sealed class ProgressBarSurfaceTests
{
    /// <summary>Verifies partial horizontal fill, style, pointer exclusion, and clamped mutation.</summary>
    [ComponentBehaviorEvidence(
        typeof(ProgressBar),
        ComponentBehavior.Mounted |
        ComponentBehavior.HoverExcluded |
        ComponentBehavior.FocusExcluded |
        ComponentBehavior.TabExcluded |
        ComponentBehavior.DirectionalExcluded |
        ComponentBehavior.PressReleaseExcluded)]
    [Fact]
    public async Task UpdateAsync_WhenHorizontalValueChanges_RendersPartialThenFullBarAsync()
    {
        // Arrange
        var bar = new ProgressBar
        {
            Minimum = 0,
            Maximum = 100,
            Value = 40,
            Foreground = Color.Indexed(3),
        };
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(10, 1),
            TestContext.Current.CancellationToken);
        surface.ShouldRender("████░░░░░░");
        surface.Cell(new Point(3, 0)).Style.Foreground.ShouldBe(Color.Indexed(3));
        surface.Cell(new Point(4, 0)).Text.ShouldBe("░");

        // Act
        await surface.Pointer.MoveToAsync(bar);
        await surface.ResizeAsync(new Size(5, 1));
        surface.ShouldRender("██░░░");
        await surface.UpdateAsync(() => bar.Value = 125, "fill ProgressBar past its maximum");

        // Assert
        bar.Value.ShouldBe(100);
        bar.IsPointerOver.ShouldBeFalse();
        bar.IsFocused.ShouldBeFalse();
        bar.IsPressed.ShouldBeFalse();
        surface.ShouldHaveState(bar, VisualState.Normal);
        surface.ShouldRender("█████");
    }

    /// <summary>Verifies vertical determinate fill starts at the bottom.</summary>
    [Fact]
    public async Task Render_WhenVerticalValueIsPartial_FillsBottomToTopAsync()
    {
        // Arrange
        var bar = new ProgressBar
        {
            Minimum = 0,
            Maximum = 100,
            Value = 40,
            Orientation = Orientation.Vertical,
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(1, 5),
            TestContext.Current.CancellationToken);

        // Assert
        surface.ShouldRender("""
            ░
            ░
            ░
            █
            █
            """);
    }

    /// <summary>Verifies indeterminate mode uses a deterministic distinct presentation.</summary>
    [Fact]
    public async Task Render_WhenProgressIsIndeterminate_FillsWithIndeterminateGlyphAsync()
    {
        // Arrange
        var bar = new ProgressBar
        {
            IsIndeterminate = true,
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(4, 1),
            TestContext.Current.CancellationToken);

        // Assert
        surface.ShouldRender("▒▒▒▒");
    }

    /// <summary>Verifies zero arranged bounds emit no progress cells.</summary>
    [Fact]
    public async Task Render_WhenProgressBarBoundsAreZero_DrawsNothingAsync()
    {
        // Arrange
        var bar = new ProgressBar
        {
            Width = Length.Cells(0),
            Height = Length.Cells(0),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(2, 1),
            TestContext.Current.CancellationToken);

        // Assert
        bar.Bounds.Width.ShouldBe(0);
        bar.Bounds.Height.ShouldBe(0);
        surface.ShouldRender(string.Empty);
    }
}
