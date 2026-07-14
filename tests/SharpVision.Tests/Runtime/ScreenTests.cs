// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Runtime;

using SharpVision.Runtime;
using SharpVision.Terminal.Runtime;


using TerminalOptions = Terminal.Runtime.Options;

/// <summary>Verifies screen ownership and startup hooks.</summary>
public sealed class ScreenTests
{
    /// <summary>Verifies attach and started hooks run in documented order.</summary>
    [Fact]
    public async Task Attach_WhenApplicationStarts_RunsHooksInOrderAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        using ProbeScreen screen = new();
        await using Application application = new(
            screen,
            terminal,
            terminal,
            TerminalOptions.Minimal);
        screen.Attach(application);

        await application.StartAsync(TestContext.Current.CancellationToken);

        screen.Order.ShouldBe(["attach", "build", "started"]);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies Build runs after OnAttach and before OnStarted.</summary>
    [Fact]
    public async Task Build_WhenApplicationStarts_RunsAfterAttachAndBeforeStartedAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        using ProbeScreen screen = new();
        await using Application application = new(
            screen,
            terminal,
            terminal,
            TerminalOptions.Minimal);
        screen.Attach(application);

        await application.StartAsync(TestContext.Current.CancellationToken);

        screen.Order.ShouldBe(["attach", "build", "started"]);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }
}
