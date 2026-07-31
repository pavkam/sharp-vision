// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Runtime;

using Capabilities;

using Kitty;

using SharpVision.Terminal.Backends;
using SharpVision.Terminal.Capabilities;
using SharpVision.Terminal.Input;
using SharpVision.Terminal.Multiplexing;

using MultiplexingOperation = Terminal.Multiplexing.Operation;

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
        var options = RuntimeOptions.Minimal with
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
        var policy = new Policy(
            [MultiplexerKind.Tmux],
            outerProfile,
            PassthroughMode.All,
            paneVisible: true,
            MultiplexingOperation.CapabilityQueries,
            maxDepth: 4,
            maxEnvelopeBytes: 8);
        var limits = Limits.Default with
        {
            MaxConcurrentQueries = 1,
            QueryTimeout = TimeSpan.FromMinutes(1)
        };
        var options = new RuntimeOptions
        {
            AlternateScreen = false,
            HideCursor = false,
            Focus = true,
            Paste = true,
            Tracking = MouseTracking.Press,
            Keyboard = KittyEnhancement.Disambiguate | KittyEnhancement.EventTypes,
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
        var policy = new Policy(
            [MultiplexerKind.Tmux],
            outerProfile,
            PassthroughMode.All,
            paneVisible: true,
            MultiplexingOperation.CapabilityQueries);
        var limits = Limits.Default with { MaxConcurrentQueries = 1 };
        var route = new Route(policy);
        var wrapped = new ArrayBufferWriter<byte>();
        route.TryWriteCapabilityQueries(wrapped, "\u001b[?1;2c"u8).ShouldBeTrue();

        foreach (var value in wrapped.WrittenSpan)
        {
            transport.Input([value]);
        }

        transport.Close();
        var options = RuntimeOptions.Minimal with
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
        var policy = new Policy(
            [MultiplexerKind.Screen],
            outerProfile,
            PassthroughMode.All,
            paneVisible: true,
            MultiplexingOperation.CapabilityQueries);
        var limits = Limits.Default with { MaxConcurrentQueries = 1 };
        var wrapped = new ArrayBufferWriter<byte>();
        GnuScreenWriter.WritePassthrough(wrapped, "\u001b[?1;2c"u8);

        foreach (var value in wrapped.WrittenSpan)
        {
            transport.Input([value]);
        }

        transport.Close();
        var options = RuntimeOptions.Minimal with
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
        _ = Should.Throw<ArgumentNullException>(() => new RuntimeOptions { Profile = null! });
        _ = Should.Throw<ArgumentNullException>(() => new RuntimeOptions { Capabilities = null! });
    }

    /// <summary>Verifies Options compatibility preserves exact capabilities and initializer source order.</summary>
    [Fact]
    public void Options_WhenProfileAndCapabilitiesAreInitialized_LastInitializerWins()
    {
        var database = new Feature(CapabilitySupport.Supported, Origin.Database);
        var capabilities = TerminalCapabilities.Conservative with { Osc52 = database };
        var profile = TerminalProfile.CreateAnsi(
            TerminalCapabilities.Conservative with { ColorDepth = ColorDepth.Indexed256 });

        var capabilitiesLast = new RuntimeOptions
        {
            Profile = profile,
            Capabilities = capabilities
        };
        var profileLast = new RuntimeOptions
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
            RuntimeOptions.Minimal with { Profile = profile }));

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
        await using Session session = new(transport, resize, sink, RuntimeOptions.Minimal);

        // Act
        await session.RunAsync(TestContext.Current.CancellationToken);

        // Assert
        RuntimeOptions.Minimal.Profile.Description.Suitability.ShouldBe(Suitability.Usable);
        RuntimeOptions.Minimal.ModifyOtherKeys.ShouldBeNull();
        transport.JoinedWrites.ShouldBeEmpty();
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
        var options = RuntimeOptions.Minimal with { Profile = profile };
        transport.Input("\u001b[99~"u8.ToArray());
        transport.Close();
        await using Session session = new(transport, resize, sink, options);

        // Act
        await session.RunAsync(TestContext.Current.CancellationToken);

        // Assert
        sink.Strokes.ShouldHaveSingleItem().Code.ShouldBe(Code.F63);
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
        var options = RuntimeOptions.Minimal with
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
        var options = RuntimeOptions.Minimal with
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
            "\u001b[?2004$p\u001b[?1006$p\u001b[?1016$p" +
            "\u001b[14t\u001b[16t\u001b[18t" +
            "\u001b]4;0;?\u001b\\\u001b]10;?\u001b\\\u001b]11;?\u001b\\");
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
        var limits = Limits.Default with { QueryTimeout = TimeSpan.FromSeconds(1) };
        var options = RuntimeOptions.Minimal with
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
        var options = RuntimeOptions.Minimal with
        {
            Negotiation = new NegotiationOptions(
                new Dictionary<string, string?>(),
                limits: Limits.Default with { MaxConcurrentQueries = 1 })
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
        var limits = Limits.Default with { QueryTimeout = TimeSpan.FromSeconds(1) };
        var options = RuntimeOptions.Minimal with
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
        var options = RuntimeOptions.Minimal with
        {
            Negotiation = new NegotiationOptions(
                new Dictionary<string, string?>(),
                limits: Limits.Default with { MaxConcurrentQueries = 8 })
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
        sink.Profiles.ShouldHaveSingleItem().SynchronizedOutput.IsSupported.ShouldBeTrue();
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
        var options = RuntimeOptions.Minimal with
        {
            Focus = true,
            Paste = true,
            Tracking = MouseTracking.Press,
            Keyboard = KittyEnhancement.Disambiguate | KittyEnhancement.EventTypes,
            Negotiation = new NegotiationOptions(
                new Dictionary<string, string?>(),
                limits: Limits.Default with { MaxConcurrentQueries = 8 })
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
            "\u001b[?1004h\u001b[?2004h\u001b[?1000h\u001b[?1006h\u001b[>3u" +
            "\u001b[<u\u001b[?1006l\u001b[?1000l\u001b[?2004l\u001b[?1004l");
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
        var options = RuntimeOptions.Minimal with
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

    /// <summary>Verifies a query write failure remains primary and publishes nothing.</summary>
    [Fact]
    public async Task RunAsync_WhenNegotiationQueryWriteFails_PreservesExceptionAsync()
    {
        // Arrange
        await using SessionTransport transport = new() { FailWriteAt = 1 };
        await using FakeResizeSource resize = new();
        var sink = new RuntimeSink();
        var options = RuntimeOptions.Minimal with
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
        var limits = Limits.Default with { QueryTimeout = TimeSpan.FromSeconds(1) };
        var options = RuntimeOptions.Minimal with
        {
            Focus = true,
            Negotiation = new NegotiationOptions(
                new Dictionary<string, string?>(),
                new Settings { FocusReporting = true },
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
        var options = RuntimeOptions.Minimal with
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
        await using SessionTransport transport = new();
        await using FakeResizeSource resize = new();
        var sink = new RuntimeSink();
        await using Session session = new(
            transport,
            resize,
            sink,
            RuntimeOptions.Minimal);
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
    /// and starve resize indefinitely (see #21).
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

        await using Session session = new(transport, resize, sink, RuntimeOptions.Minimal);
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
    /// forwarded rather than silently dropped by the read's early-return closure path (see #21).
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

        await using Session session = new(transport, resize, sink, RuntimeOptions.Minimal);

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
        await using SessionTransport transport = new();
        await using FakeResizeSource resize = new();
        var sink = new RuntimeSink();
        await using Session session = new(
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
        await using SessionTransport transport = new();
        await using FakeResizeSource resize = new();
        var sink = new RuntimeSink { TextFailure = new InvalidOperationException("handler") };
        transport.Input("x"u8.ToArray());
        await using Session session = new(
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

    /// <summary>Verifies authorized xterm enhancement is leased and restored when Kitty is absent.</summary>
    [Fact]
    public async Task RunAsync_WhenOnlyXtermKeyboardIsSupported_LeasesAndRestoresModifyOtherKeysAsync()
    {
        await using SessionTransport transport = new();
        await using FakeResizeSource resize = new();
        var sink = new RuntimeSink();
        var supported = new Feature(CapabilitySupport.Supported, Origin.Override);
        var capabilities = TerminalCapabilities.Conservative with { XtermKeyboard = supported };
        var options = RuntimeOptions.Minimal with
        {
            Capabilities = capabilities,
            Keyboard = KittyEnhancement.Disambiguate,
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
        var options = RuntimeOptions.Minimal with
        {
            Capabilities = capabilities,
            Keyboard = KittyEnhancement.Disambiguate,
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
        var options = RuntimeOptions.Minimal with
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
        var session = new Session(transport, resize, sink, RuntimeOptions.Minimal);

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
        var session = new Session(transport, resize, sink, RuntimeOptions.Minimal);
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
            new RuntimeOptions { Capabilities = Supported() });
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
            new RuntimeOptions { Capabilities = Supported() });
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
        var session = new Session(transport, resize, sink, RuntimeOptions.Minimal);
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
        var options = RuntimeOptions.Minimal with
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
        var options = RuntimeOptions.Minimal with { CleanupTimeout = TimeSpan.FromSeconds(30) };
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
        var options = RuntimeOptions.Minimal with { CleanupTimeout = TimeSpan.FromMilliseconds(50) };
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
}
