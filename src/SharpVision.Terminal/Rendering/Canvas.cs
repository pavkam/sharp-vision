// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
    /// <param name="background">Whether the supplied background replaces or preserves the destination cell.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> is a control or does not occupy exactly one cell.
    /// </exception>
    /// <exception cref="InvalidOperationException">The finite frame arena would be exceeded.</exception>
    /// <exception cref="ObjectDisposedException">The owning frame is disposed.</exception>
    public void DrawRune(
        Rune value,
        Point origin,
        CellStyle style = default,
        BackgroundMode background = BackgroundMode.Opaque)
    {
        Span<char> buffer = stackalloc char[2];
        var length = ValidateRune(value, buffer);
        _ = Draw(buffer[..length], origin, style, background: background);
    }

    /// <summary>Draws a clipped deterministic line between two inclusive cell coordinates.</summary>
    /// <param name="start">The first absolute frame cell.</param>
    /// <param name="end">The final absolute frame cell.</param>
    /// <param name="value">The printable one-cell Rune used for every rasterized point.</param>
    /// <param name="style">The semantic cell style.</param>
    /// <remarks>Both endpoints are included. Traversal uses integer Bresenham geometry in cell coordinates.</remarks>
    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> is a control or does not occupy exactly one cell.
    /// </exception>
    /// <exception cref="InvalidOperationException">The finite frame arena would be exceeded.</exception>
    /// <exception cref="ObjectDisposedException">The owning frame is disposed.</exception>
    public void DrawLine(Point start, Point end, Rune value, CellStyle style = default)
    {
        Span<char> buffer = stackalloc char[2];
        var length = ValidateRune(value, buffer);
        DrawLineValidated(start, end, buffer[..length], style);
    }

    /// <summary>Draws a clipped deterministic ellipse outline inside half-open cell bounds.</summary>
    /// <param name="bounds">The half-open ellipse bounds in absolute frame cells.</param>
    /// <param name="value">The printable one-cell Rune used for every rasterized point.</param>
    /// <param name="style">The semantic cell style.</param>
    /// <remarks>Empty bounds draw nothing. A one-cell axis degrades to the corresponding line or point.</remarks>
    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> is a control or does not occupy exactly one cell.
    /// </exception>
    /// <exception cref="InvalidOperationException">The finite frame arena would be exceeded.</exception>
    /// <exception cref="ObjectDisposedException">The owning frame is disposed.</exception>
    public void DrawEllipse(Rect bounds, Rune value, CellStyle style = default)
    {
        Span<char> buffer = stackalloc char[2];
        var length = ValidateRune(value, buffer);

        if (bounds.Width == 0 || bounds.Height == 0)
        {
            return;
        }

        if (bounds.Width == 1)
        {
            DrawLineValidated(
                new Point(bounds.X, bounds.Y),
                new Point(bounds.X, bounds.Bottom - 1),
                buffer[..length],
                style);
            return;
        }

        if (bounds.Height == 1)
        {
            DrawLineValidated(
                new Point(bounds.X, bounds.Y),
                new Point(bounds.Right - 1, bounds.Y),
                buffer[..length],
                style);
            return;
        }

        var left = (long) bounds.X;
        var right = left + bounds.Width - 1;
        var top = (long) bounds.Y;
        var bottom = top + bounds.Height - 1;
        var width = right - left;
        var height = bottom - top;
        var oddHeight = height & 1;
        var horizontalError = 4 * (1 - width) * height * height;
        var verticalError = 4 * (oddHeight + 1) * width * width;
        var error = horizontalError + verticalError + (oddHeight * width * width);

        top += (height + 1) / 2;
        bottom = top - oddHeight;
        width *= 8 * width;
        oddHeight = 8 * height * height;

        do
        {
            DrawGeometryPoint(right, top, buffer[..length], style);
            DrawGeometryPoint(left, top, buffer[..length], style);
            DrawGeometryPoint(left, bottom, buffer[..length], style);
            DrawGeometryPoint(right, bottom, buffer[..length], style);
            var doubled = 2 * error;

            if (doubled <= verticalError)
            {
                top++;
                bottom--;
                error += verticalError += width;
            }

            if (doubled >= horizontalError || 2 * error > verticalError)
            {
                left++;
                right--;
                error += horizontalError += oddHeight;
            }
        }
        while (left <= right);

        while (top - bottom < height)
        {
            DrawGeometryPoint(left - 1, top, buffer[..length], style);
            DrawGeometryPoint(right + 1, top, buffer[..length], style);
            top++;
            DrawGeometryPoint(left - 1, bottom, buffer[..length], style);
            DrawGeometryPoint(right + 1, bottom, buffer[..length], style);
            bottom--;
        }
    }

    /// <summary>Draws a clipped deterministic circle outline in cell coordinates.</summary>
    /// <param name="center">The center absolute frame cell.</param>
    /// <param name="radius">The non-negative radius measured in terminal cells.</param>
    /// <param name="value">The printable one-cell Rune used for every rasterized point.</param>
    /// <param name="style">The semantic cell style.</param>
    /// <remarks>Radius zero draws exactly the center cell.</remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="radius"/> is negative.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> is a control or does not occupy exactly one cell.
    /// </exception>
    /// <exception cref="InvalidOperationException">The finite frame arena would be exceeded.</exception>
    /// <exception cref="ObjectDisposedException">The owning frame is disposed.</exception>
    public void DrawCircle(Point center, int radius, Rune value, CellStyle style = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(radius);
        Span<char> buffer = stackalloc char[2];
        var length = ValidateRune(value, buffer);
        var x = (long) radius;
        var y = 0L;
        var error = 1L - radius;

        while (x >= y)
        {
            DrawCircleOctants(center, x, y, buffer[..length], style);
            y++;

            if (error < 0)
            {
                error += (2 * y) + 1;
            }
            else
            {
                x--;
                error += (2 * (y - x)) + 1;
            }
        }
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
    public void Fill(Rect region, Rune value, CellStyle style = default)
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
    /// <param name="background">Whether the supplied background replaces or preserves destination cells.</param>
    /// <remarks>
    /// Touching any cell of a wide owner styles the complete owner when all of
    /// its cells are inside this canvas clip. A partially clipped owner is
    /// skipped so lead and continuation styles cannot disagree.
    /// </remarks>
    /// <exception cref="ObjectDisposedException">The owning frame is disposed.</exception>
    public void ApplyStyle(
        Rect region,
        CellStyle style,
        BackgroundMode background = BackgroundMode.Opaque)
    {
        _frame.ThrowIfDisposed();

        if (!Enum.IsDefined(background))
        {
            throw new ArgumentOutOfRangeException(nameof(background), background, "The background mode is unknown.");
        }

        var target = _clip.Intersect(region).Intersect(_frame.Bounds);

        for (var y = target.Y; y < target.Bottom; y++)
        {
            for (var x = target.X; x < target.Right; x++)
            {
                var point = new Point(x, y);
                var applied = background == BackgroundMode.Transparent
                    ? new CellStyle(
                        style.Foreground,
                        _frame.GetCell(point).Style.Background,
                        style.Attributes,
                        style.Hyperlink,
                        style.Underline,
                        style.UnderlineColor)
                    : style;
                _ = _frame.TrySetOwnerStyle(_frame.GetIndex(point), _clip, applied);
            }
        }
    }

    /// <summary>Transforms foreground colors while preserving complete stored graphemes.</summary>
    /// <param name="region">The requested half-open frame region.</param>
    /// <param name="selector">
    /// The synchronous selector receiving each complete stored owner's absolute lead-cell coordinate.
    /// The canvas does not retain the callback.
    /// </param>
    /// <remarks>
    /// Stored spaces participate, while untouched blank cells do not. The selector is invoked once
    /// per complete owner in row-major order. A wide owner is transformed only when all of its cells
    /// are inside this canvas clip, so lead and continuation styles remain equal. Foreground is the
    /// only replaced field; background, attributes, hyperlink, underline, and underline color are
    /// preserved. If the callback throws, its exception propagates and the current render operation
    /// fails; owners transformed by earlier callbacks remain changed while the failing and later
    /// owners remain unchanged.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="selector"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">The owning frame is disposed.</exception>
    /// <exception cref="Exception">
    /// <paramref name="selector"/> throws; the same exception instance is propagated.
    /// </exception>
    public void ApplyForeground(Rect region, Func<Point, Color> selector)
    {
        ArgumentNullException.ThrowIfNull(selector);
        _frame.ThrowIfDisposed();
        ApplyForegroundCore(region, selector, writtenOnly: false, checkpoint: 0, drawEnd: 0);
    }

    /// <summary>Draws synchronously and transforms only stored owners mutated by that callback.</summary>
    /// <param name="region">The requested half-open foreground-effect region.</param>
    /// <param name="draw">
    /// The synchronous drawing callback. It receives this clipped canvas and is not retained.
    /// </param>
    /// <param name="selector">
    /// The synchronous foreground selector receiving each written complete owner's absolute lead coordinate.
    /// It is invoked row-major after drawing completes and is not retained.
    /// </param>
    /// <remarks>
    /// Both callbacks are validated before frame lifetime and before drawing begins.
    /// Mutation provenance, rather than semantic inequality, selects owners: writing an identical glyph
    /// still participates. The closed provenance window ends when drawing returns, so mutations performed
    /// by the foreground selector are never selected by this effect. Nested effects retain inner writes as
    /// mutations visible to an enclosing effect. Stored spaces participate, untouched blanks and
    /// pre-existing owners do not, and wide owners remain atomic. A drawing exception skips the foreground
    /// pass. A selector exception preserves the already transformed prefix and leaves the failing and later
    /// owners unchanged. Foreground is the only transformed semantic field.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="draw"/> or <paramref name="selector"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The owning frame is disposed.</exception>
    /// <exception cref="InvalidOperationException">
    /// Nested mutation capture or one bounded synchronous callback exhausts its revision capacity.
    /// </exception>
    /// <exception cref="Exception">
    /// <paramref name="draw"/> or <paramref name="selector"/> throws; the same exception instance propagates.
    /// </exception>
    public void DrawWithForeground(
        Rect region,
        Action<Canvas> draw,
        Func<Point, Color> selector)
    {
        ArgumentNullException.ThrowIfNull(draw);
        ArgumentNullException.ThrowIfNull(selector);
        _frame.ThrowIfDisposed();
        var checkpoint = _frame.BeginMutationCapture();

        try
        {
            draw(this);
            var drawEnd = _frame.CurrentMutationRevision;
            ApplyForegroundCore(region, selector, writtenOnly: true, checkpoint, drawEnd);
        }
        finally
        {
            _frame.EndMutationCapture();
        }
    }

    private void ApplyForegroundCore(
        Rect region,
        Func<Point, Color> selector,
        bool writtenOnly,
        ulong checkpoint,
        ulong drawEnd)
    {
        var target = _clip.Intersect(region).Intersect(_frame.Bounds);

        for (var y = target.Y; y < target.Bottom; y++)
        {
            var previousLead = -1;

            for (var x = target.X; x < target.Right; x++)
            {
                var index = _frame.GetIndex(new Point(x, y));
                var cell = _frame.GetCell(index);
                var leadIndex = cell.IsContinuation ? cell.LeadIndex : index;

                if (leadIndex == previousLead)
                {
                    continue;
                }

                previousLead = leadIndex;
                var lead = _frame.GetCell(leadIndex);

                if (lead.Length == 0)
                {
                    continue;
                }

                // Active captures forbid revision wrap, so ordinary unsigned ordering is exact.
                if (writtenOnly &&
                    (lead.MutationRevision <= checkpoint || lead.MutationRevision > drawEnd))
                {
                    continue;
                }

                var leadPoint = new Point(leadIndex % _frame.Size.Width, leadIndex / _frame.Size.Width);
                var width = Math.Max(1, (int) lead.Width);
                var complete = true;

                for (var offset = 0; offset < width; offset++)
                {
                    complete &= _clip.Contains(new Point(leadPoint.X + offset, leadPoint.Y));
                }

                if (!complete)
                {
                    continue;
                }

                var style = lead.Style;
                var foreground = selector(leadPoint);
                var current = _frame.GetCell(leadIndex);

                // A selector may mutate the owner it is selecting. That write lies after the
                // closed draw window and must retain the selector's exact semantic result.
                if (writtenOnly && current.MutationRevision > drawEnd)
                {
                    continue;
                }

                var replacement = new CellStyle(
                    foreground,
                    style.Background,
                    style.Attributes,
                    style.Hyperlink,
                    style.Underline,
                    style.UnderlineColor);
                _ = _frame.TrySetOwnerStyle(leadIndex, _clip, replacement);
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
    public void DrawHorizontalLine(Point origin, int length, LineStyle line, CellStyle style = default)
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
    public void DrawVerticalLine(Point origin, int length, LineStyle line, CellStyle style = default)
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
    public void DrawBox(Rect bounds, LineStyle line, CellStyle style = default)
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
    public void FillShade(Rect region, Shade shade, CellStyle style = default)
    {
        if (!Enum.IsDefined(shade))
        {
            throw new ArgumentOutOfRangeException(nameof(shade), shade, "The shade is unknown.");
        }

        Fill(region, BlockResolver.Resolve(shade, _frame.AmbiguousWidth), style);
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
    public void DrawQuadrants(Point point, Quadrants quadrants, CellStyle style = default)
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

        DrawRune(BlockResolver.Resolve(quadrants, _frame.AmbiguousWidth), point, style);
    }

    #endregion

    /// <summary>Draws borrowed UTF-16 text as complete semantic graphemes.</summary>
    /// <param name="value">The borrowed UTF-16 text.</param>
    /// <param name="origin">The logical starting cell coordinate.</param>
    /// <param name="style">The semantic style.</param>
    /// <param name="edge">The wide-cluster right-edge behavior.</param>
    /// <param name="background">Whether the supplied background replaces or preserves destination cells.</param>
    /// <returns>Logical advance and clipping metrics.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="edge"/> is unknown.</exception>
    /// <exception cref="InvalidOperationException">The finite frame arena would be exceeded.</exception>
    /// <exception cref="ObjectDisposedException">The owning frame is disposed.</exception>
    public DrawResult Draw(
        ReadOnlySpan<char> value,
        Point origin,
        CellStyle style = default,
        Edge edge = Edge.Clip,
        BackgroundMode background = BackgroundMode.Opaque)
    {
        _frame.ThrowIfDisposed();

        if (!Enum.IsDefined(edge))
        {
            throw new ArgumentOutOfRangeException(nameof(edge), edge, "The edge policy is unknown.");
        }

        if (!Enum.IsDefined(background))
        {
            throw new ArgumentOutOfRangeException(nameof(background), background, "The background mode is unknown.");
        }

        var preflight = Process(value, origin, style, edge, background, write: false, out var bytes);
        _frame.EnsureAppendable(bytes);
        var result = Process(value, origin, style, edge, background, write: true, out var written);
        Debug.Assert(preflight == result, "Canvas preflight and mutation passes must agree.");
        Debug.Assert(bytes == written, "Canvas UTF-8 preflight and mutation must agree.");

        return result;
    }

    /// <summary>Clears a clipped region while repairing complete wide owners.</summary>
    /// <param name="region">The requested region in frame coordinates.</param>
    /// <param name="style">The semantic blank style inside the region.</param>
    /// <exception cref="ObjectDisposedException">The owning frame is disposed.</exception>
    public void Clear(Rect region, CellStyle style = default)
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
        CellStyle style,
        Edge edge,
        BackgroundMode background,
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
                var applied = background == BackgroundMode.Transparent
                    ? new CellStyle(
                        style.Foreground,
                        _frame.GetCell(point).Style.Background,
                        style.Attributes,
                        style.Hyperlink,
                        style.Underline,
                        style.UnderlineColor)
                    : style;
                _frame.Write(point, stored, cellWidth, applied);
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

    private int ValidateRune(Rune value, Span<char> buffer)
    {
        var length = value.EncodeToUtf16(buffer);
        var measurement = Width.Measure(buffer[..length], _frame.AmbiguousWidth);

        return measurement.Cells == 1 && measurement.Controls == 0
            ? length
            : throw new ArgumentException(
                "A drawing Rune must be printable and exactly one cell wide.",
                nameof(value));
    }

    private void DrawCircleOctants(
        Point center,
        long x,
        long y,
        ReadOnlySpan<char> value,
        CellStyle style)
    {
        DrawGeometryPoint(center.X + x, center.Y + y, value, style);
        DrawGeometryPoint(center.X + y, center.Y + x, value, style);
        DrawGeometryPoint(center.X - y, center.Y + x, value, style);
        DrawGeometryPoint(center.X - x, center.Y + y, value, style);
        DrawGeometryPoint(center.X - x, center.Y - y, value, style);
        DrawGeometryPoint(center.X - y, center.Y - x, value, style);
        DrawGeometryPoint(center.X + y, center.Y - x, value, style);
        DrawGeometryPoint(center.X + x, center.Y - y, value, style);
    }

    private void DrawGeometryPoint(long x, long y, ReadOnlySpan<char> value, CellStyle style)
    {
        if (x < _clip.X || x >= _clip.Right || y < _clip.Y || y >= _clip.Bottom)
        {
            return;
        }

        _ = Draw(value, new Point((int) x, (int) y), style);
    }

    private void DrawLineValidated(
        Point start,
        Point end,
        ReadOnlySpan<char> value,
        CellStyle style)
    {
        var x = (long) start.X;
        var y = (long) start.Y;
        var endX = (long) end.X;
        var endY = (long) end.Y;
        var dx = Math.Abs(endX - x);
        var stepX = x < endX ? 1L : -1L;
        var dy = -Math.Abs(endY - y);
        var stepY = y < endY ? 1L : -1L;
        var error = dx + dy;

        while (true)
        {
            DrawGeometryPoint(x, y, value, style);

            if (x == endX && y == endY)
            {
                return;
            }

            var doubled = error * 2;

            if (doubled >= dy)
            {
                error += dy;
                x += stepX;
            }

            if (doubled <= dx)
            {
                error += dx;
                y += stepY;
            }
        }
    }

    private void DrawLineCell(
        Point point,
        Connections connections,
        LineStyle line,
        CellStyle style)
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

        DrawRune(
            LineResolver.Resolve(topology, _frame.AmbiguousWidth),
            point,
            style,
            BackgroundMode.Transparent);
    }
}
