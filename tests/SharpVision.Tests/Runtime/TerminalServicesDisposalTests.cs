// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Runtime;

using System.Reflection;

using SharpVision.Runtime;
using SharpVision.Terminal.Capabilities;

/// <summary>Verifies the clipboard work an application owns is torn down when it is.
///
/// <para><c>TerminalServices</c> owns a Kitty clipboard transaction and the <c>DispatcherTimer</c>
/// enforcing its deadline, and implemented no disposal interface. A transaction still in flight at
/// shutdown left both alive: <c>DispatcherTimer.Start</c> arms the underlying timer
/// <em>periodically</em>, so once the dispatcher stopped, the elapsed callback posted to it,
/// swallowed the <c>ObjectDisposedException</c>, and re-armed on the next period forever. Nothing
/// ever reached the completion path that disposes the timer.</para>
///
/// <para>The live callback then rooted the timer's closure, the services, the Application, and
/// through it the dispatcher, renderer, and whole control tree - so disposing the Application did
/// not collect any of it. Copy, then quit was enough to trigger it on a Kitty-authoritative
/// terminal, which is what <c>TextInput</c>'s Ctrl+C binding does.</para>
/// </summary>
public sealed class TerminalServicesDisposalTests
{
    /// <summary>The regression this file exists to pin: stopping with a transaction in flight must
    /// not leave a timer armed. A still-live periodic timer keeps posting after the dispatcher has
    /// stopped, which surfaces here as continued writes or a fault after shutdown.</summary>
    [Fact]
    public async Task StopAsync_WhenAKittyClipboardWriteIsInFlight_LeavesNoArmedTimerAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        await using Application application = new(new ProbeControl(), terminal, terminal, KittyOptions());
        await application.StartAsync(TestContext.Current.CancellationToken);

        // Opens a transaction and arms the deadline timer; no reply is ever fed back, so the
        // transaction is still outstanding when the application stops.
        application.Terminal.Clipboard.Write("hello");
        await FlushAsync(application);
        _ = ArmedTimer(application).ShouldNotBeNull("the write must arm a deadline timer to test anything");

        await application.StopAsync(TestContext.Current.CancellationToken);

        // Observed directly rather than through side effects: a leaked timer posts to the stopped
        // dispatcher and swallows the ObjectDisposedException, so it emits nothing an assertion on
        // writes or faults could see. Only the field itself shows whether it is still armed.
        ArmedTimer(application).ShouldBeNull("a transaction outstanding at shutdown must not leave a timer armed");
    }

    /// <summary>Verifies the same for a read, which takes the other transaction entry point.</summary>
    [Fact]
    public async Task StopAsync_WhenAKittyClipboardRequestIsInFlight_LeavesNoArmedTimerAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        await using Application application = new(new ProbeControl(), terminal, terminal, KittyOptions());
        await application.StartAsync(TestContext.Current.CancellationToken);

        application.Terminal.Clipboard.Request();
        await FlushAsync(application);
        _ = ArmedTimer(application).ShouldNotBeNull();

        await application.StopAsync(TestContext.Current.CancellationToken);

        ArmedTimer(application).ShouldBeNull();
    }

    /// <summary>Verifies disposal without any clipboard traffic is still clean, so the teardown
    /// cannot depend on a transaction existing.</summary>
    [Fact]
    public async Task StopAsync_WhenNoClipboardWorkHappened_StopsCleanlyAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        await using Application application = new(new ProbeControl(), terminal, terminal, KittyOptions());
        await application.StartAsync(TestContext.Current.CancellationToken);

        await application.StopAsync(TestContext.Current.CancellationToken);

        application.Failure.ShouldBeNull();
    }

    /// <summary>Verifies a completed transaction still tears down cleanly, so the new disposal does
    /// not double-dispose what the completion path already released.</summary>
    [Fact]
    public async Task StopAsync_WhenAWriteWasSupersededBeforeShutdown_StopsCleanlyAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        await using Application application = new(new ProbeControl(), terminal, terminal, KittyOptions());
        await application.StartAsync(TestContext.Current.CancellationToken);

        // The second write supersedes the first, which disposes the first transaction and its
        // timer through the cancellation path rather than the completion one.
        application.Terminal.Clipboard.Write("first");
        application.Terminal.Clipboard.Write("second");
        await application.StopAsync(TestContext.Current.CancellationToken);

        application.Failure.ShouldBeNull();
    }

    // Write and Request queue their transaction onto the dispatcher, so the timer is not armed on
    // the calling thread; a round-trip is required before the field means anything.
    private static async Task FlushAsync(Application application) =>
        await application.Dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);

    // The timer is private state of an internal type; nothing public reports whether it is armed,
    // and the leak is invisible from outside precisely because the leaked callback is silent.
    private static object? ArmedTimer(Application application) =>
        typeof(TerminalServices)
            .GetField("_kittyTimeoutTimer", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(application.Terminal);

    private static TerminalOptions KittyOptions()
    {
        var supported = new Feature(CapabilitySupport.Supported, Origin.Override);
        return TerminalOptions.Minimal with
        {
            Capabilities = TerminalCapabilities.Conservative with { KittyClipboard = supported }
        };
    }
}
