// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Collections;

/// <summary>Verifies TreeViewItem's own start/end affix reservation, invalidation grading, and
/// measured layout, mirroring the shapes ButtonTests pins for Button's identical affix seam.</summary>
public sealed class TreeViewItemTests
{
    /// <summary>Verifies desired width grows by exactly one reserved column per set affix, plus
    /// the row's own one-cell gap, over the row's ordinary indent/disclosure/header baseline.</summary>
    [Theory]
    [InlineData(false, false, 0)]
    [InlineData(true, false, 2)]
    [InlineData(false, true, 2)]
    [InlineData(true, true, 4)]
    public void Measure_WhenAffixesAreSet_ReservesCellsPerAffixPlusGap(
        bool hasStart,
        bool hasEnd,
        int expectedExtraWidth)
    {
        var item = new TreeViewItem
        {
            Header = "abc",
            StartAffix = hasStart ? new Affix("!") : null,
            EndAffix = hasEnd ? new Affix("!") : null
        };

        item.Measure(new Constraint(80, 1));

        // indent(0) + disclosure(1) + leading space(1) + header(3), plus one reserved column and
        // one gap cell per affix that is set - matching MeasureOverride_WhenRowIsMeasured_
        // MatchesTheRenderedLayout's own baseline formula in TreeViewTests.
        item.DesiredSize.Width.ShouldBe(1 + 1 + 3 + expectedExtraWidth);
    }

    /// <summary>Verifies null-to-set and set-to-null affix assignment requires Measure.</summary>
    [Fact]
    public void StartAffix_WhenAssignedOrCleared_InvalidatesMeasure()
    {
        using var item = new TreeViewItem("Row");
        item.Clear(Invalidation.All);

        item.StartAffix = new Affix("!");

        item.Pending.ShouldBe(Invalidation.All);
        item.Clear(Invalidation.All);

        item.StartAffix = null;

        item.Pending.ShouldBe(Invalidation.All);
    }

    /// <summary>Verifies a same-resolved-width content or color swap invalidates rendering only,
    /// the exact grading an animated affix (a spinner swapping frames) depends on.</summary>
    [Fact]
    public void StartAffix_WhenContentOrColorChangesAtTheSameResolvedWidth_InvalidatesRenderOnly()
    {
        using var item = new TreeViewItem("Row") { StartAffix = new Affix("|") };
        item.Clear(Invalidation.All);

        item.StartAffix = new Affix("/");

        item.Pending.ShouldBe(Invalidation.Render);
        item.Clear(Invalidation.All);

        item.StartAffix = new Affix("/", "?", SemanticColor.Warning);

        item.Pending.ShouldBe(Invalidation.Render);
    }

    /// <summary>Verifies a resolved-width change (one cell to two cells) invalidates Measure again,
    /// not just Render, even though both values are non-null.</summary>
    [Fact]
    public void EndAffix_WhenResolvedWidthChanges_InvalidatesMeasure()
    {
        using var item = new TreeViewItem("Row") { EndAffix = new Affix("!") };
        item.Clear(Invalidation.All);

        // U+4E16 '世' is a wide CJK ideograph (two cells wide), unlike the one-cell '!' above.
        item.EndAffix = new Affix("世");

        item.Pending.ShouldBe(Invalidation.All);
    }

    /// <summary>Verifies reassigning the identical affix value is a no-op, matching every other
    /// SetProperty-backed member.</summary>
    [Fact]
    public void StartAffix_WhenReassignedTheSameValue_DoesNotInvalidate()
    {
        var affix = new Affix("!");
        using var item = new TreeViewItem("Row") { StartAffix = affix };
        item.Clear(Invalidation.All);

        item.StartAffix = affix;

        item.Pending.ShouldBe(Invalidation.None);
    }

    /// <summary>Verifies IsExpanded rejects a disposed item without committing the requested value
    /// first. AGENTS.md requires every setter to validate before changing observable state, and
    /// this setter wrote its backing field directly ahead of the ObjectDisposedException check
    /// embedded in the subsequent NotifyPropertyChanged call.</summary>
    [Fact]
    public void IsExpanded_WhenItemIsDisposed_ThrowsObjectDisposedExceptionWithoutMutatingState()
    {
        var item = new TreeViewItem("Row") { IsExpanded = true };
        item.Dispose();

        _ = Should.Throw<ObjectDisposedException>(() => item.IsExpanded = false);

        item.IsExpanded.ShouldBeTrue();
    }

