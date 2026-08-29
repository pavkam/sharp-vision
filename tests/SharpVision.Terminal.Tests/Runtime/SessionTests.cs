// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Runtime;

using Capabilities;

using Kitty.Keyboard;

using SharpVision.Terminal.Backends;
using SharpVision.Terminal.Capabilities;
using SharpVision.Terminal.Input;
using SharpVision.Terminal.Multiplexing;


/// <summary>
/// Verifies terminal startup, ordered events, failure recovery, and reverse cleanup.
/// </summary>
public sealed class SessionTests
{
    /// <summary>Verifies negotiation refinement cannot replace the resolved terminal backend identity.</summary>
    [Fact]
    public async Task RunAsync_WhenNegotiationPublishesCapabilities_PreservesResolvedBackendAsync()
    {
        await using SessionTransport transport = new();
        await using FakeResizeSource resize = new();
        var sink = new RuntimeSink();
        var options = TerminalOptions.Minimal with
        {
            Negotiation = new NegotiationOptions(
                new Dictionary<string, string?> { ["TERM"] = "xterm-kitty" })
        };
        transport.Close();
        await using Session session = new(transport, resize, sink, options);
        var backend = session.Backend;

        await session.RunAsync(TestContext.Current.CancellationToken);

        backend.ShouldBeSameAs(KittyBackend.Instance);
        session.Backend.ShouldBeSameAs(backend);
        sink.Profiles.ShouldHaveSingleItem().KittyKeyboard.ShouldBe(
            new Feature(CapabilitySupport.Tentative, Origin.Environment));
        sink.Order.ShouldBe(["profile", "closed"]);
    }

    /// <summary>Verifies atomic route failure publishes immediately without transport output or deadline work.</summary>
    [Fact]
    public async Task RunAsync_WhenRouteCannotEncodeNegotiation_PublishesWithoutWritingAsync()
    {
        await using SessionTransport transport = new();
        await using FakeResizeSource resize = new();
        var sink = new RuntimeSink();
        var clock = new ManualTimeProvider();
        var supported = new Feature(CapabilitySupport.Supported, Origin.Override);
        var outerProfile = TerminalProfile.CreateAnsi(
            TerminalCapabilities.Conservative with
            {
                FocusReporting = supported,
                BracketedPaste = supported,
                CellMouse = supported,
                KittyKeyboard = supported
            });
        var policy = new MultiplexingPolicy(
            [MultiplexerKind.Tmux],
            outerProfile,
            PassthroughMode.All,
            paneVisible: true,
            MultiplexingOperation.CapabilityQueries,
            maxDepth: 4,
            maxEnvelopeBytes: 8);
        var limits = QueryLimits.Default with
        {
            MaxConcurrentQueries = 1,
            QueryTimeout = TimeSpan.FromMinutes(1)
        };
        var options = new TerminalOptions
        {
            AlternateScreen = false,
            HideCursor = false,
            Focus = true,
            Paste = true,
            Tracking = MouseTracking.Press,
            Keyboard = KittyKeyboardEnhancement.Disambiguate | KittyKeyboardEnhancement.EventTypes,
            Negotiation = new NegotiationOptions(
                new Dictionary<string, string?>(),
                overrides: null,
                limits: limits,
                multiplexing: policy)
        };
        await using Session session = new(transport, resize, sink, options, clock);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        var running = session.RunAsync(cancellation.Token).AsTask();

        await sink.ProfileReceived.Task.WaitAsync(
            TimeSpan.FromSeconds(2),
            TestContext.Current.CancellationToken);

        transport.JoinedWrites.ShouldBeEmpty();
        transport.FlushCount.ShouldBe(0);
        cancellation.Cancel();
        _ = await Should.ThrowAsync<OperationCanceledException>(() => running);
        transport.JoinedWrites.ShouldBeEmpty();
        transport.FlushCount.ShouldBe(0);
    }

    /// <summary>Verifies routed query and fragmented reply correlation use the explicit outer profile.</summary>
    [Fact]
    public async Task RunAsync_WhenOuterQueryReplyCrossesTmux_RoutesBeforeCorrelationAsync()
    {
        await using SessionTransport transport = new();
        await using FakeResizeSource resize = new();
        var sink = new RuntimeSink();
        var supported = new Feature(CapabilitySupport.Supported, Origin.Override);
        var outerProfile = TerminalProfile.CreateAnsi(
            TerminalCapabilities.Conservative with
            {
                KittyGraphics = supported,
                Sixel = supported
            });
        var policy = new MultiplexingPolicy(
            [MultiplexerKind.Tmux],
            outerProfile,
            PassthroughMode.All,
            paneVisible: true,
            MultiplexingOperation.CapabilityQueries);
        var limits = QueryLimits.Default with { MaxConcurrentQueries = 1 };
        var route = new MultiplexerRoute(policy);
        var wrapped = new ArrayBufferWriter<byte>();
        route.TryWriteCapabilityQueries(wrapped, "\u001b[?1;2c"u8).ShouldBeTrue();

        foreach (var value in wrapped.WrittenSpan)
        {
            transport.Input([value]);
        }

        transport.Close();
        var options = TerminalOptions.Minimal with
        {
            Negotiation = new NegotiationOptions(
                new Dictionary<string, string?> { ["TERM"] = "tmux-256color" },
                overrides: null,
                limits: limits,
                multiplexing: policy)
        };
        await using Session session = new(transport, resize, sink, options);

        await session.RunAsync(TestContext.Current.CancellationToken);

        transport.JoinedWrites.ShouldBe("\u001bPtmux;\u001b\u001b[c\u001b\\");
        sink.Responses.ShouldHaveSingleItem().Kind.ShouldBe(ResponseKind.PrimaryAttributes);
        sink.Profiles.ShouldHaveSingleItem().KittyGraphics.ShouldBe(supported);
        sink.Profiles.ShouldHaveSingleItem().Sixel.ShouldBe(supported);
        sink.Order.ShouldBe(["response", "profile", "closed"]);
    }

    /// <summary>Verifies a safe Screen-wrapped CSI reply unwraps before the originating query is retired.</summary>
    [Fact]
    public async Task RunAsync_WhenOuterQueryReplyCrossesScreen_RoutesBeforeCorrelationAsync()
    {
        await using SessionTransport transport = new();
        await using FakeResizeSource resize = new();
        var sink = new RuntimeSink();
        var supported = new Feature(CapabilitySupport.Supported, Origin.Override);
        var outerProfile = TerminalProfile.CreateAnsi(
            TerminalCapabilities.Conservative with { Sixel = supported });
        var policy = new MultiplexingPolicy(
            [MultiplexerKind.Screen],
            outerProfile,
            PassthroughMode.All,
            paneVisible: true,
            MultiplexingOperation.CapabilityQueries);
        var limits = QueryLimits.Default with { MaxConcurrentQueries = 1 };
        var wrapped = new ArrayBufferWriter<byte>();
        GnuScreenWriter.WritePassthrough(wrapped, "\u001b[?1;2c"u8);

        foreach (var value in wrapped.WrittenSpan)
        {
            transport.Input([value]);
        }

        transport.Close();
        var options = TerminalOptions.Minimal with
        {
            Negotiation = new NegotiationOptions(
                new Dictionary<string, string?> { ["TERM"] = "screen-256color" },
                overrides: null,
                limits: limits,
                multiplexing: policy)
        };
        await using Session session = new(transport, resize, sink, options);

        await session.RunAsync(TestContext.Current.CancellationToken);

        transport.JoinedWrites.ShouldBe("\u001bP\u001b[c\u001b\\");
        sink.Responses.ShouldHaveSingleItem().Kind.ShouldBe(ResponseKind.PrimaryAttributes);
        sink.Profiles.ShouldHaveSingleItem().Sixel.ShouldBe(supported);
        sink.Order.ShouldBe(["response", "profile", "closed"]);
    }

    /// <summary>Verifies both complete and compatibility profile setters reject null.</summary>
    [Fact]
    public void Options_WhenProfileOrCapabilitiesValueIsNull_Throws()
    {
        _ = Should.Throw<ArgumentNullException>(() => new TerminalOptions { Profile = null! });
        _ = Should.Throw<ArgumentNullException>(() => new TerminalOptions { Capabilities = null! });
    }

    /// <summary>Verifies Options compatibility preserves exact capabilities and initializer source order.</summary>
    [Fact]
    public void Options_WhenProfileAndCapabilitiesAreInitialized_LastInitializerWins()
    {
        var database = new Feature(CapabilitySupport.Supported, Origin.Database);
        var capabilities = TerminalCapabilities.Conservative with { Osc52 = database };
        var profile = TerminalProfile.CreateAnsi(
            TerminalCapabilities.Conservative with { ColorDepth = ColorDepth.Indexed256 });

        var capabilitiesLast = new TerminalOptions
        {
            Profile = profile,
            Capabilities = capabilities
        };
        var profileLast = new TerminalOptions
        {
            Capabilities = capabilities,
            Profile = profile
        };

        capabilitiesLast.Profile.Capabilities.ShouldBeSameAs(capabilities);
        capabilitiesLast.Capabilities.Osc52.ShouldBe(database);
        profileLast.Profile.ShouldBeSameAs(profile);
        profileLast.Capabilities.ShouldBeSameAs(profile.Capabilities);
    }

    /// <summary>Verifies every unsuitable profile is rejected during construction before terminal output.</summary>
    /// <param name="suitability">The unsupported full-screen classification.</param>
    [Theory]
    [InlineData(Suitability.Missing)]
    [InlineData(Suitability.Generic)]
    [InlineData(Suitability.Hardcopy)]
    [InlineData(Suitability.Incomplete)]
    [InlineData(Suitability.UnsupportedPadding)]
    public async Task Constructor_WhenProfileIsUnsuitable_ThrowsBeforeWritingAsync(
        Suitability suitability)
    {
        // Arrange
        await using SessionTransport transport = new();
        await using FakeResizeSource resize = new();
        var sink = new RuntimeSink();
        var profile = new TerminalProfile(
            new Description("fixture", DescriptionOrigin.Explicit, suitability),
            TerminalCapabilities.Conservative);

        // Act
        _ = Should.Throw<NotSupportedException>(() => new Session(
            transport,
            resize,
            sink,
            TerminalOptions.Minimal with { Profile = profile }));

        // Assert
        transport.JoinedWrites.ShouldBeEmpty();
    }

