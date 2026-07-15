// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Runtime;




/// <summary>Verifies screen ownership and startup hooks.</summary>
public sealed class ScreenTests
{
    /// <summary>Verifies retained composition precedes attach, first layout, and started hooks.</summary>
    [Fact]
    public async Task Attach_WhenApplicationStarts_RunsRetainedLifecycleInOrderAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        using ProbeScreen screen = new();
        await using Application application = new(
            screen,
            terminal,
            terminal,
            TerminalOptions.Minimal);
        screen.Order.ShouldBe(["compose"]);
        screen.Attach(application);

        await application.StartAsync(TestContext.Current.CancellationToken);

        screen.Order.ShouldBe(["compose", "attach", "measure", "started"]);
        screen.ContentRoot.MeasureConstraints.ShouldNotBeEmpty();
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies attach rejects an uninitialized composition before binding the application.</summary>
    [Fact]
    public async Task Attach_WhenCompositionIsMissing_RejectsBeforeApplicationMutationAsync()
    {
        await using FakeTerminal terminal = new();
        using ProbeScreen screen = new(initializeContent: false);
        await using Application application = new(
            screen,
            terminal,
            terminal,
            TerminalOptions.Minimal);

        var exception = Should.Throw<InvalidOperationException>(() => screen.Attach(application));

        exception.Message.ShouldContain("composition", Case.Insensitive);
        screen.BoundApplication.ShouldBeNull();
        screen.Order.ShouldBeEmpty();
    }

    /// <summary>Verifies a failed attach hook clears the binding and never subscribes the started hook.</summary>
    [Fact]
    public async Task Attach_WhenHookThrows_RollsBackBindingAndStartedSubscriptionAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        using ProbeScreen screen = new(throwOnAttach: true);
        await using Application application = new(
            screen,
            terminal,
            terminal,
            TerminalOptions.Minimal);

        var exception = Should.Throw<InvalidOperationException>(() => screen.Attach(application));
        await application.StartAsync(TestContext.Current.CancellationToken);

        exception.Message.ShouldBe("The screen attach hook failed.");
        screen.BoundApplication.ShouldBeNull();
        screen.Order.ShouldBe(["compose", "attach", "measure"]);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a failed disposal hook cannot retain the application or skip owned cleanup.</summary>
    [Fact]
    public async Task Dispose_WhenHooksThrow_CompletesCleanupAndRethrowsEarliestFailureAsync()
    {
        await using FakeTerminal terminal = new();
        using ProbeScreen screen = new(throwOnDispose: true, throwOnContentDisposing: true);
        await using Application application = new(
            screen,
            terminal,
            terminal,
            TerminalOptions.Minimal);
        screen.Attach(application);

        var exception = Should.Throw<InvalidOperationException>(screen.Dispose);

        exception.Message.ShouldBe("The screen dispose hook failed.");
        screen.BoundApplication.ShouldBeNull();
        screen.ContentRoot.IsDisposed.ShouldBeTrue();
        screen.IsDisposed.ShouldBeTrue();
        screen.Order.ShouldBe(["compose", "attach", "dispose"]);
    }
}
