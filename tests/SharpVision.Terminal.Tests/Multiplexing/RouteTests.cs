// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Multiplexing;

using Capabilities;

using SharpVision.Terminal.Capabilities;
using SharpVision.Terminal.Multiplexing;

using MultiplexingOperation = Terminal.Multiplexing.Operation;

/// <summary>Verifies bounded typed routing through tmux and GNU screen.</summary>
public sealed class RouteTests
{
    /// <summary>Verifies environment evidence identifies only the nearest multiplexer layer.</summary>
    [Theory]
    [InlineData(null, null, (int) MultiplexerKind.None)]
    [InlineData("xterm-256color", "/tmp/tmux,1,0", (int) MultiplexerKind.Tmux)]
    [InlineData("tmux-256color", null, (int) MultiplexerKind.Tmux)]
    [InlineData("screen-256color", null, (int) MultiplexerKind.Screen)]
    public void Detect_WhenEnvironmentVaries_IdentifiesNearestLayer(
        string? term,
        string? tmux,
        int expectedValue)
    {
        var environment = new Dictionary<string, string?>();

        if (term is not null)
        {
            environment["TERM"] = term;
        }

        if (tmux is not null)
        {
            environment["TMUX"] = tmux;
        }

        var policy = Policy.Detect(environment);

        policy.Kind.ShouldBe((MultiplexerKind) expectedValue);
        policy.OuterProfile.ShouldBeNull();
        policy.IsActive.ShouldBeFalse();
    }

    /// <summary>Verifies a detected multiplexer name never invents outer-terminal evidence.</summary>
    [Fact]
    public void Detect_WhenTermNamesTmux_KeepsOuterProfileExplicit()
    {
        var policy = Policy.Detect(new Dictionary<string, string?>
        {
            ["TERM"] = "tmux-256color",
            ["TERM_PROGRAM"] = "iTerm.app"
        });

        policy.Kind.ShouldBe(MultiplexerKind.Tmux);
        policy.OuterProfile.ShouldBeNull();
        policy.ApprovedOperations.ShouldBe(MultiplexingOperation.None);
    }

    /// <summary>Verifies only an explicit visible policy and outer profile enable query routing.</summary>
    [Theory]
    [InlineData((int) PassthroughMode.Disabled, true, false)]
    [InlineData((int) PassthroughMode.Visible, false, false)]
    [InlineData((int) PassthroughMode.Visible, true, true)]
    [InlineData((int) PassthroughMode.All, false, true)]
    public void IsActive_WhenPassthroughOrVisibilityVaries_RequiresEveryAuthorization(
        int modeValue,
        bool paneVisible,
        bool expected)
    {
        var policy = new Policy(
            [MultiplexerKind.Tmux],
            TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative),
            (PassthroughMode) modeValue,
            paneVisible,
            MultiplexingOperation.CapabilityQueries);

