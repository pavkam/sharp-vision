// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Collections;

using SharpVision.Tests.Styling;

/// <summary>Verifies TreeView composition, selection, expand/collapse, keyboard navigation, and pointer interaction through mounted surfaces.</summary>
public sealed class TreeViewSurfaceTests
{
    /// <summary>Verifies indented hierarchy, Tab entry, directional selection, keyboard and pointer activation, and unavailable cleanup.</summary>
    [ComponentBehaviorEvidence(
        typeof(TreeView),
        ComponentBehavior.Mounted |
        ComponentBehavior.Hover |
        ComponentBehavior.Focus |
        ComponentBehavior.Tab |
        ComponentBehavior.Directional |
        ComponentBehavior.PressReleaseExcluded |
        ComponentBehavior.Activation |
        ComponentBehavior.PointerActivation |
        ComponentBehavior.KeyboardActivation |
        ComponentBehavior.UnavailableCleanup |
        ComponentBehavior.Composition)]
    [ComponentBehaviorEvidence(
        typeof(TreeViewItem),
        ComponentBehavior.Mounted |
        ComponentBehavior.Hover |
        ComponentBehavior.FocusExcluded |
        ComponentBehavior.TabExcluded |
        ComponentBehavior.DirectionalExcluded |
        ComponentBehavior.PressReleaseExcluded |
        ComponentBehavior.Activation |
        ComponentBehavior.PointerActivation |
        ComponentBehavior.UnavailableCleanup)]
    [Fact]
    public async Task Render_WhenTreeIsPopulated_DrawsIndentedHierarchyAsync()
    {
        // Arrange
        var documents = new TreeViewItem { Header = "Documents" };
        var reports = new TreeViewItem { Header = "Reports" };
        var q1 = new TreeViewItem { Header = "Q1.txt" };
        var q2 = new TreeViewItem { Header = "Q2.txt" };
        reports.Children.Add(q1);
        reports.Children.Add(q2);
        documents.Children.Add(reports);
        var images = new TreeViewItem { Header = "Images" };
        var photo = new TreeViewItem { Header = "photo.jpg" };
        images.Children.Add(photo);
        var tree = CreateTree(20);
        tree.Items.Add(documents);
        tree.Items.Add(images);
        var invocations = new List<string>();
        tree.ItemInvoked += (_, e) => invocations.Add(e.Item.Header);
        await using var surface = await ComponentSurface.MountAsync(
            tree,
            new Size(20, 7),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);

        // Assert composition and mounted evidence
        surface.ShouldRender("""
                              ▼ Documents
                                ▼ Reports
                                    Q1.txt
                                    Q2.txt
                              ▼ Images
                                  photo.jpg

                              """);

        // Assert excluded item behaviors
        documents.Focused.ShouldBeFalse();
        documents.CanTabStop.ShouldBeFalse();

        // Act Tab entry
        await surface.Keyboard.PressAsync(Code.Tab);

        // Assert focus entry precedes directional selection
        tree.SelectedItem.ShouldBeNull();
        surface.ShouldHaveState(tree, VisualState.Focused);
        documents.Focused.ShouldBeFalse();

        // Act keyboard activation on first entry
        await surface.Keyboard.PressAsync(Code.Enter);
        tree.SelectedItem.ShouldBeSameAs(documents);
        invocations.Count.ShouldBeGreaterThan(0);
        invocations.Last().ShouldBe("Documents");
        invocations.Clear();

        // Act directional navigation
        await surface.Keyboard.PressAsync(Code.Down);
        tree.SelectedItem.ShouldBeSameAs(reports);

        await surface.Keyboard.PressAsync(Code.Down);
        tree.SelectedItem.ShouldBeSameAs(q1);
        surface.ShouldHaveFocus(tree);

        // Act pointer hover
        await surface.Pointer.MoveToAsync(q2);
        q2.PointerOver.ShouldBeTrue();

        // Act pointer press without capture for PressReleaseExcluded evidence
        await surface.Pointer.PressAsync();
        q2.Pressed.ShouldBeFalse();

        // Act pointer release selects item
        await surface.Pointer.ReleaseAsync();
        tree.SelectedItem.ShouldBeSameAs(q2);
        surface.ShouldHaveState(tree, VisualState.PointerOver | VisualState.Focused);
        surface.ShouldHaveFocus(tree);
        tree.Pressed.ShouldBeFalse();

        // Act and assert TreeViewItem unavailable cleanup
        await surface.Pointer.MoveToAsync(documents);
        documents.PointerOver.ShouldBeTrue();
        await surface.UpdateAsync(() => documents.Enabled = false, "disable TreeViewItem");
        documents.PointerOver.ShouldBeFalse();

        // Act and assert TreeView unavailable cleanup
        await surface.UpdateAsync(() => tree.Enabled = false, "disable focused TreeView");
        tree.Focused.ShouldBeFalse();
        surface.ShouldHaveFocus(null);
    }

