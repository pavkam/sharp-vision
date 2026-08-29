// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Layout;

using NonNegativeValue = JetBrains.Annotations.NonNegativeValueAttribute;
using ValueRange = JetBrains.Annotations.ValueRangeAttribute;

/// <summary>Owns row/column definitions and attached placement for a track Grid.</summary>
[PublicAPI]
public sealed class Grid: Container
{
    private static readonly AttachedLayoutProperty<Grid, int> _rows = new(
        0,
        InvalidationImpact.Measure,
        ValidateRow);
    private static readonly AttachedLayoutProperty<Grid, int> _columns = new(
        0,
        InvalidationImpact.Measure,
        ValidateColumn);
    private static readonly AttachedLayoutProperty<Grid, int> _rowSpans = new(
        1,
        InvalidationImpact.Measure,
        ValidateRowSpan);
    private static readonly AttachedLayoutProperty<Grid, int> _columnSpans = new(
        1,
        InvalidationImpact.Measure,
        ValidateColumnSpan);

    private Constraint? _lastResolvedContentConstraint;

    /// <summary>Initializes permanent row and column definition collections.</summary>
    public Grid()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch;
        Rows = new TrackCollection(
            count => DefinitionsChanging(count, rows: true),
            DefinitionsChanged);
        Columns = new TrackCollection(
            count => DefinitionsChanging(count, rows: false),
            DefinitionsChanged);
        EnableChromeAuthoring();
    }

    /// <summary>Gets the mutable row definitions; empty means one automatic row.</summary>
    public TrackCollection Rows { get; }

    /// <summary>Gets the mutable column definitions; empty means one automatic column.</summary>
    public TrackCollection Columns { get; }

    /// <summary>Gets or sets non-negative cells between rows.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    /// <exception cref="InvalidOperationException">The attached Grid is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The Grid is disposed.</exception>
    [NonNegativeValue]
    public int RowSpacing
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _ = SetProperty(ref field, value, InvalidationImpact.Measure);
        }
    }

    /// <summary>Gets or sets non-negative cells between columns.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    /// <exception cref="InvalidOperationException">The attached Grid is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The Grid is disposed.</exception>
    [NonNegativeValue]
    public int ColumnSpacing
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _ = SetProperty(ref field, value, InvalidationImpact.Measure);
        }
    }

    /// <summary>Gets a control's zero-based attached row.</summary>
    /// <param name="control">The non-null control.</param>
    /// <returns>The attached row, defaulting to zero.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> is null.</exception>
    [NonNegativeValue]
    public static int GetRow(ControlBase control) => _rows.Get(control);

    /// <summary>Gets a control's zero-based attached column.</summary>
    /// <param name="control">The non-null control.</param>
    /// <returns>The attached column, defaulting to zero.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> is null.</exception>
    [NonNegativeValue]
    public static int GetColumn(ControlBase control) => _columns.Get(control);

    /// <summary>Gets a control's positive attached row span.</summary>
    /// <param name="control">The non-null control.</param>
    /// <returns>The attached row span, defaulting to one.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> is null.</exception>
    [ValueRange(1, int.MaxValue)]
    public static int GetRowSpan(ControlBase control) => _rowSpans.Get(control);

    /// <summary>Gets a control's positive attached column span.</summary>
    /// <param name="control">The non-null control.</param>
    /// <returns>The attached column span, defaulting to one.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> is null.</exception>
    [ValueRange(1, int.MaxValue)]
    public static int GetColumnSpan(ControlBase control) => _columnSpans.Get(control);

    /// <summary>Sets a control's zero-based attached row.</summary>
    /// <param name="control">The non-null mutable control.</param>
    /// <param name="value">The non-negative row.</param>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The row is negative or outside committed tracks.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public static void SetRow(ControlBase control, int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        _rows.Set(control, value);
    }

    /// <summary>Sets a control's zero-based attached column.</summary>
    /// <param name="control">The non-null mutable control.</param>
    /// <param name="value">The non-negative column.</param>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The column is negative or outside committed tracks.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public static void SetColumn(ControlBase control, int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        _columns.Set(control, value);
    }

    /// <summary>Sets a control's positive attached row span.</summary>
    /// <param name="control">The non-null mutable control.</param>
    /// <param name="value">The positive row span.</param>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The span is not positive or exceeds committed tracks.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public static void SetRowSpan(ControlBase control, int value)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, 0);
        _rowSpans.Set(control, value);
    }

    /// <summary>Sets a control's positive attached column span.</summary>
    /// <param name="control">The non-null mutable control.</param>
    /// <param name="value">The positive column span.</param>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The span is not positive or exceeds committed tracks.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public static void SetColumnSpan(ControlBase control, int value)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, 0);
        _columnSpans.Set(control, value);
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        ValidatePlacements();
        var rows = Definitions(Rows);
        var columns = Definitions(Columns);

        MeasureChildren(new Constraint(width: null, height: null));
        var rowRequests = Requests(rows.Length, rows: true, rows, constraint.Height);
        var columnRequests = Requests(columns.Length, rows: false, columns, constraint.Width);
        var rowExtents = Resolve(constraint.Height, RowSpacing, rows, rowRequests);
        var columnExtents = Resolve(constraint.Width, ColumnSpacing, columns, columnRequests);

        // A finite track slot can alter wrapping and therefore the intrinsic
        // request on the other axis. Re-measure once, then resolve the stable
        // requests used for both desired size and final arrangement.
        MeasureChildren(
            rowExtents,
            columnExtents,
            constraint.Height,
            constraint.Width);
        columnRequests = Requests(columns.Length, rows: false, columns, constraint.Width);
        columnExtents = Resolve(constraint.Width, ColumnSpacing, columns, columnRequests);

        // Automatic rows must let wrapped children report their newly expanded
        // height after finite column widths are known. The initial row extent is
        // only a probe and would otherwise cap the remeasure to one old line.
        MeasureAutomaticRows(rows, columnExtents, constraint.Width);
        rowRequests = Requests(rows.Length, rows: true, rows, constraint.Height);
        rowExtents = Resolve(constraint.Height, RowSpacing, rows, rowRequests);

        _lastResolvedContentConstraint = constraint;

        return new Size(
            columnExtents.AsSpan().SaturatingSum().Add(
                LayoutMath.GapExtent(ColumnSpacing, columns.Length, constraint.Width)),
            rowExtents.AsSpan().SaturatingSum().Add(
                LayoutMath.GapExtent(RowSpacing, rows.Length, constraint.Height)));
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds)
    {
        ValidatePlacements();
        var rows = Definitions(Rows);
        var columns = Definitions(Columns);

        // A scrolling row or column axis has no real ceiling - Container.ResolveContentSlot
        // sizes bounds to Math.Max(Extent, Viewport) specifically so overflowing content can lay
        // out past the visible area, and scrolling absorbs the rest. But Extent is, by
        // construction, the sum of every child's own pre-arrange intrinsic request, so a Percent
        // track's true (viewport-relative) size is never part of it - resolving Percent against
        // that stale total as a hard ceiling always produces an artificial deficit that crushes
        // it back to its own tiny intrinsic size, or to zero, the moment an Auto sibling's own
        // request already consumes most of the extent. An unbounded pool removes that
        // false ceiling entirely: every track gets its own full, non-competing request, with
        // Percent still sized against the visible viewport instead of falling back to its own
        // unrelated intrinsic request.
        var rowScrolls = AutoScroll && (ScrollBars & ScrollBars.Vertical) != 0;
        var columnScrolls = AutoScroll && (ScrollBars & ScrollBars.Horizontal) != 0;
        int? rowAvailable = rowScrolls ? null : bounds.Height;
        int? columnAvailable = columnScrolls ? null : bounds.Width;
        var rowPercentBase = rowScrolls ? Viewport.Height : (int?) null;
        var columnPercentBase = columnScrolls ? Viewport.Width : (int?) null;
        var rowRequests = Requests(rows.Length, rows: true, rows, bounds.Height);
        var columnRequests = Requests(columns.Length, rows: false, columns, bounds.Width);
        var rowExtents = Resolve(rowAvailable, RowSpacing, rows, rowRequests, rowPercentBase);
        var columnExtents = Resolve(columnAvailable, ColumnSpacing, columns, columnRequests, columnPercentBase);

        // Final viewport dimensions can differ from measure. Re-measuring the
        // exact spanned slots keeps wrapped content deterministic after resize.
        // When the arranged viewport is exactly what MeasureOverride already
        // resolved these same extents against, the repeat is redundant.
        var viewportChanged = _lastResolvedContentConstraint is not { } measured ||
            measured.Width != bounds.Width || measured.Height != bounds.Height;

        if (viewportChanged)
        {
            MeasureChildren(rowExtents, columnExtents, bounds.Height, bounds.Width);
            columnRequests = Requests(columns.Length, rows: false, columns, bounds.Width);
            columnExtents = Resolve(columnAvailable, ColumnSpacing, columns, columnRequests, columnPercentBase);
            MeasureAutomaticRows(rows, columnExtents, bounds.Width);
            rowRequests = Requests(rows.Length, rows: true, rows, bounds.Height);
            rowExtents = Resolve(rowAvailable, RowSpacing, rows, rowRequests, rowPercentBase);

            // Children now describe bounds, not the constraint MeasureOverride last recorded. A
            // later arrange back at that stale recorded constraint would otherwise compare equal
            // and skip, arranging Requests() captured at this different width instead.
            _lastResolvedContentConstraint = new Constraint(bounds.Width, bounds.Height);
        }

        ArrangeChildren(bounds, rowExtents, columnExtents);
    }

    private static void Validate(ControlBase control, int origin, int span, bool rows)
    {
        if (control.Parent is not Grid parent)
        {
            return;
        }

        var count = rows ? Math.Max(1, parent.Rows.Count) : Math.Max(1, parent.Columns.Count);

        if (origin >= count || span > count - origin)
        {
            throw new ArgumentOutOfRangeException(
                rows ? "row" : "column",
                origin,
                "The Grid placement must fit committed definitions.");
        }
    }

    private static void ValidateRow(ControlBase control, int value) =>
        Validate(control, value, GetRowSpan(control), rows: true);

    private static void ValidateColumn(ControlBase control, int value) =>
        Validate(control, value, GetColumnSpan(control), rows: false);

    private static void ValidateRowSpan(ControlBase control, int value) =>
        Validate(control, GetRow(control), value, rows: true);

    private static void ValidateColumnSpan(ControlBase control, int value) =>
        Validate(control, GetColumn(control), value, rows: false);

    [Pure]
    private static Track[] Definitions(TrackCollection source)
    {
        Debug.Assert(source is not null, "Grid definitions require a non-null collection.");

        if (source.Count == 0)
        {
            return [Track.Auto()];
        }

        var result = new Track[source.Count];
        source.CopyTo(result, 0);

        return result;
    }

    [Pure]
    private static int[] Resolve(
        int? available,
        int spacing,
        ReadOnlySpan<Track> definitions,
        ReadOnlySpan<int> requests,
        int? percentBase = null)
    {
        Debug.Assert(spacing >= 0, "Grid spacing is non-negative.");
        Debug.Assert(definitions.Length == requests.Length, "Every track definition must have one request.");

        var lengths = new Length[definitions.Length];
        var minimum = new Length[definitions.Length];
        var maximum = new Length?[definitions.Length];
        var result = new int[definitions.Length];

        for (var index = 0; index < definitions.Length; index++)
        {
            lengths[index] = definitions[index].Length;
            minimum[index] = definitions[index].Minimum;
            maximum[index] = definitions[index].Maximum;
        }

        int? trackArea = available.HasValue
            ? Math.Max(0, available.Value - LayoutMath.GapExtent(spacing, definitions.Length, available))
            : null;
        Tracks.Resolve(trackArea, lengths, requests, minimum, maximum, result, percentBase);

        return result;
    }

    private void ArrangeChildren(
        Rect bounds,
        ReadOnlySpan<int> rowExtents,
        ReadOnlySpan<int> columnExtents)
    {
        Debug.Assert(rowExtents.Length >= 0, "Grid arrangement requires valid row extents.");
        Debug.Assert(columnExtents.Length >= 0, "Grid arrangement requires valid column extents.");

        var rowOrigins = Origins(bounds.Y, bounds.Height, RowSpacing, rowExtents);
        var columnOrigins = Origins(bounds.X, bounds.Width, ColumnSpacing, columnExtents);

        foreach (var child in Children)
        {
            if (child.Visibility == Visibility.Collapsed)
            {
                continue;
            }

            var row = GetRow(child);
            var column = GetColumn(child);
            var width = SpanExtent(
                columnExtents,
                column,
                GetColumnSpan(child),
                ColumnSpacing,
                bounds.Width);
            var height = SpanExtent(
                rowExtents,
                row,
                GetRowSpan(child),
                RowSpacing,
                bounds.Height);
            // A cell fills by default, matching every existing consumer that relies on Grid to
            // size its children - HorizontalAlignment defaults to Left, not Stretch, so blindly
            // deferring to ResolveArrangeAxis (as Stack's cross axis does) would shrink every
            // Auto-width child down to its intrinsic size instead of filling the cell. An axis is
            // left unresolved, and so participates through Width/Height and alignment instead of
            // being silently overridden, only when the child asked for an explicit non-Auto
            // Width/Height. An Auto axis stays resolved (filled) even under a non-default
            // alignment: MinWidth/MaxWidth already cap that fill and hand Align the resulting
            // slack, which is the pre-existing, already-correct MaxWidth+alignment contract this
            // fix must not disturb while fixing the Width+alignment one.
            var widthResolved = child.Width.Kind == LengthKind.Auto;
            var heightResolved = child.Height.Kind == LengthKind.Auto;
            child.Arrange(
                new Rect(columnOrigins[column], rowOrigins[row], width, height),
                widthResolved: widthResolved,
                heightResolved: heightResolved);
        }
    }

    [Pure]
    private static int[] Origins(
        int origin,
        int available,
        int spacing,
        ReadOnlySpan<int> extents)
    {
        Debug.Assert(available >= 0, "Grid origin base is non-negative.");
        Debug.Assert(spacing >= 0, "Grid origin spacing is non-negative.");

        var result = new int[extents.Length];
        var position = origin;
        var remainingSpacing = LayoutMath.GapExtent(spacing, extents.Length, available);

        for (var index = 0; index < extents.Length; index++)
        {
            result[index] = position;
            position = position.Add(extents[index]);

            if (index < extents.Length - 1)
            {
                var gap = Math.Min(spacing, remainingSpacing);
                position = position.Add(gap);
                remainingSpacing -= gap;
            }
        }

        return result;
    }

    private int[] Requests(int count, bool rows, ReadOnlySpan<Track> definitions, int? available)
    {
        Debug.Assert(count >= 1, "Grid requests require at least one track.");
        Debug.Assert(definitions.Length == count, "Every track has one definition.");

        var result = new int[count];

        // Non-spanning requests establish the individual intrinsic tracks.
        foreach (var child in Children)
        {
            if (child.Visibility == Visibility.Collapsed)
            {
                continue;
            }

            var span = rows ? GetRowSpan(child) : GetColumnSpan(child);

            if (span != 1)
            {
                continue;
            }

            var origin = rows ? GetRow(child) : GetColumn(child);
            var desired = rows
                ? child.DesiredSize.Height.Add(child.Margin.Vertical)
                : child.DesiredSize.Width.Add(child.Margin.Horizontal);
            result[origin] = Math.Max(result[origin], desired);
        }

        // ResolveCore reads the automatic request back only for the "absorbing" kinds - Auto
        // alone when bounded, everything except Cells when unbounded. A spanning
        // child's request must therefore be deposited only into the span's absorbing tracks,
        // net of the extent the span's own non-absorbing tracks (Cells always; Percent and Star
        // when bounded) already independently contribute - otherwise that contribution is
        // silently discarded rather than counted, and a non-Auto-only span degrades to a
        // fraction of the child's intrinsic size.
        var spacing = rows ? RowSpacing : ColumnSpacing;
        var resolvedNonAbsorbing = Resolve(available, spacing, definitions, result);

        // Spanning requests expand the span's absorbing tracks only by the cells still required
        // after their internal Grid gaps and the span's already-resolvable extent are accounted
        // for.
        List<int>? absorbing = null;

        foreach (var child in Children)
        {
            if (child.Visibility == Visibility.Collapsed)
            {
                continue;
            }

            var span = rows ? GetRowSpan(child) : GetColumnSpan(child);

            if (span == 1)
            {
                continue;
            }

            var origin = rows ? GetRow(child) : GetColumn(child);
            var desired = rows
                ? child.DesiredSize.Height.Add(child.Margin.Vertical)
                : child.DesiredSize.Width.Add(child.Margin.Horizontal);
            var internalSpacing = LayoutMath.GapExtent(spacing, span, desired);
            var required = Math.Max(0, desired - internalSpacing);

            absorbing ??= new List<int>(count);
            absorbing.Clear();
            var reserved = 0;

            for (var index = origin; index < origin + span; index++)
            {
                var isAbsorbing = available.HasValue
                    ? definitions[index].Length.Kind == LengthKind.Auto
                    : definitions[index].Length.Kind != LengthKind.Cells;

                if (isAbsorbing)
                {
                    absorbing.Add(index);
                }
                else
                {
                    reserved += resolvedNonAbsorbing[index];
                }
            }

            if (absorbing.Count == 0)
            {
                // No track in the span reads the automatic request back - a fixed-only span
                // (for example Cells + Cells) genuinely caps the child, matching Tracks'
                // documented, deliberately kind-blind Satisfy contract for that case.
                continue;
            }

            SatisfyAbsorbing(result, absorbing, Math.Max(0, required - reserved));
        }

        return result;
    }

    /// <summary>Distributes a deficit across a possibly non-contiguous set of absorbing track
    /// indices using the same deterministic cumulative-ratio rounding
    /// <see cref="Tracks.Satisfy"/> uses for a contiguous span, since a spanning child's
    /// absorbing tracks are not necessarily adjacent once non-absorbing tracks in the span are
    /// excluded.</summary>
    private static void SatisfyAbsorbing(int[] tracks, List<int> indices, int required)
    {
        Debug.Assert(indices.Count > 0, "Absorbing distribution requires at least one track.");

        var current = 0L;

        foreach (var index in indices)
        {
            current += tracks[index];
        }

        if (current >= required)
        {
            return;
        }

        var deficit = required - (int) current;
        var previous = 0;

        for (var offset = 0; offset < indices.Count; offset++)
        {
            var edge = (int) ((((long) deficit * (offset + 1)) + (indices.Count / 2L)) / indices.Count);
            tracks[indices[offset]] += edge - previous;
            previous = edge;
        }
    }

    private void MeasureChildren(Constraint constraint)
    {
        foreach (var child in Children)
        {
            child.Measure(constraint);
        }
    }

    private void MeasureChildren(
        ReadOnlySpan<int> rowExtents,
        ReadOnlySpan<int> columnExtents,
        int? availableHeight,
        int? availableWidth)
    {
        foreach (var child in Children)
        {
            if (child.Visibility == Visibility.Collapsed)
            {
                continue;
            }

            child.Measure(new Constraint(
                SpanExtent(
                    columnExtents,
                    GetColumn(child),
                    GetColumnSpan(child),
                    ColumnSpacing,
                    availableWidth),
                SpanExtent(
                    rowExtents,
                    GetRow(child),
                    GetRowSpan(child),
                    RowSpacing,
                    availableHeight)));
        }
    }

    private void MeasureAutomaticRows(
        ReadOnlySpan<Track> rows,
        ReadOnlySpan<int> columnExtents,
        int? availableWidth)
    {
        foreach (var child in Children)
        {
            if (child.Visibility == Visibility.Collapsed ||
                !IsAutomaticSpan(rows, GetRow(child), GetRowSpan(child)))
            {
                continue;
            }

            var width = SpanExtent(
                columnExtents,
                GetColumn(child),
                GetColumnSpan(child),
                ColumnSpacing,
                availableWidth);
            child.Measure(new Constraint(width, height: null));
        }
    }

    [Pure]
    private static bool IsAutomaticSpan(ReadOnlySpan<Track> definitions, int origin, int span)
    {
        Debug.Assert(span >= 1, "Grid automatic span is positive.");
        Debug.Assert(origin >= 0 && origin + span <= definitions.Length, "Grid automatic span fits definitions.");

        for (var index = origin; index < origin + span; index++)
        {
            if (definitions[index].Length.Kind != LengthKind.Auto)
            {
                return false;
            }
        }

        return true;
    }

    [Pure]
    private static int SpanExtent(
        ReadOnlySpan<int> extents,
        int origin,
        int span,
        int spacing,
        int? available)
    {
        Debug.Assert(span >= 1, "Grid span extent is positive.");
        Debug.Assert(origin >= 0 && origin + span <= extents.Length, "Grid span fits extents.");
        Debug.Assert(spacing >= 0, "Grid span spacing is non-negative.");

        var result = 0;

        for (var index = origin; index < origin + span; index++)
        {
            result = result.Add(extents[index]);
        }

        var totalSpacing = LayoutMath.GapExtent(spacing, extents.Length, available);

        for (var gap = origin; gap < origin + span - 1; gap++)
        {
            var used = gap.Multiply(spacing);
            result = result.Add(Math.Min(spacing, Math.Max(0, totalSpacing - used)));
        }

        return result;
    }

    private void ValidatePlacements()
    {
        foreach (var child in Children)
        {
            ValidatePlacement(child, Math.Max(1, Rows.Count), rows: true);
            ValidatePlacement(child, Math.Max(1, Columns.Count), rows: false);
        }
    }

    private static void ValidatePlacement(ControlBase child, int count, bool rows)
    {
        Debug.Assert(child is not null, "Grid placement validation requires a non-null child.");
        Debug.Assert(count >= 1, "Grid placement validation requires at least one track.");

        var origin = rows ? GetRow(child) : GetColumn(child);
        var span = rows ? GetRowSpan(child) : GetColumnSpan(child);

        if (origin >= count || span > count - origin)
        {
            throw new InvalidOperationException(
                $"The child {(rows ? "row" : "column")} placement does not fit Grid definitions.");
        }
    }

    private void DefinitionsChanged() => Invalidate(Invalidation.Measure);

    private void DefinitionsChanging(int candidateCount, bool rows)
    {
        VerifyMutable();
        var effectiveCount = Math.Max(1, candidateCount);

        foreach (var child in Children)
        {
            ValidatePlacement(child, effectiveCount, rows);
        }

        Debug.Assert(candidateCount >= 0, "A track collection candidate count cannot be negative.");
    }
}
