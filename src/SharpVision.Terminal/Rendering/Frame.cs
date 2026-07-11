using System.Buffers;
using System.Diagnostics;
using System.Text;

using SharpVision.Terminal.Geometry;
using SharpVision.Terminal.Unicode;

namespace SharpVision.Terminal.Rendering;

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
    private int _textLength;

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

        var cellCount = checked(size.Width * size.Height);
        var cells = ArrayPool<Cell>.Shared.Rent(Math.Max(1, cellCount));

        try
        {
            var initialTextLength = Math.Min(256, maxTextBytes);
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
        FillBlank(Style.Default);
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
        var index = GetIndex(point);
        var cell = Cells[index];
        var lead = cell.IsContinuation
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
        var index = ResolveLead(GetIndex(point));
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
        var index = ResolveLead(GetIndex(point));
        var cell = Cells[index];

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
    public void Clear(Style style = default)
    {
        ThrowIfDisposed();
        Text[.._textLength].Clear();
        _textLength = 0;
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
        var suspended = Size.Width == 0 || Size.Height == 0;

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
        var cells = _cells;
        var text = _text;

        if (cells is null || text is null)
        {
            return;
        }

        _cells = null;
        _text = null;
        _textLength = 0;
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
        var lead = ResolveLead(index);
        var cell = Cells[lead];
        return Text.Slice(cell.Offset, cell.Length);
    }

    /// <summary>Gets the owning lead column for a row-local cell.</summary>
    /// <param name="row">The validated row.</param>
    /// <param name="column">The validated column.</param>
    /// <returns>The lead column, or the input column for non-continuations.</returns>
    internal int GetLeadColumn(int row, int column)
    {
        var index = checked((row * Size.Width) + column);
        var cell = GetCell(index);
        return cell.IsContinuation ? cell.LeadIndex % Size.Width : column;
    }

    /// <summary>Gets the exclusive owned-cell end for a row-local cell.</summary>
    /// <param name="row">The validated row.</param>
    /// <param name="column">The validated column.</param>
    /// <returns>The exclusive column after the complete owner.</returns>
    internal int GetOwnedEnd(int row, int column)
    {
        var leadColumn = GetLeadColumn(row, column);
        var lead = GetCell(checked((row * Size.Width) + leadColumn));
        return Math.Min(Size.Width, leadColumn + Math.Max(1, (int) lead.Width));
    }

    /// <summary>Compares one cell semantically with a same-sized frame.</summary>
    /// <param name="other">The other active frame.</param>
    /// <param name="index">The validated absolute index.</param>
    /// <returns>Whether metadata and complete lead bytes are equal.</returns>
    internal bool SemanticallyEquals(Frame other, int index)
    {
        Debug.Assert(Size == other.Size, "Semantic cell comparison requires equal dimensions.");
        var left = GetCell(index);
        var right = other.GetCell(index);

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

        if (additionalBytes > MaxTextBytes - _textLength)
        {
            throw new InvalidOperationException("The frame text arena limit would be exceeded.");
        }
    }

    /// <summary>Writes one validated cluster and repairs every previous owner.</summary>
    /// <param name="point">The in-bounds lead coordinate.</param>
    /// <param name="value">The borrowed UTF-16 cluster.</param>
    /// <param name="width">The width, one or two cells.</param>
    /// <param name="style">The semantic style.</param>
    internal void Write(Point point, ReadOnlySpan<char> value, int width, Style style)
    {
        Debug.Assert(width is 1 or 2, "Only printable narrow and wide cells are stored.");
        var index = GetIndex(point);
        Debug.Assert(index + width <= Cells.Length, "Canvas edge handling guarantees cell capacity.");
        Repair(index);

        if (width == 2)
        {
            Repair(index + 1);
        }

        var offset = _textLength;
        var length = Append(value);
        var bytes = Text.Slice(offset, length);
        var hash = Hash(bytes);
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
        var cell = Cells[index];
        var leadIndex = cell.IsContinuation ? cell.LeadIndex : index;
        var lead = Cells[leadIndex];
        var width = Math.Max(1, (int) lead.Width);
        var style = lead.Style;

        for (var offset = 0; offset < width && leadIndex + offset < Cells.Length; offset++)
        {
            Cells[leadIndex + offset] = Cell.Blank(style);
        }
    }

    /// <summary>Sets one already-repaired cell to a semantic blank.</summary>
    /// <param name="index">The validated absolute index.</param>
    /// <param name="style">The blank style.</param>
    internal void SetBlank(int index, Style style)
    {
        Debug.Assert((uint) index < (uint) Cells.Length, "Blank indexes are validated by the canvas.");
        Cells[index] = Cell.Blank(style);
    }

    /// <summary>Counts UTF-8 bytes using the frame's replacement policy.</summary>
    /// <param name="value">The borrowed UTF-16 cluster.</param>
    /// <returns>The encoded byte count.</returns>
    internal static int CountUtf8(ReadOnlySpan<char> value)
    {
        var count = 0;
        var position = 0;

        while (position < value.Length)
        {
            var status = Rune.DecodeFromUtf16(value[position..], out var rune, out var consumed);

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
        var byteCount = CountUtf8(value);
        EnsureAppendable(byteCount);
        EnsureCapacity(checked(_textLength + byteCount));
        var start = _textLength;
        var position = 0;

        while (position < value.Length)
        {
            var status = Rune.DecodeFromUtf16(value[position..], out var rune, out var consumed);

            if (status != OperationStatus.Done)
            {
                rune = Rune.ReplacementChar;
                consumed = 1;
            }

            _textLength += rune.EncodeToUtf8(Text[_textLength..]);
            position += consumed;
        }

        Debug.Assert(_textLength - start == byteCount, "UTF-8 preflight and encoding must agree.");
        return byteCount;
    }

    private void EnsureCapacity(int required)
    {
        if (required <= Text.Length)
        {
            return;
        }

        var doubled = Text.Length > MaxTextBytes / 2
            ? MaxTextBytes
            : Text.Length * 2;
        var length = Math.Max(required, doubled);
        var replacement = ArrayPool<byte>.Shared.Rent(length);
        Text[.._textLength].CopyTo(replacement);
        var previous = _text;
        _text = replacement;
        ArrayPool<byte>.Shared.Return(previous!, clearArray: true);
    }

    private void FillBlank(Style style)
    {
        var cells = Cells;

        for (var index = 0; index < cells.Length; index++)
        {
            cells[index] = Cell.Blank(style);
        }
    }

    private int ResolveLead(int index)
    {
        var cell = Cells[index];
        return cell.IsContinuation ? cell.LeadIndex : index;
    }

    private static uint Hash(ReadOnlySpan<byte> value)
    {
        const uint offset = 2166136261;
        const uint prime = 16777619;
        var hash = offset;

        foreach (var item in value)
        {
            hash ^= item;
            hash *= prime;
        }

        return hash;
    }
}
