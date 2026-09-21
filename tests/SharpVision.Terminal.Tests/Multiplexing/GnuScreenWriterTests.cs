// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Multiplexing;

using SharpVision.Terminal.Multiplexing;

/// <summary>Verifies exact GNU screen DCS passthrough framing.</summary>
public sealed class GnuScreenWriterTests
{
    /// <summary>Verifies screen forwards the enclosed sequence without tmux-style ESC doubling.</summary>
    [Fact]
    public void WritePassthrough_WhenSequenceContainsEsc_WritesExactDcsEnvelope()
    {
        var destination = new ArrayBufferWriter<byte>();

        GnuScreenWriter.WritePassthrough(destination, "\u001b]52;c;YQ==\u001b\\"u8);

        destination.WrittenSpan.ToArray().ShouldBe(
            "\u001bP\u001b]52;c;YQ==\u001b\\\u001b\\"u8.ToArray());
    }

    /// <summary>Verifies a null destination is rejected before output is requested.</summary>
    [Fact]
    public void WritePassthrough_WhenDestinationIsNull_ThrowsArgumentNullException() =>
        _ = Should.Throw<ArgumentNullException>(static () => GnuScreenWriter.WritePassthrough(null!, []));

    /// <summary>Verifies a parser-delivered GNU screen DCS envelope restores the exact enclosed
    /// sequence, with no ESC-doubling repair because screen forwards bytes unmodified.</summary>
    [Fact]
    public void TryUnwrap_WhenPayloadIsValid_RestoresOuterSequence()
    {
        var destination = new ArrayBufferWriter<byte>();

        var unwrapped = GnuScreenWriter.TryUnwrap("\u001bP\u001b]52;c;YQ==\u001b\\\u001b\\"u8, destination);

        unwrapped.ShouldBeTrue();
        destination.WrittenSpan.ToArray().ShouldBe("\u001b]52;c;YQ==\u001b\\"u8.ToArray());
    }

    /// <summary>Verifies a malformed or unterminated envelope is rejected before destination
    /// mutation.</summary>
    [Theory]
    [InlineData("screen;payload")]
    [InlineData("\u001bPunterminated")]
    public void TryUnwrap_WhenPayloadIsInvalid_RejectsWithoutWriting(string value)
    {
        var destination = new ArrayBufferWriter<byte>();

        var unwrapped = GnuScreenWriter.TryUnwrap(Encoding.UTF8.GetBytes(value), destination);

        unwrapped.ShouldBeFalse();
        destination.WrittenCount.ShouldBe(0);
    }
}
