// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Runtime;

/// <summary>Verifies application-owned modality construction and shutdown lifetime.</summary>
public sealed class ApplicationModalityTests
{
    #region Lifetime

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
        var background = new OwnershipObserverControl { Focusable = true };
        var plane = new ProbeContainer();
        var initial = new ProbeControl { Focusable = true };
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
        var background = new ProbeControl { Focusable = true };
        var plane = new ProbeContainer();
        var initial = new ProbeControl { Focusable = true };
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
            Control? Focused,
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
        observed.Focused.ShouldBeSameAs(initial);
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

    #endregion

    #region Modal dispatch

    /// <summary>Verifies an eligible focused control remains the key and terminal-focus route target.</summary>
    [Fact]
    public async Task Dispatch_WhenModalFocusIsAllowed_RoutesKeyAndTerminalFocusToFocusedControlAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(12, 4)));
        var root = new ProbeContainer();
        var plane = new ProbeContainer();
        var focused = new ProbeControl { Focusable = true };
        plane.Children.Add(focused);
        root.Children.Add(plane);
        var routes = new List<string>();
        Record(root, "root");
        Record(plane, "plane");
        Record(focused, "focused");
        await using Application application = new(root, terminal, terminal, TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);
        await application.Dispatcher.InvokeAsync(
            () =>
            {
                _ = application.Modality.Enter(plane, initialFocus: focused);
            },
            TestContext.Current.CancellationToken);
        var stroke = new Stroke(
            Code.Enter,
            character: null,
            nativeCode: 0,
            Modifiers.None,
            KeyAction.Press);
        var gainedFocus = new TerminalFocus(gained: true);

        application.Input(in stroke);
        application.Input(in gainedFocus);
        await application.Dispatcher.InvokeAsync(
            static () => { },
            TestContext.Current.CancellationToken);

        routes.ShouldBe([
            "focused-key",
            "plane-key",
            "focused-focus",
            "plane-focus",
        ]);
        await application.StopAsync(TestContext.Current.CancellationToken);
        return;

        void Record(Control control, string name)
        {
            _ = control.AddHandler(Events.Key, (_, eventArgs) =>
            {
                if (eventArgs.Phase == RoutingPhase.Bubble)
                {
                    routes.Add($"{name}-key");
                }
            });
            _ = control.AddHandler(Events.TerminalFocusChanged, (_, eventArgs) =>
            {
                if (eventArgs.Phase == RoutingPhase.Bubble)
                {
                    routes.Add($"{name}-focus");
                }
            });
        }
    }

    /// <summary>Verifies modal text and paste records reach an eligible focused editor.</summary>
    [Fact]
    public async Task Dispatch_WhenModalFocusIsAllowed_RoutesTextAndPasteToFocusedControlAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(12, 4)));
        var input = new TextInput();
        var plane = new Stack { Children = { input } };
        var root = new Stack { Children = { plane } };
        var routes = new List<string>();
        Record(root, "root");
        Record(plane, "plane");
        Record(input, "input");
        await using Application application = new(root, terminal, terminal, TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);
        await application.Dispatcher.InvokeAsync(
            () =>
            {
                _ = application.Modality.Enter(plane, initialFocus: input);
            },
            TestContext.Current.CancellationToken);
        var text = new Terminal.Input.Text(new Rune('x'));

        application.Input(in text);
        application.Input(new Paste("y"u8));
        await application.Dispatcher.InvokeAsync(
            static () => { },
            TestContext.Current.CancellationToken);

        input.Text.ShouldBe("xy");
        routes.ShouldBe([
            "plane-text-Preview",
            "input-text-Preview",
            "input-text-Bubble",
            "plane-paste-Preview",
            "input-paste-Preview",
            "input-paste-Bubble",
        ]);
        await application.StopAsync(TestContext.Current.CancellationToken);
        return;

        void Record(Control control, string name)
        {
            _ = control.AddHandler(
                Events.Text,
                (_, eventArgs) => routes.Add($"{name}-text-{eventArgs.Phase}"));
            _ = control.AddHandler(
                Events.Paste,
                (_, eventArgs) => routes.Add($"{name}-paste-{eventArgs.Phase}"));
        }
    }

    /// <summary>Verifies rejected background focus leaves key and terminal-focus fallback on the modal root.</summary>
    [Fact]
    public async Task Dispatch_WhenBackgroundFocusIsRejected_RoutesKeyAndTerminalFocusToPrimaryRootAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(12, 4)));
        var root = new ProbeContainer();
        var background = new ProbeControl { Focusable = true };
        var plane = new ProbeContainer();
        root.Children.Add(background);
        root.Children.Add(plane);
        var routes = new List<string>();
        Record(root, "root");
        Record(background, "background");
        Record(plane, "plane");
        await using Application application = new(root, terminal, terminal, TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);
        await application.Dispatcher.InvokeAsync(() =>
        {
            _ = application.Modality.Enter(plane);
            application.Focus.Focused.ShouldBeNull();
        }, TestContext.Current.CancellationToken);
        var stroke = new Stroke(
            Code.Enter,
            character: null,
            nativeCode: 0,
            Modifiers.None,
            KeyAction.Press);
        var lostFocus = new TerminalFocus(gained: false);

        application.Input(in stroke);
        application.Input(in lostFocus);
        await application.Dispatcher.InvokeAsync(
            static () => { },
            TestContext.Current.CancellationToken);
        await application.Dispatcher.InvokeAsync(
            () =>
            {
                application.Focus.Focus(background).ShouldBeFalse();
                application.Focus.Focused.ShouldBeNull();
            },
            TestContext.Current.CancellationToken);
        var gainedFocus = new TerminalFocus(gained: true);
        application.Input(in stroke);
        application.Input(in gainedFocus);
        await application.Dispatcher.InvokeAsync(
            static () => { },
            TestContext.Current.CancellationToken);

        routes.ShouldBe([
            "plane-key",
            "plane-focus",
            "plane-key",
            "plane-focus",
        ]);
        await application.StopAsync(TestContext.Current.CancellationToken);
        return;

        void Record(Control control, string name)
        {
            _ = control.AddHandler(Events.Key, (_, eventArgs) =>
            {
                if (eventArgs.Phase == RoutingPhase.Bubble)
                {
                    routes.Add($"{name}-key");
                }
            });
            _ = control.AddHandler(Events.TerminalFocusChanged, (_, eventArgs) =>
            {
                if (eventArgs.Phase == RoutingPhase.Bubble)
                {
                    routes.Add($"{name}-focus");
                }
            });
        }
    }

    /// <summary>Verifies rejected background focus leaves modal text and paste without a recipient.</summary>
    [Fact]
    public async Task Dispatch_WhenBackgroundFocusIsRejected_DropsTextAndPasteAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(16, 4)));
        var root = new ProbeContainer();
        var background = new TextInput();
        var plane = new ProbeContainer();
        root.Children.Add(background);
        root.Children.Add(plane);
        var routes = 0;
        Record(root);
        Record(background);
        Record(plane);
        await using Application application = new(root, terminal, terminal, TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);
        await application.Dispatcher.InvokeAsync(() =>
        {
            _ = application.Modality.Enter(plane);
            application.Focus.Focused.ShouldBeNull();
        }, TestContext.Current.CancellationToken);
        var text = new Terminal.Input.Text(new Rune('x'));

        application.Input(in text);
        application.Input(new Paste("null"u8));
        await application.Dispatcher.InvokeAsync(
            static () => { },
            TestContext.Current.CancellationToken);
        await application.Dispatcher.InvokeAsync(
            () =>
            {
                application.Focus.Focus(background).ShouldBeFalse();
                application.Focus.Focused.ShouldBeNull();
            },
            TestContext.Current.CancellationToken);
        application.Input(in text);
        application.Input(new Paste("background"u8));
        await application.Dispatcher.InvokeAsync(
            static () => { },
            TestContext.Current.CancellationToken);

        routes.ShouldBe(0);
        background.Text.ShouldBeEmpty();
        await application.StopAsync(TestContext.Current.CancellationToken);
        return;

        void Record(Control control)
        {
            _ = control.AddHandler(Events.Text, (_, _) => routes++);
            _ = control.AddHandler(Events.Paste, (_, _) => routes++);
        }
    }

    /// <summary>Verifies terminal focus loss clears pointer ownership before modal-safe focus routing.</summary>
    [Fact]
    public async Task Dispatch_WhenTerminalFocusIsLost_CleansPointerBeforeRoutingToModalPrimaryRootAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(12, 4)));
        var root = new ProbeContainer();
        var plane = new ProbeContainer();
        var captured = new ProbeControl();
        plane.Children.Add(captured);
        root.Children.Add(plane);
        var routes = 0;
        _ = plane.AddHandler(Events.TerminalFocusChanged, (_, eventArgs) =>
        {
            if (eventArgs.Phase == RoutingPhase.Bubble)
            {
                routes++;
                captured.ProbeHasPointerCapture.ShouldBeFalse();
            }
        });
        await using Application application = new(root, terminal, terminal, TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);
        await application.Dispatcher.InvokeAsync(() =>
        {
            _ = application.Modality.Enter(plane);
            application.Capture.Capture(captured).ShouldBeTrue();
        }, TestContext.Current.CancellationToken);
        var lostFocus = new TerminalFocus(gained: false);

        application.Input(in lostFocus);
        await application.Dispatcher.InvokeAsync(
            static () => { },
            TestContext.Current.CancellationToken);

        routes.ShouldBe(1);
        captured.PointerCaptureCancellationCalls.ShouldBe(1);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies modal clipboard work routes one handled preview inside its captured plane.</summary>
    [Fact]
    public async Task Dispatch_WhenClipboardShortcutRunsInModalTextInput_RoutesHandledPreviewWithinPlaneAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 4)));
        var input = new TextInput { Text = "modal" };
        var defaults = new List<string>();
        var plane = new RecordingControl("plane", defaults);
        var root = new RecordingControl("root", defaults);
        var background = new ProbeControl();
        plane.Children.Add(input);
        root.Children.Add(background);
        root.Children.Add(plane);
        var ordinaryRoutes = 0;
        var outsideRoutes = 0;
        var handled = new List<(RoutingPhase Phase, KeyEventArgs EventArgs)>();
        _ = plane.AddHandler(Events.Key, (_, _) => ordinaryRoutes++);
        _ = input.AddHandler(Events.Key, (_, _) => ordinaryRoutes++);
        _ = plane.AddHandler(
            Events.Key,
            (_, eventArgs) =>
            {
                eventArgs.Handled.ShouldBeTrue();
                eventArgs.OriginalSource.ShouldBeSameAs(input);
                eventArgs.Source.ShouldBeSameAs(input);
                handled.Add((eventArgs.Phase, eventArgs));
            },
            handledEventsToo: true);
        _ = root.AddHandler(Events.Key, (_, _) => outsideRoutes++, handledEventsToo: true);
        _ = background.AddHandler(Events.Key, (_, _) => outsideRoutes++, handledEventsToo: true);
        await using Application application = new(root, terminal, terminal, TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);
        await application.Dispatcher.InvokeAsync(() =>
        {
            _ = application.Modality.Enter(plane, initialFocus: input);
            input.Select(0, input.Text.Length);
        }, TestContext.Current.CancellationToken);

        await ShortcutAsync('c');
        await application.Dispatcher.InvokeAsync(
            () =>
            {
                input.Text = string.Empty;
            },
            TestContext.Current.CancellationToken);
        await ShortcutAsync('v');
        await application.Dispatcher.InvokeAsync(() =>
        {
            input.Text.ShouldBe("modal");
            input.Select(0, input.Text.Length);
        }, TestContext.Current.CancellationToken);
        await ShortcutAsync('x');
        await application.Dispatcher.InvokeAsync(
            () => input.Text.ShouldBeEmpty(),
            TestContext.Current.CancellationToken);
        await ShortcutAsync('v');
        await application.Dispatcher.InvokeAsync(
            () => input.Text.ShouldBe("modal"),
            TestContext.Current.CancellationToken);

        // Handled ends ordinary handling, not the route, so the opted-in plane handler observes
        // each shortcut once in preview and once again in bubble.
        handled.Count.ShouldBe(8);
        handled.Select(entry => entry.Phase).ShouldBe([
            RoutingPhase.Preview,
            RoutingPhase.Bubble,
            RoutingPhase.Preview,
            RoutingPhase.Bubble,
            RoutingPhase.Preview,
            RoutingPhase.Bubble,
            RoutingPhase.Preview,
            RoutingPhase.Bubble,
        ]);
        handled.Select(entry => entry.EventArgs.Stroke.Character).ShouldBe([
            new Rune('c'),
            new Rune('c'),
            new Rune('v'),
            new Rune('v'),
            new Rune('x'),
            new Rune('x'),
            new Rune('v'),
            new Rune('v'),
        ]);
        handled
            .Select(entry => entry.EventArgs)
            .Distinct(ReferenceEqualityComparer.Instance)
            .Count()
            .ShouldBe(4);
        ordinaryRoutes.ShouldBe(0);
        outsideRoutes.ShouldBe(0);
        defaults.ShouldBeEmpty();
        await application.StopAsync(TestContext.Current.CancellationToken);
        return;

        async Task ShortcutAsync(char character)
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

    /// <summary>Verifies clipboard callbacks cannot rewrite the route captured before modal target and scope mutation.</summary>
    [Fact]
    public async Task Dispatch_WhenModalClipboardCallbackMutatesTargetAndScope_KeepsCapturedHandledRouteAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 4)));
        var defaults = new List<string>();
        var root = new RecordingControl("root", defaults);
        var plane = new RecordingControl("plane", defaults);
        var input = new TextInput { Text = "cut" };
        var nested = new ProbeContainer();
        plane.Children.Add(input);
        plane.Children.Add(nested);
        root.Children.Add(plane);
        var ordinaryRoutes = 0;
        var outsideRoutes = 0;
        var observed = new List<(Control Sender, RoutingPhase Phase, KeyEventArgs EventArgs)>();
        _ = plane.AddHandler(Events.Key, (_, _) => ordinaryRoutes++);
        _ = input.AddHandler(Events.Key, (_, _) => ordinaryRoutes++);
        RecordHandled(plane);
        RecordHandled(input);
        _ = root.AddHandler(Events.Key, (_, _) => outsideRoutes++, handledEventsToo: true);
        await using Application application = new(root, terminal, terminal, TerminalOptions.Minimal);
        ModalScope? outer = null;
        ModalScope? inner = null;
        input.TextChanged += (_, _) =>
        {
            plane.Children.Remove(input).ShouldBeTrue();
            inner = application.Modality.Enter(nested);
        };
        await application.StartAsync(TestContext.Current.CancellationToken);
        await application.Dispatcher.InvokeAsync(() =>
        {
            outer = application.Modality.Enter(plane, initialFocus: input);
            input.Select(0, input.Text.Length);
        }, TestContext.Current.CancellationToken);
        var stroke = new Stroke(
            Code.Character,
            new Rune('x'),
            nativeCode: 0,
            Modifiers.Control,
            KeyAction.Press);

        application.Input(in stroke);
        await application.Dispatcher.InvokeAsync(
            static () => { },
            TestContext.Current.CancellationToken);

        input.Text.ShouldBeEmpty();
        input.Parent.ShouldBeNull();
        outer.ShouldNotBeNull().IsActive.ShouldBeTrue();
        inner.ShouldNotBeNull().IsActive.ShouldBeTrue();
        application.Modality.Active.ShouldBeSameAs(inner);
        // The route captured before the callback removed the input from the plane is reused for
        // both phases, so bubble walks the same ancestry in reverse even though the tree changed.
        observed.Select(item => item.Sender).ShouldBe([plane, input, input, plane]);
        observed.Select(item => item.Phase).ShouldBe([
            RoutingPhase.Preview,
            RoutingPhase.Preview,
            RoutingPhase.Bubble,
            RoutingPhase.Bubble
        ]);
        observed
            .Select(item => (object) item.EventArgs)
            .Distinct(ReferenceEqualityComparer.Instance)
            .ShouldHaveSingleItem()
            .ShouldBeSameAs(observed[0].EventArgs);
        observed[0].EventArgs.OriginalSource.ShouldBeSameAs(input);
        ordinaryRoutes.ShouldBe(0);
        outsideRoutes.ShouldBe(0);
        defaults.ShouldBeEmpty();
        await application.StopAsync(TestContext.Current.CancellationToken);
        input.Dispose();
        return;

        void RecordHandled(Control control) =>
            _ = control.AddHandler(
                Events.Key,
                (sender, eventArgs) =>
                {
                    sender.ShouldBeSameAs(control);
                    eventArgs.Handled.ShouldBeTrue();
                    observed.Add((control, eventArgs.Phase, eventArgs));
                },
                handledEventsToo: true);
    }

    #endregion
}
