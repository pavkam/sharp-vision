// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Runtime;

using Input;

using Terminal.Capabilities;

/// <summary>Verifies terminal protocol responses traverse Session and Application onto the dispatcher.</summary>
public sealed class ProtocolRoutingTests
{
    /// <summary>Verifies exact DCS identities remain observable in transport order across every tracker outcome.</summary>
    [Fact]
    public async Task Input_WhenNegotiatedDcsResponsesVaryByMatch_PublishesOwnedTypedEventsInOrderAsync()
    {
        // Arrange
        var clock = new ManualTimeProvider();
        var limits = QueryLimits.Default with { QueryTimeout = TimeSpan.FromMilliseconds(100) };
        var options = TerminalOptions.Minimal with
        {
            Negotiation = new NegotiationOptions(
                new Dictionary<string, string?> { ["TERM"] = "xterm-256color" },
                limits: limits)
        };
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(12, 4)));
        await using Application application = new(
            new ProbeControl(),
            terminal,
            terminal,
            options,
            timeProvider: clock);
        var queryWritten = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var firstBatch = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var firstRouted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var completed = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var order = new List<string>();
        var capabilityResponses = new List<CapabilityResponse>();
        var reads = 0;
        terminal.Written += _ => queryWritten.TrySetResult();
        terminal.InputRead += value =>
        {
            _ = value;

            if (Interlocked.Increment(ref reads) == 2)
            {
                _ = firstRouted.TrySetResult();
            }
        };
        application.StatusResponseReceived += (_, eventArgs) =>
        {
            application.Dispatcher.CheckAccess().ShouldBeTrue();
            order.Add($"status:{eventArgs.Response.Name}:{Encoding.ASCII.GetString(eventArgs.Response.Value.Span)}");
            CompleteSignals();
        };
        application.CapabilityResponseReceived += (_, eventArgs) =>
        {
            application.Dispatcher.CheckAccess().ShouldBeTrue();
            capabilityResponses.Add(eventArgs.Response);
            order.Add($"capability:{Encoding.ASCII.GetString(eventArgs.Response.Items[CapabilityName.DirectColor].Span)}");
            CompleteSignals();
        };

        void CompleteSignals()
        {
            if (order.Count == 3)
            {
                _ = firstBatch.TrySetResult();
            }

            if (order.Count == 4)
            {
                _ = completed.TrySetResult();
            }
        }

        // Act
        var starting = application.StartAsync(TestContext.Current.CancellationToken).AsTask();
        await queryWritten.Task.WaitAsync(
            TimeSpan.FromSeconds(2),
            TestContext.Current.CancellationToken);
        Encoding.ASCII.GetString(terminal.Writes[0]).ShouldContain(
            "\u001bP+q524742\u001b\\");
        var first = Encoding.ASCII.GetBytes(
            "\u001bP1$r0m\u001b\\" +
            "\u001bP1+r524742=3234\u001b\\" +
            "\u001bP1+r524742=3234\u001b\\");
        terminal.QueueInput(first);
        var overwrite = new byte[first.Length];
        overwrite.AsSpan().Fill((byte) 'x');
        terminal.QueueInput(overwrite);
        first.AsSpan().Fill((byte) 'x');
        await firstRouted.Task.WaitAsync(
            TimeSpan.FromSeconds(2),
            TestContext.Current.CancellationToken);
        clock.Advance(limits.QueryTimeout);
        await starting.WaitAsync(
            TimeSpan.FromSeconds(2),
            TestContext.Current.CancellationToken);
        await firstBatch.Task.WaitAsync(
            TimeSpan.FromSeconds(2),
            TestContext.Current.CancellationToken);
        terminal.QueueInput("\u001bP1$r>4;2m\u001b\\"u8);
        await completed.Task.WaitAsync(
            TimeSpan.FromSeconds(2),
            TestContext.Current.CancellationToken);

        // Assert
        order.ShouldBe(
        [
            "status:Rendition:0m",
            "capability:24",
            "capability:24",
            "status:ModifyOtherKeys:>4;2m"
        ]);
        capabilityResponses.Count.ShouldBe(2);

        foreach (var response in capabilityResponses)
        {
            response.Items[CapabilityName.DirectColor].ToArray().ShouldBe("24"u8.ToArray());
        }

        await application.StopAsync(TestContext.Current.CancellationToken);
    }

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
        var received = new TaskCompletionSource<Response>(TaskCreationOptions.RunContinuationsAsynchronously);
        application.ResponseReceived += (_, eventArgs) =>
        {
            application.Dispatcher.CheckAccess().ShouldBeTrue();
            _ = received.TrySetResult(eventArgs.Response);
        };

        await application.StartAsync(TestContext.Current.CancellationToken);
        terminal.QueueInput("\u001b[?1;2c"u8);

        var response = await received.Task.WaitAsync(
            TimeSpan.FromSeconds(2),
            TestContext.Current.CancellationToken);

        response.Kind.ShouldBe(ResponseKind.PrimaryAttributes);
        response.Values.ToArray().ShouldBe([1, 2]);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies palette, metrics, and numeric replies retain transport order on the dispatcher.</summary>
    [Fact]
    public async Task Input_WhenStandardResponsesArrive_PublishesTypedEventsInOrderAsync()
    {
        // Arrange
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(12, 4)));
        await using Application application = new(
            new ProbeControl(),
            terminal,
            terminal,
            TerminalOptions.Minimal);
        var order = new List<string>();
        var received = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        application.PaletteResponseReceived += (_, eventArgs) =>
        {
            application.Dispatcher.CheckAccess().ShouldBeTrue();
            eventArgs.Response.Kind.ShouldBe(ResponseKind.PaletteColor);
            order.Add("palette");
        };
        application.MetricsResponseReceived += (_, eventArgs) =>
        {
            application.Dispatcher.CheckAccess().ShouldBeTrue();
            eventArgs.Response.Size.ShouldBe(new Size(1200, 800));
            order.Add("metrics");
        };
        application.ResponseReceived += (_, eventArgs) =>
        {
            application.Dispatcher.CheckAccess().ShouldBeTrue();
            eventArgs.Response.Kind.ShouldBe(ResponseKind.SecondaryAttributes);
            order.Add("numeric");
            _ = received.TrySetResult();
        };
        await application.StartAsync(TestContext.Current.CancellationToken);

        // Act
        terminal.QueueInput(Encoding.ASCII.GetBytes(
            "\u001b]4;0;rgb:1111/2222/3333\u001b\\" +
            "\u001b[4;800;1200t\u001b[>41;410;0c"));
        await received.Task.WaitAsync(
            TimeSpan.FromSeconds(2),
            TestContext.Current.CancellationToken);

        // Assert
        order.ShouldBe(["palette", "metrics", "numeric"]);
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
        var received = new TaskCompletionSource<Diagnostic>(TaskCreationOptions.RunContinuationsAsynchronously);
        application.Diagnostic += (_, eventArgs) =>
        {
            application.Dispatcher.CheckAccess().ShouldBeTrue();
            _ = received.TrySetResult(eventArgs.Diagnostic);
        };

        await application.StartAsync(TestContext.Current.CancellationToken);
        terminal.QueueInput("\u001b]777;secret\u001b\\"u8);

        var diagnostic = await received.Task.WaitAsync(
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
        var received = new TaskCompletionSource<Diagnostic>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        application.Diagnostic += (_, eventArgs) =>
            _ = received.TrySetResult(eventArgs.Diagnostic);
        await application.StartAsync(TestContext.Current.CancellationToken);

        // Act
        terminal.QueueInput("\u001bP1;2$qpayload\u001b\\"u8);
        var diagnostic = await received.Task.WaitAsync(
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
