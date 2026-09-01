// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Protocols;

using SharpVision.Terminal.Capabilities;
using SharpVision.Terminal.Kitty.Clipboard;

using TerminalInputOptions = InputOptions;

/// <summary>
/// Verifies <see cref="ProtocolRouter"/> typed runtime routing, ownership, and recovery,
/// including DECRQSS, XTGETTCAP, iTerm2 OSC 1337, and clipboard (OSC 52 / Kitty OSC 5522)
/// reply routing.
/// </summary>
public sealed class ProtocolRouterTests
{
    #region Typed response and sequence routing

    /// <summary>Gets representative values for every bounded string family.</summary>
    public static TheoryData<byte[], SequenceKind> StringCases { get; } = new()
    {
        { "\u001b]777;payload\u001b\\"u8.ToArray(), SequenceKind.Osc },
        { "\u001bP1;2$qpayload\u001b\\"u8.ToArray(), SequenceKind.Dcs },
        { "\u001b_Hpayload\u001b\\"u8.ToArray(), SequenceKind.Apc },
        { "\u001b^payload\u001b\\"u8.ToArray(), SequenceKind.Pm },
        { "\u001bXpayload\u001b\\"u8.ToArray(), SequenceKind.Sos },
        { "\u001b]777;payload\u0007"u8.ToArray(), SequenceKind.Osc }
    };

    /// <summary>Verifies every transport split of a recognized terminal reply cannot fall through as input.</summary>
    /// <param name="input">The complete terminal reply.</param>
    /// <param name="expected">The expected typed response family.</param>
    [Theory]
    [InlineData("\u001b[?1;2c", ResponseKind.PrimaryAttributes)]
    [InlineData("\u001b[>41;410;0c", ResponseKind.SecondaryAttributes)]
    [InlineData("\u001b[?2026;1$y", ResponseKind.PrivateMode)]
    [InlineData("\u001b[?3u", ResponseKind.Keyboard)]
    public void Route_WhenReplyIsFragmented_DeliversTypedResponse(
        string input,
        ResponseKind expected)
    {
        // Arrange
        var bytes = Encoding.UTF8.GetBytes(input);

        // Act / Assert
        for (var split = 0; split <= bytes.Length; split++)
        {
            var sink = new RecordingProtocolSink();
            using ProtocolRouter router = new(sink);
            router.Route(bytes.AsSpan(0, split));
            router.Route(bytes.AsSpan(split));

            sink.Responses.ShouldHaveSingleItem($"The reply differed at split {split}.").Kind.ShouldBe(expected);
            sink.Sequences.ShouldBeEmpty();
            sink.Strokes.ShouldBeEmpty();
            sink.Text.ShouldBeEmpty();
        }
    }

    /// <summary>
    /// Verifies a DSR cursor-position reply (<c>CSI &lt;row&gt;;&lt;col&gt;R</c>) is still claimed
    /// as a typed response at every transport split once
    /// <see cref="ProtocolRouter.EnableCursorPositionQuery"/> marks a query outstanding. Unlike the
    /// other shapes in <see cref="Route_WhenReplyIsFragmented_DeliversTypedResponse"/>, this one is
    /// byte-identical to a modified F3 keystroke and is therefore excluded from that shared theory:
    /// by default, with no query outstanding, it decodes as a key instead.
    /// </summary>
    [Fact]
    public void Route_WhenCursorPositionReplyIsFragmentedAndQueryIsOutstanding_DeliversTypedResponse()
    {
        // Arrange
        var bytes = Encoding.UTF8.GetBytes("\u001b[12;34R");

        // Act / Assert
        for (var split = 0; split <= bytes.Length; split++)
        {
            var sink = new RecordingProtocolSink();
            using ProtocolRouter router = new(sink);
            router.EnableCursorPositionQuery();
            router.Route(bytes.AsSpan(0, split));
            router.Route(bytes.AsSpan(split));

            sink.Responses.ShouldHaveSingleItem($"The reply differed at split {split}.")
                .Kind.ShouldBe(ResponseKind.CursorPosition);
            sink.Sequences.ShouldBeEmpty();
            sink.Strokes.ShouldBeEmpty();
            sink.Text.ShouldBeEmpty();
        }
    }

