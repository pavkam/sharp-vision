// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using SharpVision.Styling;
using SharpVision.Terminal.Protocols;
using SharpVision.Terminal.Rendering;

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

    /// <summary>Identifies the themeable header-text foreground style property.</summary>
    public static StyleProperty<Color?> HeaderForegroundProperty { get; } =
        StyleProperty<Color?>.Register<Table>("header-foreground", null, Impact.Render);

    /// <summary>Identifies the themeable header-row background style property.</summary>
    public static StyleProperty<Color?> HeaderBackgroundProperty { get; } =
        StyleProperty<Color?>.Register<Table>("header-background", null, Impact.Render);

    /// <summary>Identifies the themeable grid-line color style property.</summary>
    public static StyleProperty<Color?> GridLineColorProperty { get; } =
        StyleProperty<Color?>.Register<Table>("grid-line-color", null, Impact.Render);

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

    #endregion

    #region Layout and rendering

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        MeasureCells(constraint.Width);
        int width = Add(Sum(_columnWidths), GapWidth(Columns.Count));
        int height = Add(Sum(_rowHeights), GapHeight(Rows.Count));

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
    protected override void ArrangeOverride(Rect bounds)
    {
        MeasureCells(bounds.Width);
        int y = bounds.Y;

        if (ShowHeader && Columns.Count > 0)
        {
            y = Add(y, Add(CellPadding.Vertical, 1));

            if (Rows.Count > 0)
            {
                y = Add(y, RowGap);
            }
        }

        for (int rowIndex = 0; rowIndex < Rows.Count; rowIndex++)
        {
            TableRow row = Rows[rowIndex];
            int x = bounds.X;

            for (int columnIndex = 0; columnIndex < Columns.Count; columnIndex++)
            {
                Rect slot = new(x, y, _columnWidths[columnIndex], _rowHeights[rowIndex]);
                row.Cells[columnIndex].Arrange(CellPadding.Deflate(slot), widthResolved: true, heightResolved: true);
                x = Add(x, Add(_columnWidths[columnIndex], ColumnGap));
            }

            y = Add(y, Add(_rowHeights[rowIndex], RowGap));
        }
    }

    /// <inheritdoc/>
    protected override void OnRender(TerminalCanvas canvas)
    {
        if (Columns.Count == 0 || Bounds.Width == 0 || Bounds.Height == 0)
        {
            return;
        }

        TerminalStyle inherited = ResolvedStyle;
        (TerminalAttributes attributes, Underline underline, Color underlineColor) = Decoration.Resolve(inherited);
        TerminalStyle header = new(
            HeaderForeground ?? inherited.Foreground,
            HeaderBackground ?? inherited.Background,
            attributes,
            inherited.Hyperlink,
            underline,
            underlineColor);
        TerminalStyle grid = new(
            GridLineColor ?? inherited.Foreground,
            inherited.Background,
            attributes,
            inherited.Hyperlink,
            underline,
            underlineColor);
        int headerHeight = ShowHeader ? Add(CellPadding.Vertical, 1) : 0;

        if (ShowHeader)
        {
            if (HeaderBackground.HasValue || ControlAppearance.HasOpaqueFill(this, GetVisualState()))
            {
                canvas.Clear(new Rect(Bounds.X, Bounds.Y, Bounds.Width, headerHeight), header);
            }

            int x = Bounds.X;

            for (int index = 0; index < Columns.Count; index++)
            {
                Rect area = new(x, Bounds.Y + CellPadding.Top, _columnWidths[index], 1);
                TerminalCanvas text = canvas.Clip(CellPadding.Deflate(area));
                _ = text.Draw(Columns[index].Header.AsSpan(), new Point(area.X + CellPadding.Left, area.Y), header, background: BackgroundMode.Transparent);
                x = Add(x, Add(_columnWidths[index], ColumnGap));
            }
        }

        if (!ShowGridLines)
        {
            return;
        }

        int xLine = Bounds.X;

        for (int index = 0; index < Columns.Count - 1; index++)
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

        int y = Add(Bounds.Y, headerHeight + (ShowHeader ? RowGap : 0));

        for (int index = 0; index < Rows.Count - 1; index++)
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

        foreach (Control cell in row.Cells)
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
        TableRow row = Rows[index];

        foreach (Control cell in row.Cells)
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

        for (int index = Rows.Count - 1; index >= 0; index--)
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
        TableRow previous = Rows[index];

        foreach (Control cell in previous.Cells)
        {
            _ = Children.Remove(cell);
        }

        foreach (Control cell in row.Cells)
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

        int[] automatic = new int[Columns.Count];
        Length[] lengths = new Length[Columns.Count];

        for (int columnIndex = 0; columnIndex < Columns.Count; columnIndex++)
        {
            lengths[columnIndex] = Columns[columnIndex].Width;
            automatic[columnIndex] = Add(Terminal.Unicode.Width.Measure(Columns[columnIndex].Header).Cells, CellPadding.Horizontal);
        }

        foreach (TableRow row in Rows)
        {
            for (int columnIndex = 0; columnIndex < Columns.Count; columnIndex++)
            {
                Control cell = row.Cells[columnIndex];
                cell.Measure(new Constraint(width: null, height: null));
                automatic[columnIndex] = Math.Max(automatic[columnIndex], Add(cell.DesiredSize.Width, Add(cell.Margin.Horizontal, CellPadding.Horizontal)));
            }
        }

        int? available = availableWidth.HasValue
            ? Math.Max(0, availableWidth.Value - GapWidth(Columns.Count))
            : null;
        _columnWidths = Tracks.Resolve(available, lengths, automatic);
        _rowHeights = new int[Rows.Count];

        for (int rowIndex = 0; rowIndex < Rows.Count; rowIndex++)
        {
            int height = 0;

            for (int columnIndex = 0; columnIndex < Columns.Count; columnIndex++)
            {
                Control cell = Rows[rowIndex].Cells[columnIndex];
                int width = Math.Max(0, _columnWidths[columnIndex] - CellPadding.Horizontal);
                cell.Measure(new Constraint(width, height: null));
                height = Math.Max(height, Add(cell.DesiredSize.Height, Add(cell.Margin.Vertical, CellPadding.Vertical)));
            }

            _rowHeights[rowIndex] = height;
        }

        Debug.Assert(_columnWidths.Length == Columns.Count, "Every column must resolve to one width.");
        Debug.Assert(_rowHeights.Length == Rows.Count, "Every row must resolve to one height.");
    }

    private void ValidateRow(TableRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        VerifyMutable();

        if (Columns.Count == 0 || row.Cells.Count != Columns.Count)
        {
            throw new ArgumentException("Every row requires exactly one cell per defined column.", nameof(row));
        }

        foreach (Control cell in row.Cells)
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

    private int GapWidth(int count)
    {
        Debug.Assert(count >= 0, "Table column gap count is non-negative.");

        return count < 2 ? 0 : Multiply(ColumnGap, count - 1);
    }

    private int GapHeight(int count)
    {
        Debug.Assert(count >= 0, "Table row gap count is non-negative.");

        return count < 2 ? 0 : Multiply(RowGap, count - 1);
    }

    private static int Sum(IEnumerable<int> values)
    {
        Debug.Assert(values is not null, "Table sum requires a non-null sequence.");

        int total = 0;

        foreach (int value in values)
        {
            total = Add(total, value);
        }

        return total;
    }

    private static int Add(int left, int right)
    {
        Debug.Assert(left >= 0, "Table accumulation uses non-negative extents.");
        Debug.Assert(right >= 0, "Table accumulation uses non-negative extents.");

        return (int) Math.Min(int.MaxValue, (long) left + right);
    }

    private static int Multiply(int value, int count)
    {
        Debug.Assert(value >= 0, "Table multiplication value is non-negative.");
        Debug.Assert(count >= 0, "Table multiplication count is non-negative.");

        return (int) Math.Min(int.MaxValue, (long) value * count);
    }

    #endregion
}
