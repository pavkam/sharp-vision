// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Display;

/// <summary>Verifies AnimationTimer lifecycle, interval propagation, and playback state.</summary>
public sealed class AnimationTimerTests
{
    /// <summary>Verifies documented construction defaults.</summary>
    [Fact]
    public void Constructor_WhenCreated_StoresIntervalAndDoesNotPlay()
    {
        // Arrange and act
        var timer = new AnimationTimer(TimeSpan.FromMilliseconds(150), static () => { });

        // Assert
        timer.Interval.ShouldBe(TimeSpan.FromMilliseconds(150));
        timer.IsPlaying.ShouldBeFalse();
    }

    /// <summary>Verifies Attach creates and starts the timer when IsPlaying is true.</summary>
    [Fact]
    public async Task Attach_WhenIsPlaying_StartsTimerAsync()
    {
        // Arrange
        var clock = new ManualTimeProvider();
        await using var dispatcher = Dispatcher.Start(timeProvider: clock);
        var ticks = 0;
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var timer = new AnimationTimer(TimeSpan.FromMilliseconds(200), () =>
        {
            ticks++;
            _ = completed.TrySetResult();
        })
        {
            IsPlaying = true
        };

        // Act
        await dispatcher.InvokeAsync(
            () => timer.OnOwnerAttached(dispatcher),
            TestContext.Current.CancellationToken);

        clock.Advance(TimeSpan.FromMilliseconds(200));
        await completed.Task.WaitAsync(TestContext.Current.CancellationToken);

        // Assert
        ticks.ShouldBe(1);
        timer.Dispose();
    }

    /// <summary>Verifies Attach does not start the timer when IsPlaying is false.</summary>
    [Fact]
    public async Task Attach_WhenNotPlaying_DoesNotStartTimerAsync()
    {
        // Arrange
        var clock = new ManualTimeProvider();
        await using var dispatcher = Dispatcher.Start(timeProvider: clock);
        var ticks = 0;
        var timer = new AnimationTimer(TimeSpan.FromMilliseconds(100), () => ticks++);
        await dispatcher.InvokeAsync(
            () => timer.OnOwnerAttached(dispatcher),
            TestContext.Current.CancellationToken);

        // Act
        clock.Advance(TimeSpan.FromMilliseconds(300));
        await dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);

