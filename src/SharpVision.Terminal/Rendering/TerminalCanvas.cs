// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Rendering;

using Graphics;

/// <summary>
/// Draws semantic grapheme clusters into a clipped frame cell region.
/// </summary>
[PublicAPI]
public readonly struct TerminalCanvas
{
    private const int _tabWidth = 4;
    private readonly Frame _frame;
    private readonly Rect _clip;

    /// <summary>Initializes a frame-owned clipped canvas.</summary>
    /// <param name="frame">The owning frame.</param>
    /// <param name="clip">The validated frame intersection.</param>
    internal TerminalCanvas(Frame frame, Rect clip)
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

    /// <summary>Gets whether a previous frame is available for on-demand cell copying.</summary>
    public bool HasPreviousFrame => _frame.PreviousFrame is not null;

    /// <summary>Copies cells from the previous frame within the given region.</summary>
    /// <param name="region">The region to copy, intersected with this canvas's clip.</param>
    /// <remarks>
    /// <para>
    /// Used to restore cells for render-clean subtrees. Does nothing when no previous
    /// frame is set or the region is empty after clipping.
    /// </para>
    /// <para>
    /// Only complete grapheme owners are copied. A wide cluster straddling the clipped region is
    /// written blank rather than copied, so the frame never gains an orphan lead or an orphan
    /// continuation, and no cell outside the region is touched.
    /// </para>
    /// <para>
    /// Geometry compatibility and the destination arena bound are checked before the first write,
    /// so a rejected copy leaves this canvas unchanged.
    /// </para>
    /// </remarks>
    /// <exception cref="ObjectDisposedException">The owning frame is disposed.</exception>
    /// <exception cref="InvalidOperationException">
    /// The previous frame geometry or width policy differs, or the copy would exceed the finite
    /// text arena of this frame.
    /// </exception>
    public void CopyFromPrevious(Rect region)
    {
        _frame.ThrowIfDisposed();

        if (_frame.PreviousFrame is { } previous)
        {
            _frame.CopyRegionFrom(previous, _clip.Intersect(region));
        }
    }

    /// <summary>Creates a child canvas clipped to the requested rectangle.</summary>
    /// <param name="clip">The requested rectangle in frame coordinates.</param>
    /// <returns>A canvas using the geometric intersection.</returns>
    /// <exception cref="ObjectDisposedException">The owning frame is disposed.</exception>
    public TerminalCanvas Clip(Rect clip)
    {
        _frame.ThrowIfDisposed();
        return new TerminalCanvas(_frame, _clip.Intersect(clip).Intersect(_frame.Bounds));
    }

    #region Drawing primitives

    /// <summary>Records one clipped semantic image placement without emitting terminal bytes.</summary>
    /// <param name="image">The immutable image retained by the frame snapshot.</param>
    /// <param name="destination">The requested half-open destination in terminal cells.</param>
    /// <param name="mode">The backend-neutral fitting mode.</param>
    /// <remarks>
    /// The complete image is the pixel source. Protocol selection, upload, placement, and fallback
    /// remain renderer responsibilities; this method only appends stable semantic paint order.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="image"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="mode"/> is unknown.</exception>
    /// <exception cref="InvalidOperationException">The finite frame placement limit is exhausted.</exception>
    /// <exception cref="ObjectDisposedException">The owning frame is disposed.</exception>
    public void DrawImage(ImageSource image, Rect destination, PlacementMode mode)
    {
        ArgumentNullException.ThrowIfNull(image);

        ArgumentOutOfRangeException.ThrowIfNotDefined(mode, nameof(mode), "The placement mode is unknown.");

        _frame.ThrowIfDisposed();
        var visible = _clip.Intersect(destination).Intersect(_frame.Bounds);

        if (visible.Width == 0 || visible.Height == 0)
        {
            return;
        }

        _frame.AddPlacement(new Placement(
            image,
            new Rect(0, 0, image.Size.Width, image.Size.Height),
            visible,
            mode,
            _frame.CurrentMutationRevision));
    }

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

        if (bounds.Width == 0 || bounds.Height == 0 || IsDisjointFromClip(bounds))
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

        // These intermediates are cubic in the axis length. A roughly square rect overflows Int64
        // at about 1.3 million cells per axis, and a wrapped error term can stop advancing the
        // horizontal bounds entirely, turning the loop below into an infinite one. Int128 keeps
        // every product exact across the whole valid Rect range.
        var horizontalError = (Int128) 4 * (1 - width) * height * height;
        var verticalError = (Int128) 4 * (oddHeight + 1) * width * width;
        var error = horizontalError + verticalError + ((Int128) oddHeight * width * width);

        top += (height + 1) / 2;
        bottom = top - oddHeight;
        var horizontalStep = (Int128) 8 * width * width;
        var verticalStep = (Int128) 8 * height * height;

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
                error += verticalError += horizontalStep;
            }

            if (doubled >= horizontalError || 2 * error > verticalError)
            {
                left++;
                right--;
                error += horizontalError += verticalStep;
            }
        } while (left <= right);

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

        if (IsOutlineDisjointFromClip(center, radius))
        {
            return;
        }

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
                var point = new Point(x, y);
                var applied = style.Background == Color.Transparent ? CompositeBackground(point, style) : style;
                _frame.Write(point, buffer[..length], 1, applied);
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
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="background"/> is unknown.</exception>
    /// <exception cref="ObjectDisposedException">The owning frame is disposed.</exception>
    public void ApplyStyle(
        Rect region,
        CellStyle style,
        BackgroundMode background = BackgroundMode.Opaque)
    {
        _frame.ThrowIfDisposed();

        ArgumentOutOfRangeException.ThrowIfNotDefined(background, nameof(background), "The background mode is unknown.");

        var target = _clip.Intersect(region).Intersect(_frame.Bounds);

        for (var y = target.Y; y < target.Bottom; y++)
        {
            for (var x = target.X; x < target.Right; x++)
            {
                var point = new Point(x, y);
                var applied = background == BackgroundMode.Transparent || style.Background == Color.Transparent
                    ? CompositeBackground(point, style)
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
        Action<TerminalCanvas> draw,
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
                var cell = _frame.GetCellByIndex(index);
                var leadIndex = cell.IsContinuation ? cell.LeadIndex : index;

                if (leadIndex == previousLead)
                {
                    continue;
                }

                previousLead = leadIndex;
                var lead = _frame.GetCellByIndex(leadIndex);

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
                var current = _frame.GetCellByIndex(leadIndex);

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
    /// <remarks>
    /// The visible span is computed before iterating, so a length or origin far outside the clip
    /// costs constant time instead of one iteration per requested cell. Coordinates are resolved in
    /// 64-bit space, so an origin plus length beyond the integer range clips rather than throwing.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is negative.</exception>
    /// <exception cref="ObjectDisposedException">The owning frame is disposed.</exception>
    public void DrawHorizontalLine(Point origin, int length, LineStyle line, CellStyle style = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        _frame.ThrowIfDisposed();

        if (origin.Y < _clip.Y || origin.Y >= _clip.Bottom)
        {
            return;
        }

        var first = Math.Max((long) origin.X, _clip.X);
        var limit = Math.Min((long) origin.X + length, _clip.Right);

        for (var x = first; x < limit; x++)
        {
            DrawLineCellCore(
                new Point((int) x, origin.Y),
                LineConnections.Left | LineConnections.Right,
                line,
                style,
                mergeExisting: true);
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

        if (origin.X < _clip.X || origin.X >= _clip.Right)
        {
            return;
        }

        var first = Math.Max((long) origin.Y, _clip.Y);
        var limit = Math.Min((long) origin.Y + length, _clip.Bottom);

        for (var y = first; y < limit; y++)
        {
            DrawLineCellCore(
                new Point(origin.X, (int) y),
                LineConnections.Up | LineConnections.Down,
                line,
                style,
                mergeExisting: true);
        }
    }

    /// <summary>Draws one exact semantic line-topology cell without merging existing connections.</summary>
    /// <param name="point">The absolute frame cell.</param>
    /// <param name="connections">The non-empty set of center-to-edge connections.</param>
    /// <param name="line">The validated line family.</param>
    /// <param name="style">The semantic cell style.</param>
    /// <remarks>
    /// Horizontal, vertical, and box primitives merge their topology at intersections. This exact-cell
    /// primitive instead replaces the destination topology, allowing callers to terminate a line at a
    /// tee or another authored junction.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="connections"/> is empty or contains an undefined flag.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The owning frame is disposed.</exception>
    public void DrawLineCell(
        Point point,
        LineConnections connections,
        LineStyle line,
        CellStyle style = default)
    {
        const LineConnections all = LineConnections.Up |
            LineConnections.Right |
            LineConnections.Down |
            LineConnections.Left;

        if (connections == LineConnections.None || (connections & ~all) != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(connections),
                connections,
                "At least one defined line connection is required.");
        }

        _frame.ThrowIfDisposed();
        DrawLineCellCore(point, connections, line, style, mergeExisting: false);
    }

    /// <summary>Draws a clipped one-cell box and resolves exact corners.</summary>
    /// <param name="bounds">The half-open outer box bounds.</param>
    /// <param name="line">The validated line family.</param>
    /// <param name="style">The semantic cell style.</param>
    /// <exception cref="ObjectDisposedException">The owning frame is disposed.</exception>
    public void DrawBox(Rect bounds, LineStyle line, CellStyle style = default)
    {
        _frame.ThrowIfDisposed();

        if (bounds.Width == 0 || bounds.Height == 0 || IsDisjointFromClip(bounds))
        {
            return;
        }

        // Past the disjointness reject the box overlaps the clip, so its origin is below the clip
        // edge and the inset arithmetic below cannot wrap past int.MaxValue.

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
        DrawLineCellCore(
            new Point(bounds.X, bounds.Y),
            LineConnections.Right | LineConnections.Down,
            line,
            style,
            mergeExisting: true);
        DrawLineCellCore(
            new Point(bounds.Right - 1, bounds.Y),
            LineConnections.Down | LineConnections.Left,
            line,
            style,
            mergeExisting: true);
        DrawLineCellCore(
            new Point(bounds.X, bounds.Bottom - 1),
            LineConnections.Up | LineConnections.Right,
            line,
            style,
            mergeExisting: true);
        DrawLineCellCore(
            new Point(bounds.Right - 1, bounds.Bottom - 1),
            LineConnections.Up | LineConnections.Left,
            line,
            style,
            mergeExisting: true);
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
        ArgumentOutOfRangeException.ThrowIfNotDefined(shade, nameof(shade), "The shade is unknown.");

        Fill(region, shade.Resolve(_frame.AmbiguousWidth), style);
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
        // Deliberately NOT ArgumentOutOfRangeException.ThrowIfUndefinedFlags here: DrawQuadrants
        // is called per drawn cell, and the generic helper's Convert.ToUInt64 boxes both operands
        // on every call. The direct bitwise check below costs nothing since Quadrants' operators
        // never box.
        if ((quadrants & ~Quadrants.All) != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quadrants), quadrants, "The quadrant mask contains unknown flags.");
        }

        _frame.ThrowIfDisposed();

        if (quadrants == Quadrants.None || !_frame.Bounds.Contains(point) || !_clip.Contains(point))
        {
            return;
        }

        var bytes = _frame.GetGrapheme(_frame.GetIndex(point));

        if (Rune.DecodeFromUtf8(bytes, out var existing, out var consumed) == OperationStatus.Done &&
            consumed == bytes.Length &&
            existing.TryDecode(out Quadrants previous))
        {
            quadrants |= previous;
        }

        DrawRune(quadrants.Resolve(_frame.AmbiguousWidth), point, style);
    }

    /// <summary>Draws a fractional block bar in eighth-cell resolution.</summary>
    /// <param name="origin">The absolute baseline cell the bar grows from.</param>
    /// <param name="direction">The growth direction.</param>
    /// <param name="eighths">The non-negative bar extent in eighths of a cell.</param>
    /// <param name="style">The semantic cell style.</param>
    /// <remarks>
    /// Complete cells use the solid block; the final partial cell uses the matching eighth-block
    /// glyph. Bars growing <see cref="BarDirection.Up"/> or <see cref="BarDirection.Right"/>
    /// resolve the partial cell exactly, because Block Elements defines every lower and left
    /// eighth; bars growing down or left snap it to the nearest half cell, the finest glyph that
    /// direction has. Under a wide Ambiguous policy every cell degrades to the portable solid
    /// fallback and the extent rounds to whole cells.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="direction"/> is unknown, or
    /// <paramref name="eighths"/> is negative.</exception>
    /// <exception cref="InvalidOperationException">The finite frame arena would be exceeded.</exception>
    /// <exception cref="ObjectDisposedException">The owning frame is disposed.</exception>
    public void DrawBar(Point origin, BarDirection direction, int eighths, CellStyle style = default)
    {
        ArgumentOutOfRangeException.ThrowIfNotDefined(direction, nameof(direction), "The direction is unknown.");

        ArgumentOutOfRangeException.ThrowIfNegative(eighths);
        _frame.ThrowIfDisposed();

        var solid = Shade.Solid.Resolve(_frame.AmbiguousWidth);
        var cap = direction.ResolveCap(eighths % 8, _frame.AmbiguousWidth);
        var cells = eighths / 8;
        var (stepX, stepY) = direction switch
        {
            BarDirection.Up => (0, -1),
            BarDirection.Down => (0, 1),
            BarDirection.Left => (-1, 0),
            BarDirection.Right or _ => (1, 0)
        };

        for (var index = 0; index < cells; index++)
        {
            DrawRune(solid, new Point(origin.X + (stepX * index), origin.Y + (stepY * index)), style);
        }

        if (cap.Value != ' ')
        {
            DrawRune(cap, new Point(origin.X + (stepX * cells), origin.Y + (stepY * cells)), style);
        }
    }

    /// <summary>Draws and merges a solid line between two inclusive half-cell coordinates.</summary>
    /// <param name="start">The first point, in half-cell coordinates.</param>
    /// <param name="end">The final point, in half-cell coordinates.</param>
    /// <param name="style">The semantic cell style.</param>
    /// <remarks>
    /// Half-cell coordinates double both axes: cell (0, 0) covers half-cell columns 0-1 and rows
    /// 0-1. Traversal uses integer Bresenham geometry over that doubled grid, and every plotted
    /// point merges one quadrant into its cell exactly like <see cref="DrawQuadrants"/>, so
    /// crossing and touching lines accumulate into the connected quadrant glyphs instead of
    /// overwriting one another.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The finite frame arena would be exceeded.</exception>
    /// <exception cref="ObjectDisposedException">The owning frame is disposed.</exception>
    public void DrawQuadrantLine(Point start, Point end, CellStyle style = default) =>
        DrawQuadrantLine(start, end, LinePattern.Solid, patternStep: 0, style);

    /// <summary>Draws and merges a patterned line between two inclusive half-cell coordinates.</summary>
    /// <param name="start">The first point, in half-cell coordinates.</param>
    /// <param name="end">The final point, in half-cell coordinates.</param>
    /// <param name="pattern">The on/off dash pattern.</param>
    /// <param name="patternStep">The zero-based pattern step the segment continues from, letting
    /// a polyline's dash phase stay continuous across chained calls.</param>
    /// <param name="style">The semantic cell style.</param>
    /// <returns>The pattern step used to draw the segment's final point. When continuing a
    /// polyline from this segment's endpoint, pass the returned value as the next segment's
    /// <paramref name="patternStep"/>; the shared vertex is re-evaluated at the same step - an
    /// idempotent redraw, since an on step redraws the same quadrant and an off step stays
    /// skipped - and the following point then advances the phase by exactly one step, exactly as
    /// if the whole polyline had been drawn by one continuous call.</returns>
    /// <remarks>
    /// Half-cell coordinates double both axes: cell (0, 0) covers half-cell columns 0-1 and rows
    /// 0-1. Traversal uses integer Bresenham geometry over that doubled grid, and every plotted
    /// point merges one quadrant into its cell exactly like <see cref="DrawQuadrants"/>, so
    /// crossing and touching lines accumulate into the connected quadrant glyphs instead of
    /// overwriting one another. A monotonic step counter advances once per Bresenham iteration,
    /// independent of slope; <paramref name="pattern"/> resolves each step to an on or off run,
    /// and an off step skips the write entirely rather than drawing an empty quadrant, so a
    /// dashed line still merges correctly with a crossing solid line and still degrades to the
    /// same portable fallback under a wide Ambiguous policy.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="pattern"/> is unknown, or
    /// <paramref name="patternStep"/> is negative.</exception>
    /// <exception cref="InvalidOperationException">The finite frame arena would be exceeded.</exception>
    /// <exception cref="ObjectDisposedException">The owning frame is disposed.</exception>
    public int DrawQuadrantLine(Point start, Point end, LinePattern pattern, int patternStep, CellStyle style = default)
    {
        ArgumentOutOfRangeException.ThrowIfNotDefined(pattern, nameof(pattern), "The line pattern is unknown.");

        ArgumentOutOfRangeException.ThrowIfNegative(patternStep);
        _frame.ThrowIfDisposed();

        var deltaX = Math.Abs(end.X - start.X);
        var deltaY = -Math.Abs(end.Y - start.Y);
        var signX = start.X < end.X ? 1 : -1;
        var signY = start.Y < end.Y ? 1 : -1;
        var error = deltaX + deltaY;
        var (x, y) = (start.X, start.Y);
        var step = patternStep;

        while (true)
        {
            if (pattern.IsStepOn(step))
            {
                // Arithmetic shift and masking floor negative halves consistently, so an
                // off-frame segment clips per cell instead of folding onto cell zero.
                DrawQuadrants(
                    new Point(x >> 1, y >> 1),
                    (y & 1) == 0
                        ? (x & 1) == 0 ? Quadrants.UpperLeft : Quadrants.UpperRight
                        : (x & 1) == 0 ? Quadrants.LowerLeft : Quadrants.LowerRight,
                    style);
            }

            if (x == end.X && y == end.Y)
            {
                return step;
            }

            step++;

            var doubled = 2 * error;

            if (doubled >= deltaY)
            {
                error += deltaY;
                x += signX;
            }

            if (doubled <= deltaX)
            {
                error += deltaX;
                y += signY;
            }
        }
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

        ArgumentOutOfRangeException.ThrowIfNotDefined(edge, nameof(edge), "The edge policy is unknown.");

        ArgumentOutOfRangeException.ThrowIfNotDefined(background, nameof(background), "The background mode is unknown.");

        var preflight = Process(value, origin, style, edge, background, write: false, out var bytes);
        _frame.EnsureAppendable(bytes);
        var result = Process(value, origin, style, edge, background, write: true, out var written);
        Debug.Assert(preflight == result, "TerminalCanvas preflight and mutation passes must agree.");
        Debug.Assert(bytes == written, "TerminalCanvas UTF-8 preflight and mutation must agree.");

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
                var point = new Point(x, y);
                var applied = style.Background == Color.Transparent ? CompositeBackground(point, style) : style;
                _frame.SetBlank(_frame.GetIndex(point), applied);
            }
        }
    }

    /// <summary>Sets the owning frame cursor through this borrowed canvas.</summary>
    /// <param name="position">The in-frame cursor cell.</param>
    /// <param name="visible">Whether the terminal cursor is visible.</param>
    /// <exception cref="ArgumentOutOfRangeException">The position is outside the frame.</exception>
    /// <exception cref="ObjectDisposedException">The owning frame is disposed.</exception>
    public void SetCursor(Point position, bool visible) => _frame.SetCursor(position, visible);

    /// <summary>Sets the owning frame cursor through this borrowed canvas.</summary>
    /// <param name="position">The in-frame cursor cell.</param>
    /// <param name="visible">Whether the terminal cursor is visible.</param>
    /// <param name="shape">The semantic cursor shape.</param>
    /// <exception cref="ArgumentOutOfRangeException">The position is outside the frame or shape is unknown.</exception>
    /// <exception cref="ObjectDisposedException">The owning frame is disposed.</exception>
    public void SetCursor(Point position, bool visible, CursorShape shape) =>
        _frame.SetCursor(position, visible, shape);

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
                        cells = checked(cells + cellWidth);
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

            // Counted after any edge-policy demotion so this always matches the cell width
            // actually applied to `x`/`Final`, not the cluster's pre-demotion display width.
            cells = checked(cells + cellWidth);

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
                var applied = background == BackgroundMode.Transparent || style.Background == Color.Transparent
                    ? CompositeBackground(point, style)
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
        if (value.Contains('\n'))
        {
            // Grapheme segmentation (GB3) only ever joins "\r\n" into one cluster; a lone "\r"
            // reaches this method by itself and must not advance the row (canonical carriage
            // return returns to the line origin on the same row).
            x = lineOrigin;
            y = checked(y + 1);
        }
        else if (value.Contains('\r'))
        {
            x = lineOrigin;
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

        return measurement is { Cells: 1, Controls: 0 }
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

    private bool IsDisjointFromClip(Rect bounds)
    {
        // Rect.Right and Rect.Bottom saturate, so this comparison is safe for any valid rect.
        return bounds.Right <= _clip.X ||
               bounds.X >= _clip.Right ||
               bounds.Bottom <= _clip.Y ||
               bounds.Y >= _clip.Bottom;
    }

    private bool IsOutlineDisjointFromClip(Point center, int radius)
    {
        // An outline is visible only where the clip meets the ring itself. Testing the bounding
        // box alone would still walk a radius that swallows the whole clip, so compare the radius
        // against the nearest and farthest clip distances. Squared distances need more than 64
        // bits once coordinates approach the integer extremes.
        if (_clip.Width == 0 || _clip.Height == 0)
        {
            return true;
        }

        var left = (long) _clip.X;
        var top = (long) _clip.Y;
        var right = (long) _clip.Right - 1;
        var bottom = (long) _clip.Bottom - 1;
        var x = (long) center.X;
        var y = (long) center.Y;
        var nearX = Math.Max(0L, Math.Max(left - x, x - right));
        var nearY = Math.Max(0L, Math.Max(top - y, y - bottom));
        var farX = Math.Max(Math.Abs(x - left), Math.Abs(x - right));
        var farY = Math.Max(Math.Abs(y - top), Math.Abs(y - bottom));

        // One cell of slack on each side keeps the reject conservative against the rasterizer's
        // own rounding, so a genuinely grazing arc is never discarded.
        var near = ((Int128) nearX * nearX) + ((Int128) nearY * nearY);
        var far = ((Int128) farX * farX) + ((Int128) farY * farY);
        var inner = (Int128) Math.Max(0L, radius - 2L);
        var outer = (Int128) radius + 2;

        return (outer * outer) < near || (inner * inner) > far;
    }

    private void DrawGeometryPoint(long x, long y, ReadOnlySpan<char> value, CellStyle style)
    {
        if (x < _clip.X || x >= _clip.Right || y < _clip.Y || y >= _clip.Bottom)
        {
            return;
        }

        _ = Draw(value, new Point((int) x, (int) y), style);
    }

    private CellStyle CompositeBackground(Point point, CellStyle style) => new(
        style.Foreground,
        _frame.GetCell(point).Style.Background,
        style.Attributes,
        style.Hyperlink,
        style.Underline,
        style.UnderlineColor);

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

        // Constant-time reject. A segment whose bounding box misses the clip cannot contribute a
        // single cell, so it must not cost one iteration per integer step between its endpoints.
        if (Math.Max(x, endX) < _clip.X ||
            Math.Min(x, endX) >= _clip.Right ||
            Math.Max(y, endY) < _clip.Y ||
            Math.Min(y, endY) >= _clip.Bottom)
        {
            return;
        }

        var dx = Math.Abs(endX - x);
        var stepX = x < endX ? 1L : -1L;
        var dy = -Math.Abs(endY - y);
        var stepY = y < endY ? 1L : -1L;
        var error = dx + dy;
        var steps = Math.Max(dx, -dy);
        var skip = FirstVisibleStep(x, y, stepX, stepY, dx, -dy, steps);

        if (skip > 0)
        {
            // Fast-forward the recurrence instead of iterating an invisible prefix. The major axis
            // advances once per iteration and the minor axis advances a closed-form number of
            // times, so the resumed state is bit-identical to the state the loop would have
            // reached, preserving Bresenham's exact error and tie behavior.
            var (advancedX, advancedY) = AdvanceCounts(dx, -dy, skip);
            x += advancedX * stepX;
            y += advancedY * stepY;
            error = dx + dy - (advancedX * -dy) + (advancedY * dx);
        }

        for (var step = skip; ; step++)
        {
            DrawGeometryPoint(x, y, value, style);

            if (x == endX && y == endY)
            {
                return;
            }

            // Every later point is on the far side of the clip along the major axis, so no
            // remaining iteration can draw anything.
            if (step >= steps || IsPastClip(x, y, stepX, stepY, dx, -dy))
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

    private static (long X, long Y) AdvanceCounts(long dx, long dy, long step)
    {
        // The major axis advances on every iteration. The minor axis advance count after `step`
        // iterations is the exact integer the recurrence produces.
        Debug.Assert(step >= 0, "A fast-forward never runs backwards.");

        return dx >= dy
            ? (step, dx == 0 ? 0 : ((2 * dy * step) + dx) / (2 * dx))
            : (dy == 0 ? 0 : ((2 * dx * step) + dy) / (2 * dy), step);
    }

    private long FirstVisibleStep(
        long x,
        long y,
        long stepX,
        long stepY,
        long dx,
        long dy,
        long steps)
    {
        // Only the major axis is a strict function of the iteration index, so it alone can bound
        // the invisible prefix exactly. The minor axis is handled by ordinary per-point clipping.
        var skip = dx >= dy
            ? StepsUntilInRange(x, stepX, _clip.X, _clip.Right)
            : StepsUntilInRange(y, stepY, _clip.Y, _clip.Bottom);

        return Math.Clamp(skip, 0, steps);
    }

    private static long StepsUntilInRange(long value, long step, long minimum, long limit) =>
        step > 0
            ? value < minimum ? minimum - value : 0
            : value >= limit ? value - limit + 1 : 0;

    private bool IsPastClip(long x, long y, long stepX, long stepY, long dx, long dy) =>
        dx >= dy
            ? stepX > 0 ? x >= _clip.Right : x < _clip.X
            : stepY > 0 ? y >= _clip.Bottom : y < _clip.Y;

    private void DrawLineCellCore(
        Point point,
        LineConnections connections,
        LineStyle line,
        CellStyle style,
        bool mergeExisting)
    {
        if (!_frame.Bounds.Contains(point) || !_clip.Contains(point))
        {
            return;
        }

        var topology = new Topology(connections, line);
        var bytes = _frame.GetGrapheme(_frame.GetIndex(point));

        if (mergeExisting &&
            Rune.DecodeFromUtf8(bytes, out var existing, out var consumed) == OperationStatus.Done &&
            consumed == bytes.Length &&
            existing.TryDecode(out Topology previous))
        {
            topology = previous.Merge(topology);
        }

        DrawRune(
            topology.Resolve(_frame.AmbiguousWidth),
            point,
            style,
            BackgroundMode.Transparent);
    }
}