    /// <summary>
    /// Verifies the same byte-identical shape decodes as a modified F3 key event, not a typed
    /// response, when no cursor-position query is outstanding - the default state for the whole
    /// session outside an active negotiation window.
    /// </summary>
    [Fact]
    public void Route_WhenCursorPositionShapeArrivesWithNoQueryOutstanding_DeliversKeyNotResponse()
    {
        // Arrange
        var bytes = Encoding.UTF8.GetBytes("\u001b[12;34R");

        // Act / Assert
        for (var split = 0; split <= bytes.Length; split++)
        {
            var sink = new RecordingProtocolSink();
            using ProtocolRouter router = new(sink);
            router.Route(bytes.AsSpan(0, split));
            router.Route(bytes.AsSpan(split));

            sink.Responses.ShouldBeEmpty($"The key differed at split {split}.");
            var stroke = sink.Strokes.ShouldHaveSingleItem($"The key differed at split {split}.");
            stroke.Code.ShouldBe(Code.F3);
        }
    }

    /// <summary>Verifies every transport split of OSC 4/10/11 produces one owned color response.</summary>
    [Theory]
    [InlineData("\u001b]4;15;rgb:ffff/0000/8080\u001b\\", ResponseKind.PaletteColor)]
    [InlineData("\u001b]10;rgb:ffff/0000/8080\u001b\\", ResponseKind.ForegroundColor)]
    [InlineData("\u001b]11;rgb:0000/ffff/8080\u001b\\", ResponseKind.BackgroundColor)]
    public void Route_WhenColorReplyIsFragmented_DeliversOwnedResponse(
        string input,
        ResponseKind expected)
    {
        var bytes = Encoding.ASCII.GetBytes(input);

        for (var split = 0; split <= bytes.Length; split++)
        {
            var sink = new RecordingProtocolSink();
            using ProtocolRouter router = new(sink);
            router.Route(bytes.AsSpan(0, split));
            router.Route(bytes.AsSpan(split));

            sink.PaletteResponses.ShouldHaveSingleItem(
                $"The color reply differed at split {split}.").Kind.ShouldBe(expected);
            sink.Sequences.ShouldBeEmpty();
            sink.Strokes.ShouldBeEmpty();
            sink.Text.ShouldBeEmpty();
        }
    }

    /// <summary>Verifies every transport split of window reports produces one owned metrics response.</summary>
    [Theory]
    [InlineData("\u001b[4;1080;1920t", ResponseKind.WindowPixels)]
    [InlineData("\u001b[6;20;10t", ResponseKind.CellPixels)]
    [InlineData("\u001b[8;40;120t", ResponseKind.WindowCells)]
    [InlineData("\u001b[4;2160;70000t", ResponseKind.WindowPixels)]
    [InlineData("\u001b[6;70000;10t", ResponseKind.CellPixels)]
    [InlineData("\u001b[8;40;70000t", ResponseKind.WindowCells)]
    public void Route_WhenMetricsReplyIsFragmented_DeliversOwnedResponse(
        string input,
        ResponseKind expected)
    {
        var bytes = Encoding.ASCII.GetBytes(input);

        for (var split = 0; split <= bytes.Length; split++)
        {
            var sink = new RecordingProtocolSink();
            using ProtocolRouter router = new(sink);
            router.Route(bytes.AsSpan(0, split));
            router.Route(bytes.AsSpan(split));

            sink.MetricsResponses.ShouldHaveSingleItem(
                $"The metrics reply differed at split {split}.").Kind.ShouldBe(expected);
            sink.Sequences.ShouldBeEmpty();
            sink.Strokes.ShouldBeEmpty();
            sink.Text.ShouldBeEmpty();
        }
    }

    /// <summary>Verifies an overflowing OSC 4 index is rejected and every split recovers the next reply.</summary>
    [Fact]
    public void Route_WhenPaletteIndexOverflows_RejectsAndRecoversAtEverySplit()
    {
        var bytes = "\u001b]4;4294967296;rgb:ffff/0000/0000\u001b\\\u001b[?1;2c"u8.ToArray();

        for (var split = 0; split <= bytes.Length; split++)
        {
            var sink = new RecordingProtocolSink();
            using ProtocolRouter router = new(sink);
            router.Route(bytes.AsSpan(0, split));
            router.Route(bytes.AsSpan(split));

            sink.PaletteResponses.ShouldBeEmpty($"Overflow was accepted at split {split}.");
            sink.Sequences.ShouldBeEmpty($"Overflow should not fall through as an untagged sequence at split {split}.");
            sink.Diagnostics.ShouldHaveSingleItem($"OSC recovery differed at split {split}.")
                .Code.ShouldBe(DiagnosticCode.Malformed, $"split {split}");
            sink.Responses.ShouldHaveSingleItem($"Trailing DA1 was lost at split {split}.")
                .Kind.ShouldBe(ResponseKind.PrimaryAttributes);
        }
    }

