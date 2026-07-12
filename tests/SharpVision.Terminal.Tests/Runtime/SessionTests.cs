using SharpVision.Terminal.Capabilities;
using SharpVision.Terminal.Protocols;
using SharpVision.Terminal.Runtime;
using SharpVision.Terminal.Tests.Support;

using Shouldly;

using CapabilitySupport = SharpVision.Terminal.Capabilities.Support;
using Rune = System.Text.Rune;
using RuntimeOptions = SharpVision.Terminal.Runtime.Options;
using TerminalCapabilities = SharpVision.Terminal.Capabilities.Capabilities;

namespace SharpVision.Terminal.Tests.Runtime;

/// <summary>
/// Verifies terminal startup, ordered events, failure recovery, and reverse cleanup.
/// </summary>
public sealed class SessionTests
{
    /// <summary>Verifies replies and adjacent text retain transport order.</summary>
    [Fact]
    public async Task RunAsync_WhenReplyPrecedesText_RoutesBothInOrderAsync()
    {
        // Arrange
        await using var transport = new SessionTransport();
        await using var resize = new FakeResizeSource();
        var sink = new RuntimeSink();
        transport.Input("\u001b[?1;2cx"u8.ToArray());
        transport.Close();
        await using var session = new Session(
            transport,
            resize,
            sink,
            RuntimeOptions.Minimal);

        // Act
        await session.RunAsync(TestContext.Current.CancellationToken);

        // Assert
        sink.Responses.ShouldHaveSingleItem().Kind.ShouldBe(
            ResponseKind.PrimaryAttributes);
        sink.Order.ShouldBe(["response", "text", "closed"]);
    }

    /// <summary>
    /// Verifies supported modes wrap typed input and closure in exact reverse cleanup.
    /// </summary>
    [Fact]
    public async Task RunAsync_WhenInputCloses_EnablesForwardsAndRestoresAsync()
    {
        await using var transport = new SessionTransport();
        await using var resize = new FakeResizeSource();
        var sink = new RuntimeSink();
        transport.Input("x"u8.ToArray());
        transport.Close();
        var capabilities = Supported();
        await using var session = new Session(
            transport,
            resize,
            sink,
            new RuntimeOptions { Capabilities = capabilities });

        await session.RunAsync(TestContext.Current.CancellationToken);

        sink.Text.Single().Value.ShouldBe(new Rune('x'));
        sink.ClosedCount.ShouldBe(1);
        transport.JoinedWrites.ShouldBe(
            "\u001b[?1049h\u001b[?25l\u001b[?1004h\u001b[?2004h" +
            "\u001b[?1000h\u001b[?1006h\u001b[>3u" +
            "\u001b[<u\u001b[?1006l\u001b[?1000l\u001b[?2004l" +
            "\u001b[?1004l\u001b[?25h\u001b[?1049l");
    }

    /// <summary>
    /// Verifies resize is delivered with pixels before later closure.
    /// </summary>
    [Fact]
    public async Task RunAsync_WhenResizeArrives_ForwardsDimensionsAsync()
    {
        await using var transport = new SessionTransport();
        await using var resize = new FakeResizeSource();
        var sink = new RuntimeSink();
        await using var session = new Session(
            transport,
            resize,
            sink,
            RuntimeOptions.Minimal);
        var running = session.RunAsync(TestContext.Current.CancellationToken).AsTask();
        var expected = new Dimensions(
            new Geometry.Size(120, 40),
            new Geometry.Size(1200, 800));

        resize.Resize(expected);
        await sink.ResizeReceived.Task.WaitAsync(TestContext.Current.CancellationToken);
        transport.Close();
        await running;

        sink.Resizes.ShouldBe([expected]);
    }

