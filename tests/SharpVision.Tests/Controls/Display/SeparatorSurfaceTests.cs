// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Display;

/// <summary>Verifies Separator rendering and interaction through a mounted terminal surface.</summary>
public sealed class SeparatorSurfaceTests
{
    /// <summary>Verifies horizontal drawing, terminal-visible style, and excluded hit testing.</summary>
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
        separator.IsPointerOver.ShouldBeFalse();
        separator.IsFocused.ShouldBeFalse();
        separator.IsPressed.ShouldBeFalse();
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

    /// <summary>Verifies direct and ancestor-inherited disable painting, stable geometry across a
    /// genuine resize, and re-enable recovery for a mounted Separator.</summary>
    [Fact]
    public async Task IsEnabled_WhenSeparatorIsDisabled_ProvesDisabledContractAsync()
    {
        // Arrange
        var separator = new Separator();
        await using var surface = await ComponentSurface.MountAsync(
            separator,
            new Size(5, 1),
            TestContext.Current.CancellationToken);

        // Act — direct disable
        await surface.UpdateAsync(() => separator.IsEnabled = false, "disable Separator");

        // Assert
        surface.ShouldHaveState(separator, VisualState.Disabled);

        // Arrange — ancestor-inherited disable
        var child = new Separator();
        var stack = new Stack { Children = { child } };
        await using var ancestorSurface = await ComponentSurface.MountAsync(
            stack,
            new Size(5, 1),
            TestContext.Current.CancellationToken);

        // Act
        await ancestorSurface.UpdateAsync(() => stack.IsEnabled = false, "disable ancestor Stack");

        // Assert
        child.EffectiveIsEnabled.ShouldBeFalse();
        ancestorSurface.ShouldHaveState(child, VisualState.Disabled);

        // Act — geometry stability across a genuine resize
        await surface.ResizeAsync(new Size(3, 4));
        var enabledSeparator = new Separator();
        await using var enabledSurface = await ComponentSurface.MountAsync(
            enabledSeparator,
            new Size(3, 4),
            TestContext.Current.CancellationToken);

        // Assert
        separator.Bounds.ShouldBe(enabledSeparator.Bounds);
        separator.DesiredSize.ShouldBe(enabledSeparator.DesiredSize);

        // Act — re-enable recovery
        await surface.UpdateAsync(() => separator.IsEnabled = true, "re-enable Separator");

        // Assert
        surface.ShouldHaveState(separator, VisualState.Normal);
    }
}
