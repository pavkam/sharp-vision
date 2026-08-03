// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Runtime;

/// <summary>Verifies <see cref="ConsoleApplication"/> validation and host preflight paths.</summary>
public sealed class ConsoleApplicationTests
{
    /// <summary>Verifies adding unsupported-terminal reporting preserves every existing numeric status.</summary>
    [Fact]
    public void ConsoleRunStatus_WhenConvertedToInteger_PreservesCompatibilityValues()
    {
        ((int) ConsoleRunStatus.Redirected).ShouldBe(0);
        ((int) ConsoleRunStatus.Completed).ShouldBe(1);
        ((int) ConsoleRunStatus.Cancelled).ShouldBe(2);
        ((int) ConsoleRunStatus.Failed).ShouldBe(3);
        ((int) ConsoleRunStatus.UnsupportedTerminal).ShouldBe(4);
    }

    /// <summary>Verifies the builder factory rejects a null screen.</summary>
    [Fact]
    public void CreateBuilder_WhenScreenNull_Throws() =>
        Should.Throw<ArgumentNullException>(() => ConsoleApplication.CreateBuilder(screen: null!));

    /// <summary>Verifies a redirected console short-circuits without starting the application.</summary>
    [Fact]
    public async Task RunAsync_WhenConsoleRedirected_ReturnsRedirectedAsync()
    {
        // The test host runs with redirected standard streams, so ConsoleHost.IsInteractive is false.
        var status = await ConsoleApplication.RunAsync(new ProbeScreen());

        status.ShouldBe(ConsoleRunStatus.Redirected);
    }

    /// <summary>Verifies every explicit unsuitable description is rejected before application attachment or bytes.</summary>
    /// <param name="suitability">The unsuitable full-screen classification.</param>
    [Theory]
    [InlineData(Suitability.Missing)]
    [InlineData(Suitability.Generic)]
    [InlineData(Suitability.Hardcopy)]
    [InlineData(Suitability.Incomplete)]
    [InlineData(Suitability.UnsupportedPadding)]
    public void Build_WhenProfileIsUnsuitable_ThrowsBeforeApplicationAndDisposesResources(
        Suitability suitability)
    {
        // Arrange
        var screen = new ProbeScreen();
        var disposalOrder = new List<string>();
        var transport = new ConsoleApplicationTransport(disposalOrder);
        var resize = new ConsoleApplicationResizeSource(disposalOrder);
        var restore = new ConsoleApplicationRestoreLease(disposalOrder);
        var connection = new ConsoleConnection(transport, resize, restore);
        var builder = new ConsoleApplicationBuilder(
            screen,
            static () => true,
            _ => connection,
            _ => { })
            .UseTerminalProfile(Unsuitable(suitability));

        // Act
        _ = Should.Throw<NotSupportedException>(builder.Build);

        // Assert
        screen.Dispatcher.ShouldBeNull();
        transport.Writes.ShouldBeEmpty();
        transport.Disposals.ShouldBe(1);
        resize.Disposals.ShouldBe(1);
        restore.Disposals.ShouldBe(1);
        disposalOrder.ShouldBe(["resize", "transport", "restore"]);
    }

    /// <summary>Verifies ordinary description absence maps only to the unsupported status and optional plain message.</summary>
    [Fact]
    public async Task RunAsync_WhenDescriptionIsUnavailable_ReturnsUnsupportedWithoutTerminalBytesAsync()
    {
        // Arrange
        var messages = new List<string>();
        var screen = new ProbeScreen();
        var transport = new ConsoleApplicationTransport();
        var resize = new ConsoleApplicationResizeSource();
        var restore = new ConsoleApplicationRestoreLease();
        var connection = new ConsoleConnection(transport, resize, restore);
        var builder = new ConsoleApplicationBuilder(
            screen,
            static () => true,
            _ => connection,
            messages.Add)
            .WithUnsupportedTerminalMessage("This terminal is not supported.");

        // Act
        var status = await builder.RunAsync(TestContext.Current.CancellationToken);

        // Assert
        status.ShouldBe(ConsoleRunStatus.UnsupportedTerminal);
        messages.ShouldBe(["This terminal is not supported."]);
        screen.Dispatcher.ShouldBeNull();
        transport.Writes.ShouldBeEmpty();
        transport.Disposals.ShouldBe(1);
        resize.Disposals.ShouldBe(1);
        restore.Disposals.ShouldBe(1);
    }

    /// <summary>Verifies unrelated NotSupportedException values are never mistaken for terminal unsuitability.</summary>
    [Fact]
    public async Task RunAsync_WhenHostThrowsNotSupported_PropagatesExceptionAsync()
    {
        var expected = new NotSupportedException("host failure");
        var messages = new List<string>();
        var builder = new ConsoleApplicationBuilder(
            new ProbeScreen(),
            static () => true,
            _ => throw expected,
            messages.Add)
            .WithUnsupportedTerminalMessage("unsupported");

        var thrown = await Should.ThrowAsync<NotSupportedException>(async () =>
            await builder.RunAsync(TestContext.Current.CancellationToken));

        thrown.ShouldBeSameAs(expected);
        messages.ShouldBeEmpty();
    }

    /// <summary>Verifies a fault reported after startup surfaces as Failed instead of an unhandled exception.</summary>
    /// <remarks>
    /// Task.WhenAny never adopts the winning task's status; a post-startup fault on
    /// Application.Completion must be explicitly awaited to observe it, or it is lost and
    /// RunApplicationAsync falls through to the unguarded StopAsync call below, which rethrows
    /// the already-faulted Completion unwrapped and uncaught (see #132).
    /// </remarks>
    [Fact]
    public async Task RunAsync_WhenApplicationFailsAfterStartup_ReturnsFailedAsync()
    {
        var screen = new StartedSignalScreen();
        var transport = new ConsoleApplicationTransport();
        var resize = new ConsoleApplicationResizeSource();
        var restore = new ConsoleApplicationRestoreLease();
        resize.QueueResize(new Dimensions(new Size(10, 4)));
        var connection = new ConsoleConnection(transport, resize, restore);
        var builder = new ConsoleApplicationBuilder(
            screen,
            static () => true,
            _ => connection,
            _ => { })
            .UseTerminalProfile(TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative));

        var run = ConsoleApplication.RunCoreAsync(builder, TestContext.Current.CancellationToken).AsTask();
        var application = await screen.Started.WaitAsync(TestContext.Current.CancellationToken);

        application.Fault(new InvalidOperationException("session boom"));

        var status = await run;

        status.ShouldBe(ConsoleRunStatus.Failed);
    }

    private static TerminalProfile Unsuitable(Suitability suitability) => new(
        new Description("fixture", DescriptionOrigin.Explicit, suitability),
        TerminalCapabilities.Conservative);

    /// <summary>Signals the exact application instance once the Started hook fires.</summary>
    private sealed class StartedSignalScreen: Screen
    {
        private readonly TaskCompletionSource<Application> _started =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal StartedSignalScreen() => InitializeContent(new ProbeControl());

        internal Task<Application> Started => _started.Task;

        protected override void OnStarted(Application application)
        {
            base.OnStarted(application);
            _ = _started.TrySetResult(application);
        }
    }
}
