// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Input;

/// <summary>Verifies ModalityManager.Enter exclusion gating for key, text, paste, terminal-focus,
/// pointer, and clipboard routing, and end-to-end modal isolation through raw terminal decoding,
/// application dispatch, layout, and rendering.</summary>
public sealed partial class ModalityManagerTests
{
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

        void Record(ControlBase control, string name)
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
        var text = new TerminalText(new Rune('x'));

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

        void Record(ControlBase control, string name)
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

        void Record(ControlBase control, string name)
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
        var text = new TerminalText(new Rune('x'));

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

        void Record(ControlBase control)
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
        var observed = new List<(ControlBase Sender, RoutingPhase Phase, KeyEventArgs EventArgs)>();
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
        outer.ShouldNotBeNull().Active.ShouldBeTrue();
        inner.ShouldNotBeNull().Active.ShouldBeTrue();
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

        void RecordHandled(ControlBase control) =>
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

    #region Raw application proof

    /// <summary>Verifies one modal plane consumes raw input until dismissal and preserves final terminal output.</summary>
    [Fact]
    public async Task Input_WhenModalPlaneIsActive_IsolatesRawRecordsAndNeverReplaysDismissalAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(32, 8)));
        var root = CreateSurface(
            out var background,
            out var backgroundEditor,
            out var backgroundButton,
            out var modal,
            out var modalEditor,
            out var modalButton,
            out var modalButtonLabel);
        var backgroundRoutes = 0;
        var modalKeyRoutes = 0;
        var modalTextRoutes = 0;
        var modalPasteRoutes = 0;
        var modalFocusRoutes = 0;
        var modalPointerRoutes = 0;
        var modalWheelRoutes = 0;
        var backgroundClicks = 0;
        var modalClicks = 0;
        var dismissRequests = 0;
        backgroundButton.Click += (_, _) => backgroundClicks++;
        modalButton.Click += (_, _) => modalClicks++;
        RecordBackground(Events.Key);
        RecordBackground(Events.Text);
        RecordBackground(Events.Paste);
        RecordBackground(Events.TerminalFocusChanged);
        RecordBackground(Events.Pointer);
        _ = modal.AddHandler(Events.Key, (_, _) => modalKeyRoutes++);
        _ = modal.AddHandler(Events.Text, (_, _) => modalTextRoutes++);
        _ = modal.AddHandler(Events.Paste, (_, _) => modalPasteRoutes++);
        _ = modal.AddHandler(Events.TerminalFocusChanged, (_, _) => modalFocusRoutes++);
        _ = modal.AddHandler(Events.Pointer, (_, eventArgs) =>
        {
            modalPointerRoutes++;

            if (eventArgs.Pointer.Action == PointerAction.Wheel)
            {
                modalWheelRoutes++;
            }
        });
        await using Application application = new(root, terminal, terminal, TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);
        ModalScope? scope = null;

        await application.Dispatcher.InvokeAsync(() =>
        {
            application.Focus.Focus(backgroundEditor).ShouldBeTrue();
            scope = application.Modality.Enter(
                modal,
                OutsideInteraction.Dismiss,
                initialFocus: modalEditor);
            scope.DismissRequested += (_, _) =>
            {
                dismissRequests++;
                scope.Dispose();
            };
        }, TestContext.Current.CancellationToken);

        await SendAndWaitAsync(
            terminal,
            application,
            "λ"u8.ToArray(),
            () => modalEditor.Text == "λ",
            "modal UTF-8 text");
        await SendAndWaitAsync(
            terminal,
            application,
            "\u001b[200~界\u001b[201~"u8.ToArray(),
            () => modalEditor.Text == "λ界",
            "modal bracketed paste");
        await SendAndWaitAsync(
            terminal,
            application,
            "\t"u8.ToArray(),
            () => ReferenceEquals(application.Focus.Focused, modalButton),
            "forward modal Tab");
        await SendAndWaitAsync(
            terminal,
            application,
            "\u001b[O\u001b[I"u8.ToArray(),
            () => application.HasFocus && modalFocusRoutes >= 2,
            "terminal focus loss and gain");

        var modalButtonPoint = await application.Dispatcher.InvokeAsync(
            () => Center(modalButton),
            TestContext.Current.CancellationToken);
        await SendAndWaitAsync(
            terminal,
            application,
            EncodePointer(35, modalButtonPoint, 'M'),
            () => application.Pointer.Position == modalButtonPoint,
            "modal pointer move");
        await SendAndWaitAsync(
            terminal,
            application,
            EncodePointer(0, modalButtonPoint, 'M'),
            () => ReferenceEquals(application.Capture.Captured, modalButton),
            "modal pointer press");
        await SendAndWaitAsync(
            terminal,
            application,
            EncodePointer(0, modalButtonPoint, 'm'),
            () => modalClicks == 1 && application.Capture.Captured is null,
            "modal pointer release");
        await SendAndWaitAsync(
            terminal,
            application,
            "\t"u8.ToArray(),
            () => ReferenceEquals(application.Focus.Focused, modalEditor),
            "wrapped modal Tab");

        backgroundEditor.Text.ShouldBeEmpty();
        backgroundRoutes.ShouldBe(0);
        backgroundClicks.ShouldBe(0);
        modalKeyRoutes.ShouldBeGreaterThan(0);
        modalTextRoutes.ShouldBeGreaterThan(0);
        modalPasteRoutes.ShouldBeGreaterThan(0);
        modalPointerRoutes.ShouldBeGreaterThan(0);
        scope.ShouldNotBeNull().Active.ShouldBeTrue();
        application.Modality.Active.ShouldBeSameAs(scope);

        await WaitForIdleAsync(application);
        var postResizeWriteIndex = terminal.Writes.Count;
        var resized = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        application.FrameRendered += OnFrameRendered;
        terminal.QueueResize(new Dimensions(new Size(36, 10), new Size(288, 160)));
        await resized.Task.WaitAsync(TestContext.Current.CancellationToken);
        application.FrameRendered -= OnFrameRendered;
        await WaitForIdleAsync(application);

        application.Size.ShouldBe(new Size(36, 10));
        application.Modality.Active.ShouldBeSameAs(scope);
        scope.Active.ShouldBeTrue();
        application.Focus.Focused.ShouldBeSameAs(modalEditor);

        var backgroundButtonPoint = await application.Dispatcher.InvokeAsync(
            () => Center(backgroundButton),
            TestContext.Current.CancellationToken);
        await SendAndWaitAsync(
            terminal,
            application,
            EncodePointer(35, backgroundButtonPoint, 'M'),
            () => application.Pointer.Position == backgroundButtonPoint,
            "outside physical pointer move");

        application.Pointer.Position.ShouldBe(backgroundButtonPoint);
        application.Pointer.Hovered.ShouldBeNull();
        backgroundButton.PointerOver.ShouldBeFalse();
        dismissRequests.ShouldBe(0);

        await SendAndWaitAsync(
            terminal,
            application,
            EncodePointer(65, modalButtonPoint, 'M'),
            () => modalWheelRoutes > 0,
            "unhandled in-plane wheel, swallowed rather than dismissing");

        dismissRequests.ShouldBe(0);
        scope.Active.ShouldBeTrue();
        application.Modality.Active.ShouldBeSameAs(scope);

        await SendAndWaitAsync(
            terminal,
            application,
            EncodePointer(65, backgroundButtonPoint, 'M'),
            () => !scope.Active,
            "unhandled outside dismissing wheel");

        dismissRequests.ShouldBe(1);
        backgroundClicks.ShouldBe(0);
        backgroundRoutes.ShouldBe(0);
        application.Modality.Active.ShouldBeNull();
        application.Focus.Focused.ShouldBeSameAs(backgroundEditor);
        application.Pointer.Position.ShouldBe(backgroundButtonPoint);
        application.Pointer.PressOrigin.ShouldBeNull();

        await SendAndWaitAsync(
            terminal,
            application,
            "R"u8.ToArray(),
            () => backgroundEditor.Text == "R",
            "fresh background text after dismissal");
        await WaitForIdleAsync(application);

        backgroundEditor.Text.ShouldBe("R");
        modalEditor.Text.ShouldBe("λ界");
        backgroundRoutes.ShouldBeGreaterThan(0);
        backgroundClicks.ShouldBe(0);
        modalClicks.ShouldBe(1);

        var postResizeWrites = terminal.Writes.Skip(postResizeWriteIndex).ToArray();
        postResizeWrites.ShouldNotBeEmpty();
        var emitted = postResizeWrites.SelectMany(static value => value).ToArray();
        emitted.AsSpan().IndexOf("λ界"u8).ShouldBeGreaterThanOrEqualTo(0);
        emitted.ShouldContain((byte) 'R');
        var screen = new ComponentScreen(application.Size);

        foreach (var write in postResizeWrites)
        {
            screen.Apply(write);
        }

        await application.Dispatcher.InvokeAsync(() =>
        {
            using Frame expected = new(application.Size);
            root.Render(expected.Canvas);
            var backgroundTextOrigin = new Point(
                backgroundEditor.ContentBounds.X,
                backgroundEditor.ContentBounds.Y);
            FrameOracle.Get(expected, backgroundTextOrigin).ShouldBe("R");
            var modalTextOrigin = new Point(
                modalEditor.ContentBounds.X,
                modalEditor.ContentBounds.Y);
            FrameOracle.Get(expected, modalTextOrigin).ShouldBe("λ");
            FrameOracle.Get(expected, new Point(modalTextOrigin.X + 1, modalTextOrigin.Y)).ShouldBe("界");
            expected.GetCell(new Point(modalTextOrigin.X + 2, modalTextOrigin.Y))
                .Continuation.ShouldBeTrue();
            FrameOracle.Get(
                expected,
                new Point(modalButtonLabel.Bounds.X, modalButtonLabel.Bounds.Y)).ShouldBe("O");
            AssertScreen(expected, screen);
        }, TestContext.Current.CancellationToken);
        await application.StopAsync(TestContext.Current.CancellationToken);
        return;

        void RecordBackground<TEventArgs>(Event<TEventArgs> routedEvent)
            where TEventArgs : RoutedEventArgs =>
            _ = background.AddHandler(routedEvent, (_, _) => backgroundRoutes++);

        void OnFrameRendered(object? sender, FrameRenderedEventArgs eventArgs)
        {
            _ = sender;
            _ = eventArgs;

            if (application.Size == new Size(36, 10))
            {
                _ = resized.TrySetResult();
            }
        }
    }

    #endregion

    #region Surface fixture

    private static Overlay CreateSurface(
        out Stack background,
        out TextInput backgroundEditor,
        out Button backgroundButton,
        out Stack modal,
        out TextInput modalEditor,
        out Button modalButton,
        out ControlText modalButtonLabel)
    {
        backgroundEditor = new TextInput
        {
            Width = Length.Cells(12),
            Height = Length.Cells(3),
        };
        backgroundButton = new Button
        {
            Text = "BG",
            Width = Length.Cells(12),
            Height = Length.Cells(3),
        };
        background = new Stack
        {
            Spacing = 1,
            Width = Length.Cells(12),
            Height = Length.Cells(7),
            Children = { backgroundEditor, backgroundButton },
        };
        modalEditor = new TextInput
        {
            Width = Length.Cells(12),
            Height = Length.Cells(3),
        };
        modalButton = new Button
        {
            Text = "OK",
            Width = Length.Cells(12),
            Height = Length.Cells(3),
        };
        modalButtonLabel = modalButton.TextControl!;
        modal = new Stack
        {
            Spacing = 1,
            Width = Length.Cells(12),
            Height = Length.Cells(7),
            Children = { modalEditor, modalButton },
        };
        Overlay.SetTop(background, Length.Cells(1));
        Overlay.SetLeft(modal, Length.Cells(18));
        Overlay.SetTop(modal, Length.Cells(1));
        return new Overlay { Children = { background, modal } };
    }

    #endregion

    #region Terminal synchronization

    private static async Task SendAndWaitAsync(
        FakeTerminal terminal,
        Application application,
        ReadOnlyMemory<byte> bytes,
        Func<bool> predicate,
        string operation)
    {
        terminal.QueueInput(bytes.Span);
        await WaitUntilAsync(application, predicate, operation);
        await WaitForIdleAsync(application);
    }

    private static async Task WaitUntilAsync(
        Application application,
        Func<bool> predicate,
        string operation)
    {
        for (var attempt = 0; attempt < 10_000; attempt++)
        {
            if (await application.Dispatcher.InvokeAsync(
                predicate,
                TestContext.Current.CancellationToken))
            {
                return;
            }

            await Task.Yield();
            TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        }

        throw new TimeoutException($"Timed out waiting for {operation}.");
    }

    private static async Task WaitForIdleAsync(Application application)
    {
        var idle = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        application.Idle += OnIdle;

        try
        {
            await application.Dispatcher.InvokeAsync(
                static () => { },
                TestContext.Current.CancellationToken);
            await idle.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        }
        finally
        {
            application.Idle -= OnIdle;
        }

        return;

        void OnIdle(object? sender, EventArgs eventArgs)
        {
            _ = sender;
            _ = eventArgs;
            _ = idle.TrySetResult();
        }
    }

    #endregion

    #region Output oracle

    private static void AssertScreen(Frame expected, ComponentScreen actual)
    {
        actual.Size.ShouldBe(expected.Size);

        for (var y = 0; y < expected.Size.Height; y++)
        {
            for (var x = 0; x < expected.Size.Width; x++)
            {
                var point = new Point(x, y);
                var expectedCell = expected.GetCell(point);
                var actualCell = actual.Cell(point);
                var expectedText = FrameOracle.Get(expected, point);
                actualCell.Text.ShouldBe(
                    expectedText.Length == 0 ? " " : expectedText,
                    $"terminal cell text at {point}");
                var projectedStyle = new TerminalStyle(
                    TerminalPalette.Project(expectedCell.Style.Foreground, ColorDepth.Basic16),
                    TerminalPalette.Project(expectedCell.Style.Background, ColorDepth.Basic16),
                    expectedCell.Style.Attributes);
                actualCell.Style.ShouldBe(projectedStyle, $"terminal cell style at {point}");
                actualCell.Width.ShouldBe(expectedCell.Width, $"terminal cell width at {point}");
                actualCell.Continuation.ShouldBe(
                    expectedCell.Continuation,
                    $"terminal continuation at {point}");

                if (expectedCell.Continuation)
                {
                    actualCell.LeadX.ShouldBe(expectedCell.Lead.X, $"terminal lead at {point}");
                }
            }
        }
    }

    private static Point Center(ControlBase control) => new(
        control.Bounds.X + (control.Bounds.Width / 2),
        control.Bounds.Y + (control.Bounds.Height / 2));

    private static byte[] EncodePointer(int button, Point point, char final) =>
        Encoding.ASCII.GetBytes(
            FormattableString.Invariant($"\u001b[<{button};{point.X + 1};{point.Y + 1}{final}"));

    #endregion
}
