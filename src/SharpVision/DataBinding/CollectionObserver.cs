// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.DataBinding;

using System.Collections.Specialized;

/// <summary>Tracks membership notifications from one replaceable collection.</summary>
internal sealed class CollectionObserver: IDisposable
{
    private readonly Lock _gate = new();
    private readonly Action _invalidated;
    private INotifyCollectionChanged? _current;
    private NotifyCollectionChangedEventArgs? _pendingChange;
    private bool _coalesced;

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
            change = _coalesced ? null : _pendingChange;
            _pendingChange = null;
            _coalesced = false;
            return change is not null;
        }
    }

    /// <summary>Replaces the currently observed collection identity.</summary>
    public void Observe(object? value)
    {
        lock (_gate)
        {
            var replacement = value as INotifyCollectionChanged;

            if (ReferenceEquals(_current, replacement))
            {
                return;
            }

            _current?.CollectionChanged -= OnCollectionChanged;

            _current = replacement;
            _pendingChange = null;
            _coalesced = false;

            _current?.CollectionChanged += OnCollectionChanged;
        }
    }

    /// <summary>Releases the current collection subscription.</summary>
    public void Dispose()
    {
        lock (_gate)
        {
            _current?.CollectionChanged -= OnCollectionChanged;
            _current = null;
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
            shouldInvalidate = true;
        }

        if (shouldInvalidate)
        {
            _invalidated();
        }
    }
}
