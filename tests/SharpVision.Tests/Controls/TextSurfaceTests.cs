// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies Text formatting and semantic cells through a mounted terminal surface.</summary>
public sealed class TextSurfaceTests
{
    /// <summary>Verifies markup styles apply to complete combining and wide grapheme cells.</summary>
    [Fact]
    public async Task Render_WhenMarkupContainsUnicode_PreservesStylesAndCellOwnershipAsync()
    {
        // Arrange
        var text = new ControlText("<b>A\u0301</b><fg=14>界</fg>")
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            text,
            new Size(4, 1),
            TestContext.Current.CancellationToken);

        // Assert
        surface.ShouldRender("A\u0301界");
        var combining = surface.Cell(default);
        combining.Text.ShouldBe("A\u0301");
        combining.Width.ShouldBe(1);
        combining.Style.Attributes.ShouldBe(Attributes.Bold);
        var wide = surface.Cell(new Point(1, 0));
        wide.Text.ShouldBe("界");
        wide.Width.ShouldBe(2);
        wide.Style.Foreground.ShouldBe(Color.Indexed(14));
        var continuation = surface.Cell(new Point(2, 0));
        continuation.IsContinuation.ShouldBeTrue();
        continuation.LeadX.ShouldBe(1);
    }

    /// <summary>Verifies ellipsis and alignment mutation replace every stale terminal cell.</summary>
    [Fact]
    public async Task UpdateAsync_WhenTextAndAlignmentChange_ReplacesFormattedCellsAsync()
    {
        // Arrange
        var text = new ControlText("abcdef")
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top,
            Overflow = Overflow.Ellipsis,
        };
        await using var surface = await ComponentSurface.MountAsync(
            text,
            new Size(4, 1),
            TestContext.Current.CancellationToken);
        surface.ShouldRender("abc…");

        // Act
        await surface.UpdateAsync(
            () =>
            {
                text.Content = "xy";
                text.TextAlignment = Alignment.End;
            },
            "replace and align Text content");

        // Assert
        surface.ShouldRender("  xy");
        surface.Cell(new Point(0, 0)).Text.ShouldBe(" ");
        surface.Cell(new Point(2, 0)).Text.ShouldBe("x");
        surface.Cell(new Point(3, 0)).Text.ShouldBe("y");
    }

    /// <summary>Verifies transparent Text preserves the opaque surface painted by its parent.</summary>
    [Fact]
    public async Task Render_WhenTextBackgroundIsTransparent_PreservesParentSurfaceAsync()
    {
        // Arrange
        var text = new ControlText("A")
        {
            FillMode = FillMode.Transparent,
            Foreground = Color.Indexed(15),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };
        var background = new Dock
        {
            Background = Color.Indexed(4),
            FillMode = FillMode.Opaque,
            Children = { text },
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            background,
            new Size(3, 1),
            TestContext.Current.CancellationToken);

        // Assert
        surface.ShouldRender("A");
        surface.Cell(default).Style.Foreground.ShouldBe(Color.Indexed(15));
        var blankBackground = surface.Cell(new Point(2, 0)).Style.Background;
        blankBackground.Kind.ShouldBe(ColorKind.Indexed);
        blankBackground.Red.ShouldBe((byte) 4);
        var textBackground = surface.Cell(default).Style.Background;
        textBackground.Kind.ShouldBe(ColorKind.Indexed);
        textBackground.Red.ShouldBe((byte) 4);
    }
}
