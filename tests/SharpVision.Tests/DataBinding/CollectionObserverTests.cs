// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.DataBinding;

using System.Collections.ObjectModel;
using System.Collections.Specialized;

using SharpVision.DataBinding;

using Support;

/// <summary>Verifies collection-observer lifetime and incremental change tracking.</summary>
public sealed class CollectionObserverTests
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

    /// <summary>Verifies a failed candidate subscription remains retryable and never replaces the
    /// still-authoritative observed source.</summary>
    [Fact]
    public void Observe_WhenCandidateAddThrows_RetrySubscribesExactlyOnce()
    {
        using var observer = new CollectionObserver(() => { });
        var source = new ProbeCollectionChangedSource { ThrowOnNextAdd = true };

        _ = Should.Throw<InvalidOperationException>(() => observer.Observe(source));
        source.SubscriberCount.ShouldBe(0);

        observer.Observe(source);
        source.SubscriberCount.ShouldBe(1);
        source.RaiseAdd();

        observer.TryTakePendingChange(out var change).ShouldBeTrue();
        change.Action.ShouldBe(NotifyCollectionChangedAction.Add);
    }

    /// <summary>Verifies an add accessor that registers before throwing is compensated before a
    /// later retry, so the successful retry still owns exactly one handler.</summary>
    [Fact]
    public void Observe_WhenCandidateAddThrowsAfterRegistration_RetryDoesNotDuplicateHandler()
    {
        using var observer = new CollectionObserver(() => { });
        var source = new ProbeCollectionChangedSource { ThrowAfterNextAdd = true };

        _ = Should.Throw<InvalidOperationException>(() => observer.Observe(source));
        source.SubscriberCount.ShouldBe(0);

        observer.Observe(source);

        source.SubscriberCount.ShouldBe(1);
    }

    /// <summary>Verifies a committed replacement remains authoritative when cleanup of its old
    /// source reports a failure.</summary>
    [Fact]
    public void Observe_WhenOldRemovalThrows_KeepsReplacementAuthoritative()
    {
        using var observer = new CollectionObserver(() => { });
        var previous = new ProbeCollectionChangedSource();
        var replacement = new ProbeCollectionChangedSource();
        observer.Observe(previous);
        previous.ThrowOnNextRemove = true;

        _ = Should.Throw<InvalidOperationException>(() => observer.Observe(replacement));
        replacement.SubscriberCount.ShouldBe(1);

        previous.RaiseAdd();
        observer.TryTakePendingChange(out _).ShouldBeFalse();
        replacement.RaiseAdd();
        observer.TryTakePendingChange(out _).ShouldBeTrue();
    }

    /// <summary>Verifies synchronous candidate notification during event registration is ignored
    /// until that candidate commits and a complete snapshot can establish its baseline.</summary>
    [Fact]
    public void Observe_WhenCandidateRaisesDuringAdd_DoesNotPublishPrematureChange()
    {
        using var observer = new CollectionObserver(() => { });
        var source = new ProbeCollectionChangedSource();
        source.Added = source.RaiseAdd;

        observer.Observe(source);

        observer.TryTakePendingChange(out _).ShouldBeFalse();
        source.RaiseAdd();
        observer.TryTakePendingChange(out _).ShouldBeTrue();
    }

    /// <summary>Verifies add-accessor reentry cannot leave a superseded candidate subscribed or
    /// duplicate the final winner's handler.</summary>
    [Fact]
    public void Observe_WhenCandidateAddReentersWithNewWinner_SubscribesOnlyWinner()
    {
        using var observer = new CollectionObserver(() => { });
        var candidate = new ProbeCollectionChangedSource();
        var winner = new ProbeCollectionChangedSource();
        candidate.Adding = () =>
        {
            candidate.Adding = null;
            observer.Observe(winner);
        };

        observer.Observe(candidate);

        candidate.SubscriberCount.ShouldBe(0);
        winner.SubscriberCount.ShouldBe(1);
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

    /// <summary>Verifies concurrent Observe calls with different collections do not corrupt state.</summary>
    [Fact]
    public void Observe_FromMultipleThreads_DoesNotCorrupt()
    {
        var callbacks = 0;
        using var observer = new CollectionObserver(() => Interlocked.Increment(ref callbacks));
        var collections = Enumerable.Range(0, 100)
            .Select(_ => new ObservableCollection<BindingItem>())
            .ToArray();

        _ = Parallel.For(0, 1_000, index =>
        {
            observer.Observe(collections[index % collections.Length]);
        });
    }

    /// <summary>Verifies concurrent disposal and observation do not throw.</summary>
    [Fact]
    public async Task Dispose_ConcurrentWithObserve_DoesNotThrowAsync()
    {
        var callbacks = 0;
        var observer = new CollectionObserver(() => Interlocked.Increment(ref callbacks));
        var collection = new ObservableCollection<BindingItem>();
        using var barrier = new ManualResetEventSlim(false);

        var mutator = Task.Run(() =>
        {
            barrier.Wait(TestContext.Current.CancellationToken);

            for (var index = 0; index < 500; index++)
            {
                observer.Observe(index % 2 == 0 ? collection : null);
            }
        }, TestContext.Current.CancellationToken);

        var disposer = Task.Run(() =>
        {
            barrier.Wait(TestContext.Current.CancellationToken);
            observer.Dispose();
        }, TestContext.Current.CancellationToken);

        barrier.Set();

        await Task.WhenAll(mutator, disposer);
    }
}
