namespace SharpVision.Tests.Integration;

using SharpVision.Runtime;
using SharpVision.Terminal.Geometry;
using SharpVision.Terminal.Runtime;
using SharpVision.Tests.Support;

using Shouldly;

using TerminalOptions = Terminal.Runtime.Options;

/// <summary>Verifies suspension and resize ordering across session, layout, and renderer.</summary>
public sealed class ResizeRenderTests
{
    /// <summary>Verifies a suspended host resumes with layout before its first positive frame.</summary>
    [Fact]
    public async Task Resize_WhenSuspendedHostBecomesPositive_LayoutsBeforeFrameAsync()
    {
        await using var terminal = new FakeTerminal();
        terminal.QueueResize(new Dimensions(new Size(0, 0)));
        var root = new ProbeControl();
        await using var application = new Application(
            root,
            terminal,
            terminal,
            TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);
        var order = new List<string>();
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
}
