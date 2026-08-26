// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Runtime;

using System.Reflection;

using SharpVision.Runtime;

/// <summary>
/// Verifies the six <c>Application</c> call sites that guard a <c>Dispatcher.Post</c> attempt -
/// <see cref="Application.Profile"/>, the resize sink, <c>Enqueue</c> (reached through
/// <see cref="Application.Input(in Stroke)"/>), <c>PostOutOfBand</c>, <c>DrainInput</c>'s own
/// repost, and <c>WakeInput</c> - now narrow their guard to <see cref="ObjectDisposedException"/>
/// only. A saturated (but otherwise healthy) dispatcher queue raises
/// <see cref="InvalidOperationException"/>, a distinct and recoverable-by-the-framework condition
/// that must surface rather than be silently swallowed alongside genuine shutdown. Each site is
/// paired with a proof that the original <see cref="ObjectDisposedException"/> no-op is unchanged.
/// </summary>
public sealed partial class ApplicationTests
{
    /// <summary>Blocks the dispatcher thread inside one posted callback, then fills the queue to
    /// capacity so every subsequent <see cref="Dispatcher.Post(Action)"/> throws
    /// <see cref="InvalidOperationException"/> until the returned handle is released.</summary>
    private static async Task<ManualResetEventSlim> SaturateQueueAsync(
        Dispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new ManualResetEventSlim();

        dispatcher.Post(() =>
        {
            entered.SetResult();
            release.Wait();
        });

        await entered.Task.WaitAsync(cancellationToken);

        while (true)
        {
            try
            {
                dispatcher.Post(static () => { });
            }
            catch (InvalidOperationException)
            {
                break;
            }
        }

        return release;
    }

