// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Layout;

using SharpVision.Controls.Display;
using SharpVision.Controls.Input;

using NonNegativeValue = JetBrains.Annotations.NonNegativeValueAttribute;
using TerminalInput = Terminal.Input;
using ValueRange = JetBrains.Annotations.ValueRangeAttribute;

/// <summary>Arranges typed rows and columns into a terminal-safe table with optional headers and grid lines.</summary>
[PublicAPI]
public sealed class Table: ScrollableItemsControl, IStyled<TableStyle>
{
    private readonly TablePresenter _presenter;
    private readonly StyleSlot<TableStyle> _style;
    private readonly List<TableRow> _sourceRows = [];
    private readonly HashSet<TableRow> _selectedRows = [];
    private readonly HashSet<TableCellReference> _selectedCells = [];
    private bool _isReordering;
    private long _progressiveSortVersion;
    private long _selectionVersion;
    private long _sortVersion;
    private TableRow? _selectionAnchorRow;
    private int _selectionAnchorColumn = -1;
    private TableEditState? _edit;

    // Tracks only the Selected-state Attributes/Underline/UnderlineColor baked into the active
    // theme's interactive row style set - never the Foreground/Background half, which always comes
    // from ActualStyle.SelectedTextColor/SelectedBackground in ResolveSelectionStyle below and is
    // already covered by TableStyle's own theme-change comparer. GetInteractiveRowStyleSet is a
    // pure function of Theme (no instance state), so this can be a static, shared dependency, unlike
    // ChartControlBase's per-instance one. Nothing but an explicit Theme value dependency ever
    // notices a swap that moves only these fields: ResolveSelectionStyle is called solely from
    // render paths, never through a tracked theme-change-impact hook.
    private static readonly ThemeValueDependency<(Face Selected, Face SelectedDisabled)> _selectionFaceThemeDependency = new(
        static theme =>
        {
            var states = theme.GetInteractiveRowStyleSet().ToAppearanceStates();
            return (
                states.Resolve(VisualState.Selected).Face,
                states.Resolve(VisualState.Selected | VisualState.Disabled).Face);
        },
        InvalidationImpact.Render);

    // Lazily rebuilt, not eagerly shifted on every mutation: ReorderRows tears the row list down
    // and rebuilds it through repeated single-row remove/insert calls, and an eagerly-shifted
    // index would cost O(rows) per call during that loop (O(rows^2) total for one reorder). A
    // dirty dictionary costs O(1) to invalidate per mutation and rebuilds once, on demand, the
    // next time navigation actually needs a row's position.
    private Dictionary<TableRow, int>? _rowIndexCache;

    #region Construction and properties

    /// <summary>Initializes empty mutable row and column collections.</summary>
    public Table()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch;
        IsFocusable = true;
        IsTabStop = true;
        TabNavigation = TabNavigation.None;
        Columns = new TableColumnCollection(this);
        Rows = new TableRowCollection(this);
        _presenter = new TablePresenter(this);
        InitializeScrollableItemsHost(_presenter);
        _style = InitializeStyle(TableStyle.Definition, OnStyleChanged);
        _ = AddHandler(Events.Key, OnKeyRouted, handledEventsToo: true);
        _ = AddHandler(Events.Pointer, OnPointerRouted, handledEventsToo: true);
        _presenter.ScrollChanged += OnPresenterScrollChanged;
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
            ArgumentOutOfRangeException.ThrowIfNotDefined(value, nameof(value), "The enum value is unknown.");
            VerifyMutable();

            if (field == value)
            {
                return;
            }

            // Only a same-kind transition (row<->row, cell<->cell) can retain a still-valid
            // selection; crossing the row/cell boundary, or moving to/from None, always clears.
            // Widening retains everything; narrowing keeps the first entry in display order,
            // mirroring ListView.SelectionMode and TreeView.SelectionMode.
            var wasRowKind = field is TableSelectionMode.Row or TableSelectionMode.MultipleRows;
            var isRowKind = value is TableSelectionMode.Row or TableSelectionMode.MultipleRows;
            var wasCellKind = field is TableSelectionMode.Cell or TableSelectionMode.MultipleCells;
            var isCellKind = value is TableSelectionMode.Cell or TableSelectionMode.MultipleCells;

            IEnumerable<TableRow> nextRows = [];
            IEnumerable<TableCellReference> nextCells = [];

            if (isRowKind && wasRowKind)
            {
                nextRows = value == TableSelectionMode.Row
                    ? Rows.Where(_selectedRows.Contains).Take(1).ToArray()
                    : _selectedRows;
            }
            else if (isCellKind && wasCellKind)
            {
                nextCells = value == TableSelectionMode.Cell
                    ? FindFirstSelectedCell() is { } first ? [first] : []
                    : _selectedCells;
            }

            _ = SetProperty(ref field, value, InvalidationImpact.Render);
            CommitSelection(nextRows, nextCells);

