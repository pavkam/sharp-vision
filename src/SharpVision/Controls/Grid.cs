using System.Runtime.CompilerServices;

using SharpVision.Layout;

namespace SharpVision.Controls;

/// <summary>Owns row/column definitions and attached placement for a track Grid.</summary>
public sealed class Grid: Container
{
    private static readonly ConditionalWeakTable<Control, GridPlacement> _placements = [];

    /// <summary>Initializes permanent row and column definition collections.</summary>
    public Grid()
    {
        Rows = new TrackCollection(DefinitionsChanging, DefinitionsChanged);
        Columns = new TrackCollection(DefinitionsChanging, DefinitionsChanged);
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
        var placement = _placements.GetOrCreateValue(control);

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
        var placement = _placements.GetOrCreateValue(control);

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
        var placement = _placements.GetOrCreateValue(control);

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
        var placement = _placements.GetOrCreateValue(control);

        if (placement.ColumnSpan != value)
        {
            placement.ColumnSpan = value;
            InvalidateParent(control);
        }
    }

    private static GridPlacement? GetPlacement(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);
        return _placements.TryGetValue(control, out var placement) ? placement : null;
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

        var count = rows ? Math.Max(1, parent.Rows.Count) : Math.Max(1, parent.Columns.Count);

        if (origin >= count || span > count - origin)
        {
            throw new ArgumentOutOfRangeException(
                rows ? "row" : "column",
                origin,
                "The Grid placement must fit committed definitions.");
        }
    }

    private void DefinitionsChanged() => Invalidate(Invalidation.Measure);

    private void DefinitionsChanging() => VerifyMutable();
}
