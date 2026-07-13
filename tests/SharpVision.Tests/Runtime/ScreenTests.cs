// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Runtime;

using SharpVision.Controls;
using SharpVision.Runtime;
using SharpVision.Terminal.Geometry;
using SharpVision.Terminal.Runtime;
using SharpVision.Tests.Support;

using Shouldly;

using TerminalOptions = Terminal.Runtime.Options;

/// <summary>Verifies screen ownership and startup hooks.</summary>
public sealed class ScreenTests
{
    /// <summary>Verifies attach and started hooks run in documented order.</summary>
    [Fact]
    public async Task Attach_WhenApplicationStarts_RunsHooksInOrderAsync()
    {
        await using FakeTerminal terminal = new FakeTerminal();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        using ProbeScreen screen = new ProbeScreen();
        await using Application application = new Application(
            screen,
            terminal,
            terminal,
            TerminalOptions.Minimal);
        screen.Attach(application);

        await application.StartAsync(TestContext.Current.CancellationToken);

        screen.Order.ShouldBe(["attach", "started"]);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    private sealed class ProbeScreen: Screen
    {
        internal ProbeScreen() => Order = [];

        internal List<string> Order { get; }

        protected override void OnAttach(Application application) => Order.Add("attach");

        protected override void OnStarted(Application application) => Order.Add("started");
    }
}
