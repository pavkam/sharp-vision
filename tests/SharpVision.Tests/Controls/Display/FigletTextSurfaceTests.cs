// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Display;

/// <summary>Verifies FigletText art, mutation, style, and clipping through a mounted surface.</summary>
public sealed class FigletTextSurfaceTests
{
    /// <summary>Verifies passive FIGlet art observes hover without entering focus or press state.</summary>
    [ComponentBehaviorEvidence(
        typeof(FigletText),
        ComponentBehavior.Mounted |
        ComponentBehavior.Hover |
        ComponentBehavior.FocusExcluded |
        ComponentBehavior.TabExcluded |
        ComponentBehavior.DirectionalExcluded |
        ComponentBehavior.PressReleaseExcluded)]
    [Fact]
    public async Task Pointer_WhenFigletTextIsMounted_TracksHoverWithoutFocusOrPressAsync()
    {
        // Arrange
        var title = new FigletText(FigletCatalog.Default.Load("small"))
        {
            Content = "I",
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };
        await using var surface = await ComponentSurface.MountAsync(
            title,
            new Size(5, 5),
            TestContext.Current.CancellationToken);
        var restingCells = Enumerable.Range(0, 25)
            .Select(index => surface.Cell(new Point(index % 5, index / 5)))
            .ToArray();

        // Act
        await surface.Pointer.MoveToAsync(title);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Assert
        title.IsPointerDirectlyOver.ShouldBeTrue();
        title.IsFocused.ShouldBeFalse();
        title.IsPressed.ShouldBeFalse();
        surface.Cell(default).Text.ShouldBe(" ");
        Enumerable.Range(0, 25)
            .Select(index => surface.Cell(new Point(index % 5, index / 5)))
            .ShouldBe(restingCells);
    }

    /// <summary>Verifies audited Small font output and mutation clear stale generated cells.</summary>
    [Fact]
    public async Task UpdateAsync_WhenFigletContentChanges_ReplacesExactAuditedArtAsync()
    {
        // Arrange
        var title = new FigletText(FigletCatalog.Default.Load("small"))
        {
            Content = "A",
            Face = AppearanceTestValues.Face(foreground: ReferenceColors.Get(14)),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };
        await using var surface = await ComponentSurface.MountAsync(
            title,
            new Size(7, 5),
            TestContext.Current.CancellationToken);
        surface.ShouldRender("""
                                _
                               /_\
                              / _ \
                             /_/ \_\

                             """);
        surface.Cell(new Point(3, 0)).Style.Foreground.ShouldBe(ReferenceColors.Get(14));

        // Act
        await surface.UpdateAsync(() => title.Content = "I", "change FigletText content");

        // Assert
        surface.ShouldRender("""
                              ___
                             |_ _|
                              | |
                             |___|

                             """);
        surface.Cell(new Point(5, 0)).Text.ShouldBe(" ");
    }

    /// <summary>Verifies parent clipping exposes only complete in-bounds generated cells.</summary>
    [Fact]
    public async Task Render_WhenFigletArtExceedsSurface_ClipsToMountedBoundsAsync()
    {
        // Arrange
        var title = new FigletText(FigletCatalog.Default.Load("small"))
        {
            Content = "A",
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Width = Length.Cells(7),
            Height = Length.Cells(5)
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            title,
            new Size(4, 3),
            TestContext.Current.CancellationToken);

        // Assert
        surface.ShouldRender("""
                                _
                               /_
                              / _
                             """);

        // Act
        await surface.ResizeAsync(new Size(7, 5));

        // Assert
        surface.ShouldRender("""
                                _
                               /_\
                              / _ \
                             /_/ \_\

                             """);
    }

    /// <summary>Verifies direct and ancestor-inherited disable painting, stable geometry across a
    /// genuine resize, and re-enable recovery for a mounted FigletText.</summary>
    [ComponentBehaviorEvidence(typeof(FigletText), ComponentBehavior.Disabled)]
    [Fact]
    public async Task IsEnabled_WhenFigletTextIsDisabled_ProvesDisabledContractAsync()
    {
        // Arrange
        var title = new FigletText(FigletCatalog.Default.Load("small"))
        {
            Content = "I",
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };
        await using var surface = await ComponentSurface.MountAsync(
            title,
            new Size(5, 5),
            TestContext.Current.CancellationToken);

        // Act — direct disable
        await surface.UpdateAsync(() => title.IsEnabled = false, "disable FigletText");

        // Assert
        surface.ShouldHaveState(title, VisualState.Disabled);

        // Arrange — ancestor-inherited disable
        var child = new FigletText(FigletCatalog.Default.Load("small"))
        {
            Content = "I",
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };
        var stack = new Stack { Children = { child } };
        await using var ancestorSurface = await ComponentSurface.MountAsync(
            stack,
            new Size(5, 5),
            TestContext.Current.CancellationToken);

        // Act
        await ancestorSurface.UpdateAsync(() => stack.IsEnabled = false, "disable ancestor Stack");

        // Assert
        child.EffectiveIsEnabled.ShouldBeFalse();
        ancestorSurface.ShouldHaveState(child, VisualState.Disabled);

        // Act — geometry stability across a genuine resize
        await surface.ResizeAsync(new Size(8, 6));
        var enabledTitle = new FigletText(FigletCatalog.Default.Load("small"))
        {
            Content = "I",
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };
        await using var enabledSurface = await ComponentSurface.MountAsync(
            enabledTitle,
            new Size(8, 6),
            TestContext.Current.CancellationToken);

        // Assert
        title.Bounds.ShouldBe(enabledTitle.Bounds);
        title.DesiredSize.ShouldBe(enabledTitle.DesiredSize);

        // Act — re-enable recovery
        await surface.UpdateAsync(() => title.IsEnabled = true, "re-enable FigletText");

        // Assert
        surface.ShouldHaveState(title, VisualState.Normal);
    }
}
