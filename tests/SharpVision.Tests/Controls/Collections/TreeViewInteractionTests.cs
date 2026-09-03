// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Collections;

/// <summary>Verifies TreeView keyboard and pointer gestures through mounted surfaces: modified
/// range and toggle navigation, modified clicks across collapsed subtrees, cancelled selection
/// keeping current separate from selected, disabled rows, removal of the current item, the
/// wheel, glyph-versus-text hit targets per indent, wide headers, and disposal mid-gesture.</summary>
public sealed class TreeViewInteractionTests
{
    /// <summary>Verifies Shift-modified movement extends the range from the anchor while
    /// Control-modified movement toggles each row it lands on, both in Multiple mode.</summary>
    [Fact]
    public async Task Keyboard_WhenMovementCarriesShiftOrControl_ExtendsRangeOrTogglesAsync()
    {
        // Arrange
        var (tree, items) = CreateTree(TreeSelectionMode.Multiple);
        await using var surface = await ComponentSurface.MountAsync(
            tree,
            new Size(20, 8),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Down);
        tree.SelectedItems.ShouldBe([items["A"]]);

        // Act and assert Shift ranges from the anchor
        await surface.Keyboard.PressAsync(Code.Down, Modifiers.Shift);
        Headers(tree).ShouldBe(["A", "a1"]);
        await surface.Keyboard.PressAsync(Code.Down, Modifiers.Shift);
        Headers(tree).ShouldBe(["A", "a1", "a2"]);
        await surface.Keyboard.PressAsync(Code.End, Modifiers.Shift);
        Headers(tree).ShouldBe(["A", "a1", "a2", "B", "C", "c1", "c1x"]);
        await surface.Keyboard.PressAsync(Code.Home, Modifiers.Shift);
        Headers(tree).ShouldBe(["A"]);

        // Act and assert Control toggles while moving
        await surface.Keyboard.PressAsync(Code.Down, Modifiers.Control);
        Headers(tree).ShouldBe(["A", "a1"]);
        await surface.Keyboard.PressAsync(Code.Down, Modifiers.Control);
        Headers(tree).ShouldBe(["A", "a1", "a2"]);
        await surface.Keyboard.PressAsync(Code.Up, Modifiers.Control);
        Headers(tree).ShouldBe(["A", "a2"]);
        tree.SelectedItem.ShouldBeSameAs(items["A"]);

        // Act plain movement replaces the whole set
        await surface.Keyboard.PressAsync(Code.Down);
        Headers(tree).ShouldBe(["a2"]);
    }

    /// <summary>Verifies Control-click toggles, Shift-click ranges only over currently visible
    /// rows, collapsing keeps hidden descendants selected, and a later Shift range replaces them.</summary>
    [Fact]
    public async Task Pointer_WhenModifiedClicksSpanACollapsedSubtree_RangeOverVisibleRowsOnlyAsync()
    {
        // Arrange
        var (tree, items) = CreateTree(TreeSelectionMode.Multiple);
        await using var surface = await ComponentSurface.MountAsync(
            tree,
            new Size(20, 8),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);

        // Act
        await surface.Pointer.ClickAsync(items["a1"], new Point(4, 0));
        await surface.Pointer.ClickAsync(items["B"], Modifiers.Control);
        await surface.Pointer.ClickAsync(items["c1x"], Modifiers.Shift);

        // Assert the range runs from the Control-click anchor
        Headers(tree).ShouldBe(["B", "C", "c1", "c1x"]);

        // Act collapse C through its disclosure glyph
        await surface.Pointer.ClickAsync(items["C"], new Point(0, 0));

        // Assert hidden descendants stay selected
        items["C"].IsExpanded.ShouldBeFalse();
        Headers(tree).ShouldBe(["B", "C", "c1", "c1x"]);

        // Act a Shift range over the now shorter visible list
        await surface.Pointer.ClickAsync(items["A"], Modifiers.Shift);

        // Assert
        Headers(tree).ShouldBe(["A", "a1", "a2", "B"]);
        items["c1"].IsSelected.ShouldBeFalse();
        items["c1x"].IsSelected.ShouldBeFalse();
    }

