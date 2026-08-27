// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Discovery.Queries;

using Capabilities;

using SharpVision.Terminal.Capabilities;
using SharpVision.Terminal.Discovery.Queries;
using SharpVision.Terminal.Graphics.Backends;

using KittyResponse = Kitty.Graphics.KittyGraphicsResponse;

/// <summary>
/// Verifies bounded startup query encoding and profile publication for
/// <see cref="ActiveQueryDiscoveryStrategy"/>: the core xterm/DECRPM query batch, iTerm2 OSC 1337
/// Capabilities, Kitty graphics, sixel (DA1), xterm-specific DCS refinements (DECRQSS/XTGETTCAP),
/// and multiplexer-aware probe suppression.
/// </summary>
public sealed class ActiveQueryDiscoveryStrategyTests
{
    #region Core query batch and DECRPM/DA negotiation

    /// <summary>Verifies DA closes an unanswered ordered Kitty probe.</summary>
    [Fact]
    public void Accept_WhenDaPrecedesKeyboard_PublishesUnsupportedKeyboard()
    {
        // Arrange
        var limits = QueryLimits.Default with { MaxConcurrentQueries = 2 };
        var negotiator = new ActiveQueryDiscoveryStrategy(new NegotiationOptions(
            new Dictionary<string, string?> { ["TERM"] = "xterm-kitty" },
            limits: limits));
        _ = negotiator.TryStart(new ArrayBufferWriter<byte>(), null, null);
        var attributes = Response("?1;2"u8, [], (byte) 'c');

        // Act
        negotiator.Accept(in attributes).ShouldBe(QueryMatch.Matched);

        // Assert
        negotiator.Completed.ShouldBeTrue();
        var published = negotiator.Capabilities;
        published.KittyKeyboard.ShouldBe(
            new Feature(CapabilitySupport.Unsupported, Origin.Query));
        var keyboard = Response("?3"u8, [], (byte) 'u');
        negotiator.Accept(in keyboard).ShouldBe(QueryMatch.Late);
        negotiator.Capabilities.ShouldBeSameAs(published);
    }

    /// <summary>Verifies mode duplicates and unrelated reports cannot consume work.</summary>
    [Fact]
    public void Accept_WhenModeIsRepeatedOrUnknown_ClassifiesWithoutMutation()
    {
        // Arrange
        var limits = QueryLimits.Default with { MaxConcurrentQueries = 8 };
        var negotiator = new ActiveQueryDiscoveryStrategy(
            new NegotiationOptions(new Dictionary<string, string?>(), limits: limits));
        _ = negotiator.TryStart(new ArrayBufferWriter<byte>(), null, null);
        var synchronized = PrivateMode(2026, state: 1);
        var unknown = PrivateMode(25, state: 1);

        // Act / Assert
        negotiator.Accept(in synchronized).ShouldBe(QueryMatch.Matched);
        negotiator.Accept(in synchronized).ShouldBe(QueryMatch.Duplicate);
        negotiator.LastDiagnostic!.Value.Code.ShouldBe(
            DiagnosticCode.DuplicateResponse);
        negotiator.Accept(in unknown).ShouldBe(QueryMatch.Unknown);
        negotiator.Completed.ShouldBeFalse();
    }

    /// <summary>Verifies one deadline publishes fallback and rejects later mutation.</summary>
    [Fact]
    public void Expire_WhenDeadlineElapses_PublishesOnceAndClassifiesLateReply()
    {
        // Arrange
        var clock = new ManualTimeProvider();
        var limits = QueryLimits.Default with { QueryTimeout = TimeSpan.FromSeconds(1) };
        var options = new NegotiationOptions(
            new Dictionary<string, string?> { ["TERM"] = "xterm-kitty" },
            new CapabilityOverrides { SynchronizedOutput = false },
            limits);
        var negotiator = new ActiveQueryDiscoveryStrategy(options, clock);
        _ = negotiator.TryStart(new ArrayBufferWriter<byte>(), null, null);

        // Act / Assert
        negotiator.Expire().ShouldBeFalse();
        negotiator.Completed.ShouldBeFalse();
        clock.Advance(TimeSpan.FromSeconds(1));
        negotiator.Expire().ShouldBeTrue();
        var published = negotiator.Capabilities;
        published.KittyKeyboard.ShouldBe(
            new Feature(CapabilitySupport.Tentative, Origin.Environment));
        published.SynchronizedOutput.ShouldBe(
            new Feature(CapabilitySupport.Unsupported, Origin.Override));
        negotiator.Expire().ShouldBeFalse();
        negotiator.Capabilities.ShouldBeSameAs(published);

        var late = PrivateMode(1004, state: 1);
        negotiator.Accept(in late).ShouldBe(QueryMatch.Late);
        negotiator.Capabilities.ShouldBeSameAs(published);
    }

    /// <summary>Verifies out-of-order replies publish one query-origin profile.</summary>
    [Fact]
    public void Accept_WhenRepliesArriveOutOfOrder_PublishesCompleteProfile()
    {
        // Arrange
        var limits = QueryLimits.Default with { MaxConcurrentQueries = 8 };
        var negotiator = new ActiveQueryDiscoveryStrategy(
            new NegotiationOptions(new Dictionary<string, string?>(), limits: limits));
        _ = negotiator.TryStart(new ArrayBufferWriter<byte>(), null, null);

        // Act / Assert
        int[] modes = [1016, 1006, 2004, 1004, 2026];

        foreach (var mode in modes)
        {
            var response = PrivateMode(mode, state: 1);
            negotiator.Accept(in response).ShouldBe(QueryMatch.Matched);
        }

        var keyboard = Response("?3"u8, [], (byte) 'u');
        negotiator.Accept(in keyboard).ShouldBe(QueryMatch.Matched);
        var secondary = Response(">41;410;0"u8, [], (byte) 'c');
        negotiator.Accept(in secondary).ShouldBe(QueryMatch.Matched);
        var attributes = Response("?1;2"u8, [], (byte) 'c');
        negotiator.Accept(in attributes).ShouldBe(QueryMatch.Matched);

        negotiator.Completed.ShouldBeTrue();
        var capabilities = negotiator.Capabilities;
        capabilities.SynchronizedOutput.ShouldBe(
            new Feature(CapabilitySupport.Supported, Origin.Query));
        capabilities.FocusReporting.ShouldBe(
            new Feature(CapabilitySupport.Supported, Origin.Query));
        capabilities.BracketedPaste.ShouldBe(
            new Feature(CapabilitySupport.Supported, Origin.Query));
        capabilities.CellMouse.ShouldBe(
            new Feature(CapabilitySupport.Supported, Origin.Query));
        capabilities.PixelMouse.ShouldBe(
            new Feature(CapabilitySupport.Supported, Origin.Query));
        capabilities.KittyKeyboard.ShouldBe(
            new Feature(CapabilitySupport.Supported, Origin.Query));
    }

    /// <summary>
    /// Verifies DECRPM value 3 ("permanently set") publishes support for every probed mode
    /// except synchronized output, whose value encodes an in-progress update rather than a
    /// feature toggle and so is treated as unusable when reported permanently set.
    /// </summary>
    [Fact]
    public void Accept_WhenPrivateModeIsPermanentlySet_SupportsEveryModeExceptSynchronizedOutput()
    {
        // Arrange
        var limits = QueryLimits.Default with { MaxConcurrentQueries = 8 };
        var negotiator = new ActiveQueryDiscoveryStrategy(
            new NegotiationOptions(new Dictionary<string, string?>(), limits: limits));
        _ = negotiator.TryStart(new ArrayBufferWriter<byte>(), null, null);
        int[] modes = [1016, 1006, 2004, 1004, 2026];

        // Act
        foreach (var mode in modes)
        {
            var response = PrivateMode(mode, state: 3);
            negotiator.Accept(in response).ShouldBe(QueryMatch.Matched);
        }

        var keyboard = Response("?3"u8, [], (byte) 'u');
        negotiator.Accept(in keyboard).ShouldBe(QueryMatch.Matched);
        var secondary = Response(">41;410;0"u8, [], (byte) 'c');
        negotiator.Accept(in secondary).ShouldBe(QueryMatch.Matched);
        var attributes = Response("?1;2"u8, [], (byte) 'c');
        negotiator.Accept(in attributes).ShouldBe(QueryMatch.Matched);

        // Assert
        negotiator.Completed.ShouldBeTrue();
        var capabilities = negotiator.Capabilities;
        capabilities.SynchronizedOutput.ShouldBe(
            new Feature(CapabilitySupport.Unsupported, Origin.Query));
        capabilities.FocusReporting.ShouldBe(
            new Feature(CapabilitySupport.Supported, Origin.Query));
        capabilities.BracketedPaste.ShouldBe(
            new Feature(CapabilitySupport.Supported, Origin.Query));
        capabilities.CellMouse.ShouldBe(
            new Feature(CapabilitySupport.Supported, Origin.Query));
        capabilities.PixelMouse.ShouldBe(
            new Feature(CapabilitySupport.Supported, Origin.Query));
    }

