// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Integration;

using SharpVision.Runtime;
using SharpVision.Terminal.Runtime;


using TerminalOptions = Terminal.Runtime.Options;

/// <summary>Verifies suspension and resize ordering across session, layout, and renderer.</summary>
public sealed class ResizeRenderTests
{
    /// <summary>Verifies a suspended host resumes with layout before its first positive frame.</summary>
    [Fact]
    public async Task Resize_WhenSuspendedHostBecomesPositive_LayoutsBeforeFrameAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(0, 0)));
        ProbeControl root = new();
        await using Application application = new(
            root,
            terminal,
            terminal,
            TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);
        List<string> order = [];
        TaskCompletionSource rendered = new(TaskCreationOptions.RunContinuationsAsynchronously);
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
