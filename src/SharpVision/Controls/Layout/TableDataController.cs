// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Layout;

using NonNegativeValue = JetBrains.Annotations.NonNegativeValueAttribute;
using TerminalInput = Terminal.Input;
using ValueRange = JetBrains.Annotations.ValueRangeAttribute;

/// <summary>Owns one progressive <see cref="Table"/>'s fetch scheduling, cache, windowed
/// realization, and key-based selection state.</summary>
/// <remarks>
/// Installed by <see cref="Table.SetDataSource{T}"/> and replaced or torn down by a later
/// <see cref="Table.SetDataSource{T}"/> call, <see cref="Table.ClearDataSource"/>, or table
/// disposal. Every member here runs only on the owning table's dispatcher thread.
/// </remarks>
internal sealed class TableDataController: IDisposable
{
    private const int _maxConcurrentFetches = 4;
    private const int _maxAttempts = 3;
    private static readonly TimeSpan _retryDelay = TimeSpan.FromMilliseconds(200);

    private readonly Table _owner;
    private readonly TablePresenter _presenter;
    private readonly TableDataAdapter _adapter;
    private readonly Dictionary<int, CacheEntry> _cache = [];
    private readonly Dictionary<object, int> _keyIndex = new(EqualityComparer<object>.Default);
    private readonly List<PendingRange> _pending = [];
    private readonly HashSet<int> _errorIndices = [];
    private readonly HashSet<object> _selectedKeys = new(EqualityComparer<object>.Default);

    private long _generation;
    private CancellationTokenSource _lifetime = new();
    private TableRow?[] _window = [];
    private int _knownFrontier = -1;
    private bool _disposed;

    /// <summary>Initializes a controller bound to one owning table, presenter, and adapter.</summary>
    /// <param name="owner">The owning table.</param>
    /// <param name="presenter">The owning table's private presenter.</param>
    /// <param name="adapter">The type-erased source/template adapter.</param>
    /// <param name="rowHeight">The positive uniform row height.</param>
    public TableDataController(Table owner, TablePresenter presenter, TableDataAdapter adapter, int rowHeight)
    {
        _owner = owner;
        _presenter = presenter;
        _adapter = adapter;
        RowHeight = rowHeight;
        _adapter.Changed += OnAdapterChanged;
    }

    /// <summary>Gets the positive uniform row height.</summary>
    [ValueRange(1, int.MaxValue)]
    public int RowHeight { get; }

    /// <summary>Gets the table-wide aggregate loading state.</summary>
    public TableLoadState LoadState { get; private set; } = TableLoadState.Idle;

    /// <summary>Raised after <see cref="LoadState"/> actually changes.</summary>
    public event EventHandler<TableLoadStateChangedEventArgs>? LoadStateChanged;

    /// <summary>Raised after one range exhausts its retries.</summary>
    public event EventHandler<TableLoadFailedEventArgs>? LoadFailed;

    /// <summary>Raised after <see cref="ActiveIndex"/>, <see cref="ActiveKey"/>, or the selected-key
    /// set actually changes, mirroring <see cref="LoadStateChanged"/>'s controller-to-table
    /// forwarding shape.</summary>
    public event EventHandler<TableSelectionChangedEventArgs>? SelectionChanged;

    /// <summary>Gets the active navigation index, or -1 when nothing is active.</summary>
    [ValueRange(-1, int.MaxValue)]
    public int ActiveIndex { get; private set; } = -1;

    /// <summary>Gets the active navigation key, or null when nothing is active.</summary>
    public object? ActiveKey { get; private set; }

    /// <summary>Gets a snapshot of currently selected keys, independent of cache or window state.</summary>
    public IReadOnlyList<object> SelectedKeys => [.. _selectedKeys];

    /// <summary>Gets the current logical row extent, including one phantom row past the highest
    /// confirmed index while the source's total count is unknown and data has not ended.</summary>
    [NonNegativeValue]
    public int LogicalCount =>
        IsEndOfData
            ? _knownFrontier + 1
            : _adapter.Count is int known
                ? Math.Max(0, known)
                : _knownFrontier + 2;

    /// <summary>Gets whether the backing source has reported its true end.</summary>
    public bool IsEndOfData { get; private set; }

    /// <summary>Gets the first realized logical index, valid only while a window is realized.</summary>
    [NonNegativeValue]
    public int WindowStart { get; private set; }

    /// <summary>Gets the number of realized window slots.</summary>
    [NonNegativeValue]
    public int WindowCount => _window.Length;

    /// <summary>Gets the number of ranges currently pending a fetch, for diagnostics and tests.</summary>
    [NonNegativeValue]
    public int PendingCount => _pending.Count;

