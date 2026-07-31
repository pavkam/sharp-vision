// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Display;

/// <summary>Verifies StatusBar item ownership, alignment, validation, and layout contracts.</summary>
public sealed class StatusBarTests
{
    /// <summary>Verifies the conventional one-row passive strip and item defaults.</summary>
    [ComponentUnitEvidence(typeof(StatusBar))]
    [ComponentUnitEvidence(typeof(StatusBarItem))]
    [Fact]
    public void Constructor_WhenCreated_UsesDocumentedDefaults()
    {
        // Arrange and act
        using var bar = new StatusBar();
        using var item = new StatusBarItem();

        // Assert
        bar.Items.ShouldBeEmpty();
        bar.Spacing.ShouldBe(1);
        bar.Height.ShouldBe(Length.Cells(1));
        bar.HorizontalAlignment.ShouldBe(HorizontalAlignment.Stretch);
        bar.Face.Background.ShouldBe(ThemeColor.Control);
        bar.CanFocus.ShouldBeFalse();
        item.Alignment.ShouldBe(StatusBarItemAlignment.Left);
        item.LeftSeparator.ShouldBeNull();
        item.RightSeparator.ShouldBeNull();
        item.CanFocus.ShouldBeFalse();
        StatusBarSeparatorGlyphs.Whitespace.ShouldBe(new Rune(' '));
        StatusBarSeparatorGlyphs.Bar.ShouldBe(new Rune('│'));
        StatusBarSeparatorGlyphs.Bullet.ShouldBe(new Rune('•'));
        StatusBarSeparatorGlyphs.Chevron.ShouldBe(new Rune('›'));
        StatusBarSeparatorGlyphs.Diamond.ShouldBe(new Rune('◆'));
    }

    /// <summary>Verifies typed ownership rejects invalid reuse and permits detached reuse after removal.</summary>
    [Fact]
    public void Items_WhenMutated_EnforcesSingleParentOwnership()
    {
        // Arrange
        using var first = new StatusBar();
        using var second = new StatusBar();
        using var item = Item("Ready");

        // Act and assert
        first.Items.Add(item);
        first.Items.ShouldBe([item]);
        _ = Should.Throw<ArgumentException>(() => first.Items.Add(item));
        _ = Should.Throw<ArgumentException>(() => second.Items.Add(item));
        first.Items.Remove(item).ShouldBeTrue();
        second.Items.Add(item);
        second.Items.ShouldBe([item]);
    }

    /// <summary>Verifies invalid alignment and spacing fail before observable state changes.</summary>
    [Fact]
    public void Setters_WhenValuesAreInvalid_ThrowBeforeMutation()
    {
        // Arrange
        using var bar = new StatusBar();
        using var item = new StatusBarItem();

        // Act
        _ = Should.Throw<ArgumentOutOfRangeException>(() => bar.Spacing = -1);
        _ = Should.Throw<ArgumentOutOfRangeException>(
            () => item.Alignment = (StatusBarItemAlignment) 99);

        // Assert
        bar.Spacing.ShouldBe(1);
        item.Alignment.ShouldBe(StatusBarItemAlignment.Left);
    }

    /// <summary>Verifies separators reject controls and wide glyphs before changing valid values.</summary>
    [Fact]
    public void Separators_WhenGlyphIsNotOnePrintableCell_ThrowBeforeMutation()
    {
        // Arrange
        using var item = new StatusBarItem
        {
            LeftSeparator = StatusBarSeparatorGlyphs.Bar,
            RightSeparator = StatusBarSeparatorGlyphs.Chevron
        };

        // Act
        _ = Should.Throw<ArgumentException>(() => item.LeftSeparator = new Rune('界'));
        _ = Should.Throw<ArgumentException>(() => item.RightSeparator = new Rune('\n'));

        // Assert
        item.LeftSeparator.ShouldBe(StatusBarSeparatorGlyphs.Bar);
        item.RightSeparator.ShouldBe(StatusBarSeparatorGlyphs.Chevron);
    }

