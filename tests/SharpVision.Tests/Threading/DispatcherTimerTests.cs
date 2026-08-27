// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Threading;

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

    /// <summary>Verifies a pre-restart clock callback that only arrives after the live generation
    /// has already advanced is dropped instead of being delivered as if it belonged to the new
    /// schedule.</summary>
    [Fact]
    public async Task Interval_WhenStaleCallbackArrivesAfterRestart_DoesNotRaiseTickAsync()
    {
        // Arrange
        var clock = new ManualTimeProvider();
        await using var dispatcher = Dispatcher.Start(timeProvider: clock);
        var ticks = 0;
        var (timer, staleGeneration) = await dispatcher.InvokeAsync(
            () =>
            {
                var value = new DispatcherTimer(dispatcher, TimeSpan.FromMilliseconds(200));
                value.Tick += (_, _) => ticks++;
                value.Start();
                var generation = value.Generation;

                // Model an in-flight callback for this (now stale) generation that has already
                // fired against the underlying clock but has not yet reached DispatcherTimer's
                // internal gate. The restart below bumps the live generation out from under it
                // before the simulated callback below is delivered.
                value.Interval = TimeSpan.FromMilliseconds(300);
                return (value, generation);
            },
            TestContext.Current.CancellationToken);

        // Act
        timer.SimulateElapsedForGeneration(staleGeneration);
        await dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);

        // Assert
        ticks.ShouldBe(0);
        timer.Generation.ShouldNotBe(staleGeneration);
        timer.Dispose();
    }

    /// <summary>Verifies a new generation's first elapsed signal still produces a tick even when
    /// the previous generation's own <c>Deliver</c> callback is still queued on the dispatcher
    /// when the <see cref="DispatcherTimer.Interval"/> setter arms that new generation; the
    /// pending latch must be reset at the arm point, not only once the stale delivery drains.</summary>
    [Fact]
    public async Task Interval_WhenNewGenerationElapsesBeforeStaleDeliveryDrains_DoesNotDropFirstTickAsync()
    {
        // Arrange
        var clock = new ManualTimeProvider();
        await using var dispatcher = Dispatcher.Start(timeProvider: clock);
        var ticks = 0;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using ManualResetEventSlim release = new();
        var (timer, gen0) = await dispatcher.InvokeAsync(
            () =>
            {
                var value = new DispatcherTimer(dispatcher, TimeSpan.FromMilliseconds(200));
                value.Tick += (_, _) => ticks++;
                value.Start();
                return (value, value.Generation);
            },
            TestContext.Current.CancellationToken);
        dispatcher.Post(() =>
        {
            _ = entered.TrySetResult();
            release.Wait();
        });
        await entered.Task.WaitAsync(TestContext.Current.CancellationToken);

        // Queue the interval change first so it drains before the stale Deliver queued next.
        dispatcher.Post(() =>
        {
            timer.Interval = TimeSpan.FromMilliseconds(50);

            // Models the new generation's own clock elapsing immediately, before the stale
            // Deliver(gen0) queued behind this very callback has drained.
            timer.SimulateElapsedForGeneration(timer.Generation);
        });

        // Act

        // Models the old generation's clock firing while the dispatcher is still busy: latches
        // `_pending` and queues Deliver(gen0) behind the interval-change callback above.
        timer.SimulateElapsedForGeneration(gen0);
        release.Set();
        await dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);

        // Assert
        ticks.ShouldBe(1);
        timer.Dispose();
    }

    /// <summary>Verifies this is specific to the race, not a general property of interval
    /// changes: with no racing stale <c>Deliver</c>, a single elapsed signal after an interval
    /// change produces exactly one tick immediately.</summary>
    [Fact]
    public async Task Interval_WhenChangedWithNoRace_OneElapsedSignalProducesOneTickAsync()
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
        await dispatcher.InvokeAsync(
            () =>
            {
                timer.Interval = TimeSpan.FromMilliseconds(50);
                timer.SimulateElapsedForGeneration(timer.Generation);
            },
            TestContext.Current.CancellationToken);
        await dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);

        // Assert
        ticks.ShouldBe(1);
        timer.Dispose();
    }

    /// <summary>Verifies the same drop cannot occur across <see cref="DispatcherTimer.Stop"/>
    /// immediately followed by <see cref="DispatcherTimer.Start"/>: a still-queued stale
    /// <c>Deliver</c> from the generation that was running before <c>Stop</c> must not survive
    /// to drop the freshly restarted generation's first elapsed signal.</summary>
    [Fact]
    public async Task Start_WhenNewGenerationElapsesAfterStopBeforeStaleDeliveryDrains_DoesNotDropFirstTickAsync()
    {
        // Arrange
        var clock = new ManualTimeProvider();
        await using var dispatcher = Dispatcher.Start(timeProvider: clock);
        var ticks = 0;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using ManualResetEventSlim release = new();
        var (timer, gen0) = await dispatcher.InvokeAsync(
            () =>
            {
                var value = new DispatcherTimer(dispatcher, TimeSpan.FromMilliseconds(200));
                value.Tick += (_, _) => ticks++;
                value.Start();
                return (value, value.Generation);
            },
            TestContext.Current.CancellationToken);
        dispatcher.Post(() =>
        {
            _ = entered.TrySetResult();
            release.Wait();
        });
        await entered.Task.WaitAsync(TestContext.Current.CancellationToken);

        // Queue the stop/restart first so it drains before the stale Deliver queued next.
        dispatcher.Post(() =>
        {
            timer.Stop();
            timer.Start();

            // Models the freshly restarted generation's own clock elapsing immediately, before
            // the stale Deliver(gen0) queued behind this very callback has drained.
            timer.SimulateElapsedForGeneration(timer.Generation);
        });

        // Act

        // Models the original generation's clock firing while the dispatcher is still busy:
        // latches `_pending` and queues Deliver(gen0) behind the stop/restart callback above.
        timer.SimulateElapsedForGeneration(gen0);
        release.Set();
        await dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);

        // Assert
        ticks.ShouldBe(1);
        timer.Dispose();
    }

    /// <summary>Verifies this is specific to the race, not a general property of restarting:
    /// with no racing stale <c>Deliver</c>, a single elapsed signal after <c>Stop</c> immediately
    /// followed by <c>Start</c> produces exactly one tick immediately.</summary>
    [Fact]
    public async Task StopThenStart_WhenRestartedWithNoRace_OneElapsedSignalProducesOneTickAsync()
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
        await dispatcher.InvokeAsync(
            () =>
            {
                timer.Stop();
                timer.Start();
                timer.SimulateElapsedForGeneration(timer.Generation);
            },
            TestContext.Current.CancellationToken);
        await dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);

        // Assert
        ticks.ShouldBe(1);
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
            eventArgs.IsHandled = true;
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
