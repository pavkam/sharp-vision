// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Protocols;




/// <summary>Verifies exact GNU screen DCS passthrough framing.</summary>
public sealed class ScreenTests
{
    /// <summary>Verifies screen forwards the enclosed sequence without tmux-style ESC doubling.</summary>
    [Fact]
    public void WritePassthrough_WhenSequenceContainsEsc_WritesExactDcsEnvelope()
    {
        var destination = new ArrayBufferWriter<byte>();

        Screen.WritePassthrough(destination, "\u001b]52;c;YQ==\u001b\\"u8);

        destination.WrittenSpan.ToArray().ShouldBe(
            "\u001bP\u001b]52;c;YQ==\u001b\\\u001b\\"u8.ToArray());
    }

    /// <summary>Verifies a null destination is rejected before output is requested.</summary>
    [Fact]
    public void WritePassthrough_WhenDestinationIsNull_ThrowsArgumentNullException() =>
        _ = Should.Throw<ArgumentNullException>(static () => Screen.WritePassthrough(null!, []));
}