    /// <summary>Verifies a cancelled keyboard selection still moves the current row: the refused
    /// row carries the rendered current cue (the underline the row contributes for its Current
    /// state) while the logical selection stays refused, and a later accepted move selects
    /// normally. BorderlessContainer paints no distinct background for Selected or Current on a
    /// TreeView row - the underline is this theme's only rendered cue for either - so this proof
    /// is underline- and logical-state-only rather than a background-colour comparison.</summary>
    [Fact]
    public async Task Keyboard_WhenSelectionChangingCancels_MovesCurrentWithoutSelectingAsync()
    {
        // Arrange
        var (tree, items) = CreateTree(TreeSelectionMode.Single);
        tree.SelectionChanging += (_, eventArgs) =>
            eventArgs.Cancel = eventArgs.AddedItems.Any(item => item.Header == "a1");
        await using var surface = await ComponentSurface.MountAsync(
            tree,
            new Size(20, 8),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Down);

        // Act
        await surface.Keyboard.PressAsync(Code.Down);

        // Assert - the current cue moved to the refused row; the logical selection stays put
        tree.SelectedItem.ShouldBeSameAs(items["A"]);
        items["a1"].IsSelected.ShouldBeFalse();
        (HeaderCell(surface, items["a1"]).Attributes & TerminalAttributes.Underline).ShouldBe(TerminalAttributes.Underline);
        (HeaderCell(surface, items["A"]).Attributes & TerminalAttributes.Underline).ShouldBe(TerminalAttributes.None);

        // Act an accepted move from the refused current row
        await surface.Keyboard.PressAsync(Code.Down);

        // Assert
        tree.SelectedItem.ShouldBeSameAs(items["a2"]);
        (HeaderCell(surface, items["a2"]).Attributes & TerminalAttributes.Underline).ShouldBe(TerminalAttributes.Underline);
        (HeaderCell(surface, items["a1"]).Attributes & TerminalAttributes.Underline).ShouldBe(TerminalAttributes.None);
    }

    private static TerminalStyle HeaderCell(ComponentSurface surface, TreeViewItem item) =>
        surface.Cell(new Point(item.Bounds.X + (2 * item.Depth) + 2, item.Bounds.Y)).Style;

