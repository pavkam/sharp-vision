// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.DataBinding;

using System.Collections.Specialized;

/// <summary>Tracks membership notifications from one replaceable collection.</summary>
internal sealed class CollectionObserver: IDisposable
{
    private readonly Lock _gate = new();
    private readonly Action _invalidated;
    private readonly List<INotifyCollectionChanged> _retired = [];
    private INotifyCollectionChanged? _current;
    private NotifyCollectionChangedEventArgs? _pendingChange;
    private long _generation;
    private long _pendingGeneration;
    private long _transitionVersion;
    private bool _coalesced;
    private bool _disposed;

    /// <summary>Initializes an empty observer with one invalidation callback.</summary>
    public CollectionObserver(Action invalidated)
    {
        ArgumentNullException.ThrowIfNull(invalidated);
        _invalidated = invalidated;
    }

    /// <summary>
    /// Atomically takes and clears the single pending collection change,
    /// including any coalesced (unusable) state. A coalesced burst clears on
    /// this call rather than persisting until a future single change happens
    /// to arrive: the caller falls back to a full read on a coalesced miss, so
    /// the accumulator must not stay stuck reporting "coalesced" against a
    /// value the caller has already resynchronized past.
    /// </summary>
    /// <param name="change">The pending change, when one is available and not coalesced.</param>
    /// <returns>Whether an applicable pending change was available.</returns>
    public bool TryTakePendingChange([NotNullWhen(true)] out NotifyCollectionChangedEventArgs? change)
    {
        lock (_gate)
        {
            return TryTakePendingChangeCore(_current, enforceExpectedSource: false, out change);
        }
    }

    /// <summary>
    /// Atomically takes a pending change only when it belongs to the expected current source and
    /// observation generation. Pending state is consumed even when it is stale so a complete
    /// snapshot can establish a fresh baseline.
    /// </summary>
    /// <param name="expectedSource">The collection identity resolved from the current source path.</param>
    /// <param name="change">The current pending change, when one is available.</param>
    /// <returns>Whether a current, non-coalesced pending change was available.</returns>
    public bool TryTakePendingChange(
        object? expectedSource,
        [NotNullWhen(true)] out NotifyCollectionChangedEventArgs? change)
    {
        lock (_gate)
        {
            return TryTakePendingChangeCore(
                expectedSource as INotifyCollectionChanged,
                enforceExpectedSource: true,
                out change);
        }
    }

    /// <summary>
    /// Replaces the currently observed collection through a staged subscription transaction.
    /// Caller-controlled event accessors run outside the state lock. A candidate becomes current
    /// only after its add accessor succeeds, while a synchronous notification during that accessor
    /// is ignored and followed by the caller's complete snapshot refresh.
    /// </summary>
    /// <exception cref="Exception">A collection event accessor reports a subscription or cleanup failure.</exception>
    public void Observe(object? value)
    {
        var replacement = value as INotifyCollectionChanged;
        long transitionVersion;

        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            if (ReferenceEquals(_current, replacement))
            {
                return;
            }

            transitionVersion = ++_transitionVersion;
        }

        if (replacement is not null)
        {
            try
            {
                replacement.CollectionChanged += OnCollectionChanged;
            }
            catch
            {
                try
                {
                    replacement.CollectionChanged -= OnCollectionChanged;
                }
                catch
                {
                    // The original add failure remains authoritative. Disposal still retries any
                    // source that was known to have committed successfully.
                }

                throw;
            }
        }

        INotifyCollectionChanged? previous = null;
        var committed = false;

        lock (_gate)
        {
            if (!_disposed && _transitionVersion == transitionVersion)
            {
                previous = _current;
                _current = replacement;
                _generation++;
                _pendingChange = null;
                _pendingGeneration = 0;
                _coalesced = false;
                committed = true;
            }
        }

        if (!committed)
        {
            replacement?.CollectionChanged -= OnCollectionChanged;
            return;
        }

        if (previous is null)
        {
            return;
        }

        try
        {
            previous.CollectionChanged -= OnCollectionChanged;
        }
        catch
        {
            lock (_gate)
            {
                if (!_retired.Contains(previous))
                {
                    _retired.Add(previous);
                }
            }

            throw;
        }
    }

    /// <summary>Releases every successfully registered collection subscription.</summary>
    /// <exception cref="Exception">A collection event accessor reports a cleanup failure.</exception>
    public void Dispose()
    {
        INotifyCollectionChanged? current;
        INotifyCollectionChanged[] retired;

        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _transitionVersion++;
            current = _current;
            _current = null;
            retired = [.. _retired];
            _retired.Clear();
            _pendingChange = null;
            _pendingGeneration = 0;
            _coalesced = false;
        }

        Exception? failure = null;

        foreach (var source in current is null ? retired : [current, .. retired])
        {
            try
            {
                source.CollectionChanged -= OnCollectionChanged;
            }
            catch (Exception exception)
            {
                failure ??= exception;
            }
        }

        if (failure is not null)
        {
            throw failure;
        }
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);
        bool shouldInvalidate;

        // .NET events snapshot their invocation list before any handler in it
        // runs. Observe() can therefore resubscribe this same instance method
        // to a new collection while an already-snapshotted invocation from the
        // *old* collection is still queued; unsubscription alone cannot stop
        // it. Comparing the delivered sender against the collection Observe()
        // currently tracks — under the same lock Observe() mutates it under —
        // is what rejects that stale delivery instead of corrupting the target
        // with a change from a source the binding has already moved past.
        lock (_gate)
        {
            if (!ReferenceEquals(sender, _current))
            {
                return;
            }

            if (_pendingChange is not null)
            {
                _coalesced = true;
            }

            _pendingChange = eventArgs;
            _pendingGeneration = _generation;
            shouldInvalidate = true;
        }

        if (shouldInvalidate)
        {
            _invalidated();
        }
    }

    private bool TryTakePendingChangeCore(
        INotifyCollectionChanged? expectedSource,
        bool enforceExpectedSource,
        [NotNullWhen(true)] out NotifyCollectionChangedEventArgs? change)
    {
        var isCurrent = !enforceExpectedSource || ReferenceEquals(expectedSource, _current);
        change = isCurrent && !_coalesced && _pendingGeneration == _generation
            ? _pendingChange
            : null;
        _pendingChange = null;
        _pendingGeneration = 0;
        _coalesced = false;
        return change is not null;
    }
}
