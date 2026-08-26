// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Protocols;

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
        var destination = new ArrayBufferWriter<byte>();
        var writer = new ProtocolWriter(destination);

        Csi.Move(writer, operation, 3);

        destination.WrittenSpan.ToArray().ShouldBe(Encoding.ASCII.GetBytes(expected));
    }

    /// <summary>
    /// Verifies one-based row and column encoding.
    /// </summary>
    [Fact]
    public void Position_WhenCoordinatesAreValid_WritesExactBytes()
    {
        var destination = new ArrayBufferWriter<byte>();

        Csi.Position(new ProtocolWriter(destination), row: 12, column: 4);

        destination.WrittenSpan.ToArray().ShouldBe("\u001b[12;4H"u8.ToArray());
    }

    /// <summary>
    /// Verifies erase commands and their target values.
    /// </summary>
    [Fact]
    public void Erase_WhenAreasAreValid_WritesExactBytes()
    {
        var destination = new ArrayBufferWriter<byte>();
        var writer = new ProtocolWriter(destination);

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
        var destination = new ArrayBufferWriter<byte>();
        var writer = new ProtocolWriter(destination);

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
    /// Verifies scroll-region set and reset commands.
    /// </summary>
    [Fact]
    public void ScrollRegion_WhenMarginsAreValid_WritesExactBytes()
    {
        var destination = new ArrayBufferWriter<byte>();
        var writer = new ProtocolWriter(destination);

        Csi.SetScrollRegion(writer, top: 2, bottom: 20);
        Csi.ResetScrollRegion(writer);

        destination.WrittenSpan.ToArray().ShouldBe("[2;20r[r"u8.ToArray());
    }

    /// <summary>
    /// Verifies that a scroll region with the top margin equal to the bottom margin is allowed.
    /// </summary>
    [Fact]
    public void SetScrollRegion_WhenMarginsAreEqual_WritesExactBytes()
    {
        var destination = new ArrayBufferWriter<byte>();

        Csi.SetScrollRegion(new ProtocolWriter(destination), top: 5, bottom: 5);

        destination.WrittenSpan.ToArray().ShouldBe("[5;5r"u8.ToArray());
    }

    /// <summary>
    /// Verifies save, restore, device, and status query commands.
    /// </summary>
    [Fact]
    public void Query_WhenRequested_WritesExactBytes()
    {
        var destination = new ArrayBufferWriter<byte>();
        var writer = new ProtocolWriter(destination);

        Csi.SaveCursor(writer);
        Csi.RestoreCursor(writer);
        Csi.PrimaryDeviceAttributes(writer);
        Csi.SecondaryDeviceAttributes(writer);
        Csi.ReportCursorPosition(writer);
        Csi.ReportWindowPixels(writer);
        Csi.ReportCellPixels(writer);
        Csi.ReportWindowCells(writer);
        Csi.QueryPrivateMode(writer, 5522);

        destination.WrittenSpan.ToArray().ShouldBe(
            Encoding.ASCII.GetBytes(
                "\u001b[s\u001b[u\u001b[c\u001b[>c\u001b[6n\u001b[14t\u001b[16t\u001b[18t" +
                "\u001b[?5522$p"));
    }

    /// <summary>
    /// Verifies that invalid typed arguments fail before any bytes are written.
    /// </summary>
    [Fact]
    public void Command_WhenArgumentIsInvalid_ThrowsBeforeWriting()
    {
        var destination = new ArrayBufferWriter<byte>();
        var writer = new ProtocolWriter(destination);

        _ = Should.Throw<ArgumentOutOfRangeException>(() => Csi.Move(writer, Movement.Up, 0));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => Csi.Position(writer, 0, 1));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => Csi.Position(writer, 1, 0));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => Csi.EraseLine(writer, EraseArea.Scrollback));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => Csi.QueryPrivateMode(writer, 0));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => Csi.SetScrollRegion(writer, 0, 1));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => Csi.SetScrollRegion(writer, 1, 0));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => Csi.SetScrollRegion(writer, 5, 4));

        destination.WrittenCount.ShouldBe(0);
    }
}
