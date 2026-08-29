// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Collections;

/// <summary>Verifies TreeView composition, selection, expand/collapse, keyboard navigation, and pointer interaction through mounted surfaces.</summary>
public sealed class TreeViewSurfaceTests
{
    /// <summary>Verifies a selection callback that disables or detaches the TreeView ends the
    /// current pointer transaction before ItemInvoked can publish.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Pointer_WhenSelectionCallbackMakesTreeUnavailable_DoesNotInvokeItemAsync(bool detach)
    {
        // Arrange
        var item = new TreeViewItem { Header = "One" };
        var tree = CreateTree(8);
        tree.Items.Add(item);
        var root = new Overlay { Children = { tree } };
        var invoked = 0;
        tree.ItemInvoked += (_, _) => invoked++;
        tree.SelectionChanged += (_, _) =>
        {
            if (detach)
            {
                _ = root.Children.Remove(tree);
            }
            else
            {
                tree.IsEnabled = false;
            }
        };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(8, 2),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);

        // Act
        await surface.Pointer.ClickAsync(item);

        // Assert
        invoked.ShouldBe(0);
    }

    /// <summary>Verifies indented hierarchy, Tab entry, directional selection, keyboard and pointer activation, and unavailable cleanup.</summary>
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
        documents.IsFocused.ShouldBeFalse();
        documents.CanTabStop.ShouldBeFalse();

        // Act Tab entry
        await surface.Keyboard.PressAsync(Code.Tab);

        // Assert focus entry precedes directional selection
        tree.SelectedItem.ShouldBeNull();
        surface.ShouldHaveState(tree, VisualState.Focused);
        documents.IsFocused.ShouldBeFalse();

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
        q2.IsPointerOver.ShouldBeTrue();

        // Act pointer press without capture for PressReleaseExcluded evidence
        await surface.Pointer.PressAsync();
        q2.IsPressed.ShouldBeFalse();

        // Act pointer release selects item
        await surface.Pointer.ReleaseAsync();
        tree.SelectedItem.ShouldBeSameAs(q2);
        surface.ShouldHaveState(tree, VisualState.IsPointerOver | VisualState.Focused);
        surface.ShouldHaveFocus(tree);
        tree.IsPressed.ShouldBeFalse();

        // Act and assert TreeViewItem unavailable cleanup and direct disable
        await surface.Pointer.MoveToAsync(documents);
        documents.IsPointerOver.ShouldBeTrue();
        await surface.UpdateAsync(() => documents.IsEnabled = false, "disable TreeViewItem");
        documents.IsPointerOver.ShouldBeFalse();
        surface.ShouldHaveState(documents, VisualState.Disabled);

        // Act and assert TreeView unavailable cleanup and direct disable
        await surface.UpdateAsync(() => tree.IsEnabled = false, "disable focused TreeView");
        tree.IsFocused.ShouldBeFalse();
        surface.ShouldHaveFocus(null);
        surface.ShouldHaveState(tree, VisualState.Disabled);

        // Assert a never-directly-disabled TreeViewItem inherits Disabled from its owning
        // TreeView rather than only from its own IsEnabled flag.
        images.IsEnabled.ShouldBeTrue();
        images.EffectiveIsEnabled.ShouldBeFalse();
        surface.ShouldHaveState(images, VisualState.Disabled);

        // Act a genuine resize while disabled and assert geometry stability against an
        // independently mounted, otherwise-identical enabled tree at the same new size.
        await surface.ResizeAsync(new Size(26, 9));
        var disabledTreeBounds = tree.Bounds;
        var disabledTreeDesiredSize = tree.DesiredSize;
        var disabledDocumentsBounds = documents.Bounds;
        var disabledDocumentsDesiredSize = documents.DesiredSize;

        var referenceDocuments = new TreeViewItem { Header = "Documents" };
        var referenceReports = new TreeViewItem { Header = "Reports" };
        var referenceQ1 = new TreeViewItem { Header = "Q1.txt" };
        var referenceQ2 = new TreeViewItem { Header = "Q2.txt" };
        referenceReports.Children.Add(referenceQ1);
        referenceReports.Children.Add(referenceQ2);
        referenceDocuments.Children.Add(referenceReports);
        var referenceImages = new TreeViewItem { Header = "Images" };
        var referencePhoto = new TreeViewItem { Header = "photo.jpg" };
        referenceImages.Children.Add(referencePhoto);
        var referenceTree = CreateTree(20);
        referenceTree.Items.Add(referenceDocuments);
        referenceTree.Items.Add(referenceImages);
        await using var referenceSurface = await ComponentSurface.MountAsync(
            referenceTree,
            new Size(26, 9),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);

        referenceTree.Bounds.ShouldBe(disabledTreeBounds);
        referenceTree.DesiredSize.ShouldBe(disabledTreeDesiredSize);
        referenceDocuments.Bounds.ShouldBe(disabledDocumentsBounds);
        referenceDocuments.DesiredSize.ShouldBe(disabledDocumentsDesiredSize);

        // Act re-enable recovery
        await surface.UpdateAsync(() => tree.IsEnabled = true, "re-enable TreeView");
        await surface.UpdateAsync(() => documents.IsEnabled = true, "re-enable TreeViewItem");

        // Assert Normal state resumes, including for the ancestor-inherited item
        surface.ShouldHaveState(tree, VisualState.Normal);
        surface.ShouldHaveState(documents, VisualState.Normal);
        surface.ShouldHaveState(images, VisualState.Normal);

        // Assert interaction resumes
        await surface.Pointer.MoveToAsync(documents);
        documents.IsPointerOver.ShouldBeTrue();
    }

    /// <summary>Verifies a TreeView inherits Disabled from a disabled ancestor rather than only
    /// from its own IsEnabled flag, keeps stable geometry across a genuine resize while disabled,
    /// and resumes normal interaction once re-enabled.</summary>
    [Fact]
    public async Task IsEnabled_WhenAncestorIsDisabled_InheritsDisabledAndRecoversAsync()
    {
        // Arrange
        var tree = CreateTree(16);
        tree.Items.Add(new TreeViewItem { Header = "Node" });
        var host = new Stack
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Children = { tree }
        };
        await using var surface = await ComponentSurface.MountAsync(
            host,
            new Size(16, 4),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);

        // Act ancestor disable
        await surface.UpdateAsync(() => host.IsEnabled = false, "disable ancestor Stack");

        // Assert TreeView inherits Disabled without its own IsEnabled flag changing
        tree.IsEnabled.ShouldBeTrue();
        tree.EffectiveIsEnabled.ShouldBeFalse();
        surface.ShouldHaveState(tree, VisualState.Disabled);

        // Act a genuine resize while disabled and assert geometry stability against an
        // independently mounted, otherwise-identical enabled tree at the same new size.
        await surface.ResizeAsync(new Size(22, 6));
        var disabledBounds = tree.Bounds;
        var disabledDesiredSize = tree.DesiredSize;

        var referenceTree = CreateTree(16);
        referenceTree.Items.Add(new TreeViewItem { Header = "Node" });
        var referenceHost = new Stack
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Children = { referenceTree }
        };
        await using var referenceSurface = await ComponentSurface.MountAsync(
            referenceHost,
            new Size(22, 6),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);

        referenceTree.Bounds.ShouldBe(disabledBounds);
        referenceTree.DesiredSize.ShouldBe(disabledDesiredSize);

        // Act re-enable recovery
        await surface.UpdateAsync(() => host.IsEnabled = true, "re-enable ancestor Stack");

        // Assert Normal state and resumed interaction
        surface.ShouldHaveState(tree, VisualState.Normal);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(tree);
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
        parent.IsExpanded.ShouldBeFalse();
        surface.ShouldRender("""
                              ▶ Parent
                              ▼ Other
                                  Deep


                              """);
    }

    /// <summary>Verifies collapsing an item's Visibility removes its own row and its subtree's
    /// rows from the painted output, the remaining rows re-flow with no gap or ghosted row,
    /// keyboard navigation skips straight past the removed subtree, and restoring IsVisible fully
    /// recovers rendering.</summary>
    [Fact]
    public async Task Input_WhenAnItemsVisibilityCollapses_RemovesItsRowsAndSkipsTheSubtreeAsync()
    {
        // Arrange
        var parent = new TreeViewItem { Header = "Parent" };
        var childA = new TreeViewItem { Header = "Child A" };
        var childB = new TreeViewItem { Header = "Child B" };
        parent.Children.Add(childA);
        parent.Children.Add(childB);
        var trailer = new TreeViewItem { Header = "Trailer" };
        var tree = CreateTree(14);
        tree.Items.Add(parent);
        tree.Items.Add(trailer);
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
                                Trailer

                              """);

        // Act collapse the parent's visibility
        await surface.UpdateAsync(() => parent.Visibility = Visibility.Collapsed, "collapse parent visibility");

        // Assert the parent's own row and both children are gone; Trailer re-flows to the top with
        // no gap or ghosted row left behind.
        surface.ShouldRender("""
                                Trailer




                              """);

        // Act keyboard navigation must skip straight to Trailer, never landing on the hidden subtree.
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Down);
        tree.SelectedItem.ShouldBeSameAs(trailer);

        // Act restore visibility
        await surface.UpdateAsync(() => parent.Visibility = Visibility.Visible, "restore parent visibility");

        // Assert full recovery
        surface.ShouldRender("""
                              ▼ Parent
                                  Child A
                                  Child B
                                Trailer

                              """);
    }

    /// <summary>Verifies a Hidden item keeps its own row's blank slot in the painted output while
    /// its descendants are not painted at all - their space is not reserved, unlike the parent's
    /// own row.</summary>
    [Fact]
    public async Task Input_WhenAnItemsVisibilityHides_PaintsABlankRowAndOmitsDescendantsAsync()
    {
        // Arrange
        var parent = new TreeViewItem { Header = "Parent" };
        var child = new TreeViewItem { Header = "Child" };
        parent.Children.Add(child);
        var trailer = new TreeViewItem { Header = "Trailer" };
        var tree = CreateTree(14);
        tree.Items.Add(parent);
        tree.Items.Add(trailer);
        await using var surface = await ComponentSurface.MountAsync(
            tree,
            new Size(14, 5),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);

        surface.ShouldRender("""
                              ▼ Parent
                                  Child
                                Trailer


                              """);

        // Act
        await surface.UpdateAsync(() => parent.Visibility = Visibility.Hidden, "hide parent visibility");

        // Assert - the parent keeps its own blank slot at row 0, Child is not realized at all, and
        // Trailer re-flows into row 1.
        surface.ShouldRender("""

                                Trailer



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
        second.IsSelected.ShouldBeTrue();
        first.IsSelected.ShouldBeFalse();
        surface.ShouldHaveFocus(tree);
    }

    /// <summary>Verifies check glyph rendering and Space toggling propagate through a mounted tree.</summary>
    [Fact]
    public async Task Input_WhenCheckableNodeIsCurrent_TogglesCheckStateAsync()
    {
        var parent = new TreeViewItem { Header = "Parent", IsCheckable = true };
        var child = new TreeViewItem { Header = "Child", IsCheckable = true };
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
        var item = new TreeViewItem { Header = "Item", IsCheckable = true };
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

    /// <summary>Verifies a configured PageOverlap retains that much context on PageDown instead of
    /// jumping the full viewport height, matching Table and ListView's overlap-aware paging.</summary>
    [Fact]
    public async Task Input_WhenPageDownWithConfiguredPageOverlap_LandsOverlapAwareAsync()
    {
        // Arrange
        var tree = new TreeView
        {
            Height = Length.Cells(6),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            PageOverlap = 2
        };
        List<TreeViewItem> items = [.. Enumerable.Range(0, 12).Select(index => new TreeViewItem { Header = $"Item {index}" })];

        foreach (var item in items)
        {
            tree.Items.Add(item);
        }

        await using var surface = await ComponentSurface.MountAsync(
            tree,
            new Size(20, 8),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Down);
        tree.SelectedItem.ShouldBeSameAs(items[0]);
        var expectedIndex = tree.Viewport.Height - tree.PageOverlap;

        // Act
        await surface.Keyboard.PressAsync(Code.PageDown);

        // Assert - each item is one cell tall, so the landing index equals the overlap-reduced step.
        tree.SelectedItem.ShouldBeSameAs(items[expectedIndex]);
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
        var item = new TreeViewItem { Header = "Row", IsCheckable = true };
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
        var item = new TreeViewItem { Header = "Row", IsCheckable = true };
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
        var item = new TreeViewItem { Header = "Row", IsCheckable = true };
        var tree = new TreeView { Items = { item } };
        await using var surface = await ComponentSurface.MountAsync(
            tree,
            new Size(24, 8),
            TestContext.Current.CancellationToken);
        var before = item.ActualCheckMark.Glyphs;

        await surface.UpdateAsync(() => surface.Application.Theme = TickTheme(), "swap to tick glyphs");

        item.ActualCheckMark.Glyphs.ShouldNotBe(before);
        item.ActualCheckMark.Glyphs.Checked.ShouldBe(new Rune('☒'));
    }

    /// <summary>The counter-case that keeps the change honest: an explicit per-item override still
    /// wins over the theme, so this did not turn a local override into a suggestion.</summary>
    [Fact]
    public async Task ActualCheckMark_WhenItemOverridesTheMark_KeepsTheOverrideUnderAThemeAsync()
    {
        var item = new TreeViewItem
        {
            Header = "Row",
            IsCheckable = true,
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

    // A leaf declares no theme section of its own any more, so the only surviving way to move
    // CheckBox's resolved mark family through a theme is the theme-wide glyph family
    // (theme.Glyphs.CheckBox) every bundled theme's own "glyphs" field already selects. "blocks"
    // differs from the code-owned Brackets family in both the mark style (Square) and the glyph
    // trio (☐/☒/■) - matching the same "not vacuous on either axis" property the retired
    // markStyle/glyphs JSON override once had to be spelled out for by hand.
    private static Theme TickTheme() => ThemeCatalog.Parse(ThemeJson.Create(glyphs: "blocks"));

    /// <summary>Verifies a runtime IsVisible -&gt; Collapsed -&gt; IsVisible transition on a mounted
    /// top-level item leaves no stale rendered row behind, and that pointer hit-testing tracks the
    /// surviving item's live row at every step.</summary>
    [Fact]
    public async Task Pointer_WhenItemTogglesCollapsedThenVisible_ClearsStaleRowsAndHitTargetAsync()
    {
        // Arrange
        var a = new TreeViewItem { Header = "A" };
        var b = new TreeViewItem { Header = "B" };
        var c = new TreeViewItem { Header = "C" };
        var tree = CreateTree(10);
        tree.Items.Add(a);
        tree.Items.Add(b);
        tree.Items.Add(c);
        await using var surface = await ComponentSurface.MountAsync(
            tree,
            new Size(10, 3),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);

        surface.ShouldRender("""
              A
              B
              C
            """);

        // Act - collapse the middle item.
        await surface.UpdateAsync(() => b.Visibility = Visibility.Collapsed, "collapse middle tree item");

        // Assert - "C" now occupies row 1 (B's former row) with no stale "B" glyph.
        surface.ShouldRender("""
              A
              C

            """);
        var hitAfterCollapse = tree.HitTest(new Point(2, 1));
        hitAfterCollapse.ShouldBeSameAs(c);

        // Act - restore visibility.
        await surface.UpdateAsync(() => b.Visibility = Visibility.Visible, "restore middle tree item");

        // Assert - the original three-row layout is exactly restored.
        surface.ShouldRender("""
              A
              B
              C
            """);
        var hitAfterRestore = tree.HitTest(new Point(2, 1));
        hitAfterRestore.ShouldBeSameAs(b);
    }

    /// <summary>Verifies keyboard focus on the TreeView itself recolors its own bordered frame.
    /// TreeViewStyle previously fell back to the bare passive "container" key, which no bundled
    /// theme authors a focus delta for, so a user tabbing onto a TreeView had no cue it had
    /// happened despite the TreeView already drawing an all-sides border to recolor.</summary>
    [Fact]
    public async Task Keyboard_WhenTreeViewReceivesFocus_RecolorsItsOwnBorderAsync()
    {
        // Arrange
        var tree = new TreeView { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
        tree.Items.Add(new TreeViewItem { Header = "Documents" });
        await using var surface = await ComponentSurface.MountAsync(
            tree,
            new Size(20, 5),
            TestContext.Current.CancellationToken);
        var theme = tree.Theme.ShouldNotBeNull();
        tree.ActualBorder.Foreground.Literal.ShouldBe(ThemeColorHelper.Border(theme));

        // Act
        await surface.Keyboard.PressAsync(Code.Tab);

        // Assert
        tree.IsFocused.ShouldBeTrue();
        tree.ActualBorder.Foreground.Literal.ShouldBe(ThemeColorHelper.FocusedBorder(theme));
    }

    /// <summary>Verifies the current row renders an underline cue independent of both theme
    /// authoring and real keyboard focus, mirroring ListView's identical fix. TreeViewItem is
    /// never itself focusable - real focus stays on the owning TreeView - and no bundled theme
    /// authors a Current visual-state delta, so under TreeSelectionMode.None, where CommitCurrent's
    /// call to ApplyInputSelection never selects anything, keyboard-driven navigation would
    /// otherwise be completely invisible on screen.</summary>
    [Fact]
    public async Task Keyboard_WhenCurrentMovesWithoutSelection_UnderlinesOnlyTheCurrentRowAsync()
    {
        // Arrange
        var first = new TreeViewItem { Header = "One" };
        var second = new TreeViewItem { Header = "Two" };
        var tree = CreateTree(8);
        tree.SelectionMode = TreeSelectionMode.None;
        tree.Items.Add(first);
        tree.Items.Add(second);
        await using var surface = await ComponentSurface.MountAsync(
            tree,
            new Size(8, 2),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);

        // Act - TreeView's current-item navigator starts unset (unlike ListView, which seeds
        // Current to the first item on focus), so the first Down lands on the first item and a
        // second Down is needed to reach the second.
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Down);
        await surface.Keyboard.PressAsync(Code.Down);

        // Assert
        tree.SelectedItems.Count.ShouldBe(0, "SelectionMode.None never selects; only Current moved");
        (surface.Cell(new Point(second.Bounds.X, second.Bounds.Y)).Style.Attributes &
            TerminalAttributes.Underline).ShouldBe(TerminalAttributes.Underline);
        (surface.Cell(new Point(first.Bounds.X, first.Bounds.Y)).Style.Attributes &
            TerminalAttributes.Underline).ShouldBe(TerminalAttributes.None);
    }

    #region Asynchronous child-loading surfaces

    /// <summary>Verifies an item whose children are still loading keeps its disclosure glyph and
    /// draws an indented one-cell status glyph followed by the configured loading text.</summary>
    [Fact]
    public async Task Render_WhenChildRequestIsInFlight_DrawsDisclosureAndLoadingRowAsync()
    {
        var source = new FakeTreeViewChildSource();
        _ = source.DeferNext(null);
        var root = new TreeViewItem("Root") { ChildSource = source };
        var tree = CreateTree(20);
        tree.Items.Add(root);

        await using var surface = await ComponentSurface.MountAsync(
            tree,
            new Size(20, 3),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);

        await DialogWait.UntilAsync(surface, root, () => root.ChildState == TreeViewChildState.Loading);

        surface.ShouldRender("""
                              ▼ Root
                                • Loading…

                              """);
    }

    /// <summary>Verifies a failed request draws the configured failed text with its own status
    /// glyph, retries on a pointer click anywhere on the row, and leaves no stale cell behind once
    /// the retry commits real children.</summary>
    [Fact]
    public async Task Render_WhenChildRequestFailsAndRowIsClicked_RetriesAndClearsStaleCellsAsync()
    {
        var source = new FakeTreeViewChildSource();
        source.FailNext(null, new InvalidOperationException("boom"));
        var root = new TreeViewItem("Root") { ChildSource = source };
        var tree = CreateTree(45);
        tree.Items.Add(root);

        await using var surface = await ComponentSurface.MountAsync(
            tree,
            new Size(45, 3),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);

        await DialogWait.UntilAsync(surface, root, () => root.ChildState == TreeViewChildState.LoadFailed);

        surface.ShouldRender("""
                              ▼ Root
                                ✗ Failed to load. Press Enter to retry.

                              """);

        var statusRow = OwnedTree.Find<TreeViewStatusRow>(tree).ShouldNotBeNull();
        source.AddChildren(null, new TreeViewChildDescription("a", "A") { Presence = TreeViewChildPresence.Leaf });

        await surface.Pointer.ClickAsync(statusRow);
        await DialogWait.UntilAsync(surface, root, () => root.ChildState == TreeViewChildState.Loaded);

        root.Children.Count.ShouldBe(1);
        surface.ShouldRender("""
                              ▼ Root
                                  A

                              """);
    }

    /// <summary>Verifies Enter retries a failed request when the failed item is the current
    /// navigator item, matching the pointer-click retry affordance.</summary>
    [Fact]
    public async Task Input_WhenCurrentItemFailedAndEnterIsPressed_RetriesAsync()
    {
        var source = new FakeTreeViewChildSource();
        source.FailNext(null, new InvalidOperationException("boom"));
        var root = new TreeViewItem("Root") { ChildSource = source };
        var tree = CreateTree(20);
        tree.Items.Add(root);

        await using var surface = await ComponentSurface.MountAsync(
            tree,
            new Size(20, 3),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);

        await DialogWait.UntilAsync(surface, root, () => root.ChildState == TreeViewChildState.LoadFailed);

        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Down);
        tree.SelectedItem.ShouldBeSameAs(root);

        source.AddChildren(null, new TreeViewChildDescription("a", "A") { Presence = TreeViewChildPresence.Leaf });
        await surface.Keyboard.PressAsync(Code.Enter);

        await DialogWait.UntilAsync(surface, root, () => root.ChildState == TreeViewChildState.Loaded);
        root.Children.Count.ShouldBe(1);
    }

    /// <summary>Verifies Right on a collapsed Unloaded item expands it and starts the deferred
    /// request, matching the keyboard-driven expansion described for a directly authored item.</summary>
    [Fact]
    public async Task Input_WhenRightArrowExpandsAnUnloadedItem_StartsTheRequestAsync()
    {
        var source = new FakeTreeViewChildSource();
        source.AddChildren(null, new TreeViewChildDescription("a", "A") { Presence = TreeViewChildPresence.Leaf });
        var root = new TreeViewItem("Root") { ChildSource = source, IsExpanded = false };
        var tree = CreateTree(20);
        tree.Items.Add(root);

        await using var surface = await ComponentSurface.MountAsync(
            tree,
            new Size(20, 3),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);

        root.ChildState.ShouldBe(TreeViewChildState.Unloaded);

        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Down);
        tree.SelectedItem.ShouldBeSameAs(root);

        await surface.Keyboard.PressAsync(Code.Right);

        await DialogWait.UntilAsync(surface, root, () => root.ChildState == TreeViewChildState.Loaded);
        root.IsExpanded.ShouldBeTrue();
        root.Children.Count.ShouldBe(1);
    }

    /// <summary>Verifies Left collapses an item whose load is still in flight and its loading row
    /// disappears with no ghosted content left behind.</summary>
    [Fact]
    public async Task Input_WhenLeftArrowCollapsesDuringLoad_RemovesTheLoadingRowAsync()
    {
        var source = new FakeTreeViewChildSource();
        var deferred = source.DeferNext(null);
        var root = new TreeViewItem("Root") { ChildSource = source };
        var tree = CreateTree(20);
        tree.Items.Add(root);

        await using var surface = await ComponentSurface.MountAsync(
            tree,
            new Size(20, 3),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);

        await DialogWait.UntilAsync(surface, root, () => root.ChildState == TreeViewChildState.Loading);

        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Down);
        tree.SelectedItem.ShouldBeSameAs(root);

        await surface.Keyboard.PressAsync(Code.Left);

        root.IsExpanded.ShouldBeFalse();
        root.ChildState.ShouldBe(TreeViewChildState.Unloaded);
        surface.ShouldRender("""
                              ▶ Root


                              """);

        // The cancelled request's late completion, once delivered, must not resurrect the row.
        // Awaiting the cancelled load's own observation, then flushing one more no-op turn, proves
        // the stale commit attempt actually ran (the fake source ignores cancellation on purpose)
        // and was dropped - not merely that nothing raced ahead of an unresolved background task.
        var observation = await surface.Application.Dispatcher.InvokeAsync(
            () => root.LastChildLoadObservation!,
            TestContext.Current.CancellationToken);
        _ = deferred.TrySetResult([new TreeViewChildDescription("a", "A") { Presence = TreeViewChildPresence.Leaf }]);
        await observation.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await surface.UpdateAsync(static () => { }, "flush stale completion");

        root.ChildState.ShouldBe(TreeViewChildState.Unloaded);
        surface.ShouldRender("""
                              ▶ Root


                              """);
    }

    /// <summary>Verifies Down and Home/End skip the synthetic loading row entirely, landing only on
    /// real items - a loading row is realized but never a navigation target.</summary>
    [Fact]
    public async Task Input_WhenNavigatingPastALoadingRow_SkipsItAsync()
    {
        var source = new FakeTreeViewChildSource();
        _ = source.DeferNext(null);
        var root = new TreeViewItem("Root") { ChildSource = source };
        var trailer = new TreeViewItem("Trailer");
        var tree = CreateTree(20);
        tree.Items.Add(root);
        tree.Items.Add(trailer);

        await using var surface = await ComponentSurface.MountAsync(
            tree,
            new Size(20, 4),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);

        await DialogWait.UntilAsync(surface, root, () => root.ChildState == TreeViewChildState.Loading);

        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Down);
        tree.SelectedItem.ShouldBeSameAs(root);

        await surface.Keyboard.PressAsync(Code.Down);
        tree.SelectedItem.ShouldBeSameAs(trailer, "Down must skip the synthetic loading row");

        await surface.Keyboard.PressAsync(Code.Home);
        tree.SelectedItem.ShouldBeSameAs(root);

        await surface.Keyboard.PressAsync(Code.End);
        tree.SelectedItem.ShouldBeSameAs(trailer);
    }

    /// <summary>Verifies selecting and focusing the loading item itself survives its own commit -
    /// the atomic rebuild that materializes its children does not disturb selection or focus.</summary>
    [Fact]
    public async Task Selection_WhenChildRequestCommitsWhileItemIsSelected_KeepsSelectionAndFocusAsync()
    {
        var source = new FakeTreeViewChildSource();
        var deferred = source.DeferNext(null);
        var root = new TreeViewItem("Root") { ChildSource = source };
        var tree = CreateTree(20);
        tree.Items.Add(root);

        await using var surface = await ComponentSurface.MountAsync(
            tree,
            new Size(20, 3),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);

        await DialogWait.UntilAsync(surface, root, () => root.ChildState == TreeViewChildState.Loading);

        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Down);
        tree.SelectedItem.ShouldBeSameAs(root);
        surface.ShouldHaveFocus(tree);

        _ = deferred.TrySetResult([new TreeViewChildDescription("a", "A") { Presence = TreeViewChildPresence.Leaf }]);
        await DialogWait.UntilAsync(surface, root, () => root.ChildState == TreeViewChildState.Loaded);

        root.ChildState.ShouldBe(TreeViewChildState.Loaded);
        tree.SelectedItem.ShouldBeSameAs(root);
        root.IsSelected.ShouldBeTrue();
        surface.ShouldHaveFocus(tree);
    }

    #endregion
}
