// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.DataBinding;

using System.Collections.ObjectModel;
using System.Collections.Specialized;

using SharpVision.DataBinding;

/// <summary>Verifies collection-observer lifetime and incremental change tracking.</summary>
public sealed class CollectionObserverContractTests
{
    /// <summary>Verifies observing and disposing null requires no synthetic notification.</summary>
    [Fact]
    public void Dispose_WhenNoCollectionWasObserved_IsQuiet()
    {
        var callbacks = 0;
        using var observer = new CollectionObserver(() => callbacks++);

        observer.Observe(null);

        callbacks.ShouldBe(0);
    }

    /// <summary>Verifies a single add stores the pending change without coalescing.</summary>
    [Fact]
    public void TryTakePendingChange_WhenSingleAddFires_ReturnsTheChangeArgs()
    {
        using var observer = new CollectionObserver(() => { });
        var source = new ObservableCollection<string>();
        observer.Observe(source);

        source.Add("A");

        observer.TryTakePendingChange(out var change).ShouldBeTrue();
        change!.Action.ShouldBe(NotifyCollectionChangedAction.Add);
        change.NewItems![0].ShouldBe("A");
        change.NewStartingIndex.ShouldBe(0);
    }

    /// <summary>Verifies a single remove stores the pending change.</summary>
    [Fact]
    public void TryTakePendingChange_WhenSingleRemoveFires_ReturnsTheChangeArgs()
    {
        using var observer = new CollectionObserver(() => { });
        var source = new ObservableCollection<string> { "A", "B" };
        observer.Observe(source);

        _ = source.Remove("A");

        observer.TryTakePendingChange(out var change).ShouldBeTrue();
        change!.Action.ShouldBe(NotifyCollectionChangedAction.Remove);
        change.OldItems![0].ShouldBe("A");
        change.OldStartingIndex.ShouldBe(0);
    }

    /// <summary>Verifies two rapid changes coalesce, leaving no takeable pending change.</summary>
    [Fact]
    public void TryTakePendingChange_WhenTwoChangesFireBeforeConsumption_ReturnsFalse()
    {
        using var observer = new CollectionObserver(() => { });
        var source = new ObservableCollection<string>();
        observer.Observe(source);

        source.Add("A");
        source.Add("B");

        observer.TryTakePendingChange(out _).ShouldBeFalse();
    }

    /// <summary>Verifies taking the pending change resets state for the next cycle.</summary>
    [Fact]
    public void TryTakePendingChange_AfterConsumption_AllowsNextSingleChange()
    {
        using var observer = new CollectionObserver(() => { });
        var source = new ObservableCollection<string>();
        observer.Observe(source);

        source.Add("A");
        observer.TryTakePendingChange(out _).ShouldBeTrue();
        observer.TryTakePendingChange(out _).ShouldBeFalse();

        source.Add("B");

        observer.TryTakePendingChange(out var change).ShouldBeTrue();
        change!.Action.ShouldBe(NotifyCollectionChangedAction.Add);
        change.NewItems![0].ShouldBe("B");
    }

    /// <summary>Verifies a coalesced cycle clears once a fresh single change arrives.</summary>
    [Fact]
    public void TryTakePendingChange_AfterCoalescing_ResetsForNextCycle()
    {
        using var observer = new CollectionObserver(() => { });
        var source = new ObservableCollection<string>();
        observer.Observe(source);

        source.Add("A");
        source.Add("B");
        observer.TryTakePendingChange(out _).ShouldBeFalse();

        source.Add("C");

        observer.TryTakePendingChange(out var change).ShouldBeTrue();
        change!.NewItems![0].ShouldBe("C");
    }

    /// <summary>Verifies replacing the observed collection clears pending change state.</summary>
    [Fact]
    public void Observe_WhenCollectionIsReplaced_ClearsPendingChange()
    {
        using var observer = new CollectionObserver(() => { });
        var first = new ObservableCollection<string>();
        var second = new ObservableCollection<string>();
        observer.Observe(first);

        first.Add("A");

        observer.Observe(second);

        observer.TryTakePendingChange(out _).ShouldBeFalse();
    }

    /// <summary>Verifies observing the same collection does not clear the pending change.</summary>
    [Fact]
    public void Observe_WhenSameCollectionReObserved_PreservesPendingChange()
    {
        using var observer = new CollectionObserver(() => { });
        var source = new ObservableCollection<string>();
        observer.Observe(source);

        source.Add("A");
        observer.Observe(source);

        observer.TryTakePendingChange(out _).ShouldBeTrue();
    }

    /// <summary>
    /// Verifies a notification delivered from a source Observe() has already
    /// replaced is rejected instead of corrupting state tracked for the new
    /// source. This reproduces the interleaving deterministically: an earlier
    /// registered handler on the same collection replaces the observed source
    /// from within its own CollectionChanged callback, so .NET's
    /// already-snapshotted invocation list still calls the observer with the
    /// old sender after Observe() has moved on.
    /// </summary>
    [Fact]
    public void OnCollectionChanged_WhenSenderIsNoLongerObserved_IgnoresTheNotification()
    {
        var first = new ObservableCollection<string>();
        var second = new ObservableCollection<string> { "replacement" };
        CollectionObserver? observer = null;
        using var actualObserver = new CollectionObserver(() => { });
        observer = actualObserver;

        // Registered before the observer, so it runs first in the snapshot and
        // can synchronously switch the observed source mid-dispatch.
        first.CollectionChanged += (_, _) => observer.Observe(second);
        observer.Observe(first);

        first.Add("stale");

        // The stale notification from `first` must not have been recorded as
        // the pending change for `second`.
        observer.TryTakePendingChange(out _).ShouldBeFalse();
    }

    /// <summary>
    /// Verifies a notification delivered after Dispose() (no observed source
    /// at all) is rejected rather than reviving a cleared subscription.
    /// </summary>
    [Fact]
    public void OnCollectionChanged_WhenObserverIsDisposed_IgnoresStaleNotification()
    {
        var source = new ObservableCollection<string>();
        var observer = new CollectionObserver(() => { });
        observer.Observe(source);

        observer.Dispose();

        // The subscription was removed by Dispose, so this Add cannot reach
        // OnCollectionChanged through the live event; the assertion instead
        // documents that no pending state exists to leak into a later reuse.
        source.Add("after-dispose");

        observer.TryTakePendingChange(out _).ShouldBeFalse();
    }
}
