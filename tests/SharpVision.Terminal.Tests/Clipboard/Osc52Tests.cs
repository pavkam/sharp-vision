// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Clipboard;

/// <summary>
/// Verifies byte-exact OSC 52 text clipboard behavior.
/// </summary>
public sealed class Osc52Tests
{
    /// <summary>
    /// Verifies named selection encoding.
    /// </summary>
    /// <param name="selection">The typed selection.</param>
    /// <param name="identifier">The expected wire identifier.</param>
    [Theory]
    [InlineData(Selection.Clipboard, 'c')]
    [InlineData(Selection.Primary, 'p')]
    [InlineData(Selection.Secondary, 'q')]
    [InlineData(Selection.Select, 's')]
    [InlineData(Selection.Cut0, '0')]
    [InlineData(Selection.Cut7, '7')]
    public void Write_WhenSelectionIsKnown_WritesIdentifier(
        Selection selection,
        char identifier)
    {
        var destination = new ArrayBufferWriter<byte>();

        Osc52.Write(new ProtocolWriter(destination), selection, []);

        destination.WrittenSpan.ToArray().ShouldBe(
            Encoding.ASCII.GetBytes($"\u001b]52;{identifier};\u001b\\"));
    }

    /// <summary>
    /// Verifies canonical Base64 and ST output for ASCII and UTF-8 text.
    /// </summary>
    [Fact]
    public void Write_WhenTextIsValid_WritesExactBytes()
    {
        var destination = new ArrayBufferWriter<byte>();
        var writer = new ProtocolWriter(destination);

        Osc52.Write(writer, Selection.Clipboard, "hello"u8);
        Osc52.Write(writer, Selection.Primary, "🦄"u8);

        destination.WrittenSpan.ToArray().ShouldBe(
            Encoding.ASCII.GetBytes(
                "\u001b]52;c;aGVsbG8=\u001b\\" +
                "\u001b]52;p;8J+mhA==\u001b\\"));
    }

    /// <summary>
    /// Verifies query encoding uses a literal question mark.
    /// </summary>
    [Fact]
    public void Query_WhenSelectionIsKnown_WritesExactBytes()
    {
        var destination = new ArrayBufferWriter<byte>();

        Osc52.Query(new ProtocolWriter(destination), Selection.Clipboard);

        destination.WrittenSpan.ToArray().ShouldBe("\u001b]52;c;?\u001b\\"u8.ToArray());
    }

    /// <summary>
    /// Verifies validation happens before output mutation.
    /// </summary>
    [Fact]
    public void Write_WhenArgumentIsInvalid_ThrowsBeforeWriting()
    {
        var destination = new ArrayBufferWriter<byte>();
        var writer = new ProtocolWriter(destination);

        _ = Should.Throw<ArgumentOutOfRangeException>(() => Osc52.Write(writer, (Selection) 999, []));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => Osc52.Write(writer, Selection.Clipboard, [], maxBytes: 0));
        _ = Should.Throw<ArgumentException>(() => Osc52.Write(writer, Selection.Clipboard, "too long"u8, maxBytes: 2));
        _ = Should.Throw<ArgumentException>(() => Osc52.Write(writer, Selection.Clipboard, [0xff]));