        // Assert
        ticks.ShouldBe(0);
        timer.Dispose();
    }

    /// <summary>Verifies Detach releases the timer and suppresses ticks.</summary>
    [Fact]
    public async Task Detach_WhenAttached_ReleasesTimerAndStopsTicksAsync()
    {
        // Arrange
        var clock = new ManualTimeProvider();
        await using var dispatcher = Dispatcher.Start(timeProvider: clock);
        var ticks = 0;
        var timer = new AnimationTimer(TimeSpan.FromMilliseconds(100), () => ticks++) { IsPlaying = true };
        await dispatcher.InvokeAsync(
            () => timer.OnOwnerAttached(dispatcher),
            TestContext.Current.CancellationToken);

        // Act
        await dispatcher.InvokeAsync(
            timer.OnOwnerDetached,
            TestContext.Current.CancellationToken);

        clock.Advance(TimeSpan.FromMilliseconds(300));
        await dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);

        // Assert
        ticks.ShouldBe(0);
    }

    /// <summary>Verifies Dispose releases the timer.</summary>
    [Fact]
    public async Task Dispose_WhenAttached_ReleasesTimerAsync()
    {
        // Arrange
        var clock = new ManualTimeProvider();
        await using var dispatcher = Dispatcher.Start(timeProvider: clock);
        var ticks = 0;
        var timer = new AnimationTimer(TimeSpan.FromMilliseconds(100), () => ticks++) { IsPlaying = true };
        await dispatcher.InvokeAsync(
            () => timer.OnOwnerAttached(dispatcher),
            TestContext.Current.CancellationToken);

        // Act
        await dispatcher.InvokeAsync(
            timer.Dispose,
            TestContext.Current.CancellationToken);

        clock.Advance(TimeSpan.FromMilliseconds(300));
        await dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);

        // Assert
        ticks.ShouldBe(0);
    }

    /// <summary>Verifies IsPlaying starts the timer after attach.</summary>
    [Fact]
    public async Task IsPlaying_WhenSetTrueAfterAttach_StartsTimerAsync()
    {
        // Arrange
        var clock = new ManualTimeProvider();
        await using var dispatcher = Dispatcher.Start(timeProvider: clock);
        var ticks = 0;
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var timer = new AnimationTimer(TimeSpan.FromMilliseconds(100), () =>
        {
            ticks++;
            _ = completed.TrySetResult();
        });

        await dispatcher.InvokeAsync(
            () => timer.OnOwnerAttached(dispatcher),
            TestContext.Current.CancellationToken);

        // Act
        await dispatcher.InvokeAsync(
            () => { timer.IsPlaying = true; },
            TestContext.Current.CancellationToken);

        clock.Advance(TimeSpan.FromMilliseconds(100));
        await completed.Task.WaitAsync(TestContext.Current.CancellationToken);

        // Assert
        ticks.ShouldBe(1);
        timer.Dispose();
    }

    /// <summary>Verifies IsPlaying stops the timer.</summary>
    [Fact]
    public async Task IsPlaying_WhenSetFalseAfterAttach_StopsTimerAsync()
    {
        // Arrange
        var clock = new ManualTimeProvider();
        await using var dispatcher = Dispatcher.Start(timeProvider: clock);
        var ticks = 0;
        var timer = new AnimationTimer(TimeSpan.FromMilliseconds(100), () => ticks++) { IsPlaying = true };
        await dispatcher.InvokeAsync(
            () => timer.OnOwnerAttached(dispatcher),
            TestContext.Current.CancellationToken);

        // Act
        await dispatcher.InvokeAsync(
            () => { timer.IsPlaying = false; },
            TestContext.Current.CancellationToken);

        clock.Advance(TimeSpan.FromMilliseconds(300));
        await dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);

        // Assert
        ticks.ShouldBe(0);
        timer.Dispose();
    }

    /// <summary>Verifies Interval propagates to the underlying timer after attach.</summary>
    [Fact]
    public async Task Interval_WhenChangedAfterAttach_PropagatesToTimerAsync()
    {
        // Arrange
        var clock = new ManualTimeProvider();
        await using var dispatcher = Dispatcher.Start(timeProvider: clock);
        var ticks = 0;
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var timer = new AnimationTimer(TimeSpan.FromMilliseconds(500), () =>
        {
            ticks++;
            _ = completed.TrySetResult();
        })
        {
            IsPlaying = true
        };

        await dispatcher.InvokeAsync(
            () => timer.OnOwnerAttached(dispatcher),
            TestContext.Current.CancellationToken);

        // Act -- shorten interval to 50ms, then advance only 50ms
        await dispatcher.InvokeAsync(
            () => { timer.Interval = TimeSpan.FromMilliseconds(50); },
            TestContext.Current.CancellationToken);

        clock.Advance(TimeSpan.FromMilliseconds(50));
        await completed.Task.WaitAsync(TestContext.Current.CancellationToken);

        // Assert
        ticks.ShouldBe(1);
        timer.Interval.ShouldBe(TimeSpan.FromMilliseconds(50));
        timer.Dispose();
    }

    /// <summary>Verifies Interval stores value before attach.</summary>
    [Fact]
    public void Interval_WhenChangedBeforeAttach_StoresValue()
    {
        // Arrange
        var timer = new AnimationTimer(TimeSpan.FromMilliseconds(200), static () => { })
        {
            // Act
            Interval = TimeSpan.FromMilliseconds(300)
        };

        // Assert
        timer.Interval.ShouldBe(TimeSpan.FromMilliseconds(300));
    }

    /// <summary>Verifies EnsureRunning is a silent no-op before the timer is ever attached, since
    /// there is no underlying dispatcher timer instance yet to start.</summary>
    [Fact]
    public void EnsureRunning_WhenNeverAttached_DoesNotThrow()
    {
        // Arrange
        var timer = new AnimationTimer(TimeSpan.FromMilliseconds(100), static () => { }) { IsPlaying = true };

        // Act and assert
        Should.NotThrow(timer.EnsureRunning);
    }

    /// <summary>Verifies EnsureRunning restarts a timer that stopped itself through the
    /// <c>shouldTick</c> callback (for example, a control that became invisible) even though
    /// <see cref="AnimationTimer.IsPlaying"/> was never toggled - exactly the seam
    /// <see cref="Spinner"/> and <see cref="ChaseIndicator"/> rely on inside their own
    /// <c>OnRenderContent</c> to resume playback after visibility recovers.</summary>
    [Fact]
    public async Task EnsureRunning_WhenShouldTickStoppedTheTimer_RestartsPlaybackAsync()
    {
        // Arrange
        var clock = new ManualTimeProvider();
        await using var dispatcher = Dispatcher.Start(timeProvider: clock);
        var ticks = 0;
        var canTick = true;
        var timer = new AnimationTimer(TimeSpan.FromMilliseconds(100), () => ticks++, () => canTick)
        {
            IsPlaying = true
        };
        await dispatcher.InvokeAsync(
            () => timer.OnOwnerAttached(dispatcher),
            TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromMilliseconds(100));
        await dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);
        ticks.ShouldBe(1);

        // Act: the next tick observes shouldTick() == false and stops the underlying timer
        // internally, without changing the public IsPlaying flag.
        canTick = false;
        clock.Advance(TimeSpan.FromMilliseconds(100));
        await dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);
        ticks.ShouldBe(1);
        timer.IsPlaying.ShouldBeTrue();

        // Act: recovery restores shouldTick, then calls EnsureRunning the same way a render pass
        // would, without ever setting IsPlaying again.
        await dispatcher.InvokeAsync(
            () =>
            {
                canTick = true;
                timer.EnsureRunning();
            },
            TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromMilliseconds(100));
        await dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);

        // Assert
        ticks.ShouldBe(2);
        timer.Dispose();
    }

    /// <summary>Verifies EnsureRunning does not restart or otherwise disturb an already-running timer.</summary>
    [Fact]
    public async Task EnsureRunning_WhenAlreadyRunning_IsNoOpAsync()
    {
        // Arrange
        var clock = new ManualTimeProvider();
        await using var dispatcher = Dispatcher.Start(timeProvider: clock);
        var ticks = 0;
        var timer = new AnimationTimer(TimeSpan.FromMilliseconds(100), () => ticks++) { IsPlaying = true };
        await dispatcher.InvokeAsync(
            () => timer.OnOwnerAttached(dispatcher),
            TestContext.Current.CancellationToken);

        // Act
        await dispatcher.InvokeAsync(timer.EnsureRunning, TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromMilliseconds(100));
        await dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);

        // Assert
        ticks.ShouldBe(1);
        timer.Dispose();
    }
}