    /// <summary>Verifies ChildSource rejects a disposed item without first cancelling pending
    /// loads, evicting loader-owned children, or committing the new source - the setter validated
    /// nothing about dispatcher access or disposal before mutating state.</summary>
    [Fact]
    public void ChildSource_WhenItemIsDisposed_ThrowsObjectDisposedExceptionWithoutMutatingState()
    {
        var item = new TreeViewItem("Row");
        item.Dispose();
        var source = new FakeTreeViewChildSource();

        _ = Should.Throw<ObjectDisposedException>(() => item.ChildSource = source);

        item.ChildSource.ShouldBeNull();
        item.ChildState.ShouldBe(TreeViewChildState.Leaf);
    }

    /// <summary>Verifies OnAttached propagates InvalidOperationException instead of swallowing it
    /// when attaching an already-expanded, unloaded item while the owning dispatcher's bounded
    /// queue is full, matching the same recipe
    /// <c>DispatcherTests.Post_WhenQueueIsFull_ThrowsBeforeEnqueueAsync</c> uses. Nothing else
    /// re-evaluates IsExpanded/ChildState after attachment, so silently swallowing this failure
    /// would leave the item stuck Unloaded forever instead of faulting the attaching transaction.</summary>
    [Fact]
    public async Task OnAttached_WhenDispatcherQueueIsSaturated_FaultsAttachmentAsync()
    {
        await using var dispatcher = Dispatcher.Start(capacity: 1);
        var source = new FakeTreeViewChildSource();
        var item = new TreeViewItem("Root") { ChildSource = source };
        var tree = new TreeView { Items = { item } };

        await dispatcher.InvokeAsync(
            () =>
            {
                // Still running as the dispatcher's own currently-executing work item, so this
                // filler sits queued (unprocessed) for the whole synchronous duration of Attach
                // below - the dispatcher's bounded queue has exactly one free slot before this
                // post, matching DispatcherTests' own recipe for a guaranteed queue-full post.
                dispatcher.Post(static () => { });

                _ = Should.Throw<InvalidOperationException>(() => tree.Attach(dispatcher));
            },
            TestContext.Current.CancellationToken);

        // The deferred retry never got scheduled, so the item stays Unloaded instead of
        // advancing - a sane, non-corrupted outcome rather than a silent, permanent stall.
        item.ChildState.ShouldBe(TreeViewChildState.Unloaded);
    }

    /// <summary>Verifies RunLoadAsync's success-path CommitChildLoad post - and its own bridging
    /// retry, since the queue stays saturated through both attempts here - drops the fault instead
    /// of letting it escape and fault the unobserved <see cref="TreeViewItem.LastChildLoadObservation"/>,
    /// when the owning dispatcher's bounded queue is full at the moment a deferred child load
    /// resolves - narrower coverage of the same three sites, including the bridge actually reaching
    /// <see cref="Dispatcher.UnhandledException"/> when the retry gets a genuine chance to succeed,
    /// lives in <c>TreeViewAsyncChildLoadingTests.DispatcherFullness</c>; this test proves the
    /// double-failure edge end-to-end through the real attach/expand/resolve sequence.</summary>
    [Fact]
    public async Task Expanded_WhenDispatcherQueueIsSaturatedAtSuccessfulCompletionOnBothAttempts_DropsTheFaultAsync()
    {
        await using var dispatcher = Dispatcher.Start(capacity: 1);
        var source = new FakeTreeViewChildSource();
        var deferred = source.DeferNext(null);
        TreeViewItem item = null!;
        TreeView tree = null!;
        Task observation = null!;

        await dispatcher.InvokeAsync(
            () =>
            {
                item = new TreeViewItem("Root") { ChildSource = source, IsExpanded = false };
                tree = new TreeView { Items = { item } };
                tree.Attach(dispatcher);
                item.IsExpanded = true;
                observation = item.LastChildLoadObservation!;
            },
            TestContext.Current.CancellationToken);

        item.ChildState.ShouldBe(TreeViewChildState.Loading);

        // Saturate the queue while the load is still deferred, then resolve it - RunLoadAsync's
        // continuation resumes off the dispatcher (ConfigureAwait(false)) and must observe a
        // genuinely full queue at both of its own posts, not the one free slot they'd otherwise
        // always find.
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using ManualResetEventSlim release = new();
        dispatcher.Post(() =>
        {
            entered.SetResult();
            release.Wait();
        });
        await entered.Task.WaitAsync(TestContext.Current.CancellationToken);

        try
        {
            try
            {
                while (true)
                {
                    dispatcher.Post(static () => { });
                }
            }
            catch (InvalidOperationException)
            {
            }

            Should.NotThrow(() => { _ = deferred.TrySetResult([]); });

            await Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken);
        }
        finally
        {
            release.Set();
        }

