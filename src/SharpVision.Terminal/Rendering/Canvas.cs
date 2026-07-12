using System.Buffers;
using System.Diagnostics;
using System.Text;

using SharpVision.Terminal.Geometry;
using SharpVision.Terminal.Unicode;

namespace SharpVision.Terminal.Rendering;

/// <summary>
/// Draws semantic grapheme clusters into a clipped frame cell region.
/// </summary>
public readonly struct Canvas
{
    private const int _tabWidth = 4;
    private readonly Frame _frame;
    private readonly Rect _clip;

    /// <summary>Initializes a frame-owned clipped canvas.</summary>
    /// <param name="frame">The owning frame.</param>
    /// <param name="clip">The validated frame intersection.</param>
    internal Canvas(Frame frame, Rect clip)
    {
        _frame = frame;
        _clip = clip;
    }

    /// <summary>Gets the effective half-open clipping rectangle.</summary>
    public Rect Bounds
    {
        get
        {
            _frame.ThrowIfDisposed();
            return _clip;
        }
    }

    /// <summary>Creates a child canvas clipped to the requested rectangle.</summary>
    /// <param name="clip">The requested rectangle in frame coordinates.</param>
    /// <returns>A canvas using the geometric intersection.</returns>
    /// <exception cref="ObjectDisposedException">The owning frame is disposed.</exception>
    public Canvas Clip(Rect clip)
    {
        _frame.ThrowIfDisposed();
        return new Canvas(_frame, _clip.Intersect(clip).Intersect(_frame.Bounds));
    }

    #region Drawing primitives

    /// <summary>Draws one validated printable narrow Rune.</summary>
    /// <param name="value">The Rune to draw.</param>
    /// <param name="origin">The absolute frame cell.</param>
    /// <param name="style">The semantic cell style.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> is a control or does not occupy exactly one cell.
    /// </exception>
    /// <exception cref="InvalidOperationException">The finite frame arena would be exceeded.</exception>
    /// <exception cref="ObjectDisposedException">The owning frame is disposed.</exception>
    public void DrawRune(Rune value, Point origin, Style style = default)
    {
        Span<char> buffer = stackalloc char[2];
        var length = ValidateRune(value, buffer);
        _ = Draw(buffer[..length], origin, style);
    }

    /// <summary>Fills a clipped region with one validated printable narrow Rune.</summary>
    /// <param name="region">The requested half-open frame region.</param>
    /// <param name="value">The Rune repeated into every visible cell.</param>
    /// <param name="style">The semantic cell style.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> is a control or does not occupy exactly one cell.
    /// </exception>
    /// <exception cref="InvalidOperationException">The finite frame arena would be exceeded.</exception>
    /// <exception cref="ObjectDisposedException">The owning frame is disposed.</exception>
    public void Fill(Rect region, Rune value, Style style = default)
    {
        Span<char> buffer = stackalloc char[2];
        var length = ValidateRune(value, buffer);
        var target = _clip.Intersect(region).Intersect(_frame.Bounds);
        var bytes = checked(target.Width * target.Height * Frame.CountUtf8(buffer[..length]));
        _frame.EnsureAppendable(bytes);

        for (var y = target.Y; y < target.Bottom; y++)
        {
            for (var x = target.X; x < target.Right; x++)
            {
                _frame.Write(new Point(x, y), buffer[..length], 1, style);
            }
        }
    }

    /// <summary>Applies a style while preserving complete stored graphemes.</summary>
    /// <param name="region">The requested half-open frame region.</param>
    /// <param name="style">The replacement semantic style.</param>
    /// <remarks>
    /// Touching any cell of a wide owner styles the complete owner when all of
    /// its cells are inside this canvas clip. A partially clipped owner is
    /// skipped so lead and continuation styles cannot disagree.
    /// </remarks>
    /// <exception cref="ObjectDisposedException">The owning frame is disposed.</exception>
    public void ApplyStyle(Rect region, Style style)
    {
        _frame.ThrowIfDisposed();
        var target = _clip.Intersect(region).Intersect(_frame.Bounds);

        for (var y = target.Y; y < target.Bottom; y++)
        {
            for (var x = target.X; x < target.Right; x++)
            {
                _ = _frame.TrySetOwnerStyle(_frame.GetIndex(new Point(x, y)), _clip, style);
            }
        }
    }

    /// <summary>Draws a clipped horizontal line and merges crossing topology.</summary>
    /// <param name="origin">The absolute first cell.</param>
    /// <param name="length">The non-negative cell count.</param>
    /// <param name="line">The validated line family.</param>
    /// <param name="style">The semantic cell style.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is negative.</exception>
    /// <exception cref="ObjectDisposedException">The owning frame is disposed.</exception>
    public void DrawHorizontalLine(Point origin, int length, LineStyle line, Style style = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        _frame.ThrowIfDisposed();

        for (var offset = 0; offset < length; offset++)
        {
            DrawLineCell(
                new Point(checked(origin.X + offset), origin.Y),
                Connections.Left | Connections.Right,
                line,
                style);
        }
    }

    /// <summary>Draws a clipped vertical line and merges crossing topology.</summary>
    /// <param name="origin">The absolute first cell.</param>
    /// <param name="length">The non-negative cell count.</param>
    /// <param name="line">The validated line family.</param>
    /// <param name="style">The semantic cell style.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is negative.</exception>
    /// <exception cref="ObjectDisposedException">The owning frame is disposed.</exception>
    public void DrawVerticalLine(Point origin, int length, LineStyle line, Style style = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        _frame.ThrowIfDisposed();

        for (var offset = 0; offset < length; offset++)
        {
            DrawLineCell(
                new Point(origin.X, checked(origin.Y + offset)),
                Connections.Up | Connections.Down,
                line,
                style);
        }
    }

    /// <summary>Draws a clipped one-cell box and resolves exact corners.</summary>
    /// <param name="bounds">The half-open outer box bounds.</param>
    /// <param name="line">The validated line family.</param>
    /// <param name="style">The semantic cell style.</param>
    /// <exception cref="ObjectDisposedException">The owning frame is disposed.</exception>
    public void DrawBox(Rect bounds, LineStyle line, Style style = default)
    {
        _frame.ThrowIfDisposed();

        if (bounds.Width == 0 || bounds.Height == 0)
        {
            return;
        }

        if (bounds.Width == 1)
        {
            DrawVerticalLine(new Point(bounds.X, bounds.Y), bounds.Height, line, style);
            return;
        }

        if (bounds.Height == 1)
        {
            DrawHorizontalLine(new Point(bounds.X, bounds.Y), bounds.Width, line, style);
            return;
        }

        DrawHorizontalLine(new Point(bounds.X + 1, bounds.Y), bounds.Width - 2, line, style);
        DrawHorizontalLine(new Point(bounds.X + 1, bounds.Bottom - 1), bounds.Width - 2, line, style);
        DrawVerticalLine(new Point(bounds.X, bounds.Y + 1), bounds.Height - 2, line, style);
        DrawVerticalLine(new Point(bounds.Right - 1, bounds.Y + 1), bounds.Height - 2, line, style);
        DrawLineCell(
            new Point(bounds.X, bounds.Y),
            Connections.Right | Connections.Down,
            line,
            style);
        DrawLineCell(
            new Point(bounds.Right - 1, bounds.Y),
            Connections.Down | Connections.Left,
            line,
            style);
        DrawLineCell(
            new Point(bounds.X, bounds.Bottom - 1),
            Connections.Up | Connections.Right,
            line,
            style);
        DrawLineCell(
            new Point(bounds.Right - 1, bounds.Bottom - 1),
            Connections.Up | Connections.Left,
            line,
            style);
    }

    /// <summary>Fills a clipped region with a standard shade or solid block.</summary>
    /// <param name="region">The requested half-open frame region.</param>
    /// <param name="shade">The shade family.</param>
    /// <param name="style">The semantic cell style.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="shade"/> is unknown.</exception>
    /// <exception cref="InvalidOperationException">The finite frame arena would be exceeded.</exception>
    /// <exception cref="ObjectDisposedException">The owning frame is disposed.</exception>
    public void FillShade(Rect region, Shade shade, Style style = default)
    {
        if (!Enum.IsDefined(shade))
        {
            throw new ArgumentOutOfRangeException(nameof(shade), shade, "The shade is unknown.");
        }

        Fill(region, BlockResolver.Resolve(shade), style);
    }

    /// <summary>Draws and merges filled quadrants in one clipped cell.</summary>
    /// <param name="point">The absolute frame cell.</param>
    /// <param name="quadrants">The quadrant mask.</param>
    /// <param name="style">The semantic cell style.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="quadrants"/> contains unknown flags.
    /// </exception>
    /// <exception cref="InvalidOperationException">The finite frame arena would be exceeded.</exception>
    /// <exception cref="ObjectDisposedException">The owning frame is disposed.</exception>
    public void DrawQuadrants(Point point, Quadrants quadrants, Style style = default)
    {
        if ((quadrants & ~Quadrants.All) != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quadrants),
                quadrants,
                "The quadrant mask contains unknown flags.");
        }

        _frame.ThrowIfDisposed();

        if (quadrants == Quadrants.None || !_frame.Bounds.Contains(point) || !_clip.Contains(point))
        {
            return;
        }

        var bytes = _frame.GetGrapheme(_frame.GetIndex(point));

        if (Rune.DecodeFromUtf8(bytes, out var existing, out var consumed) == OperationStatus.Done &&
            consumed == bytes.Length &&
            BlockResolver.TryDecode(existing, out var previous))
        {
            quadrants |= previous;
        }

        DrawRune(BlockResolver.Resolve(quadrants), point, style);
    }

    #endregion

    /// <summary>Draws borrowed UTF-16 text as complete semantic graphemes.</summary>
    /// <param name="value">The borrowed UTF-16 text.</param>
    /// <param name="origin">The logical starting cell coordinate.</param>
    /// <param name="style">The semantic style.</param>
    /// <param name="edge">The wide-cluster right-edge behavior.</param>
    /// <returns>Logical advance and clipping metrics.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="edge"/> is unknown.</exception>
    /// <exception cref="InvalidOperationException">The finite frame arena would be exceeded.</exception>
    /// <exception cref="ObjectDisposedException">The owning frame is disposed.</exception>
    public DrawResult Draw(
        ReadOnlySpan<char> value,
        Point origin,
        Style style = default,
        Edge edge = Edge.Clip)
    {
        _frame.ThrowIfDisposed();

        if (!Enum.IsDefined(edge))
        {
            throw new ArgumentOutOfRangeException(nameof(edge), edge, "The edge policy is unknown.");
        }

        var preflight = Process(value, origin, style, edge, write: false, out var bytes);
        _frame.EnsureAppendable(bytes);
        var result = Process(value, origin, style, edge, write: true, out var written);
        Debug.Assert(preflight == result, "Canvas preflight and mutation passes must agree.");
        Debug.Assert(bytes == written, "Canvas UTF-8 preflight and mutation must agree.");

        return result;
    }

    /// <summary>Clears a clipped region while repairing complete wide owners.</summary>
    /// <param name="region">The requested region in frame coordinates.</param>
    /// <param name="style">The semantic blank style inside the region.</param>
    /// <exception cref="ObjectDisposedException">The owning frame is disposed.</exception>
    public void Clear(Rect region, Style style = default)
    {
        _frame.ThrowIfDisposed();
        var target = _clip.Intersect(region).Intersect(_frame.Bounds);

        for (var y = target.Y; y < target.Bottom; y++)
        {
            for (var x = target.X; x < target.Right; x++)
            {
                _frame.Repair(_frame.GetIndex(new Point(x, y)));
            }
        }

        for (var y = target.Y; y < target.Bottom; y++)
        {
            for (var x = target.X; x < target.Right; x++)
            {
                _frame.SetBlank(_frame.GetIndex(new Point(x, y)), style);
            }
        }
    }

    /// <summary>Sets the owning frame cursor through this borrowed canvas.</summary>
    /// <param name="position">The in-frame cursor cell.</param>
    /// <param name="visible">Whether the terminal cursor is visible.</param>
    /// <exception cref="ArgumentOutOfRangeException">The position is outside the frame.</exception>
    /// <exception cref="ObjectDisposedException">The owning frame is disposed.</exception>
    public void SetCursor(Point position, bool visible) => _frame.SetCursor(position, visible);

    private DrawResult Process(
        ReadOnlySpan<char> value,
        Point origin,
        Style style,
        Edge edge,
        bool write,
        out int bytes)
    {
        var x = origin.X;
        var y = origin.Y;
        var graphemes = 0;
        var cells = 0;
        var clipped = 0;
        var replaced = 0;
        bytes = 0;

        foreach (var segment in Graphemes.Enumerate(value))
        {
            var cluster = value.Slice(segment.Offset, segment.Length);
            var classification = Width.AnalyzeCluster(
                cluster,
                _frame.AmbiguousWidth,
                segment.HasInvalidData);
            graphemes = checked(graphemes + 1);

            if (classification.Width == CellWidth.Control)
            {
                AdvanceControl(cluster, origin.X, ref x, ref y);
                continue;
            }

            var cellWidth = (int) classification.Width;
            cells = checked(cells + cellWidth);
            var replacement = classification.RequiresReplacement;

            if (replacement)
            {
                replaced = checked(replaced + 1);
            }

            if (cellWidth == 2 && x + cellWidth > _frame.Size.Width)
            {
                switch (edge)
                {
                    case Edge.Clip:
                        clipped = checked(clipped + 1);
                        x = checked(x + cellWidth);
                        continue;

                    case Edge.Wrap:
                        x = 0;
                        y = checked(y + 1);
                        break;

                    case Edge.Replace:
                        cellWidth = 1;

                        if (!replacement)
                        {
                            replaced = checked(replaced + 1);
                        }

                        replacement = true;
                        break;

                    default:
                        throw new UnreachableException();
                }
            }

            var point = new Point(x, y);
            var visible = _frame.Bounds.Contains(point) &&
                _clip.Contains(point) &&
                (cellWidth == 1 ||
                    (_frame.Bounds.Contains(new Point(x + 1, y)) &&
                        _clip.Contains(new Point(x + 1, y))));

            if (!visible)
            {
                clipped = checked(clipped + 1);
                x = checked(x + cellWidth);
                continue;
            }

            var stored = replacement ? "�".AsSpan() : cluster;
            bytes = checked(bytes + Frame.CountUtf8(stored));

            if (write)
            {
                _frame.Write(point, stored, cellWidth, style);
            }

            x = checked(x + cellWidth);
        }

        return new DrawResult(new Point(x, y), graphemes, cells, clipped, replaced);
    }

    private static void AdvanceControl(
        ReadOnlySpan<char> value,
        int lineOrigin,
        ref int x,
        ref int y)
    {
        if (value.Contains('\r') || value.Contains('\n'))
        {
            x = lineOrigin;
            y = checked(y + 1);
        }
        else if (value.Contains('\t'))
        {
            var relative = Math.Max(0, x - lineOrigin);
            x = checked(x + (_tabWidth - (relative % _tabWidth)));
        }
    }

    private static int ValidateRune(Rune value, Span<char> buffer)
    {
        var length = value.EncodeToUtf16(buffer);
        var measurement = Width.Measure(buffer[..length]);

        return measurement.Cells == 1 && measurement.Controls == 0
            ? length
            : throw new ArgumentException(
                "A drawing Rune must be printable and exactly one cell wide.",
                nameof(value));
    }

    private void DrawLineCell(
        Point point,
        Connections connections,
        LineStyle line,
        Style style)
    {
        if (!_frame.Bounds.Contains(point) || !_clip.Contains(point))
        {
            return;
        }

        var topology = new Topology(connections, line);
        var bytes = _frame.GetGrapheme(_frame.GetIndex(point));

        if (Rune.DecodeFromUtf8(bytes, out var existing, out var consumed) == OperationStatus.Done &&
            consumed == bytes.Length &&
            LineResolver.TryDecode(existing, out var previous))
        {
            topology = LineResolver.Merge(previous, topology);
        }

        DrawRune(LineResolver.Resolve(topology), point, style);
    }
}
