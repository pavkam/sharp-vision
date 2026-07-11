using System.Diagnostics;

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
            var width = Width.GetCluster(
                cluster,
                _frame.AmbiguousWidth,
                segment.HasInvalidData);
            graphemes = checked(graphemes + 1);

            if (width == CellWidth.Control)
            {
                AdvanceControl(cluster, origin.X, ref x, ref y);
                continue;
            }

            var cellWidth = (int) width;
            cells = checked(cells + cellWidth);
            var replacement = false;

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
                        replacement = true;
                        replaced = checked(replaced + 1);
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
}
