// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Protocols;

using SharpVision.Terminal.Protocols;
using SharpVision.Terminal.Tests.Support;

using Shouldly;

/// <summary>
/// Verifies ground, C0, and ESC parser behavior.
/// </summary>
public sealed class ParserControlTests
{
    /// <summary>
    /// Verifies that UTF-8 bytes are delivered without char-based reinterpretation.
    /// </summary>
    [Fact]
    public void Parse_WhenInputIsUtf8Text_DeliversBorrowedBytes()
    {
        using Parser parser = new Parser();
        RecordingSink sink = new RecordingSink();
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
        using Parser parser = new Parser();
        RecordingSink sink = new RecordingSink();

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
        using Parser parser = new Parser();
        RecordingSink sink = new RecordingSink();

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
        using Parser parser = new Parser();
        RecordingSink sink = new RecordingSink();

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
        using Parser parser = new Parser();
        RecordingSink sink = new RecordingSink();
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
        using Parser parser = new Parser(Limits.Default with { AcceptEightBitControls = true });
        RecordingSink sink = new RecordingSink();
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
        using Parser parser = new Parser(Limits.Default with { AcceptEightBitControls = true });
        RecordingSink sink = new RecordingSink();

        parser.Parse([0x85], ref sink);

        sink.Observations.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            observation => observation.Type.ShouldBe("Control"),
            observation => observation.First.ShouldBe([0x85]));
    }
}
