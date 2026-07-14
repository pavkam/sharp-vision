// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Runtime;

using SharpVision.Runtime;
using SharpVision.Terminal.Runtime;

using TerminalOptions = Terminal.Runtime.Options;

/// <summary>Verifies out-of-band protocol bytes share the render write gate.</summary>
public sealed class ApplicationOutOfBandTests
{
    /// <summary>Verifies posted bytes reach the transport while the application is running.</summary>
    [Fact]
    public async Task PostOutOfBand_WhenApplicationRunning_WritesBytesToTransportAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        TaskCompletionSource bell = new(TaskCreationOptions.RunContinuationsAsynchronously);
        terminal.Written += memory =>
        {
            if (memory.Span.IndexOf((byte) 0x07) >= 0)
            {
                _ = bell.TrySetResult();
            }
        };
        await using Application application = new(new ProbeControl(), terminal, terminal, TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);

        application.PostOutOfBand(new byte[] { 0x07 });
        await bell.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        await application.StopAsync(TestContext.Current.CancellationToken);
    }
}
