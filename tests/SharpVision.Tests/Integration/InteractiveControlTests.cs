// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Integration;

using Label = ControlText;
using UiListView = ListView;

/// <summary>Proves real terminal text, paste, keys, focus, frames, and bytes through TextInput.</summary>
public sealed class InteractiveControlTests
{
    /// <summary>Verifies every Phase 5B control through raw pointer, wheel, focus, resize, and rendering paths.</summary>
    [Fact]
    public async Task Input_WhenInteractiveControlsCompose_CommitsStateEventsCellsAndDamageAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(32, 16)));
        var root = CreateControls(
            out var button,
            out var checkBox,
            out var firstRadio,
            out var secondRadio,
            out var input,
            out var scrollBar,
            out var scroll,
            out var list);
        List<string> order = [];
        button.Click += (_, _) => order.Add("button");
        checkBox.Checked += (_, _) => order.Add("check-checked");
        checkBox.StateChanged += (_, _) => order.Add("check-changed");
        firstRadio.Unchecked += (_, _) => order.Add("radio-a-unchecked");
        secondRadio.Checked += (_, _) => order.Add("radio-b-checked");
        secondRadio.SelectionChanged += (_, _) => order.Add("radio-b-changed");
        await using Application application = new(
            root,
            terminal,
            terminal,
            TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);
        var points = await application.Dispatcher.InvokeAsync(
            () => new[]
            {
                Center(button), Center(checkBox), Center(secondRadio), Center(input),
                new Point(scrollBar.Bounds.Right - 2, scrollBar.Bounds.Y), Center(scroll),
                new Point(list.ContentBounds.X, list.ContentBounds.Y + 1)
            },
            TestContext.Current.CancellationToken);

        Click(terminal, points[0]);
        await WaitUntilAsync(
            () => order.Count >= 1,
            application,
            "button click",
            TestContext.Current.CancellationToken);
        terminal.QueueInput("\u001b[13u"u8);
        await WaitUntilAsync(
            () => order.Count >= 2,
            application,
            "Kitty Enter",
            TestContext.Current.CancellationToken);
        Click(terminal, points[1]);
        await WaitUntilAsync(
            () => checkBox.IsChecked == true,
            application,
            "check-box toggle",
            TestContext.Current.CancellationToken);
        Click(terminal, points[2]);
        await WaitUntilAsync(
            () => secondRadio.IsChecked,
            application,
            "radio selection",
            TestContext.Current.CancellationToken);
        Click(terminal, points[4]);
        await WaitUntilAsync(
            () => scrollBar.Value > 0,
            application,
            "scroll-bar track click",
            TestContext.Current.CancellationToken);
        WheelDown(terminal, points[5]);
        await WaitUntilAsync(
            () => scroll.VerticalOffset == 1,
            application,
            "intrinsic container wheel",
            TestContext.Current.CancellationToken);
        Click(terminal, points[6]);
        await WaitUntilAsync(
            () => list.SelectedIndex == 1,
            application,
            "list selection",
            TestContext.Current.CancellationToken);
        Click(terminal, points[3]);
        terminal.QueueInput("界"u8);
        await WaitUntilAsync(
            () => input.Text == "界",
            application,
            "text input",
            TestContext.Current.CancellationToken);

        await application.Dispatcher.InvokeAsync(() =>
        {
            order.ShouldBe([
                "button",
                "button",
                "check-checked",
                "check-changed",
                "radio-a-unchecked",
                "radio-b-checked",
                "radio-b-changed"
            ]);
            firstRadio.IsChecked.ShouldBeFalse();
            secondRadio.IsChecked.ShouldBeTrue();
            list.SelectedItem.ShouldBe("B");
            application.Focus.Focused.ShouldBeSameAs(input);
            application.Capture.Captured.ShouldBeNull();
            using Frame frame = new(application.Size);
            root.Render(frame.Canvas);
            FrameOracle.Get(frame, new Point(secondRadio.Bounds.X, secondRadio.Bounds.Y))
                .ShouldBe("(");
            FrameOracle.Get(frame, new Point(secondRadio.Bounds.X + 1, secondRadio.Bounds.Y))
                .ShouldBe("•");
            FrameOracle.Get(
                frame,
                new Point(input.ContentBounds.X, input.ContentBounds.Y)).ShouldBe("界");
            frame.GetCell(new Point(input.ContentBounds.X + 1, input.ContentBounds.Y))
                .IsContinuation.ShouldBeTrue();
            FrameOracle.Get(frame, new Point(list.ContentBounds.X, list.ContentBounds.Y + 1)).ShouldBe("B");
        }, TestContext.Current.CancellationToken);
        terminal.Writes.SelectMany(static bytes => bytes)
            .ToArray()
            .AsSpan()
            .IndexOf("界"u8)
            .ShouldBeGreaterThanOrEqualTo(0);
        var previousWrites = terminal.Writes.Count;
        var rendered = NextFrame(application);
        await application.Dispatcher.InvokeAsync(
            () => { list.Items = new object?[] { "Only" }; },
            TestContext.Current.CancellationToken);
        await rendered.WaitAsync(TestContext.Current.CancellationToken);

        await application.Dispatcher.InvokeAsync(() =>
        {
            using Frame frame = new(application.Size);
            root.Render(frame.Canvas);
            FrameOracle.Get(frame, new Point(list.Bounds.X, list.Bounds.Y)).ShouldBe("O");
            FrameOracle.Get(frame, new Point(list.Bounds.X, list.Bounds.Y + 1)).ShouldBeEmpty();
        }, TestContext.Current.CancellationToken);
        terminal.Writes.Count.ShouldBeGreaterThan(previousWrites);
        rendered = NextFrame(application);
        terminal.QueueResize(new Dimensions(new Size(36, 18), new Size(288, 288)));
        await rendered.WaitAsync(TestContext.Current.CancellationToken);
        root.Bounds.ShouldBe(new Rect(0, 0, 36, 18));
        terminal.QueueInput("\u001b[O"u8);
        await WaitUntilAsync(
            () => application.Capture.Captured is null,
            application,
            "terminal focus cleanup",
            TestContext.Current.CancellationToken);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies raw input mutates grapheme state and incremental output without stale source cells.</summary>
    [Fact]
    public async Task Input_WhenTextPasteAndLegacyKeysArrive_CommitsExactEditorFrameAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(12, 3)));
        var input = new TextInput();
        await using Application application = new(input, terminal, terminal, TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);
        await application.Dispatcher.InvokeAsync(() =>
                application.Focus.Focus(input).ShouldBeTrue(),
            TestContext.Current.CancellationToken);

        terminal.QueueInput("界\u001b[200~e\u0301\u001b[201~"u8);
        await WaitUntilAsync(
            () => input.Text == "界e\u0301",
            application,
            "text and paste",
            TestContext.Current.CancellationToken);
        terminal.QueueInput("\u001b[D"u8);
        await WaitUntilAsync(
            () => input.CaretIndex == 1,
            application,
            "legacy Left",
            TestContext.Current.CancellationToken);
        terminal.QueueInput([0x7F]);
        await WaitUntilAsync(
            () => input.Text == "e\u0301",
            application,
            "legacy Backspace",
            TestContext.Current.CancellationToken);

        await application.Dispatcher.InvokeAsync(() =>
        {
            input.Text.ShouldBe("e\u0301");
            input.CaretIndex.ShouldBe(0);
            using Frame frame = new(application.Size);
            input.Render(frame.Canvas);
            FrameOracle.Get(frame, default).ShouldBe("┏");
            FrameOracle.Get(frame, new Point(1, 1)).ShouldBe("e\u0301");
            frame.GetCell(new Point(2, 1)).IsContinuation.ShouldBeFalse();
        }, TestContext.Current.CancellationToken);
        terminal.Writes.SelectMany(static bytes => bytes)
            .Contains((byte) 'e').ShouldBeTrue();

        var focusLost = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await application.Dispatcher.InvokeAsync(() =>
        {
            _ = input.AddHandler(Events.TerminalFocusChanged, (_, eventArgs) =>
            {
                if (eventArgs is { Phase: RoutingPhase.Bubble, Focus.Gained: false })
                {
                    _ = focusLost.TrySetResult();
                }
            });
        }, TestContext.Current.CancellationToken);
        terminal.QueueInput("\u001b[O"u8);
        await focusLost.Task.WaitAsync(TestContext.Current.CancellationToken);
        await application.Dispatcher.InvokeAsync(
            () => input.IsFocused.ShouldBeTrue(),
            TestContext.Current.CancellationToken);
        application.Capture.Captured.ShouldBeNull();
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    private static async Task WaitUntilAsync(
        Func<bool> predicate,
        Application application,
        string operation,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 10_000; attempt++)
        {
            if (await application.Dispatcher.InvokeAsync(predicate, cancellationToken))
            {
                return;
            }

            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
        }

        (await application.Dispatcher.InvokeAsync(predicate, cancellationToken))
            .ShouldBeTrue($"Timed out waiting for {operation}.");
    }

    private static Stack CreateControls(
        out Button button,
        out CheckBox checkBox,
        out RadioButton firstRadio,
        out RadioButton secondRadio,
        out TextInput input,
        out ScrollBar scrollBar,
        out Stack scroll,
        out UiListView list)
    {
        button = new Button { Text = new Label("Go"), Width = Length.Cells(20) };
        checkBox = new CheckBox { Text = new Label("Check"), Width = Length.Cells(20) };
        firstRadio = new RadioButton
        {
            Text = new Label("A"),
            GroupName = "choice",
            IsChecked = true,
            Width = Length.Cells(20)
        };
        secondRadio = new RadioButton { Text = new Label("B"), GroupName = "choice", Width = Length.Cells(20) };
        input = new TextInput { Width = Length.Cells(20), Height = Length.Cells(3) };
        scrollBar = new ScrollBar
        {
            Orientation = Orientation.Horizontal,
            Maximum = 100,
            ViewportSize = 20,
            Width = Length.Cells(20)
        };
        scroll = new Stack
        {
            AutoScroll = true,
            ShowScrollBars = ShowScrollBars.Never,
            Width = Length.Cells(20),
            Height = Length.Cells(3),
            Children = { new Label(string.Join('\n', Enumerable.Range(0, 6))) }
        };
        list = new UiListView
        {
            Items = new object?[] { "A", "B", "C" },
            Width = Length.Cells(20),
            Height = Length.Cells(3)
        };
        var root = new Stack();
        root.Children.Add(button);
        root.Children.Add(checkBox);
        root.Children.Add(firstRadio);
        root.Children.Add(secondRadio);
        root.Children.Add(input);
        root.Children.Add(scrollBar);
        root.Children.Add(scroll);
        root.Children.Add(list);
        return root;
    }

    private static Point Center(ControlBase control) => new(
        control.Bounds.X + Math.Max(0, control.Bounds.Width / 2),
        control.Bounds.Y + Math.Max(0, control.Bounds.Height / 2));

    private static void Click(FakeTerminal terminal, Point point)
    {
        var x = point.X + 1;
        var y = point.Y + 1;
        terminal.QueueInput(Encoding.ASCII.GetBytes($"\u001b[<0;{x};{y}M\u001b[<0;{x};{y}m"));
    }

    private static void WheelDown(FakeTerminal terminal, Point point) =>
        terminal.QueueInput(Encoding.ASCII.GetBytes(
            $"\u001b[<65;{point.X + 1};{point.Y + 1}M"));

    private static Task NextFrame(Application application)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        application.FrameRendered += Complete;
        return completion.Task;

        void Complete(object? sender, FrameRenderedEventArgs eventArgs)
        {
            _ = sender;
            _ = eventArgs;
            application.FrameRendered -= Complete;
            _ = completion.TrySetResult();
        }
    }
}