    /// <summary>Verifies DCS headers and payload outlive the source read.</summary>
    [Fact]
    public void Route_WhenDcsCompletes_OwnsHeaderAndPayload()
    {
        // Arrange
        var sink = new RecordingProtocolSink();
        using ProtocolRouter router = new(sink);
        var input = "\u001bP1;2$qpayload\u001b\\"u8.ToArray();

        // Act
        router.Route(input);
        input.AsSpan().Fill((byte) 'x');

        // Assert
        var sequence = sink.Sequences.ShouldHaveSingleItem();
        sequence.Kind.ShouldBe(SequenceKind.Dcs);
        sequence.Parameters.Span.SequenceEqual("1;2"u8).ShouldBeTrue();
        sequence.Intermediates.Span.SequenceEqual("$"u8).ShouldBeTrue();
        sequence.Final.ShouldBe((byte) 'q');
        sequence.Payload.Span.SequenceEqual("payload"u8).ShouldBeTrue();
        sequence.Terminator.ShouldBe(StringTerminator.EscapeBackslash);
    }

    /// <summary>Verifies every transport split produces one identical owned string.</summary>
    /// <param name="input">The complete string sequence.</param>
    /// <param name="kind">The expected sequence family.</param>
    [Theory]
    [MemberData(nameof(StringCases))]
    public void Route_WhenStringIsFragmented_DeliversOneEquivalentSequence(
        byte[] input,
        SequenceKind kind)
    {
        // Arrange
        var expectedSink = new RecordingProtocolSink();
        using (ProtocolRouter expectedRouter = new(expectedSink))
        {
            expectedRouter.Route(input);
        }

        var expected = expectedSink.Sequences.ShouldHaveSingleItem();

        // Act / Assert
        for (var split = 0; split <= input.Length; split++)
        {
            var sink = new RecordingProtocolSink();
            using ProtocolRouter router = new(sink);
            router.Route(input.AsSpan(0, split));
            router.Route(input.AsSpan(split));

            var actual = sink.Sequences.ShouldHaveSingleItem(
                $"The sequence differed at split {split}.");
            actual.Kind.ShouldBe(kind);
            AssertEquivalent(expected, actual);
        }

        var byteSink = new RecordingProtocolSink();
        using ProtocolRouter byteRouter = new(byteSink);

        foreach (var value in input)
        {
            byteRouter.Route([value]);
        }

        AssertEquivalent(expected, byteSink.Sequences.ShouldHaveSingleItem());
    }

    /// <summary>Verifies typed replies remain ordered before adjacent user input.</summary>
    [Fact]
    public void Route_WhenReplyPrecedesText_PreservesTransportOrder()
    {
        // Arrange
        var sink = new RecordingProtocolSink();
        using ProtocolRouter router = new(sink);

        // Act
        router.Route("\u001b[?1;2cx"u8);

        // Assert
        sink.Order.ShouldBe(["response", "key", "text"]);
    }

    /// <summary>Verifies a late queried metric never reinterprets pointer values already delivered.</summary>
    [Fact]
    public void Route_WhenMetricsFollowPixelPointer_UsesThemOnlyForLaterInput()
    {
        // Arrange
        var sink = new RecordingProtocolSink();
        using ProtocolRouter router = new(
            sink,
            TerminalInputOptions.Default with { PixelMouse = true });

        // Act
        router.Route("\u001b[<0;17;33M"u8);
        var delivered = sink.Pointers.ShouldHaveSingleItem();
        router.Route("\u001b[6;16;8t"u8);
        router.Route("\u001b[<0;17;33M"u8);

        // Assert
        delivered.Pixels.ShouldBe(new Point(16, 32));
        delivered.Cells.ShouldBeNull();
        sink.MetricsResponses.ShouldHaveSingleItem().Kind.ShouldBe(ResponseKind.CellPixels);
        sink.Pointers.Count.ShouldBe(2);
        sink.Pointers[0].ShouldBe(delivered);
        sink.Pointers[1].Cells.ShouldBe(new Point(2, 2));
        sink.Pointers[1].CellPositionInferred.ShouldBeTrue();
    }

