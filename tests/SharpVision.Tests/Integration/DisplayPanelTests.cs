using System.Text;

using SharpVision.Controls;
using SharpVision.Layout;
using SharpVision.Runtime;
using SharpVision.Terminal.Geometry;
using SharpVision.Terminal.Rendering;
using SharpVision.Terminal.Runtime;
using SharpVision.Tests.Support;

using Shouldly;

using ControlCanvas = SharpVision.Controls.Canvas;
using ControlText = SharpVision.Controls.Text;
using TerminalOptions = SharpVision.Terminal.Runtime.Options;

namespace SharpVision.Tests.Integration;

/// <summary>Proves display controls compose through Application, layout, and semantic cells.</summary>
public sealed class DisplayPanelTests
{
    /// <summary>Verifies composition, resize, mutation, exact cells, and terminal bytes.</summary>
    [Fact]
    public async Task Render_WhenDisplayPanelsCompose_PreservesGeometryCellsAndDamageAsync()
    {
        await using var terminal = new FakeTerminal();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        var root = Create(out var label, out var canvas);
        await using var application = new Application(
            root,
            terminal,
            terminal,
            TerminalOptions.Minimal);

        await application.StartAsync(TestContext.Current.CancellationToken);

        root.Bounds.ShouldBe(new Rect(0, 0, 20, 6));
        await application.Dispatcher.InvokeAsync(
            () => AssertFrame(root, new Size(20, 6), "界", hasCanvasText: true),
            TestContext.Current.CancellationToken);
        Bytes(terminal).AsSpan().IndexOf(Encoding.UTF8.GetBytes("界"))
            .ShouldBeGreaterThanOrEqualTo(0);
        var writes = terminal.Writes.Count;
        var rendered = NextFrame(application);

        await application.Dispatcher.InvokeAsync(
            () =>
            {
                label.Content = "Q";
                canvas.Children.Clear();
            },
            TestContext.Current.CancellationToken);
        await rendered.WaitAsync(TestContext.Current.CancellationToken);

        await application.Dispatcher.InvokeAsync(
            () => AssertFrame(root, new Size(20, 6), "Q", hasCanvasText: false),
            TestContext.Current.CancellationToken);
        terminal.Writes.Count.ShouldBeGreaterThan(writes);
        Bytes(terminal).ShouldContain((byte) 'Q');

        rendered = NextFrame(application);
        terminal.QueueResize(new Dimensions(new Size(24, 8), new Size(192, 128)));
        await rendered.WaitAsync(TestContext.Current.CancellationToken);

        root.Bounds.ShouldBe(new Rect(0, 0, 24, 8));
        await application.Dispatcher.InvokeAsync(
            () => AssertFrame(root, new Size(24, 8), "Q", hasCanvasText: false),
            TestContext.Current.CancellationToken);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    private static void AssertFrame(
        Grid root,
        Size size,
        string expectedLabel,
        bool hasCanvasText)
    {
        using var frame = new Frame(size);
        root.Render(frame.Canvas);
        FrameOracle.Get(frame, default).ShouldBe("┌");
        FrameOracle.Get(frame, new Point(1, 1)).ShouldBe(expectedLabel);

        if (expectedLabel == "界")
        {
            frame.GetCell(new Point(2, 1)).IsContinuation.ShouldBeTrue();
        }
        FrameOracle.Get(frame, new Point(6, 0)).ShouldBe("O");
        FrameOracle.Get(frame, new Point(7, 2)).ShouldBe(hasCanvasText ? "C" : string.Empty);
    }

    private static byte[] Bytes(FakeTerminal terminal) =>
        [.. terminal.Writes.SelectMany(static value => value)];

    private static Grid Create(out ControlText label, out ControlCanvas canvas)
    {
        var root = new Grid();
        root.Columns.Add(Track.Star(1));
        root.Rows.Add(Track.Star(1));
        var dock = new Dock();
        root.Children.Add(dock);
        label = new ControlText("界");
        var border = new Border
        {
            Width = Length.Cells(6),
            BorderThickness = new Thickness(1),
            Child = label,
        };
        Dock.SetSide(border, Side.Left);
        dock.Children.Add(border);
        var stack = new Stack();
        dock.Children.Add(stack);
        var overlay = new Overlay { Height = Length.Cells(2) };
        overlay.Children.Add(new ControlText("O"));
        stack.Children.Add(overlay);
        canvas = new ControlCanvas();
        var canvasText = new ControlText("C");
        ControlCanvas.SetLeft(canvasText, Length.Cells(1));
        canvas.Children.Add(canvasText);
        stack.Children.Add(canvas);
        return root;
    }

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
