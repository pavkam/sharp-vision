// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>Deterministic <see cref="ITableDataSource{T}"/> test double. Resolves every
/// <see cref="LoadAsync"/> immediately by default; call <see cref="Gate"/> to hold every future
/// response until <see cref="Release(int)"/> or <see cref="ReleaseAll"/> completes it explicitly,
/// so a test can observe in-flight state without any real delay.</summary>
/// <typeparam name="T">The item type.</typeparam>
internal sealed class FakeTableDataSource<T>: ITableDataSource<T>
{
    private readonly Lock _gate = new();
    private readonly List<T> _items;
    private readonly Func<T, object> _keySelector;

    // A list, not a dictionary keyed by StartIndex: a superseded request (e.g. one still in
    // flight when Reload() bumps the generation and a fresh request for the exact same range is
    // immediately reissued) must stay independently addressable, in issue order, rather than
    // being silently overwritten by the newer request at the same start index.
    private readonly List<HeldEntry> _held = [];
    private bool _isGated;
    private int? _count;

    /// <summary>Initializes a fake source over a fixed backing list.</summary>
    /// <param name="items">The complete backing item sequence.</param>
    /// <param name="keySelector">Derives a stable key for one item.</param>
    /// <param name="count">The initially reported total count, or null while unknown.</param>
    public FakeTableDataSource(IEnumerable<T> items, Func<T, object> keySelector, int? count = null)
    {
        _items = [.. items];
        _keySelector = keySelector;
        _count = count;
    }

    /// <summary>Gets the number of times <see cref="LoadAsync"/> was invoked.</summary>
    public int LoadCallCount { get; private set; }

    /// <summary>Gets every request <see cref="LoadAsync"/> was invoked with, in call order.</summary>
    public List<TableDataRequest> Requests { get; } = [];

    /// <summary>Gets or sets a predicate that throws a simulated failure for a matching request
    /// instead of resolving it. Evaluated once per <see cref="LoadAsync"/> call.</summary>
    public Func<TableDataRequest, bool>? FailWhen { get; set; }

    /// <summary>Gets or sets whether a held request observes its cancellation token and completes
    /// as canceled once the caller cancels it, like a well-behaved source. Set false to model a
    /// misbehaving source that ignores cancellation and only ever completes through an explicit
    /// <see cref="Release(int)"/>/<see cref="ReleaseAll"/> call, for exercising the controller's own
    /// stale-generation defense.</summary>
    public bool HonorCancellation { get; set; } = true;

    /// <inheritdoc/>
    public int? Count
    {
        get => _count;
        set
        {
            _count = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <inheritdoc/>
    public event EventHandler? Changed;

    /// <summary>Raises <see cref="Changed"/> without altering <see cref="Count"/>.</summary>
    public void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);

    /// <summary>Replaces the backing item at a zero-based index in place, without raising
    /// <see cref="Changed"/> - for exercising a caller-driven <c>Table.Reload()</c> that must pick
    /// up a backing mutation the source itself never announced.</summary>
    /// <param name="index">The zero-based index to replace.</param>
    /// <param name="item">The replacement item.</param>
    public void Replace(int index, T item)
    {
        lock (_gate)
        {
            _items[index] = item;
        }
    }

    /// <inheritdoc/>
    public object GetKey(T item) => _keySelector(item);

    /// <summary>Holds every future <see cref="LoadAsync"/> response until explicitly released.</summary>
    public void Gate()
    {
        lock (_gate)
        {
            _isGated = true;
        }
    }

    /// <summary>Stops holding future responses; already-held requests remain held until released.</summary>
    public void Ungate()
    {
        lock (_gate)
        {
            _isGated = false;
        }
    }

    /// <inheritdoc/>
    public ValueTask<TableDataResult<T>> LoadAsync(TableDataRequest request, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            LoadCallCount++;
            Requests.Add(request);
        }

        if (FailWhen?.Invoke(request) == true)
        {
            throw new InvalidOperationException("Simulated fetch failure.");
        }

        lock (_gate)
        {
            if (!_isGated)
            {
                return ValueTask.FromResult(BuildResult(request));
            }

            // Deliberately left at the default (not RunContinuationsAsynchronously): Release/
            // ReleaseAll complete this synchronously on whatever thread calls them, which is
            // usually enough to drive the controller's ConfigureAwait(false) chain inline. It is
            // NOT a guarantee, though - the TPL's stack-depth guard against unbounded synchronous
            // continuation chains can still force the resumption onto a thread-pool thread instead,
            // and callers that need to observe the effect (e.g. TableDataControllerSurfaceTests'
            // SettleUntilAsync) must tolerate that hop rather than assume a single release-and-
            // settle always lands within the same dispatcher turn.
            var completion = new TaskCompletionSource<TableDataResult<T>>();
            var entry = new HeldEntry(request, completion);
            _held.Add(entry);

            if (HonorCancellation)
            {
                _ = cancellationToken.Register(() =>
                {
                    lock (_gate)
                    {
                        _ = _held.Remove(entry);
                    }

                    _ = completion.TrySetCanceled(cancellationToken);
                });
            }

            return new ValueTask<TableDataResult<T>>(completion.Task);
        }
    }

    /// <summary>Gets whether a request starting at a given index is currently held.</summary>
    /// <param name="startIndex">The candidate request start index.</param>
    public bool IsHeld(int startIndex)
    {
        lock (_gate)
        {
            return _held.Exists(entry => entry.Request.StartIndex == startIndex);
        }
    }

    /// <summary>Gets the number of currently held (not yet released) requests.</summary>
    public int HeldCount
    {
        get
        {
            lock (_gate)
            {
                return _held.Count;
            }
        }
    }

    /// <summary>Completes the oldest still-held request matching a start index, if any. Requests
    /// are held in issue order, so releasing repeatedly for the same start index resolves a
    /// superseded (e.g. pre-Reload) request before the one that replaced it.</summary>
    /// <param name="startIndex">The held request's start index.</param>
    public void Release(int startIndex)
    {
        HeldEntry? entry = null;

        lock (_gate)
        {
            var index = _held.FindIndex(candidate => candidate.Request.StartIndex == startIndex);

            if (index >= 0)
            {
                entry = _held[index];
                _held.RemoveAt(index);
            }
        }

        if (entry is not null)
        {
            _ = entry.Completion.TrySetResult(BuildResult(entry.Request));
        }
    }

    /// <summary>Completes every currently held request, in issue order.</summary>
    public void ReleaseAll()
    {
        List<HeldEntry> held;

        lock (_gate)
        {
            held = [.. _held];
            _held.Clear();
        }

        foreach (var entry in held)
        {
            _ = entry.Completion.TrySetResult(BuildResult(entry.Request));
        }
    }

    private TableDataResult<T> BuildResult(TableDataRequest request)
    {
        if (request.StartIndex >= _items.Count)
        {
            return new TableDataResult<T> { Items = [], IsEndOfData = true };
        }

        var end = Math.Min(request.StartIndex + request.Count, _items.Count);
        var slice = _items.GetRange(request.StartIndex, end - request.StartIndex);
        var isEnd = _count is { } total ? end >= total : end >= _items.Count;
        return new TableDataResult<T> { Items = slice, IsEndOfData = isEnd };
    }

    private sealed class HeldEntry(TableDataRequest request, TaskCompletionSource<TableDataResult<T>> completion)
    {
        public TableDataRequest Request { get; } = request;

        public TaskCompletionSource<TableDataResult<T>> Completion { get; } = completion;
    }
}