        // The queue never drains during this window (the hostage stays blocked until release.Set()
        // above), so both the success-path post and its own bridging retry find it full - the
        // deliberately accepted double-failure edge, dropped rather than retried indefinitely,
        // rather than escaping and faulting this unobserved task.
        await observation.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        observation.IsCompletedSuccessfully.ShouldBeTrue();

        // The posted commit never got a chance to run, so the item stays Loading instead of
        // advancing - a sane, non-corrupted outcome rather than a silent, permanent stall.
        item.ChildState.ShouldBe(TreeViewChildState.Loading);
    }

    /// <summary>Verifies RunLoadAsync's failure-path CommitChildLoadFailure post - and its own
    /// bridging retry, since the queue stays saturated through both attempts here - drops the fault
    /// instead of letting it escape and fault the unobserved <see cref="TreeViewItem.LastChildLoadObservation"/>,
    /// when the owning dispatcher's bounded queue is full at the moment a deferred child load
    /// faults - narrower coverage of the same three sites lives in
    /// <c>TreeViewAsyncChildLoadingTests.DispatcherFullness</c>; this test proves the double-failure
    /// edge end-to-end through the real attach/expand/reject sequence.</summary>
    [Fact]
    public async Task Expanded_WhenDispatcherQueueIsSaturatedAtFailedCompletionOnBothAttempts_DropsTheFaultAsync()
    {
        await using var dispatcher = Dispatcher.Start(capacity: 1);
        var source = new FakeTreeViewChildSource();
        var deferred = source.DeferNext(null);
        TreeViewItem item = null!;
        TreeView tree = null!;
        Task observation = null!;

        await dispatcher.InvokeAsync(
            () =>
            {
                item = new TreeViewItem("Root") { ChildSource = source, IsExpanded = false };
                tree = new TreeView { Items = { item } };
                tree.Attach(dispatcher);
                item.IsExpanded = true;
                observation = item.LastChildLoadObservation!;
            },
            TestContext.Current.CancellationToken);

        item.ChildState.ShouldBe(TreeViewChildState.Loading);

        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using ManualResetEventSlim release = new();
        dispatcher.Post(() =>
        {
            entered.SetResult();
            release.Wait();
        });
        await entered.Task.WaitAsync(TestContext.Current.CancellationToken);

        try
        {
            try
            {
                while (true)
                {
                    dispatcher.Post(static () => { });
                }
            }
            catch (InvalidOperationException)
            {
            }

            Should.NotThrow(() => { _ = deferred.TrySetException(new InvalidOperationException("boom")); });

            await Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken);
        }
        finally
        {
            release.Set();
        }

        // The queue never drains during this window (the hostage stays blocked until release.Set()
        // above), so both the failure-path post and its own bridging retry find it full - the
        // deliberately accepted double-failure edge, dropped rather than retried indefinitely,
        // rather than escaping and faulting this unobserved task.
        await observation.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        observation.IsCompletedSuccessfully.ShouldBeTrue();

        // The posted failure commit never got a chance to run, so the item stays Loading instead
        // of advancing - a sane, non-corrupted outcome rather than a silent, permanent stall.
        item.ChildState.ShouldBe(TreeViewChildState.Loading);
    }
}
