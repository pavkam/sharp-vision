namespace SharpVision.Terminal.Tests.Runtime;

using SharpVision.Terminal.Capabilities;
using SharpVision.Terminal.Protocols;
using SharpVision.Terminal.Runtime;
using SharpVision.Terminal.Tests.Capabilities;
using SharpVision.Terminal.Tests.Support;

using Shouldly;

using CapabilitySupport = Terminal.Capabilities.Support;
using Rune = Rune;
using RuntimeOptions = Terminal.Runtime.Options;
using TerminalCapabilities = Terminal.Capabilities.Capabilities;

/// <summary>
/// Verifies terminal startup, ordered events, failure recovery, and reverse cleanup.
/// </summary>
public sealed class SessionTests
{
    /// <summary>Verifies early EOF publishes fallback without enabling optional modes.</summary>
    [Fact]
    public async Task RunAsync_WhenNegotiationEndsAtEof_PublishesWithoutOptionalModesAsync()
    {
        // Arrange
        await using var transport = new SessionTransport();
        await using var resize = new FakeResizeSource();
        var sink = new RuntimeSink();
        var options = RuntimeOptions.Minimal with
        {
            Focus = true,
            Negotiation = new NegotiationOptions(
                new Dictionary<string, string?> { ["TERM"] = "xterm-kitty" }),
        };
        transport.Close();
        await using var session = new Session(transport, resize, sink, options);

        // Act
        await session.RunAsync(TestContext.Current.CancellationToken);

        // Assert
        sink.Profiles.ShouldHaveSingleItem().KittyKeyboard.ShouldBe(
            new Feature(CapabilitySupport.Tentative, Origin.Environment));
        sink.Order.ShouldBe(["profile", "closed"]);
        transport.JoinedWrites.ShouldBe(
            "\u001b[?u\u001b[c\u001b[?2026$p\u001b[?1004$p" +
            "\u001b[?2004$p\u001b[?1006$p\u001b[?1016$p");
    }

    /// <summary>Verifies missing replies release startup at one finite deadline.</summary>
    [Fact]
    public async Task RunAsync_WhenNegotiationTimesOut_PublishesAndReleasesResizeAsync()
    {
        // Arrange
        await using var transport = new SessionTransport();
        await using var resize = new FakeResizeSource();
        var sink = new RuntimeSink();
        var clock = new ManualTimeProvider();
        var limits = Limits.Default with
        {
            QueryTimeout = TimeSpan.FromSeconds(1),
        };
        var options = RuntimeOptions.Minimal with
        {
            Negotiation = new NegotiationOptions(
                new Dictionary<string, string?> { ["TERM"] = "xterm-kitty" },
                limits: limits),
        };
        await using var session = new Session(
            transport,
            resize,
            sink,
            options,
            clock);
        var running = session.RunAsync(TestContext.Current.CancellationToken).AsTask();
        await transport.FirstRead.Task.WaitAsync(TestContext.Current.CancellationToken);
        var dimensions = new Dimensions(new Geometry.Size(80, 24));

        // Act
        resize.Resize(dimensions);
        clock.Advance(limits.QueryTimeout);
        await sink.ProfileReceived.Task.WaitAsync(
            TimeSpan.FromSeconds(2),
            TestContext.Current.CancellationToken);
        transport.Close();
        await running;

        // Assert
        sink.Profiles.ShouldHaveSingleItem().KittyKeyboard.ShouldBe(
            new Feature(CapabilitySupport.Tentative, Origin.Environment));
        sink.Resizes.ShouldBe([dimensions]);
        sink.Order.IndexOf("profile").ShouldBeLessThan(sink.Order.IndexOf("resize"));
    }

    /// <summary>Verifies capacity one sends only DA and can complete startup early.</summary>
    [Fact]
    public async Task RunAsync_WhenNegotiationCapacityIsOne_CompletesFromDeviceAttributesAsync()
    {
        // Arrange
        await using var transport = new SessionTransport();
        await using var resize = new FakeResizeSource();
        var sink = new RuntimeSink();
        var options = RuntimeOptions.Minimal with
        {
            Negotiation = new NegotiationOptions(
                new Dictionary<string, string?>(),
                limits: Limits.Default with { MaxConcurrentQueries = 1 }),
        };
        transport.Input("\u001b[?1;2c"u8.ToArray());
        transport.Close();
        await using var session = new Session(transport, resize, sink, options);

        // Act
        await session.RunAsync(TestContext.Current.CancellationToken);

        // Assert
        transport.JoinedWrites.ShouldBe("\u001b[c");
        _ = sink.Profiles.ShouldHaveSingleItem();
        sink.Order.ShouldBe(["response", "profile", "closed"]);
    }