    /// <summary>Verifies oversized strings recover into following user text.</summary>
    [Fact]
    public void Route_WhenStringExceedsLimit_ReportsAndRecoversFollowingText()
    {
        // Arrange
        var sink = new RecordingProtocolSink();
        var options = TerminalInputOptions.Default with { ParserLimits = ParserLimits.Default with { MaxStringBytes = 8 } };
        using ProtocolRouter router = new(sink, options);

        // Act
        router.Route("\u001b]777;0123456789\u001b\\known"u8);

        // Assert
        sink.Diagnostics.ShouldContain(value =>
            value.Code == DiagnosticCode.StringLimit &&
            value.Kind == SequenceKind.Osc);
        sink.Text.Select(value => value.Value.ToString()).ShouldBe(
            ["k", "n", "o", "w", "n"]);
        sink.Sequences.ShouldBeEmpty();
    }

    /// <summary>Verifies CAN aborts a string and restores normal text routing.</summary>
    [Fact]
    public void Route_WhenStringIsCancelled_ReportsAndRecoversFollowingText()
    {
        // Arrange
        var sink = new RecordingProtocolSink();
        using ProtocolRouter router = new(sink);

        // Act
        router.Route("\u001b]777;payload\u0018x"u8);

        // Assert
        sink.Diagnostics.ShouldContain(value =>
            value.Code == DiagnosticCode.Cancelled &&
            value.Kind == SequenceKind.Osc);
        sink.Text.Select(value => value.Value.ToString()).ShouldBe(["x"]);
        sink.Sequences.ShouldBeEmpty();
    }

    /// <summary>Verifies stream completion reports one unfinished string.</summary>
    [Fact]
    public void Complete_WhenStringIsTruncated_ReportsOnceAndRoutesNoSequence()
    {
        // Arrange
        var sink = new RecordingProtocolSink();
        using ProtocolRouter router = new(sink);
        router.Route("\u001bP1;2$qpartial"u8);

        // Act
        router.Complete();
        router.Complete();

        // Assert
        sink.Diagnostics.Count(value =>
            value is { Code: DiagnosticCode.Truncated, Kind: SequenceKind.Dcs }).ShouldBe(1);
        sink.Sequences.ShouldBeEmpty();
    }

    private static void AssertEquivalent(
        ProtocolSequence expected,
        ProtocolSequence actual)
    {
        actual.Kind.ShouldBe(expected.Kind);
        actual.Parameters.Span.SequenceEqual(expected.Parameters.Span).ShouldBeTrue();
        actual.Intermediates.Span.SequenceEqual(expected.Intermediates.Span).ShouldBeTrue();
        actual.Final.ShouldBe(expected.Final);
        actual.Payload.Span.SequenceEqual(expected.Payload.Span).ShouldBeTrue();
        actual.Terminator.ShouldBe(expected.Terminator);
    }

    #endregion

    #region Clipboard routing (OSC 52 / Kitty OSC 5522)

    /// <summary>Verifies a successful OSC 52 reply is claimed and forwarded, not treated as an
    /// unrecognized xterm response or Kitty packet.</summary>
    [Fact]
    public void Route_WhenOsc52ReplyArrives_IsClaimedByClipboardHandler()
    {
        var sink = new RecordingProtocolSink();
        using var router = new ProtocolRouter(sink);

        router.Route("]52;c;aGVsbG8=\\"u8);

        var reply = sink.ClipboardReplies.ShouldHaveSingleItem();
        reply.Status.ShouldBe(ClipboardStatus.Success);
        reply.Selection.ShouldBe(Selection.Clipboard);
        Encoding.UTF8.GetString(reply.Data.Span).ShouldBe("hello");
        sink.Responses.ShouldBeEmpty();
        sink.KittyClipboardPackets.ShouldBeEmpty();
    }

    /// <summary>Verifies a query-form OSC 52 reply parses at every transport split.</summary>
    [Fact]
    public void Route_WhenOsc52QueryReplyIsFragmented_ProducesSameReplyAtEverySplit()
    {
        var wire = "]52;c;?\\"u8.ToArray();

        for (var split = 0; split <= wire.Length; split++)
        {
            var sink = new RecordingProtocolSink();
            using var router = new ProtocolRouter(sink);
            router.Route(wire.AsSpan(0, split));
            router.Route(wire.AsSpan(split));

            var reply = sink.ClipboardReplies.ShouldHaveSingleItem($"split {split}");
            reply.Status.ShouldBe(ClipboardStatus.Query);
        }
    }

