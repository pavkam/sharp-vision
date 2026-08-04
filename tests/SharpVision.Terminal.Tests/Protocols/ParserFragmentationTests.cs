// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Protocols;

/// <summary>
/// Verifies parser state across transport fragmentation and recovery boundaries.
/// </summary>
public sealed class ParserFragmentationTests
{
    /// <summary>
    /// Gets representative complete control sequences and adjacent text.
    /// </summary>
    public static TheoryData<byte[]> CompleteSequences =>
    [
        "\u001b7"u8.ToArray(),
        "\u001b(B"u8.ToArray(),
        "\u001b[H"u8.ToArray(),
        "\u001b[?25h"u8.ToArray(),
        "\u001b[38:2:1:2:3m"u8.ToArray(),
        "left\u001b[2Jright"u8.ToArray(),
        "a\u001b[1A\nb\u001b[?25l"u8.ToArray()
    ];

    /// <summary>
    /// Verifies every split and byte-at-a-time read against whole input.
    /// </summary>
    /// <param name="input">The representative complete sequence.</param>
    [Theory]
    [MemberData(nameof(CompleteSequences))]
    public void Parse_WhenSequenceIsFragmented_MatchesWholeInput(byte[] input) =>
        Fragmentation.AssertAll(input);

    /// <summary>
    /// Verifies parameter overflow reports once and resumes after the final byte.
    /// </summary>
    [Fact]
    public void Parse_WhenParameterLimitIsExceeded_ReportsOnceAndRecovers()
    {
        var limits = ParserLimits.Default with { MaxParameterBytes = 2 };
        using ProtocolParser parser = new(limits);
        var sink = new RecordingSink();

        parser.Parse("\u001b[123mX"u8, ref sink);

        sink.Observations.Count.ShouldBe(2);
        sink.Observations[0].Diagnostic!.Value.Code.ShouldBe(DiagnosticCode.ParameterLimit);
        sink.Observations[0].Diagnostic!.Value.DiscardedBytes.ShouldBe(1);
        sink.Observations[1].First.ShouldBe("X"u8.ToArray());
    }

    /// <summary>
    /// Verifies intermediate overflow reports once and resumes after the final byte.
    /// </summary>
    [Fact]
    public void Parse_WhenIntermediateLimitIsExceeded_ReportsOnceAndRecovers()
    {
        var limits = ParserLimits.Default with { MaxIntermediateBytes = 1 };
        using ProtocolParser parser = new(limits);
        var sink = new RecordingSink();

        parser.Parse("\u001b($BX"u8, ref sink);

        sink.Observations.Count.ShouldBe(2);
        sink.Observations[0].Diagnostic!.Value.Code.ShouldBe(DiagnosticCode.IntermediateLimit);
        sink.Observations[1].First.ShouldBe("X"u8.ToArray());
    }

    /// <summary>
    /// Verifies a CSI sequence ignored for exceeding its parameter limit recovers at a new
    /// Escape introducer, with no final byte ever ending the ignored sequence first (see #266 and
    /// docs/protocols/ecma-48.md's "streaming grammar" section).
    /// </summary>
    [Fact]
    public void Parse_WhenIgnoredCsiIsFollowedByNewEscape_RecoversAtIntroducer()
    {
        var limits = ParserLimits.Default with { MaxParameterBytes = 2 };
        using ProtocolParser parser = new(limits);
        var sink = new RecordingSink();

        // "123" exceeds the two-byte parameter limit on its third byte, entering CsiIgnore;
        // no CSI final byte (0x40..0x7e) ever appears before the next ESC arrives.
        parser.Parse("\u001b[123\u001b[A"u8, ref sink);

        sink.Observations.Count.ShouldBe(2);
        sink.Observations[0].Diagnostic!.Value.Code.ShouldBe(DiagnosticCode.ParameterLimit);
        sink.Observations[1].Type.ShouldBe("Csi");
        sink.Observations[1].Final.ShouldBe((byte) 'A');
        sink.Observations[1].First.ShouldBeEmpty();
    }

