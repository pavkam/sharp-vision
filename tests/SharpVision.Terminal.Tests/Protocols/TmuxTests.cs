// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Protocols;

using System.Buffers;

using SharpVision.Terminal.Protocols;

using Shouldly;

/// <summary>Verifies exact tmux passthrough DCS framing.</summary>
public sealed class TmuxTests
{
    /// <summary>Verifies tmux framing doubles each embedded ESC before its outer ST terminator.</summary>
    [Fact]
    public void WritePassthrough_WhenSequenceContainsEsc_WritesExactDcsEnvelope()
    {
        ArrayBufferWriter<byte> destination = new ArrayBufferWriter<byte>();

        Tmux.WritePassthrough(destination, "\u001b]52;c;YQ==\u001b\\"u8);

        destination.WrittenSpan.ToArray().ShouldBe(
            "\u001bPtmux;\u001b\u001b]52;c;YQ==\u001b\u001b\\\u001b\\"u8.ToArray());
    }

    /// <summary>Verifies a valid empty outer payload still produces one complete DCS envelope.</summary>
    [Fact]
    public void WritePassthrough_WhenSequenceIsEmpty_WritesEmptyDcsEnvelope()
    {
        ArrayBufferWriter<byte> destination = new ArrayBufferWriter<byte>();

        Tmux.WritePassthrough(destination, []);

        destination.WrittenSpan.ToArray().ShouldBe("\u001bPtmux;\u001b\\"u8.ToArray());
    }

    /// <summary>Verifies a parser-delivered tmux DCS payload restores every doubled ESC byte.</summary>
    [Fact]
    public void TryUnwrap_WhenPayloadIsValid_RestoresOuterSequence()
    {
        ArrayBufferWriter<byte> destination = new ArrayBufferWriter<byte>();

        var unwrapped = Tmux.TryUnwrap("tmux;\u001b\u001b]52;c;YQ==\u001b\u001b\\"u8, destination);

        unwrapped.ShouldBeTrue();
        destination.WrittenSpan.ToArray().ShouldBe("\u001b]52;c;YQ==\u001b\\"u8.ToArray());
    }

    /// <summary>Verifies a malformed or non-tmux payload is rejected before destination mutation.</summary>
    [Theory]
    [InlineData("screen;payload")]
    [InlineData("tmux;\u001b]52")]
    public void TryUnwrap_WhenPayloadIsInvalid_RejectsWithoutWriting(string value)
    {
        ArrayBufferWriter<byte> destination = new ArrayBufferWriter<byte>();

        var unwrapped = Tmux.TryUnwrap(Encoding.UTF8.GetBytes(value), destination);

        unwrapped.ShouldBeFalse();
        destination.WrittenCount.ShouldBe(0);
    }

    /// <summary>Verifies a null destination is rejected before any output allocation is requested.</summary>
    [Fact]
    public void WritePassthrough_WhenDestinationIsNull_ThrowsArgumentNullException() =>
        _ = Should.Throw<ArgumentNullException>(static () => Tmux.WritePassthrough(null!, []));
}