    /// <summary>Verifies a valid Kitty OSC 5522 packet is claimed and forwarded, not treated as an
    /// OSC 52 reply.</summary>
    [Fact]
    public void Route_WhenKittyClipboardPacketArrives_IsClaimedByKittyClipboardHandler()
    {
        var sink = new RecordingProtocolSink();
        using var router = new ProtocolRouter(sink);

        router.Route("]5522;type=read:status=OK:id=req-1\\"u8);

        var packet = sink.KittyClipboardPackets.ShouldHaveSingleItem();
        packet.Valid.ShouldBeTrue();
        packet.Operation.ShouldBe(KittyClipboardOperation.Read);
        packet.ReplyStatus.ShouldBe(KittyClipboardReplyStatus.Ok);
        packet.Id.ShouldBe("req-1");
        sink.ClipboardReplies.ShouldBeEmpty();
        sink.Responses.ShouldBeEmpty();
    }

    /// <summary>Verifies a malformed Kitty OSC 5522 packet still routes to the typed handler as an
    /// invalid packet, rather than being silently dropped.</summary>
    [Fact]
    public void Route_WhenKittyClipboardPacketIsMalformed_ProducesInvalidPacket()
    {
        var sink = new RecordingProtocolSink();
        using var router = new ProtocolRouter(sink);

        router.Route("]5522;***\\"u8);

        var packet = sink.KittyClipboardPackets.ShouldHaveSingleItem();
        packet.Valid.ShouldBeFalse();
        _ = packet.Diagnostic.ShouldNotBeNull();
    }

    /// <summary>
    /// Verifies a malformed reply carrying no matching correlation ID does not disrupt an
    /// unrelated in-flight transaction bound to a different ID.
    /// </summary>
    [Fact]
    public void Route_WhenUnrelatedMalformedPacketArrives_DoesNotDisruptBoundTransaction()
    {
        var sink = new RecordingProtocolSink();
        using var router = new ProtocolRouter(sink);
        using var transaction = KittyClipboardTransaction.Read(id: "bound");

        // A malformed packet with a different, recoverable id is unrelated wire noise.
        router.Route("]5522;type=read:status=EIO:id=unrelated\\"u8);

        var unrelated = sink.KittyClipboardPackets.ShouldHaveSingleItem();
        transaction.Accept(unrelated).ShouldBe(KittyClipboardAcceptResult.Ignored);
        transaction.State.ShouldBe(KittyClipboardTransactionState.Created);

        router.Route("]5522;type=read:status=OK:id=bound\\"u8);
        var bound = sink.KittyClipboardPackets[1];
        transaction.Accept(bound).ShouldBe(KittyClipboardAcceptResult.Accepted);
        transaction.State.ShouldBe(KittyClipboardTransactionState.Accepted);
    }

    #endregion

    #region DECRQSS status routing

    /// <summary>Verifies a representative reply is equivalent at every transport split.</summary>
    [Fact]
    public void Route_WhenReplyIsFragmented_DeliversTypedStatusAtEverySplit()
    {
        var input = "\u001bP1$r0;4;38:2::1:2:3m\u001b\\"u8.ToArray();

        for (var split = 0; split <= input.Length; split++)
        {
            var sink = new RecordingProtocolSink();
            using var router = new ProtocolRouter(sink);
            router.Route(input.AsSpan(0, split));
            router.Route(input.AsSpan(split));

            var response = sink.StatusResponses.ShouldHaveSingleItem();
            response.Name.ShouldBe(StatusName.Rendition);
            response.Valid.ShouldBeTrue();
            response.Value.ToArray().ShouldBe("0;4;38:2::1:2:3m"u8.ToArray());
            sink.Sequences.ShouldBeEmpty();
        }
    }

