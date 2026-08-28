// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Layout;

using Terminal.Rendering;

using NonNegativeValue = JetBrains.Annotations.NonNegativeValueAttribute;

/// <summary>Owns the private cell realization and scrolling geometry of one <see cref="Table"/>.</summary>
/// <remarks>
/// The presenter is deliberately a true <see cref="Container"/>: it owns arbitrary realized cell
/// controls and provides their scrolling viewport. Its <see cref="Table"/> owner remains a semantic
/// item control and exposes rows and columns rather than a bypassable child collection.
/// </remarks>
internal sealed class TablePresenter: Container, IOwnedChildDisposalObserver
{
    // A header label and a grid separator each occupy one terminal cell.
    private const int _headerTextHeight = 1;
    private const int _gridLineThickness = 1;

    // Reserved uniformly on every column's own automatic header request, not only the sorted
    // one, so the sorted column can move between columns without any column's measured width
    // changing. Reserving it only on the sorted column would make every click that moves the
    // sort to a new column also resize that column's header, jittering the whole row.
    private const int _sortIndicatorWidth = 1;

    // A complete constructor Face outranks every theme state contribution and disables ambient
    // inheritance, permanently opting this presenter out of visual-state feedback. Only the
    // transparent background is presenter-specific; the rest matches the ControlBase role's own
    // normal defaults, so a partial FaceOverlay contribution keeps state behavior alive.
    private static readonly AppearanceStatesOverlay _presenterAppearance = new(
        normal: new AppearanceOverlay(
            face: new FaceOverlay(
                foreground: SemanticColor.ControlText,
                background: Color.Transparent,
                attributes: SemanticDecoration.NormalText,
                underline: Underline.None,
                underlineColor: Color.Default)));

    private readonly Table _owner;
    private int? _measuredWidth;
    private bool _hasMeasuredWidth;

    /// <summary>Initializes a private presenter for one non-null table owner.</summary>
    /// <param name="owner">The table whose row and column definitions drive this presenter.</param>
    /// <exception cref="ArgumentNullException"><paramref name="owner"/> is null.</exception>
    public TablePresenter(Table owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        InitializeAppearanceOverlay(_presenterAppearance);
        _owner = owner;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;
        AutoScroll = true;
        ScrollBars = ScrollBars.Vertical;
    }

    /// <summary>Gets the currently resolved cell width for each semantic column.</summary>
    public int[] ColumnWidths { get; private set; } = [];

    /// <summary>Gets the currently resolved cell height for each semantic row.</summary>
    public int[] RowHeights { get; private set; } = [];

    /// <summary>Gets the stable, un-offset-shifted content origin - the progressive counterpart to
    /// <c>ListViewHost.RowOrigin</c>. Row Y for logical index <c>i</c> is
    /// <c>ProgressiveOrigin.Y - VerticalOffset + ProgressiveHeaderHeight + i * (rowHeight + RowGap)</c>,
    /// valid both inside this presenter's own arrange transaction and from an out-of-band
    /// <see cref="Table.ProgressiveRewindow"/> call.</summary>
    internal Point ProgressiveOrigin => new(ViewportBounds.X, ViewportBounds.Y);

    /// <inheritdoc/>
    void IOwnedChildDisposalObserver.OnOwnedChildDisposalRequested(ControlBase child) =>
        _owner.OnPresenterCellDisposalRequested(child);

    /// <summary>Gets the header band height reserved above progressive rows, or zero without a
    /// shown header or defined columns.</summary>
    internal int ProgressiveHeaderHeight =>
        _owner is { ShowHeader: true, Columns.Count: > 0 }
            ? _owner.ActualStyle.CellPadding.Vertical.Add(_headerTextHeight)
                .Add(_owner.ProgressiveController is { LogicalCount: > 0 } ? RowGap : 0)
            : 0;

