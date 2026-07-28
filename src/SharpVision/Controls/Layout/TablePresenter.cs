// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Layout;

using Terminal.Rendering;

/// <summary>Owns the private cell realization and scrolling geometry of one <see cref="Table"/>.</summary>
/// <remarks>
/// The presenter is deliberately a true <see cref="Container"/>: it owns arbitrary realized cell
/// controls and provides their scrolling viewport. Its <see cref="Table"/> owner remains a semantic
/// item control and exposes rows and columns rather than a bypassable child collection.
/// </remarks>
internal sealed class TablePresenter: Container
{
    // A header label and a grid separator each occupy one terminal cell.
    private const int _headerTextHeight = 1;
    private const int _gridLineThickness = 1;

    private readonly Table _owner;
    private int? _measuredWidth;
    private bool _hasMeasuredWidth;

    /// <summary>Initializes a private presenter for one non-null table owner.</summary>
    /// <param name="owner">The table whose row and column definitions drive this presenter.</param>
    /// <exception cref="ArgumentNullException"><paramref name="owner"/> is null.</exception>
    public TablePresenter(Table owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _owner = owner;
        Face = new Face(
            ThemeColor.ControlText,
            Color.Transparent,
            ThemeDecoration.NormalText,
            Underline.None,
            Color.Default);
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;
        AutoScroll = true;
        ScrollBars = ScrollBars.Vertical;
    }

    /// <summary>Gets the currently resolved cell width for each semantic column.</summary>
    public int[] ColumnWidths { get; private set; } = [];

    /// <summary>Gets the currently resolved cell height for each semantic row.</summary>
    public int[] RowHeights { get; private set; } = [];

    /// <summary>Resolves a screen point to one arranged data cell.</summary>
    /// <param name="point">The absolute terminal-cell point.</param>
    /// <param name="row">The resolved display row.</param>
    /// <param name="columnIndex">The resolved zero-based column index.</param>
    /// <returns>True when the point is inside an arranged cell.</returns>
    internal bool TryGetCell(Point point, out TableRow row, out int columnIndex)
    {
        var y = ContentSlot.Y;

        if (_owner.ShowHeader && _owner.Columns.Count > 0)
        {
            y = LayoutMath.Add(y, LayoutMath.Add(_owner.CellPadding.Vertical, _headerTextHeight));

            if (_owner.Rows.Count > 0)
            {
                y = LayoutMath.Add(y, RowGap);
            }
        }

        for (var rowIndex = 0; rowIndex < _owner.Rows.Count; rowIndex++)
        {
            var candidate = _owner.Rows[rowIndex];

            if (point.Y >= y && point.Y < y + RowHeights[rowIndex])
            {
                var x = ContentSlot.X;

                for (var column = 0; column < candidate.Cells.Count; column++)
                {
                    if (point.X >= x && point.X < x + ColumnWidths[column])
                    {
                        row = candidate;
                        columnIndex = column;
                        return true;
                    }

                    x = LayoutMath.Add(x, LayoutMath.Add(ColumnWidths[column], ColumnGap));
                }
            }

            y = LayoutMath.Add(y, LayoutMath.Add(RowHeights[rowIndex], RowGap));
        }

        for (var rowIndex = 0; rowIndex < _owner.Rows.Count; rowIndex++)
        {
            var candidate = _owner.Rows[rowIndex];

            for (var column = 0; column < candidate.Cells.Count; column++)
            {
                if (candidate.Cells[column].Bounds.Contains(point))
                {
                    row = candidate;
                    columnIndex = column;
                    return true;
                }
            }
        }

        row = null!;
        columnIndex = -1;
        return false;
    }

    /// <summary>Resolves a screen point to a visible header column.</summary>
    /// <param name="point">The absolute terminal-cell point.</param>
    /// <param name="columnIndex">The resolved zero-based column index.</param>
    /// <returns>True when the point is inside the header's content area.</returns>
    internal bool TryGetHeaderColumn(Point point, out int columnIndex)
    {
        columnIndex = -1;

        if (!_owner.ShowHeader || _owner.Columns.Count == 0 || !ContentSlot.Contains(point))
        {
            return false;
        }

        var headerHeight = LayoutMath.Add(_owner.CellPadding.Vertical, _headerTextHeight);

        if (point.Y < ContentSlot.Y || point.Y >= ContentSlot.Y + headerHeight)
        {
            return false;
        }

        var x = ContentSlot.X;

        for (var index = 0; index < _owner.Columns.Count; index++)
        {
            if (point.X >= x && point.X < x + ColumnWidths[index])
            {
                columnIndex = index;
                return true;
            }

            x = LayoutMath.Add(x, LayoutMath.Add(ColumnWidths[index], ColumnGap));
        }

        return false;
    }

    private Rect ContentSlot { get; set; }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        MeasureCells(constraint.Width);
        var width = LayoutMath.Add(Sum(ColumnWidths), GapWidth(_owner.Columns.Count));
        var height = LayoutMath.Add(Sum(RowHeights), GapHeight(_owner.Rows.Count));

