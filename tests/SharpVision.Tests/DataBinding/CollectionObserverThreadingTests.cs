// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.DataBinding;

using System.Collections.ObjectModel;

using SharpVision.DataBinding;

using Support;

/// <summary>Verifies CollectionObserver handles concurrent Observe and Dispose calls safely.</summary>
public sealed class CollectionObserverThreadingTests
{
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
