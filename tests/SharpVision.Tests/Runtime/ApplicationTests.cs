// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Runtime;

using Input;

using SharpVision.Tests.Controls;

using TerminalCapabilities = Capabilities;

/// <summary>Verifies application startup, frame completion, suspension, and shutdown.</summary>
public sealed class ApplicationTests
{
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
                application.Theme = Themes.White;
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
                eventArgs.Handled = true;
            }
        });
        _ = root.AddHandler(
            Events.Key,
            (_, eventArgs) =>
            {
                if (IsControlCharacter(eventArgs, 'c') && eventArgs.Handled)
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
            eventArgs.Phase == Phase.Preview &&
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
            if (eventArgs.Phase == Phase.Preview &&
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
                    if (eventArgs.Phase == Phase.Preview &&
                        eventArgs.Stroke.Character == new Rune('c') &&
                        eventArgs.Handled)
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

                result.Handled.ShouldBeFalse();
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
        var root = new ProbeControl { Focusable = true };
        var phases = new List<Phase>();
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

        phases.ShouldBe([Phase.Preview, Phase.Bubble]);
        await application.StopAsync(TestContext.Current.CancellationToken);
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
}
