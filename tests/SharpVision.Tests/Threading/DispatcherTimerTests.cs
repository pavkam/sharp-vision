// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Threading;

using Input;

/// <summary>Verifies deterministic dispatcher timer scheduling and lifetime.</summary>
public sealed class DispatcherTimerTests
{
    /// <summary>Verifies the first complete interval raises one tick on the owning dispatcher.</summary>
    [Fact]
    public async Task Start_WhenOneIntervalElapses_RaisesTickOnDispatcherAsync()
    {
        // Arrange
        var clock = new ManualTimeProvider();
        await using var dispatcher = Dispatcher.Start(timeProvider: clock);
        var ticks = 0;
        var hasAccess = false;
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var timer = await dispatcher.InvokeAsync(
            () =>
            {
                var value = new DispatcherTimer(dispatcher, TimeSpan.FromMilliseconds(200));
                value.Tick += (_, _) =>
                {
                    ticks++;
                    hasAccess = dispatcher.CheckAccess();
                    _ = completed.TrySetResult();
                };
                value.Start();
                return value;
            },
            TestContext.Current.CancellationToken);

        // Act
        clock.Advance(TimeSpan.FromMilliseconds(199));
        await dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);

        // Assert
        ticks.ShouldBe(0);
        clock.Advance(TimeSpan.FromMilliseconds(1));
        await completed.Task.WaitAsync(TestContext.Current.CancellationToken);
        ticks.ShouldBe(1);
        hasAccess.ShouldBeTrue();
        timer.Dispose();
    }

    /// <summary>Verifies invalid intervals fail before a timer can be created.</summary>
    [Fact]
    public async Task Constructor_WhenIntervalIsOutsideSupportedRange_RejectsBeforeConstructionAsync()
    {
        // Arrange
        await using var dispatcher = Dispatcher.Start();

        // Act and assert
        await dispatcher.InvokeAsync(() =>
        {
            _ = Should.Throw<ArgumentOutOfRangeException>(() =>
                new DispatcherTimer(dispatcher, TimeSpan.Zero));
            _ = Should.Throw<ArgumentOutOfRangeException>(() =>
                new DispatcherTimer(dispatcher, TimeSpan.FromMilliseconds((double) int.MaxValue + 1)));
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies stopping on the dispatcher invalidates one already-posted tick.</summary>
    [Fact]
    public async Task Stop_WhenTickWasPosted_SuppressesQueuedDeliveryAsync()
    {
        // Arrange
        var clock = new ManualTimeProvider();
        await using var dispatcher = Dispatcher.Start(timeProvider: clock);
        var ticks = 0;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using ManualResetEventSlim release = new();
        var timer = await dispatcher.InvokeAsync(
            () =>
            {
                var value = new DispatcherTimer(dispatcher, TimeSpan.FromMilliseconds(200));
                value.Tick += (_, _) => ticks++;
                value.Start();
                return value;
            },
            TestContext.Current.CancellationToken);
        dispatcher.Post(() =>
        {
            _ = entered.TrySetResult();
            release.Wait();
            timer.Stop();
        });
        await entered.Task.WaitAsync(TestContext.Current.CancellationToken);

        // Act
        clock.Advance(TimeSpan.FromMilliseconds(200));
        release.Set();
        await dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);

        // Assert
        ticks.ShouldBe(0);
        timer.IsRunning.ShouldBeFalse();
        timer.Dispose();
    }

    /// <summary>Verifies replacing the interval restarts one complete cadence.</summary>
    [Fact]
    public async Task Interval_WhenChangedWhileRunning_RestartsCompleteCadenceAsync()
    {
        // Arrange
        var clock = new ManualTimeProvider();
        await using var dispatcher = Dispatcher.Start(timeProvider: clock);
        var ticks = 0;
        var timer = await dispatcher.InvokeAsync(
            () =>
            {
                var value = new DispatcherTimer(dispatcher, TimeSpan.FromMilliseconds(200));
                value.Tick += (_, _) => ticks++;
                value.Start();
                return value;
            },
            TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromMilliseconds(100));

        // Act
        await dispatcher.InvokeAsync(
            () => { timer.Interval = TimeSpan.FromMilliseconds(300); },
            TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromMilliseconds(299));
        await dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);

        // Assert
        ticks.ShouldBe(0);
        clock.Advance(TimeSpan.FromMilliseconds(1));
        await dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);
        ticks.ShouldBe(1);
        timer.Interval.ShouldBe(TimeSpan.FromMilliseconds(300));
        timer.Dispose();
    }

    /// <summary>Verifies elapsed periods coalesce while the dispatcher cannot drain them.</summary>
    [Fact]
    public async Task Advance_WhenDispatcherIsBlocked_CoalescesToOnePendingTickAsync()
    {
        // Arrange
        var clock = new ManualTimeProvider();
        await using var dispatcher = Dispatcher.Start(timeProvider: clock);
        var ticks = 0;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using ManualResetEventSlim release = new();
        var timer = await dispatcher.InvokeAsync(
            () =>
            {
                var value = new DispatcherTimer(dispatcher, TimeSpan.FromMilliseconds(200));
                value.Tick += (_, _) => ticks++;
                value.Start();
                return value;
            },
            TestContext.Current.CancellationToken);
        dispatcher.Post(() =>
        {
            _ = entered.TrySetResult();
            release.Wait();
        });
        await entered.Task.WaitAsync(TestContext.Current.CancellationToken);

        // Act
        clock.Advance(TimeSpan.FromSeconds(1));
        release.Set();
        await dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);

        // Assert
        ticks.ShouldBe(1);
        timer.Dispose();
    }

    /// <summary>Verifies disposal is idempotent and suppresses future provider signals.</summary>
    [Fact]
    public async Task Dispose_WhenCalledRepeatedly_SuppressesFutureTicksAsync()
    {
        // Arrange
        var clock = new ManualTimeProvider();
        await using var dispatcher = Dispatcher.Start(timeProvider: clock);
        var ticks = 0;
        var timer = await dispatcher.InvokeAsync(
            () =>
            {
                var value = new DispatcherTimer(dispatcher, TimeSpan.FromMilliseconds(200));
                value.Tick += (_, _) => ticks++;
                value.Start();
                return value;
            },
            TestContext.Current.CancellationToken);

        // Act
        timer.Dispose();
        timer.Dispose();
        clock.Advance(TimeSpan.FromSeconds(1));
        await dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);

        // Assert
        timer.IsRunning.ShouldBeFalse();
        ticks.ShouldBe(0);
    }

    /// <summary>Verifies a late clock signal is harmless after dispatcher shutdown begins.</summary>
    [Fact]
    public async Task Advance_WhenDispatcherIsDisposed_DoesNotLeakCallbackFailureAsync()
    {
        // Arrange
        var clock = new ManualTimeProvider();
        var dispatcher = Dispatcher.Start(timeProvider: clock);
        var timer = await dispatcher.InvokeAsync(
            () =>
            {
                var value = new DispatcherTimer(dispatcher, TimeSpan.FromMilliseconds(200));
                value.Start();
                return value;
            },
            TestContext.Current.CancellationToken);
        await dispatcher.DisposeAsync();

        // Act and assert
        Should.NotThrow(() => clock.Advance(TimeSpan.FromMilliseconds(200)));
        timer.Dispose();
    }

    /// <summary>Verifies tick handler failures use ordinary dispatcher reporting.</summary>
    [Fact]
    public async Task Tick_WhenHandlerThrows_UsesDispatcherUnhandledPolicyAsync()
    {
        // Arrange
        var clock = new ManualTimeProvider();
        await using var dispatcher = Dispatcher.Start(timeProvider: clock);
        var failure = new InvalidOperationException("tick");
        var observed = new TaskCompletionSource<Exception>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        dispatcher.UnhandledException += (_, eventArgs) =>
        {
            eventArgs.Handled = true;
            _ = observed.TrySetResult(eventArgs.Exception);
        };
        var timer = await dispatcher.InvokeAsync(
            () =>
            {
                var value = new DispatcherTimer(dispatcher, TimeSpan.FromMilliseconds(200));
                value.Tick += (_, _) => throw failure;
                value.Start();
                return value;
            },
            TestContext.Current.CancellationToken);

        // Act
        clock.Advance(TimeSpan.FromMilliseconds(200));

        // Assert
        (await observed.Task.WaitAsync(TestContext.Current.CancellationToken)).ShouldBeSameAs(failure);
        timer.Dispose();
    }
}
