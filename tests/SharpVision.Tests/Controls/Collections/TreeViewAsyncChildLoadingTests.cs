// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Collections;

/// <summary>Verifies the asynchronous on-demand child-loading lifecycle: state transitions,
/// generation-guarded cancellation, atomic commits, validation, and admission control.</summary>
public sealed partial class TreeViewTests
{
    /// <summary>Verifies the deferred attach callback from one dispatcher cannot start a request
    /// after the item migrates, while the new attachment's callback still starts exactly once.</summary>
    [Fact]
    public async Task AttachedLoad_WhenItemMigratesBeforeDeferredCallback_IgnoresPreviousDispatcherAsync()
    {
        await using var previousDispatcher = Dispatcher.Start();
        await using var currentDispatcher = Dispatcher.Start();
        var source = new FakeTreeViewChildSource();
        source.AddChildren(null);
        var item = new TreeViewItem("Root") { ChildSource = source };
        var tree = new TreeView { Items = { item } };
        var detached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var attached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using ManualResetEventSlim releasePrevious = new();
        using ManualResetEventSlim releaseCurrent = new();
        previousDispatcher.Post(() =>
        {
            tree.Attach(previousDispatcher);
            tree.Detach();
            detached.SetResult();
            releasePrevious.Wait();
        });
        await detached.Task.WaitAsync(TestContext.Current.CancellationToken);
        currentDispatcher.Post(() =>
        {
            tree.Attach(currentDispatcher);
            attached.SetResult();
            releaseCurrent.Wait();
        });
        await attached.Task.WaitAsync(TestContext.Current.CancellationToken);

        releasePrevious.Set();
        await previousDispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);
        source.Requests.ShouldBeEmpty();