    /// <summary>Gets the realized row at a logical index, or null when unrealized.</summary>
    /// <param name="logicalIndex">The zero-based logical index.</param>
    [Pure]
    public TableRow? RowAt(int logicalIndex)
    {
        var position = logicalIndex - WindowStart;
        return (uint) position < (uint) _window.Length ? _window[position] : null;
    }

    /// <summary>Gets whether a logical index is currently rendered as a placeholder or error skeleton.</summary>
    /// <param name="logicalIndex">The zero-based logical index.</param>
    [Pure]
    public bool IsPlaceholder(int logicalIndex) => !_cache.ContainsKey(logicalIndex);

    /// <summary>Reconciles realized rows, in-flight fetches, and the cache against the current
    /// scroll offset and viewport height, then arranges every realized row directly.</summary>
    /// <param name="viewportHeight">The current non-negative viewport height in cells.</param>
    /// <param name="verticalOffset">The current non-negative vertical scroll offset.</param>
    public void Rewindow(int viewportHeight, int verticalOffset)
    {
        var logicalCount = _disposed || _owner.Columns.Count == 0 ? 0 : LogicalCount;

        if (logicalCount == 0)
        {
            DerealizeAll();
            return;
        }

        var rowGap = _presenter.RowGap;
        var stride = RowHeight + rowGap;
        var viewportRows = Math.Max(1, stride == 0 ? 1 : viewportHeight / stride);
        var first = Math.Max(0, (verticalOffset / stride) - viewportRows);
        var last = Math.Min(
            logicalCount - 1,
            ((verticalOffset + Math.Max(0, viewportHeight - 1)) / stride) + viewportRows);

        // A stale, still-large verticalOffset can outrun a freshly-reset (or freshly-shrunk)
        // logicalCount - most notably right after Reload() collapses LogicalCount to 1 for an
        // unknown-count progressive source, before any layout pass gets a chance to re-clamp
        // VerticalOffset against the new extent. Realizing here would require a negative-length
        // array; derealize instead and let the next legitimate rewindow (triggered by the
        // re-clamp) pick a consistent window.
        if (first > last)
        {
            DerealizeAll();
            return;
        }

        IssueFetchesForGaps(first, last);
        CancelOutOfRangePending(first, last);
        RealizeWindow(first, last);
        ApplyCellStates();
        EvictCache(first, last, viewportRows);
        ArrangeWindow();
    }

    /// <summary>Discards the entire cache, cancels every pending fetch, bumps the generation, and
    /// derealizes every currently-realized window slot.</summary>
    /// <remarks>
    /// <see cref="DerealizeAll"/> runs after the cache is cleared, not before: it is what forces
    /// already-realized, non-placeholder rows back to a rebuildable state - <see cref="RealizeWindow"/>
    /// only ever rebuilds a slot that is null or a stale <see cref="TablePlaceholderCell"/>, so a
    /// genuinely loaded data row at an unchanged scroll position would otherwise keep showing its
    /// pre-reload content forever. Clearing the window here, with an already-empty cache, also makes
    /// <see cref="RemoveFromPresenter"/> dispose every outgoing cell instead of assuming the cache
    /// still owns it, matching the disposal it already performs for rows that scroll out of range.
    /// </remarks>
    public void Reload()
    {
        BumpGeneration();
        _cache.Clear();
        _keyIndex.Clear();
        _errorIndices.Clear();
        _knownFrontier = -1;
        IsEndOfData = false;
        DerealizeAll();
        UpdateLoadState();
    }

