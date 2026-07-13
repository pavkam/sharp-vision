namespace SharpVision.Terminal.Tests.Protocols;

using SharpVision.Terminal.Protocols;
using SharpVision.Terminal.Tests.Support;

using Shouldly;

/// <summary>
/// Verifies bounded OSC, APC, PM, and SOS parsing.
/// </summary>
public sealed class ParserStringTests
{
    /// <summary>Verifies OSC payload and canonical ST termination.</summary>
    [Fact]
    public void Parse_WhenOscEndsWithSt_DeliversPayloadAndTerminator()
    {
        using var parser = new Parser();
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
        using var parser = new Parser();
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
        var limits = Limits.Default with { AcceptBellTerminatedOsc = false };
        using var parser = new Parser(limits);
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
        using var parser = new Parser();
        var sink = new RecordingSink();

        parser.Parse(Encoding.ASCII.GetBytes(input), ref sink);

        sink.Observations.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            observation => observation.Type.ShouldBe($"{kind}:EscapeBackslash"),
            observation => observation.First.ShouldBe("payload"u8.ToArray()));
    }

    /// <summary>Verifies BEL is data in non-OSC strings.</summary>
    [Fact]
    public void Parse_WhenApcContainsBell_PreservesBellUntilSt()
    {
        using var parser = new Parser();
        var sink = new RecordingSink();

        parser.Parse("\u001b_a\ab\u001b\\"u8, ref sink);

        sink.Observations.ShouldHaveSingleItem().First.ShouldBe("a\ab"u8.ToArray());
    }

    /// <summary>Verifies ESC followed by a non-backslash remains in the payload.</summary>
    [Fact]
    public void Parse_WhenStringContainsNonTerminatingEscape_PreservesBothBytes()
    {
        using var parser = new Parser();
        var sink = new RecordingSink();

        parser.Parse("\u001b]2;a\u001bXb\u001b\\"u8, ref sink);

        sink.Observations.ShouldHaveSingleItem().First.ShouldBe(
            "2;a\u001bXb"u8.ToArray());
    }

    /// <summary>Verifies CAN aborts a string and returns directly to text.</summary>
    [Fact]
    public void Parse_WhenCanCancelsOsc_ReportsAndRecoversToText()
    {
        using var parser = new Parser();
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
        using var parser = new Parser();
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
        var limits = Limits.Default with { AcceptEightBitControls = true };
        using var parser = new Parser(limits);
        var sink = new RecordingSink();
        byte[] input = [0x9d, (byte) '2', (byte) ';', (byte) 'x', 0x9c];

        parser.Parse(input, ref sink);

        sink.Observations.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            observation => observation.Type.ShouldBe("Osc:EightBit"),
            observation => observation.First.ShouldBe("2;x"u8.ToArray()));
    }
}