            // A retained row/cell may no longer be the one range-selection last anchored to
            // (e.g. narrowing drops every selected row but the first), so every mode change
            // starts a fresh anchor rather than keeping one range-select gestures still read.
            _selectionAnchorRow = null;
            _selectionAnchorColumn = -1;
        }
    } = TableSelectionMode.Row;

    /// <summary>Gets an immutable snapshot of the selected rows in current display order.</summary>
    public IReadOnlyList<TableRow> SelectedRows =>
        Array.AsReadOnly(Rows.Where(_selectedRows.Contains).ToArray());

    /// <summary>Gets an immutable snapshot of selected cells in current display row and column order.</summary>
    public IReadOnlyList<TableCellReference> SelectedCells =>
        Array.AsReadOnly(
            Rows.SelectMany(row => Enumerable.Range(0, row.Cells.Count)
                    .Select(column => new TableCellReference(row, column)))
                .Where(_selectedCells.Contains)
                .ToArray());

    /// <summary>Gets the active row used by keyboard navigation, or null when no row exists.</summary>
    public TableRow? ActiveRow { get; private set; }

    /// <summary>Gets the active zero-based cell column, or -1 when no cell is active.</summary>
    [ValueRange(-1, int.MaxValue)]
    public int ActiveColumnIndex { get; private set; } = -1;

    /// <summary>Gets the active cell reference, or null when navigation has no active cell.</summary>
    public TableCellReference? ActiveCell => ActiveRow is { } row && ActiveColumnIndex >= 0
        ? new TableCellReference(row, ActiveColumnIndex)
        : null;

    /// <summary>Gets whether one TextInput cell edit transaction is active.</summary>
    public bool IsEditing => _edit is not null;

    /// <summary>Gets the current sorted column, or -1 when sorting is reset.</summary>
    [ValueRange(-1, int.MaxValue)]
    public int SortColumnIndex { get; private set; } = -1;

    /// <summary>Gets the current sort direction.</summary>
    public TableSortDirection SortDirection { get; private set; }

    /// <summary>Raised after selected rows or cells commit.</summary>
    public event EventHandler<TableSelectionChangedEventArgs>? SelectionChanged;

    /// <summary>Raised after a row is activated by pointer or keyboard.</summary>
    public event EventHandler<TableRowInvokedEventArgs>? RowInvoked;

    /// <summary>Raised after <see cref="SortColumnIndex"/> or <see cref="SortDirection"/>
    /// actually changes - never for a no-op <see cref="SetSort"/>/<see cref="ResetSort"/> call
    /// that re-applies the current settings, and never merely because a row insertion or
    /// replacement re-splices the row order into an unchanged active sort.</summary>
    public event EventHandler<TableSortChangedEventArgs>? SortChanged;

    /// <summary>Gets or sets whether the titled header row is rendered.</summary>
    /// <exception cref="InvalidOperationException">The attached table is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The table is disposed.</exception>
    public bool ShowHeader
    {
        get;
        set
        {
            if (SetProperty(ref field, value, InvalidationImpact.Measure))
            {
                InvalidateRetainedDescendant(_presenter, InvalidationImpact.Measure);
            }
        }
    } = true;

    /// <summary>Gets or sets the complete local presentation, or null for theme ownership.</summary>
    /// <exception cref="InvalidOperationException">The attached table is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The table is disposed.</exception>
    public TableStyle? Style
    {
        get => _style.Local;
        set => _style.Local = value;
    }

    /// <summary>Gets the complete local, theme-owned, or code-owned presentation.</summary>
    public TableStyle ActualStyle => _style.Actual;

    /// <summary>Gets or sets non-negative cells between adjacent data rows.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    /// <exception cref="InvalidOperationException">The attached table is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The table is disposed.</exception>
    [NonNegativeValue]
    public int RowSpacing
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);

            if (SetProperty(ref field, value, InvalidationImpact.Measure))
            {
                InvalidateRetainedDescendant(_presenter, InvalidationImpact.Measure);
            }
        }
    }

    /// <summary>Gets or sets non-negative cells between adjacent columns.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    /// <exception cref="InvalidOperationException">The attached table is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The table is disposed.</exception>
    [NonNegativeValue]
    public int ColumnSpacing
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);

            if (SetProperty(ref field, value, InvalidationImpact.Measure))
            {
                InvalidateRetainedDescendant(_presenter, InvalidationImpact.Measure);
            }
        }
    }

    /// <summary>Gets or sets whether one-cell light grid lines are drawn in every available table gap.</summary>
    /// <exception cref="InvalidOperationException">The attached table is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The table is disposed.</exception>
    public bool ShowGridLines
    {
        get;
        set
        {
            if (SetProperty(ref field, value, InvalidationImpact.Measure))
            {
                InvalidateRetainedDescendant(_presenter, InvalidationImpact.Measure);
            }
        }
    } = true;

    /// <summary>Scrolls minimally to expose one row-cell descendant.</summary>
    /// <param name="descendant">The non-null descendant control.</param>
    /// <returns>
    /// True when the descendant's complete arranged bounds end up contained within this table's
    /// viewport, regardless of whether an offset actually changed to get there; false when clamping
    /// at an extent boundary leaves any part of it still outside.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="descendant"/> is null.</exception>
    /// <exception cref="ArgumentException">The control is not a realized table descendant.</exception>
    /// <exception cref="InvalidOperationException">The attached table is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The table is disposed.</exception>
    public bool BringIntoView(ControlBase descendant) => _presenter.BringIntoView(descendant);

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
        RequireNotProgressive("SelectRow is unavailable while the table is progressive; use SelectIndex or SelectKey instead.");
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

        var (next, nextAnchor) = SelectionGesture<TableRow>.Resolve(
            EqualityComparer<TableRow>.Default,
            _selectedRows,
            _selectionAnchorRow is not null,
            _selectionAnchorRow!,
            row,
            modifiers,
            SelectionMode == TableSelectionMode.MultipleRows,
            AddRowRange);

        _selectionAnchorRow = nextAnchor;
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
    public void SelectCell(
        TableRow row,
        [NonNegativeValue] int columnIndex,
        TerminalInput.Modifiers modifiers = TerminalInput.Modifiers.None)
    {
        ArgumentNullException.ThrowIfNull(row);
        RequireNotProgressive("SelectCell is unavailable while the table is progressive; use SelectIndex or SelectKey instead.");
        VerifyOwned(row);
        ArgumentOutOfRangeException.ThrowIfNegative(columnIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint) columnIndex, (uint) row.Cells.Count);
        SetActive(row, columnIndex);

        if (SelectionMode is TableSelectionMode.None or TableSelectionMode.Row or TableSelectionMode.MultipleRows)
        {
            return;
        }

        var reference = new TableCellReference(row, columnIndex);
        var hasAnchor = _selectionAnchorRow is not null;

        var (next, nextAnchor) = SelectionGesture<TableCellReference>.Resolve(
            EqualityComparer<TableCellReference>.Default,
            _selectedCells,
            hasAnchor,
            hasAnchor ? new TableCellReference(_selectionAnchorRow!, _selectionAnchorColumn) : default,
            reference,
            modifiers,
            SelectionMode == TableSelectionMode.MultipleCells,
            AddCellRange);

        _selectionAnchorRow = nextAnchor.Row;
        _selectionAnchorColumn = nextAnchor.ColumnIndex;
        CommitSelection([], next);
    }

    [Pure]
    private TableCellReference? FindFirstSelectedCell()
    {
        foreach (var row in Rows)
        {
            for (var column = 0; column < row.Cells.Count; column++)
            {
                var reference = new TableCellReference(row, column);

                if (_selectedCells.Contains(reference))
                {
                    return reference;
                }
            }
        }

        return null;
    }

    /// <summary>Clears all selected rows and cells while retaining the active location.</summary>
    /// <exception cref="InvalidOperationException">The attached table is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The table is disposed.</exception>
    public void ClearSelection()
    {
        VerifyMutable();

        if (IsProgressive)
        {
            Progressive!.ClearSelection();
            return;
        }

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

        if (IsProgressive)
        {
            Progressive!.SelectAllKnown(SelectionMode);
            return;
        }

        if (Rows.Count == 0 || SelectionMode == TableSelectionMode.None)
        {
            ClearSelection();
            return;
        }

        if (SelectionMode == TableSelectionMode.MultipleRows)
        {
            CommitSelection([.. Rows], []);
        }
        else if (SelectionMode == TableSelectionMode.Row)
        {
            CommitSelection([Rows[0]], []);
        }
        else if (SelectionMode == TableSelectionMode.MultipleCells)
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
        else
        {
            CommitSelection([], [new TableCellReference(Rows[0], 0)]);
        }

        SetActive(Rows[0], 0);
    }

    /// <summary>Cycles one column through ascending, descending, and reset ordering.</summary>
    /// <param name="columnIndex">The zero-based column to sort.</param>
    /// <exception cref="ArgumentOutOfRangeException">The column index is outside the collection.</exception>
    /// <exception cref="InvalidOperationException">The attached table is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The table is disposed.</exception>
    public void SortBy([NonNegativeValue] int columnIndex)
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
        RequireNotProgressive("Sorting is unavailable while the table is progressive; the data source owns sort order.");
        ArgumentOutOfRangeException.ThrowIfNotDefined(direction, nameof(direction), "The enum value is unknown.");

        if (direction == TableSortDirection.None)
        {
            if (columnIndex is not -1 && (columnIndex < 0 || columnIndex >= Columns.Count))
            {
                throw new ArgumentOutOfRangeException(nameof(columnIndex));
            }

            var resetColumnChanged = SortColumnIndex != -1;
            var resetDirectionChanged = SortDirection != TableSortDirection.None;
            var resetSortVersion = resetColumnChanged || resetDirectionChanged
                ? ++_sortVersion
                : _sortVersion;
            SortColumnIndex = -1;
            SortDirection = TableSortDirection.None;
            ReorderRows(_sourceRows);

            // Render, not None: the header chrome draws a direction glyph in the sorted column,
            // so clearing the sort must repaint even when ReorderRows finds the row order already
            // matches insertion order and skips its own invalidation.
            if (resetColumnChanged)
            {
                NotifyPropertyChanged(nameof(SortColumnIndex), InvalidationImpact.Render);

                if (_sortVersion != resetSortVersion)
                {
                    return;
                }
            }

            if (resetDirectionChanged)
            {
                NotifyPropertyChanged(nameof(SortDirection), InvalidationImpact.Render);

                if (_sortVersion != resetSortVersion)
                {
                    return;
                }
            }

            // SortChanged reports a real change to the sort settings, not merely that
            // SetSort/ResetSort was called - gated the same way the property notifications
            // above already are, instead of firing unconditionally on every no-op reset.
            if (resetColumnChanged || resetDirectionChanged)
            {
                SortChanged?.Invoke(this, new TableSortChangedEventArgs(-1, TableSortDirection.None));
            }

            return;
        }

        ArgumentOutOfRangeException.ThrowIfNegative(columnIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint) columnIndex, (uint) Columns.Count);
        var columnChanged = SortColumnIndex != columnIndex;
        var directionChanged = SortDirection != direction;
        var sortVersion = columnChanged || directionChanged ? ++_sortVersion : _sortVersion;
        SortColumnIndex = columnIndex;
        SortDirection = direction;

        // _sourceRows.IndexOf costs O(n) per row, turning this tie-break into an O(n^2) scan on
        // top of the O(n log n) sort; a position precomputed once resolves the same tie-break in
        // O(1) per comparison.
        var position = new Dictionary<TableRow, int>(_sourceRows.Count);

        for (var index = 0; index < _sourceRows.Count; index++)
        {
            position[_sourceRows[index]] = index;
        }

        var comparer = Comparer<object?>.Create(CompareKeys);
        TableRow[] ordered = direction == TableSortDirection.Descending
            ? [.. Rows.OrderByDescending(row => GetSortKey(row, columnIndex), comparer).ThenBy(row => position[row])]
            : [.. Rows.OrderBy(row => GetSortKey(row, columnIndex), comparer).ThenBy(row => position[row])];

        ReorderRows(ordered);

        // Render, not None: see the matching comment in the reset branch above - the header
        // glyph must repaint even on the rare reorder that leaves row order unchanged.
        if (columnChanged)
        {
            NotifyPropertyChanged(nameof(SortColumnIndex), InvalidationImpact.Render);

            if (_sortVersion != sortVersion)
            {
                return;
            }
        }

        if (directionChanged)
        {
            NotifyPropertyChanged(nameof(SortDirection), InvalidationImpact.Render);

            if (_sortVersion != sortVersion)
            {
                return;
            }
        }

        // SortChanged reports a real change to the sort settings, not merely that SetSort was
        // called - gated the same way the property notifications above already are, instead of
        // firing unconditionally on every no-op re-application of the current column and
        // direction.
        if (columnChanged || directionChanged)
        {
            SortChanged?.Invoke(this, new TableSortChangedEventArgs(columnIndex, direction));
        }
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
    public bool BeginEdit(TableRow row, [NonNegativeValue] int columnIndex)
    {
        ArgumentNullException.ThrowIfNull(row);

        // Progressive v1 is read-only: no cell in a progressive table is ever an editable owned
        // row, so this reports the same false a read-only/non-TextInput cell already reports below.
        if (IsProgressive)
        {
            return false;
        }

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
        var dispatcher = Dispatcher;
        var edit = new TableEditState(row, columnIndex, editor, editor.Text);
        _edit = edit;
        editor.Submitted += OnEditorSubmitted;
        _ = editor.Focus();

        if (!CanContinueAfterFocus(dispatcher) ||
            !ReferenceEquals(_edit, edit) ||
            editor.IsDisposed ||
            !Rows.Contains(row))
        {
            if (ReferenceEquals(_edit, edit))
            {
                if (!editor.IsDisposed)
                {
                    editor.Submitted -= OnEditorSubmitted;
                }

                _edit = null;
            }

            return false;
        }

        editor.Select(0, editor.Text.Length);
        NotifyPropertyChanged(nameof(IsEditing), InvalidationImpact.None);
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
        NotifyPropertyChanged(nameof(IsEditing), InvalidationImpact.None);

        if (!IsDisposed && !edit.Editor.IsDisposed && Rows.Contains(edit.Row))
        {
            SetActive(edit.Row, edit.ColumnIndex);
        }

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
    [Pure]
    public string CopySelection()
    {
        if (IsProgressive)
        {
            return CopyProgressiveSelection();
        }

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

    // CopySelection is a synchronous read of already-realized state - it never fetches an
    // unloaded selected key, it simply skips it, so the call can never block the dispatcher.
    [Pure]
    private string CopyProgressiveSelection()
    {
        if (Progressive is not { } controller)
        {
            return string.Empty;
        }

        var lines = controller.CopyLoadedSelection()
            .Select(entry => string.Join('\t', entry.Row.Cells.Select(CellText)));
        return string.Join('\n', lines);
    }

    #endregion

    #region Layout and rendering

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
        => MeasureChild(_presenter, constraint);

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds)
    {
        var previousHeight = Progressive?.RowHeight;
        var previousOffset = VerticalOffset;
        ArrangeChild(_presenter, bounds, ResolvedAxes.Both);

        if (Progressive is { } controller)
        {
            for (var pass = 0; pass < 2; pass++)
            {
                var dataViewportHeight = Math.Max(0, Viewport.Height - _presenter.ProgressiveHeaderHeight);

                if (!controller.ResolveRowHeight(dataViewportHeight))
                {
                    break;
                }

                _ = MeasureChild(_presenter, new Constraint(bounds.Width, bounds.Height));
                ArrangeChild(_presenter, bounds, ResolvedAxes.Both);
            }

            if (previousHeight is int height && height != controller.RowHeight)
            {
                var headerHeight = _presenter.ProgressiveHeaderHeight;
                var contentOffset = Math.Max(0, previousOffset - headerHeight);
                var mapped = UniformRowHeight.RemapOffset(
                    contentOffset,
                    height,
                    controller.RowHeight,
                    _presenter.RowGap);
                var target = previousOffset < headerHeight ? previousOffset : headerHeight.Add(mapped);
                var maximum = Math.Max(0, Extent.Height - Viewport.Height);
                _ = _presenter.ScrollByKnownMaximum(target - VerticalOffset, maximum, ScrollCause.Resize);
            }
        }

        // The presenter's own arrange transaction has already closed by this point - reconciling
        // here catches every case that changed viewport, offset, or extent without a genuine
        // interactive scroll (first layout, resize, column churn), matching ListView.ArrangeOverride's
        // equivalent unconditional tail call to Rewindow().
        ProgressiveRewindow();
    }

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas) => _presenter.RenderTableChrome(canvas);

    /// <inheritdoc/>
    protected override void OnAttached()
    {
        base.OnAttached();

        // A table reattached at the same arranged size can have nothing left for the layout
        // system to redo, so ArrangeOverride's own unconditional ProgressiveRewindow() call is not
        // guaranteed to run again - reconciling here directly, independent of whether a fresh
        // arrange pass happens to follow, is what actually resumes a progressive table whose
        // in-flight fetches were canceled by the matching OnUnavailable(Detached) below.
        ProgressiveRewindow();
    }

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        base.OnUnavailable(reason);

        // A table that merely left the tree keeps its progressive source, cache, and selection -
        // only genuinely in-flight fetches are cancelled, matching Dispatcher.Hold's contract that
        // no work outlives the control that requested it. The resulting load-state transition remains
        // observable because a detached table is still live and can be reattached.
        if (reason == ReleaseReason.Detached)
        {
            Progressive?.CancelInFlight();
        }

        if (reason == ReleaseReason.Disposed)
        {
            var controller = Progressive;
            if (controller is not null)
            {
                // Owner disposal cancels pending work without publishing a final load-state change.
                // Sibling controls subscribed to the table may already have been disposed as part of
                // the same surface teardown, so no public callback may escape this ownership boundary.
                controller.LoadStateChanged -= OnControllerLoadStateChanged;
                controller.LoadFailed -= OnControllerLoadFailed;
                controller.SelectionChanged -= OnControllerSelectionChanged;
                controller.DisposeWithOwner();
            }

            Progressive = null;
            LoadStateChanged = null;
            LoadFailed = null;
            SortRequested = null;
        }
    }

    /// <summary>Gets the current state snapshot for private table chrome resolution.</summary>
    internal VisualState CurrentVisualState => GetAppearanceState();

    /// <summary>Gets the terminal-safe horizontal grid glyph for the current theme and cell policy.</summary>
    internal Rune ResolvedHorizontalGridGlyph => ResolveGridGlyph(ActualStyle.Glyphs.HorizontalGlyph);

    /// <summary>Gets the terminal-safe vertical grid glyph for the current theme and cell policy.</summary>
    internal Rune ResolvedVerticalGridGlyph => ResolveGridGlyph(ActualStyle.Glyphs.VerticalGlyph);

    /// <summary>Gets the terminal-safe grid-intersection glyph for the current theme and cell policy.</summary>
    internal Rune ResolvedCrossGridGlyph => ResolveGridGlyph(ActualStyle.Glyphs.CrossGlyph);

    /// <summary>Gets the terminal-safe ascending sort indicator for the current theme and cell policy.</summary>
    internal Rune ResolvedSortAscendingGlyph => ResolveGridGlyph(ActualStyle.Glyphs.SortAscendingGlyph);

    /// <summary>Gets the terminal-safe descending sort indicator for the current theme and cell policy.</summary>
    internal Rune ResolvedSortDescendingGlyph => ResolveGridGlyph(ActualStyle.Glyphs.SortDescendingGlyph);

    /// <summary>Gets the terminal-safe progressive-loading placeholder glyph for the current theme
    /// and cell policy.</summary>
    internal Rune ResolvedPlaceholderGlyph => ResolveGridGlyph(ActualStyle.Glyphs.PlaceholderGlyph);

    /// <summary>Gets the terminal-safe progressive load-failure placeholder glyph for the current
    /// theme and cell policy.</summary>
    internal Rune ResolvedPlaceholderErrorGlyph => ResolveGridGlyph(ActualStyle.Glyphs.PlaceholderErrorGlyph);

    #endregion

    #region Progressive loading

    /// <summary>Gets or sets the private progressive controller, or null in eager mode.</summary>
    private TableDataController? Progressive { get; set; }

    /// <summary>Gets whether this table is bound to a progressive data source through
    /// <see cref="SetDataSource{T}"/>.</summary>
    public bool IsProgressive => Progressive is not null;

    /// <summary>Gets the table-wide aggregate progressive loading state, or
    /// <see cref="TableLoadState.Idle"/> while not progressive.</summary>
    public TableLoadState LoadState => Progressive?.LoadState ?? TableLoadState.Idle;

    /// <summary>Raised after <see cref="LoadState"/> actually changes while the table is live.
    /// Disposal settles progressive work without publishing a transition.</summary>
    public event EventHandler<TableLoadStateChangedEventArgs>? LoadStateChanged;

    /// <summary>Raised after one progressive range exhausts its bounded retry attempts.</summary>
    public event EventHandler<TableLoadFailedEventArgs>? LoadFailed;

    /// <summary>Raised after a sortable column header is clicked while progressive. Table has
    /// already cycled and committed its own <see cref="SortColumnIndex"/>/<see cref="SortDirection"/>
    /// indicator state the same way <see cref="SortBy"/> does, and calls <see cref="Reload"/>
    /// immediately after raising this event - progressive sorting is entirely source-side, so no
    /// row is ever reordered here. A subscriber reconfigures its
    /// <see cref="ITableDataSource{T}"/> query to honor the reported column and direction before
    /// this handler returns, so the <see cref="Reload"/> that follows re-fetches under whatever
    /// order the source applies next.</summary>
    public event EventHandler<TableSortChangedEventArgs>? SortRequested;

    /// <summary>Gets the active progressive navigation index, or -1 while not progressive, when
    /// nothing is active, or while an unresolved <see cref="ActiveKey"/> awaits its row.</summary>
    [ValueRange(-1, int.MaxValue)]
    public int ActiveIndex => Progressive?.ActiveIndex ?? -1;

    /// <summary>Gets the active progressive navigation key, or null while not progressive, when
    /// nothing is active, or while an unloaded <see cref="ActiveIndex"/> awaits its row.</summary>
    public object? ActiveKey => Progressive?.ActiveKey;

    /// <summary>Gets an immutable snapshot of selected keys, independent of cache or window state,
    /// or an empty snapshot while not progressive.</summary>
    public IReadOnlyList<object> SelectedKeys => Progressive?.SelectedKeys ?? [];

    /// <summary>Binds this table to one progressive data source, replacing any prior mode.</summary>
    /// <typeparam name="T">The item type loaded from <paramref name="source"/>.</typeparam>
    /// <param name="source">The non-null data source.</param>
    /// <param name="rowTemplate">The non-null row template, invoked only on the dispatcher.</param>
    /// <param name="rowHeight">The positive fixed or viewport-relative uniform row height.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="rowTemplate"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="rowHeight"/> is a zero fixed or percentage length.</exception>
    /// <exception cref="ArgumentException"><paramref name="rowHeight"/> is automatic or proportional, or a defined column uses <see cref="LengthKind.Auto"/> width.</exception>
    /// <exception cref="InvalidOperationException">
    /// <see cref="Rows"/> is non-empty, <see cref="SelectionMode"/> is a cell mode, or the table is
    /// mutated off-dispatcher or without an attached dispatcher.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The table is disposed.</exception>
    public void SetDataSource<T>(
        ITableDataSource<T> source,
        TableRowTemplate<T> rowTemplate,
        Length rowHeight)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(rowTemplate);
        UniformRowHeight.Validate(rowHeight, allowAuto: false, nameof(rowHeight));
        VerifyMutable();

        if (Dispatcher is null)
        {
            throw new InvalidOperationException(
                "SetDataSource requires the table to be attached to a running dispatcher.");
        }

        Dispatcher.VerifyAccess();

        if (Rows.Count != 0)
        {
            throw new InvalidOperationException("SetDataSource requires an empty Rows collection.");
        }

        if (SelectionMode is not (TableSelectionMode.None or TableSelectionMode.Row or TableSelectionMode.MultipleRows))
        {
            throw new InvalidOperationException(
                "SetDataSource requires SelectionMode to be None, Row, or MultipleRows.");
        }

        foreach (var column in Columns)
        {
            if (column.Width.Kind == LengthKind.Auto)
            {
                throw new ArgumentException(
                    "A progressive table cannot define an automatic-width column.",
                    nameof(source));
            }
        }

        // Build the new adapter/controller fully before tearing down any previous one, so a
        // rejected candidate leaves the current mode untouched.
        var adapter = new TableDataAdapter<T>(this, source, rowTemplate);
        var controller = new TableDataController(this, _presenter, adapter, rowHeight);
        var previous = Progressive;
        Progressive = controller;

        if (previous is not null)
        {
            previous.LoadStateChanged -= OnControllerLoadStateChanged;
            previous.LoadFailed -= OnControllerLoadFailed;
            previous.SelectionChanged -= OnControllerSelectionChanged;
            previous.Dispose();
        }

        controller.LoadStateChanged += OnControllerLoadStateChanged;
        controller.LoadFailed += OnControllerLoadFailed;
        controller.SelectionChanged += OnControllerSelectionChanged;
        NotifyPropertyChanged(nameof(IsProgressive), InvalidationImpact.Measure);
        ProgressiveRewindow();
    }

    /// <summary>Detaches any progressive data source and returns to empty eager <see cref="Rows"/>.</summary>
    /// <exception cref="InvalidOperationException">The attached table is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The table is disposed.</exception>
    public void ClearDataSource()
    {
        VerifyMutable();

        if (Progressive is not { } controller)
        {
            return;
        }

        controller.LoadStateChanged -= OnControllerLoadStateChanged;
        controller.LoadFailed -= OnControllerLoadFailed;
        controller.SelectionChanged -= OnControllerSelectionChanged;
        Progressive = null;
        controller.Dispose();
        NotifyPropertyChanged(nameof(IsProgressive), InvalidationImpact.Measure);
    }

    /// <summary>Discards cached progressive data and reloads the active window from the current source.</summary>
    /// <exception cref="InvalidOperationException">The table is not progressive, or is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The table is disposed.</exception>
    public void Reload()
    {
        VerifyMutable();
        RequireProgressive();
        Progressive!.Reload();
        ProgressiveRewindow();
    }

    /// <summary>Moves the active progressive index, clears its key until an unloaded row resolves,
    /// and applies a key-based selection gesture when the key is known.</summary>
    /// <param name="index">The non-negative candidate logical index.</param>
    /// <param name="modifiers">Optional control/shift selection modifiers.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is negative.</exception>
    /// <exception cref="InvalidOperationException">The table is not progressive, or is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The table is disposed.</exception>
    public void SelectIndex(int index, TerminalInput.Modifiers modifiers = TerminalInput.Modifiers.None)
    {
        VerifyMutable();
        RequireProgressive();
        Progressive!.SelectIndex(index, modifiers, SelectionMode);
    }

    /// <summary>Applies a key-based selection gesture directly by stable key and clears its index
    /// until an unresolved key's row loads.</summary>
    /// <param name="key">The non-null candidate key.</param>
    /// <param name="modifiers">Optional control/shift selection modifiers.</param>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The table is not progressive, or is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The table is disposed.</exception>
    public void SelectKey(object key, TerminalInput.Modifiers modifiers = TerminalInput.Modifiers.None)
    {
        VerifyMutable();
        RequireProgressive();
        Progressive!.SelectKey(key, modifiers, SelectionMode);
    }

    /// <summary>Gets the private progressive controller for internal presenter and test cooperation.</summary>
    internal TableDataController? ProgressiveController => Progressive;

    /// <summary>Validates a template-built row using the same checks applied to an eager row.</summary>
    /// <param name="row">The candidate row.</param>
    internal void ValidateProgressiveRow(TableRow row) => ValidateRow(row);

    /// <summary>Throws unless this table is currently progressive.</summary>
    internal void RequireProgressive()
    {
        if (Progressive is null)
        {
            throw new InvalidOperationException("This member requires a progressive table; call SetDataSource first.");
        }
    }

    /// <summary>Throws when this table is currently progressive.</summary>
    internal void RequireNotProgressive(string message)
    {
        if (Progressive is not null)
        {
            throw new InvalidOperationException(message);
        }
    }

    /// <summary>Resolves the final theme-aware selection style used by the private presenter.</summary>
    /// <param name="theme">The non-null active theme.</param>
    /// <param name="state">The selected state, optionally including disabled.</param>
    /// <returns>The complete terminal cell style.</returns>
    internal TerminalStyle ResolveSelectionStyle(Theme theme, VisualState state)
    {
        ArgumentNullException.ThrowIfNull(theme);

        // Registers this control's dependency on the theme-authored Selected-state Attributes/
        // Underline/UnderlineColor, so GetThemeChangeImpact notices a theme swap that only moves
        // those fields even though every field TableStyle's own comparer diffs
        // (SelectedTextColor/SelectedBackground) stayed equal. ResolveThemeValue's returned value
        // is intentionally unused here - the overlay-composed value below stays the source of truth.
        _ = ResolveThemeValue(_selectionFaceThemeDependency);
        var overlay = new AppearanceStatesOverlay(
            selected: new AppearanceOverlay(
                face: new FaceOverlay(
                    foreground: ActualStyle.SelectedTextColor,
                    background: ActualStyle.SelectedBackground)));
        var selected = theme.GetInteractiveRowStyleSet()
            .ToAppearanceStates()
            .Compose(overlay)
            .Resolve(state)
            .Face;
        return new TerminalStyle(
            ResolveColor(selected.Foreground, theme),
            ResolveColor(selected.Background, theme),
            selected.Attributes.Resolve(theme),
            underline: selected.Underline,
            underlineColor: ResolveColor(selected.UnderlineColor, theme));
    }

    private void OnStyleChanged(TableStyle previous, TableStyle current)
    {
        _ = previous;
        _ = current;
        _presenter?.Invalidate(Invalidation.Render);
    }

    /// <summary>Reconciles the progressive window against the current scroll offset and viewport, a
    /// no-op while not progressive. Called from <see cref="ArrangeOverride"/> and, skipping
    /// <see cref="ScrollCause.Content"/>/<see cref="ScrollCause.Resize"/>, from the presenter's own
    /// <see cref="Container.ScrollChanged"/> - matching <c>ListView.Rewindow</c>'s equivalent skip, since both
    /// causes fire from inside an already-open arrange transaction.</summary>
    internal void ProgressiveRewindow()
    {
        if (Progressive is not { } controller)
        {
            return;
        }

        controller.Rewindow(Viewport.Height, VerticalOffset);
    }

    /// <summary>Gets the attachment generation used to reject progressive callbacks queued by a
    /// dispatcher that no longer owns this table.</summary>
    /// <summary>Runs the ordinary progressive sort transaction for tests that must initiate a
    /// newer request synchronously from the public sort callback.</summary>
    /// <param name="columnIndex">The validated progressive column index.</param>
    internal void RequestProgressiveSortForLifecycleTest(int columnIndex) => RequestProgressiveSort(columnIndex);

    // Cycles ascending/descending/reset for the clicked column using exactly SortBy's cycle, then
    // commits the same SortColumnIndex/SortDirection indicator state SetSort commits - but never
    // calls ReorderRows, since a progressive table has no owned Rows to reorder. The data source is
    // the one place the actual reorder happens, driven by the SortRequested subscriber.
    private void RequestProgressiveSort(int columnIndex)
    {
        VerifyMutable();
        var controller = Progressive;

        if (controller is null)
        {
            return;
        }

        var version = ++_progressiveSortVersion;

        var direction = SortColumnIndex != columnIndex
            ? TableSortDirection.Ascending
            : SortDirection switch
            {
                TableSortDirection.None => TableSortDirection.Ascending,
                TableSortDirection.Ascending => TableSortDirection.Descending,
                TableSortDirection.Descending => TableSortDirection.None,
                _ => throw new UnreachableException()
            };

        var resolvedColumn = direction == TableSortDirection.None ? -1 : columnIndex;
        var columnChanged = SortColumnIndex != resolvedColumn;
        var directionChanged = SortDirection != direction;
        SortColumnIndex = resolvedColumn;
        SortDirection = direction;

        if (columnChanged)
        {
            NotifyPropertyChanged(nameof(SortColumnIndex), InvalidationImpact.Render);
        }

        if (directionChanged)
        {
            NotifyPropertyChanged(nameof(SortDirection), InvalidationImpact.Render);
        }

        if (_progressiveSortVersion != version || !ReferenceEquals(Progressive, controller))
        {
            return;
        }

        SortRequested?.Invoke(this, new TableSortChangedEventArgs(resolvedColumn, direction));

        if (_progressiveSortVersion == version && ReferenceEquals(Progressive, controller))
        {
            controller.Reload();
        }
    }

    private void OnPresenterScrollChanged(object? sender, ScrollChangedEventArgs eventArgs)
    {
        _ = sender;

        if (eventArgs.Cause is ScrollCause.Content or ScrollCause.Resize)
        {
            return;
        }

        ProgressiveRewindow();
    }

    private void OnControllerLoadStateChanged(object? sender, TableLoadStateChangedEventArgs eventArgs)
    {
        _ = sender;
        LoadStateChanged?.Invoke(this, eventArgs);
    }

    private void OnControllerLoadFailed(object? sender, TableLoadFailedEventArgs eventArgs)
    {
        _ = sender;
        LoadFailed?.Invoke(this, eventArgs);
    }

    // The controller only ever raises this after ActiveIndex, ActiveKey, or the selected-key set
    // actually changed, so every property below is safe to announce unconditionally alongside the
    // shared SelectionChanged event - the same event eager SelectRow/SelectCell/CommitSelection
    // already raise, now also reachable from SelectIndex/SelectKey/SelectAllKnown/ClearSelection.
    private void OnControllerSelectionChanged(object? sender, TableSelectionChangedEventArgs eventArgs)
    {
        _ = sender;
        SelectionChanged?.Invoke(this, eventArgs);

        // SelectionChanged (and each NotifyPropertyChanged call below) can synchronously reach a
        // subscriber that disposes the table - re-check before every further disposed-guarded call.
        if (IsDisposed)
        {
            return;
        }

        NotifyPropertyChanged(nameof(ActiveIndex), InvalidationImpact.None);

        if (IsDisposed)
        {
            return;
        }

        NotifyPropertyChanged(nameof(ActiveKey), InvalidationImpact.None);

        if (IsDisposed)
        {
            return;
        }

        NotifyPropertyChanged(nameof(SelectedKeys), InvalidationImpact.None);
    }

    #endregion

    #region Selection and input

    private void OnKeyRouted(object? sender, KeyEventArgs eventArgs)
    {
        _ = sender;

        if (eventArgs.IsHandled ||
            eventArgs.Phase != RoutingPhase.Preview ||
            !eventArgs.IsKeyDown)
        {
            return;
        }

        var stroke = eventArgs.Stroke;

        if (_edit is not null)
        {
            if (stroke.Code == TerminalInput.Code.Enter)
            {
                if (eventArgs.IsInitialKeyDown)
                {
                    _ = CommitEdit();
                }

                eventArgs.IsHandled = true;
                return;
            }

            if (stroke.Code == TerminalInput.Code.Escape &&
                stroke.Modifiers.IsActivationEligible())
            {
                if (eventArgs.IsInitialKeyDown)
                {
                    _ = CancelEdit();
                }

                eventArgs.IsHandled = true;
                return;
            }

            if (stroke.Code == TerminalInput.Code.Tab)
            {
                if (eventArgs.IsInitialKeyDown)
                {
                    _ = CommitEdit();
                    _ = MoveActive(0, (stroke.Modifiers & TerminalInput.Modifiers.Shift) != 0 ? -1 : 1);
                }

                eventArgs.IsHandled = true;
            }

            return;
        }

        if (stroke.Code == TerminalInput.Code.F2)
        {
            eventArgs.IsHandled = !eventArgs.IsInitialKeyDown ||
                (ActiveCell is { } cell && BeginEdit(cell.Row, cell.ColumnIndex));
            return;
        }

        if (stroke.Code == TerminalInput.Code.Character &&
            stroke.Character is { } character &&
            KeyboardModifierPolicy.MatchesCommand(
                stroke.Modifiers,
                TerminalInput.Modifiers.Control) &&
            Rune.ToLowerInvariant(character) == new Rune('a'))
        {
            if (eventArgs.IsInitialKeyDown)
            {
                SelectAll();
            }

            eventArgs.IsHandled = true;
            return;
        }

        if (IsProgressive)
        {
            eventArgs.IsHandled = (stroke.Code == TerminalInput.Code.Enter && !eventArgs.IsInitialKeyDown) ||
                HandleProgressiveKey(stroke.Code, stroke.Modifiers);
            return;
        }

        if (stroke.Code is not (TerminalInput.Code.Up or TerminalInput.Code.Down or
            TerminalInput.Code.Left or TerminalInput.Code.Right or TerminalInput.Code.Home or
            TerminalInput.Code.End or TerminalInput.Code.PageUp or TerminalInput.Code.PageDown))
        {
            if (stroke.Code == TerminalInput.Code.Enter)
            {
                eventArgs.IsHandled = !eventArgs.IsInitialKeyDown ||
                    (stroke.Modifiers.IsActivationEligible() && ActivateCurrent());
            }

            return;
        }

        if (!KeyboardModifierPolicy.IsScalarNavigationEligible(stroke.Modifiers) &&
            !KeyboardModifierPolicy.MatchesCommand(stroke.Modifiers, TerminalInput.Modifiers.Shift))
        {
            return;
        }

        _ = stroke.Code == TerminalInput.Code.Up
            ? MoveActive(-1, 0, stroke.Modifiers)
            : stroke.Code == TerminalInput.Code.Down
                ? MoveActive(1, 0, stroke.Modifiers)
                : stroke.Code == TerminalInput.Code.Left
                    ? MoveActive(0, -1, stroke.Modifiers)
                    : stroke.Code == TerminalInput.Code.Right
                        ? MoveActive(0, 1, stroke.Modifiers)
                        : stroke.Code == TerminalInput.Code.Home
                            ? MoveToEndpoint(first: true, stroke.Modifiers)
                            : stroke.Code == TerminalInput.Code.End
                                ? MoveToEndpoint(first: false, stroke.Modifiers)
                                : stroke.Code == TerminalInput.Code.PageUp
                                    ? MovePage(-1, stroke.Modifiers)
                                    : stroke.Code == TerminalInput.Code.PageDown && MovePage(1, stroke.Modifiers);

        // IsHandled whenever the table has rows and columns to navigate, even when the active cell
        // is already at the boundary and cannot move further - otherwise the keystroke escapes to
        // page or scroll an enclosing scrollable container out from under the still-focused table,
        // mirroring TreeView/NavigationView's equivalent fix.
        eventArgs.IsHandled = Rows.Count > 0 && Columns.Count > 0;
    }

    private void OnPointerRouted(object? sender, PointerEventArgs eventArgs)
    {
        _ = sender;

        if (eventArgs.IsHandled ||
            eventArgs.Phase != RoutingPhase.Preview ||
            eventArgs.Pointer.Action != TerminalInput.PointerAction.Press ||
            (eventArgs.Pointer.Buttons & TerminalInput.Buttons.Primary) == 0 ||
            eventArgs.Pointer.Cells is not { } point)
        {
            return;
        }

        if (_presenter.TryGetHeaderColumn(point, out var headerColumn))
        {
            // The data source, not Table, owns sort order while progressive: the header click still
            // cycles and commits the visible sort indicator and asks the application to resort via
            // SortRequested, but no row is ever reordered here.
            if (IsProgressive)
            {
                RequestProgressiveSort(headerColumn);
                return;
            }

            // Sorting reorders through Rows.Clear() + re-add rather than moving rows in place, so
            // an in-progress edit must be committed here — the same as the ordinary
            // click-elsewhere check below — instead of being silently reverted when the edited
            // row's removal cancels it.
            _ = CommitEdit();
            SortBy(headerColumn);
            return;
        }

        if (IsProgressive)
        {
            OnProgressivePointerRouted(point, eventArgs.Pointer.Modifiers);
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
        var dispatcher = Dispatcher;

        if (SelectionMode is TableSelectionMode.Row or TableSelectionMode.MultipleRows)
        {
            SelectRow(row, modifiers);

            if (!IsPointerTargetAvailable(row, column, dispatcher))
            {
                return;
            }

            SetActive(row, column);
        }
        else
        {
            SelectCell(row, column, modifiers);
        }

        if (!IsPointerTargetAvailable(row, column, dispatcher))
        {
            return;
        }

        if (eventArgs.ClickCount >= 2)
        {
            _ = BeginEdit(row, column);

            if (!IsPointerTargetAvailable(row, column, dispatcher))
            {
                return;
            }
        }

        // The press itself already moved keyboard focus to the deepest focusable control under
        // the pointer, which for a TextInput cell is the editor (or one of its own parts) - and a
        // focused editor accepts typed text directly, silently bypassing the edit transaction that
        // CommitEdit, CancelEdit, and Escape's restore-original-text are built around. Only
        // BeginEdit may hand focus to an editor, so a press that did not open an edit hands focus
        // back to the table.
        if (_edit is null && row.Cells[column] is TextInput { ContainsFocus: true })
        {
            _ = Focus();

            if (!IsPointerTargetAvailable(row, column, dispatcher))
            {
                return;
            }
        }

        RowInvoked?.Invoke(this, new TableRowInvokedEventArgs(row, Rows.IndexOf(row), ActivationCause.Pointer));
    }

    [Pure]
    private bool IsPointerTargetAvailable(
        TableRow row,
        [NonNegativeValue] int columnIndex,
        Dispatcher? dispatcher) =>
        ReferenceEquals(Dispatcher, dispatcher) && IsRowCellLive(row, columnIndex);

    // Shared liveness/ownership/bounds check for a row/column pair that a caller captured before
    // publishing a notification a subscriber could react to synchronously - the dispatcher-identity
    // check above is pointer-transaction-specific and does not belong here, but every other
    // condition applies equally to the keyboard navigation paths below.
    [Pure]
    private bool IsRowCellLive(TableRow row, [NonNegativeValue] int columnIndex) =>
        !IsDisposed &&
        EffectiveIsVisible &&
        EffectiveIsEnabled &&
        Rows.IndexOf(row) >= 0 &&
        columnIndex >= 0 &&
        columnIndex < Columns.Count &&
        columnIndex < row.Cells.Count &&
        !row.Cells[columnIndex].IsDisposed;

    // The pointer's Shift/Control modifiers drive the same range and toggle gestures the eager
    // path hands to SelectRow - dropping them here silently degraded every modified click to a
    // plain single selection while progressive.
    private void OnProgressivePointerRouted(Point point, TerminalInput.Modifiers modifiers)
    {
        if (Progressive is not { } controller || !controller.TryResolvePoint(point, out var index, out _))
        {
            return;
        }

        var dispatcher = Dispatcher;
        SelectIndex(index, modifiers);

        if (!IsDisposed &&
            ReferenceEquals(Dispatcher, dispatcher) &&
            EffectiveIsVisible &&
            EffectiveIsEnabled &&
            ReferenceEquals(Progressive, controller) &&
            !controller.IsPlaceholder(index) &&
            controller.RowAt(index) is { } row)
        {
            RowInvoked?.Invoke(this, new TableRowInvokedEventArgs(row, index, ActivationCause.Pointer));
        }
    }

    // Every branch below moves ActiveIndex/selection synchronously by arithmetic alone - never
    // blocking the dispatcher on a fetch, matching ListView's virtualized navigation contract.
    private bool HandleProgressiveKey(TerminalInput.Code code, TerminalInput.Modifiers modifiers)
    {
        if (Progressive is not { } controller || Columns.Count == 0)
        {
            return false;
        }

        if (code != TerminalInput.Code.Enter &&
            !KeyboardModifierPolicy.IsScalarNavigationEligible(modifiers) &&
            !KeyboardModifierPolicy.MatchesCommand(modifiers, TerminalInput.Modifiers.Shift))
        {
            return false;
        }

        var logicalCount = controller.LogicalCount;

        if (logicalCount == 0)
        {
            return false;
        }

        var current = controller.ActiveIndex < 0 ? 0 : controller.ActiveIndex;

        var target = code == TerminalInput.Code.Up
            ? Math.Max(0, current - 1)
            : code == TerminalInput.Code.Down
                ? Math.Min(logicalCount - 1, current + 1)
                : code == TerminalInput.Code.Home
                    ? 0
                    : code == TerminalInput.Code.End
                        ? logicalCount - 1
                        : code == TerminalInput.Code.PageUp
                            ? Math.Max(0, current - Math.Max(1, StepPageRows()))
                            : code == TerminalInput.Code.PageDown
                                ? Math.Min(logicalCount - 1, current + Math.Max(1, StepPageRows()))
                                : code == TerminalInput.Code.Enter
                                    ? current
                                    : -1;

        if (target < 0)
        {
            return false;
        }

        if (code == TerminalInput.Code.Enter)
        {
            if (!modifiers.IsActivationEligible())
            {
                return false;
            }

            if (!controller.IsPlaceholder(target) && controller.RowAt(target) is { } row)
            {
                RowInvoked?.Invoke(this, new TableRowInvokedEventArgs(row, target, ActivationCause.Keyboard));
            }

            return true;
        }

        SelectIndex(target, modifiers);
        BringIntoProgressiveView(target);
        return true;
    }

    private void BringIntoProgressiveView(int index)
    {
        if (Progressive is not { } controller)
        {
            return;
        }

        var current = VerticalOffset;
        var stride = controller.RowHeight.Add(_presenter.RowGap);
        var target = PagingStep.IndexIntoViewOffset(index, stride, current, Viewport.Height, Extent.Height);

        if (target != current)
        {
            VerticalOffset = target;
        }
    }

    [Pure]
    private int StepPageRows()
    {
        var stride = (Progressive?.RowHeight ?? 1).Add(_presenter.RowGap);
        var target = PagingStep.TargetExtent(Viewport.Height, PageOverlap);
        return Math.Max(1, target.Add(stride - 1) / stride);
    }

    // Keyboard navigation calls this on every arrow keystroke, so an O(rows) Rows.IndexOf scan
    // here would make every keystroke cost O(rows) before even touching cell state. The cache is
    // rebuilt lazily on first use after any invalidation, so a caller that never navigates never
    // pays to build it.
    private int IndexOfRow(TableRow row)
    {
        _rowIndexCache ??= BuildRowIndexCache();
        return _rowIndexCache.TryGetValue(row, out var index) ? index : -1;
    }

    [Pure]
    private Dictionary<TableRow, int> BuildRowIndexCache()
    {
        var cache = new Dictionary<TableRow, int>(Rows.Count);

        for (var index = 0; index < Rows.Count; index++)
        {
            cache[Rows[index]] = index;
        }

        return cache;
    }

    private bool MoveActive(
        int rowDelta,
        int columnDelta,
        TerminalInput.Modifiers modifiers = TerminalInput.Modifiers.None)
    {
        if (Rows.Count == 0 || Columns.Count == 0)
        {
            return false;
        }

        var rowIndex = ActiveRow is null ? 0 : IndexOfRow(ActiveRow);
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
            SelectRowCore(row, modifiers, targetColumn);
        }
        else
        {
            SelectCell(row, targetColumn, modifiers);
        }

        // SelectRowCore/SelectCell above publish property-changed notifications synchronously, and a
        // subscriber can react by removing the row or disposing the table before control returns
        // here - re-check liveness before touching the possibly-stale captured row, matching the
        // pointer path's equivalent guard.
        if (!IsRowCellLive(row, targetColumn))
        {
            return true;
        }

        _ = BringIntoView(row.Cells[targetColumn]);
        return true;
    }

    private bool MovePage(int direction, TerminalInput.Modifiers modifiers = TerminalInput.Modifiers.None)
    {
        if (Rows.Count == 0 || Columns.Count == 0)
        {
            return false;
        }

        var rowIndex = ActiveRow is null ? 0 : IndexOfRow(ActiveRow);
        var columnIndex = ActiveColumnIndex < 0 ? 0 : ActiveColumnIndex;
        var targetRow = StepPageRow(rowIndex, columnIndex, direction);

        if (ActiveRow is not null && targetRow == rowIndex)
        {
            return false;
        }

        var row = Rows[targetRow];

        if (SelectionMode is TableSelectionMode.Row or TableSelectionMode.MultipleRows)
        {
            SelectRowCore(row, modifiers, columnIndex);
        }
        else
        {
            SelectCell(row, columnIndex, modifiers);
        }

        // SelectRowCore/SelectCell above publish property-changed notifications synchronously, and a
        // subscriber can react by removing the row or disposing the table before control returns
        // here - re-check liveness before touching the possibly-stale captured row, matching the
        // pointer path's equivalent guard.
        if (!IsRowCellLive(row, columnIndex))
        {
            return true;
        }

        _ = BringIntoView(row.Cells[columnIndex]);
        return true;
    }

    // Accumulates realized row heights from the current row until the sum reaches the committed
    // viewport height (minus PageOverlap), rather than treating the viewport's cell height as a
    // row count. A landing index that runs past either end is clamped into range.
    [Pure]
    private int StepPageRow(int startIndex, int columnIndex, int direction)
    {
        var target = PagingStep.TargetExtent(Viewport.Height, PageOverlap);

        return PagingStep.Accumulate(startIndex, direction, Rows.Count, target, index => Rows[index].Cells[columnIndex].Bounds.Height, clamp: true);
    }

    private bool MoveToEndpoint(
        bool first,
        TerminalInput.Modifiers modifiers = TerminalInput.Modifiers.None)
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
            SelectRowCore(row, modifiers, column);
        }
        else if (SelectionMode is TableSelectionMode.Cell or TableSelectionMode.MultipleCells)
        {
            SelectCell(row, column, modifiers);
        }

        // SelectRowCore/SelectCell above publish property-changed notifications synchronously, and a
        // subscriber can react by removing the row or disposing the table before control returns
        // here - re-check liveness before touching the possibly-stale captured row, matching the
        // pointer path's equivalent guard.
        if (!IsRowCellLive(row, column))
        {
            return true;
        }

        // Home/End select the endpoint row but previously left the viewport pinned, unlike every
        // other navigation path here.
        _ = BringIntoView(row.Cells[column]);
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

        // SetActive above publishes property-changed notifications synchronously, and a subscriber
        // can react by removing the row or disposing the table before control returns here - unlike
        // MoveActive/MovePage/MoveToEndpoint, a stale row is not just an unsafe BringIntoView call
        // away, it is also unsafe to probe for TextInput or hand to BeginEdit/RowInvoked, so the
        // re-check gates the rest of the method entirely, matching the pointer path's equivalent
        // guards.
        if (!IsRowCellLive(row, column))
        {
            return false;
        }

        if (row.Cells[column] is TextInput && BeginEdit(row, column))
        {
            return true;
        }

        if (!IsRowCellLive(row, column))
        {
            return false;
        }

        RowInvoked?.Invoke(this, new TableRowInvokedEventArgs(row, Rows.IndexOf(row), ActivationCause.Keyboard));
        return true;
    }

    private void SetActive(TableRow row, [NonNegativeValue] int columnIndex)
    {
        VerifyOwned(row);
        ArgumentOutOfRangeException.ThrowIfNegative(columnIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint) columnIndex, (uint) row.Cells.Count);

        CommitActiveCell(row, columnIndex);
    }

    // Every path that moves the active cell — keyboard navigation, selection, and the
    // row-mutation repairs — must publish the properties it commits; a repair that assigns
    // ActiveRow directly is invisible to a two-way binding otherwise.
    private void CommitActiveCell(TableRow? row, int columnIndex)
    {
        var rowChanged = !ReferenceEquals(ActiveRow, row);
        var columnChanged = ActiveColumnIndex != columnIndex;

        if (!rowChanged && !columnChanged)
        {
            return;
        }

        var previousRow = ActiveRow;
        var previousColumn = ActiveColumnIndex;
        ActiveRow = row;
        ActiveColumnIndex = columnIndex;

        // Two targeted touches replace the old blanket ApplyCellStates() sweep over every row and
        // column. The previous cell is only touched when it is still owned by a row present in
        // Rows: RemoveRowCore detaches the removed row from Rows before calling here during
        // removal-repair, so a stale reference into a row that is being torn down (and may be
        // disposed) is never touched - matching the blanket sweep's own behavior, since it only
        // ever iterated live rows in Rows to begin with.
        if (previousRow is not null && previousColumn >= 0 && IndexOfRow(previousRow) >= 0)
        {
            previousRow.Cells[previousColumn].SetCurrentState(false);
        }

        if (row is not null && columnIndex >= 0)
        {
            row.Cells[columnIndex].SetCurrentState(true);
        }

        if (rowChanged)
        {
            NotifyPropertyChanged(nameof(ActiveRow), InvalidationImpact.None);
        }

        // Each NotifyPropertyChanged call above can synchronously reach a PropertyChanged
        // subscriber that disposes the table - re-check before every further disposed-guarded call.
        if (IsDisposed)
        {
            return;
        }

        if (columnChanged)
        {
            NotifyPropertyChanged(nameof(ActiveColumnIndex), InvalidationImpact.None);
        }

        if (IsDisposed)
        {
            return;
        }

        NotifyPropertyChanged(nameof(ActiveCell), InvalidationImpact.None);
    }

    private void CommitSelection(
        IEnumerable<TableRow> rows,
        IEnumerable<TableCellReference> cells)
    {
        // A caller earlier in the same gesture (e.g. SelectRowCore's preceding SetActive call) may
        // have already let a PropertyChanged subscriber dispose the table - every call site must be
        // safe to reach post-disposal rather than relying on each caller to re-check first.
        if (IsDisposed)
        {
            return;
        }

        var nextRows = new HashSet<TableRow>(rows);
        var nextCells = new HashSet<TableCellReference>(cells);
        var oldRows = new HashSet<TableRow>(_selectedRows);
        var oldCells = new HashSet<TableCellReference>(_selectedCells);

        if (oldRows.SetEquals(nextRows) && oldCells.SetEquals(nextCells))
        {
            return;
        }

        var selectionVersion = ++_selectionVersion;

        var addedRows = nextRows.Except(oldRows).ToArray();
        var removedRows = oldRows.Except(nextRows).ToArray();
        var addedCells = nextCells.Except(oldCells).ToArray();
        var removedCells = oldCells.Except(nextCells).ToArray();

        _selectedRows.Clear();
        _selectedRows.UnionWith(nextRows);
        _selectedCells.Clear();
        _selectedCells.UnionWith(nextCells);

        // A targeted delta application replaces the old blanket ApplyCellStates() sweep over
        // every row and column - the row/cell sets above already computed exactly which rows and
        // cells actually joined or left selection, so re-touching every other cell adds no value.
        ApplyCellStateDelta(addedRows, removedRows, addedCells, removedCells);
        NotifyPropertyChanged(nameof(SelectedRows), InvalidationImpact.None);

        // NotifyPropertyChanged above can synchronously reach a PropertyChanged subscriber that
        // disposes the table - the version check alone only catches a reentrant selection change,
        // not disposal, so both must be checked before the next disposed-guarded call.
        if (IsDisposed || _selectionVersion != selectionVersion)
        {
            return;
        }

        NotifyPropertyChanged(nameof(SelectedCells), InvalidationImpact.None);

        if (IsDisposed || _selectionVersion != selectionVersion)
        {
            return;
        }

        SelectionChanged?.Invoke(
            this,
            new TableSelectionChangedEventArgs(addedRows, removedRows, addedCells, removedCells));
    }

    // Applies exactly the rows/cells that joined or left selection, instead of re-deriving every
    // cell's selected state from scratch across the whole table.
    private void ApplyCellStateDelta(
        IReadOnlyList<TableRow> addedRows,
        IReadOnlyList<TableRow> removedRows,
        IReadOnlyList<TableCellReference> addedCells,
        IReadOnlyList<TableCellReference> removedCells)
    {
        foreach (var row in addedRows)
        {
            foreach (var cell in row.Cells)
            {
                cell.SetSelectedState(true);
            }
        }

        foreach (var row in removedRows)
        {
            // Recomputed per cell rather than assumed false: a row leaving row-selection could in
            // principle still carry an individually cell-selected member.
            for (var column = 0; column < row.Cells.Count; column++)
            {
                row.Cells[column].SetSelectedState(_selectedCells.Contains(new TableCellReference(row, column)));
            }
        }

        foreach (var reference in addedCells)
        {
            reference.Cell.SetSelectedState(true);
        }

        foreach (var reference in removedCells)
        {
            reference.Cell.SetSelectedState(_selectedRows.Contains(reference.Row));
        }
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

    // Returns null when either endpoint's row has left Rows - the SelectionGesture<TKey> contract
    // for an unresolvable range, which leaves the caller's selection untouched rather than
    // clearing it.
    private IEnumerable<TableRow>? AddRowRange(TableRow start, TableRow end)
    {
        var startIndex = IndexOfRow(start);
        var endIndex = IndexOfRow(end);

        if (startIndex < 0 || endIndex < 0)
        {
            return null;
        }

        List<TableRow> range = [];

        for (var index = Math.Min(startIndex, endIndex); index <= Math.Max(startIndex, endIndex); index++)
        {
            range.Add(Rows[index]);
        }

        return range;
    }

    // Returns null under the same unresolvable-range contract as AddRowRange.
    private IEnumerable<TableCellReference>? AddCellRange(TableCellReference start, TableCellReference end)
    {
        var startIndex = IndexOfRow(start.Row);
        var endIndex = IndexOfRow(end.Row);

        if (startIndex < 0 || endIndex < 0)
        {
            return null;
        }

        // The column span is the same rectangle on every row in the range - only the boundary
        // row that shares an index with startIndex or endIndex was previously clamped to
        // startColumn/endColumn; every interior row fell through to 0..row.Cells.Count - 1,
        // selecting the entire row instead of the same column band as the anchor and target.
        var first = Math.Min(start.ColumnIndex, end.ColumnIndex);
        var last = Math.Max(start.ColumnIndex, end.ColumnIndex);
        List<TableCellReference> range = [];

        for (var rowIndex = Math.Min(startIndex, endIndex); rowIndex <= Math.Max(startIndex, endIndex); rowIndex++)
        {
            var row = Rows[rowIndex];
            var rowLast = Math.Min(last, row.Cells.Count - 1);

            for (var column = first; column <= rowLast; column++)
            {
                range.Add(new TableCellReference(row, column));
            }
        }

        return range;
    }

    private void VerifyOwned(TableRow row)
    {
        if (!Rows.Contains(row))
        {
            throw new ArgumentException("The row is not owned by this table.", nameof(row));
        }

        VerifyMutable();
    }

    private bool TryGetCell(ControlBase? source, [MaybeNullWhen(false)] out TableRow row, out int columnIndex)
    {
        if (source is null)
        {
            row = null;
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

        row = null;
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
        NotifyPropertyChanged(nameof(IsEditing), InvalidationImpact.None);

        if (Rows.Contains(edit.Row))
        {
            SetActive(edit.Row, Math.Min(edit.ColumnIndex, edit.Row.Cells.Count - 1));
        }
        else if (ActiveRow is not null && !Rows.Contains(ActiveRow))
        {
            CommitActiveCell(null, -1);
        }
    }

    [Pure]
    private static string CellText(ControlBase cell) => cell switch
    {
        Text text => text.Content,
        TextInput input => input.Text,
        InputBase { Text: var text } => text,
        ContentControl { Content: Text text } => text.Content,
        ContentControl { Content: TextInput input } => input.Text,
        _ => string.Empty
    };

    private object? GetSortKey(TableRow row, int columnIndex)
    {
        var cell = row.Cells[columnIndex];
        return Columns[columnIndex].SortKey?.Invoke(cell) ?? CellText(cell);
    }

    [Pure]
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

    /// <summary>
    /// Splices one changed row into its sorted position among the other, already-ordered rows via
    /// binary search, instead of re-sorting every row from scratch on each insert/replace while
    /// sorted — O(log n) comparisons plus the O(n) shift already paid by <see cref="ReorderRows"/>,
    /// not another full O(n log n) resort.
    /// </summary>
    /// <param name="changedRow">The newly inserted or replacement row, currently present in
    /// <see cref="Rows"/> at any position.</param>
    /// <returns>Every row in final sorted order.</returns>
    private TableRow[] SpliceIntoSortedOrder(TableRow changedRow)
    {
        var others = new List<TableRow>(Rows.Count - 1);

        foreach (var row in Rows)
        {
            if (!ReferenceEquals(row, changedRow))
            {
                others.Add(row);
            }
        }

        var key = GetSortKey(changedRow, SortColumnIndex);
        var ascending = SortDirection == TableSortDirection.Ascending;
        var low = 0;
        var high = others.Count;

        while (low < high)
        {
            var mid = (low + high) / 2;
            var comparison = CompareKeys(key, GetSortKey(others[mid], SortColumnIndex));

            if (!ascending)
            {
                comparison = -comparison;
            }

            // A tie sorts after every existing row with an equal key, matching SetSort's
            // insertion-order tie-break for the most recently changed row.
            if (comparison < 0)
            {
                high = mid;
            }
            else
            {
                low = mid + 1;
            }
        }

        others.Insert(low, changedRow);

        return [.. others];
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
    internal void ValidateColumnCount([NonNegativeValue] int count)
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
            else if (SortColumnIndex != newIndex)
            {
                SortColumnIndex = newIndex;
                NotifyPropertyChanged(nameof(SortColumnIndex), InvalidationImpact.None);
            }
        }

        Invalidate(Invalidation.Measure);
        InvalidateRetainedDescendant(_presenter, InvalidationImpact.Measure);
    }

    /// <summary>Validates one public column definition before collection ownership.</summary>
    /// <param name="column">The candidate definition.</param>
    /// <exception cref="ArgumentException">The header is missing or whitespace.</exception>
    internal static void ValidateColumn(TableColumn column) =>
        ArgumentException.ThrowIfNullOrWhiteSpace(column.Header, nameof(column));

    /// <summary>Rejects an automatic-width column while progressive - column widths must resolve
    /// without probing any row, and only the realized window is ever built.</summary>
    /// <param name="column">The candidate definition.</param>
    /// <exception cref="ArgumentException">The table is progressive and the column is automatic-width.</exception>
    internal void ValidateProgressiveColumn(TableColumn column)
    {
        if (IsProgressive && column.Width.Kind == LengthKind.Auto)
        {
            throw new ArgumentException(
                "A progressive table cannot define an automatic-width column.",
                nameof(column));
        }
    }

    internal void OnPresenterCellDisposalRequested(ControlBase cell)
    {
        for (var index = Rows.Count - 1; index >= 0; index--)
        {
            var row = Rows[index];

            if (row.Cells.Contains(cell))
            {
                RemoveRowCore(Rows, index, repairSelection: true);
                return;
            }
        }
    }

    /// <summary>Inserts and attaches one validated row.</summary>
    /// <param name="owner">The calling row collection.</param>
    /// <param name="index">The insertion index.</param>
    /// <param name="row">The non-null row.</param>
    internal void InsertRow(TableRowCollection owner, int index, TableRow row)
    {
        VerifyRowsOwner(owner);
        RequireNotProgressive("Rows cannot be mutated directly while the table is progressive.");
        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint) index, (uint) Rows.Count);
        ValidateRow(row);

        foreach (var cell in row.Cells)
        {
            _presenter.Children.Add(cell);
        }

        owner.InsertAttached(index, row);

        // Every position at or after the insertion point shifts, so the whole cache is stale, not
        // just one entry - discarded here and rebuilt lazily the next time navigation needs it.
        _rowIndexCache = null;

        if (!_isReordering)
        {
            _sourceRows.Insert(index, row);
        }

        Invalidate(Invalidation.Measure);
        InvalidateRetainedDescendant(_presenter, InvalidationImpact.Measure);
        ApplyCellStates();

        // Re-splicing into the active sorted order does not change SortColumnIndex or
        // SortDirection, so it no longer re-raises SortChanged here - the event reports a real
        // change to the sort settings, not every re-sort of the row order.
        if (!_isReordering && SortDirection != TableSortDirection.None)
        {
            ReorderRows(SpliceIntoSortedOrder(row));
        }
    }

    /// <summary>Removes and detaches one owned row.</summary>
    /// <param name="owner">The calling row collection.</param>
    /// <param name="index">The valid row index.</param>
    internal void RemoveRow(TableRowCollection owner, int index)
    {
        VerifyRowsOwner(owner);
        RequireNotProgressive("Rows cannot be mutated directly while the table is progressive.");
        RemoveRowCore(owner, index, repairSelection: true);
    }

    private void RemoveRowCore(TableRowCollection owner, int index, bool repairSelection)
    {
        var row = Rows[index];

        // A reorder relocates this exact row instance rather than removing it — cancelling an
        // edit it owns would silently revert uncommitted text for a row that is about to be
        // re-added unchanged.
        if (!_isReordering)
        {
            CancelActiveEditIfOwned(row);
        }

        if (repairSelection)
        {
            var fallback = index < Rows.Count - 1 ? Rows[index + 1] : index > 0 ? Rows[index - 1] : null;
            RepairSelectionForRows([row], fallback);
        }

        index = Rows.IndexOf(row);

        if (index < 0)
        {
            return;
        }

        // Commit semantic removal before detaching cells. Any lifecycle callback raised while a
        // cell leaves the presenter then observes that this exact row is already gone and cannot
        // recursively remove it or redirect the outer operation through its former numeric index.
        owner.RemoveAttached(index);
        _rowIndexCache = null;

        foreach (var cell in row.Cells)
        {
            _ = _presenter.Children.Remove(cell);
        }

        // Every position after the removed index shifts, so the whole cache is stale, not just
        // one entry - discarded here and rebuilt lazily the next time navigation needs it. This
        // also means IndexOfRow(row) already reports -1 for the just-detached row by the time the
        // ActiveRow repair below calls CommitActiveCell.
        if (!_isReordering)
        {
            _ = _sourceRows.Remove(row);

            if (ReferenceEquals(ActiveRow, row))
            {
                var replacement = Rows.Count == 0 ? null : Rows[Math.Min(index, Rows.Count - 1)];
                CommitActiveCell(replacement, replacement is null ? -1 : 0);
            }
        }

        Invalidate(Invalidation.Measure);
        InvalidateRetainedDescendant(_presenter, InvalidationImpact.Measure);
        ApplyCellStates();
    }

    /// <summary>Clears and detaches every owned row.</summary>
    /// <param name="owner">The calling row collection.</param>
    internal void ClearRows(TableRowCollection owner)
    {
        VerifyRowsOwner(owner);
        RequireNotProgressive("Rows cannot be mutated directly while the table is progressive.");

        // A reorder clears every row only to re-add the exact same instances in a new order —
        // none of them are actually being removed, so selection referencing those row instances
        // must survive untouched instead of being unconditionally emptied here.
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
        RequireNotProgressive("Rows cannot be mutated directly while the table is progressive.");
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

        index = Rows.IndexOf(previous);

        if (index < 0)
        {
            return;
        }

        foreach (var cell in previous.Cells)
        {
            _ = _presenter.Children.Remove(cell);
        }

        foreach (var cell in row.Cells)
        {
            _presenter.Children.Add(cell);
        }

        owner.ReplaceAttached(index, row);

        // The position at index does not move, so a full invalidate-and-rebuild would be wasteful
        // for a single-row replacement - patch the one changed entry instead.
        if (_rowIndexCache is not null)
        {
            _ = _rowIndexCache.Remove(previous);
            _rowIndexCache[row] = index;
        }

        if (!_isReordering && _sourceRows.IndexOf(previous) is var sourceIndex && sourceIndex >= 0)
        {
            _sourceRows[sourceIndex] = row;
        }

        if (ReferenceEquals(ActiveRow, previous))
        {
            CommitActiveCell(row, ActiveColumnIndex);
        }

        Invalidate(Invalidation.Measure);
        InvalidateRetainedDescendant(_presenter, InvalidationImpact.Measure);
        ApplyCellStates();

        // Re-splicing into the active sorted order does not change SortColumnIndex or
        // SortDirection, so it no longer re-raises SortChanged here - the event reports a real
        // change to the sort settings, not every re-sort of the row order.
        if (!_isReordering && SortDirection != TableSortDirection.None)
        {
            ReorderRows(SpliceIntoSortedOrder(row));
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
    // Only the primary glyph is themable; the ASCII repair value stays code-owned, which is the
    // split theming-new-controls.md asks for.
    [Pure]
    private Rune ResolveGridGlyph(ControlGlyph themed) =>
        themed.Value.Resolve(themed.Fallback, CellPolicy.AmbiguousWidth);

}