    /// <summary>Verifies every approved status and the failure form across every split with adjacent recovery.</summary>
    /// <param name="payload">The returned CSI body.</param>
    /// <param name="valid">Whether the request succeeded.</param>
    /// <param name="expected">The exact recognized identity.</param>
    [Theory]
    [InlineData("0;4;38:2::1:2:3m", true, StatusName.Rendition)]
    [InlineData("2 q", true, StatusName.CursorStyle)]
    [InlineData("1;24r", true, StatusName.VerticalMargins)]
    [InlineData("1;80s", true, StatusName.HorizontalMargins)]
    [InlineData(">4;2m", true, StatusName.ModifyOtherKeys)]
    [InlineData(">4;1f", true, StatusName.FormatOtherKeys)]
    [InlineData("", false, StatusName.Unknown)]
    public void Route_WhenApprovedOrFailedReplyIsFragmented_RecoversAtEverySplit(
        string payload,
        bool valid,
        StatusName expected)
    {
        var prefix = valid ? "\u001bP1$r" : "\u001bP0$r";
        var input = Encoding.ASCII.GetBytes($"{prefix}{payload}\u001b\\x");

        for (var split = 0; split <= input.Length; split++)
        {
            var sink = new RecordingProtocolSink();
            using var router = new ProtocolRouter(sink);
            router.Route(input.AsSpan(0, split));
            router.Route(input.AsSpan(split));

            var response = sink.StatusResponses.ShouldHaveSingleItem();
            response.Valid.ShouldBe(valid, $"split {split}");
            response.Name.ShouldBe(expected, $"split {split}");
            sink.Text.ShouldHaveSingleItem().Value.ShouldBe(new Rune('x'), $"split {split}");
            sink.Diagnostics.ShouldBeEmpty($"split {split}");
        }
    }

    /// <summary>Verifies structurally valid selector spoofs remain unknown diagnostics at every split.</summary>
    /// <param name="payload">The spoofed or invalid returned CSI body.</param>
    [Theory]
    [InlineData(">40;2m")]
    [InlineData(">4;2;3m")]
    [InlineData(">4;9m")]
    [InlineData(">4;2f")]
    public void Route_WhenReturnedSelectorIsSpoofed_ReportsUnknownAtEverySplit(string payload)
    {
        var input = Encoding.ASCII.GetBytes($"\u001bP1$r{payload}\u001b\\x");

        for (var split = 0; split <= input.Length; split++)
        {
            var sink = new RecordingProtocolSink();
            using var router = new ProtocolRouter(sink);
            router.Route(input.AsSpan(0, split));
            router.Route(input.AsSpan(split));

            var response = sink.StatusResponses.ShouldHaveSingleItem();
            response.Name.ShouldBe(StatusName.Unknown, $"split {split}");
            response.Valid.ShouldBeTrue($"split {split}");
            sink.Diagnostics.ShouldHaveSingleItem().Code.ShouldBe(
                DiagnosticCode.Unsupported,
                $"split {split}");
            sink.Text.ShouldHaveSingleItem().Value.ShouldBe(new Rune('x'), $"split {split}");
        }
    }

    /// <summary>Verifies malformed returned CSI grammar stays raw and adjacent input recovers at every split.</summary>
    [Fact]
    public void Route_WhenReturnedCsiBodyIsMalformed_RejectsTypedValueAndRecoversAtEverySplit()
    {
        var input = "\u001bP1$r>4;xm\u001b\\z"u8.ToArray();

        for (var split = 0; split <= input.Length; split++)
        {
            var sink = new RecordingProtocolSink();
            using var router = new ProtocolRouter(sink);
            router.Route(input.AsSpan(0, split));
            router.Route(input.AsSpan(split));

            sink.StatusResponses.ShouldBeEmpty($"split {split}");
            sink.Sequences.ShouldHaveSingleItem($"split {split}").Kind.ShouldBe(
                SequenceKind.Dcs,
                $"split {split}");
            sink.Text.ShouldHaveSingleItem().Value.ShouldBe(new Rune('z'), $"split {split}");
        }
    }

    /// <summary>Verifies unknown valid status remains typed and diagnostic while following input recovers.</summary>
    [Fact]
    public void Route_WhenStatusIsUnknown_ObservesDiagnosticAndRecovers()
    {
        var sink = new RecordingProtocolSink();
        using var router = new ProtocolRouter(sink);

        router.Route("\u001bP1$r?999h\u001b\\x"u8);

        sink.StatusResponses.ShouldHaveSingleItem().Name.ShouldBe(StatusName.Unknown);
        sink.Diagnostics.ShouldHaveSingleItem().Code.ShouldBe(DiagnosticCode.Unsupported);
        sink.Text.ShouldHaveSingleItem().Value.ShouldBe(new Rune('x'));
    }

    /// <summary>Verifies a representative tmux-wrapped reply reaches the same typed router seam.</summary>
    [Fact]
    public void Route_WhenReplyWasWrappedByTmux_UnwrapsBeforeTypedStatusRouting()
    {
        var wrapped = new ArrayBufferWriter<byte>();
        TmuxWriter.WritePassthrough(wrapped, "\u001bP1$r>4;2m\u001b\\"u8);
        var outerPayload = wrapped.WrittenSpan[2..^2];
        var inner = new ArrayBufferWriter<byte>();
        TmuxWriter.TryUnwrap(outerPayload, inner).ShouldBeTrue();
        var sink = new RecordingProtocolSink();
        using var router = new ProtocolRouter(sink);

        router.Route(inner.WrittenSpan);

        sink.StatusResponses.ShouldHaveSingleItem().Name.ShouldBe(StatusName.ModifyOtherKeys);
    }

