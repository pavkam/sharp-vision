// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Protocols;

using System.Buffers;

using SharpVision.Terminal.Protocols;

using Shouldly;

/// <summary>
/// Verifies typed CSI command encoding.
/// </summary>
public sealed class CsiTests
{
    /// <summary>
    /// Verifies relative cursor movement commands.
    /// </summary>
    /// <param name="operation">The movement operation.</param>
    /// <param name="expected">The exact expected sequence.</param>
    [Theory]
    [InlineData(Movement.Up, "\u001b[3A")]
    [InlineData(Movement.Down, "\u001b[3B")]
    [InlineData(Movement.Forward, "\u001b[3C")]
    [InlineData(Movement.Back, "\u001b[3D")]
    public void Move_WhenCountIsValid_WritesExactBytes(Movement operation, string expected)
    {
        ArrayBufferWriter<byte> destination = new ArrayBufferWriter<byte>();
        Writer writer = new Writer(destination);

        Csi.Move(writer, operation, 3);

        destination.WrittenSpan.ToArray().ShouldBe(Encoding.ASCII.GetBytes(expected));
    }

    /// <summary>
    /// Verifies one-based row and column encoding.
    /// </summary>
    [Fact]
    public void Position_WhenCoordinatesAreValid_WritesExactBytes()
    {
        ArrayBufferWriter<byte> destination = new ArrayBufferWriter<byte>();

        Csi.Position(new Writer(destination), row: 12, column: 4);

        destination.WrittenSpan.ToArray().ShouldBe("\u001b[12;4H"u8.ToArray());
    }

    /// <summary>
    /// Verifies erase commands and their target values.
    /// </summary>
    [Fact]
    public void Erase_WhenAreasAreValid_WritesExactBytes()
    {
        ArrayBufferWriter<byte> destination = new ArrayBufferWriter<byte>();
        Writer writer = new Writer(destination);

        Csi.EraseDisplay(writer, EraseArea.Scrollback);
        Csi.EraseLine(writer, EraseArea.All);

        destination.WrittenSpan.ToArray().ShouldBe("\u001b[3J\u001b[2K"u8.ToArray());
    }

    /// <summary>
    /// Verifies insertion, deletion, and scrolling commands.
    /// </summary>
    [Fact]
    public void Edit_WhenCountsAreValid_WritesExactBytes()
    {
        ArrayBufferWriter<byte> destination = new ArrayBufferWriter<byte>();
        Writer writer = new Writer(destination);

        Csi.InsertCharacters(writer, 2);
        Csi.DeleteCharacters(writer, 3);
        Csi.InsertLines(writer, 4);
        Csi.DeleteLines(writer, 5);
        Csi.ScrollUp(writer, 6);
        Csi.ScrollDown(writer, 7);

        destination.WrittenSpan.ToArray().ShouldBe(
            "\u001b[2@\u001b[3P\u001b[4L\u001b[5M\u001b[6S\u001b[7T"u8.ToArray());
    }

    /// <summary>
    /// Verifies save, restore, device, and status query commands.
    /// </summary>
    [Fact]
    public void Query_WhenRequested_WritesExactBytes()
    {
        ArrayBufferWriter<byte> destination = new ArrayBufferWriter<byte>();
        Writer writer = new Writer(destination);

        Csi.SaveCursor(writer);
        Csi.RestoreCursor(writer);
        Csi.PrimaryDeviceAttributes(writer);
        Csi.SecondaryDeviceAttributes(writer);
        Csi.ReportCursorPosition(writer);
        Csi.QueryPrivateMode(writer, 5522);

        destination.WrittenSpan.ToArray().ShouldBe(
            "\u001b[s\u001b[u\u001b[c\u001b[>c\u001b[6n\u001b[?5522$p"u8.ToArray());
    }

    /// <summary>
    /// Verifies that invalid typed arguments fail before any bytes are written.
    /// </summary>
    [Fact]
    public void Command_WhenArgumentIsInvalid_ThrowsBeforeWriting()
    {
        ArrayBufferWriter<byte> destination = new ArrayBufferWriter<byte>();
        Writer writer = new Writer(destination);

        _ = Should.Throw<ArgumentOutOfRangeException>(
            () => Csi.Move(writer, Movement.Up, 0));
        _ = Should.Throw<ArgumentOutOfRangeException>(
            () => Csi.Position(writer, 0, 1));
        _ = Should.Throw<ArgumentOutOfRangeException>(
            () => Csi.Position(writer, 1, 0));
        _ = Should.Throw<ArgumentOutOfRangeException>(
            () => Csi.EraseLine(writer, EraseArea.Scrollback));
        _ = Should.Throw<ArgumentOutOfRangeException>(
            () => Csi.QueryPrivateMode(writer, 0));

        destination.WrittenCount.ShouldBe(0);
    }
}
