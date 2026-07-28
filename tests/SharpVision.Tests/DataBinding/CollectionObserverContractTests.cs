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
    public void PendingChange_WhenSingleAddFires_StoresTheChangeArgs()
    {
        using var observer = new CollectionObserver(() => { });
        var source = new ObservableCollection<string>();
        observer.Observe(source);

        source.Add("A");

        _ = observer.PendingChange.ShouldNotBeNull();
        observer.PendingChange!.Action.ShouldBe(NotifyCollectionChangedAction.Add);
        observer.PendingChange.NewItems![0].ShouldBe("A");
        observer.PendingChange.NewStartingIndex.ShouldBe(0);
    }

    /// <summary>Verifies a single remove stores the pending change.</summary>
    [Fact]
    public void PendingChange_WhenSingleRemoveFires_StoresTheChangeArgs()
    {
        using var observer = new CollectionObserver(() => { });
        var source = new ObservableCollection<string> { "A", "B" };
        observer.Observe(source);

        _ = source.Remove("A");

        _ = observer.PendingChange.ShouldNotBeNull();
        observer.PendingChange!.Action.ShouldBe(NotifyCollectionChangedAction.Remove);
        observer.PendingChange.OldItems![0].ShouldBe("A");
        observer.PendingChange.OldStartingIndex.ShouldBe(0);
    }

    /// <summary>Verifies two rapid changes coalesce to null pending change.</summary>
    [Fact]
    public void PendingChange_WhenTwoChangesFireBeforeConsumption_CoalescesToNull()
    {
        using var observer = new CollectionObserver(() => { });
        var source = new ObservableCollection<string>();
        observer.Observe(source);

        source.Add("A");
        source.Add("B");

        observer.PendingChange.ShouldBeNull();
    }

    /// <summary>Verifies consuming the pending change resets state for the next cycle.</summary>
    [Fact]
    public void ConsumePendingChange_AfterConsumption_AllowsNextSingleChange()
    {
        using var observer = new CollectionObserver(() => { });
        var source = new ObservableCollection<string>();
        observer.Observe(source);

        source.Add("A");
        observer.ConsumePendingChange();
        observer.PendingChange.ShouldBeNull();

        source.Add("B");

        _ = observer.PendingChange.ShouldNotBeNull();
        observer.PendingChange!.Action.ShouldBe(NotifyCollectionChangedAction.Add);
        observer.PendingChange.NewItems![0].ShouldBe("B");
    }

    /// <summary>Verifies consuming resets coalesced state so a fresh single change is available.</summary>
    [Fact]
    public void ConsumePendingChange_AfterCoalescing_ResetsForNextCycle()
    {
        using var observer = new CollectionObserver(() => { });
        var source = new ObservableCollection<string>();
        observer.Observe(source);

        source.Add("A");
        source.Add("B");
        observer.PendingChange.ShouldBeNull();

        observer.ConsumePendingChange();
        source.Add("C");

        _ = observer.PendingChange.ShouldNotBeNull();
        observer.PendingChange!.NewItems![0].ShouldBe("C");
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
        _ = observer.PendingChange.ShouldNotBeNull();

        observer.Observe(second);
        observer.PendingChange.ShouldBeNull();
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

        _ = observer.PendingChange.ShouldNotBeNull();
    }
}
