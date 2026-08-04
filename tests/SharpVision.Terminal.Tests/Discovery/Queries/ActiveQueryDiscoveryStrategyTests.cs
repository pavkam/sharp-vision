// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Discovery.Queries;

using Capabilities;

using SharpVision.Terminal.Capabilities;
using SharpVision.Terminal.Discovery.Queries;

/// <summary>Verifies bounded startup query encoding and profile publication.</summary>
public sealed class ActiveQueryDiscoveryStrategyTests
{
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
        negotiator.IsComplete.ShouldBeTrue();
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
        negotiator.IsComplete.ShouldBeFalse();
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
        negotiator.IsComplete.ShouldBeFalse();
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

        negotiator.IsComplete.ShouldBeTrue();
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
        negotiator.IsComplete.ShouldBeTrue();
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
            // The terminating fence (see #247): a trailing CSI 6n, so an in-order terminal
            // answers it only after every other reply it is going to send at all.
            "\u001b[6n");
        negotiator.IsComplete.ShouldBeFalse();
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
        negotiator.IsComplete.ShouldBeFalse();

        var foreground = Palette("10;rgb:ffff/eeee/0000"u8);
        var palette = Palette("4;0;rgb:1111/2222/3333"u8);
        negotiator.Accept(in foreground).ShouldBe(QueryMatch.Matched);
        negotiator.Accept(in palette).ShouldBe(QueryMatch.Matched);

        negotiator.IsComplete.ShouldBeTrue();
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
        negotiator.IsComplete.ShouldBeFalse();
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
        negotiator.IsComplete.ShouldBeTrue();
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

        // The terminating fence (see #247): every other family already answered above, so this
        // reply is redundant here, but the batch still writes and tracks it and it must still be
        // accepted like any other reply.
        var cursorPosition = Response("24;80"u8, [], (byte) 'R');
        negotiator.Accept(in cursorPosition).ShouldBe(QueryMatch.Matched);

        negotiator.IsComplete.ShouldBeTrue();
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
        negotiator.IsComplete.ShouldBeTrue();

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
        negotiator.IsStarted.ShouldBeFalse();
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
}