        destination.WrittenCount.ShouldBe(0);
    }

    /// <summary>
    /// Verifies a valid clipboard reply decodes to owned UTF-8 bytes.
    /// </summary>
    [Fact]
    public void Decode_WhenReplyIsValid_ReturnsOwnedText()
    {
        var result = Osc52.Decode("52;c;8J+mhA=="u8, maxBytes: 16);

        result.Status.ShouldBe(ClipboardStatus.Success);
        result.Selection.ShouldBe(Selection.Clipboard);
        result.Data.ToArray().ShouldBe("🦄"u8.ToArray());
    }

    /// <summary>
    /// Verifies a query payload is distinguished from clipboard data.
    /// </summary>
    [Fact]
    public void Decode_WhenPayloadIsQuery_ReturnsQueryStatus()
    {
        var result = Osc52.Decode("52;p;?"u8, maxBytes: 16);

        result.Status.ShouldBe(ClipboardStatus.Query);
        result.Selection.ShouldBe(Selection.Primary);
        result.Data.Length.ShouldBe(0);
    }

    /// <summary>
    /// Verifies malformed and oversized replies degrade to a non-throwing result.
    /// </summary>
    /// <param name="payload">The invalid OSC value.</param>
    /// <param name="maximum">The decoded byte limit.</param>
    [Theory]
    [InlineData("51;c;YQ==", 16)]
    [InlineData("52;x;YQ==", 16)]
    [InlineData("52;c;***", 16)]
    [InlineData("52;c;/w==", 16)]
    [InlineData("52;c;YWJj", 2)]
    [InlineData("52;yz;YQ==", 16)]
    [InlineData("52;xc;YQ==", 16)]
    [InlineData("52;cx;YQ==", 16)]
    [InlineData("52;c!p;YQ==", 16)]
    [InlineData("52;c", 16)]
    public void Decode_WhenReplyIsInvalid_ReturnsMalformed(string payload, int maximum)
    {
        var result = Osc52.Decode(
            Encoding.ASCII.GetBytes(payload),
            maximum);

        result.Status.ShouldBe(ClipboardStatus.Malformed);
        result.Data.Length.ShouldBe(0);
    }

    /// <summary>Verifies an invalid character anywhere in the selection list remains malformed
    /// across every possible parser read boundary.</summary>
    /// <param name="selectionList">The malformed selection list.</param>
    [Theory]
    [InlineData("xc")]
    [InlineData("cx")]
    [InlineData("c!p")]
    public void Decode_WhenSelectionListContainsInvalidCharacterAtAnyReadSplit_ReturnsMalformed(
        string selectionList)
    {
        var sequence = Encoding.ASCII.GetBytes($"\u001b]52;{selectionList};YQ==\u001b\\");

        for (var split = 0; split <= sequence.Length; split++)
        {
            using ProtocolParser parser = new();
            var sink = new RecordingSink();

            parser.Parse(sequence.AsSpan(0, split), ref sink);
            parser.Parse(sequence.AsSpan(split), ref sink);
            var observation = sink.Observations.ShouldHaveSingleItem();
            var result = Osc52.Decode(observation.First, maxBytes: 16);

            result.Status.ShouldBe(ClipboardStatus.Malformed, $"split {split}");
            result.Data.Length.ShouldBe(0);
        }
    }

    /// <summary>
    /// Verifies an empty Pc field decodes with the specification's documented default of the
    /// configurable primary/clipboard selection, rather than being rejected.
    /// </summary>
    [Fact]
    public void Decode_WhenPcFieldIsEmpty_ResolvesDefaultSelection()
    {
        var result = Osc52.Decode("52;;aGk="u8, maxBytes: 16);

        result.Status.ShouldBe(ClipboardStatus.Success);
        result.Selection.ShouldBe(Selection.Select);
        result.Data.ToArray().ShouldBe("hi"u8.ToArray());
    }

    /// <summary>
    /// Verifies an empty Pc field still distinguishes a query payload.
    /// </summary>
    [Fact]
    public void Decode_WhenPcFieldIsEmptyAndPayloadIsQuery_ReturnsQueryStatus()
    {
        var result = Osc52.Decode("52;;?"u8, maxBytes: 16);

        result.Status.ShouldBe(ClipboardStatus.Query);
        result.Selection.ShouldBe(Selection.Select);
        result.Data.Length.ShouldBe(0);
    }

    /// <summary>
    /// Verifies a multi-character Pc field resolves to the first recognized selection in the
    /// order given, per the specification's selection-list semantics.
    /// </summary>
    [Fact]
    public void Decode_WhenPcFieldListsMultipleSelections_ResolvesFirstRecognized()
    {
        var result = Osc52.Decode("52;pc;aGk="u8, maxBytes: 16);

        result.Status.ShouldBe(ClipboardStatus.Success);
        result.Selection.ShouldBe(Selection.Primary);
        result.Data.ToArray().ShouldBe("hi"u8.ToArray());
    }

    /// <summary>
    /// Verifies parser callbacks from ST and BEL feed the same typed decoder.
    /// </summary>
    [Theory]
    [InlineData("\u001b]52;c;aGk=\u001b\\")]
    [InlineData("\u001b]52;c;aGk=\a")]
    public void Decode_WhenParserDeliversOsc_ReturnsClipboardText(string sequence)
    {
        using ProtocolParser parser = new();
        var sink = new RecordingSink();

        parser.Parse(Encoding.ASCII.GetBytes(sequence), ref sink);
        var observation = sink.Observations.ShouldHaveSingleItem();
        var result = Osc52.Decode(observation.First, maxBytes: 16);

        observation.Type.ShouldStartWith("Osc:");
        result.Status.ShouldBe(ClipboardStatus.Success);
        result.Data.ToArray().ShouldBe("hi"u8.ToArray());
    }
}
