// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Collections;

/// <summary>Verifies asynchronous on-demand child loading through mounted surfaces: the loading and
/// failed status rows, keyboard and pointer retry, keyboard-driven expansion, navigation skipping
/// synthetic rows, and selection/focus stability across a commit.</summary>
public sealed partial class TreeViewSurfaceTests
{
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
}
