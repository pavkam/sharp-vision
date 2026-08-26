// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Runtime;

using SharpVision.Runtime;

/// <summary>Verifies resize coalescing, input targeting, and application idleness.</summary>
public sealed partial class ApplicationTests
{
    /// <summary>Verifies a blocked dispatcher observes only the newest resize in a storm.</summary>
    [Fact]
    public async Task Resize_WhenSeveralArriveBeforeDrain_CoalescesNewestAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(10, 4)));
        await using Application application = new(
            new ProbeControl(),
            terminal,
            terminal,
            TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);
        using ManualResetEventSlim release = new();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        application.Dispatcher.Post(() =>
        {
            entered.SetResult();
            release.Wait();
        });
        await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
        List<Size> sizes = [];
        application.Resize += (_, eventArgs) => sizes.Add(eventArgs.Dimensions.Cells);

        // The dispatcher is blocked in the posted callback above, so coalescing happens in the
        // resize-reading loop rather than on the dispatcher; wait for all three queued resizes to
        // actually be dequeued before releasing the block, or the assertion below could observe
        // fewer than three still sitting in the channel.
        var resizesRead = 0;
        var allResizesRead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        terminal.ResizeRead += dimensions =>
        {
            _ = dimensions;

            if (Interlocked.Increment(ref resizesRead) == 3)
            {
                _ = allResizesRead.TrySetResult();
            }
        };

        terminal.QueueResize(new Dimensions(new Size(20, 5)));
        terminal.QueueResize(new Dimensions(new Size(30, 6)));
        terminal.QueueResize(new Dimensions(new Size(40, 7)));
        await allResizesRead.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        release.Set();
        await application.Dispatcher.InvokeAsync(
            static () => { },
            TestContext.Current.CancellationToken);

        sizes.ShouldBe([new Size(40, 7)]);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies key input routes to the manager's current focus target.</summary>
    [Fact]
    public async Task Input_WhenFocusExists_RoutesTypedKeyToFocusedControlAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(10, 4)));
        var root = new ProbeContainer();
        var child = new ProbeControl { IsFocusable = true };
        root.Children.Add(child);
        await using Application application = new(
            root,
            terminal,
            terminal,
            TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);
        List<RoutingPhase> phases = [];
        await application.Dispatcher.InvokeAsync(() =>
        {
            application.Focus.Focus(child).ShouldBeTrue();
            _ = child.AddHandler(Events.Key, (_, eventArgs) =>
                phases.Add(eventArgs.Phase));
        }, TestContext.Current.CancellationToken);
        var stroke = new Stroke(
            Code.Enter,
            character: null,
            nativeCode: 0,
            Modifiers.None,
            KeyAction.Press);

        application.Input(in stroke);
        await application.Dispatcher.InvokeAsync(
            static () => { },
            TestContext.Current.CancellationToken);

        phases.ShouldBe([RoutingPhase.Preview, RoutingPhase.Bubble]);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies input received before initial resize is retained until attachment.</summary>
    [Fact]
    public async Task Input_WhenReceivedBeforeResize_DeliversAfterTreeAttachmentAsync()
    {
        await using FakeTerminal terminal = new();
        var root = new ProbeContainer();
        var calls = 0;
        _ = root.AddHandler(Events.TerminalFocusChanged, (_, _) => calls++);
        await using Application application = new(
            root,
            terminal,
            terminal,
            TerminalOptions.Minimal);
        var focus = new TerminalFocus(gained: true);
        application.Input(in focus);
        terminal.QueueResize(new Dimensions(new Size(10, 4)));
        await application.StartAsync(TestContext.Current.CancellationToken);
        await application.Dispatcher.InvokeAsync(
            static () => { },
            TestContext.Current.CancellationToken);

        calls.ShouldBe(2);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies resize-handler invalidation is laid out before frame production.</summary>
    [Fact]
    public async Task Resize_WhenHandlerInvalidatesLayout_ReflowsBeforeFrameAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        var root = new ProbeControl();
        await using Application application = new(
            root,
            terminal,
            terminal,
            TerminalOptions.Minimal);
        application.Resize += (_, _) => root.Width = Length.Cells(5);
        root.Rendering = _ => root.Bounds.Width.ShouldBe(5);

        await application.StartAsync(TestContext.Current.CancellationToken);

        root.Bounds.Width.ShouldBe(5);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a terminal fault is primary and forces stopped completion.</summary>
    [Fact]
    public async Task Fault_WhenSessionReportsFailure_StopsWithOriginalExceptionAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(10, 4)));
        await using Application application = new(
            new ProbeControl(),
            terminal,
            terminal,
            TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);
        var failure = new IOException("terminal");

        application.Fault(failure);
        var thrown = await Should.ThrowAsync<IOException>(application.Completion);

        thrown.ShouldBeSameAs(failure);
        application.Failure.ShouldBeSameAs(failure);
    }

    /// <summary>Verifies idle-posted work drains before the next idle transition.</summary>
    [Fact]
    public async Task Idle_WhenHandlerPostsWork_DrainsBeforeSecondIdleAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(10, 4)));
        await using Application application = new(
            new ProbeControl(),
            terminal,
            terminal,
            TerminalOptions.Minimal);
        List<string> order = [];
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        application.Idle += (_, _) =>
        {
            order.Add("idle");

            if (order.Count == 1)
            {
                application.Dispatcher.Post(() => order.Add("work"));
            }
            else
            {
                completed.SetResult();
            }
        };

        await application.StartAsync(TestContext.Current.CancellationToken);
        await completed.Task.WaitAsync(TestContext.Current.CancellationToken);

        order.ShouldBe(["idle", "work", "idle"]);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a record enqueued while DrainInput's finally block is between resetting the
    /// wake latch and re-checking the queue is still delivered, instead of being stranded until some
    /// unrelated later Enqueue happens to re-arm the latch. The window is a handful of CPU
    /// instructions wide with no natural yield point, so <see cref="Application.DrainInputRaceHookForTests"/>
    /// pauses the dispatcher there deterministically.</summary>
    [Fact]
    public async Task Input_WhenEnqueueRacesDrainFinally_DeliversStrandedRecordAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(10, 4)));
        var root = new ProbeContainer();
        var child = new ProbeControl { IsFocusable = true };
        root.Children.Add(child);
        await using Application application = new(
            root,
            terminal,
            terminal,
            TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);
        List<Code> codes = [];
        var delivered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await application.Dispatcher.InvokeAsync(() =>
        {
            application.Focus.Focus(child).ShouldBeTrue();
            _ = child.AddHandler(Events.Key, (_, eventArgs) =>
            {
                if (eventArgs.Phase != RoutingPhase.Bubble)
                {
                    return;
                }

                codes.Add(eventArgs.Stroke.Code);

                if (codes.Count == 2)
                {
                    _ = delivered.TrySetResult();
                }
            });
        }, TestContext.Current.CancellationToken);
        var strokeA = new Stroke(Code.Enter, character: null, nativeCode: 0, Modifiers.None, KeyAction.Press);
        var strokeB = new Stroke(Code.Escape, character: null, nativeCode: 0, Modifiers.None, KeyAction.Press);
        var hookReached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using ManualResetEventSlim release = new();
        application.DrainInputRaceHookForTests = () =>
        {
            hookReached.SetResult();
            release.Wait();
        };

        // strokeA's Enqueue arms the latch and posts DrainInput, which dequeues strokeA, dispatches
        // it, observes the queue empty, releases the dequeue lock, and then parks in the hook above -
        // latch still true, finally's reset not yet run.
        application.Input(in strokeA);
        await hookReached.Task.WaitAsync(TestContext.Current.CancellationToken);

        // strokeB's Enqueue now races the parked drain: it observes _inputWake already true and
        // returns without posting a repost, exactly like a concurrent Enqueue landing in the old
        // two-lock reset window.
        await Task.Run(() => application.Input(in strokeB), TestContext.Current.CancellationToken);

        // Clear the hook before releasing the drain so the reposted DrainInput this triggers (once
        // fixed) does not re-enter a hook that already fired.
        application.DrainInputRaceHookForTests = null;
        release.Set();

        await delivered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        codes.ShouldBe([Code.Enter, Code.Escape]);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies DrainInput's finally-block repost tolerates a dispatcher disposal landing
    /// in the same handful-of-CPU-instructions window the stranded-record test above targets, so
    /// the resulting ObjectDisposedException from Dispatcher.Post is silently swallowed instead of
    /// surfacing through Dispatcher.Run's catch-all as a spurious Application.Failure/
    /// UnhandledException for what is really an ordinary shutdown race.</summary>
    [Fact]
    public async Task Input_WhenDisposeRacesDrainFinallyRepost_DoesNotReportFailureAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(10, 4)));
        var root = new ProbeContainer();
        var child = new ProbeControl { IsFocusable = true };
        root.Children.Add(child);
        var application = new Application(
            root,
            terminal,
            terminal,
            TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);

        var unhandledCount = 0;
        application.UnhandledException += (_, _) => Interlocked.Increment(ref unhandledCount);

        var strokeA = new Stroke(Code.Enter, character: null, nativeCode: 0, Modifiers.None, KeyAction.Press);
        var strokeB = new Stroke(Code.Escape, character: null, nativeCode: 0, Modifiers.None, KeyAction.Press);
        var hookReached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using ManualResetEventSlim release = new();
        application.DrainInputRaceHookForTests = () =>
        {
            hookReached.SetResult();
            release.Wait();
        };

        // strokeA's Enqueue arms the latch and posts DrainInput, which dequeues strokeA, dispatches
        // it, observes the queue empty, releases the dequeue lock, and then parks in the hook above -
        // latch still true, finally's reset and repost decision not yet run.
        application.Input(in strokeA);
        await hookReached.Task.WaitAsync(TestContext.Current.CancellationToken);

        // strokeB's Enqueue races the parked drain exactly like the stranded-record test above: it
        // observes _inputWake already true and returns without posting, leaving strokeB sitting in
        // the queue for the parked drain's own finally block to discover and decide to repost for.
        await Task.Run(() => application.Input(in strokeB), TestContext.Current.CancellationToken);

        // Dispose the dispatcher directly from a second thread, bypassing Application's own
        // serialized StopAsync/DisposeAsync, to force the shutdown race open reliably instead of
        // merely hoping to land inside a window a handful of CPU instructions wide. DisposeAsync
        // flips the dispatcher's internal _stopping flag synchronously, under its own gate - a gate
        // unrelated to Application's own _gate guarding _inputWake/_input - before the call below
        // returns, so by the time dispatcherDisposeStarted completes, the race window is open.
        ValueTask dispatcherDisposal = default;
        var dispatcherDisposeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = Task.Run(
            () =>
            {
                dispatcherDisposal = application.Dispatcher.DisposeAsync();
                dispatcherDisposeStarted.SetResult();
            },
            TestContext.Current.CancellationToken);
        await dispatcherDisposeStarted.Task.WaitAsync(TestContext.Current.CancellationToken);

        // Release the parked drain. Its finally block now finds the queue non-empty (strokeB),
        // decides to repost, and calls the now-guarded Dispatcher.Post(DrainInput) squarely inside
        // the window where the dispatcher just started stopping.
        application.DrainInputRaceHookForTests = null;
        release.Set();

        await dispatcherDisposal.AsTask().WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        application.Failure.ShouldBeNull();
        unhandledCount.ShouldBe(0);

        try
        {
            await application.DisposeAsync();
        }
        catch (ObjectDisposedException)
        {
            // Application's own StopAsync/DisposeAsync tries to route BeginStopping through the
            // dispatcher this test just bypassed and disposed directly out from under it; that
            // expected failure is teardown noise from the forced-race technique, not something this
            // test is asserting about.
        }
    }

    /// <summary>Verifies a record admitted into the input queue just behind a Closed record - both
    /// enqueued while the application was not yet stopping, so both pass Enqueue's admission check
    /// - is dequeued but not dispatched once the Closed record, processed earlier in the same drain
    /// pass, has already flipped <c>_stopping</c>. The skip itself is intentional (dispatching more
    /// input into a tree that is mid-teardown is not useful), but it must no longer be silent:
    /// <see cref="Application.DrainInputSkippedRecordHookForTests"/> is the only channel that
    /// surfaces it, since routing this through <c>Report</c>/<c>UnhandledException</c> would
    /// misrepresent an ordinary, successful shutdown as an application failure.</summary>
    [Fact]
    public async Task Input_WhenQueuedJustBehindClosedInSameDrainPass_SkipsDispatchObservablyAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(10, 4)));
        var root = new ProbeContainer();
        var child = new ProbeControl { IsFocusable = true };
        root.Children.Add(child);
        await using Application application = new(
            root,
            terminal,
            terminal,
            TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);
        var delivered = false;
        await application.Dispatcher.InvokeAsync(
            () =>
            {
                application.Focus.Focus(child).ShouldBeTrue();
                _ = child.AddHandler(Events.Key, (_, _) => delivered = true);
            },
            TestContext.Current.CancellationToken);

        List<RecordKind> skipped = [];
        application.DrainInputSkippedRecordHookForTests = record => skipped.Add(record.Kind);

        // Block the dispatcher so both Enqueue calls below run - and complete - before DrainInput
        // gets a chance to process either record, guaranteeing they are admitted while _stopping is
        // still false and land in the queue in the exact FIFO order this test needs.
        using ManualResetEventSlim release = new();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        application.Dispatcher.Post(() =>
        {
            entered.SetResult();
            release.Wait();
        });
        await entered.Task.WaitAsync(TestContext.Current.CancellationToken);

        application.Closed();
        var stroke = new Stroke(Code.Enter, character: null, nativeCode: 0, Modifiers.None, KeyAction.Press);
        application.Input(in stroke);

        release.Set();

        await application.Completion.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        application.Completion.IsCompletedSuccessfully.ShouldBeTrue();
        application.Failure.ShouldBeNull();
        delivered.ShouldBeFalse();
        skipped.ShouldBe([RecordKind.Key]);
    }
}