    /// <summary>Verifies a disabled row stays rendered but is skipped by keyboard navigation and
    /// ignores pointer selection and invocation.</summary>
    [Fact]
    public async Task Input_WhenRowIsDisabled_IsSkippedByKeysAndInertToPointerAsync()
    {
        // Arrange
        var (tree, items) = CreateTree(TreeSelectionMode.Single);
        items["a1"].IsEnabled = false;
        var invoked = new List<string>();
        tree.ItemInvoked += (_, eventArgs) => invoked.Add(eventArgs.Item.Header);
        await using var surface = await ComponentSurface.MountAsync(
            tree,
            new Size(20, 8),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act keyboard
        await surface.Keyboard.PressAsync(Code.Down);
        await surface.Keyboard.PressAsync(Code.Down);

        // Assert the disabled row was skipped - never current, never selected - and still rendered
        tree.SelectedItem.ShouldBeSameAs(items["a2"]);
        var bounds = items["a1"].Bounds;
        bounds.Height.ShouldBe(1);
        surface.Cell(new Point(bounds.X + 4, bounds.Y)).Text.ShouldBe("a");
        surface.Cell(new Point(bounds.X + 5, bounds.Y)).Text.ShouldBe("1");
        (HeaderCell(surface, items["a1"]).Attributes & TerminalAttributes.Underline).ShouldBe(TerminalAttributes.None);
        (HeaderCell(surface, items["a2"]).Attributes & TerminalAttributes.Underline).ShouldBe(TerminalAttributes.Underline);
        surface.ShouldHaveState(items["a1"], VisualState.Disabled);

        // Act pointer
        await surface.Pointer.ClickAsync(items["a1"], new Point(4, 0));

        // Assert
        tree.SelectedItem.ShouldBeSameAs(items["a2"]);
        invoked.ShouldBeEmpty();
        await surface.Keyboard.PressAsync(Code.Up);
        tree.SelectedItem.ShouldBeSameAs(items["A"]);
    }

    /// <summary>Verifies removing the current, selected row clears selection and current, so the
    /// next Down starts again from the first visible row.</summary>
    [Fact]
    public async Task Items_WhenCurrentRowIsRemoved_ClearsSelectionAndRestartsNavigationAsync()
    {
        // Arrange
        var (tree, items) = CreateTree(TreeSelectionMode.Single);
        var changes = new List<string>();
        tree.SelectionChanged += (_, eventArgs) =>
            changes.Add($"{eventArgs.PreviousItem?.Header ?? "-"}>{eventArgs.CurrentItem?.Header ?? "-"}");
        await using var surface = await ComponentSurface.MountAsync(
            tree,
            new Size(20, 8),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Down);
        await surface.Keyboard.PressAsync(Code.Down);
        await surface.Keyboard.PressAsync(Code.Down);
        tree.SelectedItem.ShouldBeSameAs(items["a2"]);

        // Act
        await surface.UpdateAsync(() => items["A"].Children.Remove(items["a2"]).ShouldBeTrue(), "remove current row");

        // Assert
        tree.SelectedItem.ShouldBeNull();
        items["a2"].IsSelected.ShouldBeFalse();
        changes.ShouldBe(["->A", "A>a1", "a1>a2", "a2>-"]);

        // Act
        await surface.Keyboard.PressAsync(Code.Down);

        // Assert navigation restarted at the top
        tree.SelectedItem.ShouldBeSameAs(items["A"]);
    }

    /// <summary>Verifies the wheel scrolls the items container by LineSize per notch without
    /// touching selection and saturates at both ends.</summary>
    [Fact]
    public async Task Pointer_WhenWheelScrolls_MovesViewportByLineSizeOnlyAsync()
    {
        // Arrange
        var tree = new TreeView
        {
            LineSize = 2,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        for (var index = 0; index < 30; index++)
        {
            tree.Items.Add(new TreeViewItem($"Row {index:D2}"));
        }

        await using var surface = await ComponentSurface.MountAsync(
            tree,
            new Size(20, 5),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);
        await surface.Pointer.ClickAsync(tree.Items[1], new Point(2, 0));
        tree.SelectedItem.ShouldBeSameAs(tree.Items[1]);

        // Act
        await surface.Pointer.WheelAsync(tree, new Point(2, 2), wheelY: -1);
        await surface.Pointer.WheelAsync(tree, new Point(2, 2), wheelY: -1);

        // Assert
        tree.VerticalOffset.ShouldBe(4);
        tree.SelectedItem.ShouldBeSameAs(tree.Items[1]);
        tree.Items[4].Bounds.Y.ShouldBe(tree.Bounds.Y);

        // Act back past the top
        await surface.Pointer.WheelAsync(tree, new Point(2, 2), wheelY: 1);
        await surface.Pointer.WheelAsync(tree, new Point(2, 2), wheelY: 1);
        await surface.Pointer.WheelAsync(tree, new Point(2, 2), wheelY: 1);

        // Assert
        tree.VerticalOffset.ShouldBe(0);
    }

    /// <summary>Verifies the disclosure glyph and the header text remain distinct hit targets at
    /// every indent, so a nested row toggles from its glyph cell and selects from its text.</summary>
    /// <param name="indent">The cells per depth level.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(4)]
    public async Task Pointer_WhenIndentVaries_GlyphTogglesAndTextSelectsAsync(int indent)
    {
        // Arrange
        var (tree, items) = CreateTree(TreeSelectionMode.Single);
        tree.Indent = indent;
        var invoked = new List<string>();
        tree.ItemInvoked += (_, eventArgs) => invoked.Add(eventArgs.Item.Header);
        await using var surface = await ComponentSurface.MountAsync(
            tree,
            new Size(24, 8),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);
        var nested = items["c1"];
        nested.Depth.ShouldBe(1);

        // Act glyph
        await surface.Pointer.ClickAsync(nested, new Point(indent, 0));

        // Assert toggled without selecting: the child row leaves realization entirely
        nested.IsExpanded.ShouldBeFalse();
        tree.SelectedItem.ShouldBeNull();
        invoked.ShouldBeEmpty();
        items["c1x"].Parent.ShouldBeNull();
        surface.Cell(new Point(nested.Bounds.X, nested.Bounds.Y + 1)).Text.ShouldBe(" ");

        // Act text
        await surface.Pointer.ClickAsync(nested, new Point(indent + 2, 0));

        // Assert selected and invoked, expansion unchanged
        tree.SelectedItem.ShouldBeSameAs(nested);
        invoked.ShouldBe(["c1"]);
        nested.IsExpanded.ShouldBeFalse();

        // Act glyph again re-expands and keeps selection
        await surface.Pointer.ClickAsync(nested, new Point(indent, 0));
        nested.IsExpanded.ShouldBeTrue();
        tree.SelectedItem.ShouldBeSameAs(nested);
        _ = items["c1x"].Parent.ShouldNotBeNull();
        items["c1x"].Bounds.Y.ShouldBe(nested.Bounds.Y + 1);
        items["c1x"].Bounds.Height.ShouldBe(1);
    }

    /// <summary>Verifies a wide-character header occupies continuation cells after the glyph and
    /// leading space, and clicking any of those cells selects the row.</summary>
    [Fact]
    public async Task Render_WhenHeaderIsWide_OccupiesContinuationCellsAndStaysClickableAsync()
    {
        // Arrange
        var wide = new TreeViewItem("日本 file");
        var tree = new TreeView
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Items = { new TreeViewItem("Top"), wide }
        };
        await using var surface = await ComponentSurface.MountAsync(
            tree,
            new Size(16, 4),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);
        var origin = wide.Bounds;

        // Assert cells
        surface.Cell(new Point(origin.X, origin.Y)).Text.ShouldBe(" ");
        surface.Cell(new Point(origin.X + 2, origin.Y)).Text.ShouldBe("日");
        surface.Cell(new Point(origin.X + 3, origin.Y)).Continuation.ShouldBeTrue();
        surface.Cell(new Point(origin.X + 4, origin.Y)).Text.ShouldBe("本");
        surface.Cell(new Point(origin.X + 5, origin.Y)).Continuation.ShouldBeTrue();
        surface.Cell(new Point(origin.X + 7, origin.Y)).Text.ShouldBe("f");
        wide.DesiredSize.Width.ShouldBe(2 + 9);

        // Act
        await surface.Pointer.ClickAsync(wide, new Point(3, 0));

        // Assert
        tree.SelectedItem.ShouldBeSameAs(wide);
    }

    /// <summary>Verifies a SelectionChanged handler that disposes the tree mid-keystroke completes
    /// without throwing and leaves nothing focused on the disposed control.</summary>
    [Fact]
    public async Task SelectionChanged_WhenHandlerDisposesTree_CompletesAndReleasesFocusAsync()
    {
        // Arrange
        var (tree, _) = CreateTree(TreeSelectionMode.Single);
        var host = new Overlay { Children = { tree } };
        tree.SelectionChanged += (_, _) => tree.Dispose();
        await using var surface = await ComponentSurface.MountAsync(
            host,
            new Size(20, 8),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(tree);

        // Act
        await surface.Keyboard.PressAsync(Code.Down);

        // Assert
        tree.IsDisposed.ShouldBeTrue();
        tree.Parent.ShouldBeNull();
        surface.Application.Focus.Focused.ShouldNotBeSameAs(tree);
        surface.ShouldRender("");
    }

    /// <summary>Verifies Enter with no current row selects and invokes the first visible row, and
    /// Space on a plain row in Multiple mode toggles it without invoking.</summary>
    [Fact]
    public async Task Keyboard_WhenEnterHasNoCurrentAndSpaceToggles_ActOnFirstAndCurrentRowsAsync()
    {
        // Arrange
        var (tree, items) = CreateTree(TreeSelectionMode.Multiple);
        var invoked = new List<string>();
        tree.ItemInvoked += (_, eventArgs) => invoked.Add(eventArgs.Item.Header);
        await using var surface = await ComponentSurface.MountAsync(
            tree,
            new Size(20, 8),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act
        await surface.Keyboard.PressAsync(Code.Enter);

        // Assert
        tree.SelectedItem.ShouldBeSameAs(items["A"]);
        invoked.ShouldBe(["A"]);

        // Act Space toggles the current row off and on
        await surface.Keyboard.CompleteCharacterAsync(new Rune(' '));
        tree.SelectedItems.ShouldBeEmpty();
        await surface.Keyboard.CompleteCharacterAsync(new Rune(' '));
        Headers(tree).ShouldBe(["A"]);
        invoked.ShouldBe(["A"]);
    }

    /// <summary>Verifies an arrow carrying an application-command modifier is left unhandled on a
    /// mounted tree: current, selection, expansion, and the rendered rows stay unchanged.</summary>
    /// <param name="modifiers">The modifier held with the arrow.</param>
    [Theory]
    [InlineData(Modifiers.Alt)]
    [InlineData(Modifiers.Super)]
    public async Task Keyboard_WhenArrowCarriesCommandModifier_LeavesTreeUnchangedAsync(Modifiers modifiers)
    {
        // Arrange
        var (tree, items) = CreateTree(TreeSelectionMode.Single);
        var changes = 0;
        tree.SelectionChanged += (_, _) => changes++;
        await using var surface = await ComponentSurface.MountAsync(
            tree,
            new Size(20, 8),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Down);
        tree.SelectedItem.ShouldBeSameAs(items["A"]);
        changes = 0;
        var before = string.Concat(Enumerable.Range(0, 8).Select(y => surface.Cell(new Point(4, y)).Text));

        // Act
        await surface.Keyboard.PressAsync(Code.Down, modifiers);
        await surface.Keyboard.PressAsync(Code.Left, modifiers);
        await surface.Keyboard.PressAsync(Code.Right, modifiers);
        await surface.Keyboard.PressAsync(Code.End, modifiers);

        // Assert
        tree.SelectedItem.ShouldBeSameAs(items["A"]);
        items["A"].IsExpanded.ShouldBeTrue();
        changes.ShouldBe(0);
        (HeaderCell(surface, items["A"]).Attributes & TerminalAttributes.Underline).ShouldBe(TerminalAttributes.Underline);
        string.Concat(Enumerable.Range(0, 8).Select(y => surface.Cell(new Point(4, y)).Text)).ShouldBe(before);
    }

    /// <summary>Verifies held-key repeats of Down keep moving current and selection one row per
    /// repeat, exactly like initial presses, and a repeat past the last row saturates.</summary>
    [Fact]
    public async Task Keyboard_WhenDownRepeats_ContinuesNavigationPerRepeatAsync()
    {
        // Arrange
        var (tree, items) = CreateTree(TreeSelectionMode.Single);
        var selected = new List<string>();
        tree.SelectionChanged += (_, _) => selected.Add(tree.SelectedItem?.Header ?? string.Empty);
        await using var surface = await ComponentSurface.MountAsync(
            tree,
            new Size(20, 8),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act
        await surface.Keyboard.PressAsync(Code.Down);
        await surface.Keyboard.RepeatAsync(Code.Down);
        await surface.Keyboard.RepeatAsync(Code.Down);

        // Assert
        tree.SelectedItem.ShouldBeSameAs(items["a2"]);
        selected.ShouldBe(["A", "a1", "a2"]);
        (HeaderCell(surface, items["a2"]).Attributes & TerminalAttributes.Underline).ShouldBe(TerminalAttributes.Underline);

        // Act repeats through the end of the visible rows
        for (var repeat = 0; repeat < 6; repeat++)
        {
            await surface.Keyboard.RepeatAsync(Code.Down);
        }

        // Assert saturation on the last visible row
        tree.SelectedItem.ShouldBeSameAs(items["c1x"]);
        selected.Count.ShouldBe(7);
    }

    /// <summary>Verifies Left on a leaf moves current and selection to its parent on a mounted
    /// surface, a second Left collapses that parent so the leaf's row disappears, and Right
    /// re-expands it.</summary>
    [Fact]
    public async Task Keyboard_WhenLeftIsPressedOnALeaf_MovesToTheParentThenCollapsesItAsync()
    {
        // Arrange
        var (tree, items) = CreateTree(TreeSelectionMode.Single);
        await using var surface = await ComponentSurface.MountAsync(
            tree,
            new Size(20, 8),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);
        await surface.Pointer.ClickAsync(items["a2"], new Point(6, 0));
        tree.SelectedItem.ShouldBeSameAs(items["a2"]);
        var leafRow = items["a2"].Bounds.Y;

        // Act
        await surface.Keyboard.PressAsync(Code.Left);

        // Assert the parent is current and selected while still expanded
        tree.SelectedItem.ShouldBeSameAs(items["A"]);
        items["A"].IsExpanded.ShouldBeTrue();
        (HeaderCell(surface, items["A"]).Attributes & TerminalAttributes.Underline).ShouldBe(TerminalAttributes.Underline);
        (HeaderCell(surface, items["a2"]).Attributes & TerminalAttributes.Underline).ShouldBe(TerminalAttributes.None);
        surface.Cell(new Point(4, leafRow)).Text.ShouldBe("a");

        // Act Left again collapses the parent
        await surface.Keyboard.PressAsync(Code.Left);

        // Assert
        items["A"].IsExpanded.ShouldBeFalse();
        tree.SelectedItem.ShouldBeSameAs(items["A"]);
        items["a2"].Parent.ShouldBeNull();
        surface.Cell(new Point(2, items["A"].Bounds.Y + 1)).Text.ShouldBe("B");

        // Act Right re-expands and a further Right enters the first child
        await surface.Keyboard.PressAsync(Code.Right);
        items["A"].IsExpanded.ShouldBeTrue();
        tree.SelectedItem.ShouldBeSameAs(items["A"]);
        await surface.Keyboard.PressAsync(Code.Right);

        // Assert
        tree.SelectedItem.ShouldBeSameAs(items["a1"]);
        surface.Cell(new Point(4, items["A"].Bounds.Y + 1)).Text.ShouldBe("a");
    }

    /// <summary>Verifies collapsing a row whose child request failed hides its status row, and
    /// re-expanding it shows the failed status again without issuing a new request until the
    /// user retries with Enter, which then commits the children.</summary>
    [Fact]
    public async Task Keyboard_WhenFailedRowIsCollapsedAndReExpanded_KeepsFailureUntilRetryAsync()
    {
        // Arrange
        var source = new FakeTreeViewChildSource();
        source.FailNext(null, new InvalidOperationException("boom"));
        var root = new TreeViewItem("Root") { ChildSource = source };
        var tree = new TreeView
        {
            Width = Length.Cells(45),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Items = { root }
        };
        await using var surface = await ComponentSurface.MountAsync(
            tree,
            new Size(45, 3),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);
        await DialogWait.UntilAsync(surface, root, () => root.ChildState == TreeViewChildState.LoadFailed);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Down);
        tree.SelectedItem.ShouldBeSameAs(root);
        surface.Cell(new Point(2, 1)).Text.ShouldBe("✗");

        // Act collapse
        await surface.Keyboard.PressAsync(Code.Left);

        // Assert the status row leaves the surface and the failure is retained
        root.IsExpanded.ShouldBeFalse();
        root.ChildState.ShouldBe(TreeViewChildState.LoadFailed);
        surface.Cell(new Point(2, 1)).Text.ShouldBe(" ");
        OwnedTree.Find<TreeViewStatusRow>(tree).ShouldBeNull();

        // Act re-expand
        source.AddChildren(null, new TreeViewChildDescription("a", "A") { Presence = TreeViewChildPresence.Leaf });
        await surface.Keyboard.PressAsync(Code.Right);

        // Assert the retained failure shows again without a silent retry
        root.IsExpanded.ShouldBeTrue();
        root.ChildState.ShouldBe(TreeViewChildState.LoadFailed);
        root.Children.Count.ShouldBe(0);
        surface.Cell(new Point(2, 1)).Text.ShouldBe("✗");

        // Act retry
        await surface.Keyboard.PressAsync(Code.Enter);
        await DialogWait.UntilAsync(surface, root, () => root.ChildState == TreeViewChildState.Loaded);

        // Assert
        root.Children.Count.ShouldBe(1);
        surface.ShouldRender("""
                             ▼ Root
                                 A

                             """);
    }

    private static (TreeView Tree, Dictionary<string, TreeViewItem> Items) CreateTree(TreeSelectionMode mode)
    {
        var items = new Dictionary<string, TreeViewItem>(StringComparer.Ordinal);

        TreeViewItem Item(string header)
        {
            var item = new TreeViewItem(header);
            items[header] = item;
            return item;
        }

        var a = Item("A");
        a.Children.Add(Item("a1"));
        a.Children.Add(Item("a2"));
        var c = Item("C");
        var c1 = Item("c1");
        c1.Children.Add(Item("c1x"));
        c.Children.Add(c1);
        var tree = new TreeView
        {
            SelectionMode = mode,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Items = { a, Item("B"), c }
        };
        return (tree, items);
    }

    private static string[] Headers(TreeView tree) => [.. tree.SelectedItems.Select(item => item.Header)];
}