    /// <summary>
    /// Verifies an Escape sequence ignored for exceeding its intermediate limit recovers at a new
    /// Escape introducer, with no final byte ever ending the ignored sequence first (see #266).
    /// </summary>
    [Fact]
    public void Parse_WhenIgnoredEscapeIsFollowedByNewEscape_RecoversAtIntroducer()
    {
        var limits = ParserLimits.Default with { MaxIntermediateBytes = 1 };
        using ProtocolParser parser = new(limits);
        var sink = new RecordingSink();

        // The second '$' exceeds the one-byte intermediate limit, entering EscapeIgnore; no
        // Escape final byte ever appears before the next ESC arrives.
        parser.Parse("\u001b($$\u001b[A"u8, ref sink);

        sink.Observations.Count.ShouldBe(2);
        sink.Observations[0].Diagnostic!.Value.Code.ShouldBe(DiagnosticCode.IntermediateLimit);
        sink.Observations[1].Type.ShouldBe("Csi");
        sink.Observations[1].Final.ShouldBe((byte) 'A');
    }

    /// <summary>
    /// Verifies parameters after CSI intermediates are malformed and recoverable.
    /// </summary>
    [Fact]
    public void Parse_WhenParameterFollowsIntermediate_ReportsAndRecovers()
    {
        using ProtocolParser parser = new();
        var sink = new RecordingSink();

        parser.Parse("\u001b[$1pX"u8, ref sink);

        sink.Observations.Count.ShouldBe(2);
        sink.Observations[0].Diagnostic!.Value.Code.ShouldBe(DiagnosticCode.Malformed);
        sink.Observations[1].First.ShouldBe("X"u8.ToArray());
    }

    /// <summary>
    /// Verifies end-of-stream truncation is reported exactly once.
    /// </summary>
    [Fact]
    public void Complete_WhenSequenceIsTruncated_ReportsOnceAndReturnsToGround()
    {
        using ProtocolParser parser = new();
        var sink = new RecordingSink();

        parser.Parse("\u001b[12"u8, ref sink);
        parser.Complete(ref sink);
        parser.Complete(ref sink);
        parser.Parse("X"u8, ref sink);

        sink.Observations.Count.ShouldBe(2);
        sink.Observations[0].Diagnostic!.Value.Code.ShouldBe(DiagnosticCode.Truncated);
        sink.Observations[0].Diagnostic!.Value.Kind.ShouldBe(SequenceKind.Csi);
        sink.Observations[1].First.ShouldBe("X"u8.ToArray());
    }

    /// <summary>
    /// Verifies reset discards partial state and restarts the stream offset.
    /// </summary>
    [Fact]
    public void Reset_WhenSequenceIsPartial_DiscardsWithoutDiagnostic()
    {
        using ProtocolParser parser = new();
        var sink = new RecordingSink();

        parser.Parse("\u001b[12"u8, ref sink);
        parser.Reset();
        parser.Parse("X"u8, ref sink);

        sink.Observations.ShouldHaveSingleItem().First.ShouldBe("X"u8.ToArray());
        parser.Offset.ShouldBe(1);
    }

    /// <summary>
    /// Verifies a malformed sequence cannot desynchronize a following known CSI.
    /// </summary>
    [Fact]
    public void Parse_WhenMalformedCsiPrecedesKnownCsi_RecoversKnownSequence()
    {
        using ProtocolParser parser = new();
        var sink = new RecordingSink();
        byte[] input =
        [
            0x1b,
            (byte) '[',
            0xff,
            0x1b,
            (byte) '[',
            (byte) '2',
            (byte) 'J'
        ];

        parser.Parse(input, ref sink);

        sink.Observations.Count.ShouldBe(2);
        sink.Observations[0].Diagnostic!.Value.Code.ShouldBe(DiagnosticCode.Malformed);
        sink.Observations[1].Type.ShouldBe("Csi");
        sink.Observations[1].First.ShouldBe("2"u8.ToArray());
        sink.Observations[1].Final.ShouldBe((byte) 'J');
    }

    /// <summary>
    /// Verifies lifecycle methods reject use after idempotent disposal.
    /// </summary>
    [Fact]
    public void Dispose_WhenCalled_PreventsFurtherUseAndRemainsIdempotent()
    {
        var parser = new ProtocolParser();
        var sink = new RecordingSink();

        parser.Dispose();
        parser.Dispose();

        _ = Should.Throw<ObjectDisposedException>(() => parser.Parse([], ref sink));
        _ = Should.Throw<ObjectDisposedException>(() => parser.Complete(ref sink));
        _ = Should.Throw<ObjectDisposedException>(parser.Reset);
    }
}