    /// <summary>
    /// Verifies partial startup failure restores attempted modes and preserves identity.
    /// </summary>
    [Fact]
    public async Task RunAsync_WhenStartupWriteFails_RestoresAndPreservesExceptionAsync()
    {
        await using var transport = new SessionTransport { FailWriteAt = 3 };
        await using var resize = new FakeResizeSource();
        var sink = new RuntimeSink();
        var failure = transport.WriteFailure;
        await using var session = new Session(
            transport,
            resize,
            sink,
            new RuntimeOptions { Capabilities = Supported() });

        var thrown = await Should.ThrowAsync<IOException>(async () =>
            await session.RunAsync(TestContext.Current.CancellationToken));

        thrown.ShouldBeSameAs(failure);
        transport.JoinedWrites.ShouldEndWith("\u001b[?1004l\u001b[?25h\u001b[?1049l");
    }

    /// <summary>
    /// Verifies cancellation unblocks pending input and still restores terminal state.
    /// </summary>
    [Fact]
    public async Task RunAsync_WhenCancelled_RestoresModesAsync()
    {
        await using var transport = new SessionTransport();
        await using var resize = new FakeResizeSource();
        var sink = new RuntimeSink();
        await using var session = new Session(
            transport,
            resize,
            sink,
            new RuntimeOptions { Capabilities = Supported() });
        using var cancellation = new CancellationTokenSource();
        var running = session.RunAsync(cancellation.Token).AsTask();
        await transport.FirstRead.Task.WaitAsync(TestContext.Current.CancellationToken);

        cancellation.Cancel();
        _ = await Should.ThrowAsync<OperationCanceledException>(running);

        transport.JoinedWrites.ShouldEndWith("\u001b[?25h\u001b[?1049l");
    }

    /// <summary>
    /// Verifies cleanup failure remains diagnostic and cannot replace a read failure.
    /// </summary>
    [Fact]
    public async Task RunAsync_WhenReadAndCleanupFail_PreservesReadExceptionAsync()
    {
        await using var transport = new SessionTransport
        {
            ReadFailure = new IOException("read failed"),
            FailWriteAt = 7,
        };
        await using var resize = new FakeResizeSource();
        var sink = new RuntimeSink();
        await using var session = new Session(
            transport,
            resize,
            sink,
            new RuntimeOptions { Capabilities = Supported() });

        var thrown = await Should.ThrowAsync<IOException>(async () =>
            await session.RunAsync(TestContext.Current.CancellationToken));

        thrown.ShouldBeSameAs(transport.ReadFailure);
        session.LastCleanupException.ShouldBeSameAs(transport.WriteFailure);
        sink.Faults.ShouldBe([transport.ReadFailure]);
    }

    /// <summary>
    /// Verifies an input handler failure triggers cleanup and remains the primary error.
    /// </summary>
    [Fact]
    public async Task RunAsync_WhenInputHandlerFails_PreservesHandlerExceptionAsync()
    {
        await using var transport = new SessionTransport();
        await using var resize = new FakeResizeSource();
        var sink = new RuntimeSink { TextFailure = new InvalidOperationException("handler") };
        transport.Input("x"u8.ToArray());
        await using var session = new Session(
            transport,
            resize,
            sink,
            new RuntimeOptions { Capabilities = Supported() });

        var thrown = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await session.RunAsync(TestContext.Current.CancellationToken));

        thrown.ShouldBeSameAs(sink.TextFailure);
        transport.JoinedWrites.ShouldEndWith("\u001b[?25h\u001b[?1049l");
    }

    /// <summary>
    /// Verifies repeated disposal remains safe after a completed minimal session.
    /// </summary>
    [Fact]
    public async Task DisposeAsync_WhenCalledTwice_IsIdempotentAsync()
    {
        var transport = new SessionTransport();
        var resize = new FakeResizeSource();
        var sink = new RuntimeSink();
        transport.Close();
        var session = new Session(transport, resize, sink, RuntimeOptions.Minimal);
        await session.RunAsync(TestContext.Current.CancellationToken);

        await session.DisposeAsync();
        await session.DisposeAsync();
    }

    private static TerminalCapabilities Supported()
    {
        var supported = new Feature(CapabilitySupport.Supported, Origin.Override);
        return TerminalCapabilities.Conservative with
        {
            FocusReporting = supported,
            BracketedPaste = supported,
            CellMouse = supported,
            KittyKeyboard = supported,
        };
    }

}