    /// <summary>Verifies the minimal options remain usable and byte-quiet.</summary>
    [Fact]
    public async Task RunAsync_WhenOptionsAreMinimal_WritesNoBytesAsync()
    {
        // Arrange
        await using SessionTransport transport = new();
        await using FakeResizeSource resize = new();
        var sink = new RuntimeSink();
        transport.Close();
        await using Session session = new(transport, resize, sink, TerminalOptions.Minimal);

        // Act
        await session.RunAsync(TestContext.Current.CancellationToken);

        // Assert
        TerminalOptions.Minimal.Profile.Description.Suitability.ShouldBe(Suitability.Usable);
        TerminalOptions.Minimal.ModifyOtherKeys.ShouldBeNull();
        transport.JoinedWrites.ShouldBeEmpty();
    }

    /// <summary>Verifies an explicitly requested and authoritatively supported Kitty paste-event
    /// mode is leased for the session and restored during reverse cleanup.</summary>
    [Fact]
    public async Task RunAsync_WhenClipboardPasteEventsAreEnabled_LeasesAndRestoresMode5522Async()
    {
        // Arrange
        await using SessionTransport transport = new();
        await using FakeResizeSource resize = new();
        var sink = new RuntimeSink();
        var supported = new Feature(CapabilitySupport.Supported, Origin.Override);
        var options = TerminalOptions.Minimal with
        {
            Capabilities = TerminalCapabilities.Conservative with { KittyClipboard = supported },
            ClipboardPasteEvents = true
        };
        transport.Close();
        await using Session session = new(transport, resize, sink, options);

        // Act
        await session.RunAsync(TestContext.Current.CancellationToken);

        // Assert
        transport.JoinedWrites.ShouldBe("\u001b[?5522h\u001b[?5522l");
    }

    /// <summary>Verifies the Kitty paste-event lease reaches the explicit outer terminal through
    /// the approved tmux clipboard family in both acquisition and reverse cleanup.</summary>
    [Fact]
    public async Task RunAsync_WhenClipboardPasteEventsUseTmux_RoutesLeaseAndRestorationAsync()
    {
        // Arrange
        await using SessionTransport transport = new();
        await using FakeResizeSource resize = new();
        var sink = new RuntimeSink();
        var supported = new Feature(CapabilitySupport.Supported, Origin.Override);
        var capabilities = TerminalCapabilities.Conservative with { KittyClipboard = supported };
        var policy = new MultiplexingPolicy(
            [MultiplexerKind.Tmux],
            TerminalProfile.CreateAnsi(capabilities),
            PassthroughMode.All,
            paneVisible: true,
            MultiplexingOperation.Clipboard);
        var options = TerminalOptions.Minimal with
        {
            Capabilities = capabilities,
            ClipboardPasteEvents = true,
            Multiplexing = policy
        };
        transport.Close();
        await using Session session = new(transport, resize, sink, options);

        // Act
        await session.RunAsync(TestContext.Current.CancellationToken);

        // Assert
        transport.JoinedWrites.ShouldBe(
            "\u001bPtmux;\u001b\u001b[?5522h\u001b\\" +
            "\u001bPtmux;\u001b\u001b[?5522l\u001b\\");
    }

    /// <summary>Verifies the resolved profile key map reaches the real session protocol router.</summary>
    [Fact]
    public async Task RunAsync_WhenProfileDescribesKey_RoutesDescribedMeaningAsync()
    {
        // Arrange
        await using SessionTransport transport = new();
        await using FakeResizeSource resize = new();
        var sink = new RuntimeSink();
        var ansi = TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative);
        var profile = new TerminalProfile(
            new Description("fixture", DescriptionOrigin.Database, Suitability.Usable),
            TerminalCapabilities.Conservative,
            ansi.Programs,
            new KeyMap([new KeyBinding("\u001b[99~"u8, Code.F63)]));
        var options = TerminalOptions.Minimal with { Profile = profile };
        transport.Input("\u001b[99~"u8.ToArray());
        transport.Close();
        await using Session session = new(transport, resize, sink, options);

        // Act
        await session.RunAsync(TestContext.Current.CancellationToken);

