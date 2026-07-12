using SharpVision.Terminal.Protocols;
using SharpVision.Terminal.Tests.Support;

using Shouldly;

using InputOptions = SharpVision.Terminal.Input.Options;

namespace SharpVision.Terminal.Tests.Protocols;

/// <summary>Verifies typed runtime routing, ownership, and recovery.</summary>
public sealed class RouterTests
{
    /// <summary>Gets representative values for every bounded string family.</summary>
    public static TheoryData<byte[], SequenceKind> StringCases { get; } = new()
    {
        { "\u001b]777;payload\u001b\\"u8.ToArray(), SequenceKind.Osc },
        { "\u001bP1;2$qpayload\u001b\\"u8.ToArray(), SequenceKind.Dcs },
        { "\u001b_Gpayload\u001b\\"u8.ToArray(), SequenceKind.Apc },
        { "\u001b^payload\u001b\\"u8.ToArray(), SequenceKind.Pm },
        { "\u001bXpayload\u001b\\"u8.ToArray(), SequenceKind.Sos },
        { "\u001b]777;payload\u0007"u8.ToArray(), SequenceKind.Osc },
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
    [InlineData("\u001b]10;rgb:ffff/0000/8080\u001b\\", ResponseKind.ForegroundColor)]
    public void Route_WhenReplyIsFragmented_DeliversTypedResponse(
        string input,
        ResponseKind expected)
    {
        // Arrange
        var bytes = System.Text.Encoding.UTF8.GetBytes(input);

        // Act / Assert
        for (var split = 0; split <= bytes.Length; split++)
        {
            var sink = new RecordingProtocolSink();
            using var router = new ProtocolRouter(sink);
            router.Route(bytes.AsSpan(0, split));
            router.Route(bytes.AsSpan(split));

            sink.Responses.ShouldHaveSingleItem($"The reply differed at split {split}.").Kind.ShouldBe(expected);
            sink.Sequences.ShouldBeEmpty();
            sink.Strokes.ShouldBeEmpty();
            sink.Text.ShouldBeEmpty();
        }
    }

    /// <summary>Verifies DCS headers and payload outlive the source read.</summary>
    [Fact]
    public void Route_WhenDcsCompletes_OwnsHeaderAndPayload()
    {
        // Arrange
        var sink = new RecordingProtocolSink();
        using var router = new ProtocolRouter(sink);
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
        using (var expectedRouter = new ProtocolRouter(expectedSink))
        {
            expectedRouter.Route(input);
        }

        var expected = expectedSink.Sequences.ShouldHaveSingleItem();

        // Act / Assert
        for (var split = 0; split <= input.Length; split++)
        {
            var sink = new RecordingProtocolSink();
            using var router = new ProtocolRouter(sink);
            router.Route(input.AsSpan(0, split));
            router.Route(input.AsSpan(split));

            var actual = sink.Sequences.ShouldHaveSingleItem(
                $"The sequence differed at split {split}.");
            actual.Kind.ShouldBe(kind);
            AssertEquivalent(expected, actual);
        }

        var byteSink = new RecordingProtocolSink();
        using var byteRouter = new ProtocolRouter(byteSink);

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
        using var router = new ProtocolRouter(sink);

        // Act
        router.Route("\u001b[?1;2cx"u8);

        // Assert
        sink.Order.ShouldBe(["response", "key", "text"]);
    }

    /// <summary>Verifies a legacy input-only sink observes an unsupported reply.</summary>
    [Fact]
    public void Decode_WhenSinkHandlesOnlyInput_ReportsUnsupportedReply()
    {
        // Arrange
        var sink = new RecordingInputSink();
        using var decoder = new Terminal.Input.Decoder(sink);

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
        var options = InputOptions.Default with
        {
            Limits = Limits.Default with { MaxStringBytes = 8 },
        };
        using var router = new ProtocolRouter(sink, options);

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
        using var router = new ProtocolRouter(sink);

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
        using var router = new ProtocolRouter(sink);
        router.Route("\u001bP1;2$qpartial"u8);

        // Act
        router.Complete();
        router.Complete();

        // Assert
        sink.Diagnostics.Count(value =>
            value.Code == DiagnosticCode.Truncated &&
            value.Kind == SequenceKind.Dcs).ShouldBe(1);
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
