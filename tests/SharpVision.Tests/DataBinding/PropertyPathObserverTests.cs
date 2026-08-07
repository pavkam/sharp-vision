// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.DataBinding;

using System.Linq.Expressions;

using SharpVision.DataBinding;

using Support;

/// <summary>Verifies PropertyPathObserver handles concurrent PropertyChanged notifications safely.</summary>
public sealed class PropertyPathObserverTests
{
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
}