    /// <summary>Waits until <see cref="Dispatcher.Post(Action)"/> stops throwing
    /// <see cref="InvalidOperationException"/>, i.e. the backlog <see cref="SaturateQueueAsync"/>
    /// queued behind its blocking action has actually finished draining - releasing the blocking
    /// action only unblocks the dispatcher thread, it does not make the backlog vanish
    /// instantly, and calling <see cref="Application.DisposeAsync"/> before it drains can itself
    /// observe the same "queue is full" condition on its own shutdown post.</summary>
    private static async Task WaitForQueueToDrainAsync(Dispatcher dispatcher, CancellationToken cancellationToken)
    {
        while (true)
        {
            try
            {
                dispatcher.Post(static () => { });
                return;
            }
            catch (InvalidOperationException)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(5), cancellationToken);
            }
        }
    }

    private static Stroke PlainStroke(Code code) =>
        new(code, character: null, nativeCode: 0, Modifiers.None, KeyAction.Press);

    /// <summary>Verifies the profile-wake site propagates a full-queue failure instead of
    /// swallowing it.</summary>
    [Fact]
    public async Task Profile_WhenDispatcherQueueIsFull_PropagatesInvalidOperationExceptionAsync()
    {
        await using FakeTerminal terminal = new();
        var application = new Application(new ProbeControl(), terminal, terminal, TerminalOptions.Minimal);
        var release = await SaturateQueueAsync(application.Dispatcher, TestContext.Current.CancellationToken);

        try
        {
            _ = Should.Throw<InvalidOperationException>(
                () => application.Profile(TerminalCapabilities.Conservative));
        }
        finally
        {
            release.Set();
        }

        await WaitForQueueToDrainAsync(application.Dispatcher, TestContext.Current.CancellationToken);
        await application.DisposeAsync();
    }

    /// <summary>Verifies the profile-wake site still no-ops silently once the dispatcher is
    /// disposed, exactly as before.</summary>
    [Fact]
    public async Task Profile_WhenDispatcherIsDisposed_DoesNotThrowAsync()
    {
        await using FakeTerminal terminal = new();
        var application = new Application(new ProbeControl(), terminal, terminal, TerminalOptions.Minimal);
        await application.Dispatcher.DisposeAsync();

        Should.NotThrow(() => application.Profile(TerminalCapabilities.Conservative));

        // The Dispatcher was disposed directly, bypassing Application's own shutdown sequence, so
        // Application.DisposeAsync's cleanup path would try to marshal onto an already-stopped
        // dispatcher and throw ObjectDisposedException itself - intentionally left undisposed.
    }

    /// <summary>Verifies the resize-wake site propagates a full-queue failure instead of
    /// swallowing it.</summary>
    [Fact]
    public async Task Resize_WhenDispatcherQueueIsFull_PropagatesInvalidOperationExceptionAsync()
    {
        await using FakeTerminal terminal = new();
        var application = new Application(new ProbeControl(), terminal, terminal, TerminalOptions.Minimal);
        var release = await SaturateQueueAsync(application.Dispatcher, TestContext.Current.CancellationToken);
        var dimensions = new Dimensions(new Size(10, 4));

        try
        {
            _ = Should.Throw<InvalidOperationException>(
                () => ((ISink) application).Resize(in dimensions));
        }
        finally
        {
            release.Set();
        }

        await WaitForQueueToDrainAsync(application.Dispatcher, TestContext.Current.CancellationToken);
        await application.DisposeAsync();
    }

    /// <summary>Verifies the resize-wake site still no-ops silently once the dispatcher is
    /// disposed, exactly as before.</summary>
    [Fact]
    public async Task Resize_WhenDispatcherIsDisposed_DoesNotThrowAsync()
    {
        await using FakeTerminal terminal = new();
        var application = new Application(new ProbeControl(), terminal, terminal, TerminalOptions.Minimal);
        await application.Dispatcher.DisposeAsync();
        var dimensions = new Dimensions(new Size(10, 4));

        Should.NotThrow(() => ((ISink) application).Resize(in dimensions));
    }

    /// <summary>Verifies <c>Enqueue</c>, reached through <see cref="Application.Input(in Stroke)"/>,
    /// propagates a full-queue failure instead of swallowing it.</summary>
    [Fact]
    public async Task Input_WhenDispatcherQueueIsFull_PropagatesInvalidOperationExceptionAsync()
    {
        await using FakeTerminal terminal = new();
        var application = new Application(new ProbeControl(), terminal, terminal, TerminalOptions.Minimal);
        var release = await SaturateQueueAsync(application.Dispatcher, TestContext.Current.CancellationToken);
        var stroke = PlainStroke(Code.Enter);

        try
        {
            _ = Should.Throw<InvalidOperationException>(() => application.Input(in stroke));
        }
        finally
        {
            release.Set();
        }

        await WaitForQueueToDrainAsync(application.Dispatcher, TestContext.Current.CancellationToken);
        await application.DisposeAsync();
    }

    /// <summary>Verifies <c>Enqueue</c> still no-ops silently once the dispatcher is disposed,
    /// exactly as before.</summary>
    [Fact]
    public async Task Input_WhenDispatcherIsDisposed_DoesNotThrowAsync()
    {
        await using FakeTerminal terminal = new();
        var application = new Application(new ProbeControl(), terminal, terminal, TerminalOptions.Minimal);
        await application.Dispatcher.DisposeAsync();
        var stroke = PlainStroke(Code.Enter);

        Should.NotThrow(() => application.Input(in stroke));
    }

    /// <summary>Verifies <c>PostOutOfBand</c> propagates a full-queue failure instead of swallowing
    /// it.</summary>
    [Fact]
    public async Task PostOutOfBand_WhenDispatcherQueueIsFull_PropagatesInvalidOperationExceptionAsync()
    {
        await using FakeTerminal terminal = new();
        var application = new Application(new ProbeControl(), terminal, terminal, TerminalOptions.Minimal);
        var release = await SaturateQueueAsync(application.Dispatcher, TestContext.Current.CancellationToken);
        ReadOnlyMemory<byte> bytes = new byte[] { 1, 2, 3 };

        try
        {
            _ = Should.Throw<InvalidOperationException>(() => application.PostOutOfBand(bytes));
        }
        finally
        {
            release.Set();
        }

        await WaitForQueueToDrainAsync(application.Dispatcher, TestContext.Current.CancellationToken);
        await application.DisposeAsync();
    }

    /// <summary>Verifies <c>PostOutOfBand</c> still no-ops silently once the dispatcher is disposed,
    /// exactly as before.</summary>
    [Fact]
    public async Task PostOutOfBand_WhenDispatcherIsDisposed_DoesNotThrowAsync()
    {
        await using FakeTerminal terminal = new();
        var application = new Application(new ProbeControl(), terminal, terminal, TerminalOptions.Minimal);
        await application.Dispatcher.DisposeAsync();
        ReadOnlyMemory<byte> bytes = new byte[] { 1, 2, 3 };

        Should.NotThrow(() => application.PostOutOfBand(bytes));
    }

    /// <summary>Verifies <c>DrainInput</c>'s own repost, reached only from inside the dispatcher's
    /// own dispatch loop, propagates a full-queue failure all the way to
    /// <see cref="Application.Failure"/> through the framework's existing
    /// <c>Dispatcher.UnhandledException</c> -&gt; <c>Application.Report</c> path, instead of
    /// silently stranding the wake flag.</summary>
    [Fact]
    public async Task DrainInput_WhenRepostFindsQueueFull_SetsApplicationFailureAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(10, 4)));
        await using Application application = new(
            new ProbeControl(),
            terminal,
            terminal,
            TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);

        var innerStroke = PlainStroke(Code.Enter);

        // Fires synchronously inside DrainInput, on the dispatcher thread, after the drain loop
        // observed the input queue empty but before the finally block resets _inputWake - the
        // exact "concurrent Enqueue inside the reset window" scenario this seam exists for. The
        // Input call below lands a record while _inputWake is still true, so Enqueue's own post
        // attempt is skipped and only the finally's own repost - the site under test - ever
        // touches a saturated dispatcher queue.
        application.DrainInputRaceHookForTests = () =>
        {
            application.Input(in innerStroke);

            while (true)
            {
                try
                {
                    application.Dispatcher.Post(static () => { });
                }
                catch (InvalidOperationException)
                {
                    break;
                }
            }
        };

        var failureObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        application.UnhandledException += (_, _) => failureObserved.TrySetResult();

        var outerStroke = PlainStroke(Code.Escape);
        application.Input(in outerStroke);

        await failureObserved.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        _ = application.Failure.ShouldBeOfType<InvalidOperationException>();
    }

    /// <summary>Verifies <c>DrainInput</c>'s own repost still no-ops silently when the dispatcher is
    /// disposed mid-flight, exactly as before.</summary>
    [Fact]
    public async Task DrainInput_WhenDispatcherIsDisposedDuringRepost_StaysSwallowedAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(10, 4)));
        var application = new Application(new ProbeControl(), terminal, terminal, TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);

        var innerStroke = PlainStroke(Code.Enter);
        var disposalStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        application.DrainInputRaceHookForTests = () =>
        {
            application.Input(in innerStroke);

            // Runs on the dispatcher's own thread, so the synchronous portion that flips the
            // dispatcher's internal stopping flag completes before this returns.
            _ = application.Dispatcher.DisposeAsync().AsTask();
            disposalStarted.SetResult();
        };

        var outerStroke = PlainStroke(Code.Escape);
        application.Input(in outerStroke);

        await disposalStarted.Task.WaitAsync(TestContext.Current.CancellationToken);

        // Off-thread now, and _stopping is already true, so this awaits the dispatcher thread's
        // own exit instead of racing to be the one that requests it.
        await application.Dispatcher.DisposeAsync();

        application.Failure.ShouldBeNull();

        // Intentionally left undisposed - see the comment on Profile_WhenDispatcherIsDisposed.
    }

    /// <summary>Verifies <c>WakeInput</c> - reachable only from inside the first
    /// <c>DrainResize</c>, when input arrived and was drained before the tree ever attached -
    /// propagates a full-queue failure instead of swallowing it. Reflection drives the private
    /// method directly with the exact precondition it checks
    /// (<c>_input.Count &gt; 0 &amp;&amp; !_inputWake</c>), since no test seam exists to reach it
    /// through the real first-resize race without introducing a new one.</summary>
    [Fact]
    public async Task WakeInput_WhenDispatcherQueueIsFull_PropagatesInvalidOperationExceptionAsync()
    {
        await using FakeTerminal terminal = new();
        var application = new Application(new ProbeControl(), terminal, terminal, TerminalOptions.Minimal);
        var release = await SaturateQueueAsync(application.Dispatcher, TestContext.Current.CancellationToken);

        try
        {
            var stroke = PlainStroke(Code.Enter);

            // Enqueue's own post attempt also observes the saturated queue and throws here - an
            // incidental exercise of the already-covered Input site - but the record still lands
            // in _input and _inputWake is still set true before that happens.
            _ = Should.Throw<InvalidOperationException>(() => application.Input(in stroke));

            typeof(Application)
                .GetField("_inputWake", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(application, false);

            var wakeInput = typeof(Application)
                .GetMethod("WakeInput", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var thrown = Should.Throw<TargetInvocationException>(() => wakeInput.Invoke(application, null));

            _ = thrown.InnerException.ShouldBeOfType<InvalidOperationException>();
        }
        finally
        {
            release.Set();
        }

        await WaitForQueueToDrainAsync(application.Dispatcher, TestContext.Current.CancellationToken);
        await application.DisposeAsync();
    }

    /// <summary>Verifies <c>WakeInput</c> still no-ops silently once the dispatcher is disposed,
    /// exactly as before.</summary>
    [Fact]
    public async Task WakeInput_WhenDispatcherIsDisposed_DoesNotThrowAsync()
    {
        await using FakeTerminal terminal = new();
        var application = new Application(new ProbeControl(), terminal, terminal, TerminalOptions.Minimal);
        await application.Dispatcher.DisposeAsync();
        var stroke = PlainStroke(Code.Enter);

        Should.NotThrow(() => application.Input(in stroke));

        typeof(Application)
            .GetField("_inputWake", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(application, false);

        var wakeInput = typeof(Application)
            .GetMethod("WakeInput", BindingFlags.Instance | BindingFlags.NonPublic)!;

        _ = Should.NotThrow(() => wakeInput.Invoke(application, null));
    }

    /// <summary>
    /// Verifies <c>DisposeAsync</c>'s own terminal-resource cleanup step no longer lets a merely
    /// transient full dispatcher queue escape as <see cref="InvalidOperationException"/>. The queue
    /// is still saturated - not yet drained - when <c>DisposeAsync</c> is invoked, the exact race
    /// <see cref="WaitForQueueToDrainAsync"/>'s own remarks describe; releasing the block shortly
    /// after gives the bounded retry (see <c>Application.InvokeWithQueueRetryAsync</c>) room to
    /// converge well inside the default <see cref="TerminalOptions.CleanupTimeout"/>, so
    /// <c>TerminalServices.Dispose</c> still actually runs instead of the clipboard-timer teardown
    /// being silently skipped.
    /// </summary>
    [Fact]
    public async Task DisposeAsync_WhenDispatcherQueueIsTransientlyFull_DisposesTerminalServicesWithoutThrowingAsync()
    {
        await using FakeTerminal terminal = new();
        var application = new Application(new ProbeControl(), terminal, terminal, TerminalOptions.Minimal);
        var release = await SaturateQueueAsync(application.Dispatcher, TestContext.Current.CancellationToken);

        var disposeTask = application.DisposeAsync().AsTask();

        // Deliberately does not drain the backlog first: DisposeAsync must observe the queue still
        // full at the moment it posts, then release into an already-in-flight retry loop rather
        // than a fresh one.
        await Task.Delay(TimeSpan.FromMilliseconds(20), TestContext.Current.CancellationToken);
        release.Set();

        await disposeTask.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        ((TerminalServices) application.Terminal).DisposedOnDispatcherThreadForTests.ShouldBeTrue();
    }

    /// <summary>
    /// Verifies the bounded retry gives up gracefully - folding its failure into
    /// <see cref="Application.LastCleanupException"/> - instead of hanging forever when the
    /// dispatcher queue never drains for the whole <see cref="TerminalOptions.CleanupTimeout"/>
    /// window. Drives <c>DisposeTerminalResourcesAsync</c> directly through reflection (as
    /// <see cref="WakeInput_WhenDispatcherQueueIsFull_PropagatesInvalidOperationExceptionAsync"/>
    /// does for its own private target) so the assertion is isolated to the retry loop's own
    /// give-up behavior, without also depending on <c>FinishWithoutSessionAsync</c>'s later
    /// dispatcher post - which needs the same permanently-saturated queue to drain to make any
    /// progress at all - succeeding within the test.
    /// </summary>
    [Fact]
    public async Task DisposeTerminalResourcesAsync_WhenQueueNeverDrainsWithinCleanupTimeout_GivesUpAndRecordsFailureAsync()
    {
        await using FakeTerminal terminal = new();
        var options = TerminalOptions.Minimal with { CleanupTimeout = TimeSpan.FromMilliseconds(50) };
        var application = new Application(new ProbeControl(), terminal, terminal, options);
        var release = await SaturateQueueAsync(application.Dispatcher, TestContext.Current.CancellationToken);

        try
        {
            var disposeTerminalResources = typeof(Application).GetMethod(
                "DisposeTerminalResourcesAsync",
                BindingFlags.Instance | BindingFlags.NonPublic)!;

            var task = (Task<Exception?>) disposeTerminalResources.Invoke(application, null)!;
            var failure = await task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            _ = failure.ShouldBeOfType<InvalidOperationException>();
            application.LastCleanupException.ShouldBeSameAs(failure);
        }
        finally
        {
            release.Set();
        }

        await WaitForQueueToDrainAsync(application.Dispatcher, TestContext.Current.CancellationToken);
        await application.DisposeAsync();
    }

    /// <summary>
    /// Verifies the bounded retry loop does not mistake a disposed dispatcher for a merely-full
    /// queue: <see cref="ObjectDisposedException"/> derives from <see cref="InvalidOperationException"/>,
    /// so a catch clause without the guard <c>Application.InvokeWithQueueRetryAsync</c> documents
    /// would retry it for the whole <see cref="TerminalOptions.CleanupTimeout"/> window instead of
    /// propagating it immediately as promised. Uses a long timeout and a stopwatch so a regression
    /// (retrying instead of propagating) would make this test visibly slow rather than merely wrong.
    /// </summary>
    [Fact]
    public async Task InvokeWithQueueRetryAsync_WhenDispatcherIsDisposed_PropagatesImmediatelyAsync()
    {
        await using FakeTerminal terminal = new();
        var options = TerminalOptions.Minimal with { CleanupTimeout = TimeSpan.FromSeconds(30) };
        var application = new Application(new ProbeControl(), terminal, terminal, options);
        await application.Dispatcher.DisposeAsync();

        var invokeWithQueueRetryAsync = typeof(Application).GetMethod(
            "InvokeWithQueueRetryAsync",
            BindingFlags.Instance | BindingFlags.NonPublic)!;

        Action noop = () => { };
        var stopwatch = Stopwatch.StartNew();
        var task = (Task<Exception?>) invokeWithQueueRetryAsync.Invoke(application, [noop])!;

        _ = await Should.ThrowAsync<ObjectDisposedException>(
            () => task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));

        stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(1));
    }
}
