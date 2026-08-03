// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Integration;

using ControlOverlay = Overlay;

/// <summary>Proves modal isolation through raw terminal decoding, application dispatch, layout, and rendering.</summary>
public sealed class ModalityIntegrationTests
{
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
        scope.ShouldNotBeNull().IsActive.ShouldBeTrue();
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
        scope.IsActive.ShouldBeTrue();
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
        backgroundButton.IsPointerOver.ShouldBeFalse();
        dismissRequests.ShouldBe(0);

        await SendAndWaitAsync(
            terminal,
            application,
            EncodePointer(65, modalButtonPoint, 'M'),
            () => modalWheelRoutes > 0,
            "unhandled in-plane wheel, swallowed rather than dismissing (see #225)");

        dismissRequests.ShouldBe(0);
        scope.IsActive.ShouldBeTrue();
        application.Modality.Active.ShouldBeSameAs(scope);

        await SendAndWaitAsync(
            terminal,
            application,
            EncodePointer(65, backgroundButtonPoint, 'M'),
            () => !scope.IsActive,
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
                .IsContinuation.ShouldBeTrue();
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

    private static ControlOverlay CreateSurface(
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
        modalButtonLabel = new ControlText("OK");
        modalButton = new Button
        {
            Text = modalButtonLabel,
            Width = Length.Cells(12),
            Height = Length.Cells(3),
        };
        modal = new Stack
        {
            Spacing = 1,
            Width = Length.Cells(12),
            Height = Length.Cells(7),
            Children = { modalEditor, modalButton },
        };
        ControlOverlay.SetTop(background, Length.Cells(1));
        ControlOverlay.SetLeft(modal, Length.Cells(18));
        ControlOverlay.SetTop(modal, Length.Cells(1));
        return new ControlOverlay { Children = { background, modal } };
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
                    Palette.Project(expectedCell.Style.Foreground, ColorDepth.Basic16),
                    Palette.Project(expectedCell.Style.Background, ColorDepth.Basic16),
                    expectedCell.Style.Attributes);
                actualCell.Style.ShouldBe(projectedStyle, $"terminal cell style at {point}");
                actualCell.Width.ShouldBe(expectedCell.Width, $"terminal cell width at {point}");
                actualCell.IsContinuation.ShouldBe(
                    expectedCell.IsContinuation,
                    $"terminal continuation at {point}");

                if (expectedCell.IsContinuation)
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
