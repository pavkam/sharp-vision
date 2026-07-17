// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies GroupBox header, border, content, style, clipping, and resize through mounted surfaces.</summary>
public sealed class GroupBoxSurfaceTests
{
    /// <summary>Verifies a mounted GroupBox observes descendant hover without taking focus or press state.</summary>
    [ComponentBehaviorEvidence(
        typeof(GroupBox),
        ComponentBehavior.Mounted |
        ComponentBehavior.Hover |
        ComponentBehavior.FocusExcluded |
        ComponentBehavior.TabExcluded |
        ComponentBehavior.DirectionalExcluded |
        ComponentBehavior.PressReleaseExcluded |
        ComponentBehavior.Composition)]
    [Fact]
    public async Task Pointer_WhenContentIsHovered_TracksComposedAncestryWithoutInteractionAsync()
    {
        // Arrange
        var content = new ControlText("Body");
        var group = new GroupBox
        {
            Header = "Details",
            Content = content,
            Width = Length.Cells(12),
            Height = Length.Cells(3),
        };
        await using var surface = await ComponentSurface.MountAsync(
            group,
            new Size(12, 3),
            TestContext.Current.CancellationToken);

        // Act
        await surface.Pointer.MoveToAsync(content);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Assert
        content.Parent.ShouldBeSameAs(group);
        group.IsPointerOver.ShouldBeTrue();
        group.IsPointerDirectlyOver.ShouldBeFalse();
        content.IsPointerDirectlyOver.ShouldBeTrue();
        group.IsFocused.ShouldBeFalse();
        group.IsPressed.ShouldBeFalse();
        content.IsFocused.ShouldBeFalse();
    }

    /// <summary>Verifies empty and wide headers draw exact continuous or interrupted rounded frames.</summary>
    [Fact]
    public async Task Render_WhenHeaderIsEmptyOrWide_DrawsExactFrameAndContentAsync()
    {
        // Arrange empty header
        var empty = new GroupBox
        {
            Glyphs = Glyphs.Rounded,
            Content = new ControlText("Hi"),
            Width = Length.Cells(8),
            Height = Length.Cells(3),
        };
        await using var emptySurface = await ComponentSurface.MountAsync(
            empty,
            new Size(8, 3),
            TestContext.Current.CancellationToken);

        // Assert empty header
        empty.Content.Bounds.ShouldBe(new Rect(1, 1, 6, 1));
        emptySurface.ShouldRender("""
            ╭──────╮
            │Hi    │
            ╰──────╯
            """);

        // Arrange wide header
        var wide = new GroupBox
        {
            Glyphs = Glyphs.Rounded,
            Header = "界 Tools",
            Content = new ControlText("Body"),
            Width = Length.Cells(12),
            Height = Length.Cells(3),
        };
        await using var wideSurface = await ComponentSurface.MountAsync(
            wide,
            new Size(12, 3),
            TestContext.Current.CancellationToken);

        // Assert wide header
        wideSurface.ShouldRender("""
            ╭ 界 Tools ╮
            │Body      │
            ╰──────────╯
            """);
        wideSurface.Cell(new Point(2, 0)).Text.ShouldBe("界");
        wideSurface.Cell(new Point(3, 0)).IsContinuation.ShouldBeTrue();
    }

    /// <summary>Verifies a tiny header clips between preserved corners and resize reveals content without stale cells.</summary>
    [Fact]
    public async Task ResizeAsync_WhenHeaderStartsTiny_PreservesCornersThenRevealsFrameAsync()
    {
        // Arrange
        var group = new GroupBox
        {
            Glyphs = Glyphs.Rounded,
            Header = "Long title",
            Content = new ControlText("Body"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        await using var surface = await ComponentSurface.MountAsync(
            group,
            new Size(5, 2),
            TestContext.Current.CancellationToken);
        surface.ShouldRender("""
            ╭ Lo╮
            ╰───╯
            """);

        // Act
        await surface.ResizeAsync(new Size(14, 3));

        // Assert
        group.Content.Bounds.ShouldBe(new Rect(1, 1, 12, 1));
        surface.ShouldRender("""
            ╭ Long title ╮
            │Body        │
            ╰────────────╯
            """);
    }

    /// <summary>Verifies direct foreground reaches content while border color remains explicit.</summary>
    [Fact]
    public async Task Render_WhenAppearanceIsAssigned_AppliesForegroundAcrossFrameAndContentAsync()
    {
        // Arrange
        var content = new ControlText("Styled");
        var group = new GroupBox
        {
            Header = "Theme",
            Content = content,
            Foreground = Color.Indexed(2),
            BorderColor = Color.Indexed(2),
            Width = Length.Cells(10),
            Height = Length.Cells(3),
        };
        await using var surface = await ComponentSurface.MountAsync(
            group,
            new Size(10, 3),
            TestContext.Current.CancellationToken);

        // Assert
        surface.Cell(default).Style.Foreground.Kind.ShouldBe(ColorKind.Indexed);
        surface.Cell(default).Style.Foreground.Red.ShouldBe((byte) 2);
        surface.Cell(new Point(2, 0)).Style.Foreground.Red.ShouldBe((byte) 2);
        surface.Cell(new Point(1, 1)).Style.Foreground.Red.ShouldBe((byte) 2);
    }
}
