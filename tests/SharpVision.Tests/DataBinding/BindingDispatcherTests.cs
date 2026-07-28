// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.DataBinding;

using SharpVision.DataBinding;

using Support;

/// <summary>Verifies attached bindings preserve dispatcher affinity and bounded responsiveness.</summary>
public sealed class BindingDispatcherTests
{
    /// <summary>Verifies a source change already on the dispatcher is visible before notification returns.</summary>
    [Fact]
    public async Task Source_WhenChangedOnDispatcher_UpdatesInlineAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var model = new BindingModel { Name = "Before" };
            var target = new ControlText();
            target.Attach(dispatcher);
            using var binding = target.Bind(model, source => source.Name);

            model.Name = "After";

            target.Content.ShouldBe("After");
            target.Dispose();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies explicit disposal of an attached binding is dispatcher-affine.</summary>
    [Fact]
    public async Task Dispose_WhenCalledFromWorker_ThrowsBeforeReleasingBindingAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var model = new BindingModel { Name = "Before" };
        var target = new ControlText();
        Binding? binding = null;
        await dispatcher.InvokeAsync(() =>
        {
            target.Attach(dispatcher);
            binding = target.Bind(model, source => source.Name);
        }, TestContext.Current.CancellationToken);

        _ = Should.Throw<InvalidOperationException>(binding!.Dispose);

        await dispatcher.InvokeAsync(() =>
        {
            binding.IsDisposed.ShouldBeFalse();
            binding.Dispose();
            target.Dispose();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a queued update becomes inert when an earlier queued callback disposes it.</summary>
    [Fact]
    public async Task Source_WhenBindingIsDisposedBeforeQueuedUpdate_SuppressesMutationAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var model = new BindingModel { Name = "Before" };
        var target = new ControlText();
        Binding? binding = null;
        await dispatcher.InvokeAsync(() =>
        {
            target.Attach(dispatcher);
            binding = target.Bind(model, source => source.Name);
        }, TestContext.Current.CancellationToken);
        using ManualResetEventSlim entered = new();
        using ManualResetEventSlim release = new();
        dispatcher.Post(() =>
        {
            entered.Set();
            release.Wait(TestContext.Current.CancellationToken);
        });
        entered.Wait(TestContext.Current.CancellationToken);
        dispatcher.Post(binding!.Dispose);

        await Task.Run(() =>
        {
            model.Name = "Queued";
        }, TestContext.Current.CancellationToken);
        release.Set();

        await dispatcher.InvokeAsync(() =>
        {
            binding.IsDisposed.ShouldBeTrue();
            target.Content.ShouldBe("Before");
            target.Dispose();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies queue saturation propagates and a later notification can retry.</summary>
    [Fact]
    public async Task Source_WhenDispatcherQueueIsFull_ThrowsAndPermitsRetryAsync()
    {
        await using var dispatcher = Dispatcher.Start(capacity: 1);
        var model = new BindingModel { Name = "Before" };
        var target = new ControlText();
        Binding? binding = null;
        using ManualResetEventSlim updated = new();
        await dispatcher.InvokeAsync(() =>
        {
            target.Attach(dispatcher);
            binding = target.Bind(model, source => source.Name);
            target.PropertyChanged += (_, eventArgs) =>
            {
                if (eventArgs.PropertyName == nameof(ControlText.Content) && target.Content == "Retried")
                {
                    updated.Set();
                }
            };
        }, TestContext.Current.CancellationToken);
        using ManualResetEventSlim entered = new();
        using ManualResetEventSlim release = new();
        using ManualResetEventSlim queuedCompleted = new();
        dispatcher.Post(() =>
        {
            entered.Set();
            release.Wait(TestContext.Current.CancellationToken);
        });
        entered.Wait(TestContext.Current.CancellationToken);
        dispatcher.Post(queuedCompleted.Set);

        _ = Should.Throw<InvalidOperationException>(() => model.Name = "Rejected");
        release.Set();
        queuedCompleted.Wait(TestContext.Current.CancellationToken);

        model.Name = "Retried";
        updated.Wait(TestContext.Current.CancellationToken);

        await dispatcher.InvokeAsync(() =>
        {
            target.Content.ShouldBe("Retried");
            binding!.Dispose();
            target.Dispose();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a worker burst posts one latest-value target mutation.</summary>
    [Fact]
    public async Task Source_WhenWorkerBurstArrives_CoalescesLatestOnDispatcherAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var model = new BindingModel { Name = "0" };
        var target = new ControlText();
        Binding? binding = null;
        var changes = 0;

        await dispatcher.InvokeAsync(() =>
        {
            target.Attach(dispatcher);
            binding = target.Bind(model, source => source.Name);
            target.PropertyChanged += (_, eventArgs) =>
            {
                if (eventArgs.PropertyName == nameof(ControlText.Content))
                {
                    changes++;
                }
            };
        }, TestContext.Current.CancellationToken);

        using ManualResetEventSlim entered = new();
        using ManualResetEventSlim release = new();
        dispatcher.Post(() =>
        {
            entered.Set();
            release.Wait(TestContext.Current.CancellationToken);
        });
        entered.Wait(TestContext.Current.CancellationToken);

        try
        {
            await Task.Run(() =>
            {
                for (var index = 1; index <= 10_000; index++)
                {
                    model.Name = index.ToString(CultureInfo.InvariantCulture);
                }
            }, TestContext.Current.CancellationToken);
        }
        finally
        {
            release.Set();
        }

        await dispatcher.InvokeAsync(() =>
        {
            target.Content.ShouldBe("10000");
            changes.ShouldBe(1);
            binding!.Dispose();
            target.Dispose();
        }, TestContext.Current.CancellationToken);
    }
}