    /// <summary>Verifies the configured query limit truncates by fixed priority.</summary>
    /// <param name="capacity">The maximum concurrent query count.</param>
    /// <param name="expected">The exact expected startup bytes.</param>
    [Theory]
    [InlineData(1, "\u001b[c")]
    [InlineData(2, "\u001b[?u\u001b[c")]
    [InlineData(3, "\u001b[?u\u001b[c\u001b[>c")]
    [InlineData(4, "\u001b[?u\u001b[c\u001b[>c\u001b[?2026$p")]
    [InlineData(5, "\u001b[?u\u001b[c\u001b[>c\u001b[?2026$p\u001b[?1004$p")]
    [InlineData(6, "\u001b[?u\u001b[c\u001b[>c\u001b[?2026$p\u001b[?1004$p\u001b[?2004$p")]
    [InlineData(7, "\u001b[?u\u001b[c\u001b[>c\u001b[?2026$p\u001b[?1004$p\u001b[?2004$p\u001b[?1006$p")]
    [InlineData(8, "\u001b[?u\u001b[c\u001b[>c\u001b[?2026$p\u001b[?1004$p\u001b[?2004$p\u001b[?1006$p\u001b[?1016$p")]
    [InlineData(9, "\u001b[?u\u001b[c\u001b[>c\u001b[?2026$p\u001b[?1004$p\u001b[?2004$p\u001b[?1006$p\u001b[?1016$p\u001b[?5522$p")]
    public void TryStart_WhenCapacityVaries_TruncatesByPriority(
        int capacity,
        string expected)
    {
        // Arrange
        var limits = QueryLimits.Default with { MaxConcurrentQueries = capacity };
        var options = new NegotiationOptions(
            new Dictionary<string, string?>(),
            limits: limits);
        var negotiator = new ActiveQueryDiscoveryStrategy(options, new ManualTimeProvider());
        var output = new ArrayBufferWriter<byte>();

        // Act
        _ = negotiator.TryStart(output, null, null);

        // Assert
        Encoding.ASCII.GetString(output.WrittenSpan).ShouldBe(expected);
    }

    /// <summary>Verifies the default batch is safe, exact, and ordered.</summary>
    [Fact]
    public void TryStart_WhenDefaultCapacityIsAvailable_WritesSafeQueriesInOrder()
    {
        // Arrange
        var options = new NegotiationOptions(
            new Dictionary<string, string?>());
        var negotiator = new ActiveQueryDiscoveryStrategy(options, new ManualTimeProvider());
        var output = new ArrayBufferWriter<byte>();

        // Act
        _ = negotiator.TryStart(output, null, null);

        // Assert
        Encoding.ASCII.GetString(output.WrittenSpan).ShouldBe(
            "\u001b[?u\u001b_Gi=31,s=1,v=1,a=q,t=d,f=24;AAAA\u001b\\" +
            "\u001b[c\u001b[>c\u001b[?2026$p\u001b[?1004$p" +
            "\u001b[?2004$p\u001b[?1006$p\u001b[?1016$p\u001b[?5522$p" +
            "\u001b[14t\u001b[16t\u001b[18t" +
            "\u001b]4;0;?\u001b\\\u001b]10;?\u001b\\\u001b]11;?\u001b\\" +
            "\u001b]1337;Capabilities\u001b\\" +
            // The terminating fence: a trailing CSI 6n, so an in-order terminal
            // answers it only after every other reply it is going to send at all.
            "\u001b[6n");
        negotiator.Completed.ShouldBeFalse();
    }

    /// <summary>Verifies database evidence and caller overrides suppress redundant mode probes.</summary>
    [Fact]
    public void TryStart_WhenDescriptionEvidenceIsDefinitive_QueriesOnlyUnknownSafeFamilies()
    {
        // Arrange
        var database = new Feature(CapabilitySupport.Supported, Origin.Database);
        var baseline = TerminalCapabilities.Conservative with
        {
            KittyKeyboard = database,
            SynchronizedOutput = database,
            FocusReporting = database,
            BracketedPaste = database,
            CellMouse = database,
            PixelMouse = database,
            KittyClipboard = database
        };
        var limits = QueryLimits.Default with { MaxConcurrentQueries = 3 };
        var options = new NegotiationOptions(
            new Dictionary<string, string?>(),
            new CapabilityOverrides { PixelMouse = false },
            limits);
        var negotiator = new ActiveQueryDiscoveryStrategy(options, baseline, new ManualTimeProvider());
        var output = new ArrayBufferWriter<byte>();

        // Act
        _ = negotiator.TryStart(output, null, null);

        // Assert
        Encoding.ASCII.GetString(output.WrittenSpan).ShouldBe(
            "\u001b[c\u001b[>c\u001b[14t");
    }

    /// <summary>Verifies local cell/pixel geometry suppresses lower-confidence window probes.</summary>
    [Fact]
    public void TryStart_WhenLocalGeometryIsComplete_DoesNotQueryMetrics()
    {
        // Arrange
        var limits = QueryLimits.Default with { MaxConcurrentQueries = 14 };
        var negotiator = new ActiveQueryDiscoveryStrategy(
            new NegotiationOptions(new Dictionary<string, string?>(), limits: limits),
            new ManualTimeProvider());
        var output = new ArrayBufferWriter<byte>();

        // Act
        _ = negotiator.TryStart(
            output,
            new Size(120, 40),
            new Size(1200, 800));

        // Assert
        var encoded = Encoding.ASCII.GetString(output.WrittenSpan);
        encoded.ShouldNotContain("\u001b[14t");
        encoded.ShouldNotContain("\u001b[16t");
        encoded.ShouldNotContain("\u001b[18t");
    }

    /// <summary>Verifies owned color and metrics replies match out of order without mutating capability colors.</summary>
    [Fact]
    public void Accept_WhenStandardRepliesAreOutOfOrder_PublishesOwnedEvidence()
    {
        // Arrange
        var limits = QueryLimits.Default with { MaxConcurrentQueries = 7 };
        var baseline = TerminalCapabilities.Conservative with
        {
            KittyKeyboard = new Feature(CapabilitySupport.Supported, Origin.Database),
            SynchronizedOutput = new Feature(CapabilitySupport.Supported, Origin.Database),
            FocusReporting = new Feature(CapabilitySupport.Supported, Origin.Database),
            BracketedPaste = new Feature(CapabilitySupport.Supported, Origin.Database),
            CellMouse = new Feature(CapabilitySupport.Supported, Origin.Database),
            PixelMouse = new Feature(CapabilitySupport.Supported, Origin.Database),
            KittyClipboard = new Feature(CapabilitySupport.Supported, Origin.Database)
        };
        var negotiator = new ActiveQueryDiscoveryStrategy(
            new NegotiationOptions(new Dictionary<string, string?>(), limits: limits),
            baseline,
            new ManualTimeProvider());
        _ = negotiator.TryStart(new ArrayBufferWriter<byte>(), new Size(120, 40), pixels: null);
        var background = Palette("11;rgb:0000/1111/ffff"u8);
        var window = Metrics("4;800;1200"u8);
        var cell = Metrics("6;20;10"u8);
        var secondary = Response(">41;410;0"u8, [], (byte) 'c');
        var primary = Response("?1;2"u8, [], (byte) 'c');

        // Act / Assert
        negotiator.Accept(in background).ShouldBe(QueryMatch.Matched);
        negotiator.Accept(in window).ShouldBe(QueryMatch.Matched);
        negotiator.Accept(in cell).ShouldBe(QueryMatch.Matched);
        negotiator.Accept(in secondary).ShouldBe(QueryMatch.Matched);
        negotiator.Accept(in primary).ShouldBe(QueryMatch.Matched);
        negotiator.Completed.ShouldBeFalse();

        var foreground = Palette("10;rgb:ffff/eeee/0000"u8);
        var palette = Palette("4;0;rgb:1111/2222/3333"u8);
        negotiator.Accept(in foreground).ShouldBe(QueryMatch.Matched);
        negotiator.Accept(in palette).ShouldBe(QueryMatch.Matched);

        negotiator.Completed.ShouldBeTrue();
        negotiator.Capabilities.ColorDepth.ShouldBe(baseline.ColorDepth);
        negotiator.Results.BackgroundColor.ShouldBe(background);
        negotiator.Results.WindowPixels.ShouldBe(window);
    }

    /// <summary>Verifies an unsolicited palette index cannot consume the requested index-zero transaction.</summary>
    [Fact]
    public void Accept_WhenOtherPaletteIndexPrecedesZero_KeepsRequestedTransactionActive()
    {
        // Arrange
        var clock = new ManualTimeProvider();
        var negotiator = new ActiveQueryDiscoveryStrategy(
            new NegotiationOptions(new Dictionary<string, string?>()),
            clock);
        _ = negotiator.TryStart(new ArrayBufferWriter<byte>(), null, null);
        var other = Palette("4;15;rgb:aaaa/bbbb/cccc"u8);
        var requested = Palette("4;0;rgb:1111/2222/3333"u8);

        // Act / Assert
        negotiator.Accept(in other).ShouldBe(QueryMatch.Unknown);
        negotiator.Completed.ShouldBeFalse();
        negotiator.Accept(in requested).ShouldBe(QueryMatch.Matched);
        clock.AdvanceTo(negotiator.Deadline);
        negotiator.Expire().ShouldBeTrue();
        negotiator.Results.PaletteColor.ShouldBe(requested);
    }