        // Assert
        sink.Strokes.ShouldHaveSingleItem().Code.ShouldBe(Code.F63);
    }

    /// <summary>Verifies a lone Escape is delivered at its ambiguity deadline without any
    /// further input. The decoder held the pending Escape and its deadline, but nothing in the
    /// read loop ever woke to expire it - the Escape only surfaced when the NEXT byte arrived,
    /// so every Escape-to-dismiss interaction needed a second keypress in a real terminal.</summary>
    [Fact]
    public async Task RunAsync_WhenLoneEscapeAges_DeliversEscapeWithoutFurtherInputAsync()
    {
        // Arrange
        await using SessionTransport transport = new();
        await using FakeResizeSource resize = new();
        var sink = new RuntimeSink();
        var clock = new ManualTimeProvider();
        await using Session session = new(transport, resize, sink, TerminalOptions.Minimal, clock);
        var running = session.RunAsync(TestContext.Current.CancellationToken).AsTask();
        await transport.FirstRead.Task.WaitAsync(
            TimeSpan.FromSeconds(2),
            TestContext.Current.CancellationToken);

        // Act: one lone Escape byte, then only the clock moves. The wake-up is armed when the
        // byte is routed; advancing past the ambiguity window afterwards fires it. Retry the
        // advance until the stroke lands so the test cannot race the arming read.
        transport.Input([0x1b]);

        while (!sink.StrokeReceived.Task.IsCompleted)
        {
            clock.Advance(InputOptions.Default.EscapeTimeout);
            _ = await Task.WhenAny(sink.StrokeReceived.Task, Task.Delay(50, TestContext.Current.CancellationToken));
        }

        await sink.StrokeReceived.Task.WaitAsync(
            TimeSpan.FromSeconds(2),
            TestContext.Current.CancellationToken);

        // Assert: the Escape arrived with no second byte, then the session still shuts down
        // cleanly.
        sink.Strokes.ShouldHaveSingleItem().Code.ShouldBe(Code.Escape);
        transport.Close();
        await running.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies negotiation starts from the resolved profile rather than unrelated conservative defaults.</summary>
    [Fact]
    public async Task RunAsync_WhenNegotiating_PreservesResolvedProfileCapabilitiesAsync()
    {
        // Arrange
        await using SessionTransport transport = new();
        await using FakeResizeSource resize = new();
        var sink = new RuntimeSink();
        var supported = new Feature(CapabilitySupport.Supported, Origin.Override);
        var profile = TerminalProfile.CreateAnsi(
            TerminalCapabilities.Conservative with { Sixel = supported });
        var options = TerminalOptions.Minimal with
        {
            Profile = profile,
            Negotiation = new NegotiationOptions(new Dictionary<string, string?>())
        };
        transport.Close();
        await using Session session = new(transport, resize, sink, options);

        // Act
        await session.RunAsync(TestContext.Current.CancellationToken);

        // Assert
        sink.Profiles.ShouldHaveSingleItem().Sixel.ShouldBe(supported);
    }

    /// <summary>Verifies early EOF publishes fallback without enabling optional modes.</summary>
    [Fact]
    public async Task RunAsync_WhenNegotiationEndsAtEof_PublishesWithoutOptionalModesAsync()
    {
        // Arrange
        await using SessionTransport transport = new();
        await using FakeResizeSource resize = new();
        var sink = new RuntimeSink();
        var options = TerminalOptions.Minimal with
        {
            Focus = true,
            Negotiation = new NegotiationOptions(
                new Dictionary<string, string?> { ["TERM"] = "xterm-kitty" })
        };
        transport.Close();
        await using Session session = new(transport, resize, sink, options);

        // Act
        await session.RunAsync(TestContext.Current.CancellationToken);

        // Assert
        sink.Profiles.ShouldHaveSingleItem().KittyKeyboard.ShouldBe(
            new Feature(CapabilitySupport.Tentative, Origin.Environment));
        sink.Order.ShouldBe(["profile", "closed"]);
        transport.JoinedWrites.ShouldBe(
            "\u001b[?u\u001b_Gi=31,s=1,v=1,a=q,t=d,f=24;AAAA\u001b\\" +
            "\u001b[c\u001b[>c\u001b[?2026$p\u001b[?1004$p" +
            "\u001b[?2004$p\u001b[?1006$p\u001b[?1016$p\u001b[?5522$p" +
            "\u001b[14t\u001b[16t\u001b[18t" +
            "\u001b]4;0;?\u001b\\\u001b]10;?\u001b\\\u001b]11;?\u001b\\" +
            "\u001b]1337;Capabilities\u001b\\" +
            // The terminating fence: a trailing CSI 6n.
            "\u001b[6n");
    }

    /// <summary>Verifies missing replies release startup at one finite deadline.</summary>
    [Fact]
    public async Task RunAsync_WhenNegotiationTimesOut_PublishesAndReleasesResizeAsync()
    {
        // Arrange
        await using SessionTransport transport = new();
        await using FakeResizeSource resize = new();
        var sink = new RuntimeSink();
        var clock = new ManualTimeProvider();
        var limits = QueryLimits.Default with { QueryTimeout = TimeSpan.FromSeconds(1) };
        var options = TerminalOptions.Minimal with
        {
            Negotiation = new NegotiationOptions(
                new Dictionary<string, string?> { ["TERM"] = "xterm-kitty" },
                limits: limits)
        };
        await using Session session = new(
            transport,
            resize,
            sink,
            options,
            clock);
        var running = session.RunAsync(TestContext.Current.CancellationToken).AsTask();
        await transport.FirstRead.Task.WaitAsync(TestContext.Current.CancellationToken);
        var dimensions = new Dimensions(new Size(80, 24));

        // Act
        resize.Resize(dimensions);
        clock.Advance(limits.QueryTimeout);
        await sink.ProfileReceived.Task.WaitAsync(
            TimeSpan.FromSeconds(2),
            TestContext.Current.CancellationToken);

        // Both barriers are required before the transport closes. The profile barrier proves the
        // deadline expired; the resize barrier proves the queued resize was actually delivered.
        // Without the second one this races: RunAsync only re-checks the outstanding resize read
        // against EOF when that read has already completed, which is a deliberate bound
        // rather than an oversight - a read still in flight cannot be drained without either
        // discarding its value or waiting indefinitely at shutdown. Publishing to the source only
        // queues the value; whether the read observing it completes before the closing read wins
        // the loop is thread-pool scheduling, so closing here without waiting dropped the resize
        // in roughly one run in thirty.
        await sink.ResizeReceived.Task.WaitAsync(
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
        await using SessionTransport transport = new();
        await using FakeResizeSource resize = new();
        var sink = new RuntimeSink();
        var options = TerminalOptions.Minimal with
        {
            Negotiation = new NegotiationOptions(
                new Dictionary<string, string?>(),
                limits: QueryLimits.Default with { MaxConcurrentQueries = 1 })
        };
        transport.Input("\u001b[?1;2c"u8.ToArray());
        transport.Close();
        await using Session session = new(transport, resize, sink, options);

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
        await using SessionTransport transport = new();
        await using FakeResizeSource resize = new() { SignalAfterReads = 3 };
        var sink = new RuntimeSink();
        var clock = new ManualTimeProvider();
        var limits = QueryLimits.Default with { QueryTimeout = TimeSpan.FromSeconds(1) };
        var options = TerminalOptions.Minimal with
        {
            Negotiation = new NegotiationOptions(
                new Dictionary<string, string?>(),
                limits: limits)
        };
        var first = new Dimensions(new Size(80, 24));
        var second = new Dimensions(new Size(100, 30));
        var newest = new Dimensions(new Size(120, 40));
        resize.Resize(first);
        resize.Resize(second);
        resize.Resize(newest);
        await using Session session = new(transport, resize, sink, options, clock);
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
        await using SessionTransport transport = new();
        await using FakeResizeSource resize = new();
        var sink = new RuntimeSink();
        var options = TerminalOptions.Minimal with
        {
            Negotiation = new NegotiationOptions(
                new Dictionary<string, string?>(),
                limits: QueryLimits.Default with { MaxConcurrentQueries = 8 })
        };
        await using Session session = new(transport, resize, sink, options);
        var running = session.RunAsync(TestContext.Current.CancellationToken).AsTask();
        await transport.FirstRead.Task.WaitAsync(TestContext.Current.CancellationToken);
        var dimensions = new Dimensions(new Size(80, 24));

        // Act
        transport.Input(Encoding.ASCII.GetBytes(
            "x" +
            "\u001b[?1016;1$y\u001b[?1006;1$y\u001b[?2004;1$y" +
            "\u001b[?1004;1$y\u001b[?2026;1$y\u001b[?3u" +
            "\u001b[>41;410;0c\u001b[?1;2c"));
        resize.Resize(dimensions);
        await sink.ResizeReceived.Task.WaitAsync(TestContext.Current.CancellationToken);
        transport.Close();
        await running;

        // Assert
        sink.Profiles.ShouldHaveSingleItem().SynchronizedOutput.Supported.ShouldBeTrue();
        sink.Order.IndexOf("text").ShouldBeLessThan(sink.Order.IndexOf("profile"));
        sink.Order.IndexOf("profile").ShouldBeLessThan(sink.Order.IndexOf("resize"));
        sink.Resizes.ShouldBe([dimensions]);
        transport.JoinedWrites.ShouldStartWith(
            "\u001b[?u\u001b[c\u001b[>c\u001b[?2026$p\u001b[?1004$p" +
            "\u001b[?2004$p\u001b[?1006$p\u001b[?1016$p");
    }

    /// <summary>Verifies negotiated modes enable after publication and unwind in reverse.</summary>
    [Fact]
    public async Task RunAsync_WhenNegotiationProvesModes_EnablesAndRestoresExactlyAsync()
    {
        // Arrange
        await using SessionTransport transport = new();
        await using FakeResizeSource resize = new();
        var sink = new RuntimeSink();
        var options = TerminalOptions.Minimal with
        {
            Focus = true,
            Paste = true,
            Tracking = MouseTracking.Press,
            Keyboard = KittyKeyboardEnhancement.Disambiguate | KittyKeyboardEnhancement.EventTypes,
            Negotiation = new NegotiationOptions(
                new Dictionary<string, string?>(),
                limits: QueryLimits.Default with { MaxConcurrentQueries = 8 })
        };
        transport.Input(Encoding.ASCII.GetBytes(
            "\u001b[?1016;1$y\u001b[?1006;1$y\u001b[?2004;1$y" +
            "\u001b[?1004;1$y\u001b[?2026;1$y\u001b[?3u" +
            "\u001b[>41;410;0c\u001b[?1;2c"));
        transport.Close();
        await using Session session = new(transport, resize, sink, options);

        // Act
        await session.RunAsync(TestContext.Current.CancellationToken);

        // Assert
        _ = sink.Profiles.ShouldHaveSingleItem();
        transport.JoinedWrites.ShouldBe(
            "\u001b[?u\u001b[c\u001b[>c\u001b[?2026$p\u001b[?1004$p" +
            "\u001b[?2004$p\u001b[?1006$p\u001b[?1016$p" +
            "\u001b[?1004h\u001b[?2004h\u001b[?1006h\u001b[?1000h\u001b[>3u" +
            "\u001b[<u\u001b[?1000l\u001b[?1006l\u001b[?2004l\u001b[?1004l");
    }

    /// <summary>Verifies synchronous host geometry suppresses lower-confidence geometry probes.</summary>
    [Fact]
    public async Task RunAsync_WhenHostProvidesCellAndPixelMetrics_DoesNotQueryGeometryAsync()
    {
        // Arrange
        await using SessionTransport transport = new();
        var dimensions = new Dimensions(
            new Size(120, 40),
            new Size(1200, 800));
        await using FakeResizeSource resize = new() { Current = dimensions };
        var sink = new RuntimeSink();
        var options = TerminalOptions.Minimal with
        {
            Negotiation = new NegotiationOptions(new Dictionary<string, string?>())
        };
        transport.Close();
        await using Session session = new(transport, resize, sink, options);

        // Act
        await session.RunAsync(TestContext.Current.CancellationToken);

        // Assert
        transport.JoinedWrites.ShouldNotContain("\u001b[14t");
        transport.JoinedWrites.ShouldNotContain("\u001b[16t");
        transport.JoinedWrites.ShouldNotContain("\u001b[18t");
    }

    /// <summary>Verifies a resize source whose ReadAsync reports only genuine changes - never an
    /// initial observation - still delivers its synchronous TryReadCurrent snapshot to the sink,
    /// so Application's readiness gate, which is driven exclusively by ISink.Resize, is not left
    /// waiting forever for a change that may never come.</summary>
    [Fact]
    public async Task RunAsync_WhenResizeSourceOnlyReportsCurrentDimensions_StillDeliversResizeAsync()
    {
        // Arrange
        await using SessionTransport transport = new();
        var dimensions = new Dimensions(new Size(80, 24), new Size(800, 480));
        await using FakeResizeSource resize = new() { Current = dimensions };
        var sink = new RuntimeSink();
        await using Session session = new(transport, resize, sink, TerminalOptions.Minimal);
        var running = session.RunAsync(TestContext.Current.CancellationToken).AsTask();

        // Act
        await sink.ResizeReceived.Task.WaitAsync(TestContext.Current.CancellationToken);
        transport.Close();
        await running;

        // Assert
        sink.Resizes.ShouldBe([dimensions]);
    }

    /// <summary>Verifies the same change-only source still delivers its snapshot once capability
    /// negotiation completes, through the pending-resize gate rather than only the earlier
    /// unconditional path.</summary>
    [Fact]
    public async Task RunAsync_WhenResizeSourceOnlyReportsCurrentDimensionsDuringNegotiation_DeliversResizeAfterProfileAsync()
    {
        // Arrange
        await using SessionTransport transport = new();
        var dimensions = new Dimensions(new Size(80, 24), new Size(800, 480));
        await using FakeResizeSource resize = new() { Current = dimensions };
        var sink = new RuntimeSink();
        var options = TerminalOptions.Minimal with
        {
            Negotiation = new NegotiationOptions(new Dictionary<string, string?>())
        };
        transport.Close();
        await using Session session = new(transport, resize, sink, options);

        // Act
        await session.RunAsync(TestContext.Current.CancellationToken);

        // Assert
        sink.Resizes.ShouldBe([dimensions]);
        sink.Order.IndexOf("profile").ShouldBeLessThan(sink.Order.IndexOf("resize"));
    }

    /// <summary>Verifies a query write failure remains primary and publishes nothing.</summary>
    [Fact]
    public async Task RunAsync_WhenNegotiationQueryWriteFails_PreservesExceptionAsync()
    {
        // Arrange
        await using SessionTransport transport = new() { FailWriteAt = 1 };
        await using FakeResizeSource resize = new();
        var sink = new RuntimeSink();
        var options = TerminalOptions.Minimal with
        {
            Negotiation = new NegotiationOptions(new Dictionary<string, string?>())
        };
        await using Session session = new(transport, resize, sink, options);

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
    public async Task RunAsync_WhenNegotiatedModeDeadlineFiresEarlyAndWriteFails_RestoresAndPreservesExceptionAsync()
    {
        // Arrange
        await using SessionTransport transport = new() { FailWriteAt = 2 };
        await using FakeResizeSource resize = new();
        var sink = new RuntimeSink();
        var clock = new ManualTimeProvider();
        var limits = QueryLimits.Default with { QueryTimeout = TimeSpan.FromSeconds(1) };
        var options = TerminalOptions.Minimal with
        {
            Focus = true,
            Negotiation = new NegotiationOptions(
                new Dictionary<string, string?>(),
                new CapabilityOverrides { FocusReporting = true },
                limits)
        };
        await using Session session = new(transport, resize, sink, options, clock);
        var running = session.RunAsync(TestContext.Current.CancellationToken).AsTask();
        await transport.FirstRead.Task.WaitAsync(TestContext.Current.CancellationToken);
        var createdTimerCount = clock.CreatedTimerCount;

        // Act
        clock.FireTimersEarly();

        for (var attempt = 0;
             attempt < 10_000 && !running.IsCompleted && clock.CreatedTimerCount == createdTimerCount;
             attempt++)
        {
            await Task.Yield();
        }

        (running.IsCompleted || clock.CreatedTimerCount > createdTimerCount)
            .ShouldBeTrue("The early deadline callback was not observed.");
        clock.Advance(limits.QueryTimeout);
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
        await using SessionTransport transport = new();
        await using FakeResizeSource resize = new();
        var sink = new RuntimeSink();
        var options = TerminalOptions.Minimal with
        {
            AlternateScreen = true,
            HideCursor = true,
            Negotiation = new NegotiationOptions(new Dictionary<string, string?>())
        };
        await using Session session = new(transport, resize, sink, options);
        using var cancellation = new CancellationTokenSource();
        var running = session.RunAsync(cancellation.Token).AsTask();
        await transport.FirstRead.Task.WaitAsync(TestContext.Current.CancellationToken);

        // Act
        cancellation.Cancel();
        _ = await Should.ThrowAsync<OperationCanceledException>(running);

        // Assert
        sink.Profiles.ShouldBeEmpty();
        transport.JoinedWrites.ShouldStartWith(
            "\u001b[?1049h\u001b[?25l\u001b[?u" +
            "\u001b_Gi=31,s=1,v=1,a=q,t=d,f=24;AAAA\u001b\\\u001b[c");
        transport.JoinedWrites.ShouldEndWith("\u001b[?25h\u001b[?1049l");
    }

    /// <summary>Verifies replies and adjacent text retain transport order.</summary>
    [Fact]
    public async Task RunAsync_WhenReplyPrecedesText_RoutesBothInOrderAsync()
    {
        // Arrange
        await using SessionTransport transport = new();
        await using FakeResizeSource resize = new();
        var sink = new RuntimeSink();
        transport.Input("\u001b[?1;2cx"u8.ToArray());
        transport.Close();
        await using Session session = new(
            transport,
            resize,
            sink,
            TerminalOptions.Minimal);

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
        await using SessionTransport transport = new();
        await using FakeResizeSource resize = new();
        var sink = new RuntimeSink();
        transport.Input("x"u8.ToArray());
        transport.Close();
        var capabilities = Supported();
        await using Session session = new(
            transport,
            resize,
            sink,
            new TerminalOptions { Capabilities = capabilities });

        await session.RunAsync(TestContext.Current.CancellationToken);

        sink.Text.Single().Value.ShouldBe(new Rune('x'));
        sink.ClosedCount.ShouldBe(1);
        transport.JoinedWrites.ShouldBe(
            "\u001b[?1049h\u001b[?25l\u001b[?1004h\u001b[?2004h" +
            "\u001b[?1006h\u001b[?1000h\u001b[>3u" +
            "\u001b[<u\u001b[?1000l\u001b[?1006l\u001b[?2004l" +
            "\u001b[?1004l\u001b[?25h\u001b[?1049l");
    }

    /// <summary>
    /// Verifies resize is delivered with pixels before later closure.
    /// </summary>
    [Fact]
    public async Task RunAsync_WhenResizeArrives_ForwardsDimensionsAsync()
    {
        await using SessionTransport transport = new();
        await using FakeResizeSource resize = new();
        var sink = new RuntimeSink();
        await using Session session = new(
            transport,
            resize,
            sink,
            TerminalOptions.Minimal);
        var running = session.RunAsync(TestContext.Current.CancellationToken).AsTask();
        var expected = new Dimensions(
            new Size(120, 40),
            new Size(1200, 800));

        resize.Resize(expected);
        await sink.ResizeReceived.Task.WaitAsync(TestContext.Current.CancellationToken);
        transport.Close();
        await running;

        sink.Resizes.ShouldBe([expected]);
    }

    /// <summary>
    /// Verifies a resize that is already ready is forwarded promptly even while a transport keeps
    /// completing reads synchronously — a fixed-order Task.WhenAny that always resolves to the
    /// first already-completed task previously let a synchronous read burst monopolize the loop
    /// and starve resize indefinitely.
    /// </summary>
    [Fact]
    public async Task RunAsync_WhenResizeIsReadyDuringASynchronousReadBurst_ForwardsResizePromptlyAsync()
    {
        await using SessionTransport transport = new();
        await using FakeResizeSource resize = new();
        var readCountAtResize = -1;
        var sink = new RuntimeSink { OnResize = () => readCountAtResize = transport.ReadCount };

        // Every read completes synchronously once queued input is already buffered in the
        // channel; queuing far more than any reasonable fairness bound before the resize is
        // observed reproduces the reported burst.
        for (var index = 0; index < 500; index++)
        {
            transport.Input("a"u8.ToArray());
        }

        var expected = new Dimensions(new Size(120, 40), new Size(1200, 800));
        resize.Resize(expected);

        await using Session session = new(transport, resize, sink, TerminalOptions.Minimal);
        var running = session.RunAsync(TestContext.Current.CancellationToken).AsTask();

        await sink.ResizeReceived.Task.WaitAsync(
            TimeSpan.FromSeconds(10),
            TestContext.Current.CancellationToken);

        // Bounded alternation guarantees resize is serviced within a handful of iterations, not
        // after draining the whole 500-item burst. Captured synchronously inside the Resize
        // callback itself, since ResizeReceived's continuation runs asynchronously and the loop
        // keeps consuming the burst concurrently while that continuation is merely scheduled.
        readCountAtResize.ShouldBeLessThan(20);

        transport.Close();
        await running;

        sink.Resizes.ShouldBe([expected]);
    }

    /// <summary>
    /// Verifies a resize that becomes ready in the same tick as a closing (EOF) read is still
    /// forwarded rather than silently dropped by the read's early-return closure path.
    /// </summary>
    [Fact]
    public async Task RunAsync_WhenResizeIsReadyAtTheSameTickAsEof_StillForwardsResizeAsync()
    {
        await using SessionTransport transport = new();
        await using FakeResizeSource resize = new();
        var sink = new RuntimeSink();
        var expected = new Dimensions(new Size(120, 40), new Size(1200, 800));

        // No input is ever queued, so the very first read resolves directly to EOF; the resize
        // is queued before the loop starts so it races that EOF read on the very first iteration.
        transport.Close();
        resize.Resize(expected);

        await using Session session = new(transport, resize, sink, TerminalOptions.Minimal);

        await session.RunAsync(TestContext.Current.CancellationToken);

        sink.Resizes.ShouldBe([expected]);
        sink.ClosedCount.ShouldBe(1);
    }

    /// <summary>
    /// Verifies a resize that becomes ready in the same tick as a closing (EOF) read is still
    /// forwarded even while startup negotiation is still pending — the identical setup as the
    /// sibling test above, just with negotiation still outstanding when the tie occurs.
    /// </summary>
    [Fact]
    public async Task RunAsync_WhenResizeIsReadyAtTheSameTickAsEofDuringNegotiation_StillForwardsResizeAsync()
    {
        await using SessionTransport transport = new();
        await using FakeResizeSource resize = new();
        var sink = new RuntimeSink();
        var options = TerminalOptions.Minimal with
        {
            Negotiation = new NegotiationOptions(
                new Dictionary<string, string?>(),
                limits: QueryLimits.Default with { MaxConcurrentQueries = 8 })
        };
        var expected = new Dimensions(new Size(120, 40), new Size(1200, 800));

        // No negotiation replies are ever queued and the transport is closed before RunAsync
        // even starts, so the very first read resolves directly to EOF while negotiation is
        // still pending; the resize is queued beforehand so it races that EOF read on the very
        // first iteration.
        transport.Close();
        resize.Resize(expected);

        await using Session session = new(transport, resize, sink, options);

        await session.RunAsync(TestContext.Current.CancellationToken);

        sink.Resizes.ShouldBe([expected]);
        sink.ClosedCount.ShouldBe(1);
    }

    /// <summary>
    /// Verifies partial startup failure restores attempted modes and preserves identity.
    /// </summary>
    [Fact]
    public async Task RunAsync_WhenStartupWriteFails_RestoresAndPreservesExceptionAsync()
    {
        await using SessionTransport transport = new() { FailWriteAt = 3 };
        await using FakeResizeSource resize = new();
        var sink = new RuntimeSink();
        var failure = transport.WriteFailure;
        await using Session session = new(
            transport,
            resize,
            sink,
            new TerminalOptions { Capabilities = Supported() });

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
        await using SessionTransport transport = new();
        await using FakeResizeSource resize = new();
        var sink = new RuntimeSink();
        await using Session session = new(
            transport,
            resize,
            sink,
            new TerminalOptions { Capabilities = Supported() });
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
        await using SessionTransport transport = new()
        {
            ReadFailure = new IOException("read failed"),
            FailWriteAt = 7
        };
        await using FakeResizeSource resize = new();
        var sink = new RuntimeSink();
        await using Session session = new(
            transport,
            resize,
            sink,
            new TerminalOptions { Capabilities = Supported() });

        var thrown = await Should.ThrowAsync<IOException>(async () =>
            await session.RunAsync(TestContext.Current.CancellationToken));

        thrown.ShouldBeSameAs(transport.ReadFailure);
        session.LastCleanupException.ShouldBeSameAs(transport.WriteFailure);
        sink.Faults.ShouldBe([transport.ReadFailure]);
    }

    /// <summary>
    /// Verifies a fault notification failure is combined with the original exception rather than
    /// written into <see cref="Session.LastCleanupException"/>, so a genuine lease-restoration
    /// failure that happens alongside it stays observable there instead of being discarded.
    /// </summary>
    [Fact]
    public async Task RunAsync_WhenFaultNotificationAndCleanupFail_PreservesBothExceptionsAsync()
    {
        var notificationFailure = new InvalidOperationException("notification failed");
        await using SessionTransport transport = new()
        {
            ReadFailure = new IOException("read failed"),
            FailWriteAt = 7
        };
        await using FakeResizeSource resize = new();
        var sink = new RuntimeSink { FaultFailure = notificationFailure };
        await using Session session = new(
            transport,
            resize,
            sink,
            new TerminalOptions { Capabilities = Supported() });

        var thrown = await Should.ThrowAsync<AggregateException>(async () =>
            await session.RunAsync(TestContext.Current.CancellationToken));

        thrown.InnerExceptions.ShouldBe([transport.ReadFailure, notificationFailure]);
        session.LastCleanupException.ShouldBeSameAs(transport.WriteFailure);
        sink.Faults.ShouldBe([transport.ReadFailure]);
    }

    /// <summary>
    /// Verifies an input handler failure triggers cleanup and remains the primary error.
    /// </summary>
    [Fact]
    public async Task RunAsync_WhenInputHandlerFails_PreservesHandlerExceptionAsync()
    {
        await using SessionTransport transport = new();
        await using FakeResizeSource resize = new();
        var sink = new RuntimeSink { TextFailure = new InvalidOperationException("handler") };
        transport.Input("x"u8.ToArray());
        await using Session session = new(
            transport,
            resize,
            sink,
            new TerminalOptions { Capabilities = Supported() });

        var thrown = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await session.RunAsync(TestContext.Current.CancellationToken));

        thrown.ShouldBeSameAs(sink.TextFailure);
        transport.JoinedWrites.ShouldEndWith("\u001b[?25h\u001b[?1049l");
    }

    /// <summary>
    /// Verifies DisposeAsync rejects being awaited from inside an ISink callback raised by the
    /// run it would need to wait for, instead of deadlocking. The callback itself is synchronous,
    /// so the guard must reject before the first await point: only then is the returned ValueTask
    /// already faulted by the time GetAwaiter().GetResult() observes it, rather than genuinely
    /// blocking the thread the run needs to finish on.
    /// </summary>
    [Fact]
    public async Task DisposeAsync_WhenAwaitedFromOwnClosedCallback_ThrowsInsteadOfDeadlockingAsync()
    {
        await using SessionTransport transport = new();
        await using FakeResizeSource resize = new();
        Session? session = null;
        InvalidOperationException? thrown = null;
        var sink = new RuntimeSink
        {
            OnClosed = () =>
                thrown = Should.Throw<InvalidOperationException>(
                    () => session!.DisposeAsync().AsTask().GetAwaiter().GetResult())
        };
        transport.Close();
        session = new Session(transport, resize, sink, TerminalOptions.Minimal);

        await session.RunAsync(TestContext.Current.CancellationToken);

        sink.ClosedCount.ShouldBe(1);
        var exception = thrown.ShouldNotBeNull();
        exception.Message.ShouldContain("own run");
    }

    /// <summary>
    /// Verifies the reentrancy guard is scoped to the session that is actually running.
    ///
    /// <para>The flag has to be static to be observed across the run's await chain, but it used to
    /// be a <c>bool</c> - so it recorded only that <em>a</em> session was running, and every
    /// <c>Session</c> in the process saw every other one's run. A sink callback raised by session A
    /// could not dispose an unrelated session B: the call was rejected with a message that is
    /// factually untrue of B, which has no run at all, and B's transport, resize source, and
    /// lifetime were all left undisposed.</para>
    ///
    /// <para>The documented contract is explicit that the restriction covers a callback raised by
    /// the session's <em>own</em> run, so rejecting B was outside what it sanctions.</para>
    /// </summary>
    [Fact]
    public async Task DisposeAsync_WhenAwaitedForAnUnrelatedSessionFromACallback_DisposesItAsync()
    {
        await using SessionTransport runningTransport = new();
        await using FakeResizeSource runningResize = new();
        SessionTransport unrelatedTransport = new();
        FakeResizeSource unrelatedResize = new();
        var unrelated = new Session(
            unrelatedTransport,
            unrelatedResize,
            new RuntimeSink(),
            TerminalOptions.Minimal);
        Exception? thrown = null;
        var sink = new RuntimeSink
        {
            OnClosed = () =>
            {
                try
                {
                    unrelated.DisposeAsync().AsTask().GetAwaiter().GetResult();
                }
                catch (Exception exception)
                {
                    thrown = exception;
                }
            }
        };
        runningTransport.Close();
        await using Session running = new(runningTransport, runningResize, sink, TerminalOptions.Minimal);

        await running.RunAsync(TestContext.Current.CancellationToken);

        thrown.ShouldBeNull("an unrelated session's disposal is not reentrant on this run");

        // The leak the rejection caused, observed directly: a refused DisposeAsync returns before
        // it releases anything the session owns.
        unrelatedTransport.DisposeCount.ShouldBeGreaterThan(0);
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
        var session = new Session(transport, resize, sink, TerminalOptions.Minimal);
        await session.RunAsync(TestContext.Current.CancellationToken);

        await session.DisposeAsync();
        await session.DisposeAsync();
    }

    /// <summary>Verifies authorized xterm enhancement is leased and restored when Kitty is absent.</summary>
    [Fact]
    public async Task RunAsync_WhenOnlyXtermKeyboardIsSupported_LeasesAndRestoresModifyOtherKeysAsync()
    {
        await using SessionTransport transport = new();
        await using FakeResizeSource resize = new();
        var sink = new RuntimeSink();
        var supported = new Feature(CapabilitySupport.Supported, Origin.Override);
        var capabilities = TerminalCapabilities.Conservative with { XtermKeyboard = supported };
        var options = TerminalOptions.Minimal with
        {
            Capabilities = capabilities,
            Keyboard = KittyKeyboardEnhancement.Disambiguate,
            ModifyOtherKeys = 2
        };
        transport.Close();
        await using Session session = new(transport, resize, sink, options);

        await session.RunAsync(TestContext.Current.CancellationToken);

        transport.JoinedWrites.ShouldBe("\u001b[>4;2m\u001b[>4m");
    }

    /// <summary>Verifies proven Kitty keyboard remains preferred over xterm enhancement.</summary>
    [Fact]
    public async Task RunAsync_WhenKittyAndXtermAreSupported_PrefersKittyLeaseAsync()
    {
        await using SessionTransport transport = new();
        await using FakeResizeSource resize = new();
        var sink = new RuntimeSink();
        var supported = new Feature(CapabilitySupport.Supported, Origin.Override);
        var capabilities = TerminalCapabilities.Conservative with
        {
            KittyKeyboard = supported,
            XtermKeyboard = supported
        };
        var options = TerminalOptions.Minimal with
        {
            Capabilities = capabilities,
            Keyboard = KittyKeyboardEnhancement.Disambiguate,
            ModifyOtherKeys = 2
        };
        transport.Close();
        await using Session session = new(transport, resize, sink, options);

        await session.RunAsync(TestContext.Current.CancellationToken);

        transport.JoinedWrites.ShouldBe("\u001b[>1u\u001b[<u");
    }

    /// <summary>Verifies a failed xterm enable attempt still owns its exact restoration.</summary>
    [Fact]
    public async Task RunAsync_WhenXtermKeyboardEnableFails_RestoresAndPreservesFailureAsync()
    {
        await using SessionTransport transport = new() { FailWriteAt = 1 };
        await using FakeResizeSource resize = new();
        var sink = new RuntimeSink();
        var supported = new Feature(CapabilitySupport.Supported, Origin.Override);
        var options = TerminalOptions.Minimal with
        {
            Capabilities = TerminalCapabilities.Conservative with { XtermKeyboard = supported },
            ModifyOtherKeys = 2
        };
        await using Session session = new(transport, resize, sink, options);

        var thrown = await Should.ThrowAsync<IOException>(async () =>
            await session.RunAsync(TestContext.Current.CancellationToken));

        thrown.ShouldBeSameAs(transport.WriteFailure);
        transport.JoinedWrites.ShouldBe("\u001b[>4m");
    }

    /// <summary>
    /// Verifies a failing resize disposal never abandons the transport. Every owned resource is
    /// attempted exactly once and the first failure is the one the caller observes.
    /// </summary>
    [Fact]
    public async Task DisposeAsync_WhenResizeDisposalFails_StillDisposesTransportAsync()
    {
        var transport = new SessionTransport();
        var resize = new FailingResizeSource();
        var sink = new RuntimeSink();
        var session = new Session(transport, resize, sink, TerminalOptions.Minimal);

        var thrown = await Should.ThrowAsync<IOException>(async () => await session.DisposeAsync());

        thrown.ShouldBeSameAs(resize.Failure);
        resize.DisposeCount.ShouldBe(1);
        transport.DisposeCount.ShouldBe(1);
    }

    /// <summary>
    /// Verifies a second disposal after a failed first is quiet and attempts nothing again, so an
    /// outer <c>await using</c> cannot repeat a failed teardown.
    /// </summary>
    [Fact]
    public async Task DisposeAsync_WhenCalledAgainAfterFailure_IsQuietAndAttemptsNothingAsync()
    {
        var transport = new SessionTransport();
        var resize = new FailingResizeSource();
        var sink = new RuntimeSink();
        var session = new Session(transport, resize, sink, TerminalOptions.Minimal);
        _ = await Should.ThrowAsync<IOException>(async () => await session.DisposeAsync());

        await session.DisposeAsync();

        resize.DisposeCount.ShouldBe(1);
        transport.DisposeCount.ShouldBe(1);
    }

    /// <summary>
    /// Verifies disposal during an active run completes reverse mode restoration before it tears
    /// down the transport those restoration writes depend on.
    /// </summary>
    [Fact]
    public async Task DisposeAsync_WhenRunIsActive_WritesReverseModeBytesBeforeDisposingTransportAsync()
    {
        var transport = new SessionTransport();
        await using FakeResizeSource resize = new();
        var sink = new RuntimeSink();
        var session = new Session(
            transport,
            resize,
            sink,
            new TerminalOptions { Capabilities = Supported() });
        var running = session.RunAsync(TestContext.Current.CancellationToken).AsTask();
        await transport.FirstRead.Task.WaitAsync(TestContext.Current.CancellationToken);

        await session.DisposeAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(running);
        transport.DisposeCount.ShouldBe(1);
        transport.WritesAtDisposal.ShouldEndWith("\u001b[?25h\u001b[?1049l");
        session.LastCleanupException.ShouldBeNull();
    }

    /// <summary>
    /// Verifies two concurrent disposal callers share one teardown, restore terminal modes exactly
    /// once, and both return only after that teardown finished.
    /// </summary>
    [Fact]
    public async Task DisposeAsync_WhenCalledConcurrentlyByTwoCallers_RestoresModesExactlyOnceAsync()
    {
        var transport = new SessionTransport();
        await using FakeResizeSource resize = new();
        var sink = new RuntimeSink();
        var session = new Session(
            transport,
            resize,
            sink,
            new TerminalOptions { Capabilities = Supported() });
        var running = session.RunAsync(TestContext.Current.CancellationToken).AsTask();
        await transport.FirstRead.Task.WaitAsync(TestContext.Current.CancellationToken);

        var first = session.DisposeAsync().AsTask();
        var second = session.DisposeAsync().AsTask();
        await Task.WhenAll(first, second).WaitAsync(TestContext.Current.CancellationToken);

        _ = await Should.ThrowAsync<OperationCanceledException>(running);
        transport.DisposeCount.ShouldBe(1);
        transport.WritesAtDisposal.ShouldEndWith("\u001b[?25h\u001b[?1049l");
        transport.JoinedWrites.Split("\u001b[?1049l").Length.ShouldBe(2);
        session.LastCleanupException.ShouldBeNull();
    }

    /// <summary>
    /// Verifies a run requested once disposal has begun is rejected by the lifecycle guard instead
    /// of failing later against disposed lifetime state.
    /// </summary>
    [Fact]
    public async Task RunAsync_WhenStartedAfterDisposalBegins_ThrowsObjectDisposedExceptionAsync()
    {
        var transport = new SessionTransport();
        await using FakeResizeSource resize = new();
        var sink = new RuntimeSink();
        var session = new Session(transport, resize, sink, TerminalOptions.Minimal);
        await session.DisposeAsync();

        var thrown = await Should.ThrowAsync<ObjectDisposedException>(async () =>
            await session.RunAsync(TestContext.Current.CancellationToken));

        thrown.ObjectName.ShouldBe(typeof(Session).FullName);
        transport.JoinedWrites.ShouldBeEmpty();
    }

    /// <summary>
    /// Verifies a sink failure raised while a read still borrows the rental never clears or
    /// releases that pooled array.
    /// </summary>
    [Fact]
    public async Task RunAsync_WhenResizeSinkFailsDuringPendingRead_DoesNotReturnBorrowedBufferAsync()
    {
        await using PendingReadTransport transport = new();
        await using FakeResizeSource resize = new();
        var failure = new InvalidOperationException("resize sink failed");
        var sink = new RuntimeSink { ResizeFailure = failure };
        var options = TerminalOptions.Minimal with
        {
            ReadBufferSize = 256,
            CleanupTimeout = TimeSpan.FromMilliseconds(50)
        };
        await using Session session = new(transport, resize, sink, options);
        var running = session.RunAsync(TestContext.Current.CancellationToken).AsTask();
        await transport.ReadStarted.Task.WaitAsync(TestContext.Current.CancellationToken);

        resize.Resize(new Dimensions(new Size(80, 24)));
        var thrown = await Should.ThrowAsync<InvalidOperationException>(running);

        thrown.ShouldBeSameAs(failure);
        transport.IsReadPending.ShouldBeTrue();
        transport.Borrowed.Length.ShouldBe(256);
        transport.Borrowed.ToArray().ShouldAllBe(value => value == PendingReadTransport.Sentinel);
    }

    /// <summary>
    /// Verifies the session waits for a transport whose cancellation completes asynchronously
    /// before it stops owning the rented read buffer.
    /// </summary>
    [Fact]
    public async Task RunAsync_WhenCancellationCompletesAsynchronously_DrainsReadBeforeReturningAsync()
    {
        await using PendingReadTransport transport = new();
        await using FakeResizeSource resize = new();
        var sink = new RuntimeSink();
        var options = TerminalOptions.Minimal with { CleanupTimeout = TimeSpan.FromSeconds(30) };
        await using Session session = new(transport, resize, sink, options);
        using var cancellation = new CancellationTokenSource();
        var running = session.RunAsync(cancellation.Token).AsTask();
        await transport.ReadStarted.Task.WaitAsync(TestContext.Current.CancellationToken);

        await cancellation.CancelAsync();
        await transport.CancellationObserved.Task.WaitAsync(TestContext.Current.CancellationToken);
        _ = await Should.ThrowAsync<TimeoutException>(
            () => running.WaitAsync(TimeSpan.FromMilliseconds(250), TestContext.Current.CancellationToken));

        transport.ReleaseCancelledRead();
        _ = await Should.ThrowAsync<OperationCanceledException>(running);
        transport.IsReadPending.ShouldBeFalse();
    }

    /// <summary>
    /// Verifies a transport that never completes its read cannot stall shutdown past the
    /// configured cleanup budget.
    /// </summary>
    [Fact]
    public async Task RunAsync_WhenTransportIgnoresCancellation_ReleasesWithinCleanupBudgetAsync()
    {
        await using PendingReadTransport transport = new();
        await using FakeResizeSource resize = new();
        var sink = new RuntimeSink();
        var options = TerminalOptions.Minimal with { CleanupTimeout = TimeSpan.FromMilliseconds(50) };
        await using Session session = new(transport, resize, sink, options);
        using var cancellation = new CancellationTokenSource();
        var running = session.RunAsync(cancellation.Token).AsTask();
        await transport.ReadStarted.Task.WaitAsync(TestContext.Current.CancellationToken);

        await cancellation.CancelAsync();
        _ = await Should.ThrowAsync<OperationCanceledException>(
            running.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));

        transport.IsReadPending.ShouldBeTrue();
    }

    private static TerminalCapabilities Supported()
    {
        var supported = new Feature(CapabilitySupport.Supported, Origin.Override);
        return TerminalCapabilities.Conservative with
        {
            FocusReporting = supported,
            BracketedPaste = supported,
            CellMouse = supported,
            KittyKeyboard = supported
        };
    }

    #region Description-lifecycle programs and keypad selection

    /// <summary>Verifies noncanonical lifecycle bytes are emitted exactly and restored in reverse order.</summary>
    [Fact]
    public async Task RunAsync_WhenDescriptionProvidesLifecyclePairs_UsesExactProgramsAsync()
    {
        // Arrange
        await using SessionTransport transport = new();
        await using FakeResizeSource resize = new();
        var profile = Profile(
            new Dictionary<string, DescriptionProgram>
            {
                ["smcup"] = new("screen-in"u8),
                ["rmcup"] = new("screen-out"u8),
                ["civis"] = new("cursor-off"u8),
                ["cnorm"] = new("cursor-on"u8)
            });
        transport.Close();
        await using Session session = new(
            transport,
            resize,
            new RuntimeSink(),
            TerminalOptions.Minimal with
            {
                Profile = profile,
                AlternateScreen = true,
                HideCursor = true
            });

        // Act
        await session.RunAsync(TestContext.Current.CancellationToken);

        // Assert
        transport.JoinedWrites.ShouldBe("screen-incursor-offcursor-onscreen-out");
    }

    /// <summary>Verifies one-sided lifecycle descriptions never emit a half lease.</summary>
    [Theory]
    [InlineData("smcup")]
    [InlineData("rmcup")]
    [InlineData("civis")]
    [InlineData("cnorm")]
    public async Task RunAsync_WhenDescriptionLifecyclePairIsIncomplete_OmitsPairAsync(
        string retainedName)
    {
        // Arrange
        await using SessionTransport transport = new();
        await using FakeResizeSource resize = new();
        var profile = Profile(new Dictionary<string, DescriptionProgram>
        {
            [retainedName] = new DescriptionProgram("one-sided"u8)
        });
        transport.Close();
        await using Session session = new(
            transport,
            resize,
            new RuntimeSink(),
            TerminalOptions.Minimal with
            {
                Profile = profile,
                AlternateScreen = true,
                HideCursor = true
            });

        // Act
        await session.RunAsync(TestContext.Current.CancellationToken);

        // Assert
        transport.JoinedWrites.ShouldBeEmpty();
    }

    /// <summary>Verifies a zero-parameter lifecycle path rejects parameter-consuming programs atomically.</summary>
    [Fact]
    public async Task RunAsync_WhenLifecycleProgramConsumesParameter_OmitsPairBeforeOutputAsync()
    {
        // Arrange
        await using SessionTransport transport = new();
        await using FakeResizeSource resize = new();
        var profile = Profile(new Dictionary<string, DescriptionProgram>
        {
            ["smcup"] = new DescriptionProgram("prefix%p1%dsuffix"u8),
            ["rmcup"] = new DescriptionProgram("restore"u8)
        });
        transport.Close();
        await using Session session = new(
            transport,
            resize,
            new RuntimeSink(),
            TerminalOptions.Minimal with
            {
                Profile = profile,
                AlternateScreen = true
            });

        // Act
        await session.RunAsync(TestContext.Current.CancellationToken);

        // Assert
        transport.JoinedWrites.ShouldBeEmpty();
    }

    /// <summary>Verifies every exact SS3 application cursor spelling leases the described keypad pair.</summary>
    /// <param name="code">The logical cursor, Home, or End code.</param>
    /// <param name="final">The exact SS3 final byte.</param>
    [Theory]
    [InlineData(Code.Up, 'A')]
    [InlineData(Code.Down, 'B')]
    [InlineData(Code.Right, 'C')]
    [InlineData(Code.Left, 'D')]
    [InlineData(Code.Home, 'H')]
    [InlineData(Code.End, 'F')]
    public async Task RunAsync_WhenKeyMapContainsApplicationCursorBinding_LeasesKeypadAsync(
        Code code,
        char final)
    {
        // Arrange
        await using SessionTransport transport = new();
        await using FakeResizeSource resize = new();
        var sequence = new[] { (byte) 0x1b, (byte) 'O', checked((byte) final) };
        var profile = Profile(
            new Dictionary<string, DescriptionProgram>
            {
                ["smkx"] = new("keys-in"u8),
                ["rmkx"] = new("keys-out"u8)
            },
            new KeyMap([new KeyBinding(sequence, code)]));
        transport.Close();
        await using Session session = new(
            transport,
            resize,
            new RuntimeSink(),
            TerminalOptions.Minimal with { Profile = profile });

        // Act
        await session.RunAsync(TestContext.Current.CancellationToken);

        // Assert
        transport.JoinedWrites.ShouldBe("keys-inkeys-out");
    }

    /// <summary>Verifies an eight-bit SS3 application cursor spelling selects the described keypad pair.</summary>
    [Fact]
    public async Task RunAsync_WhenKeyMapContainsEightBitApplicationCursorBinding_LeasesKeypadAsync()
    {
        // Arrange
        await using SessionTransport transport = new();
        await using FakeResizeSource resize = new();
        var profile = Profile(
            new Dictionary<string, DescriptionProgram>
            {
                ["smkx"] = new("keys-in"u8),
                ["rmkx"] = new("keys-out"u8)
            },
            new KeyMap([new KeyBinding([0x8f, (byte) 'A'], Code.Up)]));
        transport.Close();
        await using Session session = new(
            transport,
            resize,
            new RuntimeSink(),
            TerminalOptions.Minimal with { Profile = profile });

        // Act
        await session.RunAsync(TestContext.Current.CancellationToken);

        // Assert
        transport.JoinedWrites.ShouldBe("keys-inkeys-out");
    }

    /// <summary>Verifies an SS3 final paired with the wrong logical code does not request application mode.</summary>
    [Fact]
    public async Task RunAsync_WhenSs3ApplicationFinalHasMismatchedCode_OmitsKeypadAsync()
    {
        // Arrange
        await using SessionTransport transport = new();
        await using FakeResizeSource resize = new();
        var profile = Profile(
            KeypadPrograms(),
            new KeyMap([new KeyBinding("\u001bOA"u8, Code.Down)]));
        transport.Close();
        await using Session session = new(
            transport,
            resize,
            new RuntimeSink(),
            TerminalOptions.Minimal with { Profile = profile });

        // Act
        await session.RunAsync(TestContext.Current.CancellationToken);

        // Assert
        transport.JoinedWrites.ShouldBeEmpty();
    }

    /// <summary>Verifies an application binding never permits a one-sided keypad lease.</summary>
    /// <param name="retainedName">The only retained keypad program name.</param>
    [Theory]
    [InlineData("smkx")]
    [InlineData("rmkx")]
    public async Task RunAsync_WhenApplicationKeyMapHasOneSidedKeypadPair_OmitsKeypadAsync(
        string retainedName)
    {
        // Arrange
        await using SessionTransport transport = new();
        await using FakeResizeSource resize = new();
        var profile = Profile(
            new Dictionary<string, DescriptionProgram>
            {
                [retainedName] = new DescriptionProgram("one-sided"u8)
            },
            new KeyMap([new KeyBinding("\u001bOA"u8, Code.Up)]));
        transport.Close();
        await using Session session = new(
            transport,
            resize,
            new RuntimeSink(),
            TerminalOptions.Minimal with { Profile = profile });

        // Act
        await session.RunAsync(TestContext.Current.CancellationToken);

        // Assert
        transport.JoinedWrites.ShouldBeEmpty();
    }

    /// <summary>Verifies normal cursor spellings do not require terminal application-key mode.</summary>
    [Fact]
    public async Task RunAsync_WhenKeyMapContainsOnlyNormalCursorBinding_OmitsKeypadAsync()
    {
        // Arrange
        await using SessionTransport transport = new();
        await using FakeResizeSource resize = new();
        var profile = Profile(
            KeypadPrograms(),
            new KeyMap([new KeyBinding("\u001b[A"u8, Code.Up)]));
        transport.Close();
        await using Session session = new(
            transport,
            resize,
            new RuntimeSink(),
            TerminalOptions.Minimal with { Profile = profile });

        // Act
        await session.RunAsync(TestContext.Current.CancellationToken);

        // Assert
        transport.JoinedWrites.ShouldBeEmpty();
    }

    /// <summary>Verifies SS3 function keys remain valid normal-mode spellings and do not force keypad mode.</summary>
    [Fact]
    public async Task RunAsync_WhenKeyMapContainsOnlySs3FunctionKeys_OmitsKeypadAsync()
    {
        // Arrange
        await using SessionTransport transport = new();
        await using FakeResizeSource resize = new();
        var profile = Profile(
            KeypadPrograms(),
            new KeyMap(
            [
                new KeyBinding("\u001bOP"u8, Code.F1),
                new KeyBinding("\u001bOQ"u8, Code.F2),
                new KeyBinding("\u001bOR"u8, Code.F3),
                new KeyBinding("\u001bOS"u8, Code.F4)
            ]));
        transport.Close();
        await using Session session = new(
            transport,
            resize,
            new RuntimeSink(),
            TerminalOptions.Minimal with { Profile = profile });

        // Act
        await session.RunAsync(TestContext.Current.CancellationToken);

        // Assert
        transport.JoinedWrites.ShouldBeEmpty();
    }

    /// <summary>Verifies a mixed normal/application map still requests its described application mode.</summary>
    [Fact]
    public async Task RunAsync_WhenKeyMapMixesNormalAndApplicationCursorBindings_LeasesKeypadAsync()
    {
        // Arrange
        await using SessionTransport transport = new();
        await using FakeResizeSource resize = new();
        var profile = Profile(
            KeypadPrograms(),
            new KeyMap(
            [
                new KeyBinding("\u001b[A"u8, Code.Up),
                new KeyBinding("\u001bOA"u8, Code.Up)
            ]));
        transport.Close();
        await using Session session = new(
            transport,
            resize,
            new RuntimeSink(),
            TerminalOptions.Minimal with { Profile = profile });

        // Act
        await session.RunAsync(TestContext.Current.CancellationToken);

        // Assert
        transport.JoinedWrites.ShouldBe("keys-inkeys-out");
    }

    /// <summary>Verifies an empty key map never enters keypad application mode.</summary>
    [Fact]
    public async Task RunAsync_WhenKeyMapIsEmpty_OmitsKeypadAsync()
    {
        // Arrange
        await using SessionTransport transport = new();
        await using FakeResizeSource resize = new();
        var profile = Profile(KeypadPrograms());
        transport.Close();
        await using Session session = new(
            transport,
            resize,
            new RuntimeSink(),
            TerminalOptions.Minimal with { Profile = profile });

        // Act
        await session.RunAsync(TestContext.Current.CancellationToken);

        // Assert
        transport.JoinedWrites.ShouldBeEmpty();
    }

    /// <summary>Verifies a partial acquire write is conservatively restored with the exact paired program.</summary>
    [Fact]
    public async Task RunAsync_WhenDescriptionAcquirePartiallyWrites_RestoresAndPreservesFailureAsync()
    {
        // Arrange
        await using SessionTransport transport = new()
        {
            FailWriteAt = 1,
            PartialWriteBytes = 4
        };
        await using FakeResizeSource resize = new();
        var profile = Profile(new Dictionary<string, DescriptionProgram>
        {
            ["smcup"] = new DescriptionProgram("screen-in"u8),
            ["rmcup"] = new DescriptionProgram("screen-out"u8)
        });
        await using Session session = new(
            transport,
            resize,
            new RuntimeSink(),
            TerminalOptions.Minimal with
            {
                Profile = profile,
                AlternateScreen = true
            });

        // Act
        var thrown = await Should.ThrowAsync<IOException>(async () =>
            await session.RunAsync(TestContext.Current.CancellationToken));

        // Assert
        thrown.ShouldBeSameAs(transport.WriteFailure);
        transport.JoinedWrites.ShouldBe("screscreen-out");
    }

    /// <summary>Verifies cancellation after a partial acquire still runs exact uncancelled cleanup.</summary>
    [Fact]
    public async Task RunAsync_WhenDescriptionAcquireIsCancelledAfterPartialWrite_RestoresAsync()
    {
        // Arrange
        await using SessionTransport transport = new()
        {
            CancelWriteAt = 1,
            PartialWriteBytes = 3
        };
        await using FakeResizeSource resize = new();
        var profile = Profile(new Dictionary<string, DescriptionProgram>
        {
            ["smcup"] = new DescriptionProgram("screen-in"u8),
            ["rmcup"] = new DescriptionProgram("screen-out"u8)
        });
        await using Session session = new(
            transport,
            resize,
            new RuntimeSink(),
            TerminalOptions.Minimal with
            {
                Profile = profile,
                AlternateScreen = true
            });

        // Act
        _ = await Should.ThrowAsync<OperationCanceledException>(async () =>
            await session.RunAsync(TestContext.Current.CancellationToken));

        // Assert
        transport.JoinedWrites.ShouldBe("scrscreen-out");
    }

    /// <summary>Verifies a failed acquire flush still restores the exact possibly-active lease.</summary>
    [Fact]
    public async Task RunAsync_WhenDescriptionAcquireFlushFails_RestoresAndPreservesFailureAsync()
    {
        // Arrange
        await using SessionTransport transport = new() { FailFlushAt = 1 };
        await using FakeResizeSource resize = new();
        var profile = Profile(new Dictionary<string, DescriptionProgram>
        {
            ["smcup"] = new DescriptionProgram("screen-in"u8),
            ["rmcup"] = new DescriptionProgram("screen-out"u8)
        });
        await using Session session = new(
            transport,
            resize,
            new RuntimeSink(),
            TerminalOptions.Minimal with
            {
                Profile = profile,
                AlternateScreen = true
            });

        // Act
        var thrown = await Should.ThrowAsync<IOException>(async () =>
            await session.RunAsync(TestContext.Current.CancellationToken));

        // Assert
        thrown.ShouldBeSameAs(transport.FlushFailure);
        transport.JoinedWrites.ShouldBe("screen-inscreen-out");
    }

    /// <summary>Verifies cleanup continues in reverse exact order while the original read failure remains primary.</summary>
    [Fact]
    public async Task RunAsync_WhenDescriptionCleanupAndReadFail_PreservesReadAndContinuesCleanupAsync()
    {
        // Arrange
        await using SessionTransport transport = new()
        {
            ReadFailure = new IOException("read failed"),
            FailWriteAt = 4
        };
        await using FakeResizeSource resize = new();
        var profile = Profile(
            new Dictionary<string, DescriptionProgram>
            {
                ["smcup"] = new("screen-in"u8),
                ["rmcup"] = new("screen-out"u8),
                ["civis"] = new("cursor-off"u8),
                ["cnorm"] = new("cursor-on"u8),
                ["smkx"] = new("keys-in"u8),
                ["rmkx"] = new("keys-out"u8)
            },
            new KeyMap([new KeyBinding("\u001bOA"u8, Code.Up)]));
        await using Session session = new(
            transport,
            resize,
            new RuntimeSink(),
            TerminalOptions.Minimal with
            {
                Profile = profile,
                AlternateScreen = true,
                HideCursor = true
            });

        // Act
        var thrown = await Should.ThrowAsync<IOException>(async () =>
            await session.RunAsync(TestContext.Current.CancellationToken));

        // Assert
        thrown.ShouldBeSameAs(transport.ReadFailure);
        session.LastCleanupException.ShouldBeSameAs(transport.WriteFailure);
        transport.JoinedWrites.ShouldBe(
            "screen-incursor-offkeys-incursor-onscreen-out");
    }

    /// <summary>Verifies supported environment/default evidence never authorizes optional output.</summary>
    /// <param name="origin">The insufficient semantic evidence origin.</param>
    /// <param name="explicitProfile">Whether to use an explicit rather than built-in ANSI profile.</param>
    [Theory]
    [InlineData(Origin.Environment, false)]
    [InlineData(Origin.Default, false)]
    [InlineData(Origin.Environment, true)]
    [InlineData(Origin.Default, true)]
    public async Task RunAsync_WhenOptionalSupportOriginIsNotAuthoritative_OmitsModeAsync(
        Origin origin,
        bool explicitProfile)
    {
        // Arrange
        await using SessionTransport transport = new();
        await using FakeResizeSource resize = new();
        var capabilities = TerminalCapabilities.Conservative with
        {
            FocusReporting = new Feature(CapabilitySupport.Supported, origin)
        };
        var profile = explicitProfile
            ? Profile(
                new Dictionary<string, DescriptionProgram>(),
                capabilities: capabilities,
                descriptionOrigin: DescriptionOrigin.Explicit)
            : TerminalProfile.CreateAnsi(capabilities);
        transport.Close();
        await using Session session = new(
            transport,
            resize,
            new RuntimeSink(),
            TerminalOptions.Minimal with
            {
                Profile = profile,
                Focus = true
            });

        // Act
        await session.RunAsync(TestContext.Current.CancellationToken);

        // Assert
        transport.JoinedWrites.ShouldBeEmpty();
    }

    /// <summary>Verifies bounded-query and explicit-override evidence authorize typed optional output.</summary>
    /// <param name="origin">The authoritative semantic evidence origin.</param>
    [Theory]
    [InlineData(Origin.Query)]
    [InlineData(Origin.Override)]
    public async Task RunAsync_WhenOptionalSupportOriginIsAuthoritative_UsesTypedModeAsync(
        Origin origin)
    {
        // Arrange
        await using SessionTransport transport = new();
        await using FakeResizeSource resize = new();
        var capabilities = TerminalCapabilities.Conservative with
        {
            FocusReporting = new Feature(CapabilitySupport.Supported, origin)
        };
        transport.Close();
        await using Session session = new(
            transport,
            resize,
            new RuntimeSink(),
            TerminalOptions.Minimal with
            {
                Profile = TerminalProfile.CreateAnsi(capabilities),
                Focus = true
            });

        // Act
        await session.RunAsync(TestContext.Current.CancellationToken);

        // Assert
        transport.JoinedWrites.ShouldBe("\u001b[?1004h\u001b[?1004l");
    }

    /// <summary>Verifies session expansion obeys the configured program-output bound before any write.</summary>
    [Fact]
    public async Task RunAsync_WhenLifecycleExpansionExceedsLimit_OmitsPairBeforeOutputAsync()
    {
        // Arrange
        await using SessionTransport transport = new();
        await using FakeResizeSource resize = new();
        var profile = Profile(new Dictionary<string, DescriptionProgram>
        {
            ["smcup"] = new DescriptionProgram("1234"u8),
            ["rmcup"] = new DescriptionProgram("4321"u8)
        });
        transport.Close();
        await using Session session = new(
            transport,
            resize,
            new RuntimeSink(),
            TerminalOptions.Minimal with
            {
                Profile = profile,
                AlternateScreen = true,
                Input = InputOptions.Default with
                {
                    ProgramLimits = ProgramLimits.Default with { MaxProgramOutputBytes = 3 }
                }
            });

        // Act
        await session.RunAsync(TestContext.Current.CancellationToken);

        // Assert
        transport.JoinedWrites.ShouldBeEmpty();
    }

    /// <summary>Verifies exact database focus backing permits the typed focus lease.</summary>
    [Fact]
    public async Task RunAsync_WhenDatabaseFocusBackingIsComplete_UsesTypedLeaseAsync()
    {
        // Arrange
        await using SessionTransport transport = new();
        await using FakeResizeSource resize = new();
        var supported = new Feature(CapabilitySupport.Supported, Origin.Database);
        var profile = Profile(
            new Dictionary<string, DescriptionProgram>
            {
                ["fe"] = new("focus-in"u8),
                ["fd"] = new("focus-out"u8),
                ["kxIN"] = new("event-in"u8),
                ["kxOUT"] = new("event-out"u8)
            },
            capabilities: TerminalCapabilities.Conservative with
            {
                FocusReporting = supported
            });
        transport.Close();
        await using Session session = new(
            transport,
            resize,
            new RuntimeSink(),
            TerminalOptions.Minimal with
            {
                Profile = profile,
                Focus = true
            });

        // Act
        await session.RunAsync(TestContext.Current.CancellationToken);

        // Assert
        transport.JoinedWrites.ShouldBe("\u001b[?1004h\u001b[?1004l");
    }

    /// <summary>Verifies one session preserves ncurses static variables across paired programs.</summary>
    [Fact]
    public async Task RunAsync_WhenLifecycleProgramsShareStaticVariable_PreservesSessionStateAsync()
    {
        // Arrange
        await using SessionTransport transport = new();
        await using FakeResizeSource resize = new();
        var profile = Profile(new Dictionary<string, DescriptionProgram>
        {
            ["smcup"] = new DescriptionProgram("%{42}%PAenter"u8),
            ["rmcup"] = new DescriptionProgram("%gA%dexit"u8)
        });
        transport.Close();
        await using Session session = new(
            transport,
            resize,
            new RuntimeSink(),
            TerminalOptions.Minimal with
            {
                Profile = profile,
                AlternateScreen = true
            });

        // Act
        await session.RunAsync(TestContext.Current.CancellationToken);

        // Assert
        transport.JoinedWrites.ShouldBe("enter42exit");
    }

    /// <summary>Verifies a rejected pair cannot leak staged static-variable changes into a later pair.</summary>
    [Fact]
    public async Task RunAsync_WhenLifecyclePairExpansionFails_RollsBackStaticVariablesAsync()
    {
        // Arrange
        await using SessionTransport transport = new();
        await using FakeResizeSource resize = new();
        var profile = Profile(new Dictionary<string, DescriptionProgram>
        {
            ["smcup"] = new DescriptionProgram("%{42}%PAenter"u8),
            ["rmcup"] = new DescriptionProgram("%p1%d"u8),
            ["civis"] = new DescriptionProgram("%gA%dhide"u8),
            ["cnorm"] = new DescriptionProgram("show"u8)
        });
        transport.Close();
        await using Session session = new(
            transport,
            resize,
            new RuntimeSink(),
            TerminalOptions.Minimal with
            {
                Profile = profile,
                AlternateScreen = true,
                HideCursor = true
            });

        // Act
        await session.RunAsync(TestContext.Current.CancellationToken);

        // Assert
        transport.JoinedWrites.ShouldBe("0hideshow");
    }

    /// <summary>Verifies an empty pair expansion is non-emittable and cannot commit static variables.</summary>
    [Fact]
    public async Task RunAsync_WhenLifecyclePairExpandsEmpty_RollsBackStaticVariablesAsync()
    {
        // Arrange
        await using SessionTransport transport = new();
        await using FakeResizeSource resize = new();
        var profile = Profile(new Dictionary<string, DescriptionProgram>
        {
            ["smcup"] = new DescriptionProgram("%{42}%PA%?%{0}%tenter%;"u8),
            ["rmcup"] = new DescriptionProgram("restore"u8),
            ["civis"] = new DescriptionProgram("%gA%dhide"u8),
            ["cnorm"] = new DescriptionProgram("show"u8)
        });
        transport.Close();
        await using Session session = new(
            transport,
            resize,
            new RuntimeSink(),
            TerminalOptions.Minimal with
            {
                Profile = profile,
                AlternateScreen = true,
                HideCursor = true
            });

        // Act
        await session.RunAsync(TestContext.Current.CancellationToken);

        // Assert
        transport.JoinedWrites.ShouldBe("0hideshow");
    }

    private static Dictionary<string, DescriptionProgram> KeypadPrograms() => new(StringComparer.Ordinal)
    {
        ["smkx"] = new DescriptionProgram("keys-in"u8),
        ["rmkx"] = new DescriptionProgram("keys-out"u8)
    };

    private static TerminalProfile Profile(
        IReadOnlyDictionary<string, DescriptionProgram> lifecyclePrograms,
        KeyMap? keyMap = null,
        TerminalCapabilities? capabilities = null,
        DescriptionOrigin descriptionOrigin = DescriptionOrigin.Database)
    {
        var programs = new Dictionary<string, DescriptionProgram>(StringComparer.Ordinal)
        {
            ["cup"] = new DescriptionProgram("\u001b[%i%p1%d;%p2%dH"u8),
            ["sgr0"] = new DescriptionProgram("reset"u8),
            ["clear"] = new DescriptionProgram("clear"u8)
        };

        foreach (var pair in lifecyclePrograms)
        {
            programs.Add(pair.Key, pair.Value);
        }

        return new TerminalProfile(
            new Description("fixture", descriptionOrigin, Suitability.Usable),
            capabilities ?? TerminalCapabilities.Conservative,
            new Programs(programs),
            keyMap ?? KeyMap.Empty);
    }

    #endregion

    #region Cleanup budget

    /// <summary>The regression this file exists to pin. A stalled write consumes the whole budget,
    /// and the restores queued behind it must still reach the transport.</summary>
    [Fact]
    public async Task RunAsync_WhenCleanupBudgetExpiresMidWalk_StillRestoresTheAlternateScreenAsync()
    {
        await using SessionTransport transport = new();
        await using FakeResizeSource resize = new();
        var sink = new RuntimeSink();
        var options = new TerminalOptions
        {
            Capabilities = Supported(),
            CleanupTimeout = TimeSpan.FromMilliseconds(50)
        };
        await using Session session = new(transport, resize, sink, options);
        using var cancellation = new CancellationTokenSource();
        var running = session.RunAsync(cancellation.Token).AsTask();
        await transport.FirstRead.Task.WaitAsync(TestContext.Current.CancellationToken);

        // Armed here so the stall lands on the first cleanup disable, whatever index that is.
        transport.StallNextWrite = true;
        await cancellation.CancelAsync();
        _ = await Should.ThrowAsync<OperationCanceledException>(running);

        // The two the user sees. Before the fix the stalled first disable burned the budget and
        // these never reached the transport at all.
        transport.JoinedWrites.EndsWith("[?25h[?1049l", StringComparison.Ordinal)
            .ShouldBeTrue(
                "cursor policy and the alternate screen must be restored even after budget " +
                $"expiry, but the transport saw '{transport.JoinedWrites}'");
    }

    /// <summary>Verifies the whole tail is recovered, not just the last pair - the walk resumes
    /// rather than skipping to the end.</summary>
    [Fact]
    public async Task RunAsync_WhenCleanupBudgetExpiresMidWalk_EmitsEveryRemainingDisableAsync()
    {
        await using SessionTransport transport = new();
        await using FakeResizeSource resize = new();
        var sink = new RuntimeSink();
        var options = new TerminalOptions
        {
            Capabilities = Supported(),
            CleanupTimeout = TimeSpan.FromMilliseconds(50)
        };
        await using Session session = new(transport, resize, sink, options);
        using var cancellation = new CancellationTokenSource();
        var running = session.RunAsync(cancellation.Token).AsTask();
        await transport.FirstRead.Task.WaitAsync(TestContext.Current.CancellationToken);

        // Armed here so the stall lands on the first cleanup disable, whatever index that is.
        transport.StallNextWrite = true;
        await cancellation.CancelAsync();
        _ = await Should.ThrowAsync<OperationCanceledException>(running);

        var written = transport.JoinedWrites;

        // Compared against the same session's own enable sequence rather than a hardcoded list, so
        // this keeps meaning if the lease set changes.
        foreach (var disable in new[] { "[?1049l", "[?25h", "[?1000l", "[?1006l" })
        {
            written.Contains(disable, StringComparison.Ordinal).ShouldBeTrue(
                $"the walk must resume far enough to emit '{disable}', but saw '{written}'");
        }
    }

    /// <summary>The counter-case that keeps the renewal honest: with no stall, one budget covers
    /// the walk and nothing is renewed, so the ordinary path is unchanged.</summary>
    [Fact]
    public async Task RunAsync_WhenCleanupCompletesWithinBudget_ReportsNoCleanupFailureAsync()
    {
        await using SessionTransport transport = new();
        await using FakeResizeSource resize = new();
        var sink = new RuntimeSink();
        var options = new TerminalOptions
        {
            Capabilities = Supported(),
            CleanupTimeout = TimeSpan.FromSeconds(30)
        };
        await using Session session = new(transport, resize, sink, options);
        using var cancellation = new CancellationTokenSource();
        var running = session.RunAsync(cancellation.Token).AsTask();
        await transport.FirstRead.Task.WaitAsync(TestContext.Current.CancellationToken);

        await cancellation.CancelAsync();
        _ = await Should.ThrowAsync<OperationCanceledException>(running);

        session.LastCleanupException.ShouldBeNull();
        transport.JoinedWrites.ShouldEndWith("[?25h[?1049l");
    }

    #endregion
}