    /// <summary>Verifies separator cells enlarge the item and inset its retained content.</summary>
    [Fact]
    public void Arrange_WhenItemHasBothSeparators_ReservesOneCellOnEachSide()
    {
        // Arrange
        using var bar = new StatusBar();
        var content = new ControlText("Ready");
        var item = new StatusBarItem
        {
            LeftSeparator = StatusBarSeparatorGlyphs.Bar,
            RightSeparator = StatusBarSeparatorGlyphs.Chevron,
            Content = content
        };
        bar.Items.Add(item);

        // Act
        new Engine().Layout(bar, new Size(12, 1));

        // Assert
        item.DesiredSize.ShouldBe(new Size(7, 1));
        item.Bounds.ShouldBe(new Rect(0, 0, 7, 1));
        content.Bounds.ShouldBe(new Rect(1, 0, 5, 1));
    }

    /// <summary>Verifies left and right groups preserve order and anchor to their respective edges.</summary>
    [Fact]
    public void Arrange_WhenItemsUseBothAlignments_AnchorsOrderedGroupsToEdges()
    {
        // Arrange
        using var bar = new StatusBar { Spacing = 1 };
        var ready = Item("Ready");
        var branch = Item("main");
        var encoding = Item("UTF-8", StatusBarItemAlignment.Right);
        var position = Item("Ln 1", StatusBarItemAlignment.Right);
        bar.Items.Add(ready);
        bar.Items.Add(encoding);
        bar.Items.Add(branch);
        bar.Items.Add(position);

        // Act
        new Engine().Layout(bar, new Size(24, 1));

        // Assert
        ready.Bounds.ShouldBe(new Rect(0, 0, 5, 1));
        branch.Bounds.ShouldBe(new Rect(6, 0, 4, 1));
        encoding.Bounds.ShouldBe(new Rect(14, 0, 5, 1));
        position.Bounds.ShouldBe(new Rect(20, 0, 4, 1));
    }

    /// <summary>Verifies trailing status survives first while leading status yields in a tiny viewport.</summary>
    [Fact]
    public void Arrange_WhenWidthIsTight_PreservesTrailingEdgeBeforeLeadingItems()
    {
        // Arrange
        using var bar = new StatusBar { Spacing = 1 };
        var message = Item("Saving document");
        var encoding = Item("UTF-8", StatusBarItemAlignment.Right);
        var position = Item("Ln 42", StatusBarItemAlignment.Right);
        bar.Items.Add(message);
        bar.Items.Add(encoding);
        bar.Items.Add(position);

        // Act
        new Engine().Layout(bar, new Size(9, 1));

        // Assert
        message.Bounds.Width.ShouldBe(0);
        encoding.Bounds.ShouldBe(new Rect(0, 0, 3, 1));
        position.Bounds.ShouldBe(new Rect(4, 0, 5, 1));
        position.Bounds.Right.ShouldBe(bar.Bounds.Right);
    }

    /// <summary>Verifies changing alignment invalidates layout and moves the retained item.</summary>
    [Fact]
    public void Alignment_WhenChanged_RepositionsTheOwnedItem()
    {
        // Arrange
        using var bar = new StatusBar();
        var item = Item("Ready");
        bar.Items.Add(item);
        var engine = new Engine();
        engine.Layout(bar, new Size(12, 1));
        item.Bounds.X.ShouldBe(0);

        // Act
        item.Alignment = StatusBarItemAlignment.Right;
        engine.Layout(bar, new Size(12, 1));

        // Assert
        item.Bounds.ShouldBe(new Rect(7, 0, 5, 1));
    }

    private static StatusBarItem Item(
        string content,
        StatusBarItemAlignment alignment = StatusBarItemAlignment.Left) => new()
        {
            Alignment = alignment,
            Content = new ControlText(content)
        };
}
