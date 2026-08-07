// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Protocols;


/// <summary>Verifies typed runtime routing, ownership, and recovery.</summary>
public sealed class RouterTests
{
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
    [InlineData("\u001b[12;34R", ResponseKind.CursorPosition)]
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
            _ = sink.Sequences.ShouldHaveSingleItem($"OSC recovery differed at split {split}.");
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
            InputOptions.Default with { PixelMouse = true });

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

    /// <summary>Verifies a legacy input-only sink observes an unsupported reply.</summary>
    [Fact]
    public void Decode_WhenSinkHandlesOnlyInput_ReportsUnsupportedReply()
    {
        // Arrange
        var sink = new RecordingInputSink();
        using InputDecoder decoder = new(sink);

        // Act
        decoder.Decode("\u001b[?1;2c"u8);

        // Assert
        var diagnostic = sink.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Code.ShouldBe(DiagnosticCode.Unsupported);
        diagnostic.Kind.ShouldBe(SequenceKind.Csi);
    }

    /// <summary>Verifies oversized strings recover into following user text.</summary>
    [Fact]
    public void Route_WhenStringExceedsLimit_ReportsAndRecoversFollowingText()
    {
        // Arrange
        var sink = new RecordingProtocolSink();
        var options = InputOptions.Default with { ParserLimits = ParserLimits.Default with { MaxStringBytes = 8 } };
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
}
