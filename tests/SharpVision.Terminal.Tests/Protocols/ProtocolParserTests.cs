// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Protocols;

/// <summary>
/// Verifies <see cref="ProtocolParser"/> behavior across ground, C0/ESC control, CSI, DCS,
/// and generic string (OSC/APC/PM/SOS) parsing, including fragmentation and randomized
/// invariants.
/// </summary>
public sealed class ProtocolParserTests
{
    #region Ground, C0, and ESC

    /// <summary>
    /// Verifies that UTF-8 bytes are delivered without char-based reinterpretation.
    /// </summary>
    [Fact]
    public void Parse_WhenInputIsUtf8Text_DeliversBorrowedBytes()
    {
        using ProtocolParser parser = new();
        var sink = new RecordingSink();
        var input = "A🦄"u8.ToArray();

        parser.Parse(input, ref sink);

        sink.Observations.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            observation => observation.Type.ShouldBe("Text"),
            observation => observation.First.ShouldBe(input));
        parser.Offset.ShouldBe(input.Length);
    }

    /// <summary>
    /// Verifies that C0 controls split adjacent text runs.
    /// </summary>
    [Fact]
    public void Parse_WhenInputContainsC0Control_DeliversOrderedEvents()
    {
        using ProtocolParser parser = new();
        var sink = new RecordingSink();

        parser.Parse("a\nb"u8, ref sink);

        sink.Observations.Count.ShouldBe(3);
        sink.Observations[0].Type.ShouldBe("Text");
        sink.Observations[0].First.ShouldBe("a"u8.ToArray());
        sink.Observations[1].Type.ShouldBe("Control");
        sink.Observations[1].First.ShouldBe([(byte) '\n']);
        sink.Observations[2].Type.ShouldBe("Text");
        sink.Observations[2].First.ShouldBe("b"u8.ToArray());
    }

    /// <summary>
    /// Verifies an ESC sequence with no intermediates.
    /// </summary>
    [Fact]
    public void Parse_WhenEscapeHasFinal_DeliversEscape()
    {
        using ProtocolParser parser = new();
        var sink = new RecordingSink();

        parser.Parse("\u001b7"u8, ref sink);

        sink.Observations.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            observation => observation.Type.ShouldBe("Escape"),
            observation => observation.First.ShouldBeEmpty(),
            observation => observation.Final.ShouldBe((byte) '7'));
    }

    /// <summary>
    /// Verifies an ESC sequence with an intermediate byte.
    /// </summary>
    [Fact]
    public void Parse_WhenEscapeHasIntermediate_DeliversIntermediateAndFinal()
    {
        using ProtocolParser parser = new();
        var sink = new RecordingSink();

        parser.Parse("\u001b(B"u8, ref sink);

        sink.Observations.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            observation => observation.Type.ShouldBe("Escape"),
            observation => observation.First.ShouldBe("("u8.ToArray()),
            observation => observation.Final.ShouldBe((byte) 'B'));
    }

    /// <summary>
    /// Verifies that raw C1-looking bytes remain UTF-8 data by default.
    /// </summary>
    [Fact]
    public void Parse_WhenEightBitControlsAreDisabled_DeliversBytesAsText()
    {
        using ProtocolParser parser = new();
        var sink = new RecordingSink();
        byte[] input = [0xdb, 0x9b, (byte) '3', (byte) 'A'];

        parser.Parse(input, ref sink);

        sink.Observations.ShouldHaveSingleItem().First.ShouldBe(input);
    }

    /// <summary>
    /// Verifies that the optional eight-bit CSI introducer is recognized.
    /// </summary>
    [Fact]
    public void Parse_WhenEightBitControlsAreEnabled_DeliversCsi()
    {
        using ProtocolParser parser = new(ParserLimits.Default with { AcceptEightBitControls = true });
        var sink = new RecordingSink();
        byte[] input = [0x9b, (byte) '3', (byte) 'A'];

        parser.Parse(input, ref sink);

        sink.Observations.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            observation => observation.Type.ShouldBe("Csi"),
            observation => observation.First.ShouldBe("3"u8.ToArray()),
            observation => observation.Final.ShouldBe((byte) 'A'));
    }

    /// <summary>
    /// Verifies a configured non-introducer C1 byte remains observable.
    /// </summary>
    [Fact]
    public void Parse_WhenEightBitC1IsEnabled_DeliversControl()
    {
        using ProtocolParser parser = new(ParserLimits.Default with { AcceptEightBitControls = true });
        var sink = new RecordingSink();

        parser.Parse([0x85], ref sink);

        sink.Observations.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            observation => observation.Type.ShouldBe("Control"),
            observation => observation.First.ShouldBe([0x85]));
    }

    #endregion

    #region CSI streaming and cancellation

    /// <summary>
    /// Verifies an empty CSI header.
    /// </summary>
    [Fact]
    public void Parse_WhenCsiHasNoParameters_DeliversFinal()
    {
        using ProtocolParser parser = new();
        var sink = new RecordingSink();

        parser.Parse("\u001b[H"u8, ref sink);

        sink.Observations.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            observation => observation.Type.ShouldBe("Csi"),
            observation => observation.First.ShouldBeEmpty(),
            observation => observation.Second.ShouldBeEmpty(),
            observation => observation.Final.ShouldBe((byte) 'H'));
    }

    /// <summary>
    /// Verifies that private markers remain part of the raw parameter grammar.
    /// </summary>
    [Fact]
    public void Parse_WhenCsiIsPrivate_PreservesParameterBytes()
    {
        using ProtocolParser parser = new();
        var sink = new RecordingSink();

        parser.Parse("\u001b[?25h"u8, ref sink);

        sink.Observations.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            observation => observation.First.ShouldBe("?25"u8.ToArray()),
            observation => observation.Final.ShouldBe((byte) 'h'));
    }

    /// <summary>
    /// Verifies that colon subparameters are preserved verbatim.
    /// </summary>
    [Fact]
    public void Parse_WhenCsiHasSubparameters_PreservesColonBytes()
    {
        using ProtocolParser parser = new();
        var sink = new RecordingSink();

        parser.Parse("\u001b[38:2:1:2:3m"u8, ref sink);

        sink.Observations.ShouldHaveSingleItem().First.ShouldBe("38:2:1:2:3"u8.ToArray());
    }

    /// <summary>
    /// Verifies multiple text, control, and CSI events from one read.
    /// </summary>
    [Fact]
    public void Parse_WhenReadContainsMultipleEvents_DeliversInOrder()
    {
        using ProtocolParser parser = new();
        var sink = new RecordingSink();

        parser.Parse("a\u001b[2J\nb"u8, ref sink);

        sink.Observations.Select(static value => value.Type).ShouldBe(
            ["Text", "Csi", "Control", "Text"]);
    }

    /// <summary>
    /// Verifies CAN cancellation and immediate ground-state recovery.
    /// </summary>
    [Fact]
    public void Parse_WhenCanCancelsCsi_ReportsAndRecoversToText()
    {
        using ProtocolParser parser = new();
        var sink = new RecordingSink();
        byte[] input = [0x1b, (byte) '[', (byte) '1', (byte) '2', 0x18, (byte) 'x'];

        parser.Parse(input, ref sink);

        sink.Observations.Count.ShouldBe(2);
        _ = sink.Observations[0].Diagnostic.ShouldNotBeNull();
        sink.Observations[0].Diagnostic!.Value.Code.ShouldBe(DiagnosticCode.Cancelled);
        sink.Observations[0].Diagnostic!.Value.Kind.ShouldBe(SequenceKind.Csi);
        sink.Observations[1].Type.ShouldBe("Text");
        sink.Observations[1].First.ShouldBe("x"u8.ToArray());
    }

    /// <summary>
    /// Verifies a fresh ESC restarting a sequence while mid-header in a non-ignoring state
    /// reports the abandoned sequence exactly like CAN/SUB cancellation does, rather than
    /// silently discarding it.
    /// </summary>
    [Fact]
    public void Parse_WhenEscRestartsMidHeaderCsi_ReportsCancelled()
    {
        using ProtocolParser parser = new();
        var sink = new RecordingSink();
        byte[] input = [0x1b, (byte) '[', (byte) '1', 0x1b, (byte) 'A'];

        parser.Parse(input, ref sink);

        sink.Observations.Count.ShouldBe(2);
        _ = sink.Observations[0].Diagnostic.ShouldNotBeNull();
        sink.Observations[0].Diagnostic!.Value.Code.ShouldBe(DiagnosticCode.Cancelled);
        sink.Observations[0].Diagnostic!.Value.Kind.ShouldBe(SequenceKind.Csi);
        sink.Observations[1].Type.ShouldBe("Escape");
        sink.Observations[1].Final.ShouldBe((byte) 'A');
    }

    #endregion

    #region DCS header, payload, termination, and recovery

    /// <summary>Verifies raw DCS header fields and payload.</summary>
    [Fact]
    public void Parse_WhenDcsIsComplete_DeliversHeaderAndPayload()
    {
        using ProtocolParser parser = new();
        var sink = new RecordingSink();

        parser.Parse("\u001bP1;2$qdata\u001b\\"u8, ref sink);

        sink.Observations.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            observation => observation.Type.ShouldBe("Dcs:EscapeBackslash:24"),
            observation => observation.First.ShouldBe("1;2"u8.ToArray()),
            observation => observation.Second.ShouldBe("data"u8.ToArray()),
            observation => observation.Final.ShouldBe((byte) 'q'));
    }

    /// <summary>Verifies BEL is forbidden DCS payload rather than a terminator.</summary>
    [Fact]
    public void Parse_WhenDcsContainsBell_ReportsMalformedAtSt()
    {
        using ProtocolParser parser = new();
        var sink = new RecordingSink();

        parser.Parse("\u001bPqa\ab\u001b\\"u8, ref sink);

        sink.Observations.ShouldHaveSingleItem().Diagnostic!.Value.Code.ShouldBe(DiagnosticCode.Malformed);
    }

    /// <summary>Verifies DCS state survives every possible transport split.</summary>
    [Fact]
    public void Parse_WhenDcsIsFragmented_MatchesWholeInput() =>
        Fragmentation.AssertAll("left\u001bP1;2$qdata\u001b\\right"u8);

    /// <summary>Verifies DCS header overflow ignores through ST, not just final.</summary>
    [Fact]
    public void Parse_WhenDcsHeaderExceedsLimit_ReportsAtStAndRecovers()
    {
        var limits = ParserLimits.Default with { MaxParameterBytes = 2 };
        using ProtocolParser parser = new(limits);
        var sink = new RecordingSink();

        parser.Parse("\u001bP123qsecret\u001b\\X"u8, ref sink);

        sink.Observations.Count.ShouldBe(2);
        sink.Observations[0].Diagnostic!.Value.Code.ShouldBe(DiagnosticCode.ParameterLimit);
        sink.Observations[0].Diagnostic!.Value.Kind.ShouldBe(SequenceKind.Dcs);
        sink.Observations[1].First.ShouldBe("X"u8.ToArray());
    }

    /// <summary>
    /// Verifies a DEL byte arriving during DcsHeaderIgnore recovery is counted toward
    /// DiscardedBytes rather than silently absorbed.
    /// </summary>
    [Fact]
    public void Parse_WhenDelOccursDuringDcsHeaderIgnore_CountsItAsDiscarded()
    {
        var limits = ParserLimits.Default with { MaxParameterBytes = 2 };
        using ProtocolParser parser = new(limits);
        var sink = new RecordingSink();

        // "123" exceeds the two-byte parameter limit on its third byte, entering
        // DcsHeaderIgnore; the DEL that follows must still be counted before the final byte
        // 'q' transitions to StringIgnore and 'secret' is discarded until CAN cancels it.
        byte[] input =
        [
            0x1b, (byte) 'P', (byte) '1', (byte) '2', (byte) '3', 0x7f, (byte) 'q',
            (byte) 's', (byte) 'e', (byte) 'c', (byte) 'r', (byte) 'e', (byte) 't',
            0x18, (byte) 'X'
        ];

        parser.Parse(input, ref sink);

        sink.Observations.Count.ShouldBe(2);
        sink.Observations[0].Diagnostic!.Value.Code.ShouldBe(DiagnosticCode.ParameterLimit);
        sink.Observations[0].Diagnostic!.Value.DiscardedBytes.ShouldBe(8);
        sink.Observations[1].First.ShouldBe("X"u8.ToArray());
    }

    /// <summary>Verifies a string payload limit clears content and recovers at ST.</summary>
    [Fact]
    public void Parse_WhenStringPayloadExceedsLimit_ReportsAtStAndRecovers()
    {
        var limits = ParserLimits.Default with { MaxStringBytes = 4 };
        using ProtocolParser parser = new(limits);
        var sink = new RecordingSink();

        parser.Parse("\u001b]12345\u001b\\X"u8, ref sink);

        sink.Observations.Count.ShouldBe(2);
        sink.Observations[0].Diagnostic!.Value.Code.ShouldBe(DiagnosticCode.StringLimit);
        sink.Observations[0].Diagnostic!.Value.DiscardedBytes.ShouldBe(1);
        sink.Observations[1].First.ShouldBe("X"u8.ToArray());
    }

    /// <summary>
    /// Verifies a false ESC candidate (one that does not complete an ST
    /// terminator) counts toward DiscardedBytes, and that ST following it
    /// still terminates recovery correctly.
    /// </summary>
    [Fact]
    public void Parse_WhenIgnoredStringHasFalseEscCandidate_CountsItAsDiscarded()
    {
        var limits = ParserLimits.Default with { MaxStringBytes = 4 };
        using ProtocolParser parser = new(limits);
        var sink = new RecordingSink();

        parser.Parse("\u001b]12345X\u001b\\Y"u8, ref sink);

        sink.Observations.Count.ShouldBe(2);
        sink.Observations[0].Diagnostic!.Value.Code.ShouldBe(DiagnosticCode.StringLimit);
        sink.Observations[0].Diagnostic!.Value.DiscardedBytes.ShouldBe(2);
        sink.Observations[1].First.ShouldBe("Y"u8.ToArray());
    }

    /// <summary>
    /// Verifies an ignored oversized string does NOT recover at a new control introducer,
    /// unlike CSI and Escape ignore states: an embedded "ESC [ A" is swallowed as ordinary
    /// (non-terminating) payload search, and only a genuine ST ends the ignore, as described in
    /// docs/protocols/ecma-48.md's "streaming grammar" section.
    /// </summary>
    [Fact]
    public void Parse_WhenIgnoredStringIsFollowedByFakeIntroducer_DoesNotRecoverUntilRealTerminator()
    {
        var limits = ParserLimits.Default with { MaxStringBytes = 4 };
        using ProtocolParser parser = new(limits);
        var sink = new RecordingSink();

        // "12345" exceeds the four-byte string limit on its fifth byte, entering StringIgnore.
        // "\u001b[A" that follows is not a real ST and must not restart the parser as a new CSI.
        parser.Parse("\u001b]12345\u001b[A\u001b\\Z"u8, ref sink);

        sink.Observations.Count.ShouldBe(2);
        sink.Observations[0].Diagnostic!.Value.Code.ShouldBe(DiagnosticCode.StringLimit);
        sink.Observations.ShouldNotContain(observation => observation.Type == "Csi");
        sink.Observations[1].Type.ShouldBe("Text");
        sink.Observations[1].First.ShouldBe("Z"u8.ToArray());
    }

    /// <summary>
    /// Verifies every-fragment-split coverage for a false ESC candidate
    /// followed by a genuine ST terminator.
    /// </summary>
    [Fact]
    public void Parse_WhenIgnoredStringHasFalseEscCandidate_MatchesWholeInputAcrossSplits() =>
        Fragmentation.AssertAll(
            "left\u001b]12345X\u001b\\right"u8,
            limits: ParserLimits.Default with { MaxStringBytes = 4 });

    /// <summary>
    /// Verifies a trailing ESC candidate still open at end-of-stream counts
    /// toward DiscardedBytes instead of being silently dropped.
    /// </summary>
    [Fact]
    public void Complete_WhenIgnoredStringEndsOnOpenEscCandidate_CountsItAsDiscarded()
    {
        var limits = ParserLimits.Default with { MaxStringBytes = 4 };
        using ProtocolParser parser = new(limits);
        var sink = new RecordingSink();

        parser.Parse("\u001b]12345\u001b"u8, ref sink);
        parser.Complete(ref sink);

        sink.Observations.ShouldHaveSingleItem().Diagnostic!.Value.ShouldSatisfyAllConditions(
            diagnostic => diagnostic.Code.ShouldBe(DiagnosticCode.StringLimit),
            diagnostic => diagnostic.DiscardedBytes.ShouldBe(2));
    }

    /// <summary>
    /// Verifies BEL still terminates OSC recovery when it immediately follows
    /// a false ESC candidate, rather than being swallowed as ordinary payload.
    /// </summary>
    [Fact]
    public void Parse_WhenBellFollowsFalseEscCandidateInIgnoredOsc_TerminatesRecovery()
    {
        var limits = ParserLimits.Default with { MaxStringBytes = 4 };
        using ProtocolParser parser = new(limits);
        var sink = new RecordingSink();

        parser.Parse("\u001b]12345X\u001b\aY"u8, ref sink);

        sink.Observations.Count.ShouldBe(2);
        sink.Observations[0].Diagnostic!.Value.Code.ShouldBe(DiagnosticCode.StringLimit);
        sink.Observations[0].Diagnostic!.Value.DiscardedBytes.ShouldBe(3);
        sink.Observations[1].First.ShouldBe("Y"u8.ToArray());
    }

    /// <summary>
    /// Verifies an enabled C1 ST still terminates recovery when it
    /// immediately follows a false ESC candidate.
    /// </summary>
    [Fact]
    public void Parse_WhenEightBitStFollowsFalseEscCandidateInIgnoredString_TerminatesRecovery()
    {
        var limits = ParserLimits.Default with { MaxStringBytes = 4, AcceptEightBitControls = true };
        using ProtocolParser parser = new(limits);
        var sink = new RecordingSink();
        // 0x9c is the raw C1 ST byte; a string literal would UTF-8 encode
        // it as two bytes instead of the single byte the parser expects.
        byte[] input = [.. "\u001b]12345X"u8.ToArray(), 0x1b, 0x9c, .. "Y"u8.ToArray()];

        parser.Parse(input, ref sink);

        sink.Observations.Count.ShouldBe(2);
        sink.Observations[0].Diagnostic!.Value.Code.ShouldBe(DiagnosticCode.StringLimit);
        sink.Observations[0].Diagnostic!.Value.DiscardedBytes.ShouldBe(3);
        sink.Observations[1].First.ShouldBe("Y"u8.ToArray());
    }

    /// <summary>Verifies repeated false ESC candidates each count as discarded.</summary>
    [Fact]
    public void Parse_WhenIgnoredStringHasRepeatedFalseEscCandidates_CountsEachOnce()
    {
        var limits = ParserLimits.Default with { MaxStringBytes = 4 };
        using ProtocolParser parser = new(limits);
        var sink = new RecordingSink();

        parser.Parse("\u001b]12345X\u001bY\u001b\\Z"u8, ref sink);

        sink.Observations.Count.ShouldBe(2);
        sink.Observations[0].Diagnostic!.Value.Code.ShouldBe(DiagnosticCode.StringLimit);
        sink.Observations[0].Diagnostic!.Value.DiscardedBytes.ShouldBe(4);
        sink.Observations[1].First.ShouldBe("Z"u8.ToArray());
    }

    #endregion

    #region Fragmentation and recovery boundaries

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
    /// Verifies a DEL byte arriving during CsiIgnore recovery is counted toward
    /// DiscardedBytes rather than silently absorbed.
    /// </summary>
    [Fact]
    public void Parse_WhenDelOccursDuringCsiIgnore_CountsItAsDiscarded()
    {
        var limits = ParserLimits.Default with { MaxParameterBytes = 2 };
        using ProtocolParser parser = new(limits);
        var sink = new RecordingSink();

        // "123" exceeds the two-byte parameter limit on its third byte, entering CsiIgnore;
        // the DEL that follows must still be counted before the final byte 'm' ends recovery.
        byte[] input =
        [
            0x1b, (byte) '[', (byte) '1', (byte) '2', (byte) '3', 0x7f, (byte) 'm', (byte) 'X'
        ];

        parser.Parse(input, ref sink);

        sink.Observations.Count.ShouldBe(2);
        sink.Observations[0].Diagnostic!.Value.Code.ShouldBe(DiagnosticCode.ParameterLimit);
        sink.Observations[0].Diagnostic!.Value.DiscardedBytes.ShouldBe(2);
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
    /// Verifies a DEL byte arriving during EscapeIgnore recovery is counted toward
    /// DiscardedBytes rather than silently absorbed.
    /// </summary>
    [Fact]
    public void Parse_WhenDelOccursDuringEscapeIgnore_CountsItAsDiscarded()
    {
        var limits = ParserLimits.Default with { MaxIntermediateBytes = 1 };
        using ProtocolParser parser = new(limits);
        var sink = new RecordingSink();

        // '$' exceeds the one-byte intermediate limit already filled by '(', entering
        // EscapeIgnore; the DEL that follows must still be counted before the final byte
        // 'B' ends recovery.
        byte[] input =
        [
            0x1b, (byte) '(', (byte) '$', 0x7f, (byte) 'B', (byte) 'X'
        ];

        parser.Parse(input, ref sink);

        sink.Observations.Count.ShouldBe(2);
        sink.Observations[0].Diagnostic!.Value.Code.ShouldBe(DiagnosticCode.IntermediateLimit);
        sink.Observations[0].Diagnostic!.Value.DiscardedBytes.ShouldBe(2);
        sink.Observations[1].First.ShouldBe("X"u8.ToArray());
    }

    /// <summary>
    /// Verifies a CSI sequence ignored for exceeding its parameter limit recovers at a new
    /// Escape introducer, with no final byte ever ending the ignored sequence first, as described in
    /// docs/protocols/ecma-48.md's "streaming grammar" section.
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
    /// Escape introducer, with no final byte ever ending the ignored sequence first.
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

    #endregion

    #region Randomized invariants

    private const int _validSeed = 0x51A2;
    private const int _hostileSeed = 0xC015;

    /// <summary>
    /// Verifies generated valid sequences are equivalent at every fragmentation.
    /// </summary>
    [Fact]
    public void Parse_WhenValidSequencesAreRandomized_MatchesEveryFragmentation()
    {
        var random = new Random(_validSeed);

        for (var index = 0; index < 64; index++)
        {
            var input = CreateValid(random);

            try
            {
                Fragmentation.AssertAll(input);
            }
            catch (ShouldAssertException exception)
            {
                throw new InvalidOperationException(
                    $"Valid parser seed {_validSeed}, case {index}, bytes {Convert.ToHexString(input)}.",
                    exception);
            }
        }
    }

    /// <summary>
    /// Verifies arbitrary bytes cannot prevent recovery into a known trailing CSI.
    /// </summary>
    [Fact]
    public void Parse_WhenHostileBytesAreRandomized_RecoversKnownTrailingCsi()
    {
        var random = new Random(_hostileSeed);

        for (var index = 0; index < 256; index++)
        {
            var input = new byte[69];
            random.NextBytes(input.AsSpan(0, 64));
            input[64] = 0x18;
            "\u001b[2J"u8.CopyTo(input.AsSpan(65));
            using ProtocolParser parser = new(ParserLimits.Default with
            {
                MaxParameterBytes = 16,
                MaxIntermediateBytes = 4,
                MaxStringBytes = 64
            });
            var sink = new RecordingSink();

            parser.Parse(input, ref sink);
            parser.Complete(ref sink);

            sink.Observations.ShouldContain(
                static observation =>
                    observation.Type == "Csi" &&
                    observation.Final == (byte) 'J' &&
                    observation.First.Length == 1 &&
                    observation.First[0] == (byte) '2',
                $"Hostile parser seed {_hostileSeed}, case {index}.");
            parser.Offset.ShouldBe(input.Length);
        }
    }

    private static byte[] CreateValid(Random random)
    {
        var selector = random.Next(4);
        var value = random.Next(1, 10_000);

        return selector switch
        {
            0 => Encoding.ASCII.GetBytes($"left\u001b[{value}Aright"),
            1 => Encoding.ASCII.GetBytes($"\u001b]2;title-{value}\u001b\\"),
            2 => Encoding.ASCII.GetBytes($"\u001bP{value}$qdata-{value}\u001b\\"),
            _ => Encoding.ASCII.GetBytes($"\u001b_payload-{value}\u001b\\")
        };
    }

    #endregion

    #region Bounded OSC/APC/PM/SOS

    /// <summary>Verifies OSC payload and canonical ST termination.</summary>
    [Fact]
    public void Parse_WhenOscEndsWithSt_DeliversPayloadAndTerminator()
    {
        using ProtocolParser parser = new();
        var sink = new RecordingSink();

        parser.Parse("\u001b]2;title\u001b\\"u8, ref sink);

        sink.Observations.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            observation => observation.Type.ShouldBe("Osc:EscapeBackslash"),
            observation => observation.First.ShouldBe("2;title"u8.ToArray()));
    }

    /// <summary>Verifies enabled BEL termination for OSC.</summary>
    [Fact]
    public void Parse_WhenOscEndsWithAllowedBell_DeliversBellTerminator()
    {
        using ProtocolParser parser = new();
        var sink = new RecordingSink();

        parser.Parse("\u001b]2;title\a"u8, ref sink);

        sink.Observations.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            observation => observation.Type.ShouldBe("Osc:Bell"),
            observation => observation.First.ShouldBe("2;title"u8.ToArray()));
    }

    /// <summary>Verifies disabled BEL termination fails without exposing data.</summary>
    [Fact]
    public void Parse_WhenOscContainsDisallowedBell_ReportsMalformedAtSt()
    {
        var limits = ParserLimits.Default with { AcceptBellTerminatedOsc = false };
        using ProtocolParser parser = new(limits);
        var sink = new RecordingSink();

        parser.Parse("\u001b]2;secret\aignored\u001b\\X"u8, ref sink);

        sink.Observations.Count.ShouldBe(2);
        sink.Observations[0].Diagnostic!.Value.Code.ShouldBe(DiagnosticCode.Malformed);
        sink.Observations[0].Diagnostic!.Value.Kind.ShouldBe(SequenceKind.Osc);
        sink.Observations[0].Diagnostic!.Value.ToString().ShouldNotContain("secret");
        sink.Observations[1].First.ShouldBe("X"u8.ToArray());
    }

    /// <summary>Verifies generic string introducers and ST termination.</summary>
    /// <param name="input">The complete string sequence.</param>
    /// <param name="kind">The expected sequence family.</param>
    [Theory]
    [InlineData("\u001b_payload\u001b\\", SequenceKind.Apc)]
    [InlineData("\u001b^payload\u001b\\", SequenceKind.Pm)]
    [InlineData("\u001bXpayload\u001b\\", SequenceKind.Sos)]
    public void Parse_WhenGenericStringEndsWithSt_DeliversPayload(
        string input,
        SequenceKind kind)
    {
        using ProtocolParser parser = new();
        var sink = new RecordingSink();

        parser.Parse(Encoding.ASCII.GetBytes(input), ref sink);

        sink.Observations.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            observation => observation.Type.ShouldBe($"{kind}:EscapeBackslash"),
            observation => observation.First.ShouldBe("payload"u8.ToArray()));
    }

    /// <summary>Verifies BEL is forbidden APC payload rather than an OSC-style terminator.</summary>
    [Fact]
    public void Parse_WhenApcContainsBell_ReportsMalformedAtSt()
    {
        using ProtocolParser parser = new();
        var sink = new RecordingSink();

        parser.Parse("\u001b_a\ab\u001b\\"u8, ref sink);

        sink.Observations.ShouldHaveSingleItem().Diagnostic!.Value.Code.ShouldBe(DiagnosticCode.Malformed);
    }

    /// <summary>Verifies an embedded ESC that is not ST makes a command string malformed and the
    /// parser recovers at its terminator before delivering following input.</summary>
    [Fact]
    public void Parse_WhenStringContainsNonTerminatingEscape_ReportsMalformedAndRecovers()
    {
        using ProtocolParser parser = new();
        var sink = new RecordingSink();

        parser.Parse("\u001b]2;a\u001bXb\u001b\\Z"u8, ref sink);

        sink.Observations.Count.ShouldBe(2);
        sink.Observations[0].Diagnostic!.Value.Code.ShouldBe(DiagnosticCode.Malformed);
        sink.Observations[0].Diagnostic!.Value.Kind.ShouldBe(SequenceKind.Osc);
        sink.Observations[1].First.ShouldBe("Z"u8.ToArray());
    }

    /// <summary>Verifies every command-string family rejects forbidden C0 payload bytes and
    /// recovers at ST to deliver an adjacent valid CSI.</summary>
    [Fact]
    public void Parse_WhenCommandStringsContainForbiddenC0_ReportsMalformedAndRecoversEveryFamily()
    {
        byte[] introducers = [(byte) ']', (byte) 'P', (byte) '_', (byte) '^', (byte) 'X'];
        byte[] forbidden = [0x00, 0x07, 0x0e, 0x1f, 0x7f];

        foreach (var introducer in introducers)
        {
            foreach (var invalid in forbidden)
            {
                if (introducer == (byte) ']' && invalid == ControlBytes.Bell)
                {
                    continue;
                }

                using ProtocolParser parser = new();
                var sink = new RecordingSink();
                byte[] prefix = introducer == (byte) 'P'
                    ? [0x1b, introducer, (byte) 'q', (byte) 'a']
                    : [0x1b, introducer, (byte) 'a'];
                byte[] input = [.. prefix, invalid, (byte) 'b', 0x1b, (byte) '\\', 0x1b, (byte) '[', (byte) 'A'];

                parser.Parse(input, ref sink);

                sink.Observations.Count.ShouldBe(2, $"introducer {introducer:x2}, byte {invalid:x2}");
                sink.Observations[0].Diagnostic!.Value.Code.ShouldBe(DiagnosticCode.Malformed);
                sink.Observations[1].Type.ShouldBe("Csi");
            }
        }
    }

    /// <summary>Verifies CAN aborts a string and returns directly to text.</summary>
    [Fact]
    public void Parse_WhenCanCancelsOsc_ReportsAndRecoversToText()
    {
        using ProtocolParser parser = new();
        var sink = new RecordingSink();
        byte[] input = [0x1b, (byte) ']', (byte) '2', (byte) ';', (byte) 'x', 0x18, (byte) 'Y'];

        parser.Parse(input, ref sink);

        sink.Observations.Count.ShouldBe(2);
        sink.Observations[0].Diagnostic!.Value.Code.ShouldBe(DiagnosticCode.Cancelled);
        sink.Observations[0].Diagnostic!.Value.Kind.ShouldBe(SequenceKind.Osc);
        sink.Observations[1].First.ShouldBe("Y"u8.ToArray());
    }

    /// <summary>Verifies adjacent terminal strings are delivered independently.</summary>
    [Fact]
    public void Parse_WhenStringsAreAdjacent_DeliversEachSequence()
    {
        using ProtocolParser parser = new();
        var sink = new RecordingSink();

        parser.Parse("\u001b]2;a\u001b\\\u001b_b\u001b\\"u8, ref sink);

        sink.Observations.Select(static value => value.Type).ShouldBe(
            ["Osc:EscapeBackslash", "Apc:EscapeBackslash"]);
    }

    /// <summary>Verifies every split, including the two bytes of ST.</summary>
    [Fact]
    public void Parse_WhenStringsAreFragmented_MatchesWholeInput()
    {
        Fragmentation.AssertAll("left\u001b]2;title\u001b\\right"u8);
        Fragmentation.AssertAll("\u001b_a\u001bXb\u001b\\"u8);
    }

    /// <summary>Verifies enabled eight-bit OSC and ST introducers.</summary>
    [Fact]
    public void Parse_WhenEightBitStringsAreEnabled_DeliversOsc()
    {
        var limits = ParserLimits.Default with { AcceptEightBitControls = true };
        using ProtocolParser parser = new(limits);
        var sink = new RecordingSink();
        byte[] input = [0x9d, (byte) '2', (byte) ';', (byte) 'x', 0x9c];

        parser.Parse(input, ref sink);

        sink.Observations.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            observation => observation.Type.ShouldBe("Osc:EightBit"),
            observation => observation.First.ShouldBe("2;x"u8.ToArray()));
    }

    #endregion
}