        releaseCurrent.Set();
        await currentDispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);
        source.Requests.ShouldBe([null]);
        await currentDispatcher.InvokeAsync(tree.Dispose, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies detachment cancels an in-flight generation, reattachment starts a current
    /// request, and the ignored old completion cannot replace the new tree state.</summary>
    [Fact]
    public async Task ChildLoad_WhenItemMigratesBeforeCompletion_IgnoresPreviousGenerationAsync()
    {
        await using var previousDispatcher = Dispatcher.Start();
        await using var currentDispatcher = Dispatcher.Start();
        var source = new FakeTreeViewChildSource();
        var stale = source.DeferNext(null);
        source.AddChildren(null, new TreeViewChildDescription("fresh", "Fresh"));
        var item = new TreeViewItem("Root") { ChildSource = source };
        var tree = new TreeView { Items = { item } };
        await previousDispatcher.InvokeAsync(() => tree.Attach(previousDispatcher), TestContext.Current.CancellationToken);
        await previousDispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);
        source.Requests.Count.ShouldBe(1);
        await previousDispatcher.InvokeAsync(tree.Detach, TestContext.Current.CancellationToken);
        await currentDispatcher.InvokeAsync(() => tree.Attach(currentDispatcher), TestContext.Current.CancellationToken);
        await TreeViewChildLoadWait.UntilAsync(
            item,
            () => item.ChildState == TreeViewChildState.Loaded,
            TestContext.Current.CancellationToken);

        _ = stale.TrySetResult([new TreeViewChildDescription("stale", "Stale")]);
        await previousDispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);

        item.Children.ShouldHaveSingleItem().Header.ShouldBe("Fresh");
        source.Requests.Count.ShouldBe(2);
        await currentDispatcher.InvokeAsync(tree.Dispose, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies expansion invariant work survives either public expansion observer
    /// throwing after the state commits.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IsExpanded_WhenExpansionObserverThrows_StillStartsCommittedLoadAsync(
        bool throwFromPropertyObserver)
    {
        await using var dispatcher = Dispatcher.Start();
        var source = new FakeTreeViewChildSource();
        source.AddChildren(null, new TreeViewChildDescription("child", "Child")
        {
            Presence = TreeViewChildPresence.Leaf
        });
        var item = new TreeViewItem("Root") { ChildSource = source, IsExpanded = false };
        var tree = new TreeView { Items = { item } };

        await dispatcher.InvokeAsync(() =>
        {
            tree.Attach(dispatcher);

            if (throwFromPropertyObserver)
            {
                item.PropertyChanged += (_, eventArgs) =>
                {
                    if (eventArgs.PropertyName == nameof(TreeViewItem.IsExpanded))
                    {
                        throw new InvalidOperationException("The property observer failed.");
                    }
                };
            }
            else
            {
                item.ExpandedChanged += (_, _) =>
                    throw new InvalidOperationException("The expanded observer failed.");
            }

            _ = Should.Throw<InvalidOperationException>(() => item.IsExpanded = true);
        }, TestContext.Current.CancellationToken);

        await TreeViewChildLoadWait.UntilAsync(
            item,
            () => item.ChildState == TreeViewChildState.Loaded,
            TestContext.Current.CancellationToken);

        source.Requests.ShouldBe([null]);
        item.Children.Count.ShouldBe(1);
    }

    /// <summary>Verifies cancelling from the Loading callback cannot expose a token owned by the
    /// cancelled and disposed request to the superseded outer start path.</summary>
    [Fact]
    public async Task BeginLoad_WhenLoadingObserverClearsSource_DoesNotStartCancelledRequestAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var source = new FakeTreeViewChildSource();
        var item = new TreeViewItem("Root") { ChildSource = source, IsExpanded = false };
        var tree = new TreeView { Items = { item } };

        await dispatcher.InvokeAsync(() =>
        {
            tree.Attach(dispatcher);
            item.ChildStateChanged += (_, eventArgs) =>
            {
                if (eventArgs.Current == TreeViewChildState.Loading)
                {
                    item.ChildSource = null;
                }
            };

            item.IsExpanded = true;
        }, TestContext.Current.CancellationToken);

        item.ChildState.ShouldBe(TreeViewChildState.Leaf);
        item.ChildSource.ShouldBeNull();
        source.Requests.ShouldBeEmpty();
    }

    /// <summary>Verifies a failing Loading observer cannot strand a committed loading state with
    /// no request behind it.</summary>
    [Fact]
    public async Task BeginLoad_WhenLoadingObserverThrows_StillStartsRequestAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var source = new FakeTreeViewChildSource();
        var completion = source.DeferNext(null);
        var item = new TreeViewItem("Root") { ChildSource = source, IsExpanded = false };
        var tree = new TreeView { Items = { item } };

        await dispatcher.InvokeAsync(() =>
        {
            tree.Attach(dispatcher);
            item.ChildStateChanged += (_, eventArgs) =>
            {
                if (eventArgs.Current == TreeViewChildState.Loading)
                {
                    throw new InvalidOperationException("The loading observer failed.");
                }
            };

            _ = Should.Throw<InvalidOperationException>(() => item.IsExpanded = true);
        }, TestContext.Current.CancellationToken);

        item.ChildState.ShouldBe(TreeViewChildState.Loading);
        source.Requests.ShouldBe([null]);

        completion.SetResult([]);
        await TreeViewChildLoadWait.UntilAsync(
            item,
            () => item.ChildState == TreeViewChildState.Loaded,
            TestContext.Current.CancellationToken);
        item.Children.ShouldBeEmpty();
    }

    /// <summary>Verifies an item with no <see cref="TreeViewItem.ChildSource"/> and no children is
    /// a leaf, distinct from an item whose source has not yet been consulted.</summary>
    [Fact]
    public void ChildState_WhenNoChildSourceAndNoChildren_IsLeaf()
    {
        var item = new TreeViewItem("Leaf");

        item.ChildState.ShouldBe(TreeViewChildState.Leaf);
        item.HasChildren.ShouldBeFalse();
    }

    /// <summary>Verifies an item whose children were authored directly (never through
    /// <see cref="TreeViewItem.ChildSource"/>) reports Loaded, and HasChildren tracks whether that
    /// committed snapshot is non-empty rather than reporting true unconditionally.</summary>
    [Fact]
    public void ChildState_WhenChildrenAreAuthoredDirectly_IsLoadedAndTracksEmptiness()
    {
        var item = new TreeViewItem("Node");
        item.Children.Add(new TreeViewItem("Child"));

        item.ChildState.ShouldBe(TreeViewChildState.Loaded);
        item.HasChildren.ShouldBeTrue();

        item.Children.Clear();

        item.ChildState.ShouldBe(TreeViewChildState.Leaf);
        item.HasChildren.ShouldBeFalse();
    }

    /// <summary>Verifies assigning a source moves an item to Unloaded, offering a disclosure
    /// affordance before any request has ever been made.</summary>
    [Fact]
    public void ChildSource_WhenAssigned_MovesToUnloadedAndOffersDisclosure()
    {
        var source = new FakeTreeViewChildSource();
        var item = new TreeViewItem("Node") { ChildSource = source };

        item.ChildState.ShouldBe(TreeViewChildState.Unloaded);
        item.HasChildren.ShouldBeTrue();
        source.Requests.ShouldBeEmpty();
    }

    /// <summary>Verifies attaching an item that started life IsExpanded (the default) with a
    /// ChildSource already assigned triggers the deferred load the moment a dispatcher becomes
    /// available, instead of leaving it Unloaded-but-IsExpanded forever.</summary>
    [Fact]
    public async Task OnAttached_WhenItemIsExpandedAndUnloaded_TriggersTheDeferredLoadAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var source = new FakeTreeViewChildSource();
        source.AddChildren(null, new TreeViewChildDescription("a", "A") { Presence = TreeViewChildPresence.Leaf });
        TreeView tree = null!;
        TreeViewItem item = null!;

        await dispatcher.InvokeAsync(() =>
        {
            // ChildSource is assigned - and IsExpanded is already true, the constructor default -
            // entirely before this item ever reaches a dispatcher.
            item = new TreeViewItem("Root") { ChildSource = source };
            tree = new TreeView { Items = { item } };
            tree.Attach(dispatcher);
        }, TestContext.Current.CancellationToken);

        await TreeViewChildLoadWait.UntilAsync(
            item,
            () => item.ChildState == TreeViewChildState.Loaded,
            TestContext.Current.CancellationToken);

        item.Children.Count.ShouldBe(1);
        source.Requests.ShouldBe([null]);
    }

    /// <summary>Verifies re-expanding an item that is already Loading - IsExpanded already true, set
    /// to true again - is a no-op that never starts a second concurrent request.</summary>
    [Fact]
    public async Task Expanded_WhenAlreadyLoadingAndSetAgain_DoesNotStartASecondRequestAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var source = new FakeTreeViewChildSource();
        _ = source.DeferNext(null);
        TreeView tree = null!;
        TreeViewItem item = null!;

        await dispatcher.InvokeAsync(() =>
        {
            item = new TreeViewItem("Root") { ChildSource = source, IsExpanded = false };
            tree = new TreeView { Items = { item } };
            tree.Attach(dispatcher);
            item.IsExpanded = true;
        }, TestContext.Current.CancellationToken);

        item.ChildState.ShouldBe(TreeViewChildState.Loading);
        source.Requests.Count.ShouldBe(1);

        await dispatcher.InvokeAsync(() => { item.IsExpanded = true; }, TestContext.Current.CancellationToken);

        source.Requests.Count.ShouldBe(1);
        item.ChildState.ShouldBe(TreeViewChildState.Loading);
    }

    /// <summary>Verifies a load committing many children applies them as one atomic update: the
    /// Loaded transition and the full child set become observable together, never as a partial
    /// intermediate set, and the transition fires exactly once.</summary>
    [Fact]
    public async Task CommitChildLoad_WhenManyChildrenAreReturned_AppliesAsOneAtomicUpdateAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var source = new FakeTreeViewChildSource();
        var descriptions = Enumerable.Range(0, 25)
            .Select(static index => new TreeViewChildDescription($"child-{index}", $"Child {index}")
            {
                Presence = TreeViewChildPresence.Leaf
            })
            .ToArray();
        source.AddChildren(null, descriptions);
        TreeView tree = null!;
        TreeViewItem item = null!;
        var loadedTransitions = 0;
        var observedCountAtLoaded = -1;

        await dispatcher.InvokeAsync(() =>
        {
            item = new TreeViewItem("Root") { ChildSource = source, IsExpanded = false };
            tree = new TreeView { Items = { item } };
            tree.Attach(dispatcher);
            item.ChildStateChanged += (_, eventArgs) =>
            {
                if (eventArgs.Current == TreeViewChildState.Loaded)
                {
                    loadedTransitions++;
                    observedCountAtLoaded = item.Children.Count;
                }
            };
            item.IsExpanded = true;
        }, TestContext.Current.CancellationToken);

        await TreeViewChildLoadWait.UntilAsync(
            item,
            () => item.ChildState == TreeViewChildState.Loaded,
            TestContext.Current.CancellationToken);

        loadedTransitions.ShouldBe(1);
        observedCountAtLoaded.ShouldBe(25, "the full committed set must already be visible at the moment the transition fires");
        item.Children.Count.ShouldBe(25);
    }

    /// <summary>Verifies a description that never sets Presence defaults to MayHaveChildren: the
    /// materialized child inherits the parent's ChildSource instead of becoming a Leaf, so it stays
    /// independently expandable.</summary>
    [Fact]
    public async Task Presence_WhenUnspecified_DefaultsToMayHaveChildrenAndInheritsTheSourceAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var source = new FakeTreeViewChildSource();
        source.AddChildren(null, new TreeViewChildDescription("child", "Child"));
        TreeView tree = null!;
        TreeViewItem root = null!;

        await dispatcher.InvokeAsync(() =>
        {
            root = new TreeViewItem("Root") { ChildSource = source };
            tree = new TreeView { Items = { root } };
            tree.Attach(dispatcher);
        }, TestContext.Current.CancellationToken);

        await TreeViewChildLoadWait.UntilAsync(
            root,
            () => root.ChildState == TreeViewChildState.Loaded,
            TestContext.Current.CancellationToken);

        var child = root.Children.ShouldHaveSingleItem();
        child.ChildSource.ShouldBeSameAs(source);
        child.ChildState.ShouldBe(TreeViewChildState.Unloaded);
        child.HasChildren.ShouldBeTrue();
    }

    /// <summary>Verifies an empty successful result commits Loaded, not Leaf - only never having had
    /// a source, or an explicit <see cref="TreeViewChildPresence.Leaf"/> answer, means leaf.</summary>
    [Fact]
    public async Task CommitChildLoad_WhenResultIsEmpty_BecomesLoadedNotLeafAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var source = new FakeTreeViewChildSource();
        source.AddChildren(null);
        TreeView tree = null!;
        TreeViewItem item = null!;

        await dispatcher.InvokeAsync(() =>
        {
            item = new TreeViewItem("Root") { ChildSource = source };
            tree = new TreeView { Items = { item } };
            tree.Attach(dispatcher);
        }, TestContext.Current.CancellationToken);

        await TreeViewChildLoadWait.UntilAsync(
            item,
            () => item.ChildState != TreeViewChildState.Loading,
            TestContext.Current.CancellationToken);

        item.ChildState.ShouldBe(TreeViewChildState.Loaded);
        item.Children.ShouldBeEmpty();
        item.HasChildren.ShouldBeFalse();
    }

    /// <summary>Verifies collapsing an item whose very first load is still in flight cancels the
    /// request and restores the state it had before the load started - Unloaded - and that a late
    /// completion of the cancelled request is dropped rather than committed.</summary>
    [Fact]
    public async Task Expanded_WhenSetFalseDuringFirstLoad_CancelsRestoresUnloadedAndDropsStaleCompletionAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var source = new FakeTreeViewChildSource();
        var deferred = source.DeferNext(null);
        TreeView tree = null!;
        TreeViewItem item = null!;
        Task observation = null!;

        await dispatcher.InvokeAsync(() =>
        {
            item = new TreeViewItem("Root") { ChildSource = source, IsExpanded = false };
            tree = new TreeView { Items = { item } };
            tree.Attach(dispatcher);
            item.IsExpanded = true;
            observation = item.LastChildLoadObservation!;
        }, TestContext.Current.CancellationToken);

        item.ChildState.ShouldBe(TreeViewChildState.Loading);

        await dispatcher.InvokeAsync(() => { item.IsExpanded = false; }, TestContext.Current.CancellationToken);

        item.ChildState.ShouldBe(TreeViewChildState.Unloaded);
        item.Children.ShouldBeEmpty();

        // Deliver the stale completion, then flush the dispatcher queue once more: the fake source
        // ignores cancellation on purpose, so the request's own continuation still posts a commit
        // attempt - this proves it lands and is dropped by the generation guard, not merely that we
        // never gave it a chance to run.
        _ = deferred.TrySetResult([]);
        await observation.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);

        item.ChildState.ShouldBe(TreeViewChildState.Unloaded);
        item.Children.ShouldBeEmpty();
    }

    /// <summary>Verifies collapsing an item mid-reload - one that already had committed children -
    /// cancels the reload and restores the prior Loaded state and its children untouched, and that
    /// the cancelled reload's late completion is dropped.</summary>
    [Fact]
    public async Task Expanded_WhenSetFalseDuringReloadInFlight_CancelsRestoresPriorLoadedStateAndDropsStaleCompletionAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var source = new FakeTreeViewChildSource();
        source.AddChildren(null, new TreeViewChildDescription("a", "A") { Presence = TreeViewChildPresence.Leaf });
        TreeView tree = null!;
        TreeViewItem item = null!;

        await dispatcher.InvokeAsync(() =>
        {
            item = new TreeViewItem("Root") { ChildSource = source };
            tree = new TreeView { Items = { item } };
            tree.Attach(dispatcher);
        }, TestContext.Current.CancellationToken);

        await TreeViewChildLoadWait.UntilAsync(
            item,
            () => item.ChildState == TreeViewChildState.Loaded,
            TestContext.Current.CancellationToken);
        item.Children.Count.ShouldBe(1);

        var deferred = source.DeferNext(null);
        Task observation = null!;

        await dispatcher.InvokeAsync(() =>
        {
            _ = item.ReloadChildrenAsync();
            observation = item.LastChildLoadObservation!;
        }, TestContext.Current.CancellationToken);

        item.ChildState.ShouldBe(TreeViewChildState.Loading);

        await dispatcher.InvokeAsync(() => { item.IsExpanded = false; }, TestContext.Current.CancellationToken);

        item.ChildState.ShouldBe(TreeViewChildState.Loaded);
        item.Children.Count.ShouldBe(1);
        item.Children[0].Header.ShouldBe("A");

        _ = deferred.TrySetResult([new TreeViewChildDescription("b", "B") { Presence = TreeViewChildPresence.Leaf }]);
        await observation.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);

        item.ChildState.ShouldBe(TreeViewChildState.Loaded);
        item.Children.Count.ShouldBe(1);
        item.Children[0].Header.ShouldBe("A");
    }

    /// <summary>Verifies starting a second reload while the first is still in flight supersedes it:
    /// the first request's late completion is dropped by the generation guard once the second has
    /// committed.</summary>
    [Fact]
    public async Task ReloadChildrenAsync_WhenAnOlderRequestCompletesAfterANewerOne_DropsTheStaleCompletionAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var source = new FakeTreeViewChildSource();
        source.AddChildren(null, new TreeViewChildDescription("a", "A") { Presence = TreeViewChildPresence.Leaf });
        TreeView tree = null!;
        TreeViewItem item = null!;

        await dispatcher.InvokeAsync(() =>
        {
            item = new TreeViewItem("Root") { ChildSource = source };
            tree = new TreeView { Items = { item } };
            tree.Attach(dispatcher);
        }, TestContext.Current.CancellationToken);

        await TreeViewChildLoadWait.UntilAsync(
            item,
            () => item.ChildState == TreeViewChildState.Loaded,
            TestContext.Current.CancellationToken);

        var stale = source.DeferNext(null);
        Task staleObservation = null!;

        await dispatcher.InvokeAsync(() =>
        {
            _ = item.ReloadChildrenAsync();
            staleObservation = item.LastChildLoadObservation!;
        }, TestContext.Current.CancellationToken);

        source.AddChildren(null, new TreeViewChildDescription("fresh", "Fresh") { Presence = TreeViewChildPresence.Leaf });

        await dispatcher.InvokeAsync(() => { _ = item.ReloadChildrenAsync(); }, TestContext.Current.CancellationToken);

        await TreeViewChildLoadWait.UntilAsync(
            item,
            () => item.Children.Any(child => child.Header == "Fresh"),
            TestContext.Current.CancellationToken);

        _ = stale.TrySetResult([new TreeViewChildDescription("stale", "Stale") { Presence = TreeViewChildPresence.Leaf }]);
        await staleObservation.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);

        _ = item.Children.ShouldHaveSingleItem();
        item.Children[0].Header.ShouldBe("Fresh");
    }

    /// <summary>Verifies reassigning a different non-null source over loader-owned children cancels
    /// any pending load, evicts and disposes the old children, and returns to Unloaded.</summary>
    [Fact]
    public async Task ChildSource_WhenReassignedToADifferentSource_EvictsDisposesAndReturnsToUnloadedAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var firstSource = new FakeTreeViewChildSource();
        firstSource.AddChildren(null, new TreeViewChildDescription("a", "A") { Presence = TreeViewChildPresence.Leaf });
        var secondSource = new FakeTreeViewChildSource();
        TreeView tree = null!;
        TreeViewItem item = null!;

        await dispatcher.InvokeAsync(() =>
        {
            item = new TreeViewItem("Root") { ChildSource = firstSource };
            tree = new TreeView { Items = { item } };
            tree.Attach(dispatcher);
        }, TestContext.Current.CancellationToken);

        await TreeViewChildLoadWait.UntilAsync(
            item,
            () => item.ChildState == TreeViewChildState.Loaded,
            TestContext.Current.CancellationToken);
        var previousChild = item.Children.ShouldHaveSingleItem();

        await dispatcher.InvokeAsync(() => { item.ChildSource = secondSource; }, TestContext.Current.CancellationToken);

        item.ChildState.ShouldBe(TreeViewChildState.Unloaded);
        item.Children.ShouldBeEmpty();
        previousChild.IsDisposed.ShouldBeTrue();
    }

    /// <summary>Verifies clearing a loader-owned source to null evicts and disposes the loaded
    /// children and lands on Leaf - not Loaded - because it no longer has a source to answer for it.</summary>
    [Fact]
    public async Task ChildSource_WhenClearedToNull_EvictsDisposesAndBecomesLeafAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var source = new FakeTreeViewChildSource();
        source.AddChildren(null, new TreeViewChildDescription("a", "A") { Presence = TreeViewChildPresence.Leaf });
        TreeView tree = null!;
        TreeViewItem item = null!;

        await dispatcher.InvokeAsync(() =>
        {
            item = new TreeViewItem("Root") { ChildSource = source };
            tree = new TreeView { Items = { item } };
            tree.Attach(dispatcher);
        }, TestContext.Current.CancellationToken);

        await TreeViewChildLoadWait.UntilAsync(
            item,
            () => item.ChildState == TreeViewChildState.Loaded,
            TestContext.Current.CancellationToken);
        var previousChild = item.Children.ShouldHaveSingleItem();

        await dispatcher.InvokeAsync(() => { item.ChildSource = null; }, TestContext.Current.CancellationToken);

        item.ChildState.ShouldBe(TreeViewChildState.Leaf);
        item.Children.ShouldBeEmpty();
        previousChild.IsDisposed.ShouldBeTrue();
    }

    /// <summary>Verifies a failed reload retains the children an earlier successful load already
    /// committed, and publishes the failure through <see cref="TreeViewItem.LastChildLoadError"/>.</summary>
    [Fact]
    public async Task CommitChildLoadFailure_WhenPriorChildrenExist_RetainsThemAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var source = new FakeTreeViewChildSource();
        source.AddChildren(
            null,
            new TreeViewChildDescription("a", "A") { Presence = TreeViewChildPresence.Leaf },
            new TreeViewChildDescription("b", "B") { Presence = TreeViewChildPresence.Leaf });
        TreeView tree = null!;
        TreeViewItem item = null!;

        await dispatcher.InvokeAsync(() =>
        {
            item = new TreeViewItem("Root") { ChildSource = source };
            tree = new TreeView { Items = { item } };
            tree.Attach(dispatcher);
        }, TestContext.Current.CancellationToken);

        await TreeViewChildLoadWait.UntilAsync(
            item,
            () => item.ChildState == TreeViewChildState.Loaded,
            TestContext.Current.CancellationToken);
        item.Children.Count.ShouldBe(2);

        var failure = new InvalidOperationException("simulated enumeration failure");
        source.FailNext(null, failure);

        await dispatcher.InvokeAsync(() => { _ = item.ReloadChildrenAsync(); }, TestContext.Current.CancellationToken);
        await TreeViewChildLoadWait.UntilAsync(
            item,
            () => item.ChildState == TreeViewChildState.LoadFailed,
            TestContext.Current.CancellationToken);

        item.Children.Count.ShouldBe(2);
        item.LastChildLoadError.ShouldBeSameAs(failure);
    }

    /// <summary>Verifies a null result list is rejected as a failure without mutating the previously
    /// committed - or absent - children.</summary>
    [Fact]
    public async Task CommitChildLoad_WhenResultIsNull_RejectsWithoutMutatingStateAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var source = new FakeTreeViewChildSource();
        var deferred = source.DeferNext(null);
        TreeView tree = null!;
        TreeViewItem item = null!;

        await dispatcher.InvokeAsync(() =>
        {
            item = new TreeViewItem("Root") { ChildSource = source };
            tree = new TreeView { Items = { item } };
            tree.Attach(dispatcher);
        }, TestContext.Current.CancellationToken);

        _ = deferred.TrySetResult(null!);

        await TreeViewChildLoadWait.UntilAsync(
            item,
            () => item.ChildState == TreeViewChildState.LoadFailed,
            TestContext.Current.CancellationToken);

        item.Children.ShouldBeEmpty();
        _ = item.LastChildLoadError.ShouldNotBeNull();
    }

    /// <summary>Verifies a null element inside an otherwise valid result list is rejected without
    /// mutating state.</summary>
    [Fact]
    public async Task CommitChildLoad_WhenResultContainsANullElement_RejectsWithoutMutatingStateAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var source = new FakeTreeViewChildSource();
        var deferred = source.DeferNext(null);
        TreeView tree = null!;
        TreeViewItem item = null!;

        await dispatcher.InvokeAsync(() =>
        {
            item = new TreeViewItem("Root") { ChildSource = source };
            tree = new TreeView { Items = { item } };
            tree.Attach(dispatcher);
        }, TestContext.Current.CancellationToken);

        _ = deferred.TrySetResult([null!]);

        await TreeViewChildLoadWait.UntilAsync(
            item,
            () => item.ChildState == TreeViewChildState.LoadFailed,
            TestContext.Current.CancellationToken);

        item.Children.ShouldBeEmpty();
        _ = item.LastChildLoadError.ShouldNotBeNull();
    }

    /// <summary>Verifies duplicate keys within one result are rejected without mutating state.</summary>
    [Fact]
    public async Task CommitChildLoad_WhenResultHasDuplicateKeys_RejectsWithoutMutatingStateAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var source = new FakeTreeViewChildSource();
        var deferred = source.DeferNext(null);
        TreeView tree = null!;
        TreeViewItem item = null!;

        await dispatcher.InvokeAsync(() =>
        {
            item = new TreeViewItem("Root") { ChildSource = source };
            tree = new TreeView { Items = { item } };
            tree.Attach(dispatcher);
        }, TestContext.Current.CancellationToken);

        _ = deferred.TrySetResult([
            new TreeViewChildDescription("dup", "First") { Presence = TreeViewChildPresence.Leaf },
            new TreeViewChildDescription("dup", "Second") { Presence = TreeViewChildPresence.Leaf }
        ]);

        await TreeViewChildLoadWait.UntilAsync(
            item,
            () => item.ChildState == TreeViewChildState.LoadFailed,
            TestContext.Current.CancellationToken);

        item.Children.ShouldBeEmpty();
        _ = item.LastChildLoadError.ShouldNotBeNull();
    }

    /// <summary>Verifies a key that collides with an ancestor's stable key - which would materialize
    /// a cycle - is rejected without mutating state.</summary>
    [Fact]
    public async Task CommitChildLoad_WhenResultKeyCollidesWithAnAncestor_RejectsAsACycleAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var source = new FakeTreeViewChildSource();
        source.AddChildren(null, new TreeViewChildDescription("root-key", "Whoops"));
        TreeView tree = null!;
        TreeViewItem root = null!;
        TreeViewItem child = null!;

        await dispatcher.InvokeAsync(() =>
        {
            root = new TreeViewItem("Root") { RemoteKey = "root-key" };
            child = new TreeViewItem("Child") { ChildSource = source };
            root.Children.Add(child);
            tree = new TreeView { Items = { root } };
            tree.Attach(dispatcher);
        }, TestContext.Current.CancellationToken);

        await TreeViewChildLoadWait.UntilAsync(
            child,
            () => child.ChildState == TreeViewChildState.LoadFailed,
            TestContext.Current.CancellationToken);

        child.Children.ShouldBeEmpty();
        _ = child.LastChildLoadError.ShouldNotBeNull();
    }

    /// <summary>Verifies a header containing a terminal control character is rejected without
    /// mutating state, matching the same validation a caller-authored <see cref="TreeViewItem.Header"/>
    /// enforces synchronously.</summary>
    [Fact]
    public async Task CommitChildLoad_WhenAHeaderContainsAControlCharacter_RejectsWithoutMutatingStateAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var source = new FakeTreeViewChildSource();
        var deferred = source.DeferNext(null);
        TreeView tree = null!;
        TreeViewItem item = null!;

        await dispatcher.InvokeAsync(() =>
        {
            item = new TreeViewItem("Root") { ChildSource = source };
            tree = new TreeView { Items = { item } };
            tree.Attach(dispatcher);
        }, TestContext.Current.CancellationToken);

        _ = deferred.TrySetResult([
            new TreeViewChildDescription("bad", "ContainsBell") { Presence = TreeViewChildPresence.Leaf }
        ]);

        await TreeViewChildLoadWait.UntilAsync(
            item,
            () => item.ChildState == TreeViewChildState.LoadFailed,
            TestContext.Current.CancellationToken);

        item.Children.ShouldBeEmpty();
        _ = item.LastChildLoadError.ShouldNotBeNull();
    }

    /// <summary>Verifies a stable key reused across a reload keeps the same materialized instance,
    /// preserving its IsExpanded, checked, and selected state instead of rebuilding it from scratch.</summary>
    [Fact]
    public async Task ReloadChildrenAsync_WhenKeysAreStable_PreservesExpandedCheckedAndSelectedAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var source = new FakeTreeViewChildSource();
        source.AddChildren(
            null,
            new TreeViewChildDescription("k1", "One") { IsCheckable = true, Presence = TreeViewChildPresence.Leaf },
            new TreeViewChildDescription("k2", "Two") { IsCheckable = true, Presence = TreeViewChildPresence.Leaf });
        TreeView tree = null!;
        TreeViewItem root = null!;

        await dispatcher.InvokeAsync(() =>
        {
            root = new TreeViewItem("Root") { ChildSource = source };
            tree = new TreeView { Items = { root } };
            tree.Attach(dispatcher);
        }, TestContext.Current.CancellationToken);

        await TreeViewChildLoadWait.UntilAsync(
            root,
            () => root.ChildState == TreeViewChildState.Loaded,
            TestContext.Current.CancellationToken);

        var one = root.Children.Single(child => Equals(child.RemoteKey, "k1"));
        await dispatcher.InvokeAsync(() =>
        {
            one.IsExpanded = false;
            one.IsChecked = true;
            tree.SelectItem(one);
        }, TestContext.Current.CancellationToken);

        source.AddChildren(
            null,
            new TreeViewChildDescription("k1", "One Renamed") { IsCheckable = true, Presence = TreeViewChildPresence.Leaf },
            new TreeViewChildDescription("k2", "Two") { IsCheckable = true, Presence = TreeViewChildPresence.Leaf });

        await dispatcher.InvokeAsync(() => { _ = root.ReloadChildrenAsync(); }, TestContext.Current.CancellationToken);
        await TreeViewChildLoadWait.UntilAsync(
            root,
            () => root.Children.Any(child => child.Header == "One Renamed"),
            TestContext.Current.CancellationToken);

        var reloadedOne = root.Children.Single(child => Equals(child.RemoteKey, "k1"));
        reloadedOne.ShouldBeSameAs(one, "the stable key must reuse the same materialized instance");
        reloadedOne.Header.ShouldBe("One Renamed");
        reloadedOne.IsExpanded.ShouldBeFalse();
        reloadedOne.IsChecked.ShouldBe(true);
        tree.SelectedItem.ShouldBeSameAs(one);
    }

    /// <summary>Verifies a checkable parent's own check state, set before its children ever load,
    /// applies as the initial state to later-loaded checkable descendants that do not specify their
    /// own initial state.</summary>
    [Fact]
    public async Task IsChecked_WhenSetBeforeChildrenLoad_AppliesToLaterLoadedCheckableDescendantsAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var source = new FakeTreeViewChildSource();
        TreeView tree = null!;
        TreeViewItem root = null!;

        await dispatcher.InvokeAsync(() =>
        {
            root = new TreeViewItem("Root") { ChildSource = source, IsCheckable = true, IsExpanded = false };
            tree = new TreeView { Items = { root } };
            tree.Attach(dispatcher);
            root.IsChecked = true;
        }, TestContext.Current.CancellationToken);

        source.AddChildren(null, new TreeViewChildDescription("k1", "One") { IsCheckable = true, Presence = TreeViewChildPresence.Leaf });

        await dispatcher.InvokeAsync(() => { root.IsExpanded = true; }, TestContext.Current.CancellationToken);
        await TreeViewChildLoadWait.UntilAsync(
            root,
            () => root.ChildState == TreeViewChildState.Loaded,
            TestContext.Current.CancellationToken);

        var child = root.Children.ShouldHaveSingleItem();
        child.IsChecked.ShouldBe(true);
    }

    /// <summary>Verifies a description's explicit InitialCheckState overrides the checkable
    /// parent's own check state, rather than always inheriting it.</summary>
    [Fact]
    public async Task InitialCheckState_WhenSpecified_OverridesTheCheckableParentsOwnStateAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var source = new FakeTreeViewChildSource();
        TreeView tree = null!;
        TreeViewItem root = null!;

        await dispatcher.InvokeAsync(() =>
        {
            root = new TreeViewItem("Root") { ChildSource = source, IsCheckable = true, IsExpanded = false };
            tree = new TreeView { Items = { root } };
            tree.Attach(dispatcher);
            root.IsChecked = true;
        }, TestContext.Current.CancellationToken);

        source.AddChildren(
            null,
            new TreeViewChildDescription("k1", "One")
            {
                IsCheckable = true,
                InitialCheckState = false,
                Presence = TreeViewChildPresence.Leaf
            });

        await dispatcher.InvokeAsync(() => { root.IsExpanded = true; }, TestContext.Current.CancellationToken);
        await TreeViewChildLoadWait.UntilAsync(
            root,
            () => root.ChildState == TreeViewChildState.Loaded,
            TestContext.Current.CancellationToken);

        var child = root.Children.ShouldHaveSingleItem();
        child.IsChecked.ShouldBe(false, "the description's own InitialCheckState must win over the inherited parent state");
    }

    /// <summary>Verifies disposing the owning tree while a descendant's load is still in flight
    /// cancels the subtree's pending loads through the item's own disposal hook, and a late
    /// completion afterward does not fault the fire-and-forget loop.</summary>
    [Fact]
    public async Task Dispose_WhenLoadIsInFlight_CancelsTheSubtreeLoadWithoutFaultingAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var source = new FakeTreeViewChildSource();
        var deferred = source.DeferNext(null);
        TreeView tree = null!;
        TreeViewItem item = null!;
        Task observation = null!;

        await dispatcher.InvokeAsync(() =>
        {
            item = new TreeViewItem("Root") { ChildSource = source, IsExpanded = false };
            tree = new TreeView { Items = { item } };
            tree.Attach(dispatcher);
            item.IsExpanded = true;
            observation = item.LastChildLoadObservation!;
        }, TestContext.Current.CancellationToken);

        item.ChildState.ShouldBe(TreeViewChildState.Loading);

        await dispatcher.InvokeAsync(tree.Dispose, TestContext.Current.CancellationToken);

        _ = deferred.TrySetResult([new TreeViewChildDescription("a", "A") { Presence = TreeViewChildPresence.Leaf }]);

        await observation.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        observation.IsFaulted.ShouldBeFalse();
        item.IsDisposed.ShouldBeTrue();
    }

    /// <summary>Verifies directly disposing a loading item removes its semantic entry and drops a late completion.</summary>
    [Fact]
    public async Task ItemDispose_WhenLoadIsInFlight_RemovesEntryAndDropsLateCompletionAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var source = new FakeTreeViewChildSource();
        var deferred = source.DeferNext(null);
        TreeView tree = null!;
        TreeViewItem item = null!;
        Task observation = null!;

        await dispatcher.InvokeAsync(() =>
        {
            item = new TreeViewItem("Root") { ChildSource = source, IsExpanded = false };
            tree = new TreeView { Items = { item } };
            tree.Attach(dispatcher);
            item.IsExpanded = true;
            observation = item.LastChildLoadObservation!;
        }, TestContext.Current.CancellationToken);

        item.ChildState.ShouldBe(TreeViewChildState.Loading);

        await dispatcher.InvokeAsync(item.Dispose, TestContext.Current.CancellationToken);

        tree.Items.ShouldBeEmpty();
        item.ParentCollection.ShouldBeNull();
        _ = deferred.TrySetResult([new TreeViewChildDescription("late", "Late") { Presence = TreeViewChildPresence.Leaf }]);
        await observation.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        observation.IsFaulted.ShouldBeFalse();
        item.Children.ShouldBeEmpty();
    }

    /// <summary>Verifies <see cref="TreeViewItem.ReloadChildrenAsync"/> rejects a null ChildSource.</summary>
    [Fact]
    public async Task ReloadChildrenAsync_WhenChildSourceIsNull_ThrowsInvalidOperationExceptionAsync()
    {
        var item = new TreeViewItem("Leaf");

        _ = await Should.ThrowAsync<InvalidOperationException>(() => item.ReloadChildrenAsync());
    }

    /// <summary>Verifies <see cref="TreeViewItem.ReloadChildrenAsync"/> requires an item attached to
    /// a running dispatcher.</summary>
    [Fact]
    public async Task ReloadChildrenAsync_WhenItemIsUnattached_ThrowsInvalidOperationExceptionAsync()
    {
        var source = new FakeTreeViewChildSource();
        var item = new TreeViewItem("Node") { ChildSource = source };

        _ = await Should.ThrowAsync<InvalidOperationException>(() => item.ReloadChildrenAsync());
    }

    /// <summary>Verifies an unloaded branch is skipped by <see cref="TreeView.ExpandAll"/> rather
    /// than being forced to start a remote load it never promised to trigger.</summary>
    [Fact]
    public async Task ExpandAll_WhenBranchIsUnloaded_SkipsItWithoutStartingALoadAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var source = new FakeTreeViewChildSource();
        TreeView tree = null!;
        TreeViewItem item = null!;

        await dispatcher.InvokeAsync(() =>
        {
            item = new TreeViewItem("Node") { ChildSource = source, IsExpanded = false };
            tree = new TreeView { Items = { item } };
            tree.Attach(dispatcher);
            tree.ExpandAll();
        }, TestContext.Current.CancellationToken);

        item.IsExpanded.ShouldBeFalse();
        item.ChildState.ShouldBe(TreeViewChildState.Unloaded);
        source.Requests.ShouldBeEmpty();
    }

    /// <summary>Verifies a fresh tree defaults to four concurrent child-load admissions.</summary>
    [Fact]
    public void MaxConcurrentChildLoads_WhenCreated_DefaultsToFour()
    {
        var tree = new TreeView();

        tree.MaxConcurrentChildLoads.ShouldBe(4);
    }

    /// <summary>Verifies a non-positive concurrency limit is rejected before mutation.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void MaxConcurrentChildLoads_WhenSetToNonPositiveValue_ThrowsArgumentOutOfRangeException(int value)
    {
        var tree = new TreeView();

        _ = Should.Throw<ArgumentOutOfRangeException>(() => tree.MaxConcurrentChildLoads = value);

        tree.MaxConcurrentChildLoads.ShouldBe(4);
    }

    /// <summary>Verifies a changed concurrency limit publishes once, while an equivalent
    /// assignment remains notification-free.</summary>
    [Fact]
    public void MaxConcurrentChildLoads_WhenChanged_RaisesPropertyChangedOnce()
    {
        // Arrange
        var tree = new TreeView();
        List<string?> changed = [];
        tree.PropertyChanged += (_, eventArgs) => changed.Add(eventArgs.PropertyName);

        // Act
        tree.MaxConcurrentChildLoads = 2;
        tree.MaxConcurrentChildLoads = 2;

        // Assert
        changed.ShouldBe([nameof(TreeView.MaxConcurrentChildLoads)]);
    }

    /// <summary>Verifies even an equivalent concurrency-limit assignment enforces dispatcher
    /// affinity before returning as a no-op.</summary>
    [Fact]
    public async Task MaxConcurrentChildLoads_WhenAttachedAndAssignedCurrentValueOffDispatcher_ThrowsAsync()
    {
        // Arrange
        await using var dispatcher = Dispatcher.Start();
        var tree = new TreeView();
        await dispatcher.InvokeAsync(() => tree.Attach(dispatcher), TestContext.Current.CancellationToken);

        // Act and assert
        _ = Should.Throw<InvalidOperationException>(() => tree.MaxConcurrentChildLoads = 4);
        tree.MaxConcurrentChildLoads.ShouldBe(4);
    }

    /// <summary>Verifies a disposed tree rejects a concurrency-limit mutation before changing the
    /// retained admission policy.</summary>
    [Fact]
    public void MaxConcurrentChildLoads_WhenOwnerIsDisposed_ThrowsBeforeMutation()
    {
        // Arrange
        var tree = new TreeView();
        tree.Dispose();

        // Act and assert
        _ = Should.Throw<ObjectDisposedException>(() => tree.MaxConcurrentChildLoads = 2);
        tree.MaxConcurrentChildLoads.ShouldBe(4);
    }

    /// <summary>Verifies increasing the live concurrency limit immediately grants available slots
    /// to already queued requests instead of leaving capacity idle until another load finishes.</summary>
    [Fact]
    public async Task MaxConcurrentChildLoads_WhenIncreased_AdmitsQueuedRequestImmediatelyAsync()
    {
        // Arrange
        await using var dispatcher = Dispatcher.Start();
        var source = new FakeTreeViewChildSource();
        var firstDeferred = source.DeferNext(null);
        var secondDeferred = source.DeferNext(null);
        TreeView tree = null!;
        TreeViewItem first = null!;
        TreeViewItem second = null!;
        await dispatcher.InvokeAsync(() =>
        {
            tree = new TreeView { MaxConcurrentChildLoads = 1 };
            first = new TreeViewItem("First") { ChildSource = source, IsExpanded = false };
            second = new TreeViewItem("Second") { ChildSource = source, IsExpanded = false };
            tree.Items.Add(first);
            tree.Items.Add(second);
            tree.Attach(dispatcher);
            first.IsExpanded = true;
            second.IsExpanded = true;
        }, TestContext.Current.CancellationToken);
        source.Requests.Count.ShouldBe(1);
        second.IsAwaitingLoadSlot.ShouldBeTrue();

        // Act
        _ = await dispatcher.InvokeAsync(
            () => tree.MaxConcurrentChildLoads = 2,
            TestContext.Current.CancellationToken);
        _ = secondDeferred.TrySetResult([]);

        // Assert
        await TreeViewChildLoadWait.UntilAsync(
            second,
            () => second.ChildState == TreeViewChildState.Loaded,
            TestContext.Current.CancellationToken);
        source.Requests.Count.ShouldBe(2);
        second.IsAwaitingLoadSlot.ShouldBeFalse();

        _ = firstDeferred.TrySetResult([]);
        await TreeViewChildLoadWait.UntilAsync(
            first,
            () => first.ChildState == TreeViewChildState.Loaded,
            TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a second concurrent load request beyond <see cref="TreeView.MaxConcurrentChildLoads"/>
    /// is admission-queued rather than issued immediately, and is granted its own slot once an
    /// earlier request releases one.</summary>
    [Fact]
    public async Task RequestLoadSlot_WhenConcurrencyLimitIsReached_QueuesTheAdditionalRequestAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var source = new FakeTreeViewChildSource();
        var firstDeferred = source.DeferNext(null);
        var secondDeferred = source.DeferNext(null);
        TreeView tree = null!;
        TreeViewItem first = null!;
        TreeViewItem second = null!;

        await dispatcher.InvokeAsync(() =>
        {
            tree = new TreeView { MaxConcurrentChildLoads = 1 };
            first = new TreeViewItem("First") { ChildSource = source, IsExpanded = false };
            second = new TreeViewItem("Second") { ChildSource = source, IsExpanded = false };
            tree.Items.Add(first);
            tree.Items.Add(second);
            tree.Attach(dispatcher);
            first.IsExpanded = true;
            second.IsExpanded = true;
        }, TestContext.Current.CancellationToken);

        first.ChildState.ShouldBe(TreeViewChildState.Loading);
        second.ChildState.ShouldBe(TreeViewChildState.Loading);
        source.Requests.Count.ShouldBe(1, "only the admitted request should have reached the source");
        second.IsAwaitingLoadSlot.ShouldBeTrue();

        var firstObservation = first.LastChildLoadObservation!;
        var secondObservation = second.LastChildLoadObservation!;

        // Resolved from this test thread, not the dispatcher - RunLoadAsync's slot-release path
        // (ConfigureAwait(false) throughout, so its continuation and finally block run wherever
        // the source's task completed) must marshal back to the dispatcher before touching the
        // tree's admission bookkeeping or the queued item's own state, exactly as its commit and
        // failure branches already do. Awaiting both observations and asserting they never
        // faulted is what would have caught that admission handoff corrupting state or throwing
        // an unobserved exception off-dispatcher.
        _ = firstDeferred.TrySetResult([]);
        _ = secondDeferred.TrySetResult([]);

        await TreeViewChildLoadWait.UntilAsync(
            first,
            () => first.ChildState == TreeViewChildState.Loaded,
            TestContext.Current.CancellationToken);
        await TreeViewChildLoadWait.UntilAsync(
            second,
            () => second.ChildState == TreeViewChildState.Loaded,
            TestContext.Current.CancellationToken);

        await firstObservation.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await secondObservation.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        firstObservation.IsFaulted.ShouldBeFalse();
        secondObservation.IsFaulted.ShouldBeFalse();

        second.IsAwaitingLoadSlot.ShouldBeFalse();
        source.Requests.Count.ShouldBe(2, "releasing the first slot must admit the queued second request");
    }

    /// <summary>Verifies collapsing an admission-queued item and immediately re-expanding it - all
    /// within the same dispatcher turn, before the cancelled request's posted slot-cleanup callback
    /// has had a chance to run - does not strand the re-expanded request. The cancelled request's
    /// deferred cleanup must not blindly clear whatever wait handle is currently installed: by the
    /// time it runs, the re-expand has already installed its own live one, and clearing it would
    /// leave nobody to ever grant that request its slot.</summary>
    [Fact]
    public async Task Expanded_WhenCollapsedAndReExpandedWhileAdmissionQueued_StillReachesLoadedAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var source = new FakeTreeViewChildSource();
        var firstDeferred = source.DeferNext(null);
        var secondDeferred = source.DeferNext(null);
        TreeView tree = null!;
        TreeViewItem first = null!;
        TreeViewItem second = null!;

        await dispatcher.InvokeAsync(() =>
        {
            tree = new TreeView { MaxConcurrentChildLoads = 1 };
            first = new TreeViewItem("First") { ChildSource = source, IsExpanded = false };
            second = new TreeViewItem("Second") { ChildSource = source, IsExpanded = false };
            tree.Items.Add(first);
            tree.Items.Add(second);
            tree.Attach(dispatcher);
            first.IsExpanded = true;
            second.IsExpanded = true;

            // Collapse the still-queued second item and immediately re-expand it, both within this
            // same synchronous block. The first expand's cancellation posts its slot-cleanup callback
            // back through the dispatcher rather than running it inline, so it cannot possibly run
            // before this block finishes - the re-expand below is guaranteed to have already installed
            // its own live wait handle by the time that stale cleanup eventually executes.
            second.IsExpanded = false;
            second.IsExpanded = true;
        }, TestContext.Current.CancellationToken);

        first.ChildState.ShouldBe(TreeViewChildState.Loading);
        second.ChildState.ShouldBe(TreeViewChildState.Loading);
        source.Requests.Count.ShouldBe(1, "only the admitted first request should have reached the source so far");

        var firstObservation = first.LastChildLoadObservation!;
        _ = firstDeferred.TrySetResult([]);

        await TreeViewChildLoadWait.UntilAsync(
            first,
            () => first.ChildState == TreeViewChildState.Loaded,
            TestContext.Current.CancellationToken);
        await firstObservation.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        // Releasing the first slot must admit the re-expanded second request. Before the fix, the
        // first expand's stale posted cleanup unconditionally nulled the field the re-expand had
        // already repointed at its own wait handle, so nothing was ever left to grant a slot to and
        // this second item stayed Loading forever - this call would time out.
        var secondObservation = second.LastChildLoadObservation!;
        _ = secondDeferred.TrySetResult([]);

        await TreeViewChildLoadWait.UntilAsync(
            second,
            () => second.ChildState == TreeViewChildState.Loaded,
            TestContext.Current.CancellationToken);
        await secondObservation.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        second.ChildState.ShouldBe(TreeViewChildState.Loaded);
        second.IsAwaitingLoadSlot.ShouldBeFalse();
        source.Requests.Count.ShouldBe(2, "the re-expanded second request must eventually reach the source");
    }

    /// <summary>Verifies a Control-held Enter over a LoadFailed item does not retry the load and
    /// leaves the stroke unhandled - matching the activation-eligible modifier gate
    /// <c>TreeView.OnKeyRouted</c> already applies to the ordinary activation path just a
    /// few lines below.</summary>
    [Fact]
    public async Task Dispatch_WhenEnterHasControlModifierOnLoadFailedItem_DoesNotRetryAndLeavesUnhandledAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var source = new FakeTreeViewChildSource();
        source.FailNext(null, new InvalidOperationException("simulated"));
        TreeView tree = null!;
        TreeViewItem item = null!;

        await dispatcher.InvokeAsync(() =>
        {
            item = new TreeViewItem("Root") { ChildSource = source };
            tree = new TreeView { Items = { item } };
            tree.Attach(dispatcher);
        }, TestContext.Current.CancellationToken);

        await TreeViewChildLoadWait.UntilAsync(
            item,
            () => item.ChildState == TreeViewChildState.LoadFailed,
            TestContext.Current.CancellationToken);

        await dispatcher.InvokeAsync(() =>
        {
            tree.SelectItem(item);
            using FocusManager focus = new(tree);
            focus.Focus(tree).ShouldBeTrue();

            var enter = new KeyEventArgs(new Stroke(
                Code.Enter, default, nativeCode: 0, Modifiers.Control, KeyAction.Press));
            _ = Router.Route(tree, Events.Key, enter);

            enter.IsHandled.ShouldBeFalse();
        }, TestContext.Current.CancellationToken);

        item.ChildState.ShouldBe(TreeViewChildState.LoadFailed);
        source.Requests.Count.ShouldBe(1, "the gated stroke must not have started a second request");
    }

    /// <summary>Verifies an Alt-held Enter over a LoadFailed item is gated the same way a
    /// Control-held one is.</summary>
    [Fact]
    public async Task Dispatch_WhenEnterHasAltModifierOnLoadFailedItem_DoesNotRetryAndLeavesUnhandledAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var source = new FakeTreeViewChildSource();
        source.FailNext(null, new InvalidOperationException("simulated"));
        TreeView tree = null!;
        TreeViewItem item = null!;

        await dispatcher.InvokeAsync(() =>
        {
            item = new TreeViewItem("Root") { ChildSource = source };
            tree = new TreeView { Items = { item } };
            tree.Attach(dispatcher);
        }, TestContext.Current.CancellationToken);

        await TreeViewChildLoadWait.UntilAsync(
            item,
            () => item.ChildState == TreeViewChildState.LoadFailed,
            TestContext.Current.CancellationToken);

        await dispatcher.InvokeAsync(() =>
        {
            tree.SelectItem(item);
            using FocusManager focus = new(tree);
            focus.Focus(tree).ShouldBeTrue();

            var enter = new KeyEventArgs(new Stroke(
                Code.Enter, default, nativeCode: 0, Modifiers.Alt, KeyAction.Press));
            _ = Router.Route(tree, Events.Key, enter);

            enter.IsHandled.ShouldBeFalse();
        }, TestContext.Current.CancellationToken);

        item.ChildState.ShouldBe(TreeViewChildState.LoadFailed);
        source.Requests.Count.ShouldBe(1, "the gated stroke must not have started a second request");
    }

    /// <summary>Verifies a plain Enter over a LoadFailed item still retries the load and handles
    /// the stroke.</summary>
    [Fact]
    public async Task Dispatch_WhenEnterIsPlainOnLoadFailedItem_RetriesAndHandlesAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var source = new FakeTreeViewChildSource();
        source.FailNext(null, new InvalidOperationException("simulated"));
        TreeView tree = null!;
        TreeViewItem item = null!;

        await dispatcher.InvokeAsync(() =>
        {
            item = new TreeViewItem("Root") { ChildSource = source };
            tree = new TreeView { Items = { item } };
            tree.Attach(dispatcher);
        }, TestContext.Current.CancellationToken);

        await TreeViewChildLoadWait.UntilAsync(
            item,
            () => item.ChildState == TreeViewChildState.LoadFailed,
            TestContext.Current.CancellationToken);

        source.AddChildren(null, new TreeViewChildDescription("a", "A") { Presence = TreeViewChildPresence.Leaf });

        await dispatcher.InvokeAsync(() =>
        {
            tree.SelectItem(item);
            using FocusManager focus = new(tree);
            focus.Focus(tree).ShouldBeTrue();

            var enter = new KeyEventArgs(new Stroke(
                Code.Enter, default, nativeCode: 0, Modifiers.None, KeyAction.Press));
            _ = Router.Route(tree, Events.Key, enter);

            enter.IsHandled.ShouldBeTrue();
            item.ChildState.ShouldBe(TreeViewChildState.Loading);
        }, TestContext.Current.CancellationToken);

        await TreeViewChildLoadWait.UntilAsync(
            item,
            () => item.ChildState == TreeViewChildState.Loaded,
            TestContext.Current.CancellationToken);

        item.Children.Count.ShouldBe(1);
        source.Requests.Count.ShouldBe(2, "the retry must have reached the source as a second request");
    }

    /// <summary>Verifies a Shift-held Enter (a common terminal chord) over a LoadFailed item still
    /// retries the load and handles the stroke.</summary>
    [Fact]
    public async Task Dispatch_WhenEnterHasShiftModifierOnLoadFailedItem_RetriesAndHandlesAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var source = new FakeTreeViewChildSource();
        source.FailNext(null, new InvalidOperationException("simulated"));
        TreeView tree = null!;
        TreeViewItem item = null!;

        await dispatcher.InvokeAsync(() =>
        {
            item = new TreeViewItem("Root") { ChildSource = source };
            tree = new TreeView { Items = { item } };
            tree.Attach(dispatcher);
        }, TestContext.Current.CancellationToken);

        await TreeViewChildLoadWait.UntilAsync(
            item,
            () => item.ChildState == TreeViewChildState.LoadFailed,
            TestContext.Current.CancellationToken);

        source.AddChildren(null, new TreeViewChildDescription("a", "A") { Presence = TreeViewChildPresence.Leaf });

        await dispatcher.InvokeAsync(() =>
        {
            tree.SelectItem(item);
            using FocusManager focus = new(tree);
            focus.Focus(tree).ShouldBeTrue();

            var enter = new KeyEventArgs(new Stroke(
                Code.Enter, default, nativeCode: 0, Modifiers.Shift, KeyAction.Press));
            _ = Router.Route(tree, Events.Key, enter);

            enter.IsHandled.ShouldBeTrue();
            item.ChildState.ShouldBe(TreeViewChildState.Loading);
        }, TestContext.Current.CancellationToken);

        await TreeViewChildLoadWait.UntilAsync(
            item,
            () => item.ChildState == TreeViewChildState.Loaded,
            TestContext.Current.CancellationToken);

        item.Children.Count.ShouldBe(1);
        source.Requests.Count.ShouldBe(2, "the retry must have reached the source as a second request");
    }

    /// <summary>Verifies a fresh tree carries the documented default status row text.</summary>
    [Fact]
    public void LoadingAndLoadFailedText_WhenCreated_UseDocumentedDefaults()
    {
        var tree = new TreeView();

        tree.LoadingText.ShouldBe("Loading…");
        tree.LoadFailedText.ShouldBe("Failed to load. Press Enter to retry.");
    }

    /// <summary>Verifies LoadingText and LoadFailedText round-trip a caller-assigned value.</summary>
    [Fact]
    public void LoadingAndLoadFailedText_WhenAssigned_RoundTrip()
    {
        var tree = new TreeView
        {
            LoadingText = "Please wait…",
            LoadFailedText = "Could not load.",
        };

        tree.LoadingText.ShouldBe("Please wait…");
        tree.LoadFailedText.ShouldBe("Could not load.");
    }

    /// <summary>Verifies LoadingText and LoadFailedText reject a null value.</summary>
    [Fact]
    public void LoadingAndLoadFailedText_WhenAssignedNull_ThrowArgumentNullException()
    {
        var tree = new TreeView();

        _ = Should.Throw<ArgumentNullException>(() => tree.LoadingText = null!);
        _ = Should.Throw<ArgumentNullException>(() => tree.LoadFailedText = null!);

        tree.LoadingText.ShouldBe("Loading…");
        tree.LoadFailedText.ShouldBe("Failed to load. Press Enter to retry.");
    }

    /// <summary>Verifies LoadingText and LoadFailedText reject a value containing a terminal
    /// control character instead of silently corrupting the rendered status row.</summary>
    [Theory]
    [InlineData("Loading\nnow")]
    [InlineData("Loading\tnow")]
    public void LoadingAndLoadFailedText_WhenContainingControlCharacter_ThrowArgumentException(string value)
    {
        var tree = new TreeView();

        _ = Should.Throw<ArgumentException>(() => tree.LoadingText = value);
        _ = Should.Throw<ArgumentException>(() => tree.LoadFailedText = value);

        tree.LoadingText.ShouldBe("Loading…");
        tree.LoadFailedText.ShouldBe("Failed to load. Press Enter to retry.");
    }
}
