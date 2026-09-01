// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Rendering;

using Graphics;

/// <summary>
/// Owns a bounded pooled semantic cell grid and UTF-8 grapheme arena.
/// </summary>
/// <remarks>
/// Cell metadata remains valid until mutation or disposal. Grapheme bytes are
/// copied through <see cref="CopyGrapheme"/> and pooled storage is never exposed.
/// </remarks>
[DebuggerDisplay("{ToString()}")]
[PublicAPI]
public sealed class Frame: IDisposable
{
    private Cell[]? _cells;
    private int _mutationCaptureDepth;
    private ulong _mutationRevision;
    private ulong[]? _rowFingerprints;
    private ulong _rowFingerprintRevision;
    private bool _rowFingerprintsValid;
    private byte[]? _text;
    private Placement[]? _placements;
    private int _placementCount;

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
        : this(size, maxTextBytes, ambiguousWidth, 4_096)
    {
    }

    /// <summary>Initializes a blank frame with explicit finite text and placement storage.</summary>
    /// <param name="size">The non-negative frame size in terminal cells.</param>
    /// <param name="maxTextBytes">The positive UTF-8 arena limit.</param>
    /// <param name="ambiguousWidth">The explicit East Asian Ambiguous width policy.</param>
    /// <param name="maxPlacements">The positive semantic image-placement limit.</param>
    /// <exception cref="ArgumentOutOfRangeException">A limit or width policy is invalid.</exception>
    /// <exception cref="OverflowException">The cell count exceeds <see cref="int.MaxValue"/>.</exception>
    internal Frame(
        Size size,
        int maxTextBytes,
        Ambiguous ambiguousWidth,
        int maxPlacements)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxTextBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxPlacements);

        ArgumentOutOfRangeException.ThrowIfNotDefined(ambiguousWidth, nameof(ambiguousWidth), "The ambiguous-width policy is unknown.");

        var cellCount = checked(size.Width * size.Height);
        var cells = ArrayPool<Cell>.Shared.Rent(Math.Max(1, cellCount));
        var rowFingerprints = ArrayPool<ulong>.Shared.Rent(Math.Max(1, size.Height));

        try
        {
            var initialTextLength = Math.Min(256, maxTextBytes);
            _text = ArrayPool<byte>.Shared.Rent(initialTextLength);
        }
        catch
        {
            ArrayPool<Cell>.Shared.Return(cells, clearArray: true);
            ArrayPool<ulong>.Shared.Return(rowFingerprints, clearArray: true);
            throw;
        }

        _cells = cells;
        _rowFingerprints = rowFingerprints;
        MaxTextBytes = maxTextBytes;
        MaxPlacements = maxPlacements;
        AmbiguousWidth = ambiguousWidth;
        Size = size;
        FillBlank(CellStyle.Default);
    }

    /// <summary>Gets the immutable frame dimensions in terminal cells.</summary>
    public Size Size { get; }

    /// <summary>Gets the finite UTF-8 arena limit.</summary>
    public int MaxTextBytes { get; }

    /// <summary>Gets the finite semantic image-placement limit.</summary>
    public int MaxPlacements { get; }

    /// <summary>Gets the explicit East Asian Ambiguous width policy.</summary>
    public Ambiguous AmbiguousWidth { get; }

    /// <summary>Gets or sets the previous committed frame for on-demand cell copying.</summary>
    /// <remarks>
    /// When set, render-clean control subtrees can copy their cells from this reference
    /// instead of re-executing their render pass. The reference is borrowed; the renderer
    /// retains ownership and must keep it alive for the duration of rendering. This is
    /// deliberately not public: the committed frame is renderer-owned, and handing it to callers
    /// would let them mutate or dispose the baseline that damage tracking compares against.
    /// Use <see cref="Renderer.AttachCommittedFrame"/> to establish the link and the
    /// <see cref="TerminalCanvas.HasPreviousFrame"/> and <see cref="TerminalCanvas.CopyFromPrevious"/> pair to
    /// consume it.
    /// </remarks>
    internal Frame? PreviousFrame { get; set; }

    /// <summary>Gets the desired cursor state committed with this frame.</summary>
    public Cursor Cursor { get; private set; }

    /// <summary>Gets the number of semantic image placements in stable paint order.</summary>
    /// <exception cref="ObjectDisposedException">The frame is disposed.</exception>
    public int PlacementCount
    {
        get
        {
            ThrowIfDisposed();
            return _placementCount;
        }
    }

    /// <summary>Gets one semantic image placement by stable paint-order index.</summary>
    /// <param name="index">The zero-based placement index.</param>
    /// <returns>The immutable placement value.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the active snapshot.</exception>
    /// <exception cref="ObjectDisposedException">The frame is disposed.</exception>
    [Pure]
    public Placement GetPlacement(int index)
    {
        ThrowIfDisposed();

        return (uint) index < (uint) _placementCount
            ? _placements![index]
            : throw new ArgumentOutOfRangeException(nameof(index), index, "The placement index is outside the frame.");
    }

    /// <summary>Gets a full-frame canvas after validating ownership.</summary>
    /// <exception cref="ObjectDisposedException">The frame is disposed.</exception>
    public TerminalCanvas Canvas
    {
        get
        {
            ThrowIfDisposed();
            return new TerminalCanvas(this, Bounds);
        }
    }

    /// <summary>Gets semantic metadata for one cell.</summary>
    /// <param name="point">The in-bounds cell coordinate.</param>
    /// <returns>Non-owning semantic metadata.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="point"/> is outside the frame.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The frame is disposed.</exception>
    [Pure]
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
    [Pure]
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
    public void Clear(CellStyle style = default)
    {
        ThrowIfDisposed();
        ClearPlacements();
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
    public void SetCursor(Point position, bool visible) =>
        SetCursor(position, visible, CursorShape.Block);

    /// <summary>Sets the desired cursor state after validating frame geometry and shape.</summary>
    /// <param name="position">The in-bounds cell coordinate.</param>
    /// <param name="visible">Whether the terminal cursor is visible.</param>
    /// <param name="shape">The semantic cursor shape.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="position"/> is outside the frame or <paramref name="shape"/> is unknown.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The frame is disposed.</exception>
    public void SetCursor(Point position, bool visible, CursorShape shape)
    {
        ThrowIfDisposed();

        ArgumentOutOfRangeException.ThrowIfNotDefined(shape, nameof(shape), "The cursor shape is unknown.");

        var suspended = Size.Width == 0 || Size.Height == 0;

        if ((suspended && (position != default || visible)) ||
            (!suspended && !Bounds.Contains(position)))
        {
            throw new ArgumentOutOfRangeException(
                nameof(position),
                position,
                "The cursor position is outside the frame.");
        }

        Cursor = new Cursor(position, visible, shape);
    }

    /// <summary>Clears and returns every rented array. Disposal is idempotent.</summary>
    public void Dispose()
    {
        var cells = _cells;
        var text = _text;
        var rowFingerprints = _rowFingerprints;

        if (cells is null || text is null || rowFingerprints is null)
        {
            return;
        }

        _cells = null;
        _text = null;
        _rowFingerprints = null;
        ClearPlacements();
        var placements = _placements;
        _placements = null;
        _mutationRevision = 0;
        TextLength = 0;
        ArrayPool<Cell>.Shared.Return(cells, clearArray: true);
        ArrayPool<byte>.Shared.Return(text, clearArray: true);
        ArrayPool<ulong>.Shared.Return(rowFingerprints, clearArray: true);

        if (placements is not null)
        {
            ArrayPool<Placement>.Shared.Return(placements, clearArray: true);
        }
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

    /// <summary>Gets the latest frame-local semantic mutation revision.</summary>
    /// <remarks>Active captures prevent this value from wrapping through a successful callback.</remarks>
    /// <exception cref="ObjectDisposedException">The frame is disposed.</exception>
    internal ulong CurrentMutationRevision
    {
        get
        {
            ThrowIfDisposed();
            return _mutationRevision;
        }
    }

    /// <summary>Overwrites the mutation revision for exhaustion-path test seams.</summary>
    /// <param name="revision">The revision to seed.</param>
    internal void SeedMutationRevision(ulong revision)
    {
        ThrowIfDisposed();
        _mutationRevision = revision;
    }

    /// <summary>Creates an independent pooled copy of this active frame.</summary>
    /// <returns>A frame whose semantic state equals this frame.</returns>
    internal Frame Clone()
    {
        ThrowIfDisposed();
        var result = new Frame(Size, MaxTextBytes, AmbiguousWidth, MaxPlacements);

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
        EnsurePlacementCapacity(source._placementCount);
    }

    /// <summary>Copies compatible semantic state without retaining borrowed storage.</summary>
    /// <param name="source">The compatible active source frame.</param>
    internal void CopyFrom(Frame source)
    {
        ValidateCopySource(source);
        Debug.Assert(_mutationCaptureDepth == 0, "A destination capture cannot replace its revision history.");
        Debug.Assert(source._mutationCaptureDepth == 0, "An active source capture cannot be copied.");
        Debug.Assert(
            source.TextLength <= Text.Length,
            "Copy storage must be prepared before terminal output is committed.");
        Text[..TextLength].Clear();
        source.Text[..source.TextLength].CopyTo(Text);
        source.Cells.CopyTo(Cells);
        ClearPlacements();

        if (source._placementCount != 0)
        {
            source._placements.AsSpan(0, source._placementCount).CopyTo(_placements);
            _placementCount = source._placementCount;
        }
        _mutationRevision = source._mutationRevision;
        _rowFingerprintsValid = false;
        TextLength = source.TextLength;
        Cursor = source.Cursor;
    }

    /// <summary>Copies cells from a compatible source frame within a bounded region.</summary>
    /// <param name="source">The compatible active source frame with the same dimensions.</param>
    /// <param name="region">The region to copy, intersected with the frame bounds.</param>
    /// <remarks>
    /// <para>
    /// Grapheme data is re-appended to this frame's text arena with updated offsets. Blank cells
    /// are copied without text arena interaction.
    /// </para>
    /// <para>
    /// The region is a boundary, not a suggestion. A wide cluster that straddles it would leave an
    /// orphan lead or an orphan continuation behind, so any cell whose complete owner does not fit
    /// inside the region is written blank instead of copied. Every destination cell in the region is
    /// also repaired before the copy overwrites it, the same way <see cref="TerminalCanvas.Clear"/> repairs
    /// before blanking, so a destination wide owner straddling the region edge never survives with
    /// only one of its two cells intact. That repair can blank one destination cell just outside the
    /// region when a boundary continuation's lead sits there — the same reach <see cref="TerminalCanvas.Clear"/>
    /// already has, since a partial wide owner is not a valid frame regardless of which side of the
    /// boundary it started on.
    /// </para>
    /// <para>
    /// Compatibility and the arena bound are checked before the first write, so a rejected copy
    /// never leaves the destination partially mutated.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// The source geometry or width policy differs, or the copy would exceed the finite arena.
    /// </exception>
    internal void CopyRegionFrom(Frame source, Rect region)
    {
        Debug.Assert(source is not null, "A region copy always has a source frame.");
        ThrowIfDisposed();
        source.ThrowIfDisposed();

        if (source.Size != Size || source.AmbiguousWidth != AmbiguousWidth)
        {
            // Using this frame's stride against a differently sized source would silently read
            // the wrong row, so reject before touching any destination cell.
            throw new InvalidOperationException(
                "The previous frame geometry and width policy must match this frame.");
        }

        var target = Bounds.Intersect(region).Intersect(source.Bounds);

        if (target.Width == 0 || target.Height == 0)
        {
            return;
        }

        var required = MeasureRegionText(source, target);
        EnsureAppendable(required);
        EnsureCapacity(checked(TextLength + required));
        var revision = NextMutationRevision();

        // Repair every destination cell in the region before any of them is overwritten, the same
        // way Canvas.Clear repairs before blanking. Without this, a destination wide owner
        // straddling the region edge keeps one of its two cells while the other is overwritten by
        // the copy below, leaving an orphan lead or an orphan continuation behind.
        for (var y = target.Y; y < target.Bottom; y++)
        {
            var rowStart = checked(y * Size.Width);

            for (var x = target.X; x < target.Right; x++)
            {
                Repair(checked(rowStart + x), revision);
            }
        }

        for (var y = target.Y; y < target.Bottom; y++)
        {
            var rowStart = checked(y * Size.Width);

            for (var x = target.X; x < target.Right; x++)
            {
                var index = checked(rowStart + x);
                var cell = source.Cells[index];

                if (!IsCompleteWithinRow(cell, x, rowStart, target))
                {
                    Cells[index] = Cell.Blank(cell.Style, revision);
                    continue;
                }

                if (cell.IsContinuation || cell.Length == 0)
                {
                    // Blank or continuation: no grapheme data to copy. Absolute lead indices are
                    // stable across same-sized frames, so the reference stays correct.
                    cell.MutationRevision = revision;
                    Cells[index] = cell;
                }
                else
                {
                    // Lead cell: re-append grapheme bytes and adjust offset.
                    var grapheme = source.Text.Slice(cell.Offset, cell.Length);
                    grapheme.CopyTo(Text[TextLength..]);
                    cell.Offset = TextLength;
                    cell.MutationRevision = revision;
                    TextLength += cell.Length;
                    Cells[index] = cell;
                }
            }
        }
    }

    private static bool IsCompleteWithinRow(in Cell cell, int x, int rowStart, Rect target)
    {
        // A continuation owns nothing itself; it is only valid while its lead travels with it.
        if (cell.IsContinuation)
        {
            return cell.LeadIndex >= checked(rowStart + target.X);
        }

        // A lead must bring all of its continuation columns along.
        return cell.Length == 0 || checked(x + cell.Width) <= target.Right;
    }

    private static int MeasureRegionText(Frame source, Rect target)
    {
        // Counting first is what makes the arena bound enforceable: an over-budget region is
        // rejected whole instead of appending until it runs out midway through a row.
        var total = 0;

        for (var y = target.Y; y < target.Bottom; y++)
        {
            var rowStart = checked(y * source.Size.Width);

            for (var x = target.X; x < target.Right; x++)
            {
                var cell = source.Cells[checked(rowStart + x)];

                if (!cell.IsContinuation &&
                    cell.Length != 0 &&
                    IsCompleteWithinRow(cell, x, rowStart, target))
                {
                    total = checked(total + cell.Length);
                }
            }
        }

        return total;
    }

    /// <summary>Gets a copied internal cell by absolute row-major index.</summary>
    /// <param name="index">The validated absolute index.</param>
    /// <returns>The internal semantic cell.</returns>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Cell GetCellByIndex(int index)
    {
        Debug.Assert((uint) index < (uint) Cells.Length, "Internal cell indexes are bounded.");
        return Cells[index];
    }

    /// <summary>Gets borrowed complete lead grapheme bytes by absolute index.</summary>
    /// <param name="index">The validated lead or continuation index.</param>
    /// <returns>Borrowed bytes valid until frame mutation or disposal.</returns>
    [Pure]
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
    [Pure]
    internal int GetLeadColumn(int row, int column)
    {
        var index = checked((row * Size.Width) + column);
        var cell = GetCellByIndex(index);
        return cell.IsContinuation ? cell.LeadIndex % Size.Width : column;
    }

    /// <summary>Gets the exclusive owned-cell end for a row-local cell.</summary>
    /// <param name="row">The validated row.</param>
    /// <param name="column">The validated column.</param>
    /// <returns>The exclusive column after the complete owner.</returns>
    [Pure]
    internal int GetOwnedEnd(int row, int column)
    {
        var leadColumn = GetLeadColumn(row, column);
        var lead = GetCellByIndex(checked((row * Size.Width) + leadColumn));
        return Math.Min(Size.Width, leadColumn + Math.Max(1, (int) lead.Width));
    }

    /// <summary>Adds one validated placement after capacity has been proven.</summary>
    /// <param name="placement">The immutable placement retaining its image.</param>
    /// <exception cref="ArgumentException"><paramref name="placement"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The destination is not contained by this frame.</exception>
    /// <exception cref="InvalidOperationException">The finite placement limit is exhausted.</exception>
    internal void AddPlacement(Placement placement)
    {
        ThrowIfDisposed();

        if (placement.IsEmpty)
        {
            throw new ArgumentException("An active frame placement cannot be empty.", nameof(placement));
        }

        if (Bounds.Intersect(placement.Destination) != placement.Destination)
        {
            throw new ArgumentOutOfRangeException(
                nameof(placement),
                placement,
                "An active placement destination must be contained by the frame.");
        }

        if (_placementCount == MaxPlacements)
        {
            throw new InvalidOperationException("The frame placement limit would be exceeded.");
        }

        EnsurePlacementCapacity(_placementCount + 1);
        _placements![_placementCount++] = placement.PaintRevision.HasValue
            ? placement
            : placement.WithPaintRevision(_mutationRevision);
    }

    /// <summary>Compares the complete ordered placement snapshot.</summary>
    /// <param name="other">The other active frame.</param>
    /// <returns>Whether image identity and placement geometry are equal.</returns>
    [Pure]
    internal bool PlacementsEqual(Frame other)
    {
        ThrowIfDisposed();
        other.ThrowIfDisposed();

        return _placementCount == other._placementCount &&
               (_placementCount == 0 ||
                _placements.AsSpan(0, _placementCount).SequenceEqual(
                    other._placements.AsSpan(0, other._placementCount)));
    }

    /// <summary>Compares ordered semantic placements and their effective cell-paint visibility.</summary>
    /// <param name="other">The other active frame.</param>
    /// <returns>Whether both frames expose the same effective placement snapshot.</returns>
    [Pure]
    internal bool EffectivePlacementsEqual(Frame other)
    {
        if (!PlacementsEqual(other))
        {
            return false;
        }

        for (var index = 0; index < _placementCount; index++)
        {
            if (IsPlacementEffective(index) != other.IsPlacementEffective(index))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Gets whether no later cell mutation intersects one ordered placement.</summary>
    /// <param name="index">The validated zero-based placement index.</param>
    /// <returns>Whether a graphics backend may render the placement above its cell underlay.</returns>
    [Pure]
    internal bool IsPlacementEffective(int index)
    {
        var placement = GetPlacement(index);

        if (placement.PaintRevision is not { } paintRevision)
        {
            return true;
        }

        for (var y = placement.Destination.Y; y < placement.Destination.Bottom; y++)
        {
            var offset = checked(y * Size.Width);

            for (var x = placement.Destination.X; x < placement.Destination.Right; x++)
            {
                if (Cells[offset + x].MutationRevision > paintRevision)
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>Compares one cell semantically with a same-sized frame.</summary>
    /// <param name="other">The other active frame.</param>
    /// <param name="index">The validated absolute index.</param>
    /// <returns>Whether metadata and complete lead bytes are equal.</returns>
    [Pure]
    internal bool SemanticallyEquals(Frame other, int index)
    {
        Debug.Assert(Size == other.Size, "Semantic cell comparison requires equal dimensions.");
        return CellSemanticallyEquals(other, index, index);
    }

    /// <summary>Compares complete semantic rows at independent coordinates.</summary>
    /// <param name="other">The other same-sized frame.</param>
    /// <param name="row">This frame's zero-based row.</param>
    /// <param name="otherRow">The other frame's zero-based row.</param>
    /// <returns>Whether every cell and complete lead grapheme is equal.</returns>
    [Pure]
    internal bool RowSemanticallyEquals(Frame other, int row, int otherRow)
    {
        Debug.Assert(Size == other.Size, "Semantic row comparison requires equal dimensions.");
        Debug.Assert((uint) row < (uint) Size.Height, "The source row is inside the frame.");
        Debug.Assert((uint) otherRow < (uint) Size.Height, "The target row is inside the frame.");
        var left = checked(row * Size.Width);
        var right = checked(otherRow * Size.Width);

        for (var column = 0; column < Size.Width; column++)
        {
            if (!CellSemanticallyEquals(other, left + column, right + column))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Gets a cached mismatch prefilter for one complete semantic row.</summary>
    /// <param name="row">The validated zero-based row.</param>
    /// <returns>A fingerprint whose equality must still be confirmed semantically.</returns>
    internal ulong RowSemanticFingerprint(int row)
    {
        Debug.Assert((uint) row < (uint) Size.Height, "The fingerprint row is inside the frame.");

        if (!_rowFingerprintsValid || _rowFingerprintRevision != _mutationRevision)
        {
            RefreshRowFingerprints();
        }

        return _rowFingerprints![row];
    }

    /// <summary>Compares cells at independent absolute indices.</summary>
    /// <param name="other">The other same-sized frame.</param>
    /// <param name="leftIndex">This frame's absolute cell index.</param>
    /// <param name="rightIndex">The other frame's absolute cell index.</param>
    /// <returns>Whether metadata and complete lead bytes are equal.</returns>
    [Pure]
    internal bool CellSemanticallyEquals(Frame other, int leftIndex, int rightIndex)
    {
        var left = GetCellByIndex(leftIndex);
        var right = other.GetCellByIndex(rightIndex);

        return left.Width == right.Width &&
               (!left.IsContinuation || left.LeadIndex % Size.Width == right.LeadIndex % Size.Width) &&
               left.Style == right.Style &&
               left.IsContinuation == right.IsContinuation &&
               (left.IsContinuation ||
                (left.Hash == right.Hash &&
                 left.Length == right.Length &&
                 GetGrapheme(leftIndex).SequenceEqual(other.GetGrapheme(rightIndex))));
    }

    /// <summary>Gets a validated absolute index for an in-bounds point.</summary>
    /// <param name="point">The point to validate.</param>
    /// <returns>The absolute row-major index.</returns>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        var index = GetIndex(point);
        Debug.Assert(index + width <= Cells.Length, "Canvas edge handling guarantees cell capacity.");
        var revision = NextMutationRevision();
        Repair(index, revision);

        if (width == 2)
        {
            Repair(index + 1, revision);
        }

        var offset = TextLength;
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
            MutationRevision = revision
        };

        if (width == 2)
        {
            Cells[index + 1] = Cell.Continuation(index, style, revision);
        }
    }

    /// <summary>Repairs the complete glyph owning an absolute cell index.</summary>
    /// <param name="index">The validated absolute index.</param>
    internal void Repair(int index)
    {
        Debug.Assert((uint) index < (uint) Cells.Length, "Repair indexes are validated by the canvas.");
        Repair(index, NextMutationRevision());
    }

    private void Repair(int index, ulong revision)
    {
        Debug.Assert((uint) index < (uint) Cells.Length, "Repair indexes are validated by the canvas.");
        var cell = Cells[index];
        var leadIndex = cell.IsContinuation ? cell.LeadIndex : index;
        var lead = Cells[leadIndex];
        var width = Math.Max(1, (int) lead.Width);
        var style = lead.Style;

        for (var offset = 0; offset < width && leadIndex + offset < Cells.Length; offset++)
        {
            Cells[leadIndex + offset] = Cell.Blank(style, revision);
        }
    }

    /// <summary>Sets one already-repaired cell to a semantic blank.</summary>
    /// <param name="index">The validated absolute index.</param>
    /// <param name="style">The blank style.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void SetBlank(int index, CellStyle style)
    {
        Debug.Assert((uint) index < (uint) Cells.Length, "Blank indexes are validated by the canvas.");
        Cells[index] = Cell.Blank(style, NextMutationRevision());
    }

    /// <summary>Styles a complete cell owner when every occupied cell is inside a clip.</summary>
    /// <param name="index">The validated lead, continuation, or blank index.</param>
    /// <param name="clip">The effective frame clip.</param>
    /// <param name="style">The replacement semantic style.</param>
    /// <returns>Whether the complete owner was styled.</returns>
    internal bool TrySetOwnerStyle(int index, Rect clip, CellStyle style)
    {
        Debug.Assert((uint) index < (uint) Cells.Length, "CellStyle indexes are validated by the canvas.");
        var leadIndex = ResolveLead(index);
        var lead = Cells[leadIndex];
        var width = Math.Max(1, (int) lead.Width);
        var point = new Point(leadIndex % Size.Width, leadIndex / Size.Width);

        for (var offset = 0; offset < width; offset++)
        {
            if (!clip.Contains(new Point(point.X + offset, point.Y)))
            {
                return false;
            }
        }

        var revision = NextMutationRevision();

        for (var offset = 0; offset < width; offset++)
        {
            var cell = Cells[leadIndex + offset];
            cell.Style = style;
            cell.MutationRevision = revision;
            Cells[leadIndex + offset] = cell;
        }

        return true;
    }

    /// <summary>Counts UTF-8 bytes using the frame's replacement policy.</summary>
    /// <param name="value">The borrowed UTF-16 cluster.</param>
    /// <returns>The encoded byte count.</returns>
    [Pure]
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

    /// <inheritdoc/>
    public override string ToString() => $"Frame {{ Size={Size} }}";

    /// <summary>Throws when pooled frame ownership has ended.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_cells is null, this);

    /// <summary>Begins one nested synchronous mutation capture and returns its exclusive checkpoint.</summary>
    /// <returns>The revision before the drawing callback can mutate this frame.</returns>
    /// <exception cref="InvalidOperationException">The nesting depth is exhausted.</exception>
    /// <exception cref="ObjectDisposedException">The frame is disposed.</exception>
    internal ulong BeginMutationCapture()
    {
        ThrowIfDisposed();

        if (_mutationCaptureDepth == int.MaxValue)
        {
            throw new InvalidOperationException("The synchronous frame mutation capture nesting limit was exceeded.");
        }

        // Normal captures preserve the monotonic history and remain O(1). Rebase only at the
        // unreachable-in-practice wrap boundary, before a checkpoint can observe wrapped values.
        if (_mutationCaptureDepth == 0 && _mutationRevision == ulong.MaxValue)
        {
            RebaseMutationRevisions();
        }

        _mutationCaptureDepth++;
        return _mutationRevision;
    }

    /// <summary>Ends the innermost synchronous mutation capture.</summary>
    internal void EndMutationCapture()
    {
        Debug.Assert(_mutationCaptureDepth > 0, "Only an active mutation capture can end.");
        _mutationCaptureDepth--;
    }

    private int Append(ReadOnlySpan<char> value)
    {
        var byteCount = CountUtf8(value);
        EnsureAppendable(byteCount);
        EnsureCapacity(checked(TextLength + byteCount));
        var start = TextLength;
        var position = 0;

        while (position < value.Length)
        {
            var status = Rune.DecodeFromUtf16(value[position..], out var rune, out var consumed);

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

        var doubled = Text.Length > MaxTextBytes / 2
            ? MaxTextBytes
            : Text.Length * 2;
        var length = Math.Max(required, doubled);
        var replacement = ArrayPool<byte>.Shared.Rent(length);
        Text[..TextLength].CopyTo(replacement);
        var previous = _text;
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

        if (source._placementCount > MaxPlacements)
        {
            throw new ArgumentException(
                "The source frame placements exceed this frame's finite limit.",
                nameof(source));
        }
    }

    private void ClearPlacements()
    {
        _placements.AsSpan(0, _placementCount).Clear();
        _placementCount = 0;
    }

    private void EnsurePlacementCapacity(int required)
    {
        if (required == 0 || (_placements is not null && required <= _placements.Length))
        {
            return;
        }

        var current = _placements?.Length ?? 0;
        var doubled = current == 0 ? Math.Min(16, MaxPlacements) : Math.Min(MaxPlacements, current * 2);
        var replacement = ArrayPool<Placement>.Shared.Rent(Math.Max(required, doubled));
        _placements.AsSpan(0, _placementCount).CopyTo(replacement);
        var previous = _placements;
        _placements = replacement;

        if (previous is not null)
        {
            ArrayPool<Placement>.Shared.Return(previous, clearArray: true);
        }
    }

    private void FillBlank(CellStyle style)
    {
        var cells = Cells;
        var revision = NextMutationRevision();

        for (var index = 0; index < cells.Length; index++)
        {
            cells[index] = Cell.Blank(style, revision);
        }
    }

    private ulong NextMutationRevision()
    {
        if (_mutationRevision == ulong.MaxValue)
        {
            if (_mutationCaptureDepth != 0)
            {
                throw new InvalidOperationException(
                    "A synchronous drawing callback exceeded the frame mutation revision capacity.");
            }

            RebaseMutationRevisions();
        }

        _mutationRevision++;
        return _mutationRevision;
    }

    private void RebaseMutationRevisions()
    {
        Debug.Assert(_mutationCaptureDepth == 0, "Active mutation checkpoints cannot be rebased.");

        // Placement paint revisions must stay ordered against every cell mutation. Rewriting
        // that history would silently change z-order, so the finite counter cannot rebase while
        // any semantic image placement remains active.
        if (_placementCount != 0)
        {
            throw new InvalidOperationException(
                "The frame mutation revision capacity was exceeded while image placements remained active.");
        }

        var cells = Cells;

        for (var index = 0; index < cells.Length; index++)
        {
            var cell = cells[index];
            cell.MutationRevision = 0;
            cells[index] = cell;
        }

        _mutationRevision = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int ResolveLead(int index)
    {
        var cell = Cells[index];
        return cell.IsContinuation ? cell.LeadIndex : index;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

    private void RefreshRowFingerprints()
    {
        const ulong offsetBasis = 14695981039346656037;

        for (var row = 0; row < Size.Height; row++)
        {
            var value = offsetBasis;
            var offset = checked(row * Size.Width);

            for (var column = 0; column < Size.Width; column++)
            {
                var cell = GetCellByIndex(offset + column);
                value = MixCellFingerprint(
                    value,
                    cell.Width,
                    (uint) (cell.IsContinuation ? (cell.LeadIndex % Size.Width) + 2 : 1),
                    (uint) cell.Style.GetHashCode(),
                    cell.Hash,
                    (uint) cell.Length);
            }

            _rowFingerprints![row] = value;
        }

        _rowFingerprintRevision = _mutationRevision;
        _rowFingerprintsValid = true;
    }

    /// <summary>Mixes one cell's semantic fields into a row fingerprint accumulator.</summary>
    /// <remarks>
    /// Shared with <see cref="Damage"/> so a row hashes identically whether its content comes from
    /// committed cells or from synthetic overlay-projected fields standing in for them.
    /// </remarks>
    internal static ulong MixCellFingerprint(
        ulong value,
        uint width,
        uint continuationMarker,
        uint styleHash,
        uint semanticHash,
        uint length)
    {
        const ulong prime = 1099511628211;
        value = (value ^ width) * prime;
        value = (value ^ continuationMarker) * prime;
        value = (value ^ styleHash) * prime;
        value = (value ^ semanticHash) * prime;
        value = (value ^ length) * prime;
        return value;
    }
}