    #endregion

    #region XTGETTCAP capability routing

    /// <summary>Verifies a representative multi-item reply at every split.</summary>
    [Fact]
    public void Route_WhenReplyIsFragmented_DeliversOwnedValuesAtEverySplit()
    {
        var input = "\u001bP1+r524742=3234;6B63757531=1B5B41\u001b\\"u8.ToArray();

        for (var split = 0; split <= input.Length; split++)
        {
            var sink = new RecordingProtocolSink();
            using var router = new ProtocolRouter(sink);
            router.Route(input.AsSpan(0, split));
            router.Route(input.AsSpan(split));

            var response = sink.CapabilityResponses.ShouldHaveSingleItem();
            response.Valid.ShouldBeTrue();
            response.Items[CapabilityName.DirectColor].ToArray().ShouldBe("24"u8.ToArray());
            response.Items[CapabilityName.Up].ToArray().ShouldBe("\u001b[A"u8.ToArray());
        }
    }

    /// <summary>Verifies unapproved names, duplicate names, odd hex, and value overflow remain raw.</summary>
    [Theory]
    [InlineData("1+r50415448=2F746D70")]
    [InlineData("1+r524742=31;524742=32")]
    [InlineData("1+r524742=F")]
    [InlineData("1+r524742=313233")]
    [InlineData("1+r524742=31;")]
    [InlineData("1+r524742=GG")]
    public void Route_WhenReplyViolatesPolicy_DoesNotPromoteTypedValue(string dcs)
    {
        var options = TerminalInputOptions.Default with
        {
            QueryLimits = QueryLimits.Default with { MaxCapabilityValueBytes = 2 }
        };
        var input = Encoding.ASCII.GetBytes($"\u001bP{dcs}\u001b\\x");

        for (var split = 0; split <= input.Length; split++)
        {
            var sink = new RecordingProtocolSink();
            using var router = new ProtocolRouter(sink, options);
            router.Route(input.AsSpan(0, split));
            router.Route(input.AsSpan(split));

            sink.CapabilityResponses.ShouldBeEmpty($"split {split}");
            sink.Sequences.ShouldHaveSingleItem().Kind.ShouldBe(
                SequenceKind.Dcs,
                $"split {split}");
            sink.Text.ShouldHaveSingleItem().Value.ShouldBe(new Rune('x'), $"split {split}");
        }
    }

    /// <summary>Verifies exact item and value bounds accept the limit and reject limit plus one.</summary>
    [Fact]
    public void Route_WhenSpecificBoundsAreReached_EnforcesExactBoundary()
    {
        var limits = QueryLimits.Default with
        {
            MaxCapabilityItems = 1,
            MaxCapabilityValueBytes = 2
        };
        var accepted = new RecordingProtocolSink();
        using (var router = new ProtocolRouter(
                   accepted,
                   TerminalInputOptions.Default with { QueryLimits = limits }))
        {
            router.Route("\u001bP1+r524742=3234\u001b\\"u8);
        }

        accepted.CapabilityResponses.ShouldHaveSingleItem()
            .Items[CapabilityName.DirectColor].Length.ShouldBe(2);

        foreach (var value in new[]
                 {
                     "1+r524742=3234;6B63757531=1B5B41",
                     "1+r524742=323435"
                 })
        {
            var rejected = new RecordingProtocolSink();
            using var router = new ProtocolRouter(
                rejected,
                TerminalInputOptions.Default with { QueryLimits = limits });
            router.Route(Encoding.ASCII.GetBytes($"\u001bP{value}\u001b\\"));
            rejected.CapabilityResponses.ShouldBeEmpty(value);
            _ = rejected.Sequences.ShouldHaveSingleItem();
        }
    }

