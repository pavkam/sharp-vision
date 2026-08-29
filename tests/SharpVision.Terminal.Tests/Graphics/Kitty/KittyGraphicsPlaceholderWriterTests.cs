// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Graphics.Kitty;

using SharpVision.Terminal.Capabilities;
using SharpVision.Terminal.Kitty.Graphics;

/// <summary>Verifies exact Unicode placeholder grapheme encoding.</summary>
public sealed class KittyGraphicsPlaceholderWriterTests
{
    /// <summary>Verifies the image identifier's high byte follows explicit row and column marks.</summary>
    [Fact]
    public void WriteText_WhenImageIdUsesHighByte_WritesThirdProtocolDiacritic()
    {
        var value = new GraphicsCellOverlayValue(
            0x0200_002A,
            1,
            row: 0,
            column: 1,
            default,
            ColorDepth.TrueColor);
        var destination = new ArrayBufferWriter<byte>();

        KittyGraphicsPlaceholderWriter.WriteText(value, destination);

        destination.WrittenSpan.ToArray().ShouldBe("\U0010EEEE\u0305\u030D\u030E"u8.ToArray());
    }
}
