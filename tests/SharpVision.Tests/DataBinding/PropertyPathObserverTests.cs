// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.DataBinding;

using System.Linq.Expressions;

using SharpVision.DataBinding;

using Support;

/// <summary>Verifies PropertyPathObserver handles concurrent PropertyChanged notifications safely.</summary>
public sealed class PropertyPathObserverTests
{
    /// <summary>Verifies failure removing one path segment does not skip later segments.</summary>
    [Fact]
    public void Dispose_WhenOneSegmentRemovalThrows_AttemptsEverySegment()
    {
        var child = new ThrowingBindingNode { Value = "value" };
        var root = new ThrowingBindingNode { Child = child, ThrowOnRemove = true };
        Expression<Func<ThrowingBindingNode, string?>> expression = source => source.Child!.Value;
        var path = PropertyPath.Create(expression, requireWritable: false);
        var observer = new PropertyPathObserver(root, path, () => { });

        _ = Should.Throw<InvalidOperationException>(observer.Dispose);

        root.RemovalAttempts.ShouldBe(1);
        child.RemovalAttempts.ShouldBe(1);
    }

    /// <summary>Verifies concurrent PropertyChanged events from multiple threads do not corrupt the observer.</summary>
    [Fact]
    public void OnPropertyChanged_FromMultipleThreads_DoesNotCorrupt()
    {
        Expression<Func<BindingModel, string?>> expression = source => source.Name;
        var model = new BindingModel { Name = "Initial" };
        var path = PropertyPath.Create(expression, requireWritable: false);
        var callbacks = 0;
        using var observer = new PropertyPathObserver(model, path, () => Interlocked.Increment(ref callbacks));

        _ = Parallel.For(0, 1_000, index =>
        {
            model.Name = $"Value{index}";
        });

        callbacks.ShouldBeGreaterThan(0);
    }

    /// <summary>Verifies concurrent PropertyChanged events on a nested path do not corrupt the observer.</summary>
    [Fact]
    public void OnPropertyChanged_NestedPath_FromMultipleThreads_DoesNotCorrupt()
    {
        Expression<Func<BindingModel, string?>> expression = source => source.Address!.City;
        var model = new BindingModel { Address = new BindingAddress { City = "Initial" } };
        var path = PropertyPath.Create(expression, requireWritable: false);
        var callbacks = 0;
        using var observer = new PropertyPathObserver(model, path, () => Interlocked.Increment(ref callbacks));

        _ = Parallel.For(0, 1_000, index =>
        {
            model.Address!.City = $"City{index}";
        });

        callbacks.ShouldBeGreaterThan(0);
    }

    /// <summary>Verifies concurrent disposal and property changes do not throw.</summary>
    [Fact]
    public async Task Dispose_ConcurrentWithPropertyChanged_DoesNotThrowAsync()
    {
        Expression<Func<BindingModel, string?>> expression = source => source.Name;
        var model = new BindingModel { Name = "Initial" };
        var path = PropertyPath.Create(expression, requireWritable: false);
        var callbacks = 0;
        var observer = new PropertyPathObserver(model, path, () => Interlocked.Increment(ref callbacks));
        using var barrier = new ManualResetEventSlim(false);

        var mutator = Task.Run(() =>
        {
            barrier.Wait(TestContext.Current.CancellationToken);

            for (var index = 0; index < 500; index++)
            {
                model.Name = $"Value{index}";
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

    /// <summary>Verifies rapid all-property notifications from many threads converge without corruption.</summary>
    [Fact]
    public void RaiseAll_FromMultipleThreads_DoesNotCorrupt()
    {
        Expression<Func<BindingModel, string?>> expression = source => source.Name;
        var model = new BindingModel { Name = "Stable" };
        var path = PropertyPath.Create(expression, requireWritable: false);
        var callbacks = 0;
        using var observer = new PropertyPathObserver(model, path, () => Interlocked.Increment(ref callbacks));

        _ = Parallel.For(0, 1_000, _ =>
        {
            model.RaiseAll();
        });

        callbacks.ShouldBeGreaterThan(0);
    }

    /// <summary>Verifies observer disposal is idempotent and suppresses later callbacks.</summary>
    [Fact]
    public void Dispose_WhenObserverDisposedTwice_SuppressesNotifications()
    {
        Expression<Func<BindingModel, string?>> expression = source => source.Name;
        var model = new BindingModel { Name = "Before" };
        var path = PropertyPath.Create(expression, requireWritable: false);
        var callbacks = 0;
        var observer = new PropertyPathObserver(model, path, () => callbacks++);

        observer.Dispose();
        observer.Dispose();
        model.Name = "After";

        callbacks.ShouldBe(0);
    }

    /// <summary>Verifies construction racing PropertyChanged notifications on the intermediate segment
    /// owner never leaks an orphaned handler and always unsubscribes fully on Dispose.</summary>
    /// <remarks>
    /// Each iteration's mutator burst is bounded and fully joined before the subscriber-count
    /// assertions run. An unbounded, continuously-running mutator would also race the assertions
    /// themselves against the observer's own legitimate, correctly-locked rebuilds - transiently
    /// unsubscribed between <c>UnsubscribeAll</c> and re-subscribe - producing a flaky 0 that has
    /// nothing to do with the leak this test targets. Joining the burst first removes that
    /// unrelated race while still exercising the fixed construction-time window: the burst starts
    /// before the constructor and can still land calls while <see cref="PropertyPathObserver"/>'s
    /// constructor is walking the path.
    /// </remarks>
    [Fact]
    public async Task Construction_ConcurrentWithPropertyChangedOnIntermediateOwner_DoesNotLeakHandlersAsync()
    {
        Expression<Func<BindingModel, string?>> expression = source => source.Address!.City;
        var model = new BindingModel { Address = new BindingAddress { City = "Initial" } };
        var path = PropertyPath.Create(expression, requireWritable: false);

        for (var iteration = 0; iteration < 1_000; iteration++)
        {
            var burst = Task.Run(
                () =>
                {
                    for (var raise = 0; raise < 200; raise++)
                    {
                        model.RaiseAll();
                    }
                },
                TestContext.Current.CancellationToken);

            var observer = new PropertyPathObserver(model, path, static () => { });

            await burst;

            model.PropertyChangedSubscriberCount.ShouldBe(1);
            model.Address!.PropertyChangedSubscriberCount.ShouldBe(1);

            observer.Dispose();

            model.PropertyChangedSubscriberCount.ShouldBe(0);
            model.Address!.PropertyChangedSubscriberCount.ShouldBe(0);
        }
    }
}