    /// <summary>Verifies an unsolicited index before the deadline cannot make index zero timely at it.</summary>
    [Fact]
    public void Accept_WhenOtherPaletteIndexPrecedesDeadline_ZeroAtDeadlineIsLateAndAbsent()
    {
        // Arrange
        var clock = new ManualTimeProvider();
        var negotiator = new ActiveQueryDiscoveryStrategy(
            new NegotiationOptions(new Dictionary<string, string?>()),
            clock);
        _ = negotiator.TryStart(new ArrayBufferWriter<byte>(), null, null);
        var other = Palette("4;15;rgb:aaaa/bbbb/cccc"u8);
        var requested = Palette("4;0;rgb:1111/2222/3333"u8);
        clock.AdvanceTo(negotiator.Deadline - TimeSpan.FromTicks(1));

        // Act / Assert
        negotiator.Accept(in other).ShouldBe(QueryMatch.Unknown);
        clock.Advance(TimeSpan.FromTicks(1));
        negotiator.Accept(in requested).ShouldBe(QueryMatch.Late);
        negotiator.Completed.ShouldBeTrue();
        negotiator.Results.PaletteColor.ShouldBeNull();
    }

    /// <summary>Verifies every startup family accepts a response one tick before the shared deadline.</summary>
    [Fact]
    public void Accept_WhenAllFamiliesReplyStrictlyBeforeDeadline_AcceptsAndPublishes()
    {
        // Arrange
        var clock = new ManualTimeProvider { AdvanceOnRead = TimeSpan.FromMilliseconds(1) };
        var negotiator = new ActiveQueryDiscoveryStrategy(
            new NegotiationOptions(new Dictionary<string, string?>()),
            clock);
        _ = negotiator.TryStart(new ArrayBufferWriter<byte>(), null, null);
        negotiator.Deadline.ShouldBe(DateTimeOffset.UnixEpoch + QueryLimits.Default.QueryTimeout);
        clock.AdvanceOnRead = TimeSpan.Zero;
        clock.AdvanceTo(negotiator.Deadline - TimeSpan.FromTicks(1));

        // Act / Assert
        var keyboard = Response("?3"u8, [], (byte) 'u');
        negotiator.Accept(in keyboard).ShouldBe(QueryMatch.Matched);

        foreach (var mode in new[] { 1016, 1006, 2004, 1004, 2026, 5522 })
        {
            var response = PrivateMode(mode, state: 1);
            negotiator.Accept(in response).ShouldBe(QueryMatch.Matched);
        }

        var metricsResponses = new[]
        {
            Metrics("8;40;120"u8),
            Metrics("6;20;10"u8),
            Metrics("4;800;1200"u8)
        };

        foreach (var response in metricsResponses)
        {
            negotiator.Accept(in response).ShouldBe(QueryMatch.Matched);
        }

        var paletteResponses = new[]
        {
            Palette("11;rgb:0000/1111/ffff"u8),
            Palette("10;rgb:ffff/eeee/0000"u8),
            Palette("4;0;rgb:1111/2222/3333"u8)
        };

        foreach (var response in paletteResponses)
        {
            negotiator.Accept(in response).ShouldBe(QueryMatch.Matched);
        }

        var secondary = Response(">41;410;0"u8, [], (byte) 'c');
        negotiator.Accept(in secondary).ShouldBe(QueryMatch.Matched);
        var primary = Response("?1;2"u8, [], (byte) 'c');
        negotiator.Accept(in primary).ShouldBe(QueryMatch.Matched);
        _ = XtermResponses.TryOscItermCapabilities("1337;Capabilities=F"u8, out var capabilities);
        negotiator.Accept(capabilities).ShouldBe(QueryMatch.Matched);

        // The terminating fence: every other family already answered above, so this
        // reply is redundant here, but the batch still writes and tracks it and it must still be
        // accepted like any other reply.
        var cursorPosition = Response("24;80"u8, [], (byte) 'R');
        negotiator.Accept(in cursorPosition).ShouldBe(QueryMatch.Matched);

        negotiator.Completed.ShouldBeTrue();
    }

