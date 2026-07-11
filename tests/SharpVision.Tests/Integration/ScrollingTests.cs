using System.Text;

using SharpVision.Controls;
using SharpVision.Layout;
using SharpVision.Runtime;
using SharpVision.Terminal.Geometry;
using SharpVision.Terminal.Runtime;
using SharpVision.Tests.Support;

using Shouldly;

using Label = SharpVision.Controls.Text;
using TerminalOptions = SharpVision.Terminal.Runtime.Options;

namespace SharpVision.Tests.Integration;

/// <summary>Proves raw terminal wheel input consumes nested scrolling locally then outward.</summary>
public sealed class ScrollingTests
{
    /// <summary>Verifies SGR wheel bytes, nested remainder, resize clamping, and final offsets.</summary>
    [Fact]
    public async Task Input_WhenNestedViewsReceiveWheel_ConsumesRemainderAndClampsAfterResizeAsync()
    {
        await using var terminal = new FakeTerminal();
        terminal.QueueResize(new Dimensions(new Size(5, 4), new Size(50, 40)));
        var leaf = new Label(string.Join('\n', Enumerable.Range(0, 20))) { Width = Length.Cells(5) };
        var inner = Hidden(leaf);
        inner.Width = Length.Cells(5);
        inner.Height = Length.Cells(8);
        var outer = Hidden(inner);
        await using var application = new Application(outer, terminal, terminal, TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);
        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        outer.ScrollChanged += (_, _) =>
        {
            if (outer.VerticalOffset == 4)
            {
                _ = reached.TrySetResult();
            }
        };
        var wheel = string.Concat(Enumerable.Repeat("\u001b[<65;1;1M", 20));

        terminal.QueueInput(Encoding.ASCII.GetBytes(wheel));
        await reached.Task.WaitAsync(TestContext.Current.CancellationToken);

        inner.VerticalOffset.ShouldBe(12);
        outer.VerticalOffset.ShouldBe(4);
        var rendered = NextFrame(application);
        terminal.QueueResize(new Dimensions(new Size(5, 8), new Size(50, 80)));
        await rendered.WaitAsync(TestContext.Current.CancellationToken);
        outer.VerticalOffset.ShouldBe(0);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    private static ScrollView Hidden(Control content) => new()
    {
        Content = content,
        HorizontalBarVisibility = ScrollBarVisibility.Hidden,
        VerticalBarVisibility = ScrollBarVisibility.Hidden,
    };

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
