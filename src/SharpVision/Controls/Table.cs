namespace SharpVision.Controls;

using SharpVision.Layout;
using SharpVision.Terminal.Geometry;
using SharpVision.Terminal.Protocols;
using SharpVision.Terminal.Rendering;

using BackgroundMode = Terminal.Rendering.BackgroundMode;
using TerminalCanvas = Terminal.Rendering.Canvas;
using TerminalStyle = Terminal.Rendering.Style;

/// <summary>Arranges typed rows and columns into a terminal-safe table with optional headers and grid lines.</summary>
public sealed class Table: Container
{
    private int[] _columnWidths = [];
    private int[] _rowHeights = [];

    #region Construction and properties

    /// <summary>Initializes empty mutable row and column collections.</summary>
    public Table()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch;
        Columns = new TableColumns(this);
        Rows = new TableRows(this);
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
        set => _ = Set(ref field, value, Invalidation.Measure);
    } = true;

    /// <summary>Gets or sets non-negative padding applied to every header and data cell.</summary>
    /// <exception cref="InvalidOperationException">The attached table is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The table is disposed.</exception>
    public Thickness CellPadding
    {
        get;
        set => _ = Set(ref field, value, Invalidation.Measure);
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
            _ = Set(ref field, value, Invalidation.Measure);
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
            _ = Set(ref field, value, Invalidation.Measure);
        }
    }

    /// <summary>Gets or sets whether one-cell light grid lines are drawn in every available table gap.</summary>
    /// <exception cref="InvalidOperationException">The attached table is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The table is disposed.</exception>
    public bool ShowGridLines
    {
        get;
        set => _ = Set(ref field, value, Invalidation.Measure);
    } = true;

    /// <summary>Gets or sets an optional direct foreground override for header text.</summary>
    /// <exception cref="InvalidOperationException">The attached table is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The table is disposed.</exception>
    public Color? HeaderForeground
    {
        get;
        set => _ = Set(ref field, value, Invalidation.Render);
    }

    /// <summary>Gets or sets an optional direct background override for the complete header row.</summary>
    /// <exception cref="InvalidOperationException">The attached table is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The table is disposed.</exception>
    public Color? HeaderBackground
    {
        get;
        set => _ = Set(ref field, value, Invalidation.Render);
    }

    /// <summary>Gets or sets an optional direct foreground override for grid lines.</summary>
    /// <exception cref="InvalidOperationException">The attached table is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The table is disposed.</exception>
    public Color? GridLineColor
    {
        get;
        set => _ = Set(ref field, value, Invalidation.Render);
    }

    #endregion

    #region Layout and rendering

    /// <inheritdoc/>
    protected override Size MeasureCore(Constraint constraint)
    {
        MeasureCells(constraint.Width);
        var width = Add(Sum(_columnWidths), GapWidth(Columns.Count));
        var height = Add(Sum(_rowHeights), GapHeight(Rows.Count));

        if (ShowHeader && Columns.Count > 0)
        {
            height = Add(height, Add(CellPadding.Vertical, 1));

            if (Rows.Count > 0)
            {
                height = Add(height, RowGap);
            }
        }

        return new Size(width, height);
    }

    /// <inheritdoc/>
    protected override void ArrangeCore(Rect bounds)
    {
        MeasureCells(bounds.Width);
        var y = bounds.Y;

        if (ShowHeader && Columns.Count > 0)
        {
            y = Add(y, Add(CellPadding.Vertical, 1));

            if (Rows.Count > 0)
            {
                y = Add(y, RowGap);
            }
        }

        for (var rowIndex = 0; rowIndex < Rows.Count; rowIndex++)
        {
            var row = Rows[rowIndex];
            var x = bounds.X;

            for (var columnIndex = 0; columnIndex < Columns.Count; columnIndex++)
            {
                var slot = new Rect(x, y, _columnWidths[columnIndex], _rowHeights[rowIndex]);
                row.Cells[columnIndex].Arrange(CellPadding.Deflate(slot), widthResolved: true, heightResolved: true);
                x = Add(x, Add(_columnWidths[columnIndex], ColumnGap));
            }

            y = Add(y, Add(_rowHeights[rowIndex], RowGap));
        }
    }

    /// <inheritdoc/>
    protected override void RenderCore(TerminalCanvas canvas)
    {
        if (Columns.Count == 0 || Bounds.Width == 0 || Bounds.Height == 0)
        {
            return;
        }

        var inherited = ResolvedStyle;
        var (attributes, underline, underlineColor) = Decoration.Resolve(inherited);
        var header = new TerminalStyle(
            HeaderForeground ?? inherited.Foreground,
            HeaderBackground ?? inherited.Background,
            attributes,
            inherited.Hyperlink,
            underline,
            underlineColor);
        var grid = new TerminalStyle(
            GridLineColor ?? inherited.Foreground,
            inherited.Background,
            attributes,
            inherited.Hyperlink,
            underline,
            underlineColor);
        var headerHeight = ShowHeader ? Add(CellPadding.Vertical, 1) : 0;

        if (ShowHeader)
        {
            if (HeaderBackground.HasValue || ControlAppearance.HasOpaqueFill(this, GetVisualState()))
            {
                canvas.Clear(new Rect(Bounds.X, Bounds.Y, Bounds.Width, headerHeight), header);
            }

            var x = Bounds.X;

            for (var index = 0; index < Columns.Count; index++)
            {
                var area = new Rect(x, Bounds.Y + CellPadding.Top, _columnWidths[index], 1);
                var text = canvas.Clip(CellPadding.Deflate(area));
                _ = text.Draw(Columns[index].Header.AsSpan(), new Point(area.X + CellPadding.Left, area.Y), header, background: BackgroundMode.Transparent);
                x = Add(x, Add(_columnWidths[index], ColumnGap));
            }
        }

        if (!ShowGridLines)
        {
            return;
        }

        var xLine = Bounds.X;

        for (var index = 0; index < Columns.Count - 1; index++)
        {
            xLine = Add(xLine, _columnWidths[index]);
            canvas.DrawVerticalLine(new Point(xLine, Bounds.Y), Bounds.Height, LineStyle.Light, grid);
            xLine = Add(xLine, ColumnGap);
        }

        if (ShowHeader && Rows.Count > 0 && RowGap > 0)
        {
            // Canvas coordinates are absolute. A table may be arranged inside
            // any offset parent, so the divider must include the table origin.
            canvas.DrawHorizontalLine(
                new Point(Bounds.X, Add(Bounds.Y, headerHeight)),
                Bounds.Width,
                LineStyle.Light,
                grid);
        }

        var y = Add(Bounds.Y, headerHeight + (ShowHeader ? RowGap : 0));

        for (var index = 0; index < Rows.Count - 1; index++)
        {
            y = Add(y, _rowHeights[index]);
            canvas.DrawHorizontalLine(new Point(Bounds.X, y), Bounds.Width, LineStyle.Light, grid);
            y = Add(y, RowGap);
        }
    }

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
            Children.Add(cell);
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
            _ = Children.Remove(cell);
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
            _ = Children.Remove(cell);
        }

        foreach (var cell in row.Cells)
        {
            Children.Add(cell);
        }

        owner.ReplaceAttached(index, row);
        Invalidate(Invalidation.Measure);
    }

    #endregion

    #region Track resolution and validation

    private int ColumnGap => Math.Max(ColumnSpacing, ShowGridLines ? 1 : 0);

    private int RowGap => Math.Max(RowSpacing, ShowGridLines ? 1 : 0);

    private void MeasureCells(int? availableWidth)
    {
        if (Columns.Count == 0)
        {
            _columnWidths = [];
            _rowHeights = [];
            return;
        }

        var automatic = new int[Columns.Count];
        var lengths = new Length[Columns.Count];

        for (var columnIndex = 0; columnIndex < Columns.Count; columnIndex++)
        {
            lengths[columnIndex] = Columns[columnIndex].Width;
            automatic[columnIndex] = Add(Terminal.Unicode.Width.Measure(Columns[columnIndex].Header).Cells, CellPadding.Horizontal);
        }

        foreach (var row in Rows)
        {
            for (var columnIndex = 0; columnIndex < Columns.Count; columnIndex++)
            {
                var cell = row.Cells[columnIndex];
                cell.Measure(new Constraint(width: null, height: null));
                automatic[columnIndex] = Math.Max(automatic[columnIndex], Add(cell.DesiredSize.Width, Add(cell.Margin.Horizontal, CellPadding.Horizontal)));
            }
        }

        int? available = availableWidth.HasValue
            ? Math.Max(0, availableWidth.Value - GapWidth(Columns.Count))
            : null;
        _columnWidths = Tracks.Resolve(available, lengths, automatic);
        _rowHeights = new int[Rows.Count];

        for (var rowIndex = 0; rowIndex < Rows.Count; rowIndex++)
        {
            var height = 0;

            for (var columnIndex = 0; columnIndex < Columns.Count; columnIndex++)
            {
                var cell = Rows[rowIndex].Cells[columnIndex];
                var width = Math.Max(0, _columnWidths[columnIndex] - CellPadding.Horizontal);
                cell.Measure(new Constraint(width, height: null));
                height = Math.Max(height, Add(cell.DesiredSize.Height, Add(cell.Margin.Vertical, CellPadding.Vertical)));
            }

            _rowHeights[rowIndex] = height;
        }
    }

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

    private int GapWidth(int count) => count < 2 ? 0 : Multiply(ColumnGap, count - 1);

    private int GapHeight(int count) => count < 2 ? 0 : Multiply(RowGap, count - 1);

    private static int Sum(IEnumerable<int> values)
    {
        var total = 0;

        foreach (var value in values)
        {
            total = Add(total, value);
        }

        return total;
    }

    private static int Add(int left, int right) =>
        (int) Math.Min(int.MaxValue, (long) left + right);

    private static int Multiply(int value, int count) =>
        (int) Math.Min(int.MaxValue, (long) value * count);

    #endregion
}
