// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Protocols;

/// <summary>
/// Verifies the shared C0/C1 control-byte constants match the ECMA-48 values every parse and
/// encode site depends on (see #93).
/// </summary>
public sealed class ControlBytesTests
{
    /// <summary>Verifies every named control byte matches its documented ECMA-48 value.</summary>
    [Fact]
    public void Constants_WhenRead_MatchEcma48Values()
    {
        ControlBytes.Bell.ShouldBe((byte) 0x07);
        ControlBytes.Cancel.ShouldBe((byte) 0x18);
        ControlBytes.Escape.ShouldBe((byte) 0x1b);
        ControlBytes.Substitute.ShouldBe((byte) 0x1a);
        ControlBytes.EightBitSs3.ShouldBe((byte) 0x8f);
        ControlBytes.EightBitDcs.ShouldBe((byte) 0x90);
        ControlBytes.EightBitSos.ShouldBe((byte) 0x98);
        ControlBytes.EightBitCsi.ShouldBe((byte) 0x9b);
        ControlBytes.EightBitSt.ShouldBe((byte) 0x9c);
        ControlBytes.EightBitOsc.ShouldBe((byte) 0x9d);
        ControlBytes.EightBitPm.ShouldBe((byte) 0x9e);
        ControlBytes.EightBitApc.ShouldBe((byte) 0x9f);
    }

    /// <summary>Verifies Writer's emitted escape byte is the same constant ProtocolParser recognizes as
    /// the escape introducer, so the encode and parse sides cannot drift apart from each other.</summary>
    [Fact]
    public void Escape_WhenWriterEmitsIt_IsWhatParserRecognizes()
    {
        var destination = new ArrayBufferWriter<byte>();
        new Writer(destination).Csi([], [], (byte) 'A');

        destination.WrittenSpan[0].ShouldBe(ControlBytes.Escape);

        var sink = new CountingSink();
        using var parser = new ProtocolParser();
        parser.Parse(destination.WrittenSpan, ref sink);

        sink.CsiCount.ShouldBe(1);
    }
}
