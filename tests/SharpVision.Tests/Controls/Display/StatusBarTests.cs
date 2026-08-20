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
        bar.Face.Background.ShouldBe(SemanticColor.Control);
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

    /// <summary>Verifies StatusBar proves direct and ancestor-inherited disabled state at the
    /// detached unit level, and that clearing IsEnabled on each recovers EffectiveIsEnabled - the
    /// same disabled contract exercised on a live mounted terminal surface.</summary>
    [ComponentUnitEvidence(typeof(StatusBar), ComponentBehavior.Disabled)]
    [Fact]
    public void EffectiveIsEnabled_WhenBarIsDisabledDirectlyOrByAncestor_ReportsDisabledAndRecovers()
    {
        using var bar = new StatusBar();
        using var host = new Stack();
        host.Children.Add(bar);

        bar.IsEnabled = false;
        bar.EffectiveIsEnabled.ShouldBeFalse();

        bar.IsEnabled = true;
        bar.EffectiveIsEnabled.ShouldBeTrue();

        host.IsEnabled = false;
        bar.IsEnabled.ShouldBeTrue();
        bar.EffectiveIsEnabled.ShouldBeFalse();

        host.IsEnabled = true;
        bar.EffectiveIsEnabled.ShouldBeTrue();
    }

    /// <summary>Verifies StatusBarItem proves direct and owning-bar-inherited disabled state at
    /// the detached unit level, and that clearing IsEnabled on each recovers EffectiveIsEnabled.</summary>
    [ComponentUnitEvidence(typeof(StatusBarItem), ComponentBehavior.Disabled)]
    [Fact]
    public void EffectiveIsEnabled_WhenItemIsDisabledDirectlyOrByOwningBar_ReportsDisabledAndRecovers()
    {
        using var bar = new StatusBar();
        var item = Item("Ready");
        bar.Items.Add(item);

        item.IsEnabled = false;
        item.EffectiveIsEnabled.ShouldBeFalse();

        item.IsEnabled = true;
        item.EffectiveIsEnabled.ShouldBeTrue();

        bar.IsEnabled = false;
        item.IsEnabled.ShouldBeTrue();
        item.EffectiveIsEnabled.ShouldBeFalse();

        bar.IsEnabled = true;
        item.EffectiveIsEnabled.ShouldBeTrue();
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
            ShowLeftSeparator = true,
            ShowRightSeparator = true,
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

    /// <summary>Verifies each actual separator falls back to the themed glyph when its own
    /// override is unset, and that assigning an override takes precedence for that side only.</summary>
    [Fact]
    public void ActualSeparators_WhenOverrideIsUnset_FallBackToThemedGlyph()
    {
        // Arrange
        using var item = new StatusBarItem { ShowLeftSeparator = true, ShowRightSeparator = true };

        // Assert defaults
        item.LeftSeparator.ShouldBeNull();
        item.RightSeparator.ShouldBeNull();
        item.ActualLeftSeparator.ShouldBe(item.ActualStyle.LeftSeparatorGlyph);
        item.ActualRightSeparator.ShouldBe(item.ActualStyle.RightSeparatorGlyph);

        // Act
        item.LeftSeparator = StatusBarSeparatorGlyphs.Chevron;

        // Assert only the overridden side changes
        item.ActualLeftSeparator.ShouldBe(StatusBarSeparatorGlyphs.Chevron);
        item.ActualRightSeparator.ShouldBe(item.ActualStyle.RightSeparatorGlyph);
    }

    /// <summary>Verifies a local Style's separator glyphs override the theme default and clearing
    /// the Style restores it, exercising StatusBarItem's own Style/ActualStyle round trip.</summary>
    [Fact]
    public void Style_WhenSeparatorGlyphsAreCustomized_OverrideDefaultsAndClearingRestores()
    {
        // Arrange
        using var item = new StatusBarItem();
        var defaultStyle = item.ActualStyle;

        // Act
        item.Style = defaultStyle with
        {
            LeftSeparatorGlyph = StatusBarSeparatorGlyphs.Diamond,
            RightSeparatorGlyph = StatusBarSeparatorGlyphs.Bullet
        };

        // Assert custom
        _ = item.Style.ShouldNotBeNull();
        item.ActualStyle.LeftSeparatorGlyph.ShouldBe(StatusBarSeparatorGlyphs.Diamond);
        item.ActualStyle.RightSeparatorGlyph.ShouldBe(StatusBarSeparatorGlyphs.Bullet);

        // Act reset
        item.Style = null;

        // Assert restored
        item.Style.ShouldBeNull();
        item.ActualStyle.LeftSeparatorGlyph.ShouldBe(defaultStyle.LeftSeparatorGlyph);
        item.ActualStyle.RightSeparatorGlyph.ShouldBe(defaultStyle.RightSeparatorGlyph);
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
            ShowLeftSeparator = true,
            ShowRightSeparator = true,
            LeftSeparator = StatusBarSeparatorGlyphs.Bar,
            RightSeparator = StatusBarSeparatorGlyphs.Chevron,
            Content = content
        };
        bar.Items.Add(item);

        // Act
        new LayoutEngine().Layout(bar, new Size(12, 1));

        // Assert
        item.DesiredSize.ShouldBe(new Size(7, 1));
        item.Bounds.ShouldBe(new Rect(0, 0, 7, 1));
        content.Bounds.ShouldBe(new Rect(1, 0, 5, 1));
    }

    /// <summary>Verifies a Spacing value other than the constructed default actually widens the
    /// gap the layout reserves between adjacent items, proving the setter's forwarded
    /// <c>_host.Spacing</c> assignment - not merely that the property round-trips.</summary>
    [Fact]
    public void Arrange_WhenSpacingChangesFromDefault_AdjustsGapBetweenAdjacentItems()
    {
        // Arrange
        using var bar = new StatusBar { Spacing = 3 };
        var first = Item("AA");
        var second = Item("BB");
        bar.Items.Add(first);
        bar.Items.Add(second);

        // Act
        new LayoutEngine().Layout(bar, new Size(20, 1));

        // Assert
        bar.Spacing.ShouldBe(3);
        first.Bounds.ShouldBe(new Rect(0, 0, 2, 1));
        second.Bounds.ShouldBe(new Rect(5, 0, 2, 1));
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
        new LayoutEngine().Layout(bar, new Size(24, 1));

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
        new LayoutEngine().Layout(bar, new Size(9, 1));

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
        var engine = new LayoutEngine();
        engine.Layout(bar, new Size(12, 1));
        item.Bounds.X.ShouldBe(0);

        // Act
        item.Alignment = StatusBarItemAlignment.Right;
        engine.Layout(bar, new Size(12, 1));

        // Assert
        item.Bounds.ShouldBe(new Rect(7, 0, 5, 1));
    }

    /// <summary>Verifies Insert places an item at the requested position without disturbing existing order.</summary>
    [Fact]
    public void Insert_WhenCalled_PlacesItemAtRequestedPosition()
    {
        using var bar = new StatusBar();
        var first = Item("First");
        var second = Item("Second");
        bar.Items.Add(first);
        bar.Items.Add(second);
        var inserted = Item("Inserted");

        bar.Items.Insert(1, inserted);

        bar.Items.ShouldBe([first, inserted, second]);
    }

    /// <summary>Verifies an out-of-range insertion index throws before mutating the collection.</summary>
    [Fact]
    public void Insert_WhenIndexIsOutOfRange_ThrowsBeforeMutation()
    {
        using var bar = new StatusBar();
        var item = Item("First");
        bar.Items.Add(item);

        _ = Should.Throw<ArgumentOutOfRangeException>(() => bar.Items.Insert(2, Item("New")));

        bar.Items.ShouldBe([item]);
    }

    /// <summary>Verifies RemoveAt detaches the item at a position without disposing it.</summary>
    [Fact]
    public void RemoveAt_WhenCalled_DetachesItemWithoutDisposal()
    {
        using var bar = new StatusBar();
        var first = Item("First");
        var second = Item("Second");
        bar.Items.Add(first);
        bar.Items.Add(second);

        bar.Items.RemoveAt(0);

        bar.Items.ShouldBe([second]);
        first.IsDisposed.ShouldBeFalse();
        first.Parent.ShouldBeNull();
    }

    /// <summary>Verifies an out-of-range removal index throws before mutating the collection.</summary>
    [Fact]
    public void RemoveAt_WhenIndexIsOutOfRange_ThrowsBeforeMutation()
    {
        using var bar = new StatusBar();
        var item = Item("First");
        bar.Items.Add(item);

        _ = Should.Throw<ArgumentOutOfRangeException>(() => bar.Items.RemoveAt(1));

        bar.Items.ShouldBe([item]);
    }

    /// <summary>Verifies the indexer replaces one item at a position, detaching the old one without disposal.</summary>
    [Fact]
    public void Indexer_WhenAssigned_ReplacesItemAtPositionWithoutDisposingOld()
    {
        using var bar = new StatusBar();
        var first = Item("First");
        var second = Item("Second");
        bar.Items.Add(first);
        bar.Items.Add(second);
        var replacement = Item("Replacement");

        bar.Items[0] = replacement;

        bar.Items.ShouldBe([replacement, second]);
        first.IsDisposed.ShouldBeFalse();
        first.Parent.ShouldBeNull();
    }

    /// <summary>Verifies assigning null through the indexer throws.</summary>
    [Fact]
    public void Indexer_WhenAssignedNull_Throws()
    {
        using var bar = new StatusBar();
        bar.Items.Add(Item("First"));

        _ = Should.Throw<ArgumentNullException>(() => bar.Items[0] = null!);
    }

    /// <summary>Verifies Move repositions an owned item while preserving its identity.</summary>
    [Fact]
    public void Move_WhenCalled_RepositionsItemPreservingIdentity()
    {
        using var bar = new StatusBar();
        var first = Item("First");
        var second = Item("Second");
        var third = Item("Third");
        bar.Items.Add(first);
        bar.Items.Add(second);
        bar.Items.Add(third);

        bar.Items.Move(0, 2);

        bar.Items.ShouldBe([second, third, first]);
    }

    /// <summary>Verifies an out-of-range move index throws before mutating the collection.</summary>
    [Fact]
    public void Move_WhenIndexIsOutOfRange_ThrowsBeforeMutation()
    {
        using var bar = new StatusBar();
        var first = Item("First");
        var second = Item("Second");
        bar.Items.Add(first);
        bar.Items.Add(second);

        _ = Should.Throw<ArgumentOutOfRangeException>(() => bar.Items.Move(0, 2));

        bar.Items.ShouldBe([first, second]);
    }

    /// <summary>Verifies IndexOf reports the current position of an owned item and -1 for a foreign item.</summary>
    [Fact]
    public void IndexOf_WhenItemIsOwnedOrForeign_ReportsPositionOrNegativeOne()
    {
        using var bar = new StatusBar();
        var first = Item("First");
        var second = Item("Second");
        bar.Items.Add(first);
        bar.Items.Add(second);
        using var foreign = Item("Foreign");

        bar.Items.IndexOf(second).ShouldBe(1);
        bar.Items.IndexOf(foreign).ShouldBe(-1);
    }

    /// <summary>Verifies disposed collection mutations reject Insert, RemoveAt, indexer assignment, and Move.</summary>
    [Fact]
    public void Items_WhenOwnerIsDisposed_RejectsInsertRemoveAtIndexerAndMove()
    {
        var bar = new StatusBar();
        bar.Items.Add(Item("First"));
        bar.Items.Add(Item("Second"));
        bar.Dispose();

        _ = Should.Throw<ObjectDisposedException>(() => bar.Items.Insert(0, Item("New")));
        _ = Should.Throw<ObjectDisposedException>(() => bar.Items.RemoveAt(0));
        _ = Should.Throw<ObjectDisposedException>(() => bar.Items[0] = Item("New"));
        _ = Should.Throw<ObjectDisposedException>(() => bar.Items.Move(0, 1));
    }

    /// <summary>Verifies a Collapsed item in the left bucket frees its space for surviving left
    /// siblings, while a Hidden item in the same bucket keeps its slot (and the spacing it
    /// participates in) but renders nothing.</summary>
    [ComponentVisibilityEvidence(
        typeof(StatusBar),
        ComponentVisibilityEvidence.HiddenRetainsSlot |
        ComponentVisibilityEvidence.HiddenExcludesRenderInput |
        ComponentVisibilityEvidence.CollapsedExcludesSize |
        ComponentVisibilityEvidence.CollapsedRemovesSpacingOrTrack |
        ComponentVisibilityEvidence.TransitionInvalidatesCorrectly)]
    [Fact]
    public void Arrange_WhenLeftItemIsCollapsed_FreesSpaceForRemainingLeftSiblings()
    {
        // Arrange
        using var bar = new StatusBar { Spacing = 1 };
        var first = Item("First");
        var middle = Item("Middle");
        var last = Item("Last");
        bar.Items.Add(first);
        bar.Items.Add(middle);
        bar.Items.Add(last);
        var engine = new LayoutEngine();
        var size = new Size(30, 1);
        engine.Layout(bar, size);
        var baselineWidth = bar.DesiredSize.Width;
        var baselineMiddleBounds = middle.Bounds;
        var baselineLastBounds = last.Bounds;

        // Act
        middle.Visibility = Visibility.Collapsed;
        engine.Layout(bar, size);

        // Assert - "Last" closes the gap left by the collapsed "Middle" and the bar narrows by
        // exactly the collapsed item's outer width plus the one spacing cell it no longer needs.
        first.Bounds.ShouldBe(new Rect(0, 0, 5, 1));
        last.Bounds.ShouldBe(new Rect(6, 0, 4, 1));
        bar.DesiredSize.Width.ShouldBe(baselineWidth - baselineMiddleBounds.Width - 1);
        using Frame frame = new(size);
        bar.Render(frame.Canvas);
        FrameOracle.Get(frame, new Point(6, 0)).ShouldBe("L");

        // Act - Hidden instead retains Middle's exact slot and spacing (byte-identical to the
        // fully IsVisible baseline), only excluding rendering.
        middle.Visibility = Visibility.Hidden;
        engine.Layout(bar, size);

        first.Bounds.ShouldBe(new Rect(0, 0, 5, 1));
        middle.Bounds.ShouldBe(baselineMiddleBounds);
        last.Bounds.ShouldBe(baselineLastBounds);
        bar.DesiredSize.Width.ShouldBe(baselineWidth);
        using Frame hiddenFrame = new(size);
        bar.Render(hiddenFrame.Canvas);
        FrameOracle.Get(hiddenFrame, new Point(middle.Bounds.X, 0)).ShouldBeEmpty();
    }

    /// <summary>Verifies a Collapsed item in the right bucket frees its space for a sibling further
    /// from the trailing edge, mirroring the left-bucket contract. The right bucket is allocated in
    /// reverse collection order starting at the physical trailing edge, so the item actually closest
    /// to that edge ("Ln 1", added last) never moves regardless of what collapses behind it - only
    /// "UTF-8", allocated after "Ln 1" in the walk, shifts to close the gap.</summary>
    [ComponentVisibilityEvidence(
        typeof(StatusBar),
        ComponentVisibilityEvidence.CollapsedExcludesSize |
        ComponentVisibilityEvidence.CollapsedRemovesSpacingOrTrack |
        ComponentVisibilityEvidence.TransitionInvalidatesCorrectly)]
    [Fact]
    public void Arrange_WhenRightItemIsCollapsed_FreesSpaceForRemainingRightSiblings()
    {
        // Arrange
        using var bar = new StatusBar { Spacing = 1 };
        var encoding = Item("UTF-8", StatusBarItemAlignment.Right);
        var position = Item("Ln 1", StatusBarItemAlignment.Right);
        bar.Items.Add(encoding);
        bar.Items.Add(position);
        var engine = new LayoutEngine();
        var size = new Size(20, 1);
        engine.Layout(bar, size);
        var baselineEncodingX = encoding.Bounds.X;
        var baselinePositionBounds = position.Bounds;

        // Act
        position.Visibility = Visibility.Collapsed;
        engine.Layout(bar, size);

        // Assert - "UTF-8" moves right to occupy the trailing edge itself once "Ln 1" is collapsed.
        encoding.Bounds.ShouldBe(new Rect(bar.Bounds.Right - 5, 0, 5, 1));
        encoding.Bounds.X.ShouldBeGreaterThan(baselineEncodingX);

        // Act - restore visibility; the original two-item layout returns exactly.
        position.Visibility = Visibility.Visible;
        engine.Layout(bar, size);

        encoding.Bounds.X.ShouldBe(baselineEncodingX);
        position.Bounds.ShouldBe(baselinePositionBounds);
    }

    /// <summary>Verifies the private alignment-bucket Count() helper - which every arrange and
    /// measure branch consults to decide whether inter-item spacing is owed - excludes Collapsed
    /// items from the count, so a single remaining item after a collapse no longer reserves the
    /// spacing cell a two-item bucket would.</summary>
    [ComponentVisibilityEvidence(typeof(StatusBar), ComponentVisibilityEvidence.CollapsedRemovesSpacingOrTrack)]
    [Fact]
    public void Measure_WhenOneOfTwoLeftItemsIsCollapsed_ExcludesItFromSpacingCount()
    {
        // Arrange
        using var bar = new StatusBar { Spacing = 1 };
        var first = Item("First");
        var second = Item("Second");
        bar.Items.Add(first);
        bar.Items.Add(second);
        var engine = new LayoutEngine();
        var size = new Size(30, 1);
        engine.Layout(bar, size);
        var twoItemWidth = bar.DesiredSize.Width;
        var secondWidth = second.Bounds.Width;

        // Act
        second.Visibility = Visibility.Collapsed;
        engine.Layout(bar, size);

        // Assert - width drops by the collapsed item's own outer width plus the spacing cell that
        // a lone survivor no longer needs (Count() based, not an unconditional per-child spacer).
        bar.DesiredSize.Width.ShouldBe(twoItemWidth - secondWidth - 1);
    }

    private static StatusBarItem Item(
        string content,
        StatusBarItemAlignment alignment = StatusBarItemAlignment.Left) => new()
        {
            Alignment = alignment,
            Content = new ControlText(content)
        };
}