    /// <summary>Verifies a read winning at or after the deadline atomically rejects the whole batch.</summary>
    /// <param name="ticksAfterDeadline">Ticks at or after the exact deadline.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Accept_WhenReadWinsAtOrAfterDeadline_RejectsEveryFamilyAtomically(
        long ticksAfterDeadline)
    {
        // Arrange
        var clock = new ManualTimeProvider { AdvanceOnRead = TimeSpan.FromMilliseconds(1) };
        var negotiator = new ActiveQueryDiscoveryStrategy(
            new NegotiationOptions(new Dictionary<string, string?>()),
            clock);
        _ = negotiator.TryStart(new ArrayBufferWriter<byte>(), null, null);
        clock.AdvanceOnRead = TimeSpan.Zero;
        clock.AdvanceTo(negotiator.Deadline + TimeSpan.FromTicks(ticksAfterDeadline));

        // Act / Assert: private mode is deliberately first to model read winning Task.WhenAny.
        var privateMode = PrivateMode(2026, state: 1);
        negotiator.Accept(in privateMode).ShouldBe(QueryMatch.Late);
        negotiator.Completed.ShouldBeTrue();

        var numeric = new[]
        {
            Response("?3"u8, [], (byte) 'u'),
            Response(">41;410;0"u8, [], (byte) 'c'),
            Response("?1;2"u8, [], (byte) 'c')
        };

        foreach (var response in numeric)
        {
            negotiator.Accept(in response).ShouldBe(QueryMatch.Late);
        }

        foreach (var response in new[]
                 {
                     Metrics("4;800;1200"u8),
                     Metrics("6;20;10"u8),
                     Metrics("8;40;120"u8)
                 })
        {
            negotiator.Accept(in response).ShouldBe(QueryMatch.Late);
        }

        foreach (var response in new[]
                 {
                     Palette("4;0;rgb:1111/2222/3333"u8),
                     Palette("10;rgb:ffff/eeee/0000"u8),
                     Palette("11;rgb:0000/1111/ffff"u8)
                 })
        {
            negotiator.Accept(in response).ShouldBe(QueryMatch.Late);
        }

        negotiator.Results.PaletteColor.ShouldBeNull();
        negotiator.Results.ForegroundColor.ShouldBeNull();
        negotiator.Results.BackgroundColor.ShouldBeNull();
        negotiator.Results.WindowPixels.ShouldBeNull();
        negotiator.Results.CellPixels.ShouldBeNull();
        negotiator.Results.WindowCells.ShouldBeNull();
        negotiator.Expire().ShouldBeFalse();
    }

    /// <summary>Verifies invalid calls are rejected without ambiguous state.</summary>
    [Fact]
    public void TryStart_WhenStateIsInvalid_ThrowsDeterministically()
    {
        // Arrange
        var negotiator = new ActiveQueryDiscoveryStrategy(
            new NegotiationOptions(new Dictionary<string, string?>()));

        // Act / Assert
        _ = Should.Throw<ArgumentNullException>(() => negotiator.TryStart(null!, null, null));
        negotiator.Started.ShouldBeFalse();
        _ = Should.Throw<InvalidOperationException>(() => _ = negotiator.Capabilities);
        var response = PrivateMode(2026, state: 1);
        _ = Should.Throw<InvalidOperationException>(() => negotiator.Accept(in response));
        _ = Should.Throw<InvalidOperationException>(() => negotiator.Expire());
        var output = new ArrayBufferWriter<byte>();
        _ = negotiator.TryStart(output, null, null);
        _ = Should.Throw<InvalidOperationException>(() => negotiator.TryStart(output, null, null));
    }

    private static XtermCapabilitiesResponse PrivateMode(int mode, int state)
    {
        var parameters = Encoding.ASCII.GetBytes($"?{mode};{state}");
        return Response(parameters, "$"u8, (byte) 'y');
    }

    private static PaletteResponse Palette(ReadOnlySpan<byte> value)
    {
        XtermResponses.TryOsc(value, out var response).ShouldBeTrue();
        return response;
    }

    private static MetricsResponse Metrics(ReadOnlySpan<byte> parameters)
    {
        XtermResponses.TryMetricsCsi(parameters, [], (byte) 't', out var response).ShouldBeTrue();
        return response;
    }

    private static XtermCapabilitiesResponse Response(
        ReadOnlySpan<byte> parameters,
        ReadOnlySpan<byte> intermediates,
        byte final)
    {
        XtermResponses.TryCsi(
            parameters,
            intermediates,
            final,
            out var response).ShouldBeTrue();
        return response;
    }

    #endregion

    #region iTerm2 OSC 1337 Capabilities

    /// <summary>Verifies the stable query-kind ABI appends the iTerm2 family without renumbering prior families.</summary>
    [Fact]
    public void QueryKind_WhenItermCapabilitiesIsAdded_PreservesExistingNumericValues()
    {
        ((int) QueryKind.KittyGraphics).ShouldBe(15);
        ((int) QueryKind.ItermCapabilities).ShouldBe(16);
    }

    /// <summary>Verifies the official Capabilities query is emitted when iTerm2 baseline evidence is unresolved.</summary>
    [Fact]
    public void TryStart_WhenItermImagesIsUnresolved_EmitsCapabilitiesQuery()
    {
        var negotiator = CreateIterm();
        var output = new ArrayBufferWriter<byte>();

        _ = negotiator.TryStart(output, null, null);

        Encoding.ASCII.GetString(output.WrittenSpan).ShouldContain("]1337;Capabilities\u001b\\");
    }

    /// <summary>Verifies the same gate as every other bounded query family: no baseline override skips probing.</summary>
    [Fact]
    public void TryStart_WhenItermImagesOverrideIsDisabled_DoesNotEmitQuery()
    {
        var options = new NegotiationOptions(
            new Dictionary<string, string?>(),
            new CapabilityOverrides { ItermImages = false });
        var negotiator = new ActiveQueryDiscoveryStrategy(options);
        var output = new ArrayBufferWriter<byte>();

        _ = negotiator.TryStart(output, null, null);
        _ = negotiator.Complete();

        Encoding.ASCII.GetString(output.WrittenSpan).ShouldNotContain("Capabilities");
        negotiator.Capabilities.ItermImages.ShouldBe(
            new Feature(CapabilitySupport.Unsupported, Origin.Override));
    }

    /// <summary>Verifies the duplicated F code cannot distinguish FILE from focus reporting and
    /// therefore supplies no positive multipart-image evidence.</summary>
    [Fact]
    public void Accept_WhenReplyContainsAmbiguousFileCode_DoesNotAuthorizeMultipartImages()
    {
        var negotiator = CreateIterm();
        _ = negotiator.TryStart(new ArrayBufferWriter<byte>(), null, null);
        _ = XtermResponses.TryOscItermCapabilities("1337;Capabilities=U9MFH"u8, out var response);

        negotiator.Accept(response).ShouldBe(QueryMatch.Matched);
        _ = negotiator.Complete();

        negotiator.Results.ItermImages.ShouldBeNull();
        negotiator.Capabilities.ItermImages.ShouldBe(
            new Feature(CapabilitySupport.Tentative, Origin.Environment));
    }

    /// <summary>Verifies integer-valued F tokens are not mistaken for the bare Boolean code, while
    /// the specification's ignored non-alphanumeric suffix still leaves its valid prefix intact.</summary>
    [Theory]
    [InlineData("1337;Capabilities=F0", false)]
    [InlineData("1337;Capabilities=F1", false)]
    [InlineData("1337;Capabilities=F!ignored", true)]
    public void TryOscItermCapabilities_WhenFTokenShapeVaries_RecognizesOnlyBareBooleanPrefix(
        string value,
        bool expected)
    {
        XtermResponses.TryOscItermCapabilities(Encoding.ASCII.GetBytes(value), out var response).ShouldBeTrue();

        response.HasFileCode.ShouldBe(expected);
    }

    /// <summary>Verifies a reply without the FILE code proves query-origin absence.</summary>
    [Fact]
    public void Accept_WhenReplyOmitsFileCode_RecordsQueryUnsupported()
    {
        var negotiator = CreateIterm();
        _ = negotiator.TryStart(new ArrayBufferWriter<byte>(), null, null);
        _ = XtermResponses.TryOscItermCapabilities("1337;Capabilities=U9M"u8, out var response);

        negotiator.Accept(response).ShouldBe(QueryMatch.Matched);
        _ = negotiator.Complete();

        negotiator.Results.ItermImages.ShouldBe(false);
        negotiator.Capabilities.ItermImages.ShouldBe(
            new Feature(CapabilitySupport.Unsupported, Origin.Query));
    }

    /// <summary>
    /// Verifies a silent terminal leaves the OSC 1337 probe as absent query evidence rather than
    /// fabricating Unsupported/Query, which would erase the TERM_PROGRAM=iTerm.app environment
    /// hint underneath it and record a bounded query as having supplied evidence it never sent.
    /// The reply has no natural piggyback response to expire against, so this exercises
    /// the ordinary shared-deadline expiry path, not the terminating fence.
    /// </summary>
    [Fact]
    public void Expire_WhenTerminalNeverReplies_LeavesItermImagesAsTheEnvironmentHint()
    {
        var clock = new ManualTimeProvider();
        var limits = QueryLimits.Default with { QueryTimeout = TimeSpan.FromSeconds(1) };
        var negotiator = CreateIterm(limits, clock);
        _ = negotiator.TryStart(new ArrayBufferWriter<byte>(), null, null);

        clock.Advance(TimeSpan.FromSeconds(1));
        negotiator.Expire().ShouldBeTrue();

        negotiator.Results.ItermImages.ShouldBeNull();
        negotiator.Capabilities.ItermImages.ShouldBe(
            new Feature(CapabilitySupport.Tentative, Origin.Environment));

        // Authorization neutrality: absence must not enable or disable the backend, matching the
        // outcome the fabricated Unsupported/Query used to produce.
        GraphicsBackendSelector.Create(negotiator.Capabilities).ShouldBeNull();
    }

    /// <summary>Verifies a late reply after expiration cannot mutate the already-published profile.</summary>
    [Fact]
    public void Accept_WhenReplyArrivesAfterExpiry_IsLateAndDoesNotMutate()
    {
        var clock = new ManualTimeProvider();
        var limits = QueryLimits.Default with { QueryTimeout = TimeSpan.FromSeconds(1) };
        var negotiator = CreateIterm(limits, clock);
        _ = negotiator.TryStart(new ArrayBufferWriter<byte>(), null, null);
        clock.Advance(TimeSpan.FromSeconds(1));
        _ = negotiator.Expire();
        var published = negotiator.Capabilities;
        _ = XtermResponses.TryOscItermCapabilities("1337;Capabilities=F"u8, out var response);

        negotiator.Accept(response).ShouldBe(QueryMatch.Late);

        negotiator.Capabilities.ShouldBeSameAs(published);
    }

    private static ActiveQueryDiscoveryStrategy CreateIterm(
        QueryLimits? limits = null,
        TimeProvider? timeProvider = null) => new(
        new NegotiationOptions(
            new Dictionary<string, string?> { ["TERM_PROGRAM"] = "iTerm.app" },
            limits: limits),
        timeProvider);

    #endregion

    #region Kitty graphics

    /// <summary>Verifies the stable query-kind ABI appends graphics without renumbering prior families.</summary>
    [Fact]
    public void QueryKind_WhenGraphicsIsAdded_PreservesExistingNumericValues()
    {
        ((int) QueryKind.ModifyOtherKeys).ShouldBe(14);
        ((int) QueryKind.KittyGraphics).ShouldBe(15);
    }

    /// <summary>Verifies graphics correlation accepts only canonical nonzero unsigned decimal IDs.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("+1")]
    [InlineData("-1")]
    [InlineData("4294967296")]
    public void TryRegister_WhenGraphicsIdentifierIsInvalid_RejectsIt(string? value)
    {
        var tracker = new QueryTracker();

        _ = Should.Throw<ArgumentException>(() =>
            tracker.TryRegister(QueryKind.KittyGraphics, value, out _));
    }

    /// <summary>Verifies the official direct-data query is emitted before the DA barrier.</summary>
    [Fact]
    public void TryStart_WhenGraphicsIsUnresolved_EmitsOfficialQueryBeforeDa()
    {
        var negotiator = CreateKitty();
        var output = new ArrayBufferWriter<byte>();

        _ = negotiator.TryStart(output, null, null);

        Encoding.ASCII.GetString(output.WrittenSpan).ShouldStartWith(
            "\u001b[?u\u001b_Gi=31,s=1,v=1,a=q,t=d,f=24;AAAA\u001b\\\u001b[c");
    }

    /// <summary>Verifies repeated warmed startup always emits the explicit three-zero query pixel.</summary>
    [Fact]
    public void TryStart_WhenRepeated_EmitsStableOfficialZeroPixelQuery()
    {
        for (var iteration = 0; iteration < 256; iteration++)
        {
            var negotiator = CreateKitty();
            var output = new ArrayBufferWriter<byte>();

            _ = negotiator.TryStart(output, null, null);

            Encoding.ASCII.GetString(output.WrittenSpan).ShouldContain(
                "\u001b_Gi=31,s=1,v=1,a=q,t=d,f=24;AAAA\u001b\\");
        }
    }

    /// <summary>Verifies query capacity below the graphics slot suppresses probing exactly at the boundary.</summary>
    /// <param name="capacity">The bounded concurrent query capacity.</param>
    /// <param name="expected">Whether the graphics query is expected.</param>
    [Theory]
    [InlineData(15, false)]
    [InlineData(16, false)]
    [InlineData(17, true)]
    public void TryStart_WhenQueryCapacityCrossesGraphicsSlot_EmitsExpectedProbe(
        int capacity,
        bool expected)
    {
        var negotiator = CreateKitty(QueryLimits.Default with { MaxConcurrentQueries = capacity });
        var output = new ArrayBufferWriter<byte>();

        _ = negotiator.TryStart(output, null, null);

        Encoding.ASCII.GetString(output.WrittenSpan).Contains("\u001b_G").ShouldBe(expected);
    }

    /// <summary>Verifies any valid correlated graphics reply proves support, including an error reply.</summary>
    [Fact]
    public void Accept_WhenGraphicsReplyIsCorrelated_RecordsQuerySupport()
    {
        var negotiator = CreateKitty();
        _ = negotiator.TryStart(new ArrayBufferWriter<byte>(), null, null);
        var response = KittyResponse.Parse("Gi=31;EINVAL:query inspected"u8);

        negotiator.Accept(response).ShouldBe(QueryMatch.Matched);
        _ = negotiator.Complete();

        negotiator.Results.KittyGraphics.ShouldBe(true);
        negotiator.Capabilities.KittyGraphics.ShouldBe(
            new Feature(CapabilitySupport.Supported, Origin.Query));
    }

    /// <summary>Verifies another renderer identity cannot consume the active query.</summary>
    [Fact]
    public void Accept_WhenGraphicsIdentifierDoesNotMatch_LeavesQueryActive()
    {
        var negotiator = CreateKitty();
        _ = negotiator.TryStart(new ArrayBufferWriter<byte>(), null, null);

        negotiator.Accept(KittyResponse.Parse("Gi=32;OK"u8)).ShouldBe(QueryMatch.Unknown);
        negotiator.Accept(KittyResponse.Parse("Gi=31;OK"u8)).ShouldBe(QueryMatch.Matched);
    }

    /// <summary>Verifies a leading-zero reply cannot alias the active canonical identifier.</summary>
    [Fact]
    public void Accept_WhenGraphicsIdentifierHasLeadingZero_DoesNotConsumeCanonicalQuery()
    {
        var negotiator = CreateKitty();
        _ = negotiator.TryStart(new ArrayBufferWriter<byte>(), null, null);

        negotiator.Accept(KittyResponse.Parse("Gi=031;OK"u8)).ShouldBe(QueryMatch.Unknown);
        negotiator.Accept(KittyResponse.Parse("Gi=31;OK"u8)).ShouldBe(QueryMatch.Matched);
    }

    /// <summary>Verifies primary DA acts as the ordered unsupported barrier for an unanswered query.</summary>
    [Fact]
    public void Accept_WhenDaArrivesBeforeGraphicsReply_RecordsUnsupportedQueryEvidence()
    {
        var negotiator = CreateKitty();
        _ = negotiator.TryStart(new ArrayBufferWriter<byte>(), null, null);
        var attributes = new XtermCapabilitiesResponse(ResponseKind.PrimaryAttributes, [1, 2]);

        negotiator.Accept(in attributes).ShouldBe(QueryMatch.Matched);

        _ = negotiator.Complete();
        negotiator.Results.KittyGraphics.ShouldBe(false);
        negotiator.Accept(KittyResponse.Parse("Gi=31;OK"u8)).ShouldBe(QueryMatch.Late);
    }

    /// <summary>Verifies explicit policy suppresses probing and remains authoritative.</summary>
    [Fact]
    public void TryStart_WhenGraphicsOverrideIsDisabled_DoesNotEmitQuery()
    {
        var options = new NegotiationOptions(
            new Dictionary<string, string?>(),
            new CapabilityOverrides { KittyGraphics = false });
        var negotiator = new ActiveQueryDiscoveryStrategy(options);
        var output = new ArrayBufferWriter<byte>();

        _ = negotiator.TryStart(output, null, null);
        _ = negotiator.Complete();

        Encoding.ASCII.GetString(output.WrittenSpan).ShouldNotContain("\u001b_G");
        negotiator.Capabilities.KittyGraphics.ShouldBe(
            new Feature(CapabilitySupport.Unsupported, Origin.Override));
    }

    private static ActiveQueryDiscoveryStrategy CreateKitty(QueryLimits? limits = null) => new(
        new NegotiationOptions(
            new Dictionary<string, string?> { ["TERM"] = "xterm-kitty" },
            limits: limits));

    #endregion

    #region Sixel (DA1)

    /// <summary>Verifies DA1 parameter 4 proves sixel support at every transport split.</summary>
    [Fact]
    public void Accept_WhenFragmentedDa1ContainsSixel_RecordsQuerySupportAtEverySplit()
    {
        var wire = "\u001b[?62;4c"u8.ToArray();

        for (var split = 0; split <= wire.Length; split++)
        {
            var negotiator = CreateSixel();
            _ = negotiator.TryStart(new ArrayBufferWriter<byte>(), null, null);
            var sink = new RecordingProtocolSink();
            using var router = new ProtocolRouter(sink);
            router.Route(wire.AsSpan(0, split));
            router.Route(wire.AsSpan(split));
            var response = sink.Responses.ShouldHaveSingleItem();

            negotiator.Accept(in response).ShouldBe(QueryMatch.Matched, $"split {split}");
            _ = negotiator.Complete();

            negotiator.Capabilities.Sixel.ShouldBe(
                new Feature(CapabilitySupport.Supported, Origin.Query),
                $"split {split}");
            negotiator.Results.Sixel.ShouldBe(true);
        }
    }

    /// <summary>Verifies a validated DA1 without parameter 4 records unsupported query evidence.</summary>
    [Fact]
    public void Accept_WhenDa1OmitsSixel_RecordsUnsupportedQueryEvidence()
    {
        var negotiator = CreateSixel();
        _ = negotiator.TryStart(new ArrayBufferWriter<byte>(), null, null);
        var response = Response("?62;1;6"u8);

        negotiator.Accept(in response).ShouldBe(QueryMatch.Matched);
        _ = negotiator.Complete();

        negotiator.Capabilities.Sixel.ShouldBe(
            new Feature(CapabilitySupport.Unsupported, Origin.Query));
        negotiator.Results.Sixel.ShouldBe(false);
    }

    /// <summary>Verifies explicit sixel enablement remains authoritative over a negative DA1 reply.</summary>
    [Fact]
    public void Accept_WhenOverrideEnablesSixel_OverrideWinsNegativeDa1()
    {
        var options = new NegotiationOptions(
            new Dictionary<string, string?> { ["TERM"] = "xterm" },
            new CapabilityOverrides { Sixel = true });
        var negotiator = new ActiveQueryDiscoveryStrategy(options);
        _ = negotiator.TryStart(new ArrayBufferWriter<byte>(), null, null);
        var response = Response("?62;1;6"u8);

        negotiator.Accept(in response).ShouldBe(QueryMatch.Matched);
        _ = negotiator.Complete();

        negotiator.Capabilities.Sixel.ShouldBe(
            new Feature(CapabilitySupport.Supported, Origin.Override));
    }

    /// <summary>Verifies explicit sixel disablement remains authoritative over positive DA1.</summary>
    [Fact]
    public void Accept_WhenOverrideDisablesSixel_OverrideWinsPositiveDa1()
    {
        var options = new NegotiationOptions(
            new Dictionary<string, string?> { ["TERM"] = "xterm" },
            new CapabilityOverrides { Sixel = false });
        var negotiator = new ActiveQueryDiscoveryStrategy(options);
        _ = negotiator.TryStart(new ArrayBufferWriter<byte>(), null, null);
        var response = Response("?62;4"u8);

        negotiator.Accept(in response).ShouldBe(QueryMatch.Matched);
        _ = negotiator.Complete();

        negotiator.Capabilities.Sixel.ShouldBe(
            new Feature(CapabilitySupport.Unsupported, Origin.Override));
    }

    private static ActiveQueryDiscoveryStrategy CreateSixel() => new(
        new NegotiationOptions(new Dictionary<string, string?>()));

    private static XtermCapabilitiesResponse Response(ReadOnlySpan<byte> parameters)
    {
        XtermResponses.TryCsi(parameters, [], (byte) 'c', out var response).ShouldBeTrue();
        return response;
    }

    #endregion

    #region xterm-specific DCS refinements (DECRQSS/XTGETTCAP)

    /// <summary>Verifies xterm probes append after the standard safe query priority.</summary>
    [Fact]
    public void TryStart_WhenXtermIsHinted_AppendsExactBoundedDcsQueries()
    {
        var options = new NegotiationOptions(
            new Dictionary<string, string?> { ["TERM"] = "xterm-256color" },
            limits: QueryLimits.Default with { MaxConcurrentQueries = 18 });
        var negotiator = new ActiveQueryDiscoveryStrategy(options, new ManualTimeProvider());
        var destination = new ArrayBufferWriter<byte>();

        _ = negotiator.TryStart(destination, null, null);

        var bytes = Encoding.ASCII.GetString(destination.WrittenSpan);
        bytes.ShouldEndWith("\u001bP+q524742\u001b\\\u001bP$q>4m\u001b\\");
    }

    /// <summary>Verifies DCS replies match once and retain owned semantic evidence.</summary>
    [Fact]
    public void Accept_WhenDcsRepliesRepeat_ClassifiesDuplicatesWithoutRawProgramMutation()
    {
        var options = new NegotiationOptions(
            new Dictionary<string, string?> { ["TERM"] = "xterm" },
            limits: QueryLimits.Default with { MaxConcurrentQueries = 18 });
        var negotiator = new ActiveQueryDiscoveryStrategy(options, new ManualTimeProvider());
        _ = negotiator.TryStart(new ArrayBufferWriter<byte>(), null, null);
        XtermDecrqss.TryParse("1"u8, "$"u8, (byte) 'r', ">4;2m"u8, out var status)
            .ShouldBeTrue();
        XtermGetCap.TryParse(
            "1"u8,
            "+"u8,
            (byte) 'r',
            "524742=3234"u8,
            QueryLimits.Default,
            out var capability).ShouldBeTrue();

        negotiator.Accept(in status).ShouldBe(QueryMatch.Matched);
        negotiator.Accept(in status).ShouldBe(QueryMatch.Duplicate);
        negotiator.Accept(capability).ShouldBe(QueryMatch.Matched);
        negotiator.Accept(capability).ShouldBe(QueryMatch.Duplicate);
    }

    /// <summary>Verifies a valid response for another selector cannot consume requested work.</summary>
    [Fact]
    public void Accept_WhenDcsSelectorDoesNotMatch_KeepsRequestedFamilyActive()
    {
        var options = new NegotiationOptions(
            new Dictionary<string, string?> { ["TERM"] = "xterm" },
            limits: QueryLimits.Default with { MaxConcurrentQueries = 18 });
        var negotiator = new ActiveQueryDiscoveryStrategy(options, new ManualTimeProvider());
        _ = negotiator.TryStart(new ArrayBufferWriter<byte>(), null, null);
        XtermDecrqss.TryParse("1"u8, "$"u8, (byte) 'r', "0m"u8, out var otherStatus)
            .ShouldBeTrue();
        XtermDecrqss.TryParse("1"u8, "$"u8, (byte) 'r', ">4;2m"u8, out var requestedStatus)
            .ShouldBeTrue();
        XtermGetCap.TryParse(
            "1"u8,
            "+"u8,
            (byte) 'r',
            "6B63757531=1B5B41"u8,
            QueryLimits.Default,
            out var otherCapability).ShouldBeTrue();
        XtermGetCap.TryParse(
            "1"u8,
            "+"u8,
            (byte) 'r',
            "524742=3234"u8,
            QueryLimits.Default,
            out var requestedCapability).ShouldBeTrue();

        negotiator.Accept(in otherStatus).ShouldBe(QueryMatch.Unknown);
        negotiator.Accept(in requestedStatus).ShouldBe(QueryMatch.Matched);
        negotiator.Accept(otherCapability).ShouldBe(QueryMatch.Unknown);
        negotiator.Accept(requestedCapability).ShouldBe(QueryMatch.Matched);
    }

    /// <summary>Verifies validated query evidence refines semantics while overrides remain final.</summary>
    [Fact]
    public void Detect_WhenXtermEvidenceIsValidated_RefinesOnlySemanticValues()
    {
        XtermGetCap.TryParse(
            "1"u8,
            "+"u8,
            (byte) 'r',
            "524742=3234"u8,
            QueryLimits.Default,
            out var capability).ShouldBeTrue();
        var queries = new QueryResults
        {
            XtermKeyboard = true,
            CapabilityString = capability
        };
        var environment = new Dictionary<string, string?> { ["TERM"] = "xterm" };

        var detected = CapabilityDetector.Detect(environment, queries);
        var overridden = CapabilityDetector.Detect(
            environment,
            queries,
            new CapabilityOverrides { XtermKeyboard = false, ColorDepth = ColorDepth.Basic16 });

        detected.XtermKeyboard.ShouldBe(
            new Feature(CapabilitySupport.Supported, Origin.Query));
        detected.ColorDepth.ShouldBe(ColorDepth.TrueColor);
        detected.ColorOrigin.ShouldBe(Origin.Query);
        overridden.XtermKeyboard.ShouldBe(
            new Feature(CapabilitySupport.Unsupported, Origin.Override));
        overridden.ColorDepth.ShouldBe(ColorDepth.Basic16);
        overridden.ColorOrigin.ShouldBe(Origin.Override);
    }

    /// <summary>Verifies query evidence supersedes default and environment-only color heuristics.</summary>
    /// <param name="term">The optional terminal name hint.</param>
    /// <param name="colorTerm">The optional color environment hint.</param>
    /// <param name="heuristicDepth">The expected pre-query heuristic depth.</param>
    /// <param name="heuristicOrigin">The expected pre-query origin.</param>
    [Theory]
    [InlineData("xterm", null, ColorDepth.Basic16, Origin.Default)]
    [InlineData("xterm-256color", null, ColorDepth.Indexed256, Origin.Environment)]
    [InlineData("xterm", "truecolor", ColorDepth.TrueColor, Origin.Environment)]
    public void Detect_WhenRgbQueryIsDirect_OverridesOnlyHeuristicColorEvidence(
        string term,
        string? colorTerm,
        ColorDepth heuristicDepth,
        Origin heuristicOrigin)
    {
        var environment = new Dictionary<string, string?> { ["TERM"] = term };

        if (colorTerm is not null)
        {
            environment["COLORTERM"] = colorTerm;
        }

        var heuristic = CapabilityDetector.Detect(environment);
        var capability = Capability("524742=3234"u8);
        var detected = CapabilityDetector.Detect(
            environment,
            new QueryResults { CapabilityString = capability });

        heuristic.ColorDepth.ShouldBe(heuristicDepth);
        heuristic.ColorOrigin.ShouldBe(heuristicOrigin);
        detected.ColorDepth.ShouldBe(ColorDepth.TrueColor);
        detected.ColorOrigin.ShouldBe(Origin.Query);
    }

    /// <summary>Verifies NO_COLOR keeps the profile monochrome even when a live terminal answers
    /// the direct-color capability probe — the RGB refinement step must not upgrade evidence
    /// NO_COLOR already forced, or the environment-only fix would be silently defeated by a
    /// terminal that also happens to prove truecolor support.</summary>
    [Fact]
    public void Detect_WhenNoColorAndRgbQueryBothPresent_NoColorWins()
    {
        var environment = new Dictionary<string, string?>
        {
            ["TERM"] = "xterm",
            ["NO_COLOR"] = "1"
        };
        var capability = Capability("524742=3234"u8);

        var detected = CapabilityDetector.Detect(
            environment,
            new QueryResults { CapabilityString = capability });

        detected.ColorDepth.ShouldBe(ColorDepth.Monochrome);
        detected.ColorOrigin.ShouldBe(Origin.Environment);
    }

    /// <summary>
    /// Verifies an RGB reply expressed as bits-per-channel (e.g. ncurses' RGB#8, used by
    /// xterm-direct/tmux-direct/kitty/ghostty) is recognized as 24-bit TrueColor, the same as
    /// the enumerated form RGB=24.
    /// </summary>
    [Fact]
    public void Detect_WhenRgbQueryReportsBitsPerChannel_RecognizesTrueColor()
    {
        var environment = new Dictionary<string, string?> { ["TERM"] = "xterm" };
        var capability = Capability("524742=38"u8);

        var detected = CapabilityDetector.Detect(
            environment,
            new QueryResults { CapabilityString = capability });

        detected.ColorDepth.ShouldBe(ColorDepth.TrueColor);
        detected.ColorOrigin.ShouldBe(Origin.Query);
    }

    /// <summary>Verifies query color refinement cannot replace authoritative evidence or use unrelated values.</summary>
    [Fact]
    public void Detect_WhenColorEvidenceIsAuthoritativeOrNonDirect_PreservesExistingColor()
    {
        var environment = new Dictionary<string, string?> { ["TERM"] = "xterm-256color" };
        var negative = CapabilityDetector.Detect(
            environment,
            new QueryResults { CapabilityString = Capability("524742=30"u8) });
        var unrelated = CapabilityDetector.Detect(
            environment,
            new QueryResults { CapabilityString = Capability("6B63757531=1B5B41"u8) });
        var database = TerminalCapabilities.Conservative with
        {
            ColorDepth = ColorDepth.Indexed256,
            ColorOrigin = Origin.Database
        };
        var authoritative = CapabilityDetector.Detect(
            database,
            environment,
            new QueryResults { CapabilityString = Capability("524742=3234"u8) });
        var queryBaseline = CapabilityDetector.Detect(
            TerminalCapabilities.Conservative with
            {
                ColorDepth = ColorDepth.Indexed256,
                ColorOrigin = Origin.Query
            },
            environment,
            new QueryResults { CapabilityString = Capability("524742=3234"u8) });
        var overrideBaseline = CapabilityDetector.Detect(
            TerminalCapabilities.Conservative with
            {
                ColorDepth = ColorDepth.Basic16,
                ColorOrigin = Origin.Override
            },
            environment,
            new QueryResults { CapabilityString = Capability("524742=3234"u8) });

        negative.ColorDepth.ShouldBe(ColorDepth.Indexed256);
        negative.ColorOrigin.ShouldBe(Origin.Environment);
        unrelated.ColorDepth.ShouldBe(ColorDepth.Indexed256);
        unrelated.ColorOrigin.ShouldBe(Origin.Environment);
        authoritative.ColorDepth.ShouldBe(ColorDepth.Indexed256);
        authoritative.ColorOrigin.ShouldBe(Origin.Database);
        queryBaseline.ColorDepth.ShouldBe(ColorDepth.Indexed256);
        queryBaseline.ColorOrigin.ShouldBe(Origin.Query);
        overrideBaseline.ColorDepth.ShouldBe(ColorDepth.Basic16);
        overrideBaseline.ColorOrigin.ShouldBe(Origin.Override);
    }

    /// <summary>Verifies an explicit color override suppresses RGB bytes and tracker capacity.</summary>
    /// <param name="capacity">The bounded startup capacity.</param>
    /// <param name="expectsStatus">Whether the next-priority status query fits.</param>
    [Theory]
    [InlineData(16, false)]
    [InlineData(17, true)]
    [InlineData(18, true)]
    public void TryStart_WhenColorDepthIsExplicit_DoesNotRegisterRgbQuery(
        int capacity,
        bool expectsStatus)
    {
        var options = new NegotiationOptions(
            new Dictionary<string, string?> { ["TERM"] = "xterm-256color" },
            new CapabilityOverrides { ColorDepth = ColorDepth.Basic16 },
            QueryLimits.Default with { MaxConcurrentQueries = capacity });
        var negotiator = new ActiveQueryDiscoveryStrategy(options, new ManualTimeProvider());
        var destination = new ArrayBufferWriter<byte>();

        _ = negotiator.TryStart(destination, null, null);

        var bytes = Encoding.ASCII.GetString(destination.WrittenSpan);
        bytes.ShouldNotContain("\u001bP+q524742\u001b\\");
        bytes.Contains("\u001bP$q>4m\u001b\\", StringComparison.Ordinal)
            .ShouldBe(expectsStatus);
    }

    /// <summary>Verifies NO_COLOR suppresses the RGB probe the same way an explicit color
    /// override does, preserving its capacity slot for the next-priority query.</summary>
    /// <param name="capacity">The bounded startup capacity.</param>
    /// <param name="expectsStatus">Whether the next-priority status query fits.</param>
    [Theory]
    [InlineData(16, false)]
    [InlineData(17, true)]
    [InlineData(18, true)]
    public void TryStart_WhenNoColorIsPresent_DoesNotRegisterRgbQuery(
        int capacity,
        bool expectsStatus)
    {
        var options = new NegotiationOptions(
            new Dictionary<string, string?> { ["TERM"] = "xterm-256color", ["NO_COLOR"] = "1" },
            limits: QueryLimits.Default with { MaxConcurrentQueries = capacity });
        var negotiator = new ActiveQueryDiscoveryStrategy(options, new ManualTimeProvider());
        var destination = new ArrayBufferWriter<byte>();

        _ = negotiator.TryStart(destination, null, null);

        var bytes = Encoding.ASCII.GetString(destination.WrittenSpan);
        bytes.ShouldNotContain("\u001bP+q524742\u001b\\");
        bytes.Contains("\u001bP$q>4m\u001b\\", StringComparison.Ordinal)
            .ShouldBe(expectsStatus);
    }

    /// <summary>Verifies the final slot normally belongs to RGB before the following status query.</summary>
    [Theory]
    [InlineData(17, true, false)]
    [InlineData(18, true, true)]
    public void TryStart_WhenColorDepthIsNotExplicit_PreservesRgbThenStatusPriority(
        int capacity,
        bool expectsRgb,
        bool expectsStatus)
    {
        var options = new NegotiationOptions(
            new Dictionary<string, string?> { ["TERM"] = "xterm-256color" },
            limits: QueryLimits.Default with { MaxConcurrentQueries = capacity });
        var negotiator = new ActiveQueryDiscoveryStrategy(options, new ManualTimeProvider());
        var destination = new ArrayBufferWriter<byte>();

        _ = negotiator.TryStart(destination, null, null);

        var bytes = Encoding.ASCII.GetString(destination.WrittenSpan);
        bytes.Contains("\u001bP+q524742\u001b\\", StringComparison.Ordinal)
            .ShouldBe(expectsRgb);
        bytes.Contains("\u001bP$q>4m\u001b\\", StringComparison.Ordinal)
            .ShouldBe(expectsStatus);
    }

    /// <summary>Verifies suppressed RGB work neither consumes capacity nor delays publication.</summary>
    [Fact]
    public void Accept_WhenColorDepthIsExplicit_CompletesWithoutRgbReply()
    {
        var database = new Feature(CapabilitySupport.Supported, Origin.Database);
        var baseline = TerminalCapabilities.Conservative with
        {
            KittyKeyboard = database,
            SynchronizedOutput = database,
            FocusReporting = database,
            BracketedPaste = database,
            CellMouse = database,
            PixelMouse = database,
            KittyClipboard = database,
            ItermImages = database
        };
        var options = new NegotiationOptions(
            new Dictionary<string, string?> { ["TERM"] = "xterm" },
            new CapabilityOverrides { ColorDepth = ColorDepth.Basic16 },
            QueryLimits.Default with { MaxConcurrentQueries = 6 });
        var negotiator = new ActiveQueryDiscoveryStrategy(options, baseline, new ManualTimeProvider());
        var destination = new ArrayBufferWriter<byte>();
        _ = negotiator.TryStart(destination, new Size(80, 24), new Size(800, 480));
        XtermDecrqss.TryParse("1"u8, "$"u8, (byte) 'r', ">4;2m"u8, out var status)
            .ShouldBeTrue();

        negotiator.Accept(in status).ShouldBe(QueryMatch.Matched);

        foreach (var palette in new[]
                 {
                     Palette("4;0;rgb:0000/0000/0000"u8),
                     Palette("10;rgb:ffff/ffff/ffff"u8),
                     Palette("11;rgb:0000/0000/0000"u8)
                 })
        {
            negotiator.Accept(in palette).ShouldBe(QueryMatch.Matched);
        }

        var secondary = Numeric(">41;410;0"u8, (byte) 'c');
        negotiator.Accept(in secondary).ShouldBe(QueryMatch.Matched);
        var primary = Numeric("?1;2"u8, (byte) 'c');
        negotiator.Accept(in primary).ShouldBe(QueryMatch.Matched);

        negotiator.Completed.ShouldBeTrue();
        negotiator.Results.CapabilityString.ShouldBeNull();
        negotiator.Capabilities.ColorDepth.ShouldBe(ColorDepth.Basic16);
        negotiator.Capabilities.ColorOrigin.ShouldBe(Origin.Override);
    }

    private static CapabilityResponse Capability(ReadOnlySpan<byte> payload)
    {
        XtermGetCap.TryParse(
            "1"u8,
            "+"u8,
            (byte) 'r',
            payload,
            QueryLimits.Default,
            out var capability).ShouldBeTrue();
        return capability;
    }

    private static XtermCapabilitiesResponse Numeric(ReadOnlySpan<byte> parameters, byte final)
    {
        XtermResponses.TryCsi(parameters, [], final, out var response).ShouldBeTrue();
        return response;
    }

    #endregion

    #region Multiplexer-aware probe suppression

    /// <summary>Verifies a detected but unroutable tmux suppresses the APC graphics probe.</summary>
    [Fact]
    public void TryStart_WhenMultiplexerIsDetectedWithoutPassthrough_OmitsApcProbe()
    {
        var written = Start(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["TERM"] = "xterm-256color",
            ["TMUX"] = "/tmp/tmux-1000/default,1,0"
        });

        written.ShouldNotContain("\u001b_G");
        written.ShouldContain("\u001b[c");
    }

    /// <summary>Verifies the same probe is still emitted when no multiplexer is present.</summary>
    [Fact]
    public void TryStart_WhenNoMultiplexerIsDetected_EmitsApcProbe()
    {
        var written = Start(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["TERM"] = "xterm-256color"
        });

        written.ShouldContain("\u001b_G");
    }

    /// <summary>
    /// Verifies a lowercase case-insensitive environment suppresses the probe too, so the
    /// canonicalized snapshot and multiplexer detection stay consistent.
    /// </summary>
    [Fact]
    public void TryStart_WhenMultiplexerIsDetectedFromLowercaseKeys_OmitsApcProbe()
    {
        var written = Start(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["term"] = "xterm-256color",
            ["tmux"] = "/tmp/tmux-1000/default,1,0"
        });

        written.ShouldNotContain("\u001b_G");
    }

    /// <summary>
    /// Verifies a detected but unroutable tmux also suppresses the OSC 1337 iTerm2 probe and the
    /// Kitty clipboard mode probe: environment evidence already narrows both to Unsupported, so
    /// writing them would only spend a round trip tmux cannot carry.
    /// </summary>
    [Fact]
    public void TryStart_WhenMultiplexerIsDetectedWithoutPassthrough_OmitsItermAndClipboardProbes()
    {
        var written = Start(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["TERM"] = "xterm-256color",
            ["TMUX"] = "/tmp/tmux-1000/default,1,0"
        });

        written.ShouldNotContain("1337;Capabilities");
        written.ShouldNotContain("?5522$p");
        written.ShouldContain("[c");
    }

    /// <summary>
    /// Verifies SSH suppresses only the Kitty clipboard mode probe. OSC 1337 is unaffected by SSH
    /// in <c>EnvironmentEvidenceAdapter</c>, so it must still be emitted.
    /// </summary>
    [Fact]
    public void TryStart_WhenSshIsDetected_OmitsOnlyClipboardProbe()
    {
        var written = Start(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["TERM"] = "xterm-256color",
            ["SSH_CONNECTION"] = "10.0.0.1 22 10.0.0.2 22"
        });

        written.ShouldNotContain("?5522$p");
        written.ShouldContain("1337;Capabilities");
    }

    /// <summary>
    /// Verifies neither probe is suppressed when no multiplexer or SSH is detected, the negative
    /// control for the two tests above.
    /// </summary>
    [Fact]
    public void TryStart_WhenNoMultiplexerOrSshIsDetected_EmitsBothProbes()
    {
        var written = Start(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["TERM"] = "xterm-256color"
        });

        written.ShouldContain("1337;Capabilities");
        written.ShouldContain("?5522$p");
    }

    /// <summary>
    /// Verifies the routed-outer-profile carve-out: when an explicit outer route can carry
    /// capability queries, an inner multiplexer's environment variables must not narrow or
    /// suppress probes, because publication deliberately ignores that environment for the same
    /// reason.
    /// </summary>
    [Fact]
    public void TryStart_WhenRouteCanCarryCapabilityQueries_IgnoresInnerMultiplexerEnvironment()
    {
        var environment = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["TERM"] = "xterm-256color",
            ["TMUX"] = "/tmp/tmux-1000/default,1,0"
        };
        var options = new NegotiationOptions(environment);
        var strategy = new ActiveQueryDiscoveryStrategy(
            options,
            TerminalCapabilities.Conservative,
            TimeProvider.System);
        var destination = new ArrayBufferWriter<byte>();
        var policy = new MultiplexingPolicy(
            [MultiplexerKind.Tmux],
            TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative),
            PassthroughMode.All,
            paneVisible: true,
            MultiplexingOperation.CapabilityQueries);
        var route = new MultiplexerRoute(policy);

        var started = strategy.TryStart(destination, cells: null, pixels: null, route);

        started.ShouldBeTrue();
        var written = Encoding.ASCII.GetString(destination.WrittenSpan);
        written.ShouldContain("1337;Capabilities");
        written.ShouldContain("?5522$p");
    }

    /// <summary>
    /// Verifies the routed-outer-profile carve-out extends to the xterm-proprietary DCS probe
    /// gates: an approved route's own outer terminal identity decides whether the XTGETTCAP RGB
    /// and DECRQSS modifyOtherKeys probes are written, not the inner pane's TERM. tmux's own
    /// defaults (tmux-256color, screen-256color) must not suppress these probes when the outer
    /// terminal is explicitly known to be xterm.
    /// </summary>
    [Fact]
    public void TryStart_WhenRouteHasExplicitXtermOuterProfile_WritesBothDcsProbesRegardlessOfInnerTerm()
    {
        var environment = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["TERM"] = "tmux-256color"
        };
        var options = new NegotiationOptions(environment);
        var strategy = new ActiveQueryDiscoveryStrategy(
            options,
            TerminalCapabilities.Conservative,
            TimeProvider.System);
        var destination = new ArrayBufferWriter<byte>();
        var outerProfile = new TerminalProfile(
            new Description("xterm-256color", DescriptionOrigin.BuiltIn, Suitability.Usable),
            TerminalCapabilities.Conservative);
        var policy = new MultiplexingPolicy(
            [MultiplexerKind.Tmux],
            outerProfile,
            PassthroughMode.All,
            paneVisible: true,
            MultiplexingOperation.CapabilityQueries);
        var route = new MultiplexerRoute(policy);

        var started = strategy.TryStart(destination, cells: null, pixels: null, route);

        started.ShouldBeTrue();
        var written = Encoding.ASCII.GetString(destination.WrittenSpan);
        written.ShouldContain("+q524742");
        written.ShouldContain("$q>4m");
    }

    /// <summary>
    /// Verifies the negative of the test above: a declared non-xterm outer profile (here, plain
    /// ANSI) still withholds the xterm-proprietary probes even though the route is approved,
    /// because the outer terminal genuinely is not xterm.
    /// </summary>
    [Fact]
    public void TryStart_WhenRouteHasExplicitNonXtermOuterProfile_WithholdsBothDcsProbes()
    {
        var environment = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["TERM"] = "xterm-256color"
        };
        var options = new NegotiationOptions(environment);
        var strategy = new ActiveQueryDiscoveryStrategy(
            options,
            TerminalCapabilities.Conservative,
            TimeProvider.System);
        var destination = new ArrayBufferWriter<byte>();
        var policy = new MultiplexingPolicy(
            [MultiplexerKind.Tmux],
            TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative),
            PassthroughMode.All,
            paneVisible: true,
            MultiplexingOperation.CapabilityQueries);
        var route = new MultiplexerRoute(policy);

        var started = strategy.TryStart(destination, cells: null, pixels: null, route);

        started.ShouldBeTrue();
        var written = Encoding.ASCII.GetString(destination.WrittenSpan);
        written.ShouldNotContain("+q524742");
        written.ShouldNotContain("$q>4m");
    }

    /// <summary>
    /// Verifies the built-in Windows VT connection carve-out: native Windows sessions almost
    /// never set TERM (not under classic conhost, and not under modern Windows Terminal either,
    /// which sets WT_SESSION instead), so the connection's own "windows-vt" description name -
    /// selected only after ENABLE_VIRTUAL_TERMINAL_PROCESSING is confirmed active - is accepted
    /// as an xterm-like hint for the XTGETTCAP RGB and DECRQSS modifyOtherKeys probes instead of
    /// permanently withholding them.
    /// </summary>
    [Fact]
    public void TryStart_WhenDescribedTerminalIsWindowsVtWithNoTerm_WritesBothDcsProbes()
    {
        var options = new NegotiationOptions(new Dictionary<string, string?>(StringComparer.Ordinal));
        var strategy = new ActiveQueryDiscoveryStrategy(
            options,
            TerminalCapabilities.Conservative,
            TimeProvider.System);
        var destination = new ArrayBufferWriter<byte>();

        var started = strategy.TryStart(
            destination,
            cells: null,
            pixels: null,
            route: null,
            describedTerminalName: "windows-vt");

        started.ShouldBeTrue();
        var written = Encoding.ASCII.GetString(destination.WrittenSpan);
        written.ShouldContain("+q524742");
        written.ShouldContain("$q>4m");
    }

    /// <summary>
    /// Verifies the negative of the test above: a described terminal name that is not the
    /// built-in Windows VT connection (and no TERM) still withholds both xterm-proprietary
    /// probes, so the carve-out is exact rather than treating every fallback description as
    /// xterm-compatible.
    /// </summary>
    [Fact]
    public void TryStart_WhenDescribedTerminalIsNotWindowsVtWithNoTerm_WithholdsBothDcsProbes()
    {
        var options = new NegotiationOptions(new Dictionary<string, string?>(StringComparer.Ordinal));
        var strategy = new ActiveQueryDiscoveryStrategy(
            options,
            TerminalCapabilities.Conservative,
            TimeProvider.System);
        var destination = new ArrayBufferWriter<byte>();

        var started = strategy.TryStart(
            destination,
            cells: null,
            pixels: null,
            route: null,
            describedTerminalName: "ansi");

        started.ShouldBeTrue();
        var written = Encoding.ASCII.GetString(destination.WrittenSpan);
        written.ShouldNotContain("+q524742");
        written.ShouldNotContain("$q>4m");
    }

    /// <summary>
    /// Verifies an explicit TERM still wins over the described-terminal fallback: a Windows
    /// connection whose caller-supplied environment happens to set a non-xterm TERM is not
    /// upgraded by the "windows-vt" carve-out, since a present TERM is a stronger, more specific
    /// signal than the generic built-in connection name.
    /// </summary>
    [Fact]
    public void TryStart_WhenTermIsPresentAndNonXterm_WindowsVtCarveOutDoesNotApply()
    {
        var environment = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["TERM"] = "dumb"
        };
        var options = new NegotiationOptions(environment);
        var strategy = new ActiveQueryDiscoveryStrategy(
            options,
            TerminalCapabilities.Conservative,
            TimeProvider.System);
        var destination = new ArrayBufferWriter<byte>();

        var started = strategy.TryStart(
            destination,
            cells: null,
            pixels: null,
            route: null,
            describedTerminalName: "windows-vt");

        started.ShouldBeTrue();
        var written = Encoding.ASCII.GetString(destination.WrittenSpan);
        written.ShouldNotContain("+q524742");
        written.ShouldNotContain("$q>4m");
    }

    /// <summary>
    /// Verifies a suppressed probe still publishes Unsupported/Origin.Environment rather than
    /// sitting at Unknown, so callers see the same conclusion the probe would have proven, sourced
    /// honestly.
    /// </summary>
    [Fact]
    public void TryStart_WhenClipboardProbeIsSuppressed_PublishesUnsupportedEnvironmentEvidence()
    {
        var environment = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["TERM"] = "xterm-256color",
            ["TMUX"] = "/tmp/tmux-1000/default,1,0"
        };
        var options = new NegotiationOptions(environment);
        var strategy = new ActiveQueryDiscoveryStrategy(
            options,
            TerminalCapabilities.Conservative,
            TimeProvider.System);
        var destination = new ArrayBufferWriter<byte>();

        _ = strategy.TryStart(destination, cells: null, pixels: null, route: null);
        _ = strategy.Complete();

        strategy.Results.KittyClipboard.ShouldBeNull();
        strategy.Capabilities.KittyClipboard.ShouldBe(
            new Feature(CapabilitySupport.Unsupported, Origin.Environment));
    }

    private static string Start(Dictionary<string, string?> environment)
    {
        var options = new NegotiationOptions(environment);
        var strategy = new ActiveQueryDiscoveryStrategy(
            options,
            TerminalCapabilities.Conservative,
            TimeProvider.System);
        var destination = new ArrayBufferWriter<byte>();

        _ = strategy.TryStart(destination, cells: null, pixels: null, route: null);

        return Encoding.ASCII.GetString(destination.WrittenSpan);
    }

    #endregion
}
