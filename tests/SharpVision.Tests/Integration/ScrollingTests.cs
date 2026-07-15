// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Integration;


using SharpVision.Runtime;
using SharpVision.Terminal.Runtime;


using ControlCanvas = SharpVision.Controls.Canvas;
using Label = SharpVision.Controls.Text;
using TerminalOptions = Terminal.Runtime.Options;

/// <summary>Proves raw terminal wheel input consumes nested scrolling locally then outward.</summary>
public sealed class ScrollingTests
{
    /// <summary>Verifies nested two-axis bars, pixel dragging, focus reveal, Unicode cells, and resize.</summary>
    [Fact]
    public async Task Input_WhenNestedAutomaticViewsUsePixelMouse_PreservesExactOffsetsAndThumbsAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(8, 5), new Size(80, 50)));
        ControlCanvas content = new() { Width = Length.Cells(14), Height = Length.Cells(9) };
        Label label = new("界Z");
        Button target = new()
        {
            Content = new Label("Go"),
            Width = Length.Cells(2),
            Height = Length.Cells(1),
        };
        ControlCanvas.SetLeft(target, Length.Cells(12));
        ControlCanvas.SetTop(target, Length.Cells(8));
        content.Children.Add(label);
        content.Children.Add(target);
        Stack inner = new()
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Both,
            Width = Length.Cells(12),
            Height = Length.Cells(7),
            Children = { content },
        };
        Stack outer = new() { AutoScroll = true, ScrollBars = ScrollBars.Both, Children = { inner } };
        await using Application application = new(
            outer,
            terminal,
            terminal,
            TerminalOptions.Minimal with { Coordinates = MouseCoordinates.Pixel });
        await application.StartAsync(TestContext.Current.CancellationToken);

        outer.Viewport.ShouldBe(new Size(7, 4));
        inner.Viewport.ShouldBe(new Size(11, 6));
        terminal.QueueInput(Encoding.ASCII.GetBytes(
            "\u001b[<0;16;46M\u001b[<32;56;46M\u001b[<0;56;46m"));
        await WaitUntilAsync(
            () => outer.HorizontalOffset == 5 && application.Capture.Captured is null,
            application,
            "pixel thumb drag",
            TestContext.Current.CancellationToken);

        Task rendered = NextFrame(application);
        await application.Dispatcher.InvokeAsync(() =>
        {
            outer.HorizontalOffset = 0;
            inner.HorizontalOffset = 0;
            outer.VerticalOffset = 0;
            inner.VerticalOffset = 0;
        }, TestContext.Current.CancellationToken);
        await rendered.WaitAsync(TestContext.Current.CancellationToken);
        string horizontal = string.Concat(Enumerable.Repeat("\u001b[<66;16;16M", 10));
        string vertical = string.Concat(Enumerable.Repeat("\u001b[<65;16;16M", 10));
        terminal.QueueInput(Encoding.ASCII.GetBytes(horizontal + vertical));
        await WaitUntilAsync(
            () => inner.HorizontalOffset == 3 && outer.HorizontalOffset == 5 &&
                inner.VerticalOffset == 3 && outer.VerticalOffset == 3,
            application,
            "nested two-axis wheel remainder",
            TestContext.Current.CancellationToken);

        await application.Dispatcher.InvokeAsync(() =>
        {
            using Frame frame = new(application.Size);
            outer.Render(frame.Canvas);
            FrameOracle.Get(frame, new Point(5, 4)).ShouldBe("▓");
            FrameOracle.Get(frame, new Point(7, 2)).ShouldBe("▓");
            frame.GetCell(new Point(0, 0)).IsContinuation.ShouldBeFalse();
        }, TestContext.Current.CancellationToken);
        rendered = NextFrame(application);
        await application.Dispatcher.InvokeAsync(() =>
        {
            inner.HorizontalOffset = 0;
            inner.VerticalOffset = 0;
            outer.HorizontalOffset = 0;
            outer.VerticalOffset = 0;
        }, TestContext.Current.CancellationToken);
        await rendered.WaitAsync(TestContext.Current.CancellationToken);
        rendered = NextFrame(application);
        await application.Dispatcher.InvokeAsync(() =>
        {
            application.Focus.Focus(target).ShouldBeTrue();
            inner.BringIntoView(target).ShouldBeTrue();
            outer.BringIntoView(target).ShouldBeTrue();
        }, TestContext.Current.CancellationToken);
        await rendered.WaitAsync(TestContext.Current.CancellationToken);
        inner.HorizontalOffset.ShouldBe(3);
        inner.VerticalOffset.ShouldBe(3);
        outer.HorizontalOffset.ShouldBe(5);
        outer.VerticalOffset.ShouldBe(3);
        application.Focus.Focused.ShouldBeSameAs(target);

        rendered = NextFrame(application);
        terminal.QueueResize(new Dimensions(new Size(16, 11), new Size(160, 110)));
        await rendered.WaitAsync(TestContext.Current.CancellationToken);
        outer.HorizontalOffset.ShouldBe(0);
        outer.VerticalOffset.ShouldBe(0);
        outer.Viewport.ShouldBe(new Size(16, 11));
        await application.Dispatcher.InvokeAsync(() =>
        {
            using Frame frame = new(application.Size);
            outer.Render(frame.Canvas);
            FrameOracle.Get(frame, new Point(15, 10)).ShouldBeEmpty();
        }, TestContext.Current.CancellationToken);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies SGR wheel bytes, nested remainder, resize clamping, and final offsets.</summary>
    [Fact]
    public async Task Input_WhenNestedViewsReceiveWheel_ConsumesRemainderAndClampsAfterResizeAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(5, 4), new Size(50, 40)));
        Label leaf = new(string.Join('\n', Enumerable.Range(0, 20))) { Width = Length.Cells(5) };
        Stack inner = Hidden(leaf);
        inner.Width = Length.Cells(5);
        inner.Height = Length.Cells(8);
        Stack outer = Hidden(inner);
        await using Application application = new(outer, terminal, terminal, TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);
        TaskCompletionSource reached = new(TaskCreationOptions.RunContinuationsAsynchronously);
        outer.ScrollChanged += (_, _) =>
        {
            if (outer.VerticalOffset == 4)
            {
                _ = reached.TrySetResult();
            }
        };
        string wheel = string.Concat(Enumerable.Repeat("\u001b[<65;1;1M", 20));

        terminal.QueueInput(Encoding.ASCII.GetBytes(wheel));
        await reached.Task.WaitAsync(TestContext.Current.CancellationToken);

        inner.VerticalOffset.ShouldBe(12);
        outer.VerticalOffset.ShouldBe(4);
        Task rendered = NextFrame(application);
        terminal.QueueResize(new Dimensions(new Size(5, 8), new Size(50, 80)));
        await rendered.WaitAsync(TestContext.Current.CancellationToken);
        outer.VerticalOffset.ShouldBe(0);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    private static Stack Hidden(Control content) => new()
    {
        AutoScroll = true,
        ScrollBars = ScrollBars.Both,
        ShowScrollBars = ShowScrollBars.Never,
        Children = { content },
    };

    private static Task NextFrame(Application application)
    {
        TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
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

    private static async Task WaitUntilAsync(
        Func<bool> predicate,
        Application application,
        string operation,
        CancellationToken cancellationToken)
    {
        for (int attempt = 0; attempt < 10_000; attempt++)
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
}
