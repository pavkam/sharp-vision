// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Runtime;

using SharpVision.Runtime;
using SharpVision.Terminal.Runtime;


using TerminalOptions = Terminal.Runtime.Options;

/// <summary>Verifies terminal protocol responses traverse Session and Application onto the dispatcher.</summary>
public sealed class ProtocolRoutingTests
{
    /// <summary>Verifies a DA response arrives through the running session as a dispatcher-affine typed application event.</summary>
    [Fact]
    public async Task Input_WhenDeviceAttributesResponseArrives_PublishesTypedDispatcherEventAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(12, 4)));
        await using Application application = new(
            new ProbeControl(),
            terminal,
            terminal,
            TerminalOptions.Minimal);
        TaskCompletionSource<Response> received = new(TaskCreationOptions.RunContinuationsAsynchronously);
        application.ResponseReceived += (_, eventArgs) =>
        {
            application.Dispatcher.CheckAccess().ShouldBeTrue();
            _ = received.TrySetResult(eventArgs.Response);
        };

        await application.StartAsync(TestContext.Current.CancellationToken);
        terminal.QueueInput("\u001b[?1;2c"u8);

        Response response = await received.Task.WaitAsync(
            TimeSpan.FromSeconds(2),
            TestContext.Current.CancellationToken);

        response.Kind.ShouldBe(ResponseKind.PrimaryAttributes);
        response.Values.ToArray().ShouldBe([1, 2]);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies an unregistered extension string reaches the application only as a redacted structural diagnostic.</summary>
    [Fact]
    public async Task Input_WhenUnknownOscArrives_PublishesRedactedDiagnosticAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(12, 4)));
        await using Application application = new(
            new ProbeControl(),
            terminal,
            terminal,
            TerminalOptions.Minimal);
        TaskCompletionSource<Diagnostic> received = new(TaskCreationOptions.RunContinuationsAsynchronously);
        application.Diagnostic += (_, eventArgs) =>
        {
            application.Dispatcher.CheckAccess().ShouldBeTrue();
            _ = received.TrySetResult(eventArgs.Diagnostic);
        };

        await application.StartAsync(TestContext.Current.CancellationToken);
        terminal.QueueInput("\u001b]777;secret\u001b\\"u8);

        Diagnostic diagnostic = await received.Task.WaitAsync(
            TimeSpan.FromSeconds(2),
            TestContext.Current.CancellationToken);

        diagnostic.Code.ShouldBe(DiagnosticCode.Unsupported);
        diagnostic.Kind.ShouldBe(SequenceKind.Osc);
        diagnostic.DiscardedBytes.ShouldBeGreaterThan(0);
        diagnostic.ToString().ShouldNotContain("secret");
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies redacted DCS diagnostics count the owned header and payload.</summary>
    [Fact]
    public async Task Input_WhenUnknownDcsArrives_CountsAllOwnedSequenceBytesAsync()
    {
        // Arrange
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(12, 4)));
        await using Application application = new(
            new ProbeControl(),
            terminal,
            terminal,
            TerminalOptions.Minimal);
        TaskCompletionSource<Diagnostic> received = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        application.Diagnostic += (_, eventArgs) =>
            _ = received.TrySetResult(eventArgs.Diagnostic);
        await application.StartAsync(TestContext.Current.CancellationToken);

        // Act
        terminal.QueueInput("\u001bP1;2$qpayload\u001b\\"u8);
        Diagnostic diagnostic = await received.Task.WaitAsync(
            TimeSpan.FromSeconds(2),
            TestContext.Current.CancellationToken);

        // Assert
        diagnostic.Code.ShouldBe(DiagnosticCode.Unsupported);
        diagnostic.Kind.ShouldBe(SequenceKind.Dcs);
        diagnostic.DiscardedBytes.ShouldBe(12);
        diagnostic.ToString().ShouldNotContain("payload");
        await application.StopAsync(TestContext.Current.CancellationToken);
    }
}
