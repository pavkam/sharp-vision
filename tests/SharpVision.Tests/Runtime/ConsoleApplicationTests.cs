// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Runtime;

/// <summary>Verifies <see cref="ConsoleApplication"/> validation and host preflight paths.</summary>
/// <remarks>
/// In <see cref="RealProcessSignalGroup"/>: <see cref="RunCoreAsync_WhenRealPosixSignalRaised_StopsCleanlyAsync(int)"/>
/// raises a real POSIX signal against the current process, which every live signal registration in
/// the process observes regardless of which test raised it. Any other test added later that also
/// raises a real signal must join this same group - see the group's own remarks - rather than risk
/// racing this one.
/// </remarks>
[Collection(RealProcessSignalGroup.Name)]
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
        // The test host runs with redirected standard streams, so ConsoleHost.Interactive is false.
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
            _ => { },
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
            messages.Add,
            _ => { })
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
            messages.Add,
            _ => { })
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
    /// the already-faulted Completion unwrapped and uncaught.
    /// <para>
    /// The diagnostic writer is captured rather than defaulted. Reporting the fault is part of the
    /// hosting contract and is asserted here; letting it reach the real descriptor would dump an
    /// exception and stack trace onto the shared test host's standard error, where the test
    /// platform reprints it beside the run summary and it reads as a crash.
    /// </para>
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
        List<string> diagnostics = [];
        var builder = new ConsoleApplicationBuilder(
            screen,
            static () => true,
            _ => connection,
            _ => { },
            diagnostics.Add)
            .UseTerminalProfile(TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative));

        var run = ConsoleApplication.RunCoreAsync(builder, TestContext.Current.CancellationToken).AsTask();
        var application = await screen.Started.WaitAsync(TestContext.Current.CancellationToken);

        application.Fault(new InvalidOperationException("session boom"));

        var status = await run;

        status.ShouldBe(ConsoleRunStatus.Failed);
        diagnostics.ShouldHaveSingleItem().ShouldContain("session boom");
    }

    /// <summary>Verifies external cancellation reaches the guarded restoration path and reports
    /// <see cref="ConsoleRunStatus.Cancelled"/> regardless of <c>TreatControlCAsInput</c>.</summary>
    /// <param name="treatControlCAsInput">Whether Ctrl+C is opted out of cooperative shutdown.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RunCoreAsync_WhenCancelledExternally_StopsCleanlyRegardlessOfTreatControlCAsInputAsync(
        bool treatControlCAsInput)
    {
        // Arrange
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
            _ => { },
            _ => { })
            .UseTerminalProfile(TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative))
            .TreatControlCAsInput(treatControlCAsInput);

        using var cts = new CancellationTokenSource();

        // Act
        var run = ConsoleApplication.RunCoreAsync(builder, cts.Token).AsTask();
        _ = await screen.Started.WaitAsync(TestContext.Current.CancellationToken);

        cts.Cancel();
        var status = await run;

        // Assert
        status.ShouldBe(ConsoleRunStatus.Cancelled);
        restore.Disposals.ShouldBe(1);
    }

    /// <summary>Verifies a real <c>SIGTERM</c>/<c>SIGHUP</c> delivered to this process reaches the
    /// guarded restoration path, proving the registration is wired and unconditional rather than
    /// merely testing the same linked-token cancellation every other case in this class already
    /// exercises.</summary>
    /// <remarks>
    /// This raises the signal against the CURRENT process via a direct <c>kill</c> P/Invoke, the same
    /// pattern <c>UnixPseudoterminal</c> already uses to signal a child. It is safe: the production
    /// handler under test sets <see cref="PosixSignalContext.Cancel"/>, which suppresses the runtime's
    /// default terminating action for that signal, so the test host process itself is never killed -
    /// only the in-flight <see cref="ConsoleApplication"/> run observes the signal and stops. Placing
    /// the registration inside the <c>observeCtrlC</c>/<c>TreatControlCAsInput</c> gate, or omitting it
    /// entirely, would leave nothing to intercept the signal and this test would hang until timeout
    /// instead of observing <see cref="ConsoleRunStatus.Cancelled"/>.
    /// </remarks>
    /// <param name="signal">The raw POSIX signal number to raise (1 = <c>SIGHUP</c>, 15 = <c>SIGTERM</c>).</param>
    [Theory]
    [InlineData(1)]
    [InlineData(15)]
    public async Task RunCoreAsync_WhenRealPosixSignalRaised_StopsCleanlyAsync(int signal)
    {
        // Registration itself is no longer Unix-only, but raising the signal below goes through a
        // direct libc `kill(2)` P/Invoke, which does not exist on Windows: there is no equivalent
        // way to synthesize a CTRL_CLOSE_EVENT/CTRL_SHUTDOWN_EVENT from within the same process, so
        // this test - the raise mechanism, not the feature it exercises - stays Unix-only.
        Assert.SkipUnless(!OperatingSystem.IsWindows(), "Raising a signal via libc kill(2) requires Unix.");

        // Arrange
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
            _ => { },
            _ => { })
            .UseTerminalProfile(TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative));

        // Act
        var run = ConsoleApplication.RunCoreAsync(builder, CancellationToken.None).AsTask();
        _ = await screen.Started.WaitAsync(TestContext.Current.CancellationToken);

        RaiseSignal(GetProcessId(), signal).ShouldBe(0);
        var status = await run;

        // Assert
        status.ShouldBe(ConsoleRunStatus.Cancelled);
        restore.Disposals.ShouldBe(1);
    }

    /// <summary>Verifies a token already cancelled before <c>RunCoreAsync</c> is even called still lets
    /// <c>Build()</c> run to completion - entering raw/VT mode and attaching the screen - instead of
    /// leaving the process to hit the OS default disposition on a real signal, and that the guarded
    /// <c>StopAsync</c> path restores the terminal exactly once afterward.</summary>
    /// <remarks>
    /// This stands in for a real signal landing while <c>Build()</c> is blocked inside the screen's
    /// synchronous <c>OnAttach</c>: production code cannot abort that call safely, so the fix instead
    /// moves signal registration up to wrap <c>Build()</c> as well as the run, and a signal there only
    /// cancels the shared linked token - exactly what an already-cancelled caller token does here.
    /// <c>Build()</c> never observes the token at all, so it is expected to complete unconditionally
    /// once started; <see cref="StartedSignalScreen"/>'s <c>OnStarted</c> hook cannot be reused as the
    /// completion signal for that, because <c>Application.StartAsync</c> short-circuits on an
    /// already-cancelled token before the session ever reaches its first committed frame, so
    /// <c>OnStarted</c> never fires. <c>Screen.OnAttach</c> does fire unconditionally, because
    /// <c>Build()</c> runs it before <c>RunApplicationAsync</c> - and its cancellation check - is ever
    /// reached.
    /// </remarks>
    [Fact]
    public async Task RunCoreAsync_WhenCancelledDuringBuild_StillRestoresTerminalModeAsync()
    {
        // Arrange
        var screen = new AttachedSignalScreen();
        var transport = new ConsoleApplicationTransport();
        var resize = new ConsoleApplicationResizeSource();
        var restore = new ConsoleApplicationRestoreLease();
        resize.QueueResize(new Dimensions(new Size(10, 4)));
        var connection = new ConsoleConnection(transport, resize, restore);
        var builder = new ConsoleApplicationBuilder(
            screen,
            static () => true,
            _ => connection,
            _ => { },
            _ => { })
            .UseTerminalProfile(TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative));

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var status = await ConsoleApplication.RunCoreAsync(builder, cts.Token);

        // Assert
        status.ShouldBe(ConsoleRunStatus.Cancelled);
        restore.Disposals.ShouldBe(1);
        screen.Attached.IsCompletedSuccessfully.ShouldBeTrue();
    }

    // DllImport, not LibraryImport: this project does not enable AllowUnsafeBlocks, and the
    // LibraryImport source generator requires it project-wide even for this trivial signature.
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Interoperability",
        "SYSLIB1054:Use LibraryImportAttribute",
        Justification = "AllowUnsafeBlocks is not enabled for this test project.")]
    [DllImport("libc", EntryPoint = "kill", SetLastError = true)]
    private static extern int RaiseSignal(int processId, int signal);

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Interoperability",
        "SYSLIB1054:Use LibraryImportAttribute",
        Justification = "AllowUnsafeBlocks is not enabled for this test project.")]
    [DllImport("libc", EntryPoint = "getpid")]
    private static extern int GetProcessId();

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

    /// <summary>Signals the exact application instance once the Attach hook fires, proving
    /// <c>Build()</c> reached and completed the attach step.</summary>
    private sealed class AttachedSignalScreen: Screen
    {
        private readonly TaskCompletionSource<Application> _attached =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal AttachedSignalScreen() => InitializeContent(new ProbeControl());

        internal Task<Application> Attached => _attached.Task;

        protected override void OnAttach(Application application)
        {
            base.OnAttach(application);
            _ = _attached.TrySetResult(application);
        }
    }
}