    /// <summary>Verifies identity-less failure remains typed and adjacent input recovers at every split.</summary>
    [Fact]
    public void Route_WhenCapabilityRequestFails_DeliversFailureAtEverySplit()
    {
        var input = "\u001bP0+r\u001b\\x"u8.ToArray();

        for (var split = 0; split <= input.Length; split++)
        {
            var sink = new RecordingProtocolSink();
            using var router = new ProtocolRouter(sink);
            router.Route(input.AsSpan(0, split));
            router.Route(input.AsSpan(split));

            var response = sink.CapabilityResponses.ShouldHaveSingleItem();
            response.Valid.ShouldBeFalse($"split {split}");
            response.Items.ShouldBeEmpty($"split {split}");
            sink.Text.ShouldHaveSingleItem().Value.ShouldBe(new Rune('x'), $"split {split}");
        }
    }

    /// <summary>
    /// Verifies a negative reply may echo the hex-encoded name(s) of the capabilities the
    /// terminal did not find, per xterm's ctlseqs.txt.
    /// </summary>
    [Fact]
    public void Route_WhenCapabilityRequestFailsAndEchoesTheName_DeliversFailureWithItsName()
    {
        var input = "\u001bP0+r524742\u001b\\"u8.ToArray();

        for (var split = 0; split <= input.Length; split++)
        {
            var sink = new RecordingProtocolSink();
            using var router = new ProtocolRouter(sink);
            router.Route(input.AsSpan(0, split));
            router.Route(input.AsSpan(split));

            var response = sink.CapabilityResponses.ShouldHaveSingleItem();
            response.Valid.ShouldBeFalse($"split {split}");
            response.Items.ShouldContainKey(CapabilityName.DirectColor, $"split {split}");
            response.Items[CapabilityName.DirectColor].Length.ShouldBe(0, $"split {split}");
        }
    }

    /// <summary>Verifies a representative tmux-wrapped reply reaches typed XTGETTCAP routing.</summary>
    [Fact]
    public void Route_WhenReplyWasWrappedByTmux_UnwrapsBeforeTypedCapabilityRouting()
    {
        var wrapped = new ArrayBufferWriter<byte>();
        TmuxWriter.WritePassthrough(wrapped, "\u001bP1+r524742=3234\u001b\\"u8);
        var inner = new ArrayBufferWriter<byte>();
        TmuxWriter.TryUnwrap(wrapped.WrittenSpan[2..^2], inner).ShouldBeTrue();
        var sink = new RecordingProtocolSink();
        using var router = new ProtocolRouter(sink);

        router.Route(inner.WrittenSpan);

        sink.CapabilityResponses.ShouldHaveSingleItem()
            .Items.ShouldContainKey(CapabilityName.DirectColor);
    }

    #endregion

    #region iTerm2 OSC 1337 routing

    /// <summary>Verifies multipart OSC 1337 is owned at every split for ST and BEL.</summary>
    [Theory]
    [InlineData("\u001b]1337;FileEnd\u001b\\", StringTerminator.EscapeBackslash)]
    [InlineData("\u001b]1337;FileEnd\u0007", StringTerminator.Bell)]
    public void Route_WhenMultipartOscIsFragmented_DeliversOwnedBoundedSequence(
        string value,
        StringTerminator terminator)
    {
        var bytes = Encoding.ASCII.GetBytes(value);

        for (var split = 0; split <= bytes.Length; split++)
        {
            var candidate = bytes.ToArray();
            var sink = new RecordingProtocolSink();
            using var router = new ProtocolRouter(sink);
            router.Route(candidate.AsSpan(0, split));
            router.Route(candidate.AsSpan(split));
            candidate.AsSpan().Clear();

            var sequence = sink.Sequences.ShouldHaveSingleItem($"split {split}");
            sequence.Kind.ShouldBe(SequenceKind.Osc);
            sequence.Payload.ToArray().ShouldBe("1337;FileEnd"u8.ToArray());
            sequence.Terminator.ShouldBe(terminator);
        }
    }

    /// <summary>Verifies oversized OSC 1337 is discarded and following text remains input.</summary>
    [Fact]
    public void Route_WhenMultipartOscExceedsBound_RecoversFollowingText()
    {
        var sink = new RecordingProtocolSink();
        var options = TerminalInputOptions.Default with { ParserLimits = ParserLimits.Default with { MaxStringBytes = 8 } };
        using var router = new ProtocolRouter(sink, options);

        router.Route("\u001b]1337;FileEnd\u001b\\ok"u8);

        sink.Diagnostics.ShouldContain(value =>
            value.Code == DiagnosticCode.StringLimit && value.Kind == SequenceKind.Osc);
        sink.Sequences.ShouldBeEmpty();
        sink.Text.Select(value => value.Value.ToString()).ShouldBe(["o", "k"]);
    }

    #endregion
}