        policy.IsActive.ShouldBe(expected);
    }

    /// <summary>Verifies nested routes wrap the farthest layer first and double ESC at each tmux layer.</summary>
    [Fact]
    public void TryWriteCapabilityQueries_WhenRouteIsNested_WritesExactEnvelopes()
    {
        var policy = ActivePolicy([MultiplexerKind.Tmux, MultiplexerKind.Screen]);
        var route = new MultiplexerRoute(policy);
        var destination = new ArrayBufferWriter<byte>();

        var written = route.TryWriteCapabilityQueries(destination, "\u001b[c"u8);

        written.ShouldBeTrue();
        destination.WrittenSpan.ToArray().ShouldBe(
            "\u001bPtmux;\u001b\u001bP\u001b\u001b[c\u001b\u001b\\\u001b\\"u8.ToArray());
    }

    /// <summary>Verifies nested tmux graphics routing doubles APC escapes at every layer.</summary>
    [Fact]
    public void TryWriteGraphics_WhenRouteHasNestedTmux_WritesExactEnvelopes()
    {
        var policy = new Policy(
            [MultiplexerKind.Tmux, MultiplexerKind.Tmux],
            TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative),
            PassthroughMode.All,
            paneVisible: true,
            MultiplexingOperation.Graphics);
        var route = new MultiplexerRoute(policy);
        var destination = new ArrayBufferWriter<byte>();

        var written = route.TryWriteGraphics(destination, "\u001b_Gx\u001b\\"u8);

        written.ShouldBeTrue();
        destination.WrittenSpan.ToArray().ShouldBe(
            "\u001bPtmux;\u001b\u001bPtmux;\u001b\u001b\u001b\u001b_Gx"u8.ToArray()
                .Concat("\u001b\u001b\u001b\u001b\\\u001b\u001b\\\u001b\\"u8.ToArray())
                .ToArray());
    }

    /// <summary>Verifies GNU screen remains unavailable for non-CSI graphics passthrough.</summary>
    [Fact]
    public void TryWriteGraphics_WhenRouteContainsScreen_RejectsAtomically()
    {
        var policy = new Policy(
            [MultiplexerKind.Screen],
            TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative),
            PassthroughMode.All,
            paneVisible: true,
            MultiplexingOperation.Graphics);
        var destination = new ArrayBufferWriter<byte>();

        var written = new MultiplexerRoute(policy).TryWriteGraphics(destination, "\u001b_Gx\u001b\\"u8);

        written.ShouldBeFalse();
        destination.WrittenCount.ShouldBe(0);
    }

    /// <summary>Verifies tmux unwrapping owns one exact string-terminated reply.</summary>
    [Fact]
    public void TryUnwrapReply_WhenTmuxCarriesDcs_RestoresExactReply()
    {
        var route = new MultiplexerRoute(ActivePolicy([MultiplexerKind.Tmux]));
        var wrapped = new ArrayBufferWriter<byte>();
        var reply = "\u001bP1+r524742=3234\u001b\\"u8.ToArray();
        route.TryWriteCapabilityQueries(wrapped, reply).ShouldBeTrue();
        var envelope = wrapped.WrittenSpan.ToArray();

        var unwrapped = route.TryUnwrapReply(envelope, out var owned);
        envelope.AsSpan().Clear();

        unwrapped.ShouldBeTrue();
        owned.ToArray().ShouldBe(reply);
    }

    /// <summary>Verifies only one complete recognized reply is accepted after unwrapping.</summary>
    /// <param name="reply">The exact recognized reply.</param>
    [Theory]
    [InlineData("\u001b[?1;2c")]
    [InlineData("\u001b[?1004;1$y")]
    [InlineData("\u001b[?3u")]
    [InlineData("\u001b[4;800;1200t")]
    [InlineData("\u001b]10;rgb:0001/0002/0003\u001b\\")]
    [InlineData("\u001bP1$r>4;2m\u001b\\")]
    [InlineData("\u001bP1+r524742=3234\u001b\\")]
    [InlineData("\u001bP1+r544e=787465726d\u001b\\")]
    public void TryUnwrapReply_WhenEnvelopeContainsExactlyOneTypedReply_Accepts(string reply)
    {
        var route = new MultiplexerRoute(ActivePolicy([MultiplexerKind.Tmux]));
        var wrapped = new ArrayBufferWriter<byte>();
        TmuxWriter.WritePassthrough(wrapped, Encoding.ASCII.GetBytes(reply));

        route.TryUnwrapReply(wrapped.WrittenSpan, out var owned).ShouldBeTrue();

        owned.ToArray().ShouldBe(Encoding.ASCII.GetBytes(reply));
    }

    /// <summary>Verifies trailing input and concatenated replies are rejected without leaks at every split.</summary>
    /// <param name="reply">The injected unwrapped payload.</param>
    [Theory]
    [InlineData("\u001b[?1;2cX")]
    [InlineData("\u001b[?1;2c\u001b[?3u")]
    [InlineData("\u001b[1;1R")]
    [InlineData("\u001b]10;rgb:0001/0002/0003\u001b\\X\u001b\\")]
    [InlineData("\u001bP1$r>4;2m\u001b\\X\u001b\\")]
    public void Route_WhenEnvelopeInjectsBeyondOneTypedReply_RejectsAtEverySplit(string reply)
    {
        var route = new MultiplexerRoute(ActivePolicy([MultiplexerKind.Tmux]));
        var wrapped = new ArrayBufferWriter<byte>();
        TmuxWriter.WritePassthrough(wrapped, Encoding.ASCII.GetBytes(reply));
        var input = wrapped.WrittenSpan.ToArray();

        route.TryUnwrapReply(input, out _).ShouldBeFalse();

        for (var split = 0; split <= input.Length; split++)
        {
            var sink = new RecordingProtocolSink();
            using var router = new ProtocolRouter(sink, route: route);
            router.Route(input.AsSpan(0, split));
            router.Route(input.AsSpan(split));

            var diagnostic = sink.Diagnostics.ShouldHaveSingleItem($"split {split}");
            diagnostic.Offset.ShouldBe(0);
            diagnostic.DiscardedBytes.ShouldBe(input.Length);
            sink.Order.ShouldBe(["diagnostic"], $"split {split}");
        }
    }

    /// <summary>Verifies accepted wrapper overhead and rejected envelopes preserve raw offsets.</summary>
    [Fact]
    public void Route_WhenWrappedAndRejectedInputPrecedeMalformedCsi_ReportsRawOffsets()
    {
        var route = new MultiplexerRoute(ActivePolicy([MultiplexerKind.Tmux]));
        var valid = new ArrayBufferWriter<byte>();
        TmuxWriter.WritePassthrough(valid, "\u001b[?1;2c"u8);
        var rejected = new ArrayBufferWriter<byte>();
        TmuxWriter.WritePassthrough(rejected, "\u001b[?1;2cX"u8);
        var sink = new RecordingProtocolSink();
        using var router = new ProtocolRouter(sink, route: route);

        router.Route("abc"u8);
        router.Route(valid.WrittenSpan);
        router.Route(rejected.WrittenSpan);
        router.Route("\u001b[1:x"u8);

        _ = sink.Responses.ShouldHaveSingleItem();
        sink.Diagnostics.Count.ShouldBe(2);
        sink.Diagnostics[0].Offset.ShouldBe(checked(3 + valid.WrittenCount));
        sink.Diagnostics[0].DiscardedBytes.ShouldBe(rejected.WrittenCount);
        sink.Diagnostics[1].Offset.ShouldBe(
            checked(3 + valid.WrittenCount + rejected.WrittenCount + 5));
    }

    /// <summary>Verifies nested ESC expansion contributes to later raw diagnostic offsets.</summary>
    [Fact]
    public void Route_WhenNestedTmuxReplyPrecedesMalformedCsi_ReportsRawOffset()
    {
        var route = new MultiplexerRoute(ActivePolicy([MultiplexerKind.Tmux, MultiplexerKind.Tmux]));
        var inner = new ArrayBufferWriter<byte>();
        TmuxWriter.WritePassthrough(inner, "\u001b[?1;2c"u8);
        var outer = new ArrayBufferWriter<byte>();
        TmuxWriter.WritePassthrough(outer, inner.WrittenSpan);
        var sink = new RecordingProtocolSink();
        using var router = new ProtocolRouter(sink, route: route);

        router.Route(outer.WrittenSpan);
        router.Route("\u001b[1:x"u8);

        _ = sink.Responses.ShouldHaveSingleItem();
        var diagnostic = sink.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Offset.ShouldBe(outer.WrittenCount + 5);
    }

    /// <summary>Verifies failed atomic route encoding retires all negotiation work immediately.</summary>
    [Fact]
    public void TryStart_WhenRouteCannotEncodeBatch_CompletesWithoutBytesOrPendingWork()
    {
        var policy = new Policy(
            [MultiplexerKind.Tmux],
            TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative),
            PassthroughMode.All,
            paneVisible: true,
            MultiplexingOperation.CapabilityQueries,
            maxDepth: 4,
            maxEnvelopeBytes: 8);
        var limits = QueryLimits.Default with { MaxConcurrentQueries = 1 };
        var negotiator = new Negotiator(new NegotiationOptions(
            new Dictionary<string, string?>(),
            overrides: null,
            limits: limits,
            multiplexing: policy));
        var destination = new ArrayBufferWriter<byte>();

        var started = negotiator.TryStart(destination, null, null, new MultiplexerRoute(policy));

        started.ShouldBeFalse();
        destination.WrittenCount.ShouldBe(0);
        negotiator.IsComplete.ShouldBeTrue();
        negotiator.HasPendingWork.ShouldBeFalse();
        var late = Csi("?1;2"u8, [], (byte) 'c');
        negotiator.Accept(in late).ShouldBe(QueryMatch.Late);
    }

    /// <summary>Verifies every Screen-containing route atomically rejects string-terminated query families.</summary>
    /// <param name="query">A typed OSC or DCS query which Screen cannot relay with its terminator.</param>
    [Theory]
    [InlineData("\u001b]10;?\u001b\\")]
    [InlineData("\u001bP+q524742\u001b\\")]
    [InlineData("\u001bP$q>m\u001b\\")]
    public void TryWriteCapabilityQueries_WhenScreenRouteContainsStringQuery_RejectsAtomically(
        string query)
    {
        var route = new MultiplexerRoute(ActivePolicy([MultiplexerKind.Tmux, MultiplexerKind.Screen]));
        var destination = new ArrayBufferWriter<byte>();

        var written = route.TryWriteCapabilityQueries(
            destination,
            Encoding.ASCII.GetBytes(query));

        written.ShouldBeFalse();
        destination.WrittenCount.ShouldBe(0);
    }

    /// <summary>Verifies Screen cannot safely precede a farther DCS-based multiplexer layer.</summary>
    [Fact]
    public void TryWriteCapabilityQueries_WhenScreenWouldRelayTmuxEnvelope_RejectsAtomically()
    {
        MultiplexerKind[][] routes =
        [
            [MultiplexerKind.Screen, MultiplexerKind.Tmux],
            [MultiplexerKind.Tmux, MultiplexerKind.Screen, MultiplexerKind.Screen]
        ];

        foreach (var layers in routes)
        {
            var route = new MultiplexerRoute(ActivePolicy(layers));
            var destination = new ArrayBufferWriter<byte>();

            var written = route.TryWriteCapabilityQueries(destination, "\u001b[c"u8);

            written.ShouldBeFalse(layers.Length.ToString(CultureInfo.InvariantCulture));
            destination.WrittenCount.ShouldBe(0);
        }
    }

    /// <summary>Verifies a malformed tmux ESC pair is rejected without publishing partial bytes.</summary>
    [Fact]
    public void TryUnwrapReply_WhenTmuxEscapePairIsMalformed_Rejects()
    {
        var route = new MultiplexerRoute(ActivePolicy([MultiplexerKind.Tmux]));

        var unwrapped = route.TryUnwrapReply(
            "\u001bPtmux;\u001bP1+r524742=3234\u001b\\"u8,
            out var owned);

        unwrapped.ShouldBeFalse();
        owned.IsEmpty.ShouldBeTrue();
    }

    /// <summary>Verifies routing refuses values which exceed the finite envelope bound.</summary>
    [Fact]
    public void TryWriteCapabilityQueries_WhenEnvelopeExceedsLimit_RejectsWithoutWriting()
    {
        var policy = new Policy(
            [MultiplexerKind.Tmux],
            TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative),
            PassthroughMode.All,
            paneVisible: true,
            MultiplexingOperation.CapabilityQueries,
            maxDepth: 4,
            maxEnvelopeBytes: 16);
        var route = new MultiplexerRoute(policy);
        var destination = new ArrayBufferWriter<byte>();

        var written = route.TryWriteCapabilityQueries(destination, new byte[16]);

        written.ShouldBeFalse();
        destination.WrittenCount.ShouldBe(0);
    }

    /// <summary>Verifies unapproved clipboard or graphics families cannot use the route.</summary>
    [Fact]
    public void Policy_WhenOnlyQueriesAreApproved_RejectsOtherTypedFamilies()
    {
        var policy = ActivePolicy([MultiplexerKind.Tmux]);

        policy.Allows(MultiplexingOperation.CapabilityQueries).ShouldBeTrue();
        policy.Allows(MultiplexingOperation.Clipboard).ShouldBeFalse();
        policy.Allows(MultiplexingOperation.Graphics).ShouldBeFalse();
    }

    /// <summary>Verifies configured route depth is finite and enforced during construction.</summary>
    [Fact]
    public void Constructor_WhenRouteExceedsMaximumDepth_Throws()
    {
        _ = Should.Throw<ArgumentException>(() => new Policy(
            [MultiplexerKind.Tmux, MultiplexerKind.Screen, MultiplexerKind.Tmux],
            TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative),
            PassthroughMode.All,
            paneVisible: true,
            MultiplexingOperation.CapabilityQueries,
            maxDepth: 2));
    }

    /// <summary>Verifies complete wrapped replies recover through the public router at every split.</summary>
    [Fact]
    public void Route_WhenTmuxReplyIsFragmented_UnwrapsBeforeTypedProtocolAtEverySplit()
    {
        var route = new MultiplexerRoute(ActivePolicy([MultiplexerKind.Tmux]));
        var wrapped = new ArrayBufferWriter<byte>();
        route.TryWriteCapabilityQueries(wrapped, "\u001bP1+r524742=3234\u001b\\"u8).ShouldBeTrue();
        var input = wrapped.WrittenSpan.ToArray();

        for (var split = 0; split <= input.Length; split++)
        {
            var sink = new RecordingProtocolSink();
            using var router = new ProtocolRouter(sink, route: route);
            router.Route(input.AsSpan(0, split));
            router.Route(input.AsSpan(split));

            sink.CapabilityResponses.ShouldHaveSingleItem($"split {split}")
                .Items.ShouldContainKey(CapabilityName.DirectColor);
            sink.Sequences.ShouldBeEmpty($"split {split}");
            router.Route("\u001b[1:x"u8);
            sink.Diagnostics.ShouldHaveSingleItem($"split {split}")
                .Offset.ShouldBe(input.Length + 5);
        }
    }

    /// <summary>Verifies a fabricated Screen-wrapped DCS is consumed without leaking either ST at every split.</summary>
    [Fact]
    public void Route_WhenScreenEnvelopeContainsInnerDcs_RejectsWithoutInputLeakAtEverySplit()
    {
        var route = new MultiplexerRoute(ActivePolicy([MultiplexerKind.Screen]));
        var wrapped = new ArrayBufferWriter<byte>();
        GnuScreenWriter.WritePassthrough(wrapped, "\u001bP1$r>4;2m\u001b\\"u8);
        var input = wrapped.WrittenSpan.ToArray();

        route.TryUnwrapReply(input, out var rejected).ShouldBeFalse();
        rejected.IsEmpty.ShouldBeTrue();

        for (var split = 0; split <= input.Length; split++)
        {
            var sink = new RecordingProtocolSink();
            using var router = new ProtocolRouter(sink, route: route);
            router.Route(input.AsSpan(0, split));
            router.Route(input.AsSpan(split));
            router.Route("\u001b[1:x"u8);

            sink.Diagnostics.Count.ShouldBe(2, $"split {split}");
            sink.Diagnostics[0].Code.ShouldBe(DiagnosticCode.Unsupported);
            sink.Diagnostics[0].Offset.ShouldBe(0);
            sink.Diagnostics[0].DiscardedBytes.ShouldBe(input.Length);
            sink.Diagnostics[1].Offset.ShouldBe(input.Length + 5);
            sink.StatusResponses.ShouldBeEmpty($"split {split}");
            sink.CapabilityResponses.ShouldBeEmpty($"split {split}");
            sink.Strokes.ShouldBeEmpty($"split {split}");
            sink.Text.ShouldBeEmpty($"split {split}");
            sink.Sequences.ShouldBeEmpty($"split {split}");
        }
    }

    /// <summary>Verifies oversized Screen strings discard through both terminators without leaking input.</summary>
    [Fact]
    public void Route_WhenScreenEnvelopeExceedsBound_DiscardsThroughOuterTerminatorAtEverySplit()
    {
        var policy = new Policy(
            [MultiplexerKind.Screen],
            TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative),
            PassthroughMode.All,
            paneVisible: true,
            MultiplexingOperation.CapabilityQueries,
            maxDepth: 4,
            maxEnvelopeBytes: 16);
        var route = new MultiplexerRoute(policy);
        var wrapped = new ArrayBufferWriter<byte>();
        GnuScreenWriter.WritePassthrough(wrapped, "\u001bP1+r524742=3234\u001b\\"u8);
        var input = wrapped.WrittenSpan.ToArray();

        for (var split = 0; split <= input.Length; split++)
        {
            var sink = new RecordingProtocolSink();
            using var router = new ProtocolRouter(sink, route: route);
            router.Route(input.AsSpan(0, split));
            router.Route(input.AsSpan(split));
            router.Route("\u001b[1:x"u8);

            sink.Diagnostics.Count.ShouldBe(2, $"split {split}");
            sink.Diagnostics[0].Code.ShouldBe(DiagnosticCode.Unsupported);
            sink.Diagnostics[0].Offset.ShouldBe(0);
            sink.Diagnostics[0].DiscardedBytes.ShouldBe(input.Length);
            sink.Diagnostics[1].Offset.ShouldBe(input.Length + 5);
            sink.CapabilityResponses.ShouldBeEmpty($"split {split}");
            sink.StatusResponses.ShouldBeEmpty($"split {split}");
            sink.Strokes.ShouldBeEmpty($"split {split}");
            sink.Text.ShouldBeEmpty($"split {split}");
            sink.Sequences.ShouldBeEmpty($"split {split}");
        }
    }

    /// <summary>Verifies a complete Screen-wrapped CSI reply still reaches correlation at every split.</summary>
    [Fact]
    public void Route_WhenScreenEnvelopeContainsCsi_UnwrapsAtEverySplit()
    {
        var route = new MultiplexerRoute(ActivePolicy([MultiplexerKind.Screen]));
        var wrapped = new ArrayBufferWriter<byte>();
        GnuScreenWriter.WritePassthrough(wrapped, "\u001b[?1;2c"u8);
        var input = wrapped.WrittenSpan.ToArray();

        for (var split = 0; split <= input.Length; split++)
        {
            var sink = new RecordingProtocolSink();
            using var router = new ProtocolRouter(sink, route: route);
            router.Route(input.AsSpan(0, split));
            router.Route(input.AsSpan(split));

            sink.Responses.ShouldHaveSingleItem($"split {split}")
                .Kind.ShouldBe(ResponseKind.PrimaryAttributes);
            sink.Diagnostics.ShouldBeEmpty($"split {split}");
            sink.Text.ShouldBeEmpty($"split {split}");
            sink.Strokes.ShouldBeEmpty($"split {split}");
            sink.Sequences.ShouldBeEmpty($"split {split}");
        }
    }

    /// <summary>Verifies Screen negotiation registers only safe CSI families and completes from those replies.</summary>
    [Fact]
    public void Start_WhenScreenRouteIsActive_OmitsStringFamiliesWithoutDeadlineWork()
    {
        var policy = ActivePolicy([MultiplexerKind.Screen]);
        var route = new MultiplexerRoute(policy);
        var options = new NegotiationOptions(
            new Dictionary<string, string?> { ["TERM"] = "xterm-256color" },
            overrides: null,
            limits: QueryLimits.Default,
            multiplexing: policy);
        var negotiator = new Negotiator(options);
        var destination = new ArrayBufferWriter<byte>();

        negotiator.Start(destination, localCells: null, localPixels: null, route);
        var inner = new ArrayBufferWriter<byte>();
        GnuScreenWriter.TryUnwrap(destination.WrittenSpan, inner).ShouldBeTrue();

        inner.WrittenSpan.ToArray().ShouldBe(
            Encoding.ASCII.GetBytes(
                "\u001b[?u\u001b[c\u001b[>c" +
                "\u001b[?2026$p\u001b[?1004$p\u001b[?2004$p" +
                "\u001b[?1006$p\u001b[?1016$p\u001b[?5522$p" +
                "\u001b[14t\u001b[16t\u001b[18t"));

        var keyboard = Csi("?3"u8, [], (byte) 'u');
        negotiator.Accept(in keyboard).ShouldBe(QueryMatch.Matched);

        foreach (var mode in new[] { 1016, 1006, 2004, 1004, 2026, 5522 })
        {
            var response = Csi(
                Encoding.ASCII.GetBytes($"?{mode};1"),
                "$"u8,
                (byte) 'y');
            negotiator.Accept(in response).ShouldBe(QueryMatch.Matched);
        }

        foreach (var parameters in new[]
                 {
                     "8;40;120"u8.ToArray(),
                     "6;20;10"u8.ToArray(),
                     "4;800;1200"u8.ToArray()
                 })
        {
            XtermResponses.TryMetricsCsi(parameters, [], (byte) 't', out var response).ShouldBeTrue();
            negotiator.Accept(in response).ShouldBe(QueryMatch.Matched);
        }

        var secondary = Csi(">41;410;0"u8, [], (byte) 'c');
        negotiator.Accept(in secondary).ShouldBe(QueryMatch.Matched);
        var primary = Csi("?1;2"u8, [], (byte) 'c');
        negotiator.Accept(in primary).ShouldBe(QueryMatch.Matched);

        negotiator.IsComplete.ShouldBeTrue();
        negotiator.Results.PaletteColor.ShouldBeNull();
        negotiator.Results.ForegroundColor.ShouldBeNull();
        negotiator.Results.BackgroundColor.ShouldBeNull();
        negotiator.Results.CapabilityString.ShouldBeNull();
    }

    /// <summary>Verifies the negotiator sends its exact typed batch only through the selected route.</summary>
    [Fact]
    public void Start_WhenQueryRoutingIsActive_WritesOneExactEnvelope()
    {
        var policy = ActivePolicy([MultiplexerKind.Tmux]);
        var route = new MultiplexerRoute(policy);
        var limits = QueryLimits.Default with { MaxConcurrentQueries = 1 };
        var options = new NegotiationOptions(
            new Dictionary<string, string?>(),
            overrides: null,
            limits: limits,
            multiplexing: policy);
        var negotiator = new Negotiator(options);
        var destination = new ArrayBufferWriter<byte>();

        negotiator.Start(destination, localCells: null, localPixels: null, route);

        destination.WrittenSpan.ToArray().ShouldBe(
            "\u001bPtmux;\u001b\u001b[c\u001b\\"u8.ToArray());
    }

    /// <summary>Verifies unanswered routed queries preserve explicit outer evidence at the shared deadline.</summary>
    [Fact]
    public void Expire_WhenRoutedQueryTimesOut_PublishesConservativeExplicitBaseline()
    {
        var clock = new ManualTimeProvider();
        var outerCapabilities = TerminalCapabilities.Conservative with
        {
            Osc52 = new Feature(CapabilitySupport.Supported, Origin.Override)
        };
        var outerProfile = TerminalProfile.CreateAnsi(outerCapabilities);
        var policy = new Policy(
            [MultiplexerKind.Tmux],
            outerProfile,
            PassthroughMode.All,
            paneVisible: true,
            MultiplexingOperation.CapabilityQueries);
        var limits = QueryLimits.Default with
        {
            MaxConcurrentQueries = 1,
            QueryTimeout = TimeSpan.FromMilliseconds(10)
        };
        var options = new NegotiationOptions(
            new Dictionary<string, string?> { ["TERM"] = "tmux-256color" },
            overrides: null,
            limits: limits,
            multiplexing: policy);
        var negotiator = new Negotiator(options, outerCapabilities, clock);
        var destination = new ArrayBufferWriter<byte>();
        negotiator.Start(destination, localCells: null, localPixels: null, new MultiplexerRoute(policy));

        clock.Advance(limits.QueryTimeout);
        negotiator.Expire().ShouldBeTrue();

        negotiator.Capabilities.Osc52.ShouldBe(outerCapabilities.Osc52);
        negotiator.Capabilities.KittyGraphics.State.ShouldNotBe(CapabilitySupport.Supported);
    }

    /// <summary>Creates one query-only explicit outer-terminal route.</summary>
    private static Policy ActivePolicy(IReadOnlyList<MultiplexerKind> layers) => new(
        layers,
        TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative),
        PassthroughMode.All,
        paneVisible: true,
        MultiplexingOperation.CapabilityQueries);

    private static Response Csi(
        ReadOnlySpan<byte> parameters,
        ReadOnlySpan<byte> intermediates,
        byte final)
    {
        XtermResponses.TryCsi(parameters, intermediates, final, out var response).ShouldBeTrue();
        return response;
    }
}