    /// <summary>Resolves a screen point to one arranged data cell.</summary>
    /// <param name="point">The absolute terminal-cell point.</param>
    /// <param name="row">The resolved display row.</param>
    /// <param name="columnIndex">The resolved zero-based column index.</param>
    /// <returns>True when the point is inside an arranged cell.</returns>
    internal bool TryGetCell(Point point, [MaybeNullWhen(false)] out TableRow row, out int columnIndex)
    {
        var y = ContentSlot.Y;

        if (_owner.ShowHeader && _owner.Columns.Count > 0)
        {
            y = y.Add(_owner.ActualStyle.CellPadding.Vertical.Add(_headerTextHeight));

            if (_owner.Rows.Count > 0)
            {
                y = y.Add(RowGap);
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

                    x = x.Add(ColumnWidths[column].Add(ColumnGap));
                }
            }

            y = y.Add(RowHeights[rowIndex].Add(RowGap));
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

        row = null;
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

        var headerHeight = _owner.ActualStyle.CellPadding.Vertical.Add(_headerTextHeight);

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

            x = x.Add(ColumnWidths[index].Add(ColumnGap));
        }

        return false;
    }

    private Rect ContentSlot { get; set; }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        MeasureCells(constraint.Width);

        if (_owner.IsProgressive)
        {
            return MeasureProgressive();
        }

        var width = Sum(ColumnWidths).Add(GapWidth(_owner.Columns.Count));
        var height = Sum(RowHeights).Add(GapHeight(_owner.Rows.Count));

        if (_owner is { ShowHeader: true, Columns.Count: > 0 })
        {
            height = height.Add(_owner.ActualStyle.CellPadding.Vertical.Add(_headerTextHeight));

            if (_owner.Rows.Count > 0)
            {
                height = height.Add(RowGap);
            }
        }

        return new Size(width, height);
    }

    // No column is ever automatic-width while progressive (Table enforces this), so ColumnWidths
    // above already resolved from header content alone - no per-row probe is needed or possible,
    // since only the realized window's rows exist as controls at all. Height is pure arithmetic
    // against the controller's logical row count, mirroring ListViewHost's RowHeight branch.
    private Size MeasureProgressive()
    {
        var controller = _owner.ProgressiveController!;
        var width = Sum(ColumnWidths).Add(GapWidth(_owner.Columns.Count));
        var logicalCount = controller.LogicalCount;
        var height = Multiply(controller.RowHeight, logicalCount).Add(GapHeight(logicalCount));
        height = height.Add(ProgressiveHeaderHeight);
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

        if (_owner.IsProgressive)
        {
            // Row placement is handled out-of-band by TableDataController.ArrangeWindow, called
            // from Table.ProgressiveRewindow immediately after this presenter's own ArrangeChild
            // call in Table.ArrangeOverride - never here, to keep the same single code path
            // covering both the in-transaction and scroll-driven out-of-band cases.
            return;
        }

        var y = bounds.Y;

        if (_owner is { ShowHeader: true, Columns.Count: > 0 })
        {
            y = y.Add(_owner.ActualStyle.CellPadding.Vertical.Add(_headerTextHeight));

            if (_owner.Rows.Count > 0)
            {
                y = y.Add(RowGap);
            }
        }

        for (var rowIndex = 0; rowIndex < _owner.Rows.Count; rowIndex++)
        {
            var row = _owner.Rows[rowIndex];
            var x = bounds.X;

            for (var columnIndex = 0; columnIndex < _owner.Columns.Count; columnIndex++)
            {
                var slot = new Rect(x, y, ColumnWidths[columnIndex], RowHeights[rowIndex]);
                ArrangeChild(row.Cells[columnIndex], _owner.ActualStyle.CellPadding.Deflate(slot));
                x = x.Add(ColumnWidths[columnIndex].Add(ColumnGap));
            }

            y = y.Add(RowHeights[rowIndex].Add(RowGap));
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
            _owner.ActualStyle.HeaderForeground is { } headerForeground
                ? ResolveColor(headerForeground, _owner.Theme)
                : inherited.Foreground,
            _owner.ActualStyle.HeaderBackground is { } headerBackground
                ? ResolveColor(headerBackground, _owner.Theme)
                : inherited.Background,
            attributes,
            inherited.Hyperlink,
            underline,
            underlineColor);
        var grid = new TerminalStyle(
            _owner.ActualStyle.GridLineColor is { } gridLineColor
                ? ResolveColor(gridLineColor, _owner.Theme)
                : inherited.Foreground,
            inherited.Background,
            attributes,
            inherited.Hyperlink,
            underline,
            underlineColor);
        var horizontalGlyph = _owner.ResolvedHorizontalGridGlyph;
        var verticalGlyph = _owner.ResolvedVerticalGridGlyph;
        var crossGlyph = _owner.ResolvedCrossGridGlyph;
        var headerHeight = _owner.ShowHeader ? _owner.ActualStyle.CellPadding.Vertical.Add(_headerTextHeight) : 0;

        if (_owner.ShowHeader)
        {
            if (_owner.ActualStyle.HeaderBackground.HasValue || _owner.HasOpaqueFill(_owner.CurrentVisualState))
            {
                canvas.Clear(new Rect(ContentSlot.X, ContentSlot.Y, ContentSlot.Width, headerHeight), header);
            }

            var x = ContentSlot.X;

            for (var index = 0; index < _owner.Columns.Count; index++)
            {
                var area = new Rect(x, ContentSlot.Y, ColumnWidths[index], headerHeight);
                var padded = _owner.ActualStyle.CellPadding.Deflate(area);
                var isSortedColumn = index == _owner.SortColumnIndex && _owner.SortDirection != TableSortDirection.None;

                // The sorted column's caption stops one cell short of its own trailing edge -
                // the reserved cell every column's measured width already carries - so the
                // indicator glyph drawn there can never collide with the caption text.
                var captionWidth = isSortedColumn ? Math.Max(0, padded.Width - 1) : padded.Width;
                var text = canvas.Clip(new Rect(padded.X, padded.Y, captionWidth, padded.Height));
                _ = text.Draw(
                    _owner.Columns[index].Header.AsSpan(),
                    new Point(padded.X, padded.Y),
                    header,
                    background: BackgroundMode.Transparent);

                if (isSortedColumn && padded.Width > 0)
                {
                    var indicator = _owner.SortDirection == TableSortDirection.Ascending
                        ? _owner.ResolvedSortAscendingGlyph
                        : _owner.ResolvedSortDescendingGlyph;
                    canvas.DrawRune(indicator, new Point(padded.Right - 1, padded.Y), header, BackgroundMode.Transparent);
                }

                x = x.Add(ColumnWidths[index].Add(ColumnGap));
            }
        }

        if (!_owner.ShowGridLines)
        {
            return;
        }

        var xLine = ContentSlot.X;

        for (var index = 0; index < _owner.Columns.Count - 1; index++)
        {
            xLine = xLine.Add(ColumnWidths[index]);
            DrawVerticalGridLine(canvas, xLine, verticalGlyph, grid);
            xLine = xLine.Add(ColumnGap);
        }

        var hasRows = _owner.IsProgressive
            ? _owner.ProgressiveController!.LogicalCount > 0
            : _owner.Rows.Count > 0;

        if (_owner.ShowHeader && hasRows && RowGap > 0)
        {
            DrawHorizontalGridLine(
                canvas,
                ContentSlot.Y.Add(headerHeight),
                horizontalGlyph,
                crossGlyph,
                grid);
        }

        if (_owner.IsProgressive)
        {
            DrawProgressiveHorizontalGridLines(canvas, horizontalGlyph, crossGlyph, grid);
            return;
        }

        var y = ContentSlot.Y.Add(headerHeight + (_owner.ShowHeader ? RowGap : 0));

        for (var index = 0; index < _owner.Rows.Count - 1; index++)
        {
            y = y.Add(RowHeights[index]);
            DrawHorizontalGridLine(canvas, y, horizontalGlyph, crossGlyph, grid);
            y = y.Add(RowGap);
        }
    }

    // Only realized-window separators are drawn - the whole point of progressive windowing is
    // never touching a logical row outside it, and every separator outside the window is
    // off-screen regardless.
    private void DrawProgressiveHorizontalGridLines(
        TerminalCanvas canvas,
        Rune horizontalGlyph,
        Rune crossGlyph,
        TerminalStyle grid)
    {
        if (RowGap <= 0)
        {
            return;
        }

        var controller = _owner.ProgressiveController!;
        var rowHeight = controller.RowHeight;
        var baseY = ProgressiveOrigin.Y.Add(-VerticalOffset).Add(ProgressiveHeaderHeight);
        var last = Math.Min(
            controller.WindowStart.Add(controller.WindowCount - 1),
            controller.LogicalCount - 2);

        for (var index = controller.WindowStart; index <= last; index++)
        {
            var separatorY = baseY.Add(index.Multiply(rowHeight.Add(RowGap))).Add(rowHeight);
            DrawHorizontalGridLine(canvas, separatorY, horizontalGlyph, crossGlyph, grid);
        }
    }

    [NonNegativeValue]
    internal int ColumnGap => Math.Max(_owner.ColumnSpacing, _owner.ShowGridLines ? _gridLineThickness : 0);

    [NonNegativeValue]
    internal int RowGap => Math.Max(_owner.RowSpacing, _owner.ShowGridLines ? _gridLineThickness : 0);

    private void DrawVerticalGridLine(
        TerminalCanvas canvas,
        int x,
        Rune glyph,
        TerminalStyle style)
    {
        var visible = canvas.Bounds.Intersect(ContentSlot);

        for (var y = visible.Y; y < visible.Bottom; y++)
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
        var visible = canvas.Bounds.Intersect(ContentSlot);

        for (var x = visible.X; x < visible.Right; x++)
        {
            canvas.DrawRune(
                IsColumnSeparator(x) ? cross : horizontal,
                new Point(x, y),
                style,
                BackgroundMode.Transparent);
        }
    }

    [Pure]
    private bool IsColumnSeparator(int x)
    {
        var line = ContentSlot.X;

        for (var index = 0; index < _owner.Columns.Count - 1; index++)
        {
            line = line.Add(ColumnWidths[index]);

            if (line == x)
            {
                return true;
            }

            line = line.Add(ColumnGap);
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
            automatic[columnIndex] = MeasureCells(_owner.Columns[columnIndex].Header)
                .Add(_owner.ActualStyle.CellPadding.Horizontal)
                .Add(_sortIndicatorWidth);
        }

        foreach (var row in _owner.Rows)
        {
            for (var columnIndex = 0; columnIndex < _owner.Columns.Count; columnIndex++)
            {
                var cell = row.Cells[columnIndex];
                _ = MeasureChild(cell, new Constraint(width: null, height: null));
                automatic[columnIndex] = Math.Max(
                    automatic[columnIndex],
                    cell.DesiredSize.Width.Add(cell.Margin.Horizontal.Add(_owner.ActualStyle.CellPadding.Horizontal)));
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
                var width = Math.Max(0, ColumnWidths[columnIndex] - _owner.ActualStyle.CellPadding.Horizontal);
                _ = MeasureChild(cell, new Constraint(width, height: null));
                height = Math.Max(
                    height,
                    cell.DesiredSize.Height.Add(cell.Margin.Vertical.Add(_owner.ActualStyle.CellPadding.Vertical)));
            }

            RowHeights[rowIndex] = height;
        }
    }

    [Pure]
    private int GapWidth(int count) => count < 2 ? 0 : Multiply(ColumnGap, count - 1);

    [Pure]
    private int GapHeight(int count) => count < 2 ? 0 : Multiply(RowGap, count - 1);

    [Pure]
    private static int Sum(IEnumerable<int> values)
    {
        var total = 0;

        foreach (var value in values)
        {
            total = total.Add(value);
        }

        return total;
    }

    [Pure]
    private static int Multiply(int value, int count) => (int) Math.Min(int.MaxValue, (long) value * count);
}
