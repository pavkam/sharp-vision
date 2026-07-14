// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Runtime;

using SharpVision.Runtime;
using SharpVision.Terminal.Runtime;

using TerminalOptions = Terminal.Runtime.Options;

/// <summary>Verifies the terminal output services facade exposes a working bell and clipboard.</summary>
public sealed class TerminalServicesTests
{
    /// <summary>Verifies ringing the bell posts the BEL byte through the out-of-band write path.</summary>
    [Fact]
    public async Task Bell_WhenRung_EmitsBelByteAsync()
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

        application.Terminal.Bell.Ring();
        await bell.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies the terminal services facade and its members are non-null once constructed.</summary>
    [Fact]
    public async Task Terminal_WhenConstructed_IsNonNullAsync()
    {
        await using FakeTerminal terminal = new();
        await using Application application = new(new ProbeControl(), terminal, terminal, TerminalOptions.Minimal);

        _ = application.Terminal.ShouldNotBeNull();
        _ = application.Terminal.Bell.ShouldNotBeNull();
        _ = application.Terminal.Clipboard.ShouldNotBeNull();
    }
}
