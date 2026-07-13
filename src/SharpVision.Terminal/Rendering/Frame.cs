// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Rendering;

using System.Buffers;
using System.Diagnostics;
using System.Text;

using SharpVision.Terminal.Geometry;
using SharpVision.Terminal.Unicode;

/// <summary>
/// Owns a bounded pooled semantic cell grid and UTF-8 grapheme arena.
/// </summary>
/// <remarks>
/// Cell metadata remains valid until mutation or disposal. Grapheme bytes are
/// copied through <see cref="CopyGrapheme"/> and pooled storage is never exposed.
/// </remarks>
public sealed class Frame: IDisposable
{
    private Cell[]? _cells;
    private byte[]? _text;

    /// <summary>Initializes a blank frame with finite text storage.</summary>
    /// <param name="size">The non-negative frame size in terminal cells.</param>
    /// <param name="maxTextBytes">The positive UTF-8 arena limit.</param>
    /// <param name="ambiguousWidth">The explicit East Asian Ambiguous width policy.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="maxTextBytes"/> is not positive or
    /// <paramref name="ambiguousWidth"/> is unknown.
    /// </exception>
    /// <exception cref="OverflowException">The cell count exceeds <see cref="int.MaxValue"/>.</exception>
    public Frame(
        Size size,
        int maxTextBytes = 16 * 1024 * 1024,
        Ambiguous ambiguousWidth = Ambiguous.Narrow)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxTextBytes);

        if (!Enum.IsDefined(ambiguousWidth))
        {
            throw new ArgumentOutOfRangeException(
                nameof(ambiguousWidth),
                ambiguousWidth,
                "The ambiguous-width policy is unknown.");
        }

        int cellCount = checked(size.Width * size.Height);
        Cell[] cells = ArrayPool<Cell>.Shared.Rent(Math.Max(1, cellCount));

        try
        {
            int initialTextLength = Math.Min(256, maxTextBytes);
            _text = ArrayPool<byte>.Shared.Rent(initialTextLength);
        }
        catch
        {
            ArrayPool<Cell>.Shared.Return(cells, clearArray: true);
            throw;
        }

        _cells = cells;
        MaxTextBytes = maxTextBytes;
        AmbiguousWidth = ambiguousWidth;
        Size = size;
        FillBlank(CellStyle.Default);
    }

    /// <summary>Gets the immutable frame dimensions in terminal cells.</summary>
    public Size Size { get; }

    /// <summary>Gets the finite UTF-8 arena limit.</summary>
    public int MaxTextBytes { get; }

    /// <summary>Gets the explicit East Asian Ambiguous width policy.</summary>
    public Ambiguous AmbiguousWidth { get; }

    /// <summary>Gets the desired cursor state committed with this frame.</summary>
    public Cursor Cursor { get; private set; }

    /// <summary>Gets a full-frame canvas after validating ownership.</summary>
    /// <exception cref="ObjectDisposedException">The frame is disposed.</exception>
    public Canvas Canvas
    {
        get
        {
            ThrowIfDisposed();
            return new Canvas(this, Bounds);
        }
    }

    /// <summary>Gets semantic metadata for one cell.</summary>
    /// <param name="point">The in-bounds cell coordinate.</param>
    /// <returns>Non-owning semantic metadata.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="point"/> is outside the frame.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The frame is disposed.</exception>
    public CellInfo GetCell(Point point)
    {
        int index = GetIndex(point);
        Cell cell = Cells[index];
        Point lead = cell.IsContinuation
            ? new Point(cell.LeadIndex % Size.Width, cell.LeadIndex / Size.Width)
            : default;

        return new CellInfo(cell.Style, cell.Width, cell.IsContinuation, lead);
    }

    /// <summary>Gets the complete lead grapheme byte count for an occupied cell.</summary>
    /// <param name="point">The in-bounds lead or continuation coordinate.</param>
    /// <returns>The UTF-8 byte count, or zero for a blank cell.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="point"/> is outside the frame.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The frame is disposed.</exception>
    public int GetGraphemeByteCount(Point point)
    {
        int index = ResolveLead(GetIndex(point));
        return Cells[index].Length;
    }

    /// <summary>Copies a complete lead grapheme into caller-owned storage.</summary>
    /// <param name="point">The in-bounds lead or continuation coordinate.</param>
    /// <param name="destination">The destination UTF-8 span.</param>
    /// <returns>The number of bytes copied.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="point"/> is outside the frame.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="destination"/> is too small for the complete grapheme.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The frame is disposed.</exception>
    public int CopyGrapheme(Point point, Span<byte> destination)
    {
        int index = ResolveLead(GetIndex(point));
        Cell cell = Cells[index];

        if (destination.Length < cell.Length)
        {
            throw new ArgumentException(
                "The destination cannot hold the complete grapheme.",
                nameof(destination));
        }

        Text.Slice(cell.Offset, cell.Length).CopyTo(destination);
        return cell.Length;
    }

    /// <summary>Clears every cell and releases all active arena bytes for reuse.</summary>
    /// <param name="style">The semantic blank-cell style.</param>
    /// <exception cref="ObjectDisposedException">The frame is disposed.</exception>
    public void Clear(CellStyle style = default)
    {
        ThrowIfDisposed();
        Text[..TextLength].Clear();
        TextLength = 0;
        FillBlank(style);
    }

    /// <summary>Sets the desired cursor state after validating frame geometry.</summary>
    /// <param name="position">The in-bounds cell coordinate.</param>
    /// <param name="visible">Whether the terminal cursor is visible.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="position"/> is outside a non-empty frame, or a zero-sized
    /// frame requests a non-default position or visible cursor.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The frame is disposed.</exception>
    public void SetCursor(Point position, bool visible)
    {
        ThrowIfDisposed();
        bool suspended = Size.Width == 0 || Size.Height == 0;

        if ((suspended && (position != default || visible)) ||
            (!suspended && !Bounds.Contains(position)))
        {
            throw new ArgumentOutOfRangeException(
                nameof(position),
                position,
                "The cursor position is outside the frame.");
        }

        Cursor = new Cursor(position, visible);
    }

    /// <summary>Clears and returns every rented array. Disposal is idempotent.</summary>
    public void Dispose()
    {
        Cell[]? cells = _cells;
        byte[]? text = _text;

        if (cells is null || text is null)
        {
            return;
        }

        _cells = null;
        _text = null;
        TextLength = 0;
        ArrayPool<Cell>.Shared.Return(cells, clearArray: true);
        ArrayPool<byte>.Shared.Return(text, clearArray: true);
    }

    /// <summary>Gets the frame's half-open bounds.</summary>
    internal Rect Bounds => new(0, 0, Size.Width, Size.Height);

    /// <summary>Gets active cells after validating frame ownership.</summary>
    internal Span<Cell> Cells
    {
        get
        {
            ThrowIfDisposed();
            return _cells.AsSpan(0, checked(Size.Width * Size.Height));
        }
    }

    /// <summary>Gets the rented UTF-8 arena after validating frame ownership.</summary>
    internal Span<byte> Text
    {
        get
        {
            ThrowIfDisposed();
            return _text;
        }
    }

    /// <summary>Gets the active UTF-8 arena byte count.</summary>
    internal int TextLength { get; private set; }

    /// <summary>Creates an independent pooled copy of this active frame.</summary>
    /// <returns>A frame whose semantic state equals this frame.</returns>
    internal Frame Clone()
    {
        ThrowIfDisposed();
        Frame result = new(Size, MaxTextBytes, AmbiguousWidth);

        try
        {
            result.PrepareCopyFrom(this);
            result.CopyFrom(this);
            return result;
        }
        catch
        {
            result.Dispose();
            throw;
        }
    }

    /// <summary>Prepares storage so a later copy cannot allocate.</summary>
    /// <param name="source">The compatible active source frame.</param>
    internal void PrepareCopyFrom(Frame source)
    {
        ValidateCopySource(source);
        EnsureCapacity(source.TextLength);
    }

    /// <summary>Copies compatible semantic state without retaining borrowed storage.</summary>
    /// <param name="source">The compatible active source frame.</param>
    internal void CopyFrom(Frame source)
    {
        ValidateCopySource(source);
        Debug.Assert(
            source.TextLength <= Text.Length,
            "Copy storage must be prepared before terminal output is committed.");
        Text[..TextLength].Clear();
        source.Text[..source.TextLength].CopyTo(Text);
        source.Cells.CopyTo(Cells);
        TextLength = source.TextLength;
        Cursor = source.Cursor;
    }

    /// <summary>Gets a copied internal cell by absolute row-major index.</summary>
    /// <param name="index">The validated absolute index.</param>
    /// <returns>The internal semantic cell.</returns>
    internal Cell GetCell(int index)
    {
        Debug.Assert((uint) index < (uint) Cells.Length, "Internal cell indexes are bounded.");
        return Cells[index];
    }

    /// <summary>Gets borrowed complete lead grapheme bytes by absolute index.</summary>
    /// <param name="index">The validated lead or continuation index.</param>
    /// <returns>Borrowed bytes valid until frame mutation or disposal.</returns>
    internal ReadOnlySpan<byte> GetGrapheme(int index)
    {
        Debug.Assert((uint) index < (uint) Cells.Length, "Internal grapheme indexes are bounded.");
        int lead = ResolveLead(index);
        Cell cell = Cells[lead];
        return Text.Slice(cell.Offset, cell.Length);
    }

    /// <summary>Gets the owning lead column for a row-local cell.</summary>
    /// <param name="row">The validated row.</param>
    /// <param name="column">The validated column.</param>
    /// <returns>The lead column, or the input column for non-continuations.</returns>
    internal int GetLeadColumn(int row, int column)
    {
        int index = checked((row * Size.Width) + column);
        Cell cell = GetCell(index);
        return cell.IsContinuation ? cell.LeadIndex % Size.Width : column;
    }

    /// <summary>Gets the exclusive owned-cell end for a row-local cell.</summary>
    /// <param name="row">The validated row.</param>
    /// <param name="column">The validated column.</param>
    /// <returns>The exclusive column after the complete owner.</returns>
    internal int GetOwnedEnd(int row, int column)
    {
        int leadColumn = GetLeadColumn(row, column);
        Cell lead = GetCell(checked((row * Size.Width) + leadColumn));
        return Math.Min(Size.Width, leadColumn + Math.Max(1, (int) lead.Width));
    }

    /// <summary>Compares one cell semantically with a same-sized frame.</summary>
    /// <param name="other">The other active frame.</param>
    /// <param name="index">The validated absolute index.</param>
    /// <returns>Whether metadata and complete lead bytes are equal.</returns>
    internal bool SemanticallyEquals(Frame other, int index)
    {
        Debug.Assert(Size == other.Size, "Semantic cell comparison requires equal dimensions.");
        Cell left = GetCell(index);
        Cell right = other.GetCell(index);

        return left.Width == right.Width &&
            left.LeadIndex == right.LeadIndex &&
            left.Style == right.Style &&
            left.IsContinuation == right.IsContinuation &&
            (left.IsContinuation ||
                (left.Hash == right.Hash &&
                    left.Length == right.Length &&
                    GetGrapheme(index).SequenceEqual(other.GetGrapheme(index))));
    }

    /// <summary>Gets a validated absolute index for an in-bounds point.</summary>
    /// <param name="point">The point to validate.</param>
    /// <returns>The absolute row-major index.</returns>
    internal int GetIndex(Point point)
    {
        ThrowIfDisposed();

        return Bounds.Contains(point)
            ? checked((point.Y * Size.Width) + point.X)
            : throw new ArgumentOutOfRangeException(
                nameof(point),
                point,
                "The point is outside the frame.");
    }

    /// <summary>Ensures a complete draw fits before observable mutation.</summary>
    /// <param name="additionalBytes">The non-negative required byte count.</param>
    /// <exception cref="InvalidOperationException">The finite arena limit would be exceeded.</exception>
    internal void EnsureAppendable(int additionalBytes)
    {
        Debug.Assert(additionalBytes >= 0, "Preflight byte counts cannot be negative.");

        if (additionalBytes > MaxTextBytes - TextLength)
        {
            throw new InvalidOperationException("The frame text arena limit would be exceeded.");
        }
    }

    /// <summary>Writes one validated cluster and repairs every previous owner.</summary>
    /// <param name="point">The in-bounds lead coordinate.</param>
    /// <param name="value">The borrowed UTF-16 cluster.</param>
    /// <param name="width">The width, one or two cells.</param>
    /// <param name="style">The semantic style.</param>
    internal void Write(Point point, ReadOnlySpan<char> value, int width, CellStyle style)
    {
        Debug.Assert(width is 1 or 2, "Only printable narrow and wide cells are stored.");
        int index = GetIndex(point);
        Debug.Assert(index + width <= Cells.Length, "Canvas edge handling guarantees cell capacity.");
        Repair(index);

        if (width == 2)
        {
            Repair(index + 1);
        }

        int offset = TextLength;
        int length = Append(value);
        Span<byte> bytes = Text.Slice(offset, length);
        uint hash = Hash(bytes);
        Cells[index] = new Cell
        {
            Offset = offset,
            Length = length,
            Hash = hash,
            Width = (byte) width,
            LeadIndex = -1,
            Style = style,
        };

        if (width == 2)
        {
            Cells[index + 1] = Cell.Continuation(index, style);
        }
    }

    /// <summary>Repairs the complete glyph owning an absolute cell index.</summary>
    /// <param name="index">The validated absolute index.</param>
    internal void Repair(int index)
    {
        Debug.Assert((uint) index < (uint) Cells.Length, "Repair indexes are validated by the canvas.");
        Cell cell = Cells[index];
        int leadIndex = cell.IsContinuation ? cell.LeadIndex : index;
        Cell lead = Cells[leadIndex];
        int width = Math.Max(1, (int) lead.Width);
        CellStyle style = lead.Style;

        for (int offset = 0; offset < width && leadIndex + offset < Cells.Length; offset++)
        {
            Cells[leadIndex + offset] = Cell.Blank(style);
        }
    }

    /// <summary>Sets one already-repaired cell to a semantic blank.</summary>
    /// <param name="index">The validated absolute index.</param>
    /// <param name="style">The blank style.</param>
    internal void SetBlank(int index, CellStyle style)
    {
        Debug.Assert((uint) index < (uint) Cells.Length, "Blank indexes are validated by the canvas.");
        Cells[index] = Cell.Blank(style);
    }

    /// <summary>Styles a complete cell owner when every occupied cell is inside a clip.</summary>
    /// <param name="index">The validated lead, continuation, or blank index.</param>
    /// <param name="clip">The effective frame clip.</param>
    /// <param name="style">The replacement semantic style.</param>
    /// <returns>Whether the complete owner was styled.</returns>
    internal bool TrySetOwnerStyle(int index, Rect clip, CellStyle style)
    {
        Debug.Assert((uint) index < (uint) Cells.Length, "CellStyle indexes are validated by the canvas.");
        int leadIndex = ResolveLead(index);
        Cell lead = Cells[leadIndex];
        int width = Math.Max(1, (int) lead.Width);
        Point point = new(leadIndex % Size.Width, leadIndex / Size.Width);

        for (int offset = 0; offset < width; offset++)
        {
            if (!clip.Contains(new Point(point.X + offset, point.Y)))
            {
                return false;
            }
        }

        for (int offset = 0; offset < width; offset++)
        {
            Cell cell = Cells[leadIndex + offset];
            cell.Style = style;
            Cells[leadIndex + offset] = cell;
        }

        return true;
    }

    /// <summary>Counts UTF-8 bytes using the frame's replacement policy.</summary>
    /// <param name="value">The borrowed UTF-16 cluster.</param>
    /// <returns>The encoded byte count.</returns>
    internal static int CountUtf8(ReadOnlySpan<char> value)
    {
        int count = 0;
        int position = 0;

        while (position < value.Length)
        {
            OperationStatus status = Rune.DecodeFromUtf16(value[position..], out Rune rune, out int consumed);

            if (status != OperationStatus.Done)
            {
                rune = Rune.ReplacementChar;
                consumed = 1;
            }

            count = checked(count + rune.Utf8SequenceLength);
            position += consumed;
        }

        return count;
    }

    /// <summary>Throws when pooled frame ownership has ended.</summary>
    internal void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_cells is null, this);

    private int Append(ReadOnlySpan<char> value)
    {
        int byteCount = CountUtf8(value);
        EnsureAppendable(byteCount);
        EnsureCapacity(checked(TextLength + byteCount));
        int start = TextLength;
        int position = 0;

        while (position < value.Length)
        {
            OperationStatus status = Rune.DecodeFromUtf16(value[position..], out Rune rune, out int consumed);

            if (status != OperationStatus.Done)
            {
                rune = Rune.ReplacementChar;
                consumed = 1;
            }

            TextLength += rune.EncodeToUtf8(Text[TextLength..]);
            position += consumed;
        }

        Debug.Assert(TextLength - start == byteCount, "UTF-8 preflight and encoding must agree.");
        return byteCount;
    }

    private void EnsureCapacity(int required)
    {
        if (required <= Text.Length)
        {
            return;
        }

        int doubled = Text.Length > MaxTextBytes / 2
            ? MaxTextBytes
            : Text.Length * 2;
        int length = Math.Max(required, doubled);
        byte[] replacement = ArrayPool<byte>.Shared.Rent(length);
        Text[..TextLength].CopyTo(replacement);
        byte[]? previous = _text;
        _text = replacement;
        ArrayPool<byte>.Shared.Return(previous!, clearArray: true);
    }

    private void ValidateCopySource(Frame source)
    {
        ArgumentNullException.ThrowIfNull(source);
        ThrowIfDisposed();
        source.ThrowIfDisposed();

        if (source.Size != Size || source.AmbiguousWidth != AmbiguousWidth)
        {
            throw new ArgumentException(
                "The source frame geometry and width policy must match.",
                nameof(source));
        }

        if (source.TextLength > MaxTextBytes)
        {
            throw new ArgumentException(
                "The source frame text exceeds this frame's arena limit.",
                nameof(source));
        }
    }

    private void FillBlank(CellStyle style)
    {
        Span<Cell> cells = Cells;

        for (int index = 0; index < cells.Length; index++)
        {
            cells[index] = Cell.Blank(style);
        }
    }

    private int ResolveLead(int index)
    {
        Cell cell = Cells[index];
        return cell.IsContinuation ? cell.LeadIndex : index;
    }

    private static uint Hash(ReadOnlySpan<byte> value)
    {
        const uint offset = 2166136261;
        const uint prime = 16777619;
        uint hash = offset;

        foreach (byte item in value)
        {
            hash ^= item;
            hash *= prime;
        }

        return hash;
    }
}
