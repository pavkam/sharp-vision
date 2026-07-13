// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using System.Runtime.CompilerServices;


/// <summary>Owns row/column definitions and attached placement for a track Grid.</summary>
public sealed class Grid: Container
{
    private static readonly ConditionalWeakTable<Control, GridPlacement> _placements = [];

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
    }

    /// <summary>Gets the mutable row definitions; empty means one automatic row.</summary>
    public TrackCollection Rows { get; }

    /// <summary>Gets the mutable column definitions; empty means one automatic column.</summary>
    public TrackCollection Columns { get; }

    /// <summary>Gets or sets non-negative cells between rows.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    /// <exception cref="InvalidOperationException">The attached Grid is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The Grid is disposed.</exception>
    public int RowSpacing
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _ = Set(ref field, value, Invalidation.Measure);
        }
    }

    /// <summary>Gets or sets non-negative cells between columns.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    /// <exception cref="InvalidOperationException">The attached Grid is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The Grid is disposed.</exception>
    public int ColumnSpacing
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _ = Set(ref field, value, Invalidation.Measure);
        }
    }

    /// <summary>Gets a control's zero-based attached row.</summary>
    /// <param name="control">The non-null control.</param>
    /// <returns>The attached row, defaulting to zero.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> is null.</exception>
    public static int GetRow(Control control) => GetPlacement(control)?.Row ?? 0;

    /// <summary>Gets a control's zero-based attached column.</summary>
    /// <param name="control">The non-null control.</param>
    /// <returns>The attached column, defaulting to zero.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> is null.</exception>
    public static int GetColumn(Control control) => GetPlacement(control)?.Column ?? 0;

    /// <summary>Gets a control's positive attached row span.</summary>
    /// <param name="control">The non-null control.</param>
    /// <returns>The attached row span, defaulting to one.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> is null.</exception>
    public static int GetRowSpan(Control control) => GetPlacement(control)?.RowSpan ?? 1;

    /// <summary>Gets a control's positive attached column span.</summary>
    /// <param name="control">The non-null control.</param>
    /// <returns>The attached column span, defaulting to one.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> is null.</exception>
    public static int GetColumnSpan(Control control) => GetPlacement(control)?.ColumnSpan ?? 1;

    /// <summary>Sets a control's zero-based attached row.</summary>
    /// <param name="control">The non-null mutable control.</param>
    /// <param name="value">The non-negative row.</param>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The row is negative or outside committed tracks.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public static void SetRow(Control control, int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        Validate(control, value, GetRowSpan(control), rows: true);
        GridPlacement placement = _placements.GetOrCreateValue(control);

        if (placement.Row != value)
        {
            placement.Row = value;
            InvalidateParent(control);
        }
    }

    /// <summary>Sets a control's zero-based attached column.</summary>
    /// <param name="control">The non-null mutable control.</param>
    /// <param name="value">The non-negative column.</param>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The column is negative or outside committed tracks.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public static void SetColumn(Control control, int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        Validate(control, value, GetColumnSpan(control), rows: false);
        GridPlacement placement = _placements.GetOrCreateValue(control);

        if (placement.Column != value)
        {
            placement.Column = value;
            InvalidateParent(control);
        }
    }

    /// <summary>Sets a control's positive attached row span.</summary>
    /// <param name="control">The non-null mutable control.</param>
    /// <param name="value">The positive row span.</param>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The span is not positive or exceeds committed tracks.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public static void SetRowSpan(Control control, int value)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, 0);
        Validate(control, GetRow(control), value, rows: true);
        GridPlacement placement = _placements.GetOrCreateValue(control);

        if (placement.RowSpan != value)
        {
            placement.RowSpan = value;
            InvalidateParent(control);
        }
    }

    /// <summary>Sets a control's positive attached column span.</summary>
    /// <param name="control">The non-null mutable control.</param>
    /// <param name="value">The positive column span.</param>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The span is not positive or exceeds committed tracks.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public static void SetColumnSpan(Control control, int value)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, 0);
        Validate(control, GetColumn(control), value, rows: false);
        GridPlacement placement = _placements.GetOrCreateValue(control);

        if (placement.ColumnSpan != value)
        {
            placement.ColumnSpan = value;
            InvalidateParent(control);
        }
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        ValidatePlacements();
        Track[] rows = Definitions(Rows);
        Track[] columns = Definitions(Columns);

        MeasureChildren(new Constraint(width: null, height: null));
        int[] rowRequests = Requests(rows.Length, rows: true);
        int[] columnRequests = Requests(columns.Length, rows: false);
        int[] rowExtents = Resolve(constraint.Height, RowSpacing, rows, rowRequests);
        int[] columnExtents = Resolve(constraint.Width, ColumnSpacing, columns, columnRequests);

        // A finite track slot can alter wrapping and therefore the intrinsic
        // request on the other axis. Re-measure once, then resolve the stable
        // requests used for both desired size and final arrangement.
        MeasureChildren(
            rowExtents,
            columnExtents,
            constraint.Height,
            constraint.Width);
        columnRequests = Requests(columns.Length, rows: false);
        columnExtents = Resolve(constraint.Width, ColumnSpacing, columns, columnRequests);

        // Automatic rows must let wrapped children report their newly expanded
        // height after finite column widths are known. The initial row extent is
        // only a probe and would otherwise cap the remeasure to one old line.
        MeasureAutomaticRows(rows, columnExtents, constraint.Width);
        rowRequests = Requests(rows.Length, rows: true);
        rowExtents = Resolve(constraint.Height, RowSpacing, rows, rowRequests);

        return new Size(
            Add(Sum(columnExtents), Spacing(ColumnSpacing, columns.Length, constraint.Width)),
            Add(Sum(rowExtents), Spacing(RowSpacing, rows.Length, constraint.Height)));
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds)
    {
        ValidatePlacements();
        Track[] rows = Definitions(Rows);
        Track[] columns = Definitions(Columns);
        int[] rowRequests = Requests(rows.Length, rows: true);
        int[] columnRequests = Requests(columns.Length, rows: false);
        int[] rowExtents = Resolve(bounds.Height, RowSpacing, rows, rowRequests);
        int[] columnExtents = Resolve(bounds.Width, ColumnSpacing, columns, columnRequests);

        // Final viewport dimensions can differ from measure. Re-measuring the
        // exact spanned slots keeps wrapped content deterministic after resize.
        MeasureChildren(rowExtents, columnExtents, bounds.Height, bounds.Width);
        columnRequests = Requests(columns.Length, rows: false);
        columnExtents = Resolve(bounds.Width, ColumnSpacing, columns, columnRequests);
        MeasureAutomaticRows(rows, columnExtents, bounds.Width);
        rowRequests = Requests(rows.Length, rows: true);
        rowExtents = Resolve(bounds.Height, RowSpacing, rows, rowRequests);
        ArrangeChildren(bounds, rowExtents, columnExtents);
    }

    private static GridPlacement? GetPlacement(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);
        return _placements.TryGetValue(control, out GridPlacement? placement) ? placement : null;
    }

    private static void InvalidateParent(Control control)
    {
        if (control.Parent is Grid parent)
        {
            parent.Invalidate(Invalidation.Measure);
        }
    }

    private static void Validate(Control control, int origin, int span, bool rows)
    {
        ArgumentNullException.ThrowIfNull(control);
        control.VerifyMutable();

        if (control.Parent is not Grid parent)
        {
            return;
        }

        int count = rows ? Math.Max(1, parent.Rows.Count) : Math.Max(1, parent.Columns.Count);

        if (origin >= count || span > count - origin)
        {
            throw new ArgumentOutOfRangeException(
                rows ? "row" : "column",
                origin,
                "The Grid placement must fit committed definitions.");
        }
    }

    private static int Add(int left, int right)
    {
        long result = (long) left + right;
        return result >= int.MaxValue ? int.MaxValue : (int) result;
    }

    private static Track[] Definitions(TrackCollection source)
    {
        if (source.Count == 0)
        {
            return [Track.Auto()];
        }

        Track[] result = new Track[source.Count];
        source.CopyTo(result, 0);
        return result;
    }

    private static int[] Resolve(
        int? available,
        int spacing,
        ReadOnlySpan<Track> definitions,
        ReadOnlySpan<int> requests)
    {
        Length[] lengths = new Length[definitions.Length];
        int[] minimum = new int[definitions.Length];
        int[] maximum = new int[definitions.Length];
        int[] result = new int[definitions.Length];

        for (int index = 0; index < definitions.Length; index++)
        {
            lengths[index] = definitions[index].Length;
            minimum[index] = definitions[index].Minimum;
            maximum[index] = definitions[index].Maximum;
        }

        int? trackArea = available.HasValue
            ? Math.Max(0, available.Value - Spacing(spacing, definitions.Length, available))
            : null;
        Tracks.Resolve(trackArea, lengths, requests, minimum, maximum, result);
        return result;
    }

    private static int Spacing(int value, int count, int? limit)
    {
        if (count <= 1)
        {
            return 0;
        }

        long requested = Math.Min(int.MaxValue, (long) value * (count - 1));
        return limit.HasValue ? (int) Math.Min(limit.Value, requested) : (int) requested;
    }

    private static int Sum(ReadOnlySpan<int> values)
    {
        int result = 0;

        foreach (int value in values)
        {
            result = Add(result, value);
        }

        return result;
    }

    private void ArrangeChildren(
        Rect bounds,
        ReadOnlySpan<int> rowExtents,
        ReadOnlySpan<int> columnExtents)
    {
        int[] rowOrigins = Origins(bounds.Y, bounds.Height, RowSpacing, rowExtents);
        int[] columnOrigins = Origins(bounds.X, bounds.Width, ColumnSpacing, columnExtents);

        foreach (Control child in Children)
        {
            if (child.Visibility == Visibility.Collapsed)
            {
                child.Arrange(default);
                continue;
            }

            int row = GetRow(child);
            int column = GetColumn(child);
            int width = SpanExtent(
                columnExtents,
                column,
                GetColumnSpan(child),
                ColumnSpacing,
                bounds.Width);
            int height = SpanExtent(
                rowExtents,
                row,
                GetRowSpan(child),
                RowSpacing,
                bounds.Height);
            child.Arrange(
                new Rect(columnOrigins[column], rowOrigins[row], width, height),
                widthResolved: true,
                heightResolved: true);
        }
    }

    private static int[] Origins(
        int origin,
        int available,
        int spacing,
        ReadOnlySpan<int> extents)
    {
        int[] result = new int[extents.Length];
        int position = origin;
        int remainingSpacing = Spacing(spacing, extents.Length, available);

        for (int index = 0; index < extents.Length; index++)
        {
            result[index] = position;
            position = Add(position, extents[index]);

            if (index < extents.Length - 1)
            {
                int gap = Math.Min(spacing, remainingSpacing);
                position = Add(position, gap);
                remainingSpacing -= gap;
            }
        }

        return result;
    }

    private int[] Requests(int count, bool rows)
    {
        int[] result = new int[count];

        // Non-spanning requests establish the individual intrinsic tracks.
        foreach (Control child in Children)
        {
            if (child.Visibility == Visibility.Collapsed)
            {
                continue;
            }

            int span = rows ? GetRowSpan(child) : GetColumnSpan(child);

            if (span != 1)
            {
                continue;
            }

            int origin = rows ? GetRow(child) : GetColumn(child);
            int desired = rows
                ? Add(child.DesiredSize.Height, child.Margin.Vertical)
                : Add(child.DesiredSize.Width, child.Margin.Horizontal);
            result[origin] = Math.Max(result[origin], desired);
        }

        // Spanning requests expand the established tracks only by the cells
        // still required after their internal Grid gaps are accounted for.
        foreach (Control child in Children)
        {
            if (child.Visibility == Visibility.Collapsed)
            {
                continue;
            }

            int span = rows ? GetRowSpan(child) : GetColumnSpan(child);

            if (span == 1)
            {
                continue;
            }

            int origin = rows ? GetRow(child) : GetColumn(child);
            int desired = rows
                ? Add(child.DesiredSize.Height, child.Margin.Vertical)
                : Add(child.DesiredSize.Width, child.Margin.Horizontal);
            int spacing = rows ? RowSpacing : ColumnSpacing;
            int internalSpacing = Spacing(spacing, span, desired);
            Tracks.Satisfy(result, origin, span, Math.Max(0, desired - internalSpacing));
        }

        return result;
    }

    private void MeasureChildren(Constraint constraint)
    {
        foreach (Control child in Children)
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
        foreach (Control child in Children)
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
        foreach (Control child in Children)
        {
            if (child.Visibility == Visibility.Collapsed ||
                !IsAutomaticSpan(rows, GetRow(child), GetRowSpan(child)))
            {
                continue;
            }

            int width = SpanExtent(
                columnExtents,
                GetColumn(child),
                GetColumnSpan(child),
                ColumnSpacing,
                availableWidth);
            child.Measure(new Constraint(width, height: null));
        }
    }

    private static bool IsAutomaticSpan(ReadOnlySpan<Track> definitions, int origin, int span)
    {
        for (int index = origin; index < origin + span; index++)
        {
            if (definitions[index].Length.Kind != Kind.Auto)
            {
                return false;
            }
        }

        return true;
    }

    private static int SpanExtent(
        ReadOnlySpan<int> extents,
        int origin,
        int span,
        int spacing,
        int? available)
    {
        int result = 0;

        for (int index = origin; index < origin + span; index++)
        {
            result = Add(result, extents[index]);
        }

        int totalSpacing = Spacing(spacing, extents.Length, available);

        for (int gap = origin; gap < origin + span - 1; gap++)
        {
            long used = Math.Min(int.MaxValue, (long) gap * spacing);
            result = Add(result, (int) Math.Min(spacing, Math.Max(0, totalSpacing - used)));
        }

        return result;
    }

    private void ValidatePlacements()
    {
        foreach (Control child in Children)
        {
            ValidatePlacement(child, Math.Max(1, Rows.Count), rows: true);
            ValidatePlacement(child, Math.Max(1, Columns.Count), rows: false);
        }
    }

    private static void ValidatePlacement(Control child, int count, bool rows)
    {
        int origin = rows ? GetRow(child) : GetColumn(child);
        int span = rows ? GetRowSpan(child) : GetColumnSpan(child);

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
        int effectiveCount = Math.Max(1, candidateCount);

        foreach (Control child in Children)
        {
            ValidatePlacement(child, effectiveCount, rows);
        }

        Debug.Assert(candidateCount >= 0, "A track collection candidate count cannot be negative.");
    }
}
