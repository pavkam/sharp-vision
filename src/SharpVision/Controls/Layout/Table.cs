// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Layout;

using SharpVision.Controls.Display;
using SharpVision.Controls.Input;
using SharpVision.Controls.Scrolling;

using TerminalInput = Terminal.Input;

/// <summary>Arranges typed rows and columns into a terminal-safe table with optional headers and grid lines.</summary>
[PublicAPI]
public sealed class Table: ItemsControl
{
    private readonly TablePresenter _presenter;
    private readonly List<TableRow> _sourceRows = [];
    private readonly HashSet<TableRow> _selectedRows = [];
    private readonly HashSet<TableCellReference> _selectedCells = [];
    private bool _isReordering;
    private TableRow? _selectionAnchorRow;
    private int _selectionAnchorColumn = -1;
    private TableEditState? _edit;
    private Rune? _horizontalGridGlyph;
    private Rune? _verticalGridGlyph;
    private Rune? _crossGridGlyph;

    #region Construction and properties

    /// <summary>Initializes empty mutable row and column collections.</summary>
    public Table()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch;
        Focusable = true;
        TabStop = true;
        TabNavigation = TabNavigation.None;
        Columns = new TableColumnCollection(this);
        Rows = new TableRowCollection(this);
        _presenter = new TablePresenter(this);
        InitializeItemsHost(_presenter);
        _ = AddHandler(Events.Key, OnKeyRouted, handledEventsToo: true);
        _ = AddHandler(Events.Pointer, OnPointerRouted, handledEventsToo: true);
    }

    /// <summary>Gets the mutable titled column definitions.</summary>
    public TableColumnCollection Columns { get; }

    /// <summary>Gets the mutable owned data rows.</summary>
    public TableRowCollection Rows { get; }

    /// <summary>Gets or sets the row or cell selection policy.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is undefined.</exception>
    /// <exception cref="InvalidOperationException">The attached table is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The table is disposed.</exception>
    public TableSelectionMode SelectionMode
    {
        get;
        set
        {
            EnumValidation.ValidateDefined(value);

            if (SetProperty(ref field, value, InvalidationImpact.Render))
            {
                ClearSelection();
            }
        }
    } = TableSelectionMode.Row;

    /// <summary>Gets the selected rows in current display order.</summary>
    public IReadOnlyList<TableRow> SelectedRows => Rows.Where(_selectedRows.Contains).ToArray();

    /// <summary>Gets the selected cells in current display row and column order.</summary>
    public IReadOnlyList<TableCellReference> SelectedCells =>
        Rows.SelectMany(row => Enumerable.Range(0, row.Cells.Count)
                .Select(column => new TableCellReference(row, column)))
            .Where(_selectedCells.Contains)
            .ToArray();

    /// <summary>Gets the active row used by keyboard navigation, or null when no row exists.</summary>
    public TableRow? ActiveRow { get; private set; }

    /// <summary>Gets the active zero-based cell column, or -1 when no cell is active.</summary>
    public int ActiveColumnIndex { get; private set; } = -1;

    /// <summary>Gets the active cell reference, or null when navigation has no active cell.</summary>
    public TableCellReference? ActiveCell => ActiveRow is { } row && ActiveColumnIndex >= 0
        ? new TableCellReference(row, ActiveColumnIndex)
        : null;

    /// <summary>Gets whether one TextInput cell edit transaction is active.</summary>
    public bool IsEditing => _edit is not null;

    /// <summary>Gets the current sorted column, or -1 when sorting is reset.</summary>
    public int SortColumnIndex { get; private set; } = -1;

    /// <summary>Gets the current sort direction.</summary>
    public TableSortDirection SortDirection { get; private set; }

    /// <summary>Raised after selected rows or cells commit.</summary>
    public event EventHandler<TableSelectionChangedEventArgs>? SelectionChanged;

    /// <summary>Raised after a row is activated by pointer or keyboard.</summary>
    public event EventHandler<TableRowInvokedEventArgs>? RowInvoked;

    /// <summary>Raised after a column sort direction and order commit.</summary>
    public event EventHandler<TableSortChangedEventArgs>? SortChanged;

    /// <summary>Gets or sets whether the titled header row is rendered.</summary>
    /// <exception cref="InvalidOperationException">The attached table is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The table is disposed.</exception>
    public bool ShowHeader
    {
        get;
        set => _ = SetProperty(ref field, value, InvalidationImpact.Measure);
    } = true;

    /// <summary>Gets or sets non-negative padding applied to every header and data cell.</summary>
    /// <exception cref="InvalidOperationException">The attached table is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The table is disposed.</exception>
    public Thickness CellPadding
    {
        get;
        set => _ = SetProperty(ref field, value, InvalidationImpact.Measure);
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
            _ = SetProperty(ref field, value, InvalidationImpact.Measure);
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
            _ = SetProperty(ref field, value, InvalidationImpact.Measure);
        }
    }

    /// <summary>Gets or sets whether one-cell light grid lines are drawn in every available table gap.</summary>
    /// <exception cref="InvalidOperationException">The attached table is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The table is disposed.</exception>
    public bool ShowGridLines
    {
        get;
        set => _ = SetProperty(ref field, value, InvalidationImpact.Measure);
    } = true;

    /// <summary>Gets or sets an optional foreground override for header text, resolved through the theme.</summary>
    /// <exception cref="InvalidOperationException">The attached table is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The table is disposed.</exception>
    public Color? HeaderForeground
    {
        get;
        set
        {
            ColorValidation.ValidatePaint(value, nameof(value));
            _ = SetProperty(ref field, value, InvalidationImpact.Render);
        }
    }

    /// <summary>Gets or sets an optional background override for the header row.</summary>
    /// <exception cref="InvalidOperationException">The attached table is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The table is disposed.</exception>
    public Color? HeaderBackground
    {
        get;
        set => _ = SetProperty(ref field, value, InvalidationImpact.Render);
    }

    /// <summary>Gets or sets an optional grid-line color.</summary>
    /// <exception cref="InvalidOperationException">The attached table is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The table is disposed.</exception>
    public Color? GridLineColor
    {
        get;
        set
        {
            ColorValidation.ValidatePaint(value, nameof(value));
            _ = SetProperty(ref field, value, InvalidationImpact.Render);
        }
    }

    /// <summary>Gets or sets the local horizontal grid glyph.</summary>
    public Rune HorizontalGridGlyph
    {
        get => _horizontalGridGlyph ?? ControlGlyphs.Separators.TableHorizontal.Value;
        set => SetGridGlyph(ref _horizontalGridGlyph, value, nameof(HorizontalGridGlyph));
    }

    /// <summary>Gets or sets the local vertical grid glyph.</summary>
    public Rune VerticalGridGlyph
    {
        get => _verticalGridGlyph ?? ControlGlyphs.Separators.TableVertical.Value;
        set => SetGridGlyph(ref _verticalGridGlyph, value, nameof(VerticalGridGlyph));
    }

    /// <summary>Gets or sets the local grid-intersection glyph.</summary>
    public Rune CrossGridGlyph
    {
        get => _crossGridGlyph ?? ControlGlyphs.Separators.TableCross.Value;
        set => SetGridGlyph(ref _crossGridGlyph, value, nameof(CrossGridGlyph));
    }

    /// <summary>Clears all local grid glyphs to the code-owned defaults.</summary>
    /// <exception cref="InvalidOperationException">The attached table is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The table is disposed.</exception>
    public void ResetGridGlyphs()
    {
        VerifyMutable();

        ResetGridGlyph(ref _horizontalGridGlyph, nameof(HorizontalGridGlyph));
        ResetGridGlyph(ref _verticalGridGlyph, nameof(VerticalGridGlyph));
        ResetGridGlyph(ref _crossGridGlyph, nameof(CrossGridGlyph));
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

    /// <summary>Gets or sets the complete local style for both private scrollbars.</summary>
    /// <exception cref="InvalidOperationException">The attached table is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The table is disposed.</exception>
    public ScrollBarStyle? ScrollBarStyle
    {
        get => _presenter.ScrollBarStyle;
        set
        {
            VerifyMutable();

            if (_presenter.ScrollBarStyle == value)
            {
                return;
            }

            var previous = ActualScrollBarStyle;
            _presenter.ScrollBarStyle = value;
            var current = ActualScrollBarStyle;
            NotifyPropertyChanged(nameof(ScrollBarStyle), InvalidationImpact.None);

            if (previous != current)
            {
                NotifyPropertyChanged(nameof(ActualScrollBarStyle), InvalidationImpact.None);
            }
        }
    }

    /// <summary>Gets the resolved private-scrollbar style.</summary>
    public ScrollBarStyle ActualScrollBarStyle =>
        ScrollBarStyle ?? Scrolling.ScrollBarStyle.Default;

    /// <inheritdoc/>
    protected override string? GetThemeResolvedStylePropertyName(Theme? previous, Theme? current) =>
        base.GetThemeResolvedStylePropertyName(previous, current);

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
    public bool ScrollBy(int x, int y, ScrollCause cause = ScrollCause.Programmatic) =>
        _presenter.ScrollBy(x, y, cause);

    /// <summary>Scrolls minimally to expose one row-cell descendant.</summary>
    /// <param name="descendant">The non-null descendant control.</param>
    /// <returns>True when at least one offset changed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="descendant"/> is null.</exception>
    /// <exception cref="ArgumentException">The control is not a realized table descendant.</exception>
    /// <exception cref="InvalidOperationException">The attached table is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The table is disposed.</exception>
    public bool BringIntoView(Control descendant) => _presenter.BringIntoView(descendant);

    /// <summary>Selects one owned row and makes its first cell active.</summary>
    /// <param name="row">The non-null row owned by this table.</param>
    /// <param name="modifiers">Optional control/shift selection modifiers.</param>
    /// <exception cref="ArgumentNullException"><paramref name="row"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="row"/> is not owned by this table.</exception>
    /// <exception cref="InvalidOperationException">The attached table is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The table is disposed.</exception>
    public void SelectRow(TableRow row, TerminalInput.Modifiers modifiers = TerminalInput.Modifiers.None)
    {
        ArgumentNullException.ThrowIfNull(row);
        VerifyOwned(row);
        SelectRowCore(row, modifiers, 0);
    }

    private void SelectRowCore(TableRow row, TerminalInput.Modifiers modifiers, int activeColumnIndex)
    {
        SetActive(row, activeColumnIndex);

        if (SelectionMode is TableSelectionMode.None or TableSelectionMode.Cell or TableSelectionMode.MultipleCells)
        {
            return;
        }

        var next = new HashSet<TableRow>(_selectedRows);
        var control = (modifiers & TerminalInput.Modifiers.Control) != 0;
        var shift = (modifiers & TerminalInput.Modifiers.Shift) != 0;

        if (SelectionMode == TableSelectionMode.MultipleRows && shift && _selectionAnchorRow is not null)
        {
            next.Clear();
            AddRowRange(next, _selectionAnchorRow, row);
        }
        else if (SelectionMode == TableSelectionMode.MultipleRows && control)
        {
            if (!next.Remove(row))
            {
                _ = next.Add(row);
            }

            _selectionAnchorRow = row;
        }
        else
        {
            next.Clear();
            _ = next.Add(row);
            _selectionAnchorRow = row;
        }

        CommitSelection(next, []);
    }

    /// <summary>Selects one owned cell and makes it active.</summary>
    /// <param name="row">The non-null row owned by this table.</param>
    /// <param name="columnIndex">The zero-based cell column.</param>
    /// <param name="modifiers">Optional control/shift selection modifiers.</param>
    /// <exception cref="ArgumentNullException"><paramref name="row"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="row"/> is not owned by this table.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="columnIndex"/> is outside the row.</exception>
    /// <exception cref="InvalidOperationException">The attached table is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The table is disposed.</exception>
    public void SelectCell(TableRow row, int columnIndex, TerminalInput.Modifiers modifiers = TerminalInput.Modifiers.None)
    {
        ArgumentNullException.ThrowIfNull(row);
        VerifyOwned(row);
        ArgumentOutOfRangeException.ThrowIfNegative(columnIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint) columnIndex, (uint) row.Cells.Count);
        SetActive(row, columnIndex);

        if (SelectionMode is TableSelectionMode.None or TableSelectionMode.Row or TableSelectionMode.MultipleRows)
        {
            return;
        }

        var reference = new TableCellReference(row, columnIndex);
        var next = new HashSet<TableCellReference>(_selectedCells);
        var control = (modifiers & TerminalInput.Modifiers.Control) != 0;
        var shift = (modifiers & TerminalInput.Modifiers.Shift) != 0;

        if (SelectionMode == TableSelectionMode.MultipleCells && shift && _selectionAnchorRow is not null)
        {
            next.Clear();
            AddCellRange(next, _selectionAnchorRow, _selectionAnchorColumn, row, columnIndex);
        }
        else if (SelectionMode == TableSelectionMode.MultipleCells && control)
        {
            if (!next.Remove(reference))
            {
                _ = next.Add(reference);
            }

            _selectionAnchorRow = row;
            _selectionAnchorColumn = columnIndex;
        }
        else
        {
            next.Clear();
            _ = next.Add(reference);
            _selectionAnchorRow = row;
            _selectionAnchorColumn = columnIndex;
        }

        CommitSelection([], next);
    }

    /// <summary>Clears all selected rows and cells while retaining the active location.</summary>
    /// <exception cref="InvalidOperationException">The attached table is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The table is disposed.</exception>
    public void ClearSelection()
    {
        VerifyMutable();
        CommitSelection([], []);
        _selectionAnchorRow = null;
        _selectionAnchorColumn = -1;
    }

    /// <summary>Selects every row or cell allowed by the current selection mode.</summary>
    /// <exception cref="InvalidOperationException">The attached table is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The table is disposed.</exception>
    public void SelectAll()
    {
        VerifyMutable();

        if (Rows.Count == 0 || SelectionMode == TableSelectionMode.None)
        {
            ClearSelection();
            return;
        }

        if (SelectionMode is TableSelectionMode.Row or TableSelectionMode.MultipleRows)
        {
            CommitSelection([.. Rows], []);
        }
        else
        {
            HashSet<TableCellReference> all = [];

            foreach (var row in Rows)
            {
                for (var column = 0; column < row.Cells.Count; column++)
                {
                    _ = all.Add(new TableCellReference(row, column));
                }
            }

            CommitSelection([], all);
        }

        SetActive(Rows[0], 0);
    }

    /// <summary>Cycles one column through ascending, descending, and reset ordering.</summary>
    /// <param name="columnIndex">The zero-based column to sort.</param>
    /// <exception cref="ArgumentOutOfRangeException">The column index is outside the collection.</exception>
    /// <exception cref="InvalidOperationException">The attached table is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The table is disposed.</exception>
    public void SortBy(int columnIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(columnIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint) columnIndex, (uint) Columns.Count);

        var direction = SortColumnIndex != columnIndex
            ? TableSortDirection.Ascending
            : SortDirection switch
            {
                TableSortDirection.None => TableSortDirection.Ascending,
                TableSortDirection.Ascending => TableSortDirection.Descending,
                TableSortDirection.Descending => TableSortDirection.None,
                _ => throw new UnreachableException()
            };
        SetSort(columnIndex, direction);
    }

    /// <summary>Commits an explicit sort direction, or resets to insertion order.</summary>
    /// <param name="columnIndex">The zero-based column, or -1 when resetting.</param>
    /// <param name="direction">The desired direction.</param>
    /// <exception cref="ArgumentOutOfRangeException">The column or direction is invalid.</exception>
    /// <exception cref="InvalidOperationException">The attached table is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The table is disposed.</exception>
    public void SetSort(int columnIndex, TableSortDirection direction)
    {
        VerifyMutable();
        EnumValidation.ValidateDefined(direction);

        if (direction == TableSortDirection.None)
        {
            if (columnIndex is not -1 && (columnIndex < 0 || columnIndex >= Columns.Count))
            {
                throw new ArgumentOutOfRangeException(nameof(columnIndex));
            }

            SortColumnIndex = -1;
            SortDirection = TableSortDirection.None;
            ReorderRows(_sourceRows);
            SortChanged?.Invoke(this, new TableSortChangedEventArgs(-1, TableSortDirection.None));
            return;
        }

        ArgumentOutOfRangeException.ThrowIfNegative(columnIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint) columnIndex, (uint) Columns.Count);
        SortColumnIndex = columnIndex;
        SortDirection = direction;

        // _sourceRows.IndexOf costs O(n) per row, turning this tie-break into an O(n^2) scan on
        // top of the O(n log n) sort; a position precomputed once resolves the same tie-break in
        // O(1) per comparison (see #118).
        var position = new Dictionary<TableRow, int>(_sourceRows.Count);

        for (var index = 0; index < _sourceRows.Count; index++)
        {
            position[_sourceRows[index]] = index;
        }

        var ordered = Rows.OrderBy(row => GetSortKey(row, columnIndex), Comparer<object?>.Create(CompareKeys))
            .ThenBy(row => position[row])
            .ToArray();

        if (direction == TableSortDirection.Descending)
        {
            ordered = [.. Rows.OrderByDescending(row => GetSortKey(row, columnIndex), Comparer<object?>.Create(CompareKeys)).ThenBy(row => position[row])];
        }

        ReorderRows(ordered);
        SortChanged?.Invoke(this, new TableSortChangedEventArgs(columnIndex, direction));
    }

    /// <summary>Resets active sorting to the original insertion order.</summary>
    /// <exception cref="InvalidOperationException">The attached table is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The table is disposed.</exception>
    public void ResetSort() => SetSort(-1, TableSortDirection.None);

    /// <summary>Begins editing one existing TextInput cell.</summary>
    /// <param name="row">The non-null owned row.</param>
    /// <param name="columnIndex">The zero-based column index.</param>
    /// <returns>True when the cell entered edit mode; false for read-only or non-TextInput cells.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="row"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="row"/> is not owned by this table.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The column index is outside the row.</exception>
    /// <exception cref="InvalidOperationException">The attached table is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The table is disposed.</exception>
    public bool BeginEdit(TableRow row, int columnIndex)
    {
        ArgumentNullException.ThrowIfNull(row);
        VerifyOwned(row);
        ArgumentOutOfRangeException.ThrowIfNegative(columnIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint) columnIndex, (uint) row.Cells.Count);

        if (Columns[columnIndex].IsReadOnly || row.Cells[columnIndex] is not TextInput editor || editor.IsReadOnly)
        {
            return false;
        }

        if (_edit is not null)
        {
            _ = CommitEdit();
        }

        SetActive(row, columnIndex);
        _edit = new TableEditState(row, columnIndex, editor, editor.Text);
        editor.Submitted += OnEditorSubmitted;
        _ = editor.Focus();
        editor.Select(0, editor.Text.Length);
        return true;
    }

    /// <summary>Commits the current TextInput edit transaction.</summary>
    /// <returns>True when an edit was committed.</returns>
    /// <exception cref="InvalidOperationException">The attached table is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The table is disposed.</exception>
    public bool CommitEdit()
    {
        VerifyMutable();

        if (_edit is not { } edit)
        {
            return false;
        }

        edit.Editor.Submitted -= OnEditorSubmitted;
        _edit = null;
        SetActive(edit.Row, edit.ColumnIndex);
        return true;
    }

    /// <summary>Restores the original text and cancels the current TextInput edit transaction.</summary>
    /// <returns>True when an edit was cancelled.</returns>
    /// <exception cref="InvalidOperationException">The attached table is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The table is disposed.</exception>
    public bool CancelEdit()
    {
        VerifyMutable();

        if (_edit is not { } edit)
        {
            return false;
        }

        CancelActiveEditIfOwned(edit.Row);
        return true;
    }

    /// <summary>Returns selected rows or cells as deterministic tab-separated clipboard text.</summary>
    /// <returns>Owned text with rows separated by LF, or an empty string when nothing is selected.</returns>
    public string CopySelection()
    {
        var lines = new List<string>();

        foreach (var row in Rows)
        {
            var columns = SelectionMode is TableSelectionMode.Row or TableSelectionMode.MultipleRows
                ? Enumerable.Range(0, row.Cells.Count)
                : Enumerable.Range(0, row.Cells.Count).Where(column =>
                    _selectedCells.Contains(new TableCellReference(row, column)));

            var values = columns.Select(column => CellText(row.Cells[column])).ToArray();

            if (values.Length > 0 && (SelectionMode is not TableSelectionMode.Row and not TableSelectionMode.MultipleRows || _selectedRows.Contains(row)))
            {
                lines.Add(string.Join('\t', values));
            }
        }

        return string.Join('\n', lines);
    }

    #endregion

    #region Layout and rendering

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
        => MeasureChild(_presenter, constraint);

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds) =>
        ArrangeChild(_presenter, bounds, ResolvedAxes.Both);

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas) => _presenter.RenderTableChrome(canvas);

    /// <summary>Gets the current state snapshot for private table chrome resolution.</summary>
    internal VisualState CurrentVisualState => GetAppearanceState();

    /// <summary>Gets the terminal-safe horizontal grid glyph for the current theme and cell policy.</summary>
    internal Rune ResolvedHorizontalGridGlyph => CellGlyphResolver.Resolve(
        HorizontalGridGlyph,
        ControlGlyphs.Separators.TableHorizontal.Fallback,
        CellPolicy.AmbiguousWidth);

    /// <summary>Gets the terminal-safe vertical grid glyph for the current theme and cell policy.</summary>
    internal Rune ResolvedVerticalGridGlyph => CellGlyphResolver.Resolve(
        VerticalGridGlyph,
        ControlGlyphs.Separators.TableVertical.Fallback,
        CellPolicy.AmbiguousWidth);

    /// <summary>Gets the terminal-safe grid-intersection glyph for the current theme and cell policy.</summary>
    internal Rune ResolvedCrossGridGlyph => CellGlyphResolver.Resolve(
        CrossGridGlyph,
        ControlGlyphs.Separators.TableCross.Fallback,
        CellPolicy.AmbiguousWidth);

    #endregion

    private void SetGridGlyph(ref Rune? storage, Rune value, string propertyName)
    {
        _ = new ControlGlyph(value, value);
        VerifyMutable();
        if (storage == value)
        {
            return;
        }

        storage = value;
        NotifyPropertyChanged(propertyName, InvalidationImpact.Render);
    }

    private void ResetGridGlyph(ref Rune? storage, string propertyName)
    {
        if (storage.HasValue)
        {
            storage = null;
            NotifyPropertyChanged(propertyName, InvalidationImpact.Render);
        }
    }

    #region Selection and input

    private void OnKeyRouted(object? sender, KeyEventArgs eventArgs)
    {
        _ = sender;

        if (eventArgs.Phase != Phase.Preview || eventArgs.Stroke.Action is not (KeyAction.Press or KeyAction.Repeat))
        {
            return;
        }

        var stroke = eventArgs.Stroke;

        if (_edit is not null)
        {
            if (stroke.Code == TerminalInput.Code.Enter)
            {
                _ = CommitEdit();
                eventArgs.Handled = true;
                return;
            }

            if (stroke.Code == TerminalInput.Code.Escape)
            {
                _ = CancelEdit();
                eventArgs.Handled = true;
                return;
            }

            if (stroke.Code == TerminalInput.Code.Tab)
            {
                _ = CommitEdit();
                eventArgs.Handled = MoveActive(0, (stroke.Modifiers & TerminalInput.Modifiers.Shift) != 0 ? -1 : 1);
            }

            return;
        }

        if (stroke.Code == TerminalInput.Code.F2)
        {
            eventArgs.Handled = ActiveCell is { } cell && BeginEdit(cell.Row, cell.ColumnIndex);
            return;
        }

        if (stroke.Code == TerminalInput.Code.Character &&
            stroke.Character is { } character &&
            (stroke.Modifiers & TerminalInput.Modifiers.Control) != 0 &&
            Rune.ToLowerInvariant(character) == new Rune('a'))
        {
            SelectAll();
            eventArgs.Handled = true;
            return;
        }

        if (stroke.Code is not (TerminalInput.Code.Up or TerminalInput.Code.Down or
            TerminalInput.Code.Left or TerminalInput.Code.Right or TerminalInput.Code.Home or
            TerminalInput.Code.End))
        {
            if (stroke.Code == TerminalInput.Code.Enter)
            {
                eventArgs.Handled = ActivateCurrent();
            }

            return;
        }

        var moved = stroke.Code == TerminalInput.Code.Up
            ? MoveActive(-1, 0)
            : stroke.Code == TerminalInput.Code.Down
                ? MoveActive(1, 0)
                : stroke.Code == TerminalInput.Code.Left
                    ? MoveActive(0, -1)
                    : stroke.Code == TerminalInput.Code.Right
                        ? MoveActive(0, 1)
                        : stroke.Code == TerminalInput.Code.Home
                            ? MoveToEndpoint(first: true)
                            : stroke.Code == TerminalInput.Code.End && MoveToEndpoint(first: false);

        if (moved)
        {
            eventArgs.Handled = true;
            return;
        }

        if (stroke.Code == TerminalInput.Code.Enter)
        {
            eventArgs.Handled = ActivateCurrent();
        }
    }

    private void OnPointerRouted(object? sender, PointerEventArgs eventArgs)
    {
        _ = sender;

        if (eventArgs.Phase != Phase.Preview ||
            eventArgs.Pointer.Action != TerminalInput.PointerAction.Press ||
            (eventArgs.Pointer.Buttons & TerminalInput.Buttons.Primary) == 0 ||
            eventArgs.Pointer.Cells is not { } point)
        {
            return;
        }

        if (_presenter.TryGetHeaderColumn(point, out var headerColumn))
        {
            // Sorting reorders through Rows.Clear() + re-add rather than moving rows in place, so
            // an in-progress edit must be committed here — the same as the ordinary
            // click-elsewhere check below — instead of being silently reverted when the edited
            // row's removal cancels it (see #109).
            _ = CommitEdit();
            SortBy(headerColumn);
            return;
        }

        if (!_presenter.TryGetCell(point, out var row, out var column) &&
            !TryGetCell(eventArgs.OriginalSource, out row, out column))
        {
            return;
        }

        if (_edit is not null && (!_edit.Row.Equals(row) || _edit.ColumnIndex != column))
        {
            _ = CommitEdit();
        }

        var modifiers = eventArgs.Pointer.Modifiers;

        if (SelectionMode is TableSelectionMode.Row or TableSelectionMode.MultipleRows)
        {
            SelectRow(row, modifiers);
            SetActive(row, column);
        }
        else
        {
            SelectCell(row, column, modifiers);
        }

        if (eventArgs.ClickCount >= 2)
        {
            _ = BeginEdit(row, column);
        }

        RowInvoked?.Invoke(this, new TableRowInvokedEventArgs(row, Rows.IndexOf(row), ActivationCause.Pointer));
    }

    private bool MoveActive(int rowDelta, int columnDelta)
    {
        if (Rows.Count == 0 || Columns.Count == 0)
        {
            return false;
        }

        var rowIndex = ActiveRow is null ? 0 : Rows.IndexOf(ActiveRow);
        var columnIndex = ActiveColumnIndex < 0 ? 0 : ActiveColumnIndex;
        var targetRow = Math.Clamp(rowIndex + rowDelta, 0, Rows.Count - 1);
        var targetColumn = Math.Clamp(columnIndex + columnDelta, 0, Columns.Count - 1);

        if (ActiveRow is not null && targetRow == rowIndex && targetColumn == columnIndex)
        {
            return false;
        }

        var row = Rows[targetRow];

        if (SelectionMode is TableSelectionMode.Row or TableSelectionMode.MultipleRows)
        {
            SelectRowCore(row, TerminalInput.Modifiers.None, targetColumn);
        }
        else
        {
            SelectCell(row, targetColumn);
        }

        _ = BringIntoView(row.Cells[targetColumn]);
        return true;
    }

    private bool MoveToEndpoint(bool first)
    {
        if (Rows.Count == 0 || Columns.Count == 0)
        {
            return false;
        }

        var row = first ? Rows[0] : Rows[^1];
        var column = SelectionMode is TableSelectionMode.Row or TableSelectionMode.MultipleRows
            ? Math.Clamp(ActiveColumnIndex, 0, row.Cells.Count - 1)
            : first ? 0 : Columns.Count - 1;

        if (SelectionMode is TableSelectionMode.Row or TableSelectionMode.MultipleRows)
        {
            SelectRowCore(row, TerminalInput.Modifiers.None, column);
        }
        else if (SelectionMode is TableSelectionMode.Cell or TableSelectionMode.MultipleCells)
        {
            SelectCell(row, column);
        }

        return true;
    }

    private bool ActivateCurrent()
    {
        if (Rows.Count == 0 || Columns.Count == 0)
        {
            return false;
        }

        var row = ActiveRow ?? Rows[0];
        var column = ActiveColumnIndex < 0 ? 0 : ActiveColumnIndex;
        SetActive(row, column);

        if (row.Cells[column] is TextInput && BeginEdit(row, column))
        {
            return true;
        }

        RowInvoked?.Invoke(this, new TableRowInvokedEventArgs(row, Rows.IndexOf(row), ActivationCause.Keyboard));
        return true;
    }

    private void SetActive(TableRow row, int columnIndex)
    {
        VerifyOwned(row);
        ArgumentOutOfRangeException.ThrowIfNegative(columnIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint) columnIndex, (uint) row.Cells.Count);

        if (ReferenceEquals(ActiveRow, row) && ActiveColumnIndex == columnIndex)
        {
            return;
        }

        ActiveRow = row;
        ActiveColumnIndex = columnIndex;
        ApplyCellStates();
    }

    private void CommitSelection(
        IEnumerable<TableRow> rows,
        IEnumerable<TableCellReference> cells)
    {
        var nextRows = new HashSet<TableRow>(rows);
        var nextCells = new HashSet<TableCellReference>(cells);
        var oldRows = new HashSet<TableRow>(_selectedRows);
        var oldCells = new HashSet<TableCellReference>(_selectedCells);

        if (oldRows.SetEquals(nextRows) && oldCells.SetEquals(nextCells))
        {
            return;
        }

        _selectedRows.Clear();
        _selectedRows.UnionWith(nextRows);
        _selectedCells.Clear();
        _selectedCells.UnionWith(nextCells);
        ApplyCellStates();

        SelectionChanged?.Invoke(
            this,
            new TableSelectionChangedEventArgs(
                nextRows.Except(oldRows).ToArray(),
                oldRows.Except(nextRows).ToArray(),
                nextCells.Except(oldCells).ToArray(),
                oldCells.Except(nextCells).ToArray()));
    }

    private void ApplyCellStates()
    {
        foreach (var row in Rows)
        {
            for (var column = 0; column < row.Cells.Count; column++)
            {
                var cell = row.Cells[column];
                var selected = _selectedRows.Contains(row) ||
                               _selectedCells.Contains(new TableCellReference(row, column));
                cell.SetSelectedState(selected);
                cell.SetCurrentState(ReferenceEquals(ActiveRow, row) && ActiveColumnIndex == column);
            }
        }
    }

    private void AddRowRange(HashSet<TableRow> target, TableRow start, TableRow end)
    {
        var startIndex = Rows.IndexOf(start);
        var endIndex = Rows.IndexOf(end);

        if (startIndex < 0 || endIndex < 0)
        {
            return;
        }

        for (var index = Math.Min(startIndex, endIndex); index <= Math.Max(startIndex, endIndex); index++)
        {
            _ = target.Add(Rows[index]);
        }
    }

    private void AddCellRange(
        HashSet<TableCellReference> target,
        TableRow startRow,
        int startColumn,
        TableRow endRow,
        int endColumn)
    {
        var startIndex = Rows.IndexOf(startRow);
        var endIndex = Rows.IndexOf(endRow);

        if (startIndex < 0 || endIndex < 0)
        {
            return;
        }

        for (var rowIndex = Math.Min(startIndex, endIndex); rowIndex <= Math.Max(startIndex, endIndex); rowIndex++)
        {
            var row = Rows[rowIndex];
            var first = rowIndex == startIndex ? startColumn : 0;
            var last = rowIndex == endIndex ? endColumn : row.Cells.Count - 1;

            for (var column = Math.Min(first, last); column <= Math.Max(first, last); column++)
            {
                _ = target.Add(new TableCellReference(row, column));
            }
        }
    }

    private void VerifyOwned(TableRow row)
    {
        if (!Rows.Contains(row))
        {
            throw new ArgumentException("The row is not owned by this table.", nameof(row));
        }

        VerifyMutable();
    }

    private bool TryGetCell(Control? source, out TableRow row, out int columnIndex)
    {
        if (source is null)
        {
            row = null!;
            columnIndex = -1;
            return false;
        }

        for (var rowIndex = 0; rowIndex < Rows.Count; rowIndex++)
        {
            var candidate = Rows[rowIndex];

            for (var column = 0; column < candidate.Cells.Count; column++)
            {
                for (var current = source; current is not null; current = current.Parent)
                {
                    if (ReferenceEquals(current, candidate.Cells[column]))
                    {
                        row = candidate;
                        columnIndex = column;
                        return true;
                    }
                }
            }
        }

        row = null!;
        columnIndex = -1;
        return false;
    }

    private void OnEditorSubmitted(object? sender, SubmittedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        _ = CommitEdit();
    }

    private void CancelActiveEditIfOwned(TableRow row)
    {
        if (_edit is not { } edit || !ReferenceEquals(edit.Row, row))
        {
            return;
        }

        edit.Editor.Submitted -= OnEditorSubmitted;
        edit.Editor.Text = edit.OriginalText;
        _edit = null;

        if (Rows.Contains(edit.Row))
        {
            SetActive(edit.Row, Math.Min(edit.ColumnIndex, edit.Row.Cells.Count - 1));
        }
        else if (ActiveRow is not null && !Rows.Contains(ActiveRow))
        {
            ActiveRow = null;
            ActiveColumnIndex = -1;
            ApplyCellStates();
        }
    }

    private static string CellText(Control cell) => cell switch
    {
        Text text => text.Content,
        TextInput input => input.Text,
        ContentControl { Content: Text text } => text.Content,
        ContentControl { Content: TextInput input } => input.Text,
        _ => string.Empty
    };

    private object? GetSortKey(TableRow row, int columnIndex)
    {
        var cell = row.Cells[columnIndex];
        return Columns[columnIndex].SortKey?.Invoke(cell) ?? CellText(cell);
    }

    private static int CompareKeys(object? left, object? right)
    {
        if (ReferenceEquals(left, right))
        {
            return 0;
        }

        if (left is null)
        {
            return -1;
        }

        if (right is null)
        {
            return 1;
        }

        if (left is string leftText && right is string rightText)
        {
            return string.CompareOrdinal(leftText, rightText);
        }

        if (left is IComparable comparable)
        {
            try
            {
                return comparable.CompareTo(right);
            }
            catch (ArgumentException)
            {
                // Fall through to ordinal text comparison when keys use unlike types.
            }
        }

        return string.CompareOrdinal(Convert.ToString(left, CultureInfo.InvariantCulture), Convert.ToString(right, CultureInfo.InvariantCulture));
    }

    private void ReorderRows(IReadOnlyList<TableRow> ordered)
    {
        if (Rows.SequenceEqual(ordered))
        {
            return;
        }

        _isReordering = true;

        try
        {
            Rows.Clear();

            foreach (var row in ordered)
            {
                Rows.Add(row);
            }
        }
        finally
        {
            _isReordering = false;
        }

        ApplyCellStates();
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

    /// <summary>Gets the currently sorted column before a collection mutation.</summary>
    /// <param name="index">Receives the sorted column's current index, or -1 when unsorted.</param>
    /// <returns>The sorted column value, or null when the table is unsorted.</returns>
    internal TableColumn? GetSortedColumn(out int index)
    {
        index = SortColumnIndex;
        return SortDirection == TableSortDirection.None || SortColumnIndex < 0 || SortColumnIndex >= Columns.Count
            ? null
            : Columns[SortColumnIndex];
    }

    /// <summary>Invalidates measurement after a committed column mutation and remaps sorting.</summary>
    /// <param name="previousColumn">The column that was sorted before the mutation, if any.</param>
    /// <param name="previousIndex">The sorted column's index before the mutation.</param>
    internal void ColumnsChanged(TableColumn? previousColumn, int previousIndex)
    {
        if (previousColumn.HasValue && SortDirection != TableSortDirection.None)
        {
            var newIndex = previousIndex >= 0 && previousIndex < Columns.Count && Columns[previousIndex].Equals(previousColumn.Value)
                ? previousIndex
                : previousIndex + 1 < Columns.Count && Columns[previousIndex + 1].Equals(previousColumn.Value)
                    ? previousIndex + 1
                    : previousIndex > 0 && previousIndex - 1 < Columns.Count && Columns[previousIndex - 1].Equals(previousColumn.Value)
                        ? previousIndex - 1
                        : Columns.IndexOf(previousColumn.Value);

            if (newIndex < 0)
            {
                ResetSort();
            }
            else
            {
                SortColumnIndex = newIndex;
            }
        }

        Invalidate(Invalidation.Measure);
    }

    /// <summary>Validates one public column definition before collection ownership.</summary>
    /// <param name="column">The candidate definition.</param>
    /// <exception cref="ArgumentException">The header is missing or whitespace.</exception>
    internal static void ValidateColumn(TableColumn column) =>
        ArgumentException.ThrowIfNullOrWhiteSpace(column.Header, nameof(column));

    /// <summary>Inserts and attaches one validated row.</summary>
    /// <param name="owner">The calling row collection.</param>
    /// <param name="index">The insertion index.</param>
    /// <param name="row">The non-null row.</param>
    internal void InsertRow(TableRowCollection owner, int index, TableRow row)
    {
        VerifyRowsOwner(owner);
        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint) index, (uint) Rows.Count);
        ValidateRow(row);

        foreach (var cell in row.Cells)
        {
            _presenter.Children.Add(cell);
        }

        owner.InsertAttached(index, row);
        if (!_isReordering)
        {
            _sourceRows.Insert(index, row);
        }

        Invalidate(Invalidation.Measure);
        ApplyCellStates();

        if (!_isReordering && SortDirection != TableSortDirection.None)
        {
            SetSort(SortColumnIndex, SortDirection);
        }
    }

    /// <summary>Removes and detaches one owned row.</summary>
    /// <param name="owner">The calling row collection.</param>
    /// <param name="index">The valid row index.</param>
    internal void RemoveRow(TableRowCollection owner, int index)
    {
        VerifyRowsOwner(owner);
        RemoveRowCore(owner, index, repairSelection: true);
    }

    private void RemoveRowCore(TableRowCollection owner, int index, bool repairSelection)
    {
        var row = Rows[index];

        // A reorder relocates this exact row instance rather than removing it — cancelling an
        // edit it owns would silently revert uncommitted text for a row that is about to be
        // re-added unchanged (see #109).
        if (!_isReordering)
        {
            CancelActiveEditIfOwned(row);
        }

        if (repairSelection)
        {
            var fallback = index < Rows.Count - 1 ? Rows[index + 1] : index > 0 ? Rows[index - 1] : null;
            RepairSelectionForRows([row], fallback);
        }

        foreach (var cell in row.Cells)
        {
            _ = _presenter.Children.Remove(cell);
        }

        owner.RemoveAttached(index);
        if (!_isReordering)
        {
            _ = _sourceRows.Remove(row);

            if (ReferenceEquals(ActiveRow, row))
            {
                ActiveRow = Rows.Count == 0 ? null : Rows[Math.Min(index, Rows.Count - 1)];
                ActiveColumnIndex = ActiveRow is null ? -1 : 0;
            }
        }

        Invalidate(Invalidation.Measure);
        ApplyCellStates();
    }

    /// <summary>Clears and detaches every owned row.</summary>
    /// <param name="owner">The calling row collection.</param>
    internal void ClearRows(TableRowCollection owner)
    {
        VerifyRowsOwner(owner);

        // A reorder clears every row only to re-add the exact same instances in a new order —
        // none of them are actually being removed, so selection referencing those row instances
        // must survive untouched instead of being unconditionally emptied here (see #109).
        if (!_isReordering)
        {
            var removedRows = Rows.ToArray();
            RepairSelectionForRows(removedRows, null);
        }

        for (var index = Rows.Count - 1; index >= 0; index--)
        {
            RemoveRowCore(owner, index, repairSelection: false);
        }
    }

    /// <summary>Atomically replaces one row after validating the candidate ownership transfer.</summary>
    /// <param name="owner">The calling row collection.</param>
    /// <param name="index">The valid row index.</param>
    /// <param name="row">The non-null replacement row.</param>
    internal void ReplaceRow(TableRowCollection owner, int index, TableRow row)
    {
        VerifyRowsOwner(owner);
        _ = Rows[index];
        ValidateRow(row);
        var previous = Rows[index];
        CancelActiveEditIfOwned(previous);

        if (ReferenceEquals(_selectionAnchorRow, previous))
        {
            _selectionAnchorRow = row;
            _selectionAnchorColumn = Math.Min(_selectionAnchorColumn, row.Cells.Count - 1);
        }

        RepairSelectionForRows([previous], row);

        foreach (var cell in previous.Cells)
        {
            _ = _presenter.Children.Remove(cell);
        }

        foreach (var cell in row.Cells)
        {
            _presenter.Children.Add(cell);
        }

        owner.ReplaceAttached(index, row);
        if (!_isReordering && _sourceRows.IndexOf(previous) is var sourceIndex && sourceIndex >= 0)
        {
            _sourceRows[sourceIndex] = row;
        }

        if (ReferenceEquals(ActiveRow, previous))
        {
            ActiveRow = row;
        }

        Invalidate(Invalidation.Measure);
        ApplyCellStates();

        if (!_isReordering && SortDirection != TableSortDirection.None)
        {
            SetSort(SortColumnIndex, SortDirection);
        }
    }

    private void RepairSelectionForRows(IEnumerable<TableRow> removedRows, TableRow? replacementAnchor)
    {
        var removed = removedRows.ToHashSet();

        if (removed.Count == 0)
        {
            return;
        }

        if (_selectionAnchorRow is not null && removed.Contains(_selectionAnchorRow))
        {
            _selectionAnchorRow = replacementAnchor;
            _selectionAnchorColumn = replacementAnchor is null
                ? -1
                : Math.Clamp(_selectionAnchorColumn, 0, replacementAnchor.Cells.Count - 1);
        }

        CommitSelection(
            _selectedRows.Where(selected => !removed.Contains(selected)),
            _selectedCells.Where(selected => !removed.Contains(selected.Row)));
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

    private void VerifyRowsOwner(TableRowCollection owner)
    {
        ArgumentNullException.ThrowIfNull(owner);

        if (!ReferenceEquals(owner, Rows))
        {
            throw new ArgumentException("The row collection does not belong to this table.", nameof(owner));
        }
    }

    #endregion
}
