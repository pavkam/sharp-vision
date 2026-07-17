// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using SharpVision.Terminal.Rendering;

/// <summary>Owns the private cell realization and scrolling geometry of one <see cref="Table"/>.</summary>
/// <remarks>
/// The presenter is deliberately a true <see cref="Container"/>: it owns arbitrary realized cell
/// controls and provides their scrolling viewport. Its <see cref="Table"/> owner remains a semantic
/// item control and exposes rows and columns rather than a bypassable child collection.
/// </remarks>
internal sealed class TablePresenter: Container
{
    private readonly Table _owner;
    private int? _measuredWidth;
    private bool _hasMeasuredWidth;

    /// <summary>Initializes a private presenter for one non-null table owner.</summary>
    /// <param name="owner">The table whose row and column definitions drive this presenter.</param>
    /// <exception cref="ArgumentNullException"><paramref name="owner"/> is null.</exception>
    internal TablePresenter(Table owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _owner = owner;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;
        AutoScroll = true;
        ScrollBars = ScrollBars.Vertical;
    }

    /// <summary>Gets the currently resolved cell width for each semantic column.</summary>
    internal int[] ColumnWidths { get; private set; } = [];

    /// <summary>Gets the currently resolved cell height for each semantic row.</summary>
    internal int[] RowHeights { get; private set; } = [];

    private Rect ContentSlot { get; set; }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        MeasureCells(constraint.Width);
        var width = Add(Sum(ColumnWidths), GapWidth(_owner.Columns.Count));
        var height = Add(Sum(RowHeights), GapHeight(_owner.Rows.Count));

        if (_owner.ShowHeader && _owner.Columns.Count > 0)
        {
            height = Add(height, Add(_owner.CellPadding.Vertical, 1));

            if (_owner.Rows.Count > 0)
            {
                height = Add(height, RowGap);
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

        if (_owner.ShowHeader && _owner.Columns.Count > 0)
        {
            y = Add(y, Add(_owner.CellPadding.Vertical, 1));

            if (_owner.Rows.Count > 0)
            {
                y = Add(y, RowGap);
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
                x = Add(x, Add(ColumnWidths[columnIndex], ColumnGap));
            }

            y = Add(y, Add(RowHeights[rowIndex], RowGap));
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
    internal void RenderTableChrome(TerminalCanvas canvas)
    {
        if (_owner.Columns.Count == 0 || ContentSlot.Width == 0 || ContentSlot.Height == 0)
        {
            return;
        }

        var inherited = _owner.ResolvedStyle;
        (var attributes, var underline, var underlineColor) = Decoration.Resolve(inherited);
        var header = new TerminalStyle(
            _owner.HeaderForeground is { } headerForeground ? _owner.ResolveThemeColor(headerForeground) : inherited.Foreground,
            _owner.HeaderBackground is { } headerBackground ? _owner.ResolveThemeColor(headerBackground) : inherited.Background,
            attributes,
            inherited.Hyperlink,
            underline,
            underlineColor);
        var grid = new TerminalStyle(
            _owner.GridLineColor is { } gridLineColor ? _owner.ResolveThemeColor(gridLineColor) : inherited.Foreground,
            inherited.Background,
            attributes,
            inherited.Hyperlink,
            underline,
            underlineColor);
        var headerHeight = _owner.ShowHeader ? Add(_owner.CellPadding.Vertical, 1) : 0;

        if (_owner.ShowHeader)
        {
            if (_owner.HeaderBackground.HasValue || ControlAppearance.HasOpaqueFill(_owner, _owner.CurrentVisualState))
            {
                canvas.Clear(new Rect(ContentSlot.X, ContentSlot.Y, ContentSlot.Width, headerHeight), header);
            }

            var x = ContentSlot.X;

            for (var index = 0; index < _owner.Columns.Count; index++)
            {
                var area = new Rect(x, ContentSlot.Y + _owner.CellPadding.Top, ColumnWidths[index], 1);
                var text = canvas.Clip(_owner.CellPadding.Deflate(area));
                _ = text.Draw(
                    _owner.Columns[index].Header.AsSpan(),
                    new Point(area.X + _owner.CellPadding.Left, area.Y),
                    header,
                    background: BackgroundMode.Transparent);
                x = Add(x, Add(ColumnWidths[index], ColumnGap));
            }
        }

        if (!_owner.ShowGridLines)
        {
            return;
        }

        var xLine = ContentSlot.X;

        for (var index = 0; index < _owner.Columns.Count - 1; index++)
        {
            xLine = Add(xLine, ColumnWidths[index]);
            canvas.DrawVerticalLine(new Point(xLine, ContentSlot.Y), ContentSlot.Height, LineStyle.Light, grid);
            xLine = Add(xLine, ColumnGap);
        }

        if (_owner.ShowHeader && _owner.Rows.Count > 0 && RowGap > 0)
        {
            canvas.DrawHorizontalLine(
                new Point(ContentSlot.X, Add(ContentSlot.Y, headerHeight)),
                ContentSlot.Width,
                LineStyle.Light,
                grid);
        }

        var y = Add(ContentSlot.Y, headerHeight + (_owner.ShowHeader ? RowGap : 0));

        for (var index = 0; index < _owner.Rows.Count - 1; index++)
        {
            y = Add(y, RowHeights[index]);
            canvas.DrawHorizontalLine(new Point(ContentSlot.X, y), ContentSlot.Width, LineStyle.Light, grid);
            y = Add(y, RowGap);
        }
    }

    private int ColumnGap => Math.Max(_owner.ColumnSpacing, _owner.ShowGridLines ? 1 : 0);

    private int RowGap => Math.Max(_owner.RowSpacing, _owner.ShowGridLines ? 1 : 0);

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
            automatic[columnIndex] = Add(
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
                    Add(cell.DesiredSize.Width, Add(cell.Margin.Horizontal, _owner.CellPadding.Horizontal)));
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
                    Add(cell.DesiredSize.Height, Add(cell.Margin.Vertical, _owner.CellPadding.Vertical)));
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
            total = Add(total, value);
        }

        return total;
    }

    private static int Add(int left, int right) => (int) Math.Min(int.MaxValue, (long) left + right);

    private static int Multiply(int value, int count) => (int) Math.Min(int.MaxValue, (long) value * count);
}
