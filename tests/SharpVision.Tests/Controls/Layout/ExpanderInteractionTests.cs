// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Layout;

/// <summary>Verifies mounted Expander behavior: focus leaving collapsed content and returning
/// once re-expanded, ExpandedChanged arguments per activation path, Space activating on release,
/// and a one-cell host.</summary>
public sealed class ExpanderInteractionTests
{
    private static Expander CreateExpander(ControlBase content) => new()
    {
        HeaderText = "Head",
        Content = content,
        HorizontalAlignment = HorizontalAlignment.Stretch,
        VerticalAlignment = VerticalAlignment.Stretch
    };

    /// <summary>Verifies collapsing through a header click while the content owns focus moves
    /// focus to the header, keeps the collapsed content out of Tab traversal, and re-expanding
    /// through Enter makes the content reachable again.</summary>
    [Fact]
    public async Task Focus_WhenContentOwnsFocusAndHeaderCollapses_MovesFocusOffTheHiddenContentAsync()
    {
        // Arrange
        var box = new CheckBox { Text = "Opt" };
        var trailing = new CheckBox { Text = "After" };
        var expander = CreateExpander(box);
        var host = new Stack { Children = { expander, trailing } };
        var changes = new List<bool>();
        expander.ExpandedChanged += (_, args) => changes.Add(args.IsExpanded);
        await using var surface = await ComponentSurface.MountAsync(
            host,
            new Size(12, 4),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(box);

        // Act collapse with the pointer
        await surface.Pointer.ClickAsync(expander, new Point(2, 0));

        // Assert
        expander.IsExpanded.ShouldBeFalse();
        changes.ShouldBe([false]);
        surface.ShouldHaveFocus(expander);
        box.Bounds.ShouldBe(default);
        trailing.Bounds.Y.ShouldBe(1);

        // Act Tab skips the collapsed content
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(trailing);

        // Act re-expand through Enter on the header
        await surface.Keyboard.PressAsync(Code.Tab, Modifiers.Shift);
        surface.ShouldHaveFocus(expander);
        await surface.Keyboard.PressAsync(Code.Enter);

        // Assert
        expander.IsExpanded.ShouldBeTrue();
        changes.ShouldBe([false, true]);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(box);
        box.Bounds.Height.ShouldBe(1);
    }

    /// <summary>Verifies Space toggles expansion, publishing exactly one transition per complete
    /// press-and-release, while a held press alone never toggles twice.</summary>
    [Fact]
    public async Task Keyboard_WhenSpaceIsPressedAndReleased_TogglesOncePerCompleteStrokeAsync()
    {
        // Arrange
        var expander = CreateExpander(new ControlText("Body"));
        var changes = new List<bool>();
        expander.ExpandedChanged += (_, args) => changes.Add(args.IsExpanded);
        await using var surface = await ComponentSurface.MountAsync(
            expander,
            new Size(12, 3),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(expander);

        // Act
        await surface.Keyboard.CompleteCharacterAsync(new Rune(' '));

        // Assert
        expander.IsExpanded.ShouldBeFalse();
        changes.ShouldBe([false]);
        surface.ShouldRender("▶ Head      \n            \n            ");

        // Act
        await surface.Keyboard.CompleteCharacterAsync(new Rune(' '));

        // Assert
        expander.IsExpanded.ShouldBeTrue();
        changes.ShouldBe([false, true]);
        surface.Cell(new Point(2, 1)).Text.ShouldBe("B");
    }

    /// <summary>Verifies a one-cell host shows only the disclosure glyph, still toggles through
    /// the pointer, and recovers the full header and content on growth.</summary>
    [Fact]
    public async Task ResizeAsync_WhenHostShrinksToOneCell_KeepsTheDisclosureGlyphAndRecoversAsync()
    {
        // Arrange
        var expander = CreateExpander(new ControlText("Body"));
        await using var surface = await ComponentSurface.MountAsync(
            expander,
            new Size(8, 2),
            TestContext.Current.CancellationToken);
        surface.ShouldRender("▼ Head  \n  Body  ");

        // Act
        await surface.ResizeAsync(new Size(1, 1));

        // Assert
        surface.ShouldRender("▼");
        await surface.Pointer.ClickAsync(expander, new Point(0, 0));
        expander.IsExpanded.ShouldBeFalse();
        surface.ShouldRender("▶");

        // Act
        await surface.ResizeAsync(new Size(8, 2));

        // Assert
        surface.ShouldRender("▶ Head  \n        ");
    }
}
