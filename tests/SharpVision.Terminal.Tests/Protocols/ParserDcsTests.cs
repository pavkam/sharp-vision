// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Protocols;

/// <summary>
/// Verifies DCS header, payload, termination, and recovery.
/// </summary>
public sealed class ParserDcsTests
{
    /// <summary>Verifies raw DCS header fields and payload.</summary>
    [Fact]
    public void Parse_WhenDcsIsComplete_DeliversHeaderAndPayload()
    {
        using Parser parser = new();
        var sink = new RecordingSink();

        parser.Parse("\u001bP1;2$qdata\u001b\\"u8, ref sink);

        sink.Observations.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            observation => observation.Type.ShouldBe("Dcs:EscapeBackslash:24"),
            observation => observation.First.ShouldBe("1;2"u8.ToArray()),
            observation => observation.Second.ShouldBe("data"u8.ToArray()),
            observation => observation.Final.ShouldBe((byte) 'q'));
    }

    /// <summary>Verifies BEL is payload data rather than a DCS terminator.</summary>
    [Fact]
    public void Parse_WhenDcsContainsBell_PreservesBellUntilSt()
    {
        using Parser parser = new();
        var sink = new RecordingSink();

        parser.Parse("\u001bPqa\ab\u001b\\"u8, ref sink);

        sink.Observations.ShouldHaveSingleItem().Second.ShouldBe("a\ab"u8.ToArray());
    }

    /// <summary>Verifies DCS state survives every possible transport split.</summary>
    [Fact]
    public void Parse_WhenDcsIsFragmented_MatchesWholeInput() =>
        Fragmentation.AssertAll("left\u001bP1;2$qdata\u001b\\right"u8);

    /// <summary>Verifies DCS header overflow ignores through ST, not just final.</summary>
    [Fact]
    public void Parse_WhenDcsHeaderExceedsLimit_ReportsAtStAndRecovers()
    {
        var limits = Limits.Default with { MaxParameterBytes = 2 };
        using Parser parser = new(limits);
        var sink = new RecordingSink();

        parser.Parse("\u001bP123qsecret\u001b\\X"u8, ref sink);

        sink.Observations.Count.ShouldBe(2);
        sink.Observations[0].Diagnostic!.Value.Code.ShouldBe(DiagnosticCode.ParameterLimit);
        sink.Observations[0].Diagnostic!.Value.Kind.ShouldBe(SequenceKind.Dcs);
        sink.Observations[1].First.ShouldBe("X"u8.ToArray());
    }

    /// <summary>Verifies a string payload limit clears content and recovers at ST.</summary>
    [Fact]
    public void Parse_WhenStringPayloadExceedsLimit_ReportsAtStAndRecovers()
    {
        var limits = Limits.Default with { MaxStringBytes = 4 };
        using Parser parser = new(limits);
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
        var limits = Limits.Default with { MaxStringBytes = 4 };
        using Parser parser = new(limits);
        var sink = new RecordingSink();

        parser.Parse("\u001b]12345X\u001b\\Y"u8, ref sink);

        sink.Observations.Count.ShouldBe(2);
        sink.Observations[0].Diagnostic!.Value.Code.ShouldBe(DiagnosticCode.StringLimit);
        sink.Observations[0].Diagnostic!.Value.DiscardedBytes.ShouldBe(2);
        sink.Observations[1].First.ShouldBe("Y"u8.ToArray());
    }

    /// <summary>
    /// Verifies every-fragment-split coverage for a false ESC candidate
    /// followed by a genuine ST terminator.
    /// </summary>
    [Fact]
    public void Parse_WhenIgnoredStringHasFalseEscCandidate_MatchesWholeInputAcrossSplits() =>
        Fragmentation.AssertAll(
            "left\u001b]12345X\u001b\\right"u8,
            limits: Limits.Default with { MaxStringBytes = 4 });

    /// <summary>
    /// Verifies a trailing ESC candidate still open at end-of-stream counts
    /// toward DiscardedBytes instead of being silently dropped.
    /// </summary>
    [Fact]
    public void Complete_WhenIgnoredStringEndsOnOpenEscCandidate_CountsItAsDiscarded()
    {
        var limits = Limits.Default with { MaxStringBytes = 4 };
        using Parser parser = new(limits);
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
        var limits = Limits.Default with { MaxStringBytes = 4 };
        using Parser parser = new(limits);
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
        var limits = Limits.Default with { MaxStringBytes = 4, AcceptEightBitControls = true };
        using Parser parser = new(limits);
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
        var limits = Limits.Default with { MaxStringBytes = 4 };
        using Parser parser = new(limits);
        var sink = new RecordingSink();

        parser.Parse("\u001b]12345X\u001bY\u001b\\Z"u8, ref sink);

        sink.Observations.Count.ShouldBe(2);
        sink.Observations[0].Diagnostic!.Value.Code.ShouldBe(DiagnosticCode.StringLimit);
        sink.Observations[0].Diagnostic!.Value.DiscardedBytes.ShouldBe(4);
        sink.Observations[1].First.ShouldBe("Z"u8.ToArray());
    }
}