        if (_owner is { ShowHeader: true, Columns.Count: > 0 })
        {
            height = LayoutMath.Add(height, LayoutMath.Add(_owner.CellPadding.Vertical, _headerTextHeight));

            if (_owner.Rows.Count > 0)
            {
                height = LayoutMath.Add(height, RowGap);
            }
        }

        return new Size(width, height);
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds)
    {
        ContentSlot = bounds;

        // A scroll or focus transition invalidates arrangement without
        // invalidating measurement. Repeating the unbounded/bounded cell probes
        // in that path would make each child measurement re-invalidate this
        // presenter while it is arranging, producing an endless frame loop.
        // Resize can supply a genuinely different final width, so only that
        // width transition earns one final constrained measurement pass.
        if (!_hasMeasuredWidth || _measuredWidth != bounds.Width)
        {
            MeasureCells(bounds.Width);
        }

        var y = bounds.Y;

        if (_owner is { ShowHeader: true, Columns.Count: > 0 })
        {
            y = LayoutMath.Add(y, LayoutMath.Add(_owner.CellPadding.Vertical, _headerTextHeight));

            if (_owner.Rows.Count > 0)
            {
                y = LayoutMath.Add(y, RowGap);
            }
        }

        for (var rowIndex = 0; rowIndex < _owner.Rows.Count; rowIndex++)
        {
            var row = _owner.Rows[rowIndex];
            var x = bounds.X;

            for (var columnIndex = 0; columnIndex < _owner.Columns.Count; columnIndex++)
            {
                var slot = new Rect(x, y, ColumnWidths[columnIndex], RowHeights[rowIndex]);
                ArrangeChild(row.Cells[columnIndex], _owner.CellPadding.Deflate(slot));
                x = LayoutMath.Add(x, LayoutMath.Add(ColumnWidths[columnIndex], ColumnGap));
            }

            y = LayoutMath.Add(y, LayoutMath.Add(RowHeights[rowIndex], RowGap));
        }
    }

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas)
    {
        // Table.OnRender draws headers and grid lines on the table canvas first.
        // The presenter must not clear the body with an opaque fill, which would
        // overwrite that chrome. Skip the base chrome entirely; scrollbar framework
        // parts render independently as owned children.
    }

    /// <summary>Renders the table header and grid before ordinary cell content is rendered by the shared owner path.</summary>
    /// <param name="canvas">The clipped frame canvas.</param>
    public void RenderTableChrome(TerminalCanvas canvas)
    {
        if (_owner.Columns.Count == 0 || ContentSlot.Width == 0 || ContentSlot.Height == 0)
        {
            return;
        }

        var inherited = _owner.ResolvedStyle;
        var (attributes, underline, underlineColor) = DecorationResolver.Resolve(inherited);
        var header = new TerminalStyle(
            _owner.HeaderForeground ?? inherited.Foreground,
            _owner.HeaderBackground ?? inherited.Background,
            attributes,
            inherited.Hyperlink,
            underline,
            underlineColor);
        var grid = new TerminalStyle(
            _owner.GridLineColor ?? inherited.Foreground,
            inherited.Background,
            attributes,
            inherited.Hyperlink,
            underline,
            underlineColor);
        var horizontalGlyph = _owner.ResolvedHorizontalGridGlyph;
        var verticalGlyph = _owner.ResolvedVerticalGridGlyph;
        var crossGlyph = _owner.ResolvedCrossGridGlyph;
        var headerHeight = _owner.ShowHeader ? LayoutMath.Add(_owner.CellPadding.Vertical, _headerTextHeight) : 0;

        if (_owner.ShowHeader)
        {
            if (_owner.HeaderBackground.HasValue || ControlAppearance.HasOpaqueFill(_owner, _owner.CurrentVisualState))
            {
                canvas.Clear(new Rect(ContentSlot.X, ContentSlot.Y, ContentSlot.Width, headerHeight), header);
            }

            var x = ContentSlot.X;

            for (var index = 0; index < _owner.Columns.Count; index++)
            {
                var area = new Rect(x, ContentSlot.Y + _owner.CellPadding.Top, ColumnWidths[index], _headerTextHeight);
                var text = canvas.Clip(_owner.CellPadding.Deflate(area));
                _ = text.Draw(
                    _owner.Columns[index].Header.AsSpan(),
                    new Point(area.X + _owner.CellPadding.Left, area.Y),
                    header,
                    background: BackgroundMode.Transparent);
                x = LayoutMath.Add(x, LayoutMath.Add(ColumnWidths[index], ColumnGap));
            }
        }

        if (!_owner.ShowGridLines)
        {
            return;
        }

        var xLine = ContentSlot.X;

        for (var index = 0; index < _owner.Columns.Count - 1; index++)
        {
            xLine = LayoutMath.Add(xLine, ColumnWidths[index]);
            DrawVerticalGridLine(canvas, xLine, verticalGlyph, grid);
            xLine = LayoutMath.Add(xLine, ColumnGap);
        }

        if (_owner is { ShowHeader: true, Rows.Count: > 0 } && RowGap > 0)
        {
            DrawHorizontalGridLine(
                canvas,
                LayoutMath.Add(ContentSlot.Y, headerHeight),
                horizontalGlyph,
                crossGlyph,
                grid);
        }

        var y = LayoutMath.Add(ContentSlot.Y, headerHeight + (_owner.ShowHeader ? RowGap : 0));

        for (var index = 0; index < _owner.Rows.Count - 1; index++)
        {
            y = LayoutMath.Add(y, RowHeights[index]);
            DrawHorizontalGridLine(canvas, y, horizontalGlyph, crossGlyph, grid);
            y = LayoutMath.Add(y, RowGap);
        }
    }

    private int ColumnGap => Math.Max(_owner.ColumnSpacing, _owner.ShowGridLines ? _gridLineThickness : 0);

    private int RowGap => Math.Max(_owner.RowSpacing, _owner.ShowGridLines ? _gridLineThickness : 0);

    private void DrawVerticalGridLine(
        TerminalCanvas canvas,
        int x,
        Rune glyph,
        TerminalStyle style)
    {
        for (var y = ContentSlot.Y; y < ContentSlot.Bottom; y++)
        {
            canvas.DrawRune(glyph, new Point(x, y), style, BackgroundMode.Transparent);
        }
    }

    private void DrawHorizontalGridLine(
        TerminalCanvas canvas,
        int y,
        Rune horizontal,
        Rune cross,
        TerminalStyle style)
    {
        for (var x = ContentSlot.X; x < ContentSlot.Right; x++)
        {
            canvas.DrawRune(
                IsColumnSeparator(x) ? cross : horizontal,
                new Point(x, y),
                style,
                BackgroundMode.Transparent);
        }
    }

    private bool IsColumnSeparator(int x)
    {
        var line = ContentSlot.X;

        for (var index = 0; index < _owner.Columns.Count - 1; index++)
        {
            line = LayoutMath.Add(line, ColumnWidths[index]);

            if (line == x)
            {
                return true;
            }

            line = LayoutMath.Add(line, ColumnGap);
        }

        return false;
    }

    private void MeasureCells(int? availableWidth)
    {
        _measuredWidth = availableWidth;
        _hasMeasuredWidth = true;

        if (_owner.Columns.Count == 0)
        {
            ColumnWidths = [];
            RowHeights = [];
            return;
        }

        var automatic = new int[_owner.Columns.Count];
        var lengths = new Length[_owner.Columns.Count];

        for (var columnIndex = 0; columnIndex < _owner.Columns.Count; columnIndex++)
        {
            lengths[columnIndex] = _owner.Columns[columnIndex].Width;
            automatic[columnIndex] = LayoutMath.Add(
                Terminal.Unicode.Width.Measure(_owner.Columns[columnIndex].Header).Cells,
                _owner.CellPadding.Horizontal);
        }

        foreach (var row in _owner.Rows)
        {
            for (var columnIndex = 0; columnIndex < _owner.Columns.Count; columnIndex++)
            {
                var cell = row.Cells[columnIndex];
                _ = MeasureChild(cell, new Constraint(width: null, height: null));
                automatic[columnIndex] = Math.Max(
                    automatic[columnIndex],
                    LayoutMath.Add(cell.DesiredSize.Width, LayoutMath.Add(cell.Margin.Horizontal, _owner.CellPadding.Horizontal)));
            }
        }

        int? available = availableWidth.HasValue
            ? Math.Max(0, availableWidth.Value - GapWidth(_owner.Columns.Count))
            : null;
        ColumnWidths = Tracks.Resolve(available, lengths, automatic);
        RowHeights = new int[_owner.Rows.Count];

        for (var rowIndex = 0; rowIndex < _owner.Rows.Count; rowIndex++)
        {
            var height = 0;

            for (var columnIndex = 0; columnIndex < _owner.Columns.Count; columnIndex++)
            {
                var cell = _owner.Rows[rowIndex].Cells[columnIndex];
                var width = Math.Max(0, ColumnWidths[columnIndex] - _owner.CellPadding.Horizontal);
                _ = MeasureChild(cell, new Constraint(width, height: null));
                height = Math.Max(
                    height,
                    LayoutMath.Add(cell.DesiredSize.Height, LayoutMath.Add(cell.Margin.Vertical, _owner.CellPadding.Vertical)));
            }

            RowHeights[rowIndex] = height;
        }
    }

    private int GapWidth(int count) => count < 2 ? 0 : Multiply(ColumnGap, count - 1);

    private int GapHeight(int count) => count < 2 ? 0 : Multiply(RowGap, count - 1);

    private static int Sum(IEnumerable<int> values)
    {
        var total = 0;

        foreach (var value in values)
        {
            total = LayoutMath.Add(total, value);
        }

        return total;
    }

    private static int Multiply(int value, int count) => (int) Math.Min(int.MaxValue, (long) value * count);
}
