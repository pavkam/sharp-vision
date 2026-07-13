// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Protocols;

using SharpVision.Terminal.Protocols;
using SharpVision.Terminal.Tests.Support;

using Shouldly;

/// <summary>
/// Verifies streaming CSI parsing and cancellation recovery.
/// </summary>
public sealed class ParserCsiTests
{
    /// <summary>
    /// Verifies an empty CSI header.
    /// </summary>
    [Fact]
    public void Parse_WhenCsiHasNoParameters_DeliversFinal()
    {
        using Parser parser = new Parser();
        RecordingSink sink = new RecordingSink();

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
        using Parser parser = new Parser();
        RecordingSink sink = new RecordingSink();

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
        using Parser parser = new Parser();
        RecordingSink sink = new RecordingSink();

        parser.Parse("\u001b[38:2:1:2:3m"u8, ref sink);

        sink.Observations.ShouldHaveSingleItem().First.ShouldBe("38:2:1:2:3"u8.ToArray());
    }

    /// <summary>
    /// Verifies multiple text, control, and CSI events from one read.
    /// </summary>
    [Fact]
    public void Parse_WhenReadContainsMultipleEvents_DeliversInOrder()
    {
        using Parser parser = new Parser();
        RecordingSink sink = new RecordingSink();

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
        using Parser parser = new Parser();
        RecordingSink sink = new RecordingSink();
        byte[] input = [0x1b, (byte) '[', (byte) '1', (byte) '2', 0x18, (byte) 'x'];

        parser.Parse(input, ref sink);

        sink.Observations.Count.ShouldBe(2);
        _ = sink.Observations[0].Diagnostic.ShouldNotBeNull();
        sink.Observations[0].Diagnostic!.Value.Code.ShouldBe(DiagnosticCode.Cancelled);
        sink.Observations[0].Diagnostic!.Value.Kind.ShouldBe(SequenceKind.Csi);
        sink.Observations[1].Type.ShouldBe("Text");
        sink.Observations[1].First.ShouldBe("x"u8.ToArray());
    }
}