    /// <summary>Verifies Left arrow collapses an expanded parent and hides its children from the rendered output.</summary>
    [Fact]
    public async Task Input_WhenCollapseToggled_HidesChildrenAsync()
    {
        // Arrange
        var parent = new TreeViewItem { Header = "Parent" };
        var childA = new TreeViewItem { Header = "Child A" };
        var childB = new TreeViewItem { Header = "Child B" };
        parent.Children.Add(childA);
        parent.Children.Add(childB);
        var other = new TreeViewItem { Header = "Other" };
        other.Children.Add(new TreeViewItem { Header = "Deep" });
        var tree = CreateTree(14);
        tree.Items.Add(parent);
        tree.Items.Add(other);
        await using var surface = await ComponentSurface.MountAsync(
            tree,
            new Size(14, 5),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);

        // Assert initial expanded state
        surface.ShouldRender("""
                              ▼ Parent
                                  Child A
                                  Child B
                              ▼ Other
                                  Deep
                              """);

        // Act Tab in and select parent
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Down);
        tree.SelectedItem.ShouldBeSameAs(parent);

        // Act collapse via Left arrow
        await surface.Keyboard.PressAsync(Code.Left);

        // Assert children are hidden
        parent.Expanded.ShouldBeFalse();
        surface.ShouldRender("""
                              ▶ Parent
                              ▼ Other
                                  Deep


                              """);
    }

    /// <summary>Verifies Down arrow walks the visible DFS order and updates selection on each step.</summary>
    [Fact]
    public async Task Input_WhenDownArrowNavigates_SelectsItemsAsync()
    {
        // Arrange
        var root = new TreeViewItem { Header = "Root" };
        var child = new TreeViewItem { Header = "Child" };
        root.Children.Add(child);
        var sibling = new TreeViewItem { Header = "Sibling" };
        var nested = new TreeViewItem { Header = "Nested" };
        sibling.Children.Add(nested);
        var tree = CreateTree(20);
        tree.Items.Add(root);
        tree.Items.Add(sibling);
        var observations = new List<string>();
        tree.SelectionChanged += (_, _) => observations.Add(tree.SelectedItem?.Header ?? "none");
        await using var surface = await ComponentSurface.MountAsync(
            tree,
            new Size(20, 5),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);

        // Act Tab entry
        await surface.Keyboard.PressAsync(Code.Tab);
        tree.SelectedItem.ShouldBeNull();

        // Act Down through visible items
        await surface.Keyboard.PressAsync(Code.Down);
        tree.SelectedItem.ShouldBeSameAs(root);

        await surface.Keyboard.PressAsync(Code.Down);
        tree.SelectedItem.ShouldBeSameAs(child);

        await surface.Keyboard.PressAsync(Code.Down);
        tree.SelectedItem.ShouldBeSameAs(sibling);

        await surface.Keyboard.PressAsync(Code.Down);
        tree.SelectedItem.ShouldBeSameAs(nested);

        // Act Down past last item stays on last
        await surface.Keyboard.PressAsync(Code.Down);
        tree.SelectedItem.ShouldBeSameAs(nested);

        // Assert full selection event order
        observations.ShouldBe(["Root", "Child", "Sibling", "Nested"]);
    }

    /// <summary>Verifies a pointer click on a tree view item commits selection through the owning tree view.</summary>
    [Fact]
    public async Task Pointer_WhenItemClicked_SelectsItemAsync()
    {
        // Arrange
        var first = new TreeViewItem { Header = "First" };
        var second = new TreeViewItem { Header = "Second" };
        var tree = CreateTree(20);
        tree.Items.Add(first);
        tree.Items.Add(second);
        await using var surface = await ComponentSurface.MountAsync(
            tree,
            new Size(20, 4),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);

        // Act
        await surface.Pointer.ClickAsync(second);

        // Assert
        tree.SelectedItem.ShouldBeSameAs(second);
        second.Selected.ShouldBeTrue();
        first.Selected.ShouldBeFalse();
        surface.ShouldHaveFocus(tree);
    }

    /// <summary>Verifies check glyph rendering and Space toggling propagate through a mounted tree.</summary>
    [Fact]
    public async Task Input_WhenCheckableNodeIsCurrent_TogglesCheckStateAsync()
    {
        var parent = new TreeViewItem { Header = "Parent", Checkable = true };
        var child = new TreeViewItem { Header = "Child", Checkable = true };
        parent.Children.Add(child);
        var tree = CreateTree(20);
        tree.Items.Add(parent);

        await using var surface = await ComponentSurface.MountAsync(
            tree,
            new Size(20, 4),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);

        // Disclosure at 0, a gap at 1, then the default three-cell bracket mark.
        surface.Cell(new Point(1, 0)).Text.ShouldBe(" ");
        surface.Cell(new Point(2, 0)).Text.ShouldBe("[");
        surface.Cell(new Point(3, 0)).Text.ShouldBe(" ");
        surface.Cell(new Point(4, 0)).Text.ShouldBe("]");

        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Down);
        await surface.Keyboard.PressCharacterAsync(new Rune(' '));

        parent.IsChecked.ShouldBe(true);
        child.IsChecked.ShouldBe(true);
        surface.Cell(new Point(3, 0)).Text.ShouldBe("✓");
    }

    /// <summary>
    /// Verifies a bracket mark renders its three cells, keeps the header offset consistent with the
    /// measured reservation, and toggles from any cell of the mark rather than only its first.
    /// </summary>
    [Fact]
    public async Task Pointer_WhenBracketMarkIsClicked_RendersThreeCellsAndTogglesFromAnyCellAsync()
    {
        var item = new TreeViewItem { Header = "Item", Checkable = true };
        var tree = CreateTree(20);
        tree.CheckMark = CheckMark.Brackets;
        tree.Items.Add(item);

        await using var surface = await ComponentSurface.MountAsync(
            tree,
            new Size(20, 4),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);

        // One disclosure cell, a gap, the three-cell mark, then the leading space before the
        // header.
        surface.Cell(new Point(1, 0)).Text.ShouldBe(" ");
        surface.Cell(new Point(2, 0)).Text.ShouldBe("[");
        surface.Cell(new Point(3, 0)).Text.ShouldBe(" ");
        surface.Cell(new Point(4, 0)).Text.ShouldBe("]");
        surface.Cell(new Point(6, 0)).Text.ShouldBe("I");

        // The closing bracket is the cell a single-cell hit test used to miss.
        await surface.Pointer.ClickAsync(item, new Point(4, 0));

        item.IsChecked.ShouldBe(true);
        surface.Cell(new Point(3, 0)).Text.ShouldBe("✓");

        await surface.Pointer.ClickAsync(item, new Point(2, 0));

        item.IsChecked.ShouldBe(false);
    }

    /// <summary>Verifies PageDown/PageUp move the current item and mark the record handled, so it
    /// does not escape to page the enclosing scrollable container out from under the still-focused
    /// tree.</summary>
    [Fact]
    public async Task Input_WhenPageKeysArePressed_MoveCurrentWithoutEscapingToOuterContainerAsync()
    {
        // Arrange
        var tree = new TreeView { Height = Length.Cells(6), HorizontalAlignment = HorizontalAlignment.Stretch };
        List<TreeViewItem> items = [.. Enumerable.Range(0, 12).Select(index => new TreeViewItem { Header = $"Item {index}" })];

        foreach (var item in items)
        {
            tree.Items.Add(item);
        }

        var outer = new Stack
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Vertical,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Children = { tree }
        };
        await using var surface = await ComponentSurface.MountAsync(
            outer,
            new Size(20, 8),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Down);
        tree.SelectedItem.ShouldBeSameAs(items[0]);

        // Act
        await surface.Keyboard.PressAsync(Code.PageDown);

        // Assert - the record was handled by the tree, so the outer Stack never sees it.
        outer.VerticalOffset.ShouldBe(0);
        tree.SelectedItem.ShouldNotBeSameAs(items[0]);
    }

    private static TreeView CreateTree(int width)
    {
        var tree = new TreeView
        {
            Width = Length.Cells(width),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        return tree;
    }

    /// <summary>The regression this file exists to pin: both sides agree under a theme that
    /// restyles the mark family.</summary>
    [Fact]
    public async Task ActualCheckMark_WhenThemeAuthorsCheckBoxGlyphs_MatchesTheCheckBoxAsync()
    {
        var checkBox = new CheckBox();
        var item = new TreeViewItem { Header = "Row", Checkable = true };
        var tree = new TreeView { Items = { item } };
        var root = new Stack { Children = { checkBox, tree } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(24, 8),
            TestContext.Current.CancellationToken);

        await surface.UpdateAsync(() => surface.Application.Theme = TickTheme(), "author tick glyphs");

        var expected = checkBox.ActualStyle.Glyphs;
        item.ActualCheckMark.Glyphs.ShouldBe(
            expected,
            "a tree row and a CheckBox must render the same themed mark family");
        tree.ActualCheckMark.Glyphs.ShouldBe(expected);
    }

    /// <summary>Verifies the mark style travels too, not only the glyph trio - a themed one-cell
    /// family and a three-cell bracket family occupy different widths.</summary>
    [Fact]
    public async Task ActualCheckMark_WhenThemeAuthorsMarkStyle_MatchesTheCheckBoxAsync()
    {
        var checkBox = new CheckBox();
        var item = new TreeViewItem { Header = "Row", Checkable = true };
        var tree = new TreeView { Items = { item } };
        var root = new Stack { Children = { checkBox, tree } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(24, 8),
            TestContext.Current.CancellationToken);

        await surface.UpdateAsync(() => surface.Application.Theme = TickTheme(), "author tick mark style");

        item.ActualCheckMark.MarkStyle.ShouldBe(checkBox.ActualStyle.MarkStyle);
    }

    /// <summary>Verifies replacing the theme re-resolves the fallback rather than latching the
    /// family observed at attachment.</summary>
    [Fact]
    public async Task ActualCheckMark_WhenThemeIsReplaced_FollowsTheNewFamilyAsync()
    {
        var item = new TreeViewItem { Header = "Row", Checkable = true };
        var tree = new TreeView { Items = { item } };
        await using var surface = await ComponentSurface.MountAsync(
            tree,
            new Size(24, 8),
            TestContext.Current.CancellationToken);
        var before = item.ActualCheckMark.Glyphs;

        await surface.UpdateAsync(() => surface.Application.Theme = TickTheme(), "swap to tick glyphs");

        item.ActualCheckMark.Glyphs.ShouldNotBe(before);
        item.ActualCheckMark.Glyphs.Checked.ShouldBe(new Rune('X'));
    }

    /// <summary>The counter-case that keeps the change honest: an explicit per-item override still
    /// wins over the theme, so this did not turn a local override into a suggestion.</summary>
    [Fact]
    public async Task ActualCheckMark_WhenItemOverridesTheMark_KeepsTheOverrideUnderAThemeAsync()
    {
        var item = new TreeViewItem
        {
            Header = "Row",
            Checkable = true,
            CheckMark = CheckMark.Brackets
        };
        var tree = new TreeView { Items = { item } };
        await using var surface = await ComponentSurface.MountAsync(
            tree,
            new Size(24, 8),
            TestContext.Current.CancellationToken);

        await surface.UpdateAsync(() => surface.Application.Theme = TickTheme(), "author tick glyphs");

        item.ActualCheckMark.ShouldBe(CheckMark.Brackets);
    }

    /// <summary>Verifies the unthemed default is unchanged, so the fifteen bundled themes - none of
    /// which author <c>checkBox</c> - render exactly as before.</summary>
    [Fact]
    public void ActualCheckMark_WhenNoThemeAuthorsCheckBox_KeepsTheCodeOwnedFamily()
    {
        using var tree = new TreeView();

        tree.ActualCheckMark.Glyphs.ShouldBe(CheckMark.Brackets.Glyphs);
        tree.ActualCheckMark.MarkStyle.ShouldBe(CheckMark.Brackets.MarkStyle);
    }

    // Differs from the code-owned brackets family in both the mark style and the glyph trio.
    // Authoring markStyle alone would leave Glyphs on the code-owned brackets, which would make the
    // glyph assertions above pass vacuously.
    private static Theme TickTheme() =>
        ThemeCatalog.Parse(
            ThemeJson.Create(
                extraStyles:
                """, "checkBox": { "normal": { "markStyle": "tick", "glyphs": { "unchecked": ".", "checked": "X", "indeterminate": "-" } } } """));
}
