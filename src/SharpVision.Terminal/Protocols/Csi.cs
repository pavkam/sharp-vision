// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Protocols;


/// <summary>
/// Encodes typed control sequence commands used by the terminal runtime.
/// </summary>
/// <example>
/// <code>
/// Csi.Position(writer, row: 4, column: 12);
/// Csi.EraseDisplay(writer, EraseArea.All);
/// </code>
/// </example>
public static class Csi
{
    /// <summary>Moves the cursor relative to its current position.</summary>
    /// <param name="writer">The validated protocol writer.</param>
    /// <param name="direction">The direction of movement.</param>
    /// <param name="count">The positive number of cells.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="direction"/> is unknown or <paramref name="count"/> is
    /// not positive.
    /// </exception>
    public static void Move(Writer writer, Movement direction, int count = 1)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);

        var final = direction switch
        {
            Movement.Up => (byte) 'A',
            Movement.Down => (byte) 'B',
            Movement.Forward => (byte) 'C',
            Movement.Back => (byte) 'D',
            _ => throw new ArgumentOutOfRangeException(
                nameof(direction), direction, "The cursor direction is unknown."),
        };

        Span<byte> parameters = stackalloc byte[10];
        var length = Format(count, parameters);
        writer.Csi(parameters[..length], [], final);
    }

    /// <summary>Places the cursor at a one-based row and column.</summary>
    /// <param name="writer">The validated protocol writer.</param>
    /// <param name="row">The one-based row.</param>
    /// <param name="column">The one-based column.</param>
    /// <exception cref="ArgumentOutOfRangeException">A coordinate is not positive.</exception>
    public static void Position(Writer writer, int row, int column)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(row);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(column);

        Span<byte> parameters = stackalloc byte[21];
        var length = Format(row, parameters);
        parameters[length++] = (byte) ';';
        length += Format(column, parameters[length..]);
        writer.Csi(parameters[..length], [], (byte) 'H');
    }

    /// <summary>Erases a portion of the display.</summary>
    /// <param name="writer">The validated protocol writer.</param>
    /// <param name="area">The display region to erase.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="area"/> is unknown.</exception>
    public static void EraseDisplay(Writer writer, EraseArea area)
    {
        ValidateEraseArea(area, allowScrollback: true);
        WriteNumber(writer, (int) area, (byte) 'J');
    }

    /// <summary>Erases a portion of the current line.</summary>
    /// <param name="writer">The validated protocol writer.</param>
    /// <param name="area">The line region to erase.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="area"/> is unknown or requests scrollback.
    /// </exception>
    public static void EraseLine(Writer writer, EraseArea area)
    {
        ValidateEraseArea(area, allowScrollback: false);
        WriteNumber(writer, (int) area, (byte) 'K');
    }

    /// <summary>Inserts blank characters at the cursor.</summary>
    /// <param name="writer">The validated protocol writer.</param>
    /// <param name="count">The positive number of characters.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is not positive.</exception>
    public static void InsertCharacters(Writer writer, int count = 1) =>
        WritePositive(writer, count, (byte) '@');

    /// <summary>Deletes characters at the cursor.</summary>
    /// <param name="writer">The validated protocol writer.</param>
    /// <param name="count">The positive number of characters.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is not positive.</exception>
    public static void DeleteCharacters(Writer writer, int count = 1) =>
        WritePositive(writer, count, (byte) 'P');

    /// <summary>Inserts blank lines at the cursor row.</summary>
    /// <param name="writer">The validated protocol writer.</param>
    /// <param name="count">The positive number of lines.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is not positive.</exception>
    public static void InsertLines(Writer writer, int count = 1) =>
        WritePositive(writer, count, (byte) 'L');

    /// <summary>Deletes lines at the cursor row.</summary>
    /// <param name="writer">The validated protocol writer.</param>
    /// <param name="count">The positive number of lines.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is not positive.</exception>
    public static void DeleteLines(Writer writer, int count = 1) =>
        WritePositive(writer, count, (byte) 'M');

    /// <summary>Scrolls display content upward.</summary>
    /// <param name="writer">The validated protocol writer.</param>
    /// <param name="count">The positive number of lines.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is not positive.</exception>
    public static void ScrollUp(Writer writer, int count = 1) =>
        WritePositive(writer, count, (byte) 'S');

    /// <summary>Scrolls display content downward.</summary>
    /// <param name="writer">The validated protocol writer.</param>
    /// <param name="count">The positive number of lines.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is not positive.</exception>
    public static void ScrollDown(Writer writer, int count = 1) =>
        WritePositive(writer, count, (byte) 'T');

    /// <summary>Saves the current cursor position using ANSI CSI s.</summary>
    /// <param name="writer">The validated protocol writer.</param>
    public static void SaveCursor(Writer writer) => writer.Csi([], [], (byte) 's');

    /// <summary>Restores the cursor position using ANSI CSI u.</summary>
    /// <param name="writer">The validated protocol writer.</param>
    public static void RestoreCursor(Writer writer) => writer.Csi([], [], (byte) 'u');

    /// <summary>Requests primary device attributes.</summary>
    /// <param name="writer">The validated protocol writer.</param>
    public static void PrimaryDeviceAttributes(Writer writer) => writer.Csi([], [], (byte) 'c');

    /// <summary>Requests secondary device attributes.</summary>
    /// <param name="writer">The validated protocol writer.</param>
    public static void SecondaryDeviceAttributes(Writer writer) => writer.Csi(">"u8, [], (byte) 'c');

    /// <summary>Requests a cursor position report using device status report 6.</summary>
    /// <param name="writer">The validated protocol writer.</param>
    public static void ReportCursorPosition(Writer writer) => writer.Csi("6"u8, [], (byte) 'n');

    /// <summary>Queries one DEC private mode using DECRQM.</summary>
    /// <param name="writer">The validated protocol writer.</param>
    /// <param name="mode">The positive private mode number.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="mode"/> is not positive.</exception>
    public static void QueryPrivateMode(Writer writer, int mode)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(mode);

        Span<byte> parameters = stackalloc byte[11];
        parameters[0] = (byte) '?';
        var length = Format(mode, parameters[1..]) + 1;
        writer.Csi(parameters[..length], "$"u8, (byte) 'p');
    }

    private static int Format(int value, Span<byte> destination)
    {
        var formatted = Utf8Formatter.TryFormat(value, destination, out var written);
        Debug.Assert(formatted, "A ten-byte span must hold any positive Int32 value.");

        return written;
    }

    private static void ValidateEraseArea(EraseArea area, bool allowScrollback)
    {
        if (area is < EraseArea.After or > EraseArea.Scrollback ||
            (!allowScrollback && area == EraseArea.Scrollback))
        {
            throw new ArgumentOutOfRangeException(nameof(area), area, "The erase area is invalid.");
        }
    }

    private static void WriteNumber(Writer writer, int value, byte final)
    {
        Span<byte> parameters = stackalloc byte[10];
        var length = Format(value, parameters);
        writer.Csi(parameters[..length], [], final);
    }

    private static void WritePositive(Writer writer, int count, byte final)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        WriteNumber(writer, count, final);
    }
}
