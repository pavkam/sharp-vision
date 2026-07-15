// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Arranges typed rows and columns into a terminal-safe table with optional headers and grid lines.</summary>
public sealed class Table: ItemsControl
{
    private readonly TablePresenter _presenter;

    #region Construction and properties

    /// <summary>Initializes empty mutable row and column collections.</summary>
    public Table()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch;
        Columns = new TableColumns(this);
        Rows = new TableRows(this);
        _presenter = new TablePresenter(this);
        InitializeItemsHost(_presenter);
    }

    /// <summary>Gets the mutable titled column definitions.</summary>
    public TableColumns Columns { get; }

    /// <summary>Gets the mutable owned data rows.</summary>
    public TableRows Rows { get; }

    /// <summary>Gets or sets whether the titled header row is rendered.</summary>
    /// <exception cref="InvalidOperationException">The attached table is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The table is disposed.</exception>
    public bool ShowHeader
    {
        get;
        set => _ = SetProperty(ref field, value, ChangeImpact.Measure);
    } = true;

    /// <summary>Gets or sets non-negative padding applied to every header and data cell.</summary>
    /// <exception cref="InvalidOperationException">The attached table is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The table is disposed.</exception>
    public Thickness CellPadding
    {
        get;
        set => _ = SetProperty(ref field, value, ChangeImpact.Measure);
    }

    /// <summary>Gets or sets non-negative cells between adjacent data rows.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    /// <exception cref="InvalidOperationException">The attached table is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The table is disposed.</exception>
    public int RowSpacing
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _ = SetProperty(ref field, value, ChangeImpact.Measure);
        }
    }

    /// <summary>Gets or sets non-negative cells between adjacent columns.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    /// <exception cref="InvalidOperationException">The attached table is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The table is disposed.</exception>
    public int ColumnSpacing
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _ = SetProperty(ref field, value, ChangeImpact.Measure);
        }
    }

    /// <summary>Gets or sets whether one-cell light grid lines are drawn in every available table gap.</summary>
    /// <exception cref="InvalidOperationException">The attached table is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The table is disposed.</exception>
    public bool ShowGridLines
    {
        get;
        set => _ = SetProperty(ref field, value, ChangeImpact.Measure);
    } = true;

    /// <summary>Identifies the themeable header-text foreground style property.</summary>
    public static StyleProperty<Color?> HeaderForegroundProperty { get; } =
        StyleProperty<Color?>.Register<Table>("header-foreground", null, ChangeImpact.Render);

    /// <summary>Identifies the themeable header-row background style property.</summary>
    public static StyleProperty<Color?> HeaderBackgroundProperty { get; } =
        StyleProperty<Color?>.Register<Table>("header-background", null, ChangeImpact.Render);

    /// <summary>Identifies the themeable grid-line color style property.</summary>
    public static StyleProperty<Color?> GridLineColorProperty { get; } =
        StyleProperty<Color?>.Register<Table>("grid-line-color", null, ChangeImpact.Render);

    /// <summary>Gets or sets an optional foreground override for header text, resolved through the theme.</summary>
    /// <exception cref="InvalidOperationException">The attached table is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The table is disposed.</exception>
    public Color? HeaderForeground
    {
        get => GetValue(HeaderForegroundProperty);
        set => SetValue(HeaderForegroundProperty, value);
    }

    /// <summary>Gets or sets an optional background override for the header row, resolved through the theme.</summary>
    /// <exception cref="InvalidOperationException">The attached table is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The table is disposed.</exception>
    public Color? HeaderBackground
    {
        get => GetValue(HeaderBackgroundProperty);
        set => SetValue(HeaderBackgroundProperty, value);
    }

    /// <summary>Gets or sets an optional grid-line color, resolved through the theme.</summary>
    /// <exception cref="InvalidOperationException">The attached table is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The table is disposed.</exception>
    public Color? GridLineColor
    {
        get => GetValue(GridLineColorProperty);
        set => SetValue(GridLineColorProperty, value);
    }

    /// <summary>Gets the committed non-negative scrolling content extent.</summary>
    public Size Extent => _presenter.Extent;

    /// <summary>Gets the committed non-negative scrolling viewport extent.</summary>
    public Size Viewport => _presenter.Viewport;

    /// <summary>Raised after the private table viewport commits one or both offsets.</summary>
    public event EventHandler<ScrollChangedEventArgs> ScrollChanged
    {
        add => _presenter.ScrollChanged += value;
        remove => _presenter.ScrollChanged -= value;
    }

    /// <summary>Gets or sets the scrollable axes of the private cell presenter.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value contains unknown axis flags.</exception>
    /// <exception cref="InvalidOperationException">The attached table is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The table is disposed.</exception>
    public ScrollBars ScrollBars
    {
        get => _presenter.ScrollBars;
        set => _presenter.ScrollBars = value;
    }

    /// <summary>Gets or sets the common scrollbar reservation policy for the private cell presenter.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached table is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The table is disposed.</exception>
    public ShowScrollBars ShowScrollBars
    {
        get => _presenter.ShowScrollBars;
        set => _presenter.ShowScrollBars = value;
    }

    /// <summary>Gets or sets the generated chrome form for both private scrollbars.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached table is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The table is disposed.</exception>
    public ScrollBarChrome ScrollBarChrome
    {
        get => _presenter.ScrollBarChrome;
        set => _presenter.ScrollBarChrome = value;
    }

    /// <summary>Gets or sets the generated fill treatment for both private scrollbars.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached table is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The table is disposed.</exception>
    public ScrollBarFill ScrollBarFill
    {
        get => _presenter.ScrollBarFill;
        set => _presenter.ScrollBarFill = value;
    }

    /// <summary>Gets or sets the non-negative keyboard and wheel scrolling increment in cells.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    /// <exception cref="InvalidOperationException">The attached table is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The table is disposed.</exception>
    public int LineSize
    {
        get => _presenter.LineSize;
        set => _presenter.LineSize = value;
    }

    /// <summary>Gets or sets non-negative cells retained between page commands.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    /// <exception cref="InvalidOperationException">The attached table is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The table is disposed.</exception>
    public int PageOverlap
    {
        get => _presenter.PageOverlap;
        set => _presenter.PageOverlap = value;
    }

    /// <summary>Gets or sets the valid horizontal content offset.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is outside the current extent.</exception>
    /// <exception cref="InvalidOperationException">The attached table is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The table is disposed.</exception>
    public int HorizontalOffset
    {
        get => _presenter.HorizontalOffset;
        set => _presenter.HorizontalOffset = value;
    }

    /// <summary>Gets or sets the valid vertical content offset.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is outside the current extent.</exception>
    /// <exception cref="InvalidOperationException">The attached table is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The table is disposed.</exception>
    public int VerticalOffset
    {
        get => _presenter.VerticalOffset;
        set => _presenter.VerticalOffset = value;
    }

    /// <summary>Adds signed scrolling deltas with endpoint clamping.</summary>
    /// <param name="x">The requested horizontal delta.</param>
    /// <param name="y">The requested vertical delta.</param>
    /// <param name="cause">The defined input path.</param>
    /// <returns>True when at least one offset changed.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="cause"/> is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached table is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The table is disposed.</exception>
    public bool ScrollBy(int x, int y, Scrolling.Cause cause = Scrolling.Cause.Programmatic) =>
        _presenter.ScrollBy(x, y, cause);

    /// <summary>Scrolls minimally to expose one row-cell descendant.</summary>
    /// <param name="descendant">The non-null descendant control.</param>
    /// <returns>True when at least one offset changed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="descendant"/> is null.</exception>
    /// <exception cref="ArgumentException">The control is not a realized table descendant.</exception>
    /// <exception cref="InvalidOperationException">The attached table is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The table is disposed.</exception>
    public bool BringIntoView(Control descendant) => _presenter.BringIntoView(descendant);

    #endregion

    #region Layout and rendering

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
        => MeasureChild(_presenter, constraint);

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds) =>
        ArrangeChild(_presenter, bounds, ResolvedAxes.Both);

    /// <inheritdoc/>
    protected override void OnRender(TerminalCanvas canvas) => _presenter.RenderTableChrome(canvas);

    /// <summary>Gets the current state snapshot for private table chrome resolution.</summary>
    internal State CurrentVisualState => GetVisualState();

    #endregion

    #region Row ownership

    /// <summary>Validates a candidate column count against every owned row.</summary>
    /// <param name="count">The candidate count.</param>
    internal void ValidateColumnCount(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        VerifyMutable();

        if (Rows.Count != 0 && Rows.Any(row => row.Cells.Count != count))
        {
            throw new ArgumentException("Every existing row must retain one cell per column.", nameof(count));
        }
    }

    /// <summary>Invalidates measurement after a committed column mutation.</summary>
    internal void ColumnsChanged() => Invalidate(Invalidation.Measure);

    /// <summary>Inserts and attaches one validated row.</summary>
    /// <param name="owner">The calling row collection.</param>
    /// <param name="index">The insertion index.</param>
    /// <param name="row">The non-null row.</param>
    internal void InsertRow(TableRows owner, int index, TableRow row)
    {
        VerifyRowsOwner(owner);
        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint) index, (uint) Rows.Count);
        ValidateRow(row);

        foreach (var cell in row.Cells)
        {
            _presenter.Children.Add(cell);
        }

        owner.InsertAttached(index, row);
        Invalidate(Invalidation.Measure);
    }

    /// <summary>Removes and detaches one owned row.</summary>
    /// <param name="owner">The calling row collection.</param>
    /// <param name="index">The valid row index.</param>
    internal void RemoveRow(TableRows owner, int index)
    {
        VerifyRowsOwner(owner);
        var row = Rows[index];

        foreach (var cell in row.Cells)
        {
            _ = _presenter.Children.Remove(cell);
        }

        owner.RemoveAttached(index);
        Invalidate(Invalidation.Measure);
    }

    /// <summary>Clears and detaches every owned row.</summary>
    /// <param name="owner">The calling row collection.</param>
    internal void ClearRows(TableRows owner)
    {
        VerifyRowsOwner(owner);

        for (var index = Rows.Count - 1; index >= 0; index--)
        {
            RemoveRow(owner, index);
        }
    }

    /// <summary>Atomically replaces one row after validating the candidate ownership transfer.</summary>
    /// <param name="owner">The calling row collection.</param>
    /// <param name="index">The valid row index.</param>
    /// <param name="row">The non-null replacement row.</param>
    internal void ReplaceRow(TableRows owner, int index, TableRow row)
    {
        VerifyRowsOwner(owner);
        _ = Rows[index];
        ValidateRow(row);
        var previous = Rows[index];

        foreach (var cell in previous.Cells)
        {
            _ = _presenter.Children.Remove(cell);
        }

        foreach (var cell in row.Cells)
        {
            _presenter.Children.Add(cell);
        }

        owner.ReplaceAttached(index, row);
        Invalidate(Invalidation.Measure);
    }

    #endregion

    #region Validation

    private void ValidateRow(TableRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        VerifyMutable();

        if (Columns.Count == 0 || row.Cells.Count != Columns.Count)
        {
            throw new ArgumentException("Every row requires exactly one cell per defined column.", nameof(row));
        }

        foreach (var cell in row.Cells)
        {
            if (cell.Parent is not null || cell.IsDisposed)
            {
                throw new ArgumentException("Every table cell must be detached and available.", nameof(row));
            }
        }
    }

    private void VerifyRowsOwner(TableRows owner)
    {
        ArgumentNullException.ThrowIfNull(owner);

        if (!ReferenceEquals(owner, Rows))
        {
            throw new ArgumentException("The row collection does not belong to this table.", nameof(owner));
        }
    }

    #endregion
}