    /// <summary>Verifies a pre-publication resize storm retains only its newest value.</summary>
    [Fact]
    public async Task RunAsync_WhenResizeStormPrecedesDeadline_ForwardsOnlyNewestAsync()
    {
        // Arrange
        await using var transport = new SessionTransport();
        await using var resize = new FakeResizeSource { SignalAfterReads = 3 };
        var sink = new RuntimeSink();
        var clock = new ManualTimeProvider();
        var limits = Limits.Default with { QueryTimeout = TimeSpan.FromSeconds(1) };
        var options = RuntimeOptions.Minimal with
        {
            Negotiation = new NegotiationOptions(
                new Dictionary<string, string?>(),
                limits: limits),
        };
        var first = new Dimensions(new Geometry.Size(80, 24));
        var second = new Dimensions(new Geometry.Size(100, 30));
        var newest = new Dimensions(new Geometry.Size(120, 40));
        resize.Resize(first);
        resize.Resize(second);
        resize.Resize(newest);
        await using var session = new Session(transport, resize, sink, options, clock);
        var running = session.RunAsync(TestContext.Current.CancellationToken).AsTask();
        await resize.ReadsObserved.Task.WaitAsync(TestContext.Current.CancellationToken);

        // Act
        clock.Advance(limits.QueryTimeout);
        await sink.ProfileReceived.Task.WaitAsync(TestContext.Current.CancellationToken);
        transport.Close();
        await running;

        // Assert
        sink.Resizes.ShouldBe([newest]);
        sink.Order.IndexOf("profile").ShouldBeLessThan(sink.Order.IndexOf("resize"));
    }

    /// <summary>Verifies negotiation publishes before the retained first resize.</summary>
    [Fact]
    public async Task RunAsync_WhenNegotiationRepliesComplete_PublishesBeforeResizeAsync()
    {
        // Arrange
        await using var transport = new SessionTransport();
        await using var resize = new FakeResizeSource();
        var sink = new RuntimeSink();
        var options = RuntimeOptions.Minimal with
        {
            Negotiation = new NegotiationOptions(
                new Dictionary<string, string?>()),
        };
        await using var session = new Session(transport, resize, sink, options);
        var running = session.RunAsync(TestContext.Current.CancellationToken).AsTask();
        await transport.FirstRead.Task.WaitAsync(TestContext.Current.CancellationToken);
        var dimensions = new Dimensions(new Geometry.Size(80, 24));

        // Act
        transport.Input(Encoding.ASCII.GetBytes(
            "x" +
            "\u001b[?1016;1$y\u001b[?1006;1$y\u001b[?2004;1$y" +
            "\u001b[?1004;1$y\u001b[?2026;1$y\u001b[?3u\u001b[?1;2c"));
        resize.Resize(dimensions);
        await sink.ResizeReceived.Task.WaitAsync(TestContext.Current.CancellationToken);
        transport.Close();
        await running;

        // Assert
        sink.Profiles.ShouldHaveSingleItem().SynchronizedOutput.IsSupported.ShouldBeTrue();
        sink.Order.IndexOf("text").ShouldBeLessThan(sink.Order.IndexOf("profile"));
        sink.Order.IndexOf("profile").ShouldBeLessThan(sink.Order.IndexOf("resize"));
        sink.Resizes.ShouldBe([dimensions]);
        transport.JoinedWrites.ShouldStartWith(
            "\u001b[?u\u001b[c\u001b[?2026$p\u001b[?1004$p" +
            "\u001b[?2004$p\u001b[?1006$p\u001b[?1016$p");
    }

