// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Runtime;

using System.Buffers;

using SharpVision.Runtime;
using SharpVision.Tests.Controls;

using Terminal.Capabilities;
using Terminal.Graphics;
using Terminal.Kitty.Graphics;
using Terminal.Multiplexing;

using BindingFlags = System.Reflection.BindingFlags;
using GraphicsImage = Terminal.Graphics.ImageSource;
using MultiplexerKind = Terminal.Multiplexing.MultiplexerKind;
using TargetInvocationException = System.Reflection.TargetInvocationException;

/// <summary>Verifies application startup, frame completion, suspension, and shutdown.</summary>
public sealed class ApplicationTests
{
    /// <summary>Verifies hosted Kitty graphics acknowledgements reach the renderer backend so a
    /// terminal-assigned image id replaces the temporary client image number.</summary>
    [Fact]
    public async Task Response_WhenKittyAssignsImageId_ForwardsAcknowledgementToRendererAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(4, 2)));
        var backend = new RecordingGraphicsBackend();
        await using Application application = new(
            new ProbeControl(),
            terminal,
            terminal,
            TerminalOptions.Minimal);
        application.SeedRenderer(new Renderer(backend));
        await application.StartAsync(TestContext.Current.CancellationToken);
        var response = KittyGraphicsResponse.Parse("Gi=99,I=1;OK"u8);

        application.Response(response);
        await application.Dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);

        backend.Response.ShouldBeSameAs(response);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies unfinished programmatic Themes cannot be published as immutable state.</summary>
    [Fact]
    public async Task Theme_WhenAssignedUnfrozen_ThrowsBeforePublicationAsync()
    {
        await using FakeTerminal terminal = new();
        await using Application application = new(new ProbeControl(), terminal, terminal, TerminalOptions.Minimal);
        var unfinished = new Theme();

        _ = await application.Dispatcher.InvokeAsync(
            () => Should.Throw<InvalidOperationException>(() => application.Theme = unfinished),
            TestContext.Current.CancellationToken);

        application.Theme.ShouldBeSameAs(ThemeCatalog.Dark);
    }

    /// <summary>Verifies Window activation is an empty read model before the control tree initializes.</summary>
    [Fact]
    public async Task ActiveWindow_WhenApplicationIsNotStarted_IsNullAsync()
    {
        await using FakeTerminal terminal = new();
        await using Application application = new(
            new ProbeControl(),
            terminal,
            terminal,
            TerminalOptions.Minimal);

        application.ActiveWindow.ShouldBeNull();
    }

    /// <summary>Verifies shutdown clears application and Window activation before releasing the tree.</summary>
    [Fact]
    public async Task StopAsync_WhenWindowIsActive_ClearsActivationAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(10, 4)));
        var window = new Window();
        await using Application application = new(
            window,
            terminal,
            terminal,
            TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);
        await application.Dispatcher.InvokeAsync(
            () =>
            {
                var pointer = new Pointer(
                    new Point(1, 1),
                    pixels: null,
                    Buttons.Primary,
                    PointerAction.Press,
                    wheelX: 0,
                    wheelY: 0,
                    Modifiers.None,
                    isMotion: false,
                    isCellPositionInferred: false);
                application.Capture.Dispatch(pointer).ShouldBeSameAs(window);
            },
            TestContext.Current.CancellationToken);
        application.ActiveWindow.ShouldBeSameAs(window);

        await application.StopAsync(TestContext.Current.CancellationToken);

        application.ActiveWindow.ShouldBeNull();
        window.IsActive.ShouldBeFalse();
    }

    /// <summary>Verifies a cancellation landing after the session has gone live still stops and disposes
    /// it, instead of leaking a fully-started session that StartAsync's own throw never observes.</summary>
    [Fact]
    public async Task StartAsync_WhenCancelledAfterSessionGoesLive_StopsSessionBeforeThrowingAsync()
    {
        await using BlockingResizeTerminal terminal = new();
        await using Application application = new(
            new ProbeControl(),
            terminal,
            terminal,
            TerminalOptions.Minimal);
        using var cancellation = new CancellationTokenSource();

        var starting = application.StartAsync(cancellation.Token).AsTask();
        await terminal.ResizeRequested.WaitAsync(TestContext.Current.CancellationToken);
        cancellation.Cancel();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await starting);

        // Without this fix, nothing ever calls StopAsync here, so Completion never
        // completes and this wait would time out instead of observing a clean shutdown.
        await Should.NotThrowAsync(async () => await application.Completion
            .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
    }

    /// <summary>Verifies RunAsync mirrors its later cancellation handling for a cancellation that lands
    /// while StartAsync's own tail wait is pending, returning cleanly instead of leaking the session.</summary>
    [Fact]
    public async Task RunAsync_WhenCancelledAfterSessionGoesLive_ReturnsWithoutThrowingAsync()
    {
        await using BlockingResizeTerminal terminal = new();
        await using Application application = new(
            new ProbeControl(),
            terminal,
            terminal,
            TerminalOptions.Minimal);
        using var cancellation = new CancellationTokenSource();

        var running = application.RunAsync(cancellation.Token);
        await terminal.ResizeRequested.WaitAsync(TestContext.Current.CancellationToken);
        cancellation.Cancel();

        await running.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        // RunAsync returning without throwing is not by itself proof of a clean shutdown — the
        // buggy StartAsync also returns to a caller that swallows its cancellation without ever
        // calling StopAsync, leaving the session running unobserved. Completion must actually
        // finish, not just be left pending forever.
        await Should.NotThrowAsync(async () => await application.Completion
            .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
    }

    /// <summary>Verifies an off-dispatcher Theme assignment throws on the calling thread instead of
    /// being silently marshaled - a deferred assignment previously surfaced its
    /// ObjectDisposedException on the dispatcher thread instead, poisoning Failure and faulting an
    /// unrelated StopAsync for whichever caller happened to be shutting the application down.</summary>
    [Fact]
    public async Task Theme_WhenAssignedOffDispatcher_ThrowsOnCallingThreadAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(10, 4)));
        await using Application application = new(new ProbeControl(), terminal, terminal, TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);

        _ = await Should.ThrowAsync<InvalidOperationException>(() => Task.Run(() => application.Theme = ThemeCatalog.White));

        application.Failure.ShouldBeNull();
        await application.StopAsync(TestContext.Current.CancellationToken);
        application.Failure.ShouldBeNull();
    }

    /// <summary>Verifies RefreshScreen forces a render pass - and the pending renderer-invalidation
    /// flag that starts the next render from a clean baseline - even when nothing else invalidated
    /// the tree, the supported recovery for external terminal corruption.</summary>
    [Fact]
    public async Task RefreshScreen_WhenNothingElseInvalidated_StillProducesAFrameAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(10, 4)));
        await using Application application = new(new ProbeControl(), terminal, terminal, TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);
        var rendered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        application.FrameRendered += (_, _) => rendered.TrySetResult();

        await application.Dispatcher.InvokeAsync(
            () =>
            {
                application.Root.Pending.ShouldBe(Invalidation.None);
                application.RefreshScreen();
            },
            TestContext.Current.CancellationToken);

        await rendered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies RefreshScreen throws on the calling thread instead of silently deferring
    /// when called off the dispatcher, matching every other public mutation seam.</summary>
    [Fact]
    public async Task RefreshScreen_WhenAccessedOffDispatcher_ThrowsOnCallingThreadAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(10, 4)));
        await using Application application = new(new ProbeControl(), terminal, terminal, TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);

        _ = await Should.ThrowAsync<InvalidOperationException>(() => Task.Run(() => application.RefreshScreen()));

        application.Failure.ShouldBeNull();
        await application.StopAsync(TestContext.Current.CancellationToken);
        application.Failure.ShouldBeNull();
    }

    /// <summary>Verifies a control that calls RefreshScreen synchronously from inside its own
    /// render pass - legal, reachable application-level misuse, since any OnRenderContent
    /// override or event handler can run on the dispatcher thread mid-paint - coalesces through
    /// the same _renderRequested path every other invalidation source uses, instead of
    /// reentering Root.Render on the same Root control and letting ControlBase.RenderCore's own
    /// per-control guard throw InvalidOperationException("Render cannot be reentered.") uncaught
    /// into the dispatcher's unhandled-exception path, which would force the whole application to
    /// shut down.</summary>
    [Fact]
    public async Task RefreshScreen_WhenCalledSynchronouslyFromWithinRender_CoalescesInsteadOfReenteringAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(10, 4)));
        var probe = new ProbeControl();
        await using Application application = new(probe, terminal, terminal, TerminalOptions.Minimal);

        var reentered = false;
        probe.Rendering = _ =>
        {
            // Guard against recursing on every subsequent frame; one reentrant call mid-paint is
            // enough to reproduce the defect, and the coalesced second frame must render cleanly.
            if (reentered)
            {
                return;
            }

            reentered = true;
            application.RefreshScreen();
        };

        var frameCount = 0;
        var secondFrame = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        application.FrameRendered += (_, _) =>
        {
            if (++frameCount >= 2)
            {
                _ = secondFrame.TrySetResult();
            }
        };

        await application.StartAsync(TestContext.Current.CancellationToken);

        // Without the fix, the reentrant RefreshScreen call inside the first render pass reenters
        // Root.Render and throws; that exception is reported as an unhandled failure and the
        // application force-stops, so neither of these would ever become true and this would time
        // out instead of observing the coalesced second frame.
        await secondFrame.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        application.Failure.ShouldBeNull();

        await application.StopAsync(TestContext.Current.CancellationToken);
        application.Failure.ShouldBeNull();
    }

    /// <summary>Verifies Shutdown() drives the identical cooperative stop path as the ISink Closed()
    /// callback, giving application code a discoverable, intention-named exit call.</summary>
    [Fact]
    public async Task Shutdown_WhenCalled_StopsTheApplicationLikeClosedAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(10, 4)));
        await using Application application = new(new ProbeControl(), terminal, terminal, TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);

        application.Shutdown();

        await application.Completion.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a visual-only Theme change renders without the former unconditional root measure.</summary>
    [Fact]
    public async Task Theme_WhenOnlyResolvedColorsChange_DoesNotRemeasureRootAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(10, 4)));
        var child = new StyledProbe();
        var root = new Stack { Children = { child } };
        await using Application application = new(root, terminal, terminal, TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);
        var measurements = child.MeasureCalls;

        await application.Dispatcher.InvokeAsync(
            () =>
            {
                application.Theme = ThemeCatalog.White;
            },
            TestContext.Current.CancellationToken);

        child.MeasureCalls.ShouldBe(measurements);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies unsuitable profiles are rejected before UI mutation or resource ownership.</summary>
    [Theory]
    [InlineData(Suitability.Missing)]
    [InlineData(Suitability.Generic)]
    [InlineData(Suitability.Hardcopy)]
    [InlineData(Suitability.Incomplete)]
    [InlineData(Suitability.UnsupportedPadding)]
    public void Constructor_WhenProfileIsUnsuitable_RejectsBeforeMutatingOrOwningResources(
        Suitability suitability)
    {
        // Arrange
        var root = new ProbeControl
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom
        };
        var transport = new ConsoleApplicationTransport();
        var resize = new ConsoleApplicationResizeSource();
        var hostLease = new TrackingLease();
        var options = new TerminalOptions
        {
            Profile = new TerminalProfile(
                new Description("unsuitable", DescriptionOrigin.BuiltIn, suitability),
                TerminalCapabilities.Conservative)
        };

        // Act
        _ = Should.Throw<NotSupportedException>(() =>
            new Application(root, transport, resize, options, hostLease));

        // Assert
        root.HorizontalAlignment.ShouldBe(HorizontalAlignment.Center);
        root.VerticalAlignment.ShouldBe(VerticalAlignment.Bottom);
        root.Dispatcher.ShouldBeNull();
        root.Parent.ShouldBeNull();
        root.OwningSlot.ShouldBeNull();
        root.IsDisposed.ShouldBeFalse();
        transport.Writes.ShouldBeEmpty();
        transport.Disposals.ShouldBe(0);
        resize.Disposals.ShouldBe(0);
        hostLease.Disposals.ShouldBe(0);
    }

    /// <summary>Verifies one supplied clock drives dispatcher-owned application timers.</summary>
    [Fact]
    public async Task Constructor_WhenTimeProviderIsSupplied_PropagatesClockToDispatcherAsync()
    {
        // Arrange
        var clock = new ManualTimeProvider();
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(10, 4)));
        await using Application application = new(
            new ProbeControl(),
            terminal,
            terminal,
            TerminalOptions.Minimal,
            timeProvider: clock);
        await application.StartAsync(TestContext.Current.CancellationToken);
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var timer = await application.Dispatcher.InvokeAsync(
            () =>
            {
                var value = new DispatcherTimer(
                    application.Dispatcher,
                    TimeSpan.FromMilliseconds(200));
                value.Tick += (_, _) => _ = completed.TrySetResult();
                value.Start();
                return value;
            },
            TestContext.Current.CancellationToken);

        // Act
        clock.Advance(TimeSpan.FromMilliseconds(200));

        // Assert
        await completed.Task.WaitAsync(TestContext.Current.CancellationToken);
        timer.Dispose();
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies starting precedes modes and started follows layout, resize, and frame.</summary>
    [Fact]
    public async Task StartAsync_WhenFirstResizeArrives_UsesDocumentedOrderingAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        List<string> order = [];
        var root = new ProbeControl
        {
            Measuring = _ => order.Add("layout"),
            Rendering = _ => order.Add("control-frame")
        };
        await using Application application = new(
            root,
            terminal,
            terminal,
            TerminalOptions.Minimal with { AlternateScreen = true });
        application.Starting += (_, _) => order.Add("starting");
        terminal.Written += _ => order.Add("write");
        application.Resize += (_, eventArgs) =>
        {
            root.Bounds.Width.ShouldBe(eventArgs.Dimensions.Cells.Width);
            root.Bounds.Height.ShouldBe(eventArgs.Dimensions.Cells.Height);
            order.Add("resize");
        };
        application.FrameRendered += (_, _) => order.Add("frame");
        application.Started += (_, _) => order.Add("started");

        await application.StartAsync(TestContext.Current.CancellationToken);

        order.IndexOf("starting").ShouldBeLessThan(order.IndexOf("write"));
        order.IndexOf("layout").ShouldBeLessThan(order.IndexOf("resize"));
        order.IndexOf("resize").ShouldBeLessThan(order.IndexOf("frame"));
        order.IndexOf("frame").ShouldBeLessThan(order.IndexOf("started"));
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies frame callbacks and startup wait for transport flush completion.</summary>
    [Fact]
    public async Task StartAsync_WhenFlushIsPaused_DoesNotCommitFrameEarlyAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.PauseFlush();
        terminal.QueueResize(new Dimensions(new Size(10, 4)));
        await using Application application = new(
            new ProbeControl(),
            terminal,
            terminal,
            TerminalOptions.Minimal);
        var rendered = false;
        application.FrameRendered += (_, _) => rendered = true;

        var starting = application.StartAsync(TestContext.Current.CancellationToken).AsTask();
        await application.Dispatcher.InvokeAsync(
            static () => { },
            TestContext.Current.CancellationToken);
        rendered.ShouldBeFalse();
        starting.IsCompleted.ShouldBeFalse();
        terminal.ReleaseFlush();
        await starting;

        rendered.ShouldBeTrue();
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies zero-cell startup commits suspended layout without a frame.</summary>
    [Fact]
    public async Task StartAsync_WhenSizeIsSuspended_StartsWithoutRenderingFrameAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(0, 0)));
        await using Application application = new(
            new ProbeControl(),
            terminal,
            terminal,
            TerminalOptions.Minimal);
        var frames = 0;
        application.FrameRendered += (_, _) => frames++;

        await application.StartAsync(TestContext.Current.CancellationToken);

        application.Size.ShouldBe(new Size(0, 0));
        frames.ShouldBe(0);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies repeated stop raises lifecycle events once and restores modes.</summary>
    [Fact]
    public async Task StopAsync_WhenCalledRepeatedly_StopsAndCleansOnceAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(10, 4)));
        await using Application application = new(
            new ProbeControl(),
            terminal,
            terminal,
            TerminalOptions.Minimal with { AlternateScreen = true });
        var stopping = 0;
        var stopped = 0;
        application.Stopping += (_, _) => stopping++;
        application.Stopped += (_, _) => stopped++;
        await application.StartAsync(TestContext.Current.CancellationToken);

        await application.StopAsync(TestContext.Current.CancellationToken);
        await application.StopAsync(TestContext.Current.CancellationToken);

        stopping.ShouldBe(1);
        stopped.ShouldBe(1);
        terminal.Writes.Count.ShouldBeGreaterThanOrEqualTo(3);
    }

    /// <summary>
    /// Verifies an already-cancelled caller token ends only the caller's wait. The shutdown
    /// request itself is irrevocable, so cleanup must still run to completion.
    /// </summary>
    [Fact]
    public async Task StopAsync_WhenCallerTokenIsAlreadyCancelled_StillCompletesShutdownAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(10, 4)));
        await using Application application = new(
            new ProbeControl(),
            terminal,
            terminal,
            TerminalOptions.Minimal with { AlternateScreen = true });
        var stopping = 0;
        var stopped = 0;
        application.Stopping += (_, _) => stopping++;
        application.Stopped += (_, _) => stopped++;
        await application.StartAsync(TestContext.Current.CancellationToken);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () =>
            await application.StopAsync(cancelled.Token));

        await application.Completion.WaitAsync(TestContext.Current.CancellationToken);
        stopping.ShouldBe(1);
        stopped.ShouldBe(1);
        application.Completion.IsCompletedSuccessfully.ShouldBeTrue();
    }

    /// <summary>
    /// Verifies cancelling the wait after the request was queued still completes shutdown, and
    /// that a later uncancelled stop observes the same single lifecycle.
    /// </summary>
    [Fact]
    public async Task StopAsync_WhenCallerStopsWaiting_CompletesShutdownExactlyOnceAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(10, 4)));
        await using Application application = new(
            new ProbeControl(),
            terminal,
            terminal,
            TerminalOptions.Minimal with { AlternateScreen = true });
        var stopping = 0;
        var stopped = 0;
        application.Stopping += (_, _) => stopping++;
        application.Stopped += (_, _) => stopped++;
        await application.StartAsync(TestContext.Current.CancellationToken);
        using var cancellation = new CancellationTokenSource();

        var stopRequest = application.StopAsync(cancellation.Token).AsTask();
        await cancellation.CancelAsync();

        try
        {
            await stopRequest;
        }
        catch (OperationCanceledException)
        {
            // The caller may or may not win the race against its own request; either way the
            // application must still shut down exactly once.
        }

        await application.StopAsync(TestContext.Current.CancellationToken);
        stopping.ShouldBe(1);
        stopped.ShouldBe(1);
        application.Completion.IsCompletedSuccessfully.ShouldBeTrue();
    }

    /// <summary>
    /// Verifies a Stopping handler that requests shutdown again cannot re-enter the cancellable
    /// event. Dispatcher invocation runs inline on the dispatcher thread, so an unguarded nested
    /// request recurses until the stack is exhausted.
    /// </summary>
    [Fact]
    public async Task StopAsync_WhenRequestedFromStoppingHandler_RaisesStoppingOnceAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(10, 4)));
        await using Application application = new(
            new ProbeControl(),
            terminal,
            terminal,
            TerminalOptions.Minimal);
        var calls = 0;
        await application.StartAsync(TestContext.Current.CancellationToken);
        application.Stopping += OnStopping;

        await application.StopAsync(TestContext.Current.CancellationToken);

        calls.ShouldBe(1);
        application.Completion.IsCompletedSuccessfully.ShouldBeTrue();
        return;

        void OnStopping(object? sender, StoppingEventArgs eventArgs)
        {
            _ = sender;
            _ = eventArgs;
            calls++;

            // Bounded so a regression fails the assertion instead of exhausting the stack.
            if (calls < 8)
            {
                _ = application.StopAsync().AsTask();
            }
        }
    }

    /// <summary>
    /// Verifies a nested StopAsync call made from inside a Stopping handler does not report its
    /// own completion until real cleanup has actually run. The nested call is absorbed by the
    /// _raisingStopping reentrancy guard while still nested inside the outer, not-yet-returned
    /// Stopping raise; it must wait for that raise's real outcome instead of observing _stopping
    /// mid-raise, where it is still guaranteed false purely because of where in the call stack it
    /// happens to be read.
    /// </summary>
    [Fact]
    public async Task StopAsync_WhenCalledFromStoppingHandler_WaitsForCleanupBeforeCompletingAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(10, 4)));
        await using Application application = new(
            new ProbeControl(),
            terminal,
            terminal,
            TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);

        Task? nestedTask = null;
        var nestedCompletedDuringRaise = false;

        application.Stopping += OnStopping;

        await application.StopAsync(TestContext.Current.CancellationToken);

        var completedNested = nestedTask.ShouldNotBeNull();

        // The bug this guards against: the nested call's own task must not already report
        // success while still nested inside the very Stopping raise it asked to join - cleanup
        // has not run yet at that synchronous point.
        nestedCompletedDuringRaise.ShouldBeFalse();

        await completedNested;

        completedNested.IsCompletedSuccessfully.ShouldBeTrue();
        terminal.Disposals.ShouldBeGreaterThan(0);
        return;

        void OnStopping(object? sender, StoppingEventArgs eventArgs)
        {
            _ = sender;
            _ = eventArgs;
            nestedTask = application.StopAsync().AsTask();
            nestedCompletedDuringRaise = nestedTask.IsCompleted;
        }
    }

    /// <summary>
    /// Verifies a nested StopAsync call made from a Stopping handler that also cancels the request
    /// returns promptly rather than hanging: the outer raise it joined was canceled, so there is no
    /// completion to wait for.
    /// </summary>
    [Fact]
    public async Task StopAsync_WhenHandlerCancelsAndCallsNested_NestedReturnsPromptlyAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(10, 4)));
        await using Application application = new(
            new ProbeControl(),
            terminal,
            terminal,
            TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);

        Task? nestedTask = null;

        application.Stopping += OnStopping;

        await application.StopAsync(TestContext.Current.CancellationToken);

        var completedNested = nestedTask.ShouldNotBeNull();

        // Bounded so a regression hangs the assertion instead of the whole test run.
        await completedNested.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        completedNested.IsCompletedSuccessfully.ShouldBeTrue();
        application.Completion.IsCompleted.ShouldBeFalse();
        return;

        void OnStopping(object? sender, StoppingEventArgs eventArgs)
        {
            _ = sender;
            eventArgs.Cancel = true;
            nestedTask = application.StopAsync().AsTask();
        }
    }

    /// <summary>
    /// Verifies a handler that cancels the request while also requesting shutdown again leaves the
    /// application running: a nested unforced request cannot override the cancellation it saw.
    /// </summary>
    [Fact]
    public async Task StopAsync_WhenHandlerCancelsAndRequestsAgain_LeavesApplicationRunningAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(10, 4)));
        await using Application application = new(
            new ProbeControl(),
            terminal,
            terminal,
            TerminalOptions.Minimal);
        var calls = 0;
        await application.StartAsync(TestContext.Current.CancellationToken);
        application.Stopping += OnStopping;

        await application.StopAsync(TestContext.Current.CancellationToken);

        calls.ShouldBe(1);
        application.Completion.IsCompleted.ShouldBeFalse();
        application.Stopping -= OnStopping;
        await application.StopAsync(TestContext.Current.CancellationToken);
        application.Completion.IsCompletedSuccessfully.ShouldBeTrue();
        return;

        void OnStopping(object? sender, StoppingEventArgs eventArgs)
        {
            _ = sender;
            calls++;
            eventArgs.Cancel = true;

            if (calls < 8)
            {
                _ = application.StopAsync().AsTask();
            }
        }
    }

    /// <summary>Verifies an explicit stopping preview may cancel one request.</summary>
    [Fact]
    public async Task StopAsync_WhenPreviewCancels_LeavesApplicationRunningAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(10, 4)));
        await using Application application = new(
            new ProbeControl(),
            terminal,
            terminal,
            TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);
        application.Stopping += Cancel;

        await application.StopAsync(TestContext.Current.CancellationToken);

        application.Completion.IsCompleted.ShouldBeFalse();
        application.Stopping -= Cancel;
        await application.StopAsync(TestContext.Current.CancellationToken);
        application.Completion.IsCompletedSuccessfully.ShouldBeTrue();
        return;

        static void Cancel(object? sender, StoppingEventArgs eventArgs)
        {
            _ = sender;
            eventArgs.Cancel = true;
        }
    }

    /// <summary>Verifies callback failure identity survives terminal cleanup.</summary>
    [Fact]
    public async Task StartAsync_WhenResizeHandlerThrows_PreservesPrimaryExceptionAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(10, 4)));
        var cleanup = new IOException("cleanup");
        terminal.FailWriteNumber = 2;
        terminal.WriteFailure = cleanup;
        await using Application application = new(
            new ProbeControl(),
            terminal,
            terminal,
            TerminalOptions.Minimal with { AlternateScreen = true });
        var failure = new InvalidOperationException("resize-handler");
        application.Resize += (_, _) => throw failure;

        var thrown = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await application.StartAsync(TestContext.Current.CancellationToken));

        thrown.ShouldBeSameAs(failure);
        application.Failure.ShouldBeSameAs(failure);
        application.LastCleanupException.ShouldBeSameAs(cleanup);
        terminal.Writes.Count.ShouldBeGreaterThanOrEqualTo(2);
    }

    /// <summary>Verifies disposal before start still releases the owned root.</summary>
    [Fact]
    public async Task DisposeAsync_WhenNeverStarted_ReleasesOwnedResourcesAsync()
    {
        await using FakeTerminal terminal = new();
        var root = new ProbeControl();
        var application = new Application(
            root,
            terminal,
            terminal,
            TerminalOptions.Minimal);

        await application.DisposeAsync();

        root.IsDisposed.ShouldBeTrue();
        application.Completion.IsCompletedSuccessfully.ShouldBeTrue();
    }

    /// <summary>Verifies hosted clipboard shortcuts copy, cut, paste, and retain normal edit history.</summary>
    [Fact]
    public async Task Input_WhenClipboardShortcutsTargetTextInputs_SharesApplicationBufferAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 4)));
        var first = new TextInput { Text = "cafe\u0301" };
        var second = new TextInput();
        var root = new Stack { Children = { first, second } };
        await using Application application = new(root, terminal, terminal, TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);
        await application.Dispatcher.InvokeAsync(() =>
        {
            application.Focus.Focus(first).ShouldBeTrue();
            first.Select(0, first.Text.Length);
        }, TestContext.Current.CancellationToken);

        await ShortcutAsync(application, 'c');
        await application.Dispatcher.InvokeAsync(
            () => application.Focus.Focus(second).ShouldBeTrue(),
            TestContext.Current.CancellationToken);
        await ShortcutAsync(application, 'v');

        await application.Dispatcher.InvokeAsync(() =>
        {
            second.Text.ShouldBe("cafe\u0301");
            second.CanUndo.ShouldBeTrue();
            second.Select(0, second.Text.Length);
        }, TestContext.Current.CancellationToken);
        await ShortcutAsync(application, 'x');

        await application.Dispatcher.InvokeAsync(() =>
        {
            second.Text.ShouldBeEmpty();
            second.CaretIndex.ShouldBe(0);
        }, TestContext.Current.CancellationToken);
        await application.Dispatcher.InvokeAsync(
            () =>
            {
                application.Focus.Focus(first).ShouldBeTrue();
                first.CaretIndex = first.Text.Length;
            },
            TestContext.Current.CancellationToken);
        await ShortcutAsync(application, 'v');

        await application.Dispatcher.InvokeAsync(
            () => first.Text.ShouldBe("cafe\u0301cafe\u0301"),
            TestContext.Current.CancellationToken);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies password-suppressed copy and an empty initial buffer never disclose or mutate text.</summary>
    [Fact]
    public async Task Input_WhenClipboardHasNoPublishableText_PreservesBufferAndDocumentAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 4)));
        var source = new TextInput { Text = "safe" };
        var password = new TextInput { Text = "secret", PasswordCharacter = new Rune('*') };
        var target = new TextInput();
        var root = new Stack { Children = { source, password, target } };
        await using Application application = new(root, terminal, terminal, TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);

        await application.Dispatcher.InvokeAsync(
            () => application.Focus.Focus(target).ShouldBeTrue(),
            TestContext.Current.CancellationToken);
        await ShortcutAsync(application, 'v');
        await application.Dispatcher.InvokeAsync(() =>
        {
            target.Text.ShouldBeEmpty();
            application.Focus.Focus(source).ShouldBeTrue();
            source.Select(0, source.Text.Length);
        }, TestContext.Current.CancellationToken);
        await ShortcutAsync(application, 'c');
        await application.Dispatcher.InvokeAsync(() =>
        {
            application.Focus.Focus(password).ShouldBeTrue();
            password.Select(0, password.Text.Length);
        }, TestContext.Current.CancellationToken);
        await ShortcutAsync(application, 'c');
        await application.Dispatcher.InvokeAsync(
            () => application.Focus.Focus(target).ShouldBeTrue(),
            TestContext.Current.CancellationToken);
        await ShortcutAsync(application, 'v');

        await application.Dispatcher.InvokeAsync(() =>
        {
            target.Text.ShouldBe("safe");
            password.Text.ShouldBe("secret");
        }, TestContext.Current.CancellationToken);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies earlier root preview handling suppresses clipboard work while handled observers still run.</summary>
    [Fact]
    public async Task Input_WhenEarlierRootPreviewHandlesClipboardShortcut_PreservesRoutedInterceptionAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 4)));
        var source = new TextInput { Text = "blocked" };
        var target = new TextInput();
        var root = new Stack { Children = { source, target } };
        var intercepted = 0;
        var observedHandled = 0;
        _ = root.AddHandler(Events.Key, (_, eventArgs) =>
        {
            if (IsControlCharacter(eventArgs, 'c'))
            {
                intercepted++;
                eventArgs.IsHandled = true;
            }
        });
        _ = root.AddHandler(
            Events.Key,
            (_, eventArgs) =>
            {
                if (IsControlCharacter(eventArgs, 'c') && eventArgs.IsHandled)
                {
                    observedHandled++;
                }
            },
            handledEventsToo: true);
        await using Application application = new(root, terminal, terminal, TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);
        await application.Dispatcher.InvokeAsync(() =>
        {
            application.Focus.Focus(source).ShouldBeTrue();
            source.Select(0, source.Text.Length);
        }, TestContext.Current.CancellationToken);

        await ShortcutAsync(application, 'c');
        await application.Dispatcher.InvokeAsync(
            () => application.Focus.Focus(target).ShouldBeTrue(),
            TestContext.Current.CancellationToken);
        await ShortcutAsync(application, 'v');

        intercepted.ShouldBe(1);
        observedHandled.ShouldBe(1);
        target.Text.ShouldBeEmpty();
        await application.StopAsync(TestContext.Current.CancellationToken);
        return;

        static bool IsControlCharacter(KeyEventArgs eventArgs, char character) =>
            eventArgs.Phase == RoutingPhase.Preview &&
            eventArgs.Stroke.Action == KeyAction.Press &&
            eventArgs.Stroke.Code == Code.Character &&
            eventArgs.Stroke.Character == new Rune(character) &&
            (eventArgs.Stroke.Modifiers & ~(Modifiers.CapsLock | Modifiers.NumLock)) == Modifiers.Control;
    }

    /// <summary>Verifies a scope entered during root preview does not rewrite the initiating clipboard route.</summary>
    [Fact]
    public async Task Input_WhenScopeEntersDuringNonModalPreview_PreservesCapturedClipboardRouteAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 4)));
        var source = new TextInput { Text = "captured" };
        var target = new TextInput();
        var plane = new ProbeContainer();
        var root = new Stack { Children = { source, target, plane } };
        await using Application application = new(root, terminal, terminal, TerminalOptions.Minimal);
        ModalScope? scope = null;
        _ = root.AddHandler(Events.Key, (_, eventArgs) =>
        {
            if (eventArgs.Phase == RoutingPhase.Preview &&
                eventArgs.Stroke.Code == Code.Character &&
                eventArgs.Stroke.Character == new Rune('c') &&
                scope is null)
            {
                scope = application.Modality.Enter(plane);
                application.Focus.Focus(source).ShouldBeFalse();
                application.Focus.Focused.ShouldBeNull();
            }
        });
        await application.StartAsync(TestContext.Current.CancellationToken);
        var observedHandled = 0;
        await application.Dispatcher.InvokeAsync(() =>
        {
            _ = root.AddHandler(
                Events.Key,
                (_, eventArgs) =>
                {
                    if (eventArgs.Phase == RoutingPhase.Preview &&
                        eventArgs.Stroke.Character == new Rune('c') &&
                        eventArgs.IsHandled)
                    {
                        observedHandled++;
                    }
                },
                handledEventsToo: true);
            application.Focus.Focus(source).ShouldBeTrue();
            source.Select(0, source.Text.Length);
        }, TestContext.Current.CancellationToken);

        await ShortcutAsync(application, 'c');
        await application.Dispatcher.InvokeAsync(() =>
        {
            _ = scope.ShouldNotBeNull();
            scope.Dispose();
            application.Focus.Focus(target).ShouldBeTrue();
        }, TestContext.Current.CancellationToken);
        await ShortcutAsync(application, 'v');

        observedHandled.ShouldBe(1);
        target.Text.ShouldBe("captured");
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies direct routes cannot borrow application clipboard behavior for an unfocused target.</summary>
    [Fact]
    public async Task Route_WhenClipboardTargetIsUnfocused_DoesNotHandleClipboardCommandsAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 4)));
        var focused = new TextInput { Text = "focused" };
        var unfocused = new TextInput { Text = "unfocused" };
        var root = new Stack { Children = { focused, unfocused } };
        await using Application application = new(root, terminal, terminal, TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);
        await application.Dispatcher.InvokeAsync(() =>
        {
            application.Focus.Focus(focused).ShouldBeTrue();
            unfocused.Select(0, unfocused.Text.Length);

            foreach (var command in "cxv")
            {
                var stroke = new Stroke(
                    Code.Character,
                    new Rune(command),
                    command,
                    Modifiers.Control,
                    KeyAction.Press);
                var result = Router.Route(unfocused, Events.Key, new KeyEventArgs(stroke));

                result.IsHandled.ShouldBeFalse();
                application.Focus.Focused.ShouldBeSameAs(focused);
                unfocused.Text.ShouldBe("unfocused");
            }
        }, TestContext.Current.CancellationToken);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a non-clipboard key still traverses one ordinary preview and bubble route.</summary>
    [Fact]
    public async Task Input_WhenOrdinaryKeyIsNotClipboardShortcut_RoutesOnceAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(10, 4)));
        var root = new ProbeControl { IsFocusable = true };
        var phases = new List<RoutingPhase>();
        _ = root.AddHandler(Events.Key, (_, eventArgs) => phases.Add(eventArgs.Phase));
        await using Application application = new(root, terminal, terminal, TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);
        await application.Dispatcher.InvokeAsync(
            () => application.Focus.Focus(root).ShouldBeTrue(),
            TestContext.Current.CancellationToken);
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

    /// <summary>Verifies a clipboard shortcut suppresses its adjacent paired text record, so
    /// Ctrl+C does not also type 'c' into the focused editor once the copy itself completes.
    /// Before the fix, only the menu-shortcut and access-key paths armed suppression - a
    /// preview-phase clipboard shortcut left the paired text record to route normally and land
    /// as typed input.</summary>
    [Fact]
    public async Task Input_WhenClipboardShortcutConsumesTheStroke_SuppressesPairedTextAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 4)));
        var input = new TextInput { Text = "seed" };
        await using Application application = new(input, terminal, terminal, TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);
        await application.Dispatcher.InvokeAsync(() =>
        {
            application.Focus.Focus(input).ShouldBeTrue();
            input.Select(0, input.Text.Length);
        }, TestContext.Current.CancellationToken);
        var stroke = new Stroke(Code.Character, new Rune('c'), nativeCode: 0, Modifiers.Control, KeyAction.Press);
        var text = new TerminalText(new Rune('c'));

        application.Input(in stroke);
        application.Input(in text);
        await application.Dispatcher.InvokeAsync(
            static () => { },
            TestContext.Current.CancellationToken);

        input.Text.ShouldBe("seed");
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies an ordinary control default that sets IsHandled - not a shortcut or
    /// access key - also suppresses the stroke's paired text record, so Ctrl+A selecting the
    /// whole document does not then have its leaked paired 'a' replace the selection. Before
    /// the fix, suppression only armed from the menu-shortcut and access-key paths, so any
    /// other consume path - including every routed control default - let the paired text
    /// record through to replace the just-made selection.</summary>
    [Fact]
    public async Task Input_WhenControlDefaultConsumesTheStroke_SuppressesPairedTextAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 4)));
        var input = new TextInput { Text = "AB" };
        await using Application application = new(input, terminal, terminal, TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);
        await application.Dispatcher.InvokeAsync(
            () => application.Focus.Focus(input).ShouldBeTrue(),
            TestContext.Current.CancellationToken);
        var stroke = new Stroke(Code.Character, new Rune('a'), nativeCode: 0, Modifiers.Control, KeyAction.Press);
        var text = new TerminalText(new Rune('a'));

        application.Input(in stroke);
        application.Input(in text);
        await application.Dispatcher.InvokeAsync(
            static () => { },
            TestContext.Current.CancellationToken);

        input.Text.ShouldBe("AB");
        input.SelectionStart.ShouldBe(0);
        input.SelectionLength.ShouldBe(2);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a throwing Stopping handler still completes the cleanup chain - including
    /// host-lease disposal - instead of leaving the request permanently stuck.</summary>
    [Fact]
    public async Task StopAsync_WhenStoppingHandlerThrows_StillCompletesCleanupAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(10, 4)));
        var hostLease = new TrackingLease();
        await using Application application = new(
            new ProbeControl(),
            terminal,
            terminal,
            TerminalOptions.Minimal,
            hostLease);
        await application.StartAsync(TestContext.Current.CancellationToken);
        var stoppingFailure = new InvalidOperationException("stopping-handler");
        application.Stopping += (_, _) => throw stoppingFailure;

        var thrown = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await application.StopAsync(TestContext.Current.CancellationToken)
                .AsTask()
                .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));

        thrown.ShouldBeSameAs(stoppingFailure);
        application.Failure.ShouldBeSameAs(stoppingFailure);
        hostLease.Disposals.ShouldBe(1);
    }

    /// <summary>Verifies an UnhandledException handler that itself throws does not skip
    /// terminal-resource cleanup, and that the original failure - not the handler's own exception -
    /// remains what Failure reports.</summary>
    [Fact]
    public async Task StartAsync_WhenUnhandledExceptionHandlerThrows_StillCompletesCleanupWithOriginalFailureAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(10, 4)));
        var hostLease = new TrackingLease();
        await using Application application = new(
            new ProbeControl(),
            terminal,
            terminal,
            TerminalOptions.Minimal,
            hostLease);
        var originalFailure = new NotSupportedException("resize-handler");
        var handlerFailure = new InvalidOperationException("unhandled-handler");
        application.Resize += (_, _) => throw originalFailure;
        application.UnhandledException += (_, _) => throw handlerFailure;

        var thrown = await Should.ThrowAsync<NotSupportedException>(async () =>
            await application.StartAsync(TestContext.Current.CancellationToken)
                .AsTask()
                .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));

        thrown.ShouldBeSameAs(originalFailure);
        application.Failure.ShouldBeSameAs(originalFailure);
        application.LastCleanupException.ShouldBeSameAs(handlerFailure);
        hostLease.Disposals.ShouldBe(1);
    }

    /// <summary>Verifies a throwing input handler does not permanently latch the drain loop's
    /// wake flag: with UnhandledException marked handled, a later keystroke is still delivered
    /// instead of the application silently going deaf while it keeps running.</summary>
    [Fact]
    public async Task Input_WhenHandlerThrowsAndUnhandledExceptionIsHandled_StillDeliversLaterInputAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(10, 4)));
        var root = new ProbeControl { IsFocusable = true };
        var deliveries = 0;
        var shouldThrow = true;
        _ = root.AddHandler(Events.Key, (_, eventArgs) =>
        {
            if (eventArgs.Phase != RoutingPhase.Bubble || eventArgs.Stroke.Code != Code.Character)
            {
                return;
            }

            deliveries++;

            if (shouldThrow)
            {
                shouldThrow = false;
                throw new InvalidOperationException("boom");
            }
        });
        await using Application application = new(root, terminal, terminal, TerminalOptions.Minimal);
        var unhandled = 0;
        application.UnhandledException += (_, eventArgs) =>
        {
            unhandled++;
            eventArgs.IsHandled = true;
        };
        await application.StartAsync(TestContext.Current.CancellationToken);
        await application.Dispatcher.InvokeAsync(
            () => application.Focus.Focus(root).ShouldBeTrue(),
            TestContext.Current.CancellationToken);

        await CharacterAsync(application, 'a');
        await CharacterAsync(application, 'b');

        unhandled.ShouldBe(1);
        deliveries.ShouldBe(2);

        // Handling UnhandledException lets the dispatcher continue but does not erase Failure
        // (docs/architecture/error-handling.md), so StopAsync still surfaces it - the assertion
        // that matters here is that the second keystroke was delivered at all.
        var thrown = await Should.ThrowAsync<InvalidOperationException>(
            async () => await application.StopAsync(TestContext.Current.CancellationToken));
        thrown.Message.ShouldBe("boom");
    }

    /// <summary>Verifies a handledEventsToo bubble handler that detaches the focused control
    /// during the same Tab route does not fault the application when the post-route traversal
    /// command later runs against that now-stale anchor.</summary>
    [Fact]
    public async Task Input_WhenHandledEventsTooHandlerDetachesAnchorDuringTabRoute_DoesNotFaultAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 4)));
        var first = new Button { Text = "First" };
        var second = new Button { Text = "Second" };
        var root = new Stack { Children = { first, second } };
        _ = root.AddHandler(
            Events.Key,
            (_, eventArgs) =>
            {
                if (eventArgs.Phase == RoutingPhase.Bubble &&
                    eventArgs.Stroke.Code == Code.Tab &&
                    eventArgs.IsHandled)
                {
                    _ = root.Children.Remove(first);
                }
            },
            handledEventsToo: true);
        await using Application application = new(root, terminal, terminal, TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);
        await application.Dispatcher.InvokeAsync(
            () => application.Focus.Focus(first).ShouldBeTrue(),
            TestContext.Current.CancellationToken);
        var stroke = new Stroke(Code.Tab, character: null, nativeCode: 0, Modifiers.None, KeyAction.Press);

        // Act
        application.Input(in stroke);
        await application.Dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);

        // Assert - no ArgumentException escaped Dispatch, and the application is still usable.
        application.Failure.ShouldBeNull();
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    private static async Task CharacterAsync(Application application, char character)
    {
        var stroke = new Stroke(
            Code.Character,
            new Rune(character),
            nativeCode: 0,
            Modifiers.None,
            KeyAction.Press);
        application.Input(in stroke);
        await application.Dispatcher.InvokeAsync(
            static () => { },
            TestContext.Current.CancellationToken);
    }

    private static async Task ShortcutAsync(Application application, char character)
    {
        var stroke = new Stroke(
            Code.Character,
            new Rune(character),
            nativeCode: 0,
            Modifiers.Control,
            KeyAction.Press);
        application.Input(in stroke);
        await application.Dispatcher.InvokeAsync(
            static () => { },
            TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a handled Alt key consumes its adjacent text record after activating the target.</summary>
    [Fact]
    public async Task Input_WhenAltKeyActivatesButton_SuppressesAdjacentMnemonicTextAsync()
    {
        // Arrange
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 4)));
        var input = new TextInput { Text = "seed" };
        var button = new Button { Text = "&Name" };
        var root = new Stack { Children = { input, button } };
        await using Application application = new(root, terminal, terminal, TerminalOptions.Minimal);
        var clicks = 0;
        button.Click += (_, eventArgs) =>
        {
            eventArgs.Cause.ShouldBe(ActivationCause.Keyboard);
            clicks++;
        };
        await application.StartAsync(TestContext.Current.CancellationToken);
        await application.Dispatcher.InvokeAsync(
            () => application.Focus.Focus(input).ShouldBeTrue(),
            TestContext.Current.CancellationToken);
        var stroke = Alt('n');
        var text = new TerminalText(new Rune('n'));

        // Act
        application.Input(in stroke);
        application.Input(in text);
        await application.Dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);

        // Assert
        clicks.ShouldBe(1);
        input.Text.ShouldBe("seed");
        application.Focus.Focused.ShouldBeSameAs(button);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies an earlier preview handler can reserve Alt input, and that reservation
    /// also suppresses the paired text record - not only the access-key and menu-shortcut paths.
    /// A stroke consumed anywhere on or around its route, by any handler or control
    /// default, never delivers its paired text; before that fix only the two named paths armed
    /// suppression, so this same reservation left the paired 'n' to type into the focused
    /// editor even though the stroke itself was already claimed.</summary>
    [Fact]
    public async Task Input_WhenPreviewHandlesAltKey_DoesNotInvokeAndSuppressesTextAsync()
    {
        // Arrange
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 4)));
        var input = new TextInput();
        var button = new Button { Text = "&Name" };
        var root = new Stack { Children = { input, button } };
        _ = root.AddHandler(Events.Key, (_, eventArgs) =>
        {
            if (eventArgs.Phase == RoutingPhase.Preview && eventArgs.Stroke.Modifiers == Modifiers.Alt)
            {
                eventArgs.IsHandled = true;
            }
        });
        await using Application application = new(root, terminal, terminal, TerminalOptions.Minimal);
        var clicks = 0;
        button.Click += (_, _) => clicks++;
        await application.StartAsync(TestContext.Current.CancellationToken);
        await application.Dispatcher.InvokeAsync(
            () => application.Focus.Focus(input).ShouldBeTrue(),
            TestContext.Current.CancellationToken);
        var stroke = Alt('n');
        var text = new TerminalText(new Rune('n'));

        // Act
        application.Input(in stroke);
        application.Input(in text);
        await application.Dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);

        // Assert
        clicks.ShouldBe(0);
        input.Text.ShouldBeEmpty();
        application.Focus.Focused.ShouldBeSameAs(input);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a single consumed stroke suppresses more than one paired text record,
    /// as Kitty associated text emits one record per colon-separated scalar for a single
    /// stroke.</summary>
    [Fact]
    public async Task Input_WhenAltKeyPairsWithMultipleTextRecords_SuppressesAllOfThemAsync()
    {
        // Arrange
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 4)));
        var input = new TextInput { Text = "seed" };
        var button = new Button { Text = "&Name" };
        var root = new Stack { Children = { input, button } };
        await using Application application = new(root, terminal, terminal, TerminalOptions.Minimal);
        var clicks = 0;
        button.Click += (_, _) => clicks++;
        await application.StartAsync(TestContext.Current.CancellationToken);
        await application.Dispatcher.InvokeAsync(
            () => application.Focus.Focus(input).ShouldBeTrue(),
            TestContext.Current.CancellationToken);
        var stroke = Alt('n');
        var first = new TerminalText(new Rune('n'));
        var second = new TerminalText(new Rune('~'));

        // Act
        application.Input(in stroke);
        application.Input(in first);
        application.Input(in second);
        await application.Dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);

        // Assert
        clicks.ShouldBe(1);
        input.Text.ShouldBe("seed");
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a concurrent, unrelated record (a diagnostic) landing between a
    /// consumed stroke and its paired text record does not strand the suppression, since only a
    /// new keystroke should reset it.</summary>
    [Fact]
    public async Task Input_WhenUnrelatedRecordInterleavesStrokeAndPairedText_StillSuppressesTextAsync()
    {
        // Arrange
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 4)));
        var input = new TextInput { Text = "seed" };
        var button = new Button { Text = "&Name" };
        var root = new Stack { Children = { input, button } };
        await using Application application = new(root, terminal, terminal, TerminalOptions.Minimal);
        var clicks = 0;
        button.Click += (_, _) => clicks++;
        await application.StartAsync(TestContext.Current.CancellationToken);
        await application.Dispatcher.InvokeAsync(
            () => application.Focus.Focus(input).ShouldBeTrue(),
            TestContext.Current.CancellationToken);
        var stroke = Alt('n');
        var text = new TerminalText(new Rune('n'));
        var diagnostic = new Diagnostic(DiagnosticCode.Malformed, SequenceKind.Csi, offset: 0, discardedBytes: 0);

        // Act
        application.Input(in stroke);
        application.Input(in diagnostic);
        application.Input(in text);
        await application.Dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);

        // Assert
        clicks.ShouldBe(1);
        input.Text.ShouldBe("seed");
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies each protocol diagnostic family reports before configured promotion stops the application.</summary>
    /// <param name="code">The representative protocol diagnostic code.</param>
    /// <param name="promotion">The configured family expected to promote it.</param>
    [Theory]
    [InlineData(DiagnosticCode.Malformed, DiagnosticPromotion.MalformedInput)]
    [InlineData(DiagnosticCode.UnexpectedPacket, DiagnosticPromotion.InconsistentReply)]
    [InlineData(DiagnosticCode.Unsupported, DiagnosticPromotion.UnsupportedFeature)]
    [InlineData(DiagnosticCode.Fallback, DiagnosticPromotion.Fallback)]
    public async Task Input_WhenDiagnosticFamilyIsPromoted_ReportsThenStopsAsync(
        DiagnosticCode code,
        DiagnosticPromotion promotion)
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(10, 4)));
        var options = TerminalOptions.Minimal with
        {
            DiagnosticPromotions = promotion
        };
        await using Application application = new(new ProbeControl(), terminal, terminal, options);
        var reported = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        application.Diagnostic += (_, _) => reported.TrySetResult();
        await application.StartAsync(TestContext.Current.CancellationToken);
        var diagnostic = new Diagnostic(
            code,
            SequenceKind.Csi,
            offset: 4,
            discardedBytes: 2);

        application.Input(in diagnostic);

        await reported.Task.WaitAsync(TestContext.Current.CancellationToken);
        var thrown = await Should.ThrowAsync<TerminalDiagnosticException>(async () =>
            await application.Completion.WaitAsync(TestContext.Current.CancellationToken));
        thrown.Promotion.ShouldBe(promotion);
        application.Failure.ShouldBeSameAs(thrown);
    }

    /// <summary>Verifies unavailable synchronized output is a frame fallback promoted after commit.</summary>
    [Fact]
    public async Task StartAsync_WhenSynchronizedOutputIsUnavailableAndFallbackPromoted_StopsAfterFrameAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(10, 4)));
        var options = TerminalOptions.Minimal with
        {
            DiagnosticPromotions = DiagnosticPromotion.Fallback
        };
        await using Application application = new(new ProbeControl(), terminal, terminal, options);
        var rendered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        application.FrameRendered += (_, eventArgs) =>
        {
            eventArgs.RenderMetrics.UsedFallback.ShouldBeTrue();
            _ = rendered.TrySetResult();
        };

        await application.StartAsync(TestContext.Current.CancellationToken);
        await rendered.Task.WaitAsync(TestContext.Current.CancellationToken);
        var thrown = await Should.ThrowAsync<TerminalDiagnosticException>(async () =>
            await application.Completion.WaitAsync(TestContext.Current.CancellationToken));

        thrown.Promotion.ShouldBe(DiagnosticPromotion.Fallback);
    }

    /// <summary>Verifies lenient frame fallback stays in metrics without displacing protocol diagnostics.</summary>
    [Fact]
    public async Task StartAsync_WhenFrameFallbackIsLenient_ReportsMetricsWithoutDiagnosticEventAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(10, 4)));
        await using Application application = new(
            new ProbeControl(),
            terminal,
            terminal,
            TerminalOptions.Minimal);
        var diagnosticRaised = false;
        application.Diagnostic += (_, _) => diagnosticRaised = true;
        var rendered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        application.FrameRendered += (_, eventArgs) =>
        {
            eventArgs.RenderMetrics.UsedFallback.ShouldBeTrue();
            _ = rendered.TrySetResult();
        };

        await application.StartAsync(TestContext.Current.CancellationToken);
        await rendered.Task.WaitAsync(TestContext.Current.CancellationToken);
        await application.Dispatcher.InvokeAsync(
            static () => { },
            TestContext.Current.CancellationToken);

        diagnosticRaised.ShouldBeFalse();
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    private static Stroke Alt(char value) =>
        new(Code.Character, new Rune(value), nativeCode: 0, Modifiers.Alt, KeyAction.Press);

    /// <summary>Gets every consume path the routing contract enumerates.</summary>
    public static TheoryData<string> ConsumePaths =>
        ["PreviewHandler", "BubbleHandler", "ControlDefault", "AncestorPreviewHandler"];

    /// <summary>Verifies a Stroke consumed on each path leaves the focused editor's text
    /// untouched, because the paired TerminalText record is suppressed. Without suppression the
    /// character arrives as ordinary typing and replaces the selection the consuming handler just
    /// made.</summary>
    /// <param name="path">The consume path under test.</param>
    [Theory]
    [MemberData(nameof(ConsumePaths))]
    public async Task Input_WhenStrokeIsConsumedOnAnyPath_SuppressesThePairedTextAsync(string path)
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 4)));
        var input = new TextInput { Text = "AB" };
        var root = new Dock();
        root.Children.Add(input);
        await using Application application = new(root, terminal, terminal, TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);
        await application.Dispatcher.InvokeAsync(
            () =>
            {
                application.Focus.Focus(input).ShouldBeTrue();
                Arm(path, input, root);
            },
            TestContext.Current.CancellationToken);

        var stroke = new Stroke(Code.Character, new Rune('a'), nativeCode: 0, Modifiers.Control, KeyAction.Press);
        var text = new TerminalText(new Rune('a'));
        application.Input(in stroke);
        application.Input(in text);
        await application.Dispatcher.InvokeAsync(
            static () => { },
            TestContext.Current.CancellationToken);

        input.Text.ShouldBe("AB", $"the {path} consume path must suppress the paired text record");
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>The counter-case that keeps the assertion above honest: an identical pair that
    /// nobody consumes must still type. Without this, suppression that fired unconditionally -
    /// swallowing every paired record - would satisfy every case above.</summary>
    [Fact]
    public async Task Input_WhenStrokeIsNotConsumed_StillDeliversThePairedTextAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 4)));
        var input = new TextInput { Text = "AB" };
        await using Application application = new(input, terminal, terminal, TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);
        await application.Dispatcher.InvokeAsync(
            () => application.Focus.Focus(input).ShouldBeTrue(),
            TestContext.Current.CancellationToken);

        // No modifier, no handler, no control default claims this - it is ordinary typing.
        var stroke = new Stroke(Code.Character, new Rune('c'), nativeCode: 0, Modifiers.None, KeyAction.Press);
        var text = new TerminalText(new Rune('c'));
        application.Input(in stroke);
        application.Input(in text);
        await application.Dispatcher.InvokeAsync(
            static () => { },
            TestContext.Current.CancellationToken);

        input.Text.ShouldBe("ABc", "an unconsumed stroke must still deliver its paired text");
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a consumed stroke suppresses <em>every</em> record of a multi-scalar
    /// associated-text sequence, not just the first.
    ///
    /// <para>A <c>TerminalText</c> record carries exactly one <c>Rune</c>, so Kitty reports one
    /// record per colon-separated scalar and a single grapheme can arrive as several
    /// consecutive records. <c>Application.Dispatch</c> handles that with a latch cleared only by
    /// the next Key record, so the whole run drops. The single-record table above cannot tell that
    /// design apart from one that suppresses only the record immediately following the stroke -
    /// which would deliver the base character's combining marks on their own and corrupt the
    /// grapheme.</para>
    /// </summary>
    /// <param name="path">The consume path under test.</param>
    [Theory]
    [MemberData(nameof(ConsumePaths))]
    public async Task Input_WhenStrokeIsConsumedOnAnyPath_SuppressesEveryPairedTextRecordAsync(string path)
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 4)));
        var input = new TextInput { Text = "AB" };
        var root = new Dock();
        root.Children.Add(input);
        await using Application application = new(root, terminal, terminal, TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);
        await application.Dispatcher.InvokeAsync(
            () =>
            {
                application.Focus.Focus(input).ShouldBeTrue();
                Arm(path, input, root);
            },
            TestContext.Current.CancellationToken);

        var stroke = new Stroke(Code.Character, new Rune('a'), nativeCode: 0, Modifiers.Control, KeyAction.Press);
        application.Input(in stroke);
        SendGrapheme(application);
        await application.Dispatcher.InvokeAsync(
            static () => { },
            TestContext.Current.CancellationToken);

        input.Text.ShouldBe("AB", $"the {path} consume path must suppress every paired text record");
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>The counter-case for the multi-record run: unconsumed, all three records arrive and
    /// compose the one grapheme they encode. Suppression that swallowed the tail unconditionally
    /// would still satisfy the theory above.</summary>
    [Fact]
    public async Task Input_WhenStrokeIsNotConsumed_DeliversEveryPairedTextRecordAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 4)));
        var input = new TextInput { Text = "AB" };
        await using Application application = new(input, terminal, terminal, TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);
        await application.Dispatcher.InvokeAsync(
            () => application.Focus.Focus(input).ShouldBeTrue(),
            TestContext.Current.CancellationToken);

        var stroke = new Stroke(Code.Character, new Rune('e'), nativeCode: 0, Modifiers.None, KeyAction.Press);
        application.Input(in stroke);
        SendGrapheme(application);
        await application.Dispatcher.InvokeAsync(
            static () => { },
            TestContext.Current.CancellationToken);

        input.Text.ShouldBe("AB" + _grapheme, "an unconsumed stroke must deliver its whole text run");
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies the latch is armed per keystroke rather than left set: after a consumed
    /// stroke swallows a multi-record run, the very next unconsumed stroke still types. A latch
    /// that failed to clear would go permanently deaf, which is the failure mode a "suppress the
    /// whole run" implementation risks.</summary>
    [Fact]
    public async Task Input_WhenAConsumedRunIsFollowedByAnUnconsumedStroke_ResumesTypingAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 4)));
        var input = new TextInput { Text = "AB" };
        var root = new Dock();
        root.Children.Add(input);
        await using Application application = new(root, terminal, terminal, TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);
        await application.Dispatcher.InvokeAsync(
            () =>
            {
                application.Focus.Focus(input).ShouldBeTrue();

                // Consumes only Control-modified strokes, unlike the table's BubbleHandler arm which
                // consumes every one - the second stroke below has to route unconsumed.
                _ = input.AddHandler(Events.Key, static (_, eventArgs) =>
                {
                    if (eventArgs.Phase == RoutingPhase.Bubble &&
                        eventArgs.Stroke.Modifiers == Modifiers.Control)
                    {
                        eventArgs.IsHandled = true;
                    }
                });
            },
            TestContext.Current.CancellationToken);

        var consumed = new Stroke(Code.Character, new Rune('q'), nativeCode: 0, Modifiers.Control, KeyAction.Press);
        application.Input(in consumed);
        SendGrapheme(application);
        await application.Dispatcher.InvokeAsync(
            static () => { },
            TestContext.Current.CancellationToken);
        input.Text.ShouldBe("AB");

        var free = new Stroke(Code.Character, new Rune('z'), nativeCode: 0, Modifiers.None, KeyAction.Press);
        var freeText = new TerminalText(new Rune('z'));
        application.Input(in free);
        application.Input(in freeText);
        await application.Dispatcher.InvokeAsync(
            static () => { },
            TestContext.Current.CancellationToken);

        input.Text.ShouldBe("ABz", "a new keystroke must clear suppression left by the consumed run");
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    // One grapheme, three scalars, three records - a base letter and two combining marks. Delivering
    // any strict subset of these would leave orphaned marks in the buffer, so a partial suppression
    // bug shows up as visibly wrong text rather than a count that happens to differ.
    private const string _grapheme = "é̂";

    private static void SendGrapheme(Application application)
    {
        foreach (var rune in _grapheme.EnumerateRunes())
        {
            var record = new TerminalText(rune);
            application.Input(in record);
        }
    }

    private static void Arm(string path, TextInput input, ControlBase root)
    {
        switch (path)
        {
            case "PreviewHandler":
                _ = input.AddHandler(Events.Key, static (_, eventArgs) =>
                {
                    if (eventArgs.Phase == RoutingPhase.Preview)
                    {
                        eventArgs.IsHandled = true;
                    }
                });
                break;

            case "BubbleHandler":
                _ = input.AddHandler(Events.Key, static (_, eventArgs) =>
                {
                    if (eventArgs.Phase == RoutingPhase.Bubble)
                    {
                        eventArgs.IsHandled = true;
                    }
                });
                break;

            case "AncestorPreviewHandler":
                // Consumed before the event ever reaches the focused editor, which is the path a
                // tunnelling shell handler takes.
                _ = root.AddHandler(Events.Key, static (_, eventArgs) =>
                {
                    if (eventArgs.Phase == RoutingPhase.Preview)
                    {
                        eventArgs.IsHandled = true;
                    }
                });
                break;

            case "ControlDefault":
                // TextInput's own Ctrl+A select-all default consumes the stroke.
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(path), path, "Unknown consume path.");
        }
    }

    /// <summary>Verifies the public Image control emits its cell fallback before selected graphics.</summary>
    [Fact]
    public async Task StartAsync_WhenImageControlUsesSixel_RendersFallbackBeforeExactPlacementAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(2, 1), new Size(5, 3)));
        var image = new Image
        {
            Source = Rgba(),
            AlternateText = "AL",
            Width = Length.Cells(2),
            Height = Length.Cells(1)
        };
        var profile = TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative with
        {
            Sixel = new Feature(CapabilitySupport.Supported, Origin.Override)
        });
        await using Application application = new(
            image,
            terminal,
            terminal,
            TerminalOptions.Minimal with { Profile = profile });

        await application.StartAsync(TestContext.Current.CancellationToken);

        var bytes = Joined(terminal);
        var fallback = bytes.AsSpan().IndexOf("AL"u8);
        var graphics = bytes.AsSpan().IndexOf("\u001bP0;1;0q"u8);
        fallback.ShouldBeGreaterThanOrEqualTo(0);
        graphics.ShouldBeGreaterThan(fallback);
        bytes.AsSpan().IndexOf("\"1;1;5;3"u8).ShouldBeGreaterThan(graphics);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a public PNG Image reaches explicit iTerm2 multipart after cell fallback.</summary>
    [Fact]
    public async Task StartAsync_WhenPngImageUsesIterm_RendersFallbackBeforeMultipartAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(2, 1)));
        var image = new Image
        {
            Source = Png(),
            AlternateText = "PN",
            Width = Length.Cells(2),
            Height = Length.Cells(1),
            Stretch = ImageStretch.Contain
        };
        await using Application application = new(
            image,
            terminal,
            terminal,
            Options(iterm: true));

        await application.StartAsync(TestContext.Current.CancellationToken);

        var bytes = Joined(terminal);
        var fallback = bytes.AsSpan().IndexOf("PN"u8);
        var multipart = bytes.AsSpan().IndexOf("\u001b]1337;MultipartFile="u8);
        fallback.ShouldBeGreaterThanOrEqualTo(0);
        multipart.ShouldBeGreaterThan(fallback);
        bytes.AsSpan().IndexOf("\u001b]1337;FileEnd"u8).ShouldBeGreaterThan(multipart);
        await application.StopAsync(TestContext.Current.CancellationToken);
        terminal.Disposals.ShouldBe(1);
    }

    /// <summary>
    /// Verifies a placement whose format has no encodable path on any enabled protocol pushes a
    /// GraphicsDiagnostic event instead of leaving the degradation observable only through a
    /// renderer the hosted Application never exposes. RGBA no longer demonstrates this on an
    /// iTerm-only backend - RGBA is PNG-encoded on demand there now - so this uses a PNG this
    /// decoder cannot decode, with only sixel (which does need to decode it) authorized.
    /// </summary>
    [Fact]
    public async Task StartAsync_WhenImageFormatHasNoEncodablePath_RaisesGraphicsDiagnosticAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(2, 1), new Size(5, 3)));
        var image = new Image
        {
            Source = UndecodablePng(),
            AlternateText = "PN",
            Width = Length.Cells(2),
            Height = Length.Cells(1)
        };
        await using Application application = new(
            image,
            terminal,
            terminal,
            Options(sixel: true));
        var diagnosed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        GraphicsDiagnosticEventArgs? received = null;
        application.GraphicsDiagnostic += OnGraphicsDiagnostic;

        await application.StartAsync(TestContext.Current.CancellationToken);
        await diagnosed.Task.WaitAsync(TestContext.Current.CancellationToken);
        application.GraphicsDiagnostic -= OnGraphicsDiagnostic;

        _ = received.ShouldNotBeNull();
        var placement = received.Placements.ShouldHaveSingleItem();
        placement.Reason.ShouldBe(GraphicsPlacementSkipReason.FormatNotEncodable);
        await application.StopAsync(TestContext.Current.CancellationToken);
        return;

        void OnGraphicsDiagnostic(object? sender, GraphicsDiagnosticEventArgs eventArgs)
        {
            _ = sender;
            received = eventArgs;
            _ = diagnosed.TrySetResult();
        }
    }

    /// <summary>Verifies cell fallback is reported before configured frame-boundary promotion.</summary>
    [Fact]
    public async Task StartAsync_WhenGraphicsFallbackIsPromoted_ReportsThenStopsAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(2, 1), new Size(5, 3)));
        var image = new Image
        {
            Source = UndecodablePng(),
            AlternateText = "PN",
            Width = Length.Cells(2),
            Height = Length.Cells(1)
        };
        var options = Options(sixel: true) with
        {
            DiagnosticPromotions = DiagnosticPromotion.Fallback
        };
        await using Application application = new(image, terminal, terminal, options);
        var reported = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        application.GraphicsDiagnostic += (_, _) => reported.TrySetResult();

        await application.StartAsync(TestContext.Current.CancellationToken);
        await reported.Task.WaitAsync(TestContext.Current.CancellationToken);
        var thrown = await Should.ThrowAsync<TerminalDiagnosticException>(async () =>
            await application.Completion.WaitAsync(TestContext.Current.CancellationToken));

        thrown.Promotion.ShouldBe(DiagnosticPromotion.Fallback);
        application.Failure.ShouldBeSameAs(thrown);
    }

    /// <summary>
    /// Verifies a clean frame - every placement encodes normally - never raises GraphicsDiagnostic
    /// at all, matching how the other opportunistic terminal-diagnostic events on Application only
    /// fire on an actual occurrence rather than every frame.
    /// </summary>
    [Fact]
    public async Task StartAsync_WhenFrameHasNoSkippedPlacements_NeverRaisesGraphicsDiagnosticAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(2, 1), new Size(5, 3)));
        var image = new Image
        {
            Source = Rgba(),
            AlternateText = "AL",
            Width = Length.Cells(2),
            Height = Length.Cells(1)
        };
        await using Application application = new(
            image,
            terminal,
            terminal,
            Options(sixel: true));
        var raised = false;
        application.GraphicsDiagnostic += OnGraphicsDiagnostic;
        var rendered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        application.FrameRendered += OnFrameRendered;

        await application.StartAsync(TestContext.Current.CancellationToken);
        await rendered.Task.WaitAsync(TestContext.Current.CancellationToken);
        application.FrameRendered -= OnFrameRendered;
        application.GraphicsDiagnostic -= OnGraphicsDiagnostic;

        raised.ShouldBeFalse();
        await application.StopAsync(TestContext.Current.CancellationToken);
        return;

        void OnGraphicsDiagnostic(object? sender, GraphicsDiagnosticEventArgs eventArgs)
        {
            _ = sender;
            _ = eventArgs;
            raised = true;
        }

        void OnFrameRendered(object? sender, FrameRenderedEventArgs eventArgs)
        {
            _ = sender;
            _ = eventArgs;
            _ = rendered.TrySetResult();
        }
    }

    /// <summary>Verifies a resolved Kitty identity cannot promote tentative graphics capability evidence.</summary>
    [Fact]
    public async Task StartAsync_WhenKittyIdentityOnlyHasTentativeGraphics_PreservesCellFallbackAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(1, 1), new Size(2, 3)));
        var options = TerminalOptions.Minimal with
        {
            Negotiation = new NegotiationOptions(
                new Dictionary<string, string?> { ["TERM"] = "xterm-kitty" },
                limits: QueryLimits.Default with { QueryTimeout = TimeSpan.FromMilliseconds(100) })
        };
        await using Application application = new(
            new Image { Source = Rgba(), AlternateText = "F" },
            terminal,
            terminal,
            options);

        await application.StartAsync(TestContext.Current.CancellationToken);

        var bytes = Joined(terminal);
        bytes.AsSpan().IndexOf("F"u8).ShouldBeGreaterThanOrEqualTo(0);
        bytes.AsSpan().IndexOf("a=t,"u8).ShouldBe(-1);
        bytes.AsSpan().IndexOf("\u001b]1337;MultipartFile="u8).ShouldBe(-1);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies an occluded upper Image transitively blocks an overlapping lower Kitty placement.</summary>
    [Fact]
    public async Task StartAsync_WhenUpperImageFallsBack_BlocksOverlappingLowerKittyPlacementAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(3, 1), new Size(6, 3)));
        var lower = new Image
        {
            Source = GraphicsImage.FromRgba(new Size(1, 1), [255, 0, 0, 255]),
            AlternateText = "AA",
            Width = Length.Cells(2),
            Height = Length.Cells(1)
        };
        var upper = new Image
        {
            Source = GraphicsImage.FromRgba(new Size(1, 1), [0, 255, 0, 255]),
            AlternateText = "BB",
            Width = Length.Cells(2),
            Height = Length.Cells(1)
        };
        var cover = new ControlText("X")
        {
            Width = Length.Cells(1),
            Height = Length.Cells(1)
        };
        var root = new Overlay { Children = { lower, upper, cover } };
        Overlay.SetLeft(upper, Length.Cells(1));
        Overlay.SetLeft(cover, Length.Cells(2));
        await using Application application = new(
            root,
            terminal,
            terminal,
            Options(kitty: true));

        await application.StartAsync(TestContext.Current.CancellationToken);

        var bytes = Joined(terminal);
        bytes.AsSpan().IndexOf("ABX"u8).ShouldBeGreaterThanOrEqualTo(0);
        bytes.AsSpan().IndexOf("a=t,"u8).ShouldBe(-1);
        bytes.AsSpan().IndexOf("a=p,"u8).ShouldBe(-1);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies negotiated graphics selection waits for the profile barrier and receives live exact metrics.</summary>
    [Fact]
    public async Task StartAsync_WhenSixelIsNegotiated_SelectsBackendAfterBarrierWithLiveMetricsAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(2, 1), new Size(5, 3)));
        var options = TerminalOptions.Minimal with
        {
            Negotiation = new NegotiationOptions(
                new Dictionary<string, string?>(),
                new CapabilityOverrides { Sixel = true })
        };
        var queried = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        terminal.Written += value =>
        {
            if (value.Span.IndexOf("\u001b[c"u8) >= 0)
            {
                _ = queried.TrySetResult();
            }
        };
        await using Application application = new(
            new GraphicsProbeControl(Rgba()),
            terminal,
            terminal,
            options);

        var starting = application.StartAsync(TestContext.Current.CancellationToken).AsTask();
        await queried.Task.WaitAsync(TestContext.Current.CancellationToken);
        Joined(terminal).AsSpan().IndexOf("\u001bP0;1;0q"u8).ShouldBe(-1);
        terminal.QueueInput(NegotiationReplies());
        await starting;

        var bytes = Joined(terminal);
        bytes.AsSpan().IndexOf("\u001bP0;1;0q"u8).ShouldBeGreaterThanOrEqualTo(0);
        bytes.AsSpan().IndexOf("\"1;1;5;3"u8).ShouldBeGreaterThanOrEqualTo(0);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies detected multiplexer layers prevent unwrapped graphics when routing is unauthorized.</summary>
    [Fact]
    public async Task StartAsync_WhenLayeredRouteIsUnauthorized_PreservesCellFallbackWithoutDirectLeakAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(1, 1), new Size(2, 3)));
        var policy = new MultiplexingPolicy(
            [MultiplexerKind.Tmux],
            TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative));
        var options = TerminalOptions.Minimal with
        {
            Negotiation = new NegotiationOptions(
                new Dictionary<string, string?>(),
                new CapabilityOverrides { Sixel = true },
                limits: null,
                multiplexing: policy)
        };
        var queried = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        terminal.Written += value =>
        {
            if (value.Span.IndexOf("\u001b[c"u8) >= 0)
            {
                _ = queried.TrySetResult();
            }
        };
        await using Application application = new(
            new GraphicsProbeControl(Rgba()),
            terminal,
            terminal,
            options);

        var starting = application.StartAsync(TestContext.Current.CancellationToken).AsTask();
        await queried.Task.WaitAsync(TestContext.Current.CancellationToken);
        terminal.QueueInput(NegotiationReplies());
        await starting;

        var bytes = Joined(terminal);
        bytes.AsSpan().IndexOf("\u001bP0;1;0q"u8).ShouldBe(-1);
        bytes.AsSpan().IndexOf("\u001bPtmux;"u8).ShouldBe(-1);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a pinned profile - which suppresses startup negotiation entirely - still
    /// routes graphics selection through an explicit Multiplexing policy carried independently of
    /// Negotiation, so an unauthorized route cannot silently degrade into a direct leak just because
    /// the host pinned capabilities to avoid probing.</summary>
    [Fact]
    public async Task StartAsync_WhenProfileIsPinnedWithUnauthorizedMultiplexing_PreservesCellFallbackWithoutDirectLeakAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(1, 1), new Size(2, 3)));
        var policy = new MultiplexingPolicy(
            [MultiplexerKind.Tmux],
            TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative));
        var profile = TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative with
        {
            Sixel = new Feature(CapabilitySupport.Supported, Origin.Override)
        });
        var options = TerminalOptions.Minimal with
        {
            Profile = profile,
            Multiplexing = policy
        };
        await using Application application = new(
            new GraphicsProbeControl(Rgba()),
            terminal,
            terminal,
            options);

        await application.StartAsync(TestContext.Current.CancellationToken);

        var bytes = Joined(terminal);
        bytes.AsSpan().IndexOf("\u001bP0;1;0q"u8).ShouldBe(-1);
        bytes.AsSpan().IndexOf("\u001bPtmux;"u8).ShouldBe(-1);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies Kitty cleanup delete and flush complete before Session disposes transport.</summary>
    [Fact]
    public async Task StopAsync_WhenKittyStateExists_DeletesAndFlushesBeforeTransportDisposalAsync()
    {
        List<string> order = [];
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(1, 1), new Size(2, 3)));
        terminal.Written += value =>
        {
            if (value.Span.IndexOf("a=d,d=N"u8) >= 0)
            {
                order.Add("delete");
            }
        };
        terminal.Flushed += () =>
        {
            if (order.Contains("delete", StringComparer.Ordinal) &&
                !order.Contains("flush", StringComparer.Ordinal))
            {
                order.Add("flush");
            }
        };
        terminal.IsDisposed += () => order.Add("dispose");
        await using Application application = new(
            new GraphicsProbeControl(Rgba()),
            terminal,
            terminal,
            Options(kitty: true));
        await application.StartAsync(TestContext.Current.CancellationToken);

        await application.StopAsync(TestContext.Current.CancellationToken);

        order.ShouldBe(["delete", "flush", "dispose"]);
    }

    /// <summary>Verifies cleanup write failure remains diagnostic and cannot skip transport disposal.</summary>
    [Fact]
    public async Task StopAsync_WhenGraphicsCleanupFails_StillDisposesSessionTransportAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(1, 1), new Size(2, 3)));
        await using Application application = new(
            new GraphicsProbeControl(Rgba()),
            terminal,
            terminal,
            Options(kitty: true));
        await application.StartAsync(TestContext.Current.CancellationToken);
        var failure = new IOException("graphics cleanup failed");
        terminal.FailWriteNumber = terminal.Writes.Count + 1;
        terminal.WriteFailure = failure;

        _ = await Should.ThrowAsync<IOException>(async () =>
            await application.StopAsync(TestContext.Current.CancellationToken));

        terminal.Disposals.ShouldBe(1);
        application.LastCleanupException.ShouldBeSameAs(failure);
    }

    /// <summary>Verifies a later profile revocation removes retained graphics without another upload.</summary>
    [Fact]
    public async Task Profile_WhenKittySupportIsRevoked_RemovesRemoteImageAndStopsGraphicsAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(1, 1), new Size(2, 3)));
        await using Application application = new(
            new Image { Source = Rgba(), AlternateText = "F" },
            terminal,
            terminal,
            Options(kitty: true));
        await application.StartAsync(TestContext.Current.CancellationToken);
        var writeCount = terminal.Writes.Count;
        var rendered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        application.FrameRendered += OnFrameRendered;
        var revoked = TerminalCapabilities.Conservative with
        {
            KittyGraphics = new Feature(CapabilitySupport.Unsupported, Origin.Override)
        };

        application.Profile(revoked);
        await rendered.Task.WaitAsync(TestContext.Current.CancellationToken);
        application.FrameRendered -= OnFrameRendered;

        var bytes = terminal.Writes.Skip(writeCount).SelectMany(static value => value).ToArray();
        bytes.AsSpan().IndexOf("a=d,d=N"u8).ShouldBeGreaterThanOrEqualTo(0);
        bytes.AsSpan().IndexOf("a=t,"u8).ShouldBe(-1);
        bytes.AsSpan().IndexOf("a=p,"u8).ShouldBe(-1);
        application.Capabilities.ShouldBeSameAs(revoked);
        await application.StopAsync(TestContext.Current.CancellationToken);
        return;

        void OnFrameRendered(object? sender, FrameRenderedEventArgs eventArgs)
        {
            _ = sender;
            _ = eventArgs;
            _ = rendered.TrySetResult();
        }
    }

    /// <summary>
    /// Verifies a later profile republish that newly authorizes Kitty support reconsiders the
    /// renderer's frozen backend choice and starts graphics output that never ran at construction.
    /// </summary>
    [Fact]
    public async Task Profile_WhenKittySupportIsGranted_ActivatesGraphicsOutputAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(1, 1), new Size(2, 3)));
        await using Application application = new(
            new Image { Source = Rgba(), AlternateText = "F" },
            terminal,
            terminal,
            Options());
        await application.StartAsync(TestContext.Current.CancellationToken);
        var writeCount = terminal.Writes.Count;
        var rendered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        application.FrameRendered += OnFrameRendered;
        var granted = TerminalCapabilities.Conservative with
        {
            KittyGraphics = new Feature(CapabilitySupport.Supported, Origin.Override)
        };

        application.Profile(granted);
        await rendered.Task.WaitAsync(TestContext.Current.CancellationToken);
        application.FrameRendered -= OnFrameRendered;

        var bytes = terminal.Writes.Skip(writeCount).SelectMany(static value => value).ToArray();
        bytes.AsSpan().IndexOf("a=t,"u8).ShouldBeGreaterThanOrEqualTo(0);
        bytes.AsSpan().IndexOf("a=p,"u8).ShouldBeGreaterThanOrEqualTo(0);
        application.Capabilities.ShouldBeSameAs(granted);
        await application.StopAsync(TestContext.Current.CancellationToken);
        return;

        void OnFrameRendered(object? sender, FrameRenderedEventArgs eventArgs)
        {
            _ = sender;
            _ = eventArgs;
            _ = rendered.TrySetResult();
        }
    }

    /// <summary>Verifies profile revocation waits for an in-flight Kitty transaction to commit.</summary>
    [Fact]
    public async Task Profile_WhenKittyFlushIsPaused_CommitsThenRemovesOnNextFrameAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(1, 1), new Size(2, 3)));
        var image = new Image { Source = Rgba(), AlternateText = "F" };
        await using Application application = new(
            image,
            terminal,
            terminal,
            Options(kitty: true));
        await application.StartAsync(TestContext.Current.CancellationToken);
        var writeCount = terminal.Writes.Count;
        terminal.PauseFlush();
        var uploaded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var rendered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var frameCount = 0;
        terminal.Written += OnWritten;
        application.FrameRendered += OnFrameRendered;

        await application.Dispatcher.InvokeAsync(
            () =>
            {
                image.Source = GraphicsImage.FromRgba(new Size(1, 1), [0, 255, 0, 255]);
            },
            TestContext.Current.CancellationToken);
        await uploaded.Task.WaitAsync(TestContext.Current.CancellationToken);
        var revoked = TerminalCapabilities.Conservative with
        {
            KittyGraphics = new Feature(CapabilitySupport.Unsupported, Origin.Override)
        };
        application.Profile(revoked);
        await application.Dispatcher.InvokeAsync(
            static () => { },
            TestContext.Current.CancellationToken);

        application.Capabilities.ShouldBeSameAs(revoked);
        terminal.ReleaseFlush();
        await rendered.Task.WaitAsync(TestContext.Current.CancellationToken);
        terminal.Written -= OnWritten;
        application.FrameRendered -= OnFrameRendered;

        var bytes = terminal.Writes.Skip(writeCount).SelectMany(static value => value).ToArray();
        var upload = bytes.AsSpan().IndexOf("a=t,"u8);
        var removal = bytes.AsSpan().IndexOf("a=d,d=N"u8);
        upload.ShouldBeGreaterThanOrEqualTo(0);
        removal.ShouldBeGreaterThan(upload);
        await application.StopAsync(TestContext.Current.CancellationToken);
        terminal.Disposals.ShouldBe(1);
        return;

        void OnWritten(ReadOnlyMemory<byte> value)
        {
            if (value.Span.IndexOf("a=t,"u8) >= 0)
            {
                _ = uploaded.TrySetResult();
            }
        }

        void OnFrameRendered(object? sender, FrameRenderedEventArgs eventArgs)
        {
            _ = sender;
            _ = eventArgs;
            frameCount++;

            if (frameCount == 2)
            {
                _ = rendered.TrySetResult();
            }
        }
    }

    /// <summary>Verifies a later profile revocation stops sixel and repairs the cell fallback.</summary>
    [Fact]
    public async Task Profile_WhenSixelSupportIsRevoked_StopsRasterOutputAndRepairsCellsAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(1, 1), new Size(2, 3)));
        await using Application application = new(
            new Image { Source = Rgba(), AlternateText = "F" },
            terminal,
            terminal,
            Options(sixel: true));
        await application.StartAsync(TestContext.Current.CancellationToken);
        var writeCount = terminal.Writes.Count;
        var rendered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        application.FrameRendered += OnFrameRendered;
        var revoked = TerminalCapabilities.Conservative with
        {
            Sixel = new Feature(CapabilitySupport.Unsupported, Origin.Override)
        };

        application.Profile(revoked);
        await rendered.Task.WaitAsync(TestContext.Current.CancellationToken);
        application.FrameRendered -= OnFrameRendered;

        var bytes = terminal.Writes.Skip(writeCount).SelectMany(static value => value).ToArray();
        bytes.ShouldNotBeEmpty();
        bytes.AsSpan().IndexOf("\u001bP0;1;0q"u8).ShouldBe(-1);
        bytes.AsSpan().IndexOf("F"u8).ShouldBeGreaterThanOrEqualTo(0);
        application.Capabilities.ShouldBeSameAs(revoked);
        await application.StopAsync(TestContext.Current.CancellationToken);
        return;

        void OnFrameRendered(object? sender, FrameRenderedEventArgs eventArgs)
        {
            _ = sender;
            _ = eventArgs;
            _ = rendered.TrySetResult();
        }
    }

    private static TerminalOptions Options(
        bool kitty = false,
        bool sixel = false,
        bool iterm = false) => TerminalOptions.Minimal with
        {
            Profile = TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative with
            {
                KittyGraphics = new Feature(
                kitty ? CapabilitySupport.Supported : CapabilitySupport.Unsupported,
                Origin.Override),
                Sixel = new Feature(
                sixel ? CapabilitySupport.Supported : CapabilitySupport.Unsupported,
                Origin.Override),
                ItermImages = new Feature(
                iterm ? CapabilitySupport.Supported : CapabilitySupport.Unsupported,
                Origin.Override)
            })
        };

    private static GraphicsImage Rgba() => GraphicsImage.FromRgba(
        new Size(1, 1),
        [255, 0, 0, 255]);

    private static GraphicsImage Png() => GraphicsImage.FromPng(Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAIAAAACCAIAAAD91JpzAAAAEUlEQVR4nGP4z8DA8B+MgBgAHfAD/dPQfSYAAAAASUVORK5CYII="));

    // Structurally valid with correct chunk CRCs, but its empty IDAT chunk is not valid zlib
    // data, so DecodeRgba (which sixel needs) throws; iTerm2 would still transmit these bytes
    // verbatim, which is exactly why this fixture is paired with a sixel-only backend.
    private static GraphicsImage UndecodablePng() => GraphicsImage.FromPng(Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAAAElEQVQ1rwYeAAAAAElFTkSuQmCC"));

    private static byte[] NegotiationReplies() => Encoding.ASCII.GetBytes(
        "\u001b[?1016;1$y\u001b[?1006;1$y\u001b[?2004;1$y" +
        "\u001b[?1004;1$y\u001b[?2026;1$y\u001b[?3u\u001b[?1;2c");

    private static byte[] Joined(FakeTerminal terminal) =>
        [.. terminal.Writes.SelectMany(static value => value)];

    /// <summary>Verifies disposal before start still disposes the owned host lease exactly once.</summary>
    [Fact]
    public async Task DisposeAsync_WhenNeverStarted_DisposesHostLeaseOnceAsync()
    {
        await using FakeTerminal terminal = new();
        var lease = new TrackingLease();
        var application = new Application(
            new ProbeControl(),
            terminal,
            terminal,
            options: null,
            hostLease: lease);

        await application.DisposeAsync();

        lease.Disposals.ShouldBe(1);
    }

    /// <summary>Verifies the mnemonic focuses the declared target even when an unrelated control
    /// sits between the label and its target in tab order — the exact scenario the default
    /// next-tab-stop behavior gets wrong.</summary>
    [Fact]
    public async Task Input_WhenLabelHasAccessKeyTarget_FocusesTargetDespiteInterveningTabStopAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 4)));
        var unrelated = new TextInput { Text = "unrelated" };
        var target = new TextInput { Text = "target" };
        var label = new ControlText("&Name") { UseMnemonic = true };
        var root = new Stack { Children = { label, unrelated, target } };
        label.AccessKeyTarget = target;
        await using Application application = new(root, terminal, terminal, TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);
        var stroke = Alt('n');

        application.Input(in stroke);
        await application.Dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);

        application.Focus.Focused.ShouldBeSameAs(target);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies an unset target preserves the documented default: focus moves to the next
    /// tab stop after the label.</summary>
    [Fact]
    public async Task Input_WhenLabelHasNoAccessKeyTarget_MovesToNextTabStopAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 4)));
        var next = new TextInput { Text = "next" };
        var label = new ControlText("&Name") { UseMnemonic = true };
        var root = new Stack { Children = { label, next } };
        await using Application application = new(root, terminal, terminal, TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);
        var stroke = Alt('n');

        application.Input(in stroke);
        await application.Dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);

        application.Focus.Focused.ShouldBeSameAs(next);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a disabled target declines the access key deterministically instead of
    /// falling back to tab-stop traversal or throwing.</summary>
    [Fact]
    public async Task Input_WhenAccessKeyTargetIsDisabled_DeclinesWithoutChangingFocusAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 4)));
        var target = new TextInput { Text = "target", IsEnabled = false };
        var next = new TextInput { Text = "next" };
        var label = new ControlText("&Name") { UseMnemonic = true };
        var root = new Stack { Children = { label, target, next } };
        label.AccessKeyTarget = target;
        await using Application application = new(root, terminal, terminal, TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);
        var stroke = Alt('n');

        application.Input(in stroke);
        await application.Dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);

        application.Focus.Focused.ShouldBeNull();
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a target that does not belong to the same tree declines deterministically
    /// rather than throwing.</summary>
    [Fact]
    public async Task Input_WhenAccessKeyTargetIsDetached_DeclinesWithoutThrowingAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 4)));
        var detachedTarget = new TextInput { Text = "detached" };
        var label = new ControlText("&Name") { UseMnemonic = true };
        var root = new Stack { Children = { label } };
        label.AccessKeyTarget = detachedTarget;
        await using Application application = new(root, terminal, terminal, TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);
        var stroke = Alt('n');

        application.Input(in stroke);
        await application.Dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);

        application.Focus.Focused.ShouldBeNull();
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies posted bytes reach the transport while the application is running.</summary>
    [Fact]
    public async Task PostOutOfBand_WhenApplicationRunning_WritesBytesToTransportAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        var bell = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        terminal.Written += memory =>
        {
            if (memory.Span.IndexOf((byte) 0x07) >= 0)
            {
                _ = bell.TrySetResult();
            }
        };
        await using Application application = new(new ProbeControl(), terminal, terminal, TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);

        application.PostOutOfBand(new byte[] { 0x07 });
        await bell.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a render deferred behind an in-flight frame flush still lands once a
    /// successful out-of-band write completes, without waiting for a separate dispatcher idle
    /// tick. CompleteRender prefers draining the out-of-band queue over the deferred render
    /// whenever both are pending, and PumpAfterWrite already re-checks that same deferred render
    /// once the out-of-band write's own completion runs - this pins that existing chain so a
    /// future change cannot silently reintroduce a stranding gap in the common, non-faulting
    /// path.</summary>
    [Fact]
    public async Task FrameRendered_WhenOutOfBandWriteCompletesWhileRenderIsPending_RunsWithoutIdleTickAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(10, 4)));
        var probe = new ProbeControl { Content = "a".AsMemory() };
        await using Application application = new(probe, terminal, terminal, TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);

        terminal.PauseFlush();

        // Content actually changes each time - an invalidate alone can produce a zero-byte diff
        // that the renderer never hands to the transport at all, which would never engage the
        // paused flush this test relies on.
        await application.Dispatcher.InvokeAsync(
            () =>
            {
                probe.Content = "b".AsMemory();
                probe.InvalidateKernel(InvalidationImpact.Render);
            },
            TestContext.Current.CancellationToken);
        await WaitForAsync(() => application.IsRendering, TimeSpan.FromSeconds(5));

        // Only observed from here on: while the first frame's write holds the dispatcher pending,
        // no idle transition can occur at all, so anything caught earlier would be the ordinary
        // idle tick that started this very frame - not evidence of the recovery under test.
        var idleFired = false;
        void IdleHandler(object? sender, EventArgs eventArgs) => idleFired = true;
        application.Dispatcher.Idle += IdleHandler;

        var secondFrame = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var frames = 0;
        application.FrameRendered += (_, _) =>
        {
            frames++;

            if (frames == 2)
            {
                // Unsubscribed from inside the very handler that observes the target frame, still
                // on the dispatcher thread and before this frame's own Dispatcher.Hold releases -
                // the legitimate idle tick that follows full quiescence must not be mistaken for a
                // stranding symptom.
                application.Dispatcher.Idle -= IdleHandler;
                _ = secondFrame.TrySetResult();
            }
        };

        await application.Dispatcher.InvokeAsync(
            () =>
            {
                probe.Content = "c".AsMemory();
                probe.InvalidateKernel(InvalidationImpact.Render);
            },
            TestContext.Current.CancellationToken);
        application.PostOutOfBand(new byte[] { 0x07 });
        terminal.ReleaseFlush();

        await secondFrame.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        frames.ShouldBe(2);
        idleFired.ShouldBeFalse();

        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a render request pending behind an out-of-band write still lands once
    /// that write faults and the failure is handled, without waiting for a separate dispatcher
    /// idle tick. Before the fix, CompleteOutOfBand returned as soon as Report ran, skipping
    /// PumpAfterWrite entirely on this path and leaving the deferred render stranded until an
    /// unrelated invalidation or idle tick happened to notice it.</summary>
    [Fact]
    public async Task FrameRendered_WhenOutOfBandWriteFaultsAndFailureIsHandled_RunsWithoutIdleTickAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(10, 4)));
        var probe = new ProbeControl();
        await using Application application = new(probe, terminal, terminal, TerminalOptions.Minimal);
        application.UnhandledException += (_, eventArgs) => eventArgs.IsHandled = true;
        await application.StartAsync(TestContext.Current.CancellationToken);

        // Targets the very next write - the out-of-band write triggered below is the only thing
        // that writes anything between here and the assertion.
        terminal.WriteFailure = new InvalidOperationException("Injected out-of-band write failure.");
        terminal.FailWriteNumber = terminal.Writes.Count + 1;

        var idleFired = false;
        void IdleHandler(object? sender, EventArgs eventArgs) => idleFired = true;

        var recovered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        application.FrameRendered += (_, _) =>
        {
            // Unsubscribed from inside this very handler, still on the dispatcher thread and
            // before this frame's own Dispatcher.Hold releases - the legitimate idle tick that
            // follows full quiescence must not be mistaken for a stranding symptom.
            application.Dispatcher.Idle -= IdleHandler;
            _ = recovered.TrySetResult();
        };

        // Subscribing to Idle inside the same dispatcher callback that requests the render and
        // posts the out-of-band bytes leaves no gap in which an unrelated idle tick - e.g. the
        // one that would ordinarily follow the first frame settling - could be mistaken for
        // evidence one way or the other.
        await application.Dispatcher.InvokeAsync(
            () =>
            {
                application.Dispatcher.Idle += IdleHandler;

                // A content change (rather than a no-op invalidate) gives the deferred render an
                // actual diff to transmit, so its recovery is also visible on the wire and not
                // just through the FrameRendered event.
                probe.Content = "recovered".AsMemory();
                probe.InvalidateKernel(InvalidationImpact.Render);
                application.PostOutOfBand(new byte[] { 0x07 });
            },
            TestContext.Current.CancellationToken);

        await recovered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        idleFired.ShouldBeFalse();

        // The handled failure is still the application's recorded Failure - by design, it
        // resurfaces from StopAsync once the run completes, even though the run itself kept going
        // and this deferred render still landed. That resurfacing is not what this test is about.
        _ = await Should.ThrowAsync<InvalidOperationException>(
            async () => await application.StopAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>Verifies out-of-band bytes buffered behind an in-flight frame are still flushed to
    /// the wire when a forced stop (a session/transport closure) commits before that frame's write
    /// completes. CompleteRender's stopping branch used to gate the flush on <c>!_stopping</c> with
    /// no fallback, so bytes buffered under a not-yet-stopping guarantee were silently and
    /// permanently stranded once stopping committed first - with <c>Failure</c> and
    /// <c>LastCleanupException</c> both staying null.</summary>
    [Fact]
    public async Task CompleteRender_WhenStopCommitsWhileOutOfBandIsBuffered_FlushesBufferedOutOfBandBytesAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(10, 4)));
        var probe = new ProbeControl { Content = "a".AsMemory() };
        await using Application application = new(probe, terminal, terminal, TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);

        terminal.PauseFlush();

        // A frame render is already in flight (its transport write/flush will not complete until
        // it is released below).
        await application.Dispatcher.InvokeAsync(
            () =>
            {
                probe.Content = "b".AsMemory();
                probe.InvalidateKernel(InvalidationImpact.Render);
            },
            TestContext.Current.CancellationToken);
        await WaitForAsync(() => application.IsRendering, TimeSpan.FromSeconds(5));

        // Buffer an out-of-band write while the render is in flight.
        application.PostOutOfBand(new byte[] { 0x07 });

        // Commit a forced stop (session closure) BEFORE that frame's write completes. Shutdown()
        // funnels through the identical ISink.Closed() path a real transport closure would use.
        application.Shutdown();

        // Guarantee the queued Closed record has already been dispatched (and _stopping latched)
        // before releasing the paused write: Enqueue's dispatcher wake was posted strictly before
        // this InvokeAsync call, so FIFO ordering on the same dispatcher queue guarantees it.
        await application.Dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);

        terminal.ReleaseFlush(); // let the in-flight frame's write succeed

        await application.Completion.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        terminal.Writes.ShouldContain(write => write.Length > 0); // the frame itself landed
        terminal.Writes.ShouldContain(write => write.Length == 1 && write[0] == 0x07); // BEL still sent
        application.Failure.ShouldBeNull();
        application.LastCleanupException.ShouldBeNull();
    }

    /// <summary>Control for
    /// <see cref="CompleteRender_WhenStopCommitsWhileOutOfBandIsBuffered_FlushesBufferedOutOfBandBytesAsync"/>:
    /// identical scaffold but without the concurrent <c>Shutdown()</c> call, isolating the stop
    /// race as the only variable. Out-of-band bytes buffered behind an in-flight frame have always
    /// been flushed correctly once that frame's write completes, in the ordinary (non-racing)
    /// case.</summary>
    [Fact]
    public async Task CompleteRender_WhenNoStopRaces_FlushesBufferedOutOfBandBytesAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(10, 4)));
        var probe = new ProbeControl { Content = "a".AsMemory() };
        await using Application application = new(probe, terminal, terminal, TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);

        terminal.PauseFlush();

        await application.Dispatcher.InvokeAsync(
            () =>
            {
                probe.Content = "b".AsMemory();
                probe.InvalidateKernel(InvalidationImpact.Render);
            },
            TestContext.Current.CancellationToken);
        await WaitForAsync(() => application.IsRendering, TimeSpan.FromSeconds(5));

        application.PostOutOfBand(new byte[] { 0x07 });

        terminal.ReleaseFlush();

        await WaitForAsync(
            () => terminal.Writes.Any(write => write.Length == 1 && write[0] == 0x07),
            TimeSpan.FromSeconds(5));

        application.Failure.ShouldBeNull();
        application.LastCleanupException.ShouldBeNull();

        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies out-of-band bytes buffered behind an in-flight EARLIER out-of-band write
    /// (not a frame render) are still flushed when a forced stop commits before that first
    /// write's flush completes. <c>PumpAfterWrite</c> - the sibling of <c>CompleteRender</c> that
    /// decides what runs next once an out-of-band write retires - used to gate on <c>_stopping</c>
    /// alone and return, silently stranding anything a later <see
    /// cref="Application.PostOutOfBand"/> buffered behind the first write once stopping committed
    /// first, exactly the class of bug <see
    /// cref="CompleteRender_WhenStopCommitsWhileOutOfBandIsBuffered_FlushesBufferedOutOfBandBytesAsync"/>
    /// fixes for a frame render's own in-flight write.</summary>
    [Fact]
    public async Task PumpAfterWrite_WhenStopCommitsWhileOutOfBandIsBuffered_FlushesBufferedOutOfBandBytesAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(10, 4)));
        await using Application application = new(new ProbeControl(), terminal, terminal, TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);

        terminal.PauseFlush();

        // The first out-of-band write starts flushing immediately (nothing else is rendering) and
        // its flush pauses; IsRendering becomes true for the duration, exactly like a frame render.
        application.PostOutOfBand(new byte[] { 0x07 });
        await WaitForAsync(() => application.IsRendering, TimeSpan.FromSeconds(5));

        // A second out-of-band write arrives while the first is still in flight - buffered, not
        // flushed, since DrainOutOfBand's own IsRendering guard defers to PumpAfterWrite below.
        application.PostOutOfBand(new byte[] { 0x08 });

        // Commit a forced stop BEFORE the first write's flush completes.
        application.Shutdown();

        // Guarantee the queued Closed record has already been dispatched (and _stopping latched)
        // before releasing the paused write, same FIFO argument as the frame-render sibling test.
        await application.Dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);

        terminal.ReleaseFlush(); // let the first write's flush succeed (or observe the shutdown cancellation)

        await application.Completion.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        terminal.Writes.ShouldContain(write => write.Length == 1 && write[0] == 0x07); // the first write landed
        terminal.Writes.ShouldContain(write => write.Length == 1 && write[0] == 0x08); // BUG (pre-fix): stranded
        application.Failure.ShouldBeNull();
        application.LastCleanupException.ShouldBeNull();
    }

    // IsRendering has no change notification of its own, so a condition built on it cannot be
    // awaited through an event or a completion source. Polling the flag is the only available
    // signal; the wait stays real wall-clock by necessity, not oversight.
    private static async Task WaitForAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("The awaited condition never became true.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(10));
        }
    }

    /// <summary>Verifies the pointer snapshot is non-null and unobserved before any input.</summary>
    [Fact]
    public async Task Pointer_WhenConstructed_IsNonNullSnapshotAsync()
    {
        await using FakeTerminal terminal = new();
        await using Application application = new(
            new ProbeControl(),
            terminal,
            terminal,
            TerminalOptions.Minimal);

        _ = application.Pointer.ShouldNotBeNull();
        application.Pointer.Position.ShouldBeNull();
        application.HasFocus.ShouldBeTrue();
    }

    /// <summary>Verifies IsRendering returns to false after the dispatcher is disposed mid-render
    /// and the paused transport flush later completes, instead of staying latched true forever.</summary>
    [Fact]
    public async Task IsRendering_WhenDispatcherIsDisposedWhileFlushIsPending_RetiresAfterFlushCompletesAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.PauseFlush();
        terminal.QueueResize(new Dimensions(new Size(10, 4)));
        var application = new Application(
            new ProbeControl(),
            terminal,
            terminal,
            TerminalOptions.Minimal);

        try
        {
            var starting = application.StartAsync(TestContext.Current.CancellationToken).AsTask();

            // Swallow the fault StartAsync will surface once the dispatcher beneath it disposes;
            // the race under test is entirely about IsRendering's own bookkeeping, not startup
            // completion.
            _ = starting.ContinueWith(
                static task => _ = task.Exception,
                TestContext.Current.CancellationToken,
                TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default);

            await WaitForAsync(() => application.IsRendering, TimeSpan.FromSeconds(5));
            application.IsRendering.ShouldBeTrue();

            await application.Dispatcher.DisposeAsync();
            terminal.ReleaseFlush();

            await WaitForAsync(() => !application.IsRendering, TimeSpan.FromSeconds(5));

            application.IsRendering.ShouldBeFalse();
        }
        finally
        {
            // The dispatcher was already disposed directly above to construct the race; a second,
            // orderly Application.DisposeAsync would itself throw ObjectDisposedException trying to
            // hop back onto it, which is expected and irrelevant to the assertion above.
            try
            {
                await application.DisposeAsync();
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }

    /// <summary>Verifies a synchronous Prepare fault on the very first render surfaces through
    /// StartAsync and Failure, and still disposes the transport, instead of leaving _rendering
    /// permanently set and the shutdown drain awaiting an unobservable render task.</summary>
    [Fact]
    public async Task StartAsync_WhenRenderAsyncFaultsSynchronously_CompletesInsteadOfHangingAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(4, 2)));
        var backend = new ThrowingGraphicsBackend();
        await using Application application = new(
            new ProbeControl(),
            terminal,
            terminal,
            TerminalOptions.Minimal);
        application.SeedRenderer(new Renderer(backend));

        var thrown = await Should.ThrowAsync<InvalidOperationException>(
            async () => await application.StartAsync(TestContext.Current.CancellationToken));

        thrown.ShouldBeSameAs(backend.Failure);
        application.Failure.ShouldBeSameAs(backend.Failure);
        terminal.Disposals.ShouldBe(1);
    }

    /// <summary>Verifies out-of-band bytes buffered behind a frame render are still flushed when a
    /// reentrant stop cancels <c>_lifetime</c> synchronously between <c>Root.Render</c> returning and
    /// <c>Renderer.RenderAsync</c>'s own <c>ThrowIfCancellationRequested</c> check observing it - the
    /// StartRender counterpart of <see
    /// cref="CompleteRender_WhenStopCommitsWhileOutOfBandIsBuffered_FlushesBufferedOutOfBandBytesAsync"/>
    /// for the synchronous fault path <see
    /// cref="StartAsync_WhenRenderAsyncFaultsSynchronously_CompletesInsteadOfHangingAsync"/> exercises
    /// with an unrelated exception. StartRender's own <c>RenderAsync</c> catch used to rethrow this
    /// benign, stop-driven cancellation unconditionally instead of swallowing it like
    /// <c>CompleteRender</c> does, and never flushed the bytes it stranded behind - with
    /// <c>Failure</c> and <c>LastCleanupException</c> both staying null throughout.</summary>
    [Fact]
    public async Task StartRender_WhenReentrantStopCancelsBeforeRenderAsyncObservesIt_FlushesBufferedOutOfBandBytesAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(10, 4)));
        var probe = new ProbeControl { Content = "a".AsMemory() };
        await using Application application = new(probe, terminal, terminal, TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);

        // Fires from inside Root.Render (OnRenderContent), synchronously on the dispatcher thread,
        // for the next render this test triggers below. Out-of-band bytes must be buffered before
        // the reentrant stop, since PostOutOfBand discards anything posted once _stopping is
        // already true. Dispatcher.InvokeAsync runs inline when already on the dispatcher thread
        // (see its own remarks), so this StopAsync call's BeginStopping - and the _lifetime.Cancel()
        // inside it - has already committed by the time this hook returns, well before StartRender
        // reaches RenderAsync.
        probe.Rendering = renderingProbe =>
        {
            _ = renderingProbe; // unused: the hook only needs to run on the dispatcher thread
            application.PostOutOfBand(new byte[] { 0x07 });
            _ = application.StopAsync();
        };

        await application.Dispatcher.InvokeAsync(
            () =>
            {
                probe.Content = "b".AsMemory();
                probe.InvalidateKernel(InvalidationImpact.Render);
            },
            TestContext.Current.CancellationToken);

        await application.Completion.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        terminal.Writes.ShouldContain(write => write.Length == 1 && write[0] == 0x07); // BEL still sent
        application.Failure.ShouldBeNull();
        application.LastCleanupException.ShouldBeNull();
    }

    /// <summary>Verifies a shortcut invokes its item without ever reaching Router.Route, so a
    /// focused TextInput neither consumes the chord nor sees it as typed text.</summary>
    [Fact]
    public async Task Input_WhenShortcutMatchesWhileTextInputIsFocused_InvokesItemBeforeRoutingAsync()
    {
        // Arrange
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 4)));
        var input = new TextInput { Text = "seed" };
        var menu = new Menu();
        var gesture = new KeyGesture(Code.Character, Modifiers.Control, new Rune('s'));
        var save = new MenuItem { Text = "Save", Shortcut = gesture };
        menu.Items.Add(save);
        var root = new Stack { Children = { input, menu } };
        var routedKeyPresses = 0;
        _ = root.AddHandler(Events.Key, (_, eventArgs) =>
        {
            if (eventArgs.Phase == RoutingPhase.Preview)
            {
                routedKeyPresses++;
            }
        });
        await using Application application = new(root, terminal, terminal, TerminalOptions.Minimal);
        ActivationCause? cause = null;
        save.Invoked += (_, eventArgs) => cause = eventArgs.Cause;
        await application.StartAsync(TestContext.Current.CancellationToken);
        await application.Dispatcher.InvokeAsync(
            () => application.Focus.Focus(input).ShouldBeTrue(),
            TestContext.Current.CancellationToken);
        var stroke = new Stroke(Code.Character, new Rune('s'), nativeCode: 0, Modifiers.Control, KeyAction.Press);

        // Act
        application.Input(in stroke);
        await application.Dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);

        // Assert
        cause.ShouldBe(ActivationCause.Keyboard);
        routedKeyPresses.ShouldBe(0);
        input.Text.ShouldBe("seed");
        application.Focus.Focused.ShouldBeSameAs(input);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a modifier-less shortcut suppresses its adjacent paired text record, so
    /// the chord fires its command without also typing the character into the focused editor.
    /// Plain printable chords are the leaking case: the terminal reports a stroke and a text
    /// record for the same keystroke, and only the access-key path suppressed the text record
    /// before this fix.</summary>
    [Fact]
    public async Task Input_WhenPlainShortcutMatchesWhileTextInputIsFocused_SuppressesPairedTextAsync()
    {
        // Arrange
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 4)));
        var input = new TextInput { Text = "seed" };
        var menu = new Menu();
        var gesture = new KeyGesture(Code.Character, Modifiers.None, new Rune('q'));
        var quit = new MenuItem { Text = "Quit", Shortcut = gesture };
        menu.Items.Add(quit);
        var root = new Stack { Children = { input, menu } };
        await using Application application = new(root, terminal, terminal, TerminalOptions.Minimal);
        var invocations = 0;
        quit.Invoked += (_, _) => invocations++;
        await application.StartAsync(TestContext.Current.CancellationToken);
        await application.Dispatcher.InvokeAsync(
            () => application.Focus.Focus(input).ShouldBeTrue(),
            TestContext.Current.CancellationToken);
        var stroke = new Stroke(Code.Character, new Rune('q'), nativeCode: 0, Modifiers.None, KeyAction.Press);
        var text = new TerminalText(new Rune('q'));

        // Act
        application.Input(in stroke);
        application.Input(in text);
        await application.Dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);

        // Assert
        invocations.ShouldBe(1);
        input.Text.ShouldBe("seed");
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies an unmatched key still reaches routed handling normally.</summary>
    [Fact]
    public async Task Input_WhenNoShortcutMatches_RoutesTheKeyNormallyAsync()
    {
        // Arrange
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 4)));
        var input = new TextInput();
        var menu = new Menu();
        var save = new MenuItem
        {
            Text = "Save",
            Shortcut = new KeyGesture(Code.Character, Modifiers.Control, new Rune('s'))
        };
        menu.Items.Add(save);
        var root = new Stack { Children = { input, menu } };
        await using Application application = new(root, terminal, terminal, TerminalOptions.Minimal);
        var invocations = 0;
        save.Invoked += (_, _) => invocations++;
        await application.StartAsync(TestContext.Current.CancellationToken);
        await application.Dispatcher.InvokeAsync(
            () => application.Focus.Focus(input).ShouldBeTrue(),
            TestContext.Current.CancellationToken);
        var stroke = new Stroke(Code.Character, new Rune('x'), nativeCode: 0, Modifiers.None, KeyAction.Press);
        var text = new TerminalText(new Rune('x'));

        // Act
        application.Input(in stroke);
        application.Input(in text);
        await application.Dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);

        // Assert
        invocations.ShouldBe(0);
        input.Text.ShouldBe("x");
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies modality is unavailable until initial tree attachment and then inherited before callbacks.</summary>
    [Fact]
    public async Task Modality_WhenReadBeforeFirstResize_ThrowsInvalidOperationExceptionAsync()
    {
        await using FakeTerminal terminal = new();
        var root = new ProbeOwnedControl();
        var observer = new OwnershipObserverControl();
        root.AddPrimary(observer);
        await using Application application = new(
            root,
            terminal,
            terminal,
            TerminalOptions.Minimal);
        ModalityManager? observed = null;
        observer.Attaching = control =>
        {
            observed = application.Modality;
            control.InheritedModalityOwner.ShouldBeSameAs(observed);
        };

        _ = Should.Throw<InvalidOperationException>(() => application.Modality);

        terminal.QueueResize(new Dimensions(new Size(10, 4)));
        await application.StartAsync(TestContext.Current.CancellationToken);

        observed.ShouldBeSameAs(application.Modality);
        application.Modality.Active.ShouldBeNull();
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies application shutdown unwinds modality before pointer and focus without restoring saved focus.</summary>
    [Fact]
    public async Task Dispose_WhenApplicationStops_UnwindsWithoutRestoringFocusAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(12, 4)));
        var root = new ProbeContainer();
        var background = new OwnershipObserverControl { IsFocusable = true };
        var plane = new ProbeContainer();
        var initial = new ProbeControl { IsFocusable = true };
        plane.Children.Add(initial);
        root.Children.Add(background);
        root.Children.Add(plane);
        await using Application application = new(root, terminal, terminal, TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);
        ModalScope? scope = null;
        var backgroundRestorations = 0;
        var exited = 0;

        await application.Dispatcher.InvokeAsync(() =>
        {
            application.Focus.Focus(background).ShouldBeTrue();
            scope = application.Modality.Enter(plane, initialFocus: initial);
            application.Focus.Gained += (_, args) =>
            {
                if (ReferenceEquals(args.Current, background))
                {
                    backgroundRestorations++;
                }
            };
            scope.Exited += (_, _) =>
            {
                exited++;
                background.InheritedFocusOwner.ShouldBeSameAs(application.Focus);
                background.InheritedCaptureOwner.ShouldBeSameAs(application.Capture);
            };
        }, TestContext.Current.CancellationToken);

        await application.StopAsync(TestContext.Current.CancellationToken);

        _ = scope.ShouldNotBeNull();
        scope.IsActive.ShouldBeFalse();
        exited.ShouldBe(1);
        backgroundRestorations.ShouldBe(0);
    }

    /// <summary>Verifies a throwing modal exit cannot skip pointer, focus, root, or application cleanup.</summary>
    [Fact]
    public async Task Dispose_WhenModalExitCallbackThrows_CompletesApplicationCleanupAndPreservesFailureAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(12, 4)));
        var root = new ProbeContainer();
        var plane = new ProbeContainer();
        root.Children.Add(plane);
        await using Application application = new(root, terminal, terminal, TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);
        ModalScope? scope = null;
        var expected = new InvalidOperationException("modal exit failed");

        await application.Dispatcher.InvokeAsync(() =>
        {
            scope = application.Modality.Enter(plane);
            scope.Exited += (_, _) => throw expected;
        }, TestContext.Current.CancellationToken);

        var thrown = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await application.StopAsync(TestContext.Current.CancellationToken));

        thrown.ShouldBeSameAs(expected);
        application.Failure.ShouldBeSameAs(expected);
        application.Completion.IsFaulted.ShouldBeTrue();
        root.IsDisposed.ShouldBeTrue();
        root.FocusOwner.ShouldBeNull();
        root.CaptureOwner.ShouldBeNull();
        root.ModalityOwner.ShouldBeNull();
        _ = scope.ShouldNotBeNull();
        scope.IsActive.ShouldBeFalse();
    }

    /// <summary>Verifies resize preserves the exact modal services, active scope, and focused target.</summary>
    [Fact]
    public async Task Resize_WhenModalScopeIsActive_PreservesServiceScopeAndFocusIdentityAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(12, 4)));
        var root = new ProbeContainer();
        var background = new ProbeControl { IsFocusable = true };
        var plane = new ProbeContainer();
        var initial = new ProbeControl { IsFocusable = true };
        plane.Children.Add(initial);
        root.Children.Add(background);
        root.Children.Add(plane);
        await using Application application = new(root, terminal, terminal, TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);
        ModalScope? scope = null;
        var modality = application.Modality;
        var focus = application.Focus;
        var capture = application.Capture;
        var resized = new TaskCompletionSource<(
            Dimensions Dimensions,
            ModalityManager Modality,
            FocusManager Focus,
            PointerManager Capture,
            ModalScope? Active,
            ControlBase? IsFocused,
            Size Size,
            Rect Bounds)>(TaskCreationOptions.RunContinuationsAsynchronously);

        await application.Dispatcher.InvokeAsync(() =>
        {
            application.Focus.Focus(background).ShouldBeTrue();
            scope = application.Modality.Enter(plane, initialFocus: initial);
            application.Resize += (_, eventArgs) =>
            {
                try
                {
                    _ = resized.TrySetResult((
                        eventArgs.Dimensions,
                        application.Modality,
                        application.Focus,
                        application.Capture,
                        application.Modality.Active,
                        application.Focus.Focused,
                        application.Size,
                        root.Bounds));
                }
                catch (Exception exception)
                {
                    _ = resized.TrySetException(exception);
                }
            };
        }, TestContext.Current.CancellationToken);

        terminal.QueueResize(new Dimensions(new Size(20, 7), new Size(160, 112)));
        var observed = await resized.Task.WaitAsync(
            TimeSpan.FromSeconds(2),
            TestContext.Current.CancellationToken);

        observed.Dimensions.Cells.ShouldBe(new Size(20, 7));
        observed.Modality.ShouldBeSameAs(modality);
        observed.Focus.ShouldBeSameAs(focus);
        observed.Capture.ShouldBeSameAs(capture);
        observed.Active.ShouldBeSameAs(scope);
        observed.IsFocused.ShouldBeSameAs(initial);
        observed.Size.ShouldBe(new Size(20, 7));
        observed.Bounds.ShouldBe(new Rect(0, 0, 20, 7));
        application.Size.ShouldBe(new Size(20, 7));
        application.Modality.ShouldBeSameAs(modality);
        application.Modality.Active.ShouldBeSameAs(scope);
        scope.ShouldNotBeNull().IsActive.ShouldBeTrue();
        application.Focus.Focused.ShouldBeSameAs(initial);
        root.Bounds.ShouldBe(new Rect(0, 0, 20, 7));
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a suspended host resumes with layout before its first positive frame.</summary>
    [Fact]
    public async Task Resize_WhenSuspendedHostBecomesPositive_LayoutsBeforeFrameAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(0, 0)));
        var root = new ProbeControl();
        await using Application application = new(
            root,
            terminal,
            terminal,
            TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);
        List<string> order = [];
        var rendered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        application.Resize += (_, eventArgs) =>
        {
            root.Bounds.Width.ShouldBe(eventArgs.Dimensions.Cells.Width);
            root.Bounds.Height.ShouldBe(eventArgs.Dimensions.Cells.Height);
            order.Add("resize");
        };
        application.FrameRendered += (_, _) =>
        {
            order.Add("frame");
            _ = rendered.TrySetResult();
        };

        terminal.QueueResize(new Dimensions(new Size(12, 5), new Size(96, 80)));
        await rendered.Task.WaitAsync(TestContext.Current.CancellationToken);

        application.Size.ShouldBe(new Size(12, 5));
        order.ShouldBe(["resize", "frame"]);
        terminal.Writes.ShouldNotBeEmpty();
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies an off-thread capabilities profile update propagates a full-queue failure
    /// instead of swallowing it. Profile's Dispatcher.Post guard now catches only
    /// ObjectDisposedException, the genuine shutdown signal; InvalidOperationException means the
    /// queue is transiently full - an ordinary, recoverable load condition unrelated to shutdown -
    /// and must reach whatever off-dispatcher caller triggered the profile update - in practice
    /// SharpVision.Terminal's background read loop - rather than permanently stranding
    /// <c>_profileWake</c> true with no repost ever scheduled.</summary>
    [Fact]
    public async Task Profile_WhenFiredOffThreadWithSaturatedDispatcher_PropagatesInvalidOperationExceptionAsync()
    {
        await using FakeTerminal terminal = new();
        await using Application application = new(new ProbeControl(), terminal, terminal, TerminalOptions.Minimal);
        using var release = await SaturateDispatcherAsync(application.Dispatcher, TestContext.Current.CancellationToken);

        try
        {
            _ = Should.Throw<InvalidOperationException>(() => application.Profile(TerminalCapabilities.Conservative));
        }
        finally
        {
            await ReleaseAndDrainAsync(application.Dispatcher, release, TestContext.Current.CancellationToken);
        }
    }

    /// <summary>Verifies an off-thread ISink.Resize call propagates a full-queue failure instead of
    /// swallowing it, matching Profile's guard for the same reason - both are reachable from
    /// SharpVision.Terminal's background read loop, off the dispatcher thread.</summary>
    [Fact]
    public async Task Resize_WhenFiredOffThreadWithSaturatedDispatcher_PropagatesInvalidOperationExceptionAsync()
    {
        await using FakeTerminal terminal = new();
        await using Application application = new(new ProbeControl(), terminal, terminal, TerminalOptions.Minimal);
        using var release = await SaturateDispatcherAsync(application.Dispatcher, TestContext.Current.CancellationToken);
        var dimensions = new Dimensions(new Size(10, 4));

        try
        {
            _ = Should.Throw<InvalidOperationException>(() => ((ISink) application).Resize(dimensions));
        }
        finally
        {
            await ReleaseAndDrainAsync(application.Dispatcher, release, TestContext.Current.CancellationToken);
        }
    }

    /// <summary>Verifies an off-thread Input call propagates a full-queue failure instead of
    /// swallowing it. Every IProtocolSink.Input overload - and Fault, which enqueues its own record -
    /// funnels through Enqueue's single Dispatcher.Post call site, so this one case covers all of
    /// them.</summary>
    [Fact]
    public async Task Input_WhenFiredOffThreadWithSaturatedDispatcher_PropagatesInvalidOperationExceptionAsync()
    {
        await using FakeTerminal terminal = new();
        await using Application application = new(new ProbeControl(), terminal, terminal, TerminalOptions.Minimal);
        using var release = await SaturateDispatcherAsync(application.Dispatcher, TestContext.Current.CancellationToken);
        var stroke = new Stroke(Code.Character, new Rune('a'), nativeCode: 0, Modifiers.None, KeyAction.Press);

        try
        {
            _ = Should.Throw<InvalidOperationException>(() => application.Input(in stroke));
        }
        finally
        {
            await ReleaseAndDrainAsync(application.Dispatcher, release, TestContext.Current.CancellationToken);
        }
    }

    /// <summary>Verifies an off-thread PostOutOfBand call propagates a full-queue failure instead
    /// of swallowing it. TerminalServices.Request's own comment documents it as "callable from any
    /// thread" on its OSC 52 fallback path, which reaches PostOutOfBand directly - the same
    /// off-thread exposure Profile/Resize/Input have, just via a different entry point.</summary>
    [Fact]
    public async Task PostOutOfBand_WhenFiredOffThreadWithSaturatedDispatcher_PropagatesInvalidOperationExceptionAsync()
    {
        await using FakeTerminal terminal = new();
        await using Application application = new(new ProbeControl(), terminal, terminal, TerminalOptions.Minimal);
        using var release = await SaturateDispatcherAsync(application.Dispatcher, TestContext.Current.CancellationToken);

        try
        {
            _ = Should.Throw<InvalidOperationException>(() => application.PostOutOfBand(new byte[] { 1 }));
        }
        finally
        {
            await ReleaseAndDrainAsync(application.Dispatcher, release, TestContext.Current.CancellationToken);
        }
    }

    /// <summary>Application hard-codes its own dispatcher's capacity via the default
    /// <see cref="Dispatcher.Start"/> parameter and offers no seam to substitute a smaller one for
    /// tests, unlike InputBaseTests' <c>Command_WhenCanExecuteChangedFiresOffThreadWithSaturatedDispatcher_DoesNotThrowAsync</c>,
    /// which starts its own dispatcher with <c>capacity: 1</c>. This mirrors that recipe - block
    /// one in-flight callback, then fill the remainder of the queue - sized to the real hard-coded
    /// capacity instead.</summary>
    private const int _applicationDispatcherCapacity = 4096;

    /// <summary>Blocks <paramref name="dispatcher"/> on one in-flight callback and fills the rest of
    /// its queue to capacity, guaranteeing the next <see cref="Dispatcher.Post(Action)"/> call observes a
    /// full queue. The caller must call <see cref="ManualResetEventSlim.Set"/> on the returned
    /// handle - inside a <c>finally</c> block around its own saturated-queue assertion - before its
    /// application disposes, or the dispatcher thread stays parked on the blocking callback
    /// forever.</summary>
    private static async Task<ManualResetEventSlim> SaturateDispatcherAsync(
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

        // The dispatcher's queue is empty once the blocking callback above is dequeued and
        // running, so filling it with exactly its capacity guarantees the next post observes a
        // full queue - matching DispatcherTests' own recipe for a guaranteed queue-full post.
        for (var i = 0; i < _applicationDispatcherCapacity; i++)
        {
            dispatcher.Post(static () => { });
        }

        return release;
    }

    /// <summary>Releases a handle returned by <see cref="SaturateDispatcherAsync"/> and waits for
    /// the dispatcher to fully drain the filler queue before returning. Without this, the test's
    /// own <c>await using</c> disposal can race ahead of the dispatcher thread still working
    /// through thousands of queued no-ops, hitting the same "queue is full" condition on
    /// <see cref="Application.DisposeAsync"/>'s own unrelated post - not a regression in the guard
    /// under test, just a timing gap in draining <see cref="_applicationDispatcherCapacity"/> items
    /// instead of the single item the borrowed InputBaseTests recipe drains. Deliberately does NOT
    /// use <see cref="Dispatcher.Idle"/>: that event also depends on the dispatcher's unrelated
    /// pending-lease count reaching zero, which a freshly constructed, never-started Application
    /// has no path to satisfy - it would wait forever. Instead this posts one more sentinel action
    /// once the queue has room and awaits it: the queue is strict FIFO, so the sentinel firing
    /// proves every filler ahead of it - the entire original backlog - has already run.</summary>
    private static async Task ReleaseAndDrainAsync(
        Dispatcher dispatcher,
        ManualResetEventSlim release,
        CancellationToken cancellationToken)
    {
        release.Set();

        var drained = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        while (true)
        {
            try
            {
                dispatcher.Post(() => drained.TrySetResult());
                break;
            }
            catch (InvalidOperationException)
            {
                // The queue was still full at post time - the dispatcher thread is actively
                // draining it in the background, so a brief yield and retry is guaranteed to make
                // progress rather than spin indefinitely.
                await Task.Yield();
            }
        }

        await drained.Task.WaitAsync(cancellationToken);
    }

    /// <summary>Never delivers input or a resize, so a session started against it stays live until
    /// its lifetime token cancels — signaling exactly when the resize wait is first reached.</summary>
    private sealed class BlockingResizeTerminal: ITransport, IResizeSource
    {
        private readonly TaskCompletionSource _resizeRequested =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal Task ResizeRequested => _resizeRequested.Task;

        public async ValueTask<int> ReadAsync(Memory<byte> destination, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return 0;
        }

        public ValueTask WriteAsync(ReadOnlyMemory<byte> source, CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;

        public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public async ValueTask<Dimensions> ReadAsync(CancellationToken cancellationToken)
        {
            _ = _resizeRequested.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new UnreachableException();
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    #region Dispatcher fullness

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
            var drained = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            try
            {
                dispatcher.Post(drained.SetResult);
                await drained.Task.WaitAsync(cancellationToken);
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

    // None of the six sites above ever rolled the wake flag back when the Post attempt
    // itself failed with anything other than ObjectDisposedException - so a single transient
    // full-queue trip, survived via a handled UnhandledException or simply outlived once the
    // backlog drained, permanently and silently froze that entire pipeline for the rest of the
    // run: every later, ordinary call saw the flag still latched true and returned without even
    // attempting another Post. The *_WhenDispatcherQueueIsFull_PropagatesInvalidOperationExceptionAsync
    // tests above only ever asserted the exception itself propagates; the tests below extend each
    // one to also prove a later, ordinary call - made only once the dispatcher has genuinely
    // recovered - is still applied instead of swallowed.

    /// <summary>Verifies the profile-wake site recovers from a full-queue trip: once the dispatcher
    /// drains, a later, ordinary <see cref="Application.Profile"/> call is actually applied instead
    /// of the flag staying stuck latched from the failed post.</summary>
    [Fact]
    public async Task Profile_AfterQueueFullTrip_LaterOrdinaryProfileIsAppliedAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(10, 4)));
        var application = new Application(new ProbeControl(), terminal, terminal, TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);
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

        // The dispatcher is now completely healthy again. A brand-new, ordinary profile update
        // arrives.
        var laterProfile = TerminalCapabilities.Conservative with { ColorDepth = ColorDepth.TrueColor };
        Should.NotThrow(() => application.Profile(laterProfile));

        await application.Dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);
        await application.Dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);

        // BUG (pre-fix): the profile pipeline stayed permanently frozen, so this later call never
        // reached DrainProfile and Capabilities never moved off whatever the tree attached with.
        application.Capabilities.ShouldBeSameAs(laterProfile);

        await application.DisposeAsync();
    }

    /// <summary>Verifies the resize-wake site recovers from a full-queue trip: once the dispatcher
    /// drains, a later, ordinary resize is actually applied instead of the flag staying stuck
    /// latched from the failed post.</summary>
    [Fact]
    public async Task Resize_AfterQueueFullTrip_LaterOrdinaryResizeIsAppliedAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(10, 4)));
        var application = new Application(new ProbeControl(), terminal, terminal, TerminalOptions.Minimal);
        application.UnhandledException += static (_, eventArgs) => eventArgs.IsHandled = true;
        await application.StartAsync(TestContext.Current.CancellationToken);
        application.Size.ShouldBe(new Size(10, 4));

        var resizeEvents = 0;
        application.Resize += (_, _) => resizeEvents++;

        var release = await SaturateQueueAsync(application.Dispatcher, TestContext.Current.CancellationToken);
        var trippingResize = new Dimensions(new Size(20, 8));

        try
        {
            _ = Should.Throw<InvalidOperationException>(
                () => ((ISink) application).Resize(in trippingResize));
        }
        finally
        {
            release.Set();
        }

        await WaitForQueueToDrainAsync(application.Dispatcher, TestContext.Current.CancellationToken);

        // The dispatcher is now completely healthy again. A brand-new, ordinary resize arrives.
        var laterResize = new Dimensions(new Size(30, 12));
        Should.NotThrow(() => ((ISink) application).Resize(in laterResize));

        await application.Dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);
        await application.Dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);
        await Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken);

        // BUG (pre-fix): the resize pipeline stayed permanently frozen at its pre-trip size.
        application.Size.ShouldBe(new Size(30, 12));
        resizeEvents.ShouldBeGreaterThan(0);

        await application.DisposeAsync();
    }

    /// <summary>Verifies <c>Enqueue</c>, reached through <see cref="Application.Input(in Stroke)"/>,
    /// recovers from a full-queue trip: once the dispatcher drains, a later, ordinary keystroke is
    /// actually routed instead of the flag staying stuck latched from the failed post.</summary>
    [Fact]
    public async Task Input_AfterQueueFullTrip_LaterOrdinaryInputIsAppliedAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(10, 4)));
        var root = new ProbeControl { IsFocusable = true };
        var observedCodes = new List<Code>();
        _ = root.AddHandler(Events.Key, (_, eventArgs) => observedCodes.Add(eventArgs.Stroke.Code));
        var application = new Application(root, terminal, terminal, TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);
        await application.Dispatcher.InvokeAsync(
            () => application.Focus.Focus(root).ShouldBeTrue(),
            TestContext.Current.CancellationToken);

        var release = await SaturateQueueAsync(application.Dispatcher, TestContext.Current.CancellationToken);
        var trippingStroke = PlainStroke(Code.Enter);

        try
        {
            _ = Should.Throw<InvalidOperationException>(() => application.Input(in trippingStroke));
        }
        finally
        {
            release.Set();
        }

        await WaitForQueueToDrainAsync(application.Dispatcher, TestContext.Current.CancellationToken);

        // The dispatcher is now completely healthy again. A brand-new, ordinary keystroke arrives.
        var laterStroke = PlainStroke(Code.Escape);
        Should.NotThrow(() => application.Input(in laterStroke));

        await application.Dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);
        await application.Dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);

        // BUG (pre-fix): the input pipeline stayed permanently frozen, so neither this later
        // keystroke - nor the tripping one still sitting in _input - was ever routed.
        observedCodes.ShouldContain(Code.Escape);

        await application.DisposeAsync();
    }

    /// <summary>Verifies <c>DrainInput</c>'s own repost recovers from a full-queue trip: once the
    /// application survives the resulting <see cref="Application.UnhandledException"/> report and
    /// the backlog it triggered finishes draining on its own, a later, ordinary keystroke is still
    /// routed instead of the flag staying stuck latched from the failed repost.</summary>
    [Fact]
    public async Task DrainInput_AfterRepostQueueFullTrip_LaterOrdinaryInputIsAppliedAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(10, 4)));
        var root = new ProbeControl { IsFocusable = true };
        var observedCodes = new List<Code>();
        _ = root.AddHandler(Events.Key, (_, eventArgs) => observedCodes.Add(eventArgs.Stroke.Code));
        await using Application application = new(root, terminal, terminal, TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);
        await application.Dispatcher.InvokeAsync(
            () => application.Focus.Focus(root).ShouldBeTrue(),
            TestContext.Current.CancellationToken);

        var innerStroke = PlainStroke(Code.Enter);

        // Same "concurrent Enqueue inside the reset window" seam as
        // DrainInput_WhenRepostFindsQueueFull_SetsApplicationFailureAsync above, but this time the
        // application is left running afterward instead of tearing down.
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
        application.UnhandledException += (_, eventArgs) =>
        {
            eventArgs.IsHandled = true;
            _ = failureObserved.TrySetResult();
        };

        var outerStroke = PlainStroke(Code.Escape);
        application.Input(in outerStroke);

        await failureObserved.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        application.DrainInputRaceHookForTests = null;

        // The backlog the hook filled above drains on its own - nothing in the hook blocks the
        // dispatcher thread, it only fills the queue from inside a callback already running on it.
        await WaitForQueueToDrainAsync(application.Dispatcher, TestContext.Current.CancellationToken);
        observedCodes.Clear();

        // The dispatcher is now completely healthy again. A brand-new, ordinary keystroke arrives.
        var laterStroke = PlainStroke(Code.Tab);
        Should.NotThrow(() => application.Input(in laterStroke));

        await application.Dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);
        await application.Dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);

        // BUG (pre-fix): the input pipeline stayed permanently frozen after the repost trip, so
        // this later keystroke was never routed.
        observedCodes.ShouldContain(Code.Tab);
    }

    /// <summary>Verifies <c>WakeInput</c> recovers from a full-queue trip: once the dispatcher
    /// drains, a later call actually dispatches the record still sitting in <c>_input</c> from the
    /// earlier trip, instead of the flag staying stuck latched from the failed post. Reflection
    /// drives the private method directly, exactly as
    /// <see cref="WakeInput_WhenDispatcherQueueIsFull_PropagatesInvalidOperationExceptionAsync"/>
    /// does above.</summary>
    [Fact]
    public async Task WakeInput_AfterQueueFullTrip_LaterCallDispatchesPendingInputAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(10, 4)));
        var root = new ProbeControl { IsFocusable = true };
        var observedCodes = new List<Code>();
        _ = root.AddHandler(Events.Key, (_, eventArgs) => observedCodes.Add(eventArgs.Stroke.Code));
        var application = new Application(root, terminal, terminal, TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);
        await application.Dispatcher.InvokeAsync(
            () => application.Focus.Focus(root).ShouldBeTrue(),
            TestContext.Current.CancellationToken);

        var release = await SaturateQueueAsync(application.Dispatcher, TestContext.Current.CancellationToken);
        var wakeInput = typeof(Application)
            .GetMethod("WakeInput", BindingFlags.Instance | BindingFlags.NonPublic)!;

        try
        {
            var stroke = PlainStroke(Code.Enter);

            // Enqueue's own post attempt observes the saturated queue and throws here - an
            // incidental exercise of the already-covered Input site - but the record still lands
            // in _input, and this fix's own reset already leaves _inputWake false: exactly
            // WakeInput's precondition, as in the sibling test above.
            _ = Should.Throw<InvalidOperationException>(() => application.Input(in stroke));

            var thrown = Should.Throw<TargetInvocationException>(() => wakeInput.Invoke(application, null));
            _ = thrown.InnerException.ShouldBeOfType<InvalidOperationException>();
        }
        finally
        {
            release.Set();
        }

        await WaitForQueueToDrainAsync(application.Dispatcher, TestContext.Current.CancellationToken);

        // The dispatcher is now completely healthy again, and the record from the tripping Input
        // call above is still sitting in _input, undispatched. A later, ordinary WakeInput call
        // must actually schedule and run the drain instead of finding a permanently stuck latch.
        _ = Should.NotThrow(() => wakeInput.Invoke(application, null));

        await application.Dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);
        await application.Dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);

        // BUG (pre-fix): the input pipeline stayed permanently frozen, so the record from the
        // tripping Input call above was never dispatched.
        observedCodes.ShouldContain(Code.Enter);

        await application.DisposeAsync();
    }

    /// <summary>Verifies <c>PostOutOfBand</c> recovers from a full-queue trip: once the dispatcher
    /// drains, a later, ordinary out-of-band write is actually flushed to the transport instead of
    /// the flag staying stuck latched from the failed post.</summary>
    [Fact]
    public async Task PostOutOfBand_AfterQueueFullTrip_LaterOrdinaryPostIsAppliedAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        var laterWritten = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        terminal.Written += memory =>
        {
            if (memory.Span.IndexOf((byte) 0x08) >= 0)
            {
                _ = laterWritten.TrySetResult();
            }
        };
        var application = new Application(new ProbeControl(), terminal, terminal, TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);

        var release = await SaturateQueueAsync(application.Dispatcher, TestContext.Current.CancellationToken);
        ReadOnlyMemory<byte> trippingBytes = new byte[] { 0x07 };

        try
        {
            _ = Should.Throw<InvalidOperationException>(() => application.PostOutOfBand(trippingBytes));
        }
        finally
        {
            release.Set();
        }

        await WaitForQueueToDrainAsync(application.Dispatcher, TestContext.Current.CancellationToken);

        // The dispatcher is now completely healthy again. A brand-new, ordinary out-of-band write
        // arrives.
        ReadOnlyMemory<byte> laterBytes = new byte[] { 0x08 };
        Should.NotThrow(() => application.PostOutOfBand(laterBytes));

        // BUG (pre-fix): the out-of-band pipeline stayed permanently frozen, so this later write
        // never reached the transport and this would time out instead.
        await laterWritten.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        await application.DisposeAsync();
    }

    /// <summary>
    /// Verifies <c>DisposeTerminalResourcesAsync</c> preserves the first-recorded
    /// <c>TerminalServices.Dispose</c> cleanup failure instead of letting a later
    /// <c>renderer.ShutdownAsync</c> failure silently replace it. The dispatcher queue is kept
    /// saturated for the whole <see cref="TerminalOptions.CleanupTimeout"/> window so the bounded
    /// retry around <c>TerminalServices.Dispose</c> (see
    /// <see cref="DisposeTerminalResourcesAsync_WhenQueueNeverDrainsWithinCleanupTimeout_GivesUpAndRecordsFailureAsync"/>,
    /// its sibling on the give-up side of the same retry) itself gives up and returns a real
    /// <see cref="InvalidOperationException"/> as the first failure - not a substitute or a
    /// reflection-only fake. A live Kitty graphics backend then forces
    /// <c>Renderer.ShutdownAsync</c>'s own remote-cleanup write to fail with a distinct
    /// <see cref="IOException"/> right after, exactly as
    /// <c>StopAsync_WhenGraphicsCleanupFails_StillDisposesSessionTransportAsync</c> proves that
    /// write is genuinely reachable. Before the fix at the renderer catch site, the second failure
    /// unconditionally overwrote the first; this asserts the first failure - the one the method's
    /// own remarks say guards a real armed-<c>DispatcherTimer</c> resource leak - survives.
    /// </summary>
    [Fact]
    public async Task DisposeTerminalResourcesAsync_WhenRendererShutdownAlsoFails_PreservesFirstFailureAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(1, 1), new Size(2, 3)));
        var options = Options(kitty: true) with { CleanupTimeout = TimeSpan.FromMilliseconds(50) };
        var application = new Application(
            new GraphicsProbeControl(Rgba()),
            terminal,
            terminal,
            options);
        await application.StartAsync(TestContext.Current.CancellationToken);

        var rendererFailure = new IOException("renderer shutdown failed");
        terminal.FailWriteNumber = terminal.Writes.Count + 1;
        terminal.WriteFailure = rendererFailure;

        var release = await SaturateQueueAsync(application.Dispatcher, TestContext.Current.CancellationToken);

        try
        {
            var disposeTerminalResources = typeof(Application).GetMethod(
                "DisposeTerminalResourcesAsync",
                BindingFlags.Instance | BindingFlags.NonPublic)!;

            var task = (Task<Exception?>) disposeTerminalResources.Invoke(application, null)!;
            var failure = await task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            _ = failure.ShouldBeOfType<InvalidOperationException>();
            failure.ShouldNotBeSameAs(rendererFailure);

            // DisposeTerminalResourcesAsync assigns LastCleanupException from its own separate
            // fallback chain (lifetimeDiagnostic ?? _renderer?.LastCleanupException ?? ...) before
            // returning failure - the public property must reflect the same preserved first
            // failure the return value does, not silently diverge from it.
            application.LastCleanupException.ShouldBeSameAs(failure);
        }
        finally
        {
            release.Set();
        }

        await WaitForQueueToDrainAsync(application.Dispatcher, TestContext.Current.CancellationToken);
        await application.DisposeAsync();
    }

    #endregion

    #region Lifecycle

    /// <summary>The ordering regression this file exists to pin. A Starting handler that throws
    /// takes the terminal path without <c>BeginStopping</c>, and the host's guarded StopAsync must
    /// not then raise Stopping against an application that has already stopped.</summary>
    [Fact]
    public async Task StopAsync_AfterAStartingHandlerThrew_DoesNotRaiseStoppingAfterStoppedAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        await using Application application = new(
            new ProbeControl(),
            terminal,
            terminal,
            TerminalOptions.Minimal);
        List<string> order = [];
        application.Starting += (_, _) => throw new InvalidOperationException("starting-boom");
        application.Stopping += (_, _) => order.Add("stopping");
        application.Stopped += (_, _) => order.Add("stopped");

        _ = await Should.ThrowAsync<InvalidOperationException>(
            async () => await application.StartAsync(TestContext.Current.CancellationToken));

        // Exactly what ConsoleApplication.RunApplicationAsync does with the rethrow. It must now
        // reach StopAsync's documented rethrow rather than detouring through a second
        // BeginStopping - previously a Stopping handler setting Cancel here returned early and
        // silently suppressed the error the host was about to report.
        var rethrown = await Should.ThrowAsync<InvalidOperationException>(
            async () => await application.StopAsync(CancellationToken.None));

        rethrown.Message.ShouldBe("starting-boom");
        order.ShouldBe(["stopped"]);
    }

    /// <summary>Verifies the second, permanent form of the same divergent state: disposing an
    /// application that was never started also reaches the terminal path directly.</summary>
    [Fact]
    public async Task DisposeAsync_WhenTheApplicationNeverStarted_DoesNotRaiseStoppingAfterStoppedAsync()
    {
        await using FakeTerminal terminal = new();
        Application application = new(new ProbeControl(), terminal, terminal, TerminalOptions.Minimal);
        List<string> order = [];
        application.Stopping += (_, _) => order.Add("stopping");
        application.Stopped += (_, _) => order.Add("stopped");

        await application.DisposeAsync();

        order.ShouldBe(["stopped"]);
    }

    /// <summary>Verifies the guard that documents "stopping or stopped" now rejects a stopped
    /// application, which it could not while the two flags were able to diverge.</summary>
    [Fact]
    public async Task RefreshScreen_WhenCalledFromAStoppedHandler_ThrowsAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        await using Application application = new(
            new ProbeControl(),
            terminal,
            terminal,
            TerminalOptions.Minimal);
        Exception? observed = null;
        application.Starting += (_, _) => throw new InvalidOperationException("starting-boom");
        application.Stopped += (sender, _) =>
        {
            try
            {
                ((Application) sender!).RefreshScreen();
            }
            catch (Exception exception)
            {
                observed = exception;
            }
        };

        _ = await Should.ThrowAsync<InvalidOperationException>(
            async () => await application.StartAsync(TestContext.Current.CancellationToken));

        _ = observed.ShouldBeOfType<ObjectDisposedException>();
    }

    /// <summary>The unrecoverable half of the isolation regression. <c>MarkStarted</c> latches
    /// before it invokes and <c>TrySetResult</c> has no other caller, so a throwing Started handler
    /// used to strand StartAsync on an application that was otherwise fully live.</summary>
    [Fact]
    public async Task StartAsync_WhenAStartedHandlerThrowsAndIsHandled_StillReturnsAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        await using Application application = new(
            new ProbeControl(),
            terminal,
            terminal,
            TerminalOptions.Minimal);
        List<Exception> reported = [];
        application.UnhandledException += (_, eventArgs) =>
        {
            reported.Add(eventArgs.Exception);
            eventArgs.IsHandled = true;
        };
        application.Started += (_, _) => throw new InvalidOperationException("started-boom");

        // Without isolation this never completes: _started is stranded, and because the handler
        // marked the failure handled, _completion never settles either.
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(10));
        await application.StartAsync(timeout.Token);

        reported.Select(static exception => exception.Message).ShouldContain("started-boom");

        // Handling the failure lets the application keep running but does not erase Failure
        // (docs/architecture/error-handling.md), so StopAsync still surfaces it. What this test
        // pins is that StartAsync returned at all.
        var thrown = await Should.ThrowAsync<InvalidOperationException>(
            async () => await application.StopAsync(TestContext.Current.CancellationToken));

        thrown.Message.ShouldBe("started-boom");
    }

    /// <summary>Pins the ordering inside the isolation: the Started-handler failure is reported
    /// through UnhandledException BEFORE StartAsync's awaiter is released. Settling first raced
    /// the report against the caller's continuation, so a test (or application) reading the
    /// failure list right after StartAsync returned could observe it mid-write - the assertion
    /// above flaked exactly that way. The deliberate pause inside the handler makes the inverted
    /// order fail dependably rather than one run in three: with the settle first, StartAsync
    /// resumes during the pause and the flag is already set by the time the handler samples it.</summary>
    [Fact]
    public async Task StartAsync_WhenAStartedHandlerThrows_ReportsTheFailureBeforeCompletingAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        await using Application application = new(
            new ProbeControl(),
            terminal,
            terminal,
            TerminalOptions.Minimal);

        var startReturned = false;
        var sampledAfterReturn = new List<bool>();
        application.UnhandledException += (_, eventArgs) =>
        {
            Thread.Sleep(50);
            sampledAfterReturn.Add(Volatile.Read(ref startReturned));
            eventArgs.IsHandled = true;
        };
        application.Started += (_, _) => throw new InvalidOperationException("started-boom");

        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(10));
        await application.StartAsync(timeout.Token);
        Volatile.Write(ref startReturned, true);

        sampledAfterReturn.ShouldBe([false]);

        var thrown = await Should.ThrowAsync<InvalidOperationException>(
            async () => await application.StopAsync(TestContext.Current.CancellationToken));

        thrown.Message.ShouldBe("started-boom");
    }

    /// <summary>Verifies the self-healing half: a throwing FrameRendered handler must not suppress
    /// the invalidation pump behind it, so an ordinary Invalidate still produces a frame.</summary>
    [Fact]
    public async Task Invalidate_WhenAFrameRenderedHandlerThrowsAndIsHandled_StillRendersTheNextFrameAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        var control = new ProbeControl();
        await using Application application = new(control, terminal, terminal, TerminalOptions.Minimal);
        var frames = 0;
        int? threshold = null;
        var exceededThreshold = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        application.UnhandledException += (_, eventArgs) => eventArgs.IsHandled = true;
        application.FrameRendered += (_, _) =>
        {
            frames++;

            if (threshold is { } value && frames > value)
            {
                _ = exceededThreshold.TrySetResult();
            }

            throw new InvalidOperationException("frame-boom");
        };

        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(10));
        await application.StartAsync(timeout.Token);
        var afterStart = frames;
        threshold = afterStart;

        await application.Dispatcher.InvokeAsync(
            () => control.Invalidate(Invalidation.Render),
            TestContext.Current.CancellationToken);
        await exceededThreshold.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        frames.ShouldBeGreaterThan(afterStart, "the pump behind a throwing handler must still run");
        _ = await Should.ThrowAsync<InvalidOperationException>(
            async () => await application.StopAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>Verifies the suspended-startup path, which reaches MarkStarted through the Resize
    /// callback instead - the same hang with FrameRendered never involved.</summary>
    [Fact]
    public async Task StartAsync_WhenAResizeHandlerThrowsOnASuspendedStart_StillReturnsAsync()
    {
        await using FakeTerminal terminal = new();

        // A zero axis is the suspended branch: layout runs, no frame is produced, and MarkStarted
        // is reached from DrainResize rather than from CompleteRender.
        terminal.QueueResize(new Dimensions(new Size(0, 6)));
        await using Application application = new(
            new ProbeControl(),
            terminal,
            terminal,
            TerminalOptions.Minimal);
        application.UnhandledException += (_, eventArgs) => eventArgs.IsHandled = true;
        application.Resize += (_, _) => throw new InvalidOperationException("resize-boom");

        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(10));
        await application.StartAsync(timeout.Token);

        var thrown = await Should.ThrowAsync<InvalidOperationException>(
            async () => await application.StopAsync(TestContext.Current.CancellationToken));

        thrown.Message.ShouldBe("resize-boom");
    }

    /// <summary>The counter-case that keeps the isolation honest: with no handler throwing, the
    /// documented frame-before-started ordering is unchanged.</summary>
    [Fact]
    public async Task StartAsync_WhenNoHandlerThrows_StillRaisesStartedAfterTheFirstFrameAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        await using Application application = new(
            new ProbeControl(),
            terminal,
            terminal,
            TerminalOptions.Minimal);
        List<string> order = [];
        application.FrameRendered += (_, _) => order.Add("frame");
        application.Started += (_, _) => order.Add("started");

        await application.StartAsync(TestContext.Current.CancellationToken);

        order.IndexOf("frame").ShouldBeLessThan(order.IndexOf("started"));
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    #endregion

    #region Event ordering

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

    /// <summary>Verifies a control invalidated directly from an <c>Idle</c> handler - an ordinary
    /// property mutation, not queued dispatcher work - is rendered promptly instead of being
    /// stranded until unrelated dispatcher work happens to arrive and re-arm idle detection.
    /// </summary>
    [Fact]
    public async Task Idle_WhenHandlerInvalidatesAControl_RendersPromptlyAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(10, 4)));
        var probe = new ProbeControl { Content = "a".AsMemory() };
        await using Application application = new(probe, terminal, terminal, TerminalOptions.Minimal);
        var frames = 0;
        var idles = 0;
        var secondFrame = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        application.FrameRendered += (_, _) =>
        {
            frames++;

            if (frames == 2)
            {
                _ = secondFrame.TrySetResult();
            }
        };
        application.Idle += (_, _) =>
        {
            idles++;

            if (idles == 1)
            {
                // An ordinary, idiomatic mutation performed from Idle - not queuing more dispatcher
                // work, just invalidating a control the way any property setter would.
                probe.Content = "b".AsMemory();
                probe.InvalidateKernel(InvalidationImpact.Render);
            }
        };

        await application.StartAsync(TestContext.Current.CancellationToken);

        // With nothing else happening on the dispatcher, the invalidation raised from inside the
        // first Idle callback must still reach a render without waiting for unrelated dispatcher
        // work to arrive and re-arm idle detection.
        await secondFrame.Task.WaitAsync(TestContext.Current.CancellationToken);

        frames.ShouldBe(2);
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

    #endregion

    #region Render dispatcher fullness

    /// <summary>Saturates <paramref name="dispatcher"/>'s bounded queue behind a blocked hostage
    /// callback, tracking completion of whichever filler lands in the one slot the queue actually
    /// grants - the rest never get the chance to run.</summary>
    /// <param name="dispatcher">The dispatcher to saturate.</param>
    /// <param name="cancellationToken">Cancels waiting for the hostage to start running.</param>
    /// <returns>The hostage release handle and a completion source that settles once the specific
    /// filler that claimed the queue's one free slot actually runs.</returns>
    private static async Task<(ManualResetEventSlim Release, TaskCompletionSource FillerDrained)>
        SaturateQueueTrackingLastFillerAsync(Dispatcher dispatcher, CancellationToken cancellationToken)
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new ManualResetEventSlim();

        dispatcher.Post(() =>
        {
            entered.SetResult();
            release.Wait();
        });

        await entered.Task.WaitAsync(cancellationToken);

        var fillerDrained = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        try
        {
            while (true)
            {
                dispatcher.Post(() => fillerDrained.TrySetResult());
            }
        }
        catch (InvalidOperationException)
        {
        }

        return (release, fillerDrained);
    }

    /// <summary>Verifies the render-completion post's bridging retry - given a genuine chance to
    /// succeed once the saturated slot frees, exactly as a live dispatcher queue drains in
    /// practice - reaches <see cref="Dispatcher.UnhandledException"/> with the original
    /// "queue is full" failure, the same outcome a synchronous dispatcher-callback failure already
    /// produces.</summary>
    [Fact]
    public async Task ObserveRenderAsync_WhenCompleteRenderPostFindsQueueFullThenFrees_BridgesToUnhandledExceptionAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.PauseFlush();
        terminal.QueueResize(new Dimensions(new Size(10, 4)));
        var application = new Application(
            new ProbeControl(),
            terminal,
            terminal,
            TerminalOptions.Minimal);

        var starting = application.StartAsync(TestContext.Current.CancellationToken).AsTask();

        // The paused flush keeps the very first render in flight indefinitely, so StartAsync
        // never completes along this path; observe its eventual outcome instead of awaiting it.
        _ = starting.ContinueWith(
            static task => _ = task.Exception,
            TestContext.Current.CancellationToken,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);

        await WaitForAsync(() => application.IsRendering, TimeSpan.FromSeconds(5));

        var (hostageRelease, fillerDrained) = await SaturateQueueTrackingLastFillerAsync(
            application.Dispatcher,
            TestContext.Current.CancellationToken);

        // Frees the one saturated slot deterministically, in the otherwise nanosecond-wide window
        // between the first (failed) attempt and the bridging retry, instead of racing a genuine
        // drain: releasing the hostage lets the dispatcher thread dequeue and run fillers until it
        // reaches the one this test is tracking, which signals fillerDrained the moment it does,
        // before the retry ever attempts to post.
        application.Dispatcher.BackgroundCompletionRetryHookForTests = () =>
        {
            hostageRelease.Set();
            _ = fillerDrained.Task.Wait(TimeSpan.FromSeconds(5));
        };

        var unhandled = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        application.Dispatcher.UnhandledException += (_, eventArgs) => unhandled.TrySetResult(eventArgs.Exception);

        // Unblocks the paused transport flush; RenderAsync's remaining awaits resume off the
        // dispatcher thread (ConfigureAwait(false) throughout), so ObserveRenderAsync's own
        // completion post below runs against the queue this test just saturated.
        terminal.ReleaseFlush();

        var reported = await unhandled.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        reported.ShouldBeOfType<InvalidOperationException>().Message.ShouldBe("The dispatcher queue is full.");
        await WaitForAsync(() => !application.IsRendering, TimeSpan.FromSeconds(5));

        await WaitForQueueToDrainAsync(application.Dispatcher, TestContext.Current.CancellationToken);
        await application.DisposeAsync();
    }

    /// <summary>Verifies the render-completion post's bridging retry, when it is also rejected for
    /// a full queue - the queue never drains at all in this scenario - drops the fault as the
    /// documented, accepted edge instead of retrying indefinitely, while still retiring
    /// <see cref="Application.IsRendering"/> so the shutdown drain never wedges.</summary>
    [Fact]
    public async Task ObserveRenderAsync_WhenCompleteRenderPostFindsQueueFullOnBothAttempts_DropsTheFaultAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.PauseFlush();
        terminal.QueueResize(new Dimensions(new Size(10, 4)));
        var application = new Application(
            new ProbeControl(),
            terminal,
            terminal,
            TerminalOptions.Minimal);

        var starting = application.StartAsync(TestContext.Current.CancellationToken).AsTask();

        _ = starting.ContinueWith(
            static task => _ = task.Exception,
            TestContext.Current.CancellationToken,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);

        await WaitForAsync(() => application.IsRendering, TimeSpan.FromSeconds(5));

        var release = await SaturateQueueAsync(application.Dispatcher, TestContext.Current.CancellationToken);

        var unhandledObserved = false;
        application.Dispatcher.UnhandledException += (_, _) => unhandledObserved = true;

        terminal.ReleaseFlush();

        // Nothing ever frees the saturated slot, so both the original attempt and the bridging
        // retry observe the same full queue; give the off-thread continuation a moment to reach
        // and exhaust both before asserting the drop.
        await WaitForAsync(() => !application.IsRendering, TimeSpan.FromSeconds(5));

        unhandledObserved.ShouldBeFalse();
        application.Failure.ShouldBeNull();

        release.Set();
        await WaitForQueueToDrainAsync(application.Dispatcher, TestContext.Current.CancellationToken);
        await application.DisposeAsync();
    }

    /// <summary>Invokes the private, parameterless <c>FlushStrandedOutOfBandAsync</c> on
    /// <paramref name="application"/> via reflection and awaits it.</summary>
    private static Task InvokeFlushStrandedOutOfBandAsync(Application application) =>
        (Task) typeof(Application)
            .GetMethod("FlushStrandedOutOfBandAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(application, null)!;

    /// <summary>Sets <paramref name="application"/>'s private <c>_stopping</c> field via
    /// reflection - no public API commits a forced stop without itself posting through
    /// <c>Enqueue</c>, and reliably forcing the abandonment fallback itself to run with
    /// <c>_stopping</c> already true needs winning a race between an in-flight operation's own
    /// cancellation-driven resumption and dispatcher-queue saturation that has no deterministic
    /// public hook.</summary>
    private static void SetStoppingDirectly(Application application, bool value) =>
        typeof(Application)
            .GetField("_stopping", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(application, value);

    /// <summary>Buffers <paramref name="bytes"/> directly into <paramref name="application"/>'s
    /// private <c>_outOfBand</c> writer, bypassing <see cref="Application.PostOutOfBand"/> - which
    /// refuses to buffer anything once <c>_stopping</c> is set, exactly the state these tests need
    /// bytes buffered under with nothing else racing to drain them first. <c>_outOfBand</c>'s
    /// declared field type (<see cref="ArrayBufferWriter{T}"/>) is reflection-only
    /// here, but the <see cref="IBufferWriter{T}"/> interface it implements - and the
    /// <c>Write</c> extension method on it - are public, so the write itself is a normal,
    /// non-reflected call.</summary>
    private static void BufferOutOfBandBytesDirectly(Application application, byte[] bytes)
    {
        var outOfBand = (IBufferWriter<byte>) typeof(Application)
            .GetField("_outOfBand", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(application)!;

        outOfBand.Write(bytes);
    }

    /// <summary>Verifies <c>FlushStrandedOutOfBandAsync</c> - the fallback
    /// <c>ObserveRenderAsync</c>'s and <c>ObserveOutOfBandAsync</c>'s own abandonment cleanup runs
    /// when <c>Dispatcher.PostBackgroundCompletionAsync</c> cannot run the real completion - writes
    /// out-of-band bytes still buffered at that point instead of silently stranding them. Before
    /// this fix, that cleanup only retired <see cref="Application.IsRendering"/> and the
    /// render/write's own resources, leaving anything <see cref="Application.PostOutOfBand"/> had
    /// buffered behind the failed operation permanently lost - the same class of bug
    /// <c>CompleteRender</c>'s and <c>PumpAfterWrite</c>'s own stopping branches were already fixed
    /// for (see <see cref="CompleteRender_WhenStopCommitsWhileOutOfBandIsBuffered_FlushesBufferedOutOfBandBytesAsync"/>),
    /// just reached through an abandoned background completion instead of a cancelled write.
    /// </summary>
    [Fact]
    public async Task FlushStrandedOutOfBandAsync_WhenStoppingWithBytesBuffered_WritesThemAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(10, 4)));
        await using Application application = new(new ProbeControl(), terminal, terminal, TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);

        BufferOutOfBandBytesDirectly(application, [0x07]);
        SetStoppingDirectly(application, true);

        await InvokeFlushStrandedOutOfBandAsync(application);

        terminal.Writes.ShouldContain(write => write.Length == 1 && write[0] == 0x07);
        application.LastCleanupException.ShouldBeNull();

        // Restore the field this test latched directly so the application's own real stop
        // sequence - not this test's reflection shortcut - drives normal disposal below.
        SetStoppingDirectly(application, false);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a write failure inside <c>FlushStrandedOutOfBandAsync</c> is folded into
    /// <see cref="Application.LastCleanupException"/> - this codebase's convention for shutdown-path
    /// diagnostics, since <c>Report</c> exists for a still running application and no longer applies
    /// once <c>_stopping</c> is set - rather than silently discarded.</summary>
    [Fact]
    public async Task FlushStrandedOutOfBandAsync_WhenWriteFails_RecordsLastCleanupExceptionAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(10, 4)));
        await using Application application = new(new ProbeControl(), terminal, terminal, TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);

        var failure = new InvalidOperationException("Injected out-of-band write failure.");
        terminal.FailWriteNumber = terminal.Writes.Count + 1;
        terminal.WriteFailure = failure;

        BufferOutOfBandBytesDirectly(application, [0x07]);
        SetStoppingDirectly(application, true);

        await InvokeFlushStrandedOutOfBandAsync(application);

        application.LastCleanupException.ShouldBeSameAs(failure);

        SetStoppingDirectly(application, false);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies the out-of-band-completion post's bridging retry - the sibling site to
    /// <see cref="ObserveRenderAsync_WhenCompleteRenderPostFindsQueueFullThenFrees_BridgesToUnhandledExceptionAsync"/>
    /// covered above, exercised through <c>Application.PostOutOfBand</c> directly instead of a
    /// frame render - also reaches <see cref="Dispatcher.UnhandledException"/> once the saturated
    /// slot frees.</summary>
    [Fact]
    public async Task ObserveOutOfBandAsync_WhenCompleteOutOfBandPostFindsQueueFullThenFrees_BridgesToUnhandledExceptionAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(10, 4)));
        await using Application application = new(
            new ProbeControl(),
            terminal,
            terminal,
            TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);

        terminal.PauseFlush();
        ReadOnlyMemory<byte> bytes = new byte[] { 1, 2, 3 };
        await application.Dispatcher.InvokeAsync(
            () => application.PostOutOfBand(bytes),
            TestContext.Current.CancellationToken);

        await WaitForAsync(() => application.IsRendering, TimeSpan.FromSeconds(5));

        var (hostageRelease, fillerDrained) = await SaturateQueueTrackingLastFillerAsync(
            application.Dispatcher,
            TestContext.Current.CancellationToken);

        application.Dispatcher.BackgroundCompletionRetryHookForTests = () =>
        {
            hostageRelease.Set();
            _ = fillerDrained.Task.Wait(TimeSpan.FromSeconds(5));
        };

        var unhandled = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        application.Dispatcher.UnhandledException += (_, eventArgs) => unhandled.TrySetResult(eventArgs.Exception);

        terminal.ReleaseFlush();

        var reported = await unhandled.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        reported.ShouldBeOfType<InvalidOperationException>().Message.ShouldBe("The dispatcher queue is full.");
        await WaitForAsync(() => !application.IsRendering, TimeSpan.FromSeconds(5));

        await WaitForQueueToDrainAsync(application.Dispatcher, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies the out-of-band-completion post's bridging retry, when it is also
    /// rejected for a full queue, drops the fault instead of retrying indefinitely - the sibling
    /// edge case to the render-completion site's own double-failure drop above.</summary>
    [Fact]
    public async Task ObserveOutOfBandAsync_WhenCompleteOutOfBandPostFindsQueueFullOnBothAttempts_DropsTheFaultAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(10, 4)));
        await using Application application = new(
            new ProbeControl(),
            terminal,
            terminal,
            TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);

        terminal.PauseFlush();
        ReadOnlyMemory<byte> bytes = new byte[] { 1, 2, 3 };
        await application.Dispatcher.InvokeAsync(
            () => application.PostOutOfBand(bytes),
            TestContext.Current.CancellationToken);

        await WaitForAsync(() => application.IsRendering, TimeSpan.FromSeconds(5));

        var release = await SaturateQueueAsync(application.Dispatcher, TestContext.Current.CancellationToken);

        var unhandledObserved = false;
        application.Dispatcher.UnhandledException += (_, _) => unhandledObserved = true;

        terminal.ReleaseFlush();

        await WaitForAsync(() => !application.IsRendering, TimeSpan.FromSeconds(5));

        unhandledObserved.ShouldBeFalse();
        application.Failure.ShouldBeNull();

        release.Set();
        await WaitForQueueToDrainAsync(application.Dispatcher, TestContext.Current.CancellationToken);
    }

    #endregion

    #region Response validation

    /// <summary>Verifies an empty <see cref="PaletteResponse"/> is rejected synchronously instead
    /// of being enqueued and only failing later during dispatch.</summary>
    [Fact]
    public async Task Response_WhenPaletteResponseIsEmpty_ThrowsArgumentExceptionAsync()
    {
        await using FakeTerminal terminal = new();
        var application = new Application(new ProbeControl(), terminal, terminal, TerminalOptions.Minimal);

        _ = Should.Throw<ArgumentException>(() => application.Response(default(PaletteResponse)));

        await application.DisposeAsync();
    }

    /// <summary>Verifies an empty <see cref="MetricsResponse"/> is rejected synchronously instead
    /// of being enqueued and only failing later during dispatch.</summary>
    [Fact]
    public async Task Response_WhenMetricsResponseIsEmpty_ThrowsArgumentExceptionAsync()
    {
        await using FakeTerminal terminal = new();
        var application = new Application(new ProbeControl(), terminal, terminal, TerminalOptions.Minimal);

        _ = Should.Throw<ArgumentException>(() => application.Response(default(MetricsResponse)));

        await application.DisposeAsync();
    }

    #endregion
}