    /// <summary>Updates key-based selection synchronously, independent of cache or window state.</summary>
    /// <param name="index">The candidate logical index.</param>
    /// <param name="modifiers">The selection modifiers.</param>
    /// <param name="selectionMode">The table's active selection mode.</param>
    public void SelectIndex(int index, TerminalInput.Modifiers modifiers, TableSelectionMode selectionMode)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        var (previousActiveIndex, previousActiveKey, previousSelectedKeys) = CaptureSelectionState();
        ActiveIndex = index;
        ActiveKey = _cache.TryGetValue(index, out var entry) ? entry.Key : ActiveKey;
        ApplySelectionGesture(ResolveSelectionTarget(index), modifiers, selectionMode);
        NotifySelectionChanged(previousActiveIndex, previousActiveKey, previousSelectedKeys);
    }

    /// <summary>Updates key-based selection synchronously by stable key.</summary>
    /// <param name="key">The candidate key.</param>
    /// <param name="modifiers">The selection modifiers.</param>
    /// <param name="selectionMode">The table's active selection mode.</param>
    public void SelectKey(object key, TerminalInput.Modifiers modifiers, TableSelectionMode selectionMode)
    {
        ArgumentNullException.ThrowIfNull(key);
        var (previousActiveIndex, previousActiveKey, previousSelectedKeys) = CaptureSelectionState();
        ActiveKey = key;

        if (_keyIndex.TryGetValue(key, out var index))
        {
            ActiveIndex = index;
        }

        ApplySelectionGesture(key, modifiers, selectionMode);
        NotifySelectionChanged(previousActiveIndex, previousActiveKey, previousSelectedKeys);
    }

    /// <summary>Selects every row allowed by <paramref name="selectionMode"/> among currently loaded
    /// keys: nothing for <see cref="TableSelectionMode.None"/>, the active key (or the first loaded
    /// key when nothing is active) for <see cref="TableSelectionMode.Row"/>, and every loaded key
    /// for <see cref="TableSelectionMode.MultipleRows"/> - the same branching
    /// <see cref="Table.SelectAll"/> already applies in eager mode.</summary>
    /// <param name="selectionMode">The table's active selection mode.</param>
    public void SelectAllKnown(TableSelectionMode selectionMode)
    {
        var (previousActiveIndex, previousActiveKey, previousSelectedKeys) = CaptureSelectionState();
        _selectedKeys.Clear();

        if (selectionMode == TableSelectionMode.Row)
        {
            var candidate = ActiveKey is { } activeKey && _keyIndex.ContainsKey(activeKey)
                ? activeKey
                : _cache.Count > 0 ? _cache[_cache.Keys.Min()].Key : null;

            if (candidate is not null)
            {
                _ = _selectedKeys.Add(candidate);
            }
        }
        else if (selectionMode == TableSelectionMode.MultipleRows)
        {
            foreach (var entry in _cache.Values)
            {
                _ = _selectedKeys.Add(entry.Key);
            }
        }

        NotifySelectionChanged(previousActiveIndex, previousActiveKey, previousSelectedKeys);
    }

    /// <summary>Clears every selected key.</summary>
    public void ClearSelection()
    {
        var (previousActiveIndex, previousActiveKey, previousSelectedKeys) = CaptureSelectionState();
        _selectedKeys.Clear();
        NotifySelectionChanged(previousActiveIndex, previousActiveKey, previousSelectedKeys);
    }

    /// <summary>Copies currently loaded selected rows, in ascending index order, skipping unloaded keys.</summary>
    [Pure]
    public IReadOnlyList<(int Index, TableRow Row)> CopyLoadedSelection()
    {
        List<(int, TableRow)> result = [];

        foreach (var pair in _cache.OrderBy(static entry => entry.Key))
        {
            if (_selectedKeys.Contains(pair.Value.Key))
            {
                result.Add((pair.Key, pair.Value.Row));
            }
        }

        return result;
    }

    /// <summary>Cancels every in-flight fetch without clearing the cache or selection state, for a
    /// table that left the tree but was not disposed.</summary>
    public void CancelInFlight()
    {
        if (_disposed)
        {
            return;
        }

        foreach (var range in _pending)
        {
            range.Cts.Cancel();
            range.Cts.Dispose();
            range.RetryTimer?.Dispose();
        }

        _pending.Clear();
        UpdateLoadState();
    }

    /// <inheritdoc/>
    public void Dispose() => Dispose(derealize: true);

    /// <summary>Stops progressive loading while the owning table's disposal transaction remains
    /// responsible for disposing every realized presenter cell.</summary>
    internal void DisposeWithOwner() => Dispose(derealize: false);

    private void Dispose(bool derealize)
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _adapter.Changed -= OnAdapterChanged;
        _adapter.Detach();
        _lifetime.Cancel();
        _lifetime.Dispose();

        foreach (var range in _pending)
        {
            range.Cts.Cancel();
            range.Cts.Dispose();
            range.RetryTimer?.Dispose();
        }

        _pending.Clear();

        if (derealize)
        {
            DerealizeAll();
        }
    }

    private void BumpGeneration()
    {
        _generation++;
        _lifetime.Cancel();
        _lifetime.Dispose();
        _lifetime = new CancellationTokenSource();

        foreach (var range in _pending)
        {
            range.Cts.Cancel();
            range.Cts.Dispose();
            range.RetryTimer?.Dispose();
        }

        _pending.Clear();
        UpdateLoadState();
    }

    // Mirrors the public Table.Reload()'s pairing of Progressive!.Reload() with an explicit
    // ProgressiveRewindow() call: Reload() alone only clears state, it never repopulates _pending
    // or calls IssueFetchesForGaps, so without this follow-up an adapter-driven Changed event would
    // leave the table showing an empty/placeholder window with no fetch in flight until an
    // unrelated scroll, resize, or attach happened to trigger a rewindow.
    private void OnAdapterChanged(object? sender, EventArgs eventArgs)
    {
        Reload();
        RewindowFollowingCommit();
    }

    [Pure]
    private object? ResolveSelectionTarget(int index) =>
        _cache.TryGetValue(index, out var entry) ? entry.Key : null;

    private void ApplySelectionGesture(object? key, TerminalInput.Modifiers modifiers, TableSelectionMode selectionMode)
    {
        if (selectionMode == TableSelectionMode.None)
        {
            return;
        }

        if (key is null)
        {
            // Selecting an unrealized/unloaded index still moves ActiveIndex/ActiveKey above; there
            // is no key yet to record in the selection set until the range loads.
            if (selectionMode is not (TableSelectionMode.MultipleRows or TableSelectionMode.MultipleCells))
            {
                _selectedKeys.Clear();
            }

            return;
        }

        var control = (modifiers & TerminalInput.Modifiers.Control) != 0;
        var multiple = selectionMode is TableSelectionMode.MultipleRows or TableSelectionMode.MultipleCells;

        if (multiple && control)
        {
            if (!_selectedKeys.Remove(key))
            {
                _ = _selectedKeys.Add(key);
            }

            return;
        }

        if (!multiple)
        {
            _selectedKeys.Clear();
        }

        _ = _selectedKeys.Add(key);
    }

    [Pure]
    private (int ActiveIndex, object? ActiveKey, HashSet<object> SelectedKeys) CaptureSelectionState() =>
        (ActiveIndex, ActiveKey, new HashSet<object>(_selectedKeys, EqualityComparer<object>.Default));

    // Bundles the SelectionChanged event with the ActiveIndex/ActiveKey/SelectedKeys property
    // notifications it always travels with - Table's forwarding handler raises all three together
    // whenever this fires, since every path here already gates on an actual change before calling
    // it, matching UpdateLoadState's equivalent gate for LoadStateChanged.
    //
    // newlyRealizedSelectedRows covers a case the set/active comparison alone misses: SelectKey
    // can select a key before its range ever loads (ApplySelectionGesture never requires the key
    // to already be cache-resolved), so a later fetch can realize a row for a key that was already
    // selected - the key set and the active location are both unchanged, but the row itself just
    // became visible/copyable for the first time. OnFetchSucceeded is the only caller that ever
    // passes this.
    private void NotifySelectionChanged(
        int previousActiveIndex,
        object? previousActiveKey,
        HashSet<object> previousSelectedKeys,
        List<TableRow>? newlyRealizedSelectedRows = null)
    {
        var selectionSetChanged = !previousSelectedKeys.SetEquals(_selectedKeys);
        var hasNewlyRealized = newlyRealizedSelectedRows is { Count: > 0 };

        if (previousActiveIndex == ActiveIndex &&
            Equals(previousActiveKey, ActiveKey) &&
            !selectionSetChanged &&
            !hasNewlyRealized)
        {
            return;
        }

        ApplyCellStates();

        var addedRows = selectionSetChanged
            ? ResolveRealizedRows(_selectedKeys.Except(previousSelectedKeys))
            : [];

        if (hasNewlyRealized)
        {
            addedRows.AddRange(newlyRealizedSelectedRows!);
        }

        IReadOnlyList<TableRow> removedRows = selectionSetChanged
            ? ResolveRealizedRows(previousSelectedKeys.Except(_selectedKeys))
            : [];

        // Progressive tables never allow a cell selection mode (SetDataSource rejects Cell and
        // MultipleCells), so the cell columns of this shared event-args type are always empty here.
        SelectionChanged?.Invoke(
            _owner,
            new TableSelectionChangedEventArgs(addedRows, removedRows, [], []));
    }

    private List<TableRow> ResolveRealizedRows(IEnumerable<object> keys)
    {
        List<TableRow> rows = [];

        foreach (var key in keys)
        {
            if (_keyIndex.TryGetValue(key, out var index) && _cache.TryGetValue(index, out var entry))
            {
                rows.Add(entry.Row);
            }
        }

        return rows;
    }

    // Applies current visual selected/current state to every realized window slot, including
    // placeholders - a placeholder can legitimately sit at ActiveIndex mid-fetch, but never carries
    // selected state: the check below requires the position's own cache entry, not merely
    // membership in _selectedKeys, since SelectKey can select a key before its range ever loads
    // (see NotifySelectionChanged's newlyRealizedSelectedRows handling for the SelectionChanged
    // side of that same case).
    private void ApplyCellStates()
    {
        for (var position = 0; position < _window.Length; position++)
        {
            if (_window[position] is not { } row)
            {
                continue;
            }

            var logicalIndex = WindowStart + position;
            var selected = _cache.TryGetValue(logicalIndex, out var entry) && _selectedKeys.Contains(entry.Key);
            var current = logicalIndex == ActiveIndex;

            foreach (var cell in row.Cells)
            {
                cell.SetSelectedState(selected);
                cell.SetCurrentState(current);
            }
        }
    }

    private void IssueFetchesForGaps(int first, int last)
    {
        var capacity = _maxConcurrentFetches - _pending.Count;

        if (capacity <= 0)
        {
            return;
        }

        List<(int Start, int Count)> gaps = [];
        var index = first;

        while (index <= last)
        {
            if (IsSatisfied(index))
            {
                index++;
                continue;
            }

            var start = index;

            while (index <= last && !IsSatisfied(index))
            {
                index++;
            }

            gaps.Add((start, index - start));
        }

        if (gaps.Count == 0)
        {
            return;
        }

        var center = (first + last) / 2.0;
        var ordered = gaps
            .OrderBy(gap => Math.Abs((((gap.Start * 2) + gap.Count - 1) / 2.0) - center))
            .Take(capacity);

        foreach (var (start, count) in ordered)
        {
            IssueFetch(start, count);
        }
    }

    // A permanently failed index counts as satisfied too - otherwise RewindowFollowingCommit's
    // unconditional call after OnFetchFailed's own exhaustion branch would immediately requeue the
    // exact same broken range, storming a persistently failing backend forever instead of settling
    // on the rendered error placeholder until Reload() or eviction-then-return gives it a fresh
    // attempt.
    [Pure]
    private bool IsSatisfied(int index) => _cache.ContainsKey(index) || IsPending(index) || _errorIndices.Contains(index);

    [Pure]
    private bool IsPending(int index)
    {
        foreach (var range in _pending)
        {
            if (index >= range.Start && index < range.Start + range.Count)
            {
                return true;
            }
        }

        return false;
    }

    private void IssueFetch(int start, int count)
    {
        var range = new PendingRange(start, count, _generation, CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token));
        _pending.Add(range);
        UpdateLoadState();
        _ = RunFetchAsync(range);
    }

    private async Task RunFetchAsync(PendingRange range)
    {
        var request = new TableDataRequest(range.Start, range.Count);
        Exception? failure = null;
        TableDataAdapterResult result = default;

        try
        {
            result = await _adapter.LoadAsync(request, range.Cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (range.Cts.IsCancellationRequested)
        {
            await MarshalAsync(() => OnFetchCancelled(range)).ConfigureAwait(false);
            return;
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        if (failure is not null)
        {
            await MarshalAsync(() => OnFetchFailed(range, request, failure)).ConfigureAwait(false);
            return;
        }

        await MarshalAsync(() => OnFetchSucceeded(range, request, result)).ConfigureAwait(false);
    }

    private async Task MarshalAsync(Action action)
    {
        try
        {
            var dispatcher = _owner.Dispatcher;

            if (dispatcher is null)
            {
                return;
            }

            await dispatcher.InvokeAsync(action).ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
        }
        catch (InvalidOperationException)
        {
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void OnFetchCancelled(PendingRange range)
    {
        if (_disposed)
        {
            return;
        }

        _ = _pending.Remove(range);
        UpdateLoadState();
    }

    private void OnFetchSucceeded(PendingRange range, TableDataRequest request, TableDataAdapterResult result)
    {
        if (_disposed)
        {
            return;
        }

        // Mirrors OnFetchFailed's structure: the range is removed from _pending unconditionally,
        // before the generation check, not folded into the same guard that rejects a stale
        // completion. A source that ignores its cancellation token and completes successfully
        // after Reload() already bumped the generation must still free its concurrency slot here -
        // otherwise it leaks a permanently stranded _pending entry, and four such leaks silently
        // stop IssueFetchesForGaps from ever issuing another fetch for this binding's lifetime.
        if (!_pending.Remove(range))
        {
            return;
        }

        if (range.Generation != _generation)
        {
            range.Cts.Dispose();
            UpdateLoadState();
            return;
        }

        Exception? failure = null;
        List<CacheEntry>? built = null;

        try
        {
            ValidateStructural(result, request);
            built = BuildEntries(result, request);
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        if (failure is not null)
        {
            OnFetchFailed(range, request, failure);
            return;
        }

        range.Cts.Dispose();

        // A newly cached entry can silently correct ActiveIndex when its key matches a
        // previously unresolved ActiveKey (see AddToCache) - captured here so that correction
        // still raises the same notification an explicit SelectIndex/SelectKey call would.
        var (previousActiveIndex, previousActiveKey, previousSelectedKeys) = CaptureSelectionState();

        // A key can already be selected before its own range ever loaded (SelectKey never
        // requires cache-resolution first) - tracked here so a row realized for an
        // already-selected key still reaches SelectionChanged, even though neither the selected-
        // key set nor the active location changes as a result of this commit.
        List<TableRow>? newlyRealizedSelectedRows = null;

        foreach (var entry in built!)
        {
            AddToCache(entry);

            if (_selectedKeys.Contains(entry.Key))
            {
                (newlyRealizedSelectedRows ??= []).Add(entry.Row);
            }
        }

        _ = _errorIndices.RemoveWhere(index => index >= request.StartIndex && index < request.StartIndex + request.Count);
        _knownFrontier = Math.Max(_knownFrontier, request.StartIndex + result.Items.Count - 1);

        if (result.IsEndOfData)
        {
            IsEndOfData = true;
        }

        NotifySelectionChanged(previousActiveIndex, previousActiveKey, previousSelectedKeys, newlyRealizedSelectedRows);
        UpdateLoadState();
        RewindowFollowingCommit();
    }

    private void OnFetchFailed(PendingRange range, TableDataRequest request, Exception failure)
    {
        if (_disposed)
        {
            return;
        }

        _ = _pending.Remove(range);

        if (range.Generation != _generation)
        {
            range.Cts.Dispose();
            UpdateLoadState();
            return;
        }

        if (range.Attempt < _maxAttempts)
        {
            range.Attempt++;
            ScheduleRetry(range);
            UpdateLoadState();
            return;
        }

        range.Cts.Dispose();

        for (var index = request.StartIndex; index < request.StartIndex + request.Count; index++)
        {
            _ = _errorIndices.Add(index);
        }

        UpdateLoadState();
        LoadFailed?.Invoke(_owner, new TableLoadFailedEventArgs(request, failure));
        RewindowFollowingCommit();
    }

    private void ScheduleRetry(PendingRange range)
    {
        _pending.Add(range);
        var dispatcher = _owner.Dispatcher;

        if (dispatcher is null)
        {
            _ = _pending.Remove(range);
            return;
        }

        range.RetryTimer = dispatcher.TimeProvider.CreateTimer(
            static state =>
            {
                var retryState = (RetryState) state!;
                retryState.Controller.OnRetryElapsed(retryState.Range);
            },
            new RetryState(this, range),
            _retryDelay,
            Timeout.InfiniteTimeSpan);
    }

    private void OnRetryElapsed(PendingRange range)
    {
        try
        {
            _owner.Dispatcher?.Post(() => ReissueRetry(range));
        }
        catch (ObjectDisposedException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    private void ReissueRetry(PendingRange range)
    {
        if (_disposed || range.Generation != _generation || !_pending.Contains(range) || range.Cts.IsCancellationRequested)
        {
            _ = _pending.Remove(range);
            range.RetryTimer?.Dispose();
            return;
        }

        range.RetryTimer?.Dispose();
        range.RetryTimer = null;
        _ = RunFetchAsync(range);
    }

    private sealed record RetryState(TableDataController Controller, PendingRange Range);

    private static void ValidateStructural(TableDataAdapterResult result, TableDataRequest request)
    {
        if (result.Items.Count > request.Count)
        {
            throw new InvalidOperationException(
                "ITableDataSource<T>.LoadAsync returned more items than requested.");
        }

        if (result.Items.Count < request.Count && !result.IsEndOfData)
        {
            throw new InvalidOperationException(
                "ITableDataSource<T>.LoadAsync returned fewer items than requested without reporting IsEndOfData.");
        }

        if (result.Items.Count == 0 && !result.IsEndOfData)
        {
            throw new InvalidOperationException(
                "ITableDataSource<T>.LoadAsync returned no items without reporting IsEndOfData.");
        }
    }

    private List<CacheEntry> BuildEntries(TableDataAdapterResult result, TableDataRequest request)
    {
        var entries = new List<CacheEntry>(result.Items.Count);
        HashSet<object> seenInBatch = new(EqualityComparer<object>.Default);

        for (var offset = 0; offset < result.Items.Count; offset++)
        {
            var item = result.Items[offset]!;
            var key = _adapter.GetKey(item);
            var index = request.StartIndex + offset;

            if (!seenInBatch.Add(key))
            {
                throw new InvalidOperationException("ITableDataSource<T>.LoadAsync returned a duplicate key.");
            }

            if (_keyIndex.TryGetValue(key, out var existingIndex) && existingIndex != index)
            {
                throw new InvalidOperationException(
                    "ITableDataSource<T>.GetKey returned a key already cached at a different index.");
            }

            var row = _adapter.BuildRow(item);
            entries.Add(new CacheEntry(index, item, key, row));
        }

        return entries;
    }

    private void AddToCache(CacheEntry entry)
    {
        _cache[entry.Index] = entry;
        _keyIndex[entry.Key] = entry.Index;

        if (entry.Key.Equals(ActiveKey))
        {
            ActiveIndex = entry.Index;
        }
    }

    private void CancelOutOfRangePending(int first, int last)
    {
        for (var index = _pending.Count - 1; index >= 0; index--)
        {
            var range = _pending[index];
            var rangeLast = range.Start + range.Count - 1;

            if (rangeLast < first || range.Start > last)
            {
                range.Cts.Cancel();
                range.Cts.Dispose();
                range.RetryTimer?.Dispose();
                _pending.RemoveAt(index);
            }
        }

        UpdateLoadState();
    }

    private void RealizeWindow(int first, int last)
    {
        var newCount = last - first + 1;
        var newWindow = new TableRow?[newCount];

        for (var position = 0; position < _window.Length; position++)
        {
            var logicalIndex = WindowStart + position;

            if (logicalIndex >= first && logicalIndex <= last)
            {
                continue;
            }

            if (_window[position] is { } leaving)
            {
                RemoveFromPresenter(leaving);
            }
        }

        for (var i = 0; i < newCount; i++)
        {
            var logicalIndex = first + i;
            var existingPosition = logicalIndex - WindowStart;
            var row = (uint) existingPosition < (uint) _window.Length ? _window[existingPosition] : null;

            // A placeholder occupying this window position can go stale without ever leaving the
            // window: the range it stands in for can resolve into a real cached row, or its error
            // status can change (a pending fetch just failed in place, or a failed range is
            // retried) - while this loop otherwise only ever rebuilds a row when it enters the
            // window fresh. Left alone, the stale placeholder instance built at first realization
            // would keep rendering forever, never actually swapping in the loaded row or the
            // updated error glyph.
            if (row is not null && row.Cells.Count > 0 && row.Cells[0] is TablePlaceholderCell placeholderCell)
            {
                var nowCached = _cache.ContainsKey(logicalIndex);
                var shouldShowError = _errorIndices.Contains(logicalIndex);

                if (nowCached || placeholderCell.IsError != shouldShowError)
                {
                    RemoveFromPresenter(row);
                    row = null;
                }
            }

            if (row is null)
            {
                row = Realize(logicalIndex);
                AddToPresenter(row);
            }

            newWindow[i] = row;
        }

        _window = newWindow;
        WindowStart = first;
    }

    private TableRow Realize(int logicalIndex)
    {
        if (_cache.TryGetValue(logicalIndex, out var entry))
        {
            return entry.Row;
        }

        var isError = _errorIndices.Contains(logicalIndex);
        var columnCount = Math.Max(1, _owner.Columns.Count);
        var cells = new ControlBase[columnCount];

        for (var index = 0; index < columnCount; index++)
        {
            cells[index] = new TablePlaceholderCell(_owner, isError);
        }

        return new TableRow(cells);
    }

    private void AddToPresenter(TableRow row)
    {
        foreach (var cell in row.Cells)
        {
            _presenter.Children.Add(cell);
        }
    }

    private void RemoveFromPresenter(TableRow row)
    {
        var isCached = false;

        foreach (var entry in _cache.Values)
        {
            if (ReferenceEquals(entry.Row, row))
            {
                isCached = true;
                break;
            }
        }

        foreach (var cell in row.Cells)
        {
            _ = _presenter.Children.Remove(cell);

            if (!isCached)
            {
                cell.Dispose();
            }
        }
    }

    private void EvictCache(int first, int last, int viewportRows)
    {
        var lowerBound = first - (2 * viewportRows);
        var upperBound = last + (2 * viewportRows);
        List<int> stale = [];

        foreach (var index in _cache.Keys)
        {
            if (index < lowerBound || index > upperBound)
            {
                stale.Add(index);
            }
        }

        foreach (var index in stale)
        {
            var entry = _cache[index];
            _ = _cache.Remove(index);

            if (_keyIndex.TryGetValue(entry.Key, out var mappedIndex) && mappedIndex == index)
            {
                _ = _keyIndex.Remove(entry.Key);
            }
        }

        // _errorIndices is unbounded otherwise: a backend that keeps failing the same distant
        // range would grow it forever, since nothing else ever removes an index from it besides a
        // later successful fetch of that exact range (see OnFetchSucceeded).
        _ = _errorIndices.RemoveWhere(index => index < lowerBound || index > upperBound);
    }

    private void DerealizeAll()
    {
        foreach (var row in _window)
        {
            if (row is { } present)
            {
                RemoveFromPresenter(present);
            }
        }

        _window = [];
        WindowStart = 0;
    }

    private void RewindowFollowingCommit()
    {
        if (!_disposed)
        {
            _owner.ProgressiveRewindow();
        }
    }

    private void ArrangeWindow()
    {
        if (_window.Length == 0)
        {
            return;
        }

        var origin = _presenter.ProgressiveOrigin;
        var headerHeight = _presenter.ProgressiveHeaderHeight;
        var rowGap = _presenter.RowGap;
        var columnGap = _presenter.ColumnGap;
        var columnWidths = _presenter.ColumnWidths;
        var cellPadding = _owner.ActualStyle.CellPadding;
        var baseY = origin.Y - _presenter.VerticalOffset + headerHeight;
        var baseX = origin.X - _presenter.HorizontalOffset;

        for (var i = 0; i < _window.Length; i++)
        {
            if (_window[i] is not { } row)
            {
                continue;
            }

            var logicalIndex = WindowStart + i;
            var y = baseY + (logicalIndex * (RowHeight + rowGap));
            var x = baseX;

            for (var column = 0; column < row.Cells.Count && column < columnWidths.Length; column++)
            {
                var slot = new Rect(x, y, columnWidths[column], RowHeight);
                var padded = cellPadding.Deflate(slot);
                var cell = row.Cells[column];
                cell.Measure(new Constraint(Math.Max(0, padded.Width), Math.Max(0, padded.Height)));
                cell.Arrange(padded, widthResolved: true, heightResolved: true);
                x += columnWidths[column] + columnGap;
            }
        }
    }

    private void UpdateLoadState()
    {
        var next = _pending.Count > 0
            ? TableLoadState.Loading
            : _errorIndices.Count > 0
                ? TableLoadState.Failed
                : TableLoadState.Idle;

        if (next == LoadState)
        {
            return;
        }

        var previous = LoadState;
        LoadState = next;
        LoadStateChanged?.Invoke(_owner, new TableLoadStateChangedEventArgs(previous, next));
    }

    /// <summary>Resolves the realized row and column at a screen point, using arithmetic row geometry.</summary>
    /// <param name="point">The absolute terminal-cell point.</param>
    /// <param name="logicalIndex">The resolved zero-based logical index.</param>
    /// <param name="columnIndex">The resolved zero-based column index.</param>
    /// <returns>True when the point resolves to a valid logical index and column.</returns>
    public bool TryResolvePoint(Point point, out int logicalIndex, out int columnIndex)
    {
        logicalIndex = -1;
        columnIndex = -1;

        if (_owner.Columns.Count == 0 || LogicalCount == 0)
        {
            return false;
        }

        var origin = _presenter.ProgressiveOrigin;
        var headerHeight = _presenter.ProgressiveHeaderHeight;
        var rowGap = _presenter.RowGap;
        var baseY = origin.Y - _presenter.VerticalOffset + headerHeight;

        if (point.Y < baseY)
        {
            return false;
        }

        var stride = RowHeight + rowGap;
        var candidate = (point.Y - baseY) / stride;

        if ((point.Y - baseY) % stride >= RowHeight)
        {
            // Inside the inter-row gap, not a data row.
            return false;
        }

        if (candidate < 0 || candidate >= LogicalCount)
        {
            return false;
        }

        var columnWidths = _presenter.ColumnWidths;
        var columnGap = _presenter.ColumnGap;
        var x = origin.X - _presenter.HorizontalOffset;

        for (var column = 0; column < columnWidths.Length; column++)
        {
            if (point.X >= x && point.X < x + columnWidths[column])
            {
                logicalIndex = candidate;
                columnIndex = column;
                return true;
            }

            x += columnWidths[column] + columnGap;
        }

        return false;
    }

    private sealed class CacheEntry(int index, object item, object key, TableRow row)
    {
        public int Index { get; } = index;

        public object Item { get; } = item;

        public object Key { get; } = key;

        public TableRow Row { get; } = row;
    }

    private sealed class PendingRange(int start, int count, long generation, CancellationTokenSource cts)
    {
        public int Start { get; } = start;

        public int Count { get; } = count;

        public long Generation { get; } = generation;

        public CancellationTokenSource Cts { get; } = cts;

        public int Attempt { get; set; } = 1;

        public ITimer? RetryTimer { get; set; }
    }
}