    /// <summary>Verifies negotiated modes enable after publication and unwind in reverse.</summary>
    [Fact]
    public async Task RunAsync_WhenNegotiationProvesModes_EnablesAndRestoresExactlyAsync()
    {
        // Arrange
        await using var transport = new SessionTransport();
        await using var resize = new FakeResizeSource();
        var sink = new RuntimeSink();
        var options = RuntimeOptions.Minimal with
        {
            Focus = true,
            Paste = true,
            Tracking = MouseTracking.Press,
            Keyboard = Enhancement.Disambiguate | Enhancement.EventTypes,
            Negotiation = new NegotiationOptions(new Dictionary<string, string?>()),
        };
        transport.Input(Encoding.ASCII.GetBytes(
            "\u001b[?1016;1$y\u001b[?1006;1$y\u001b[?2004;1$y" +
            "\u001b[?1004;1$y\u001b[?2026;1$y\u001b[?3u\u001b[?1;2c"));
        transport.Close();
        await using var session = new Session(transport, resize, sink, options);

        // Act
        await session.RunAsync(TestContext.Current.CancellationToken);

        // Assert
        _ = sink.Profiles.ShouldHaveSingleItem();
        transport.JoinedWrites.ShouldBe(
            "\u001b[?u\u001b[c\u001b[?2026$p\u001b[?1004$p" +
            "\u001b[?2004$p\u001b[?1006$p\u001b[?1016$p" +
            "\u001b[?1004h\u001b[?2004h\u001b[?1000h\u001b[?1006h\u001b[>3u" +
            "\u001b[<u\u001b[?1006l\u001b[?1000l\u001b[?2004l\u001b[?1004l");
    }

    /// <summary>Verifies a query write failure remains primary and publishes nothing.</summary>
    [Fact]
    public async Task RunAsync_WhenNegotiationQueryWriteFails_PreservesExceptionAsync()
    {
        // Arrange
        await using var transport = new SessionTransport { FailWriteAt = 1 };
        await using var resize = new FakeResizeSource();
        var sink = new RuntimeSink();
        var options = RuntimeOptions.Minimal with
        {
            Negotiation = new NegotiationOptions(new Dictionary<string, string?>()),
        };
        await using var session = new Session(transport, resize, sink, options);

        // Act
        var thrown = await Should.ThrowAsync<IOException>(async () =>
            await session.RunAsync(TestContext.Current.CancellationToken));

        // Assert
        thrown.ShouldBeSameAs(transport.WriteFailure);
        sink.Faults.ShouldBe([transport.WriteFailure]);
        sink.Profiles.ShouldBeEmpty();
        transport.JoinedWrites.ShouldBeEmpty();
    }

    /// <summary>Verifies optional-mode failure restores the attempted lease.</summary>
    [Fact]
    public async Task RunAsync_WhenNegotiatedModeWriteFails_RestoresAndPreservesExceptionAsync()
    {
        // Arrange
        await using var transport = new SessionTransport { FailWriteAt = 2 };
        await using var resize = new FakeResizeSource();
        var sink = new RuntimeSink();
        var options = RuntimeOptions.Minimal with
        {
            Focus = true,
            Negotiation = new NegotiationOptions(
                new Dictionary<string, string?>(),
                new Settings { FocusReporting = true },
                Limits.Default with { QueryTimeout = TimeSpan.FromMilliseconds(20) }),
        };
        await using var session = new Session(transport, resize, sink, options);
        var running = session.RunAsync(TestContext.Current.CancellationToken).AsTask();
        await transport.FirstRead.Task.WaitAsync(TestContext.Current.CancellationToken);

        // Act
        var thrown = await Should.ThrowAsync<IOException>(running);

        // Assert
        thrown.ShouldBeSameAs(transport.WriteFailure);
        _ = sink.Profiles.ShouldHaveSingleItem();
        transport.JoinedWrites.ShouldEndWith("\u001b[?1004l");
    }

    /// <summary>Verifies cancellation during negotiation restores only acquired base leases.</summary>
    [Fact]
    public async Task RunAsync_WhenNegotiationIsCancelled_RestoresBaseModesWithoutProfileAsync()
    {
        // Arrange
        await using var transport = new SessionTransport();
        await using var resize = new FakeResizeSource();
        var sink = new RuntimeSink();
        var options = RuntimeOptions.Minimal with
        {
            AlternateScreen = true,
            HideCursor = true,
            Negotiation = new NegotiationOptions(new Dictionary<string, string?>()),
        };
        await using var session = new Session(transport, resize, sink, options);
        using var cancellation = new CancellationTokenSource();
        var running = session.RunAsync(cancellation.Token).AsTask();
        await transport.FirstRead.Task.WaitAsync(TestContext.Current.CancellationToken);

        // Act
        cancellation.Cancel();
        _ = await Should.ThrowAsync<OperationCanceledException>(running);

        // Assert
        sink.Profiles.ShouldBeEmpty();
        transport.JoinedWrites.ShouldStartWith(
            "\u001b[?1049h\u001b[?25l\u001b[?u\u001b[c");
        transport.JoinedWrites.ShouldEndWith("\u001b[?25h\u001b[?1049l");
    }

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
