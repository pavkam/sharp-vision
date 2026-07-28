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

    /// <summary>Gets the single pending collection change, or null when coalesced or absent.</summary>
    public NotifyCollectionChangedEventArgs? PendingChange => _coalesced ? null : _pendingChange;

    /// <summary>Clears the stored pending change after the binding applies it.</summary>
    public void ConsumePendingChange()
    {
        _pendingChange = null;
        _coalesced = false;
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
        _ = sender;
        ArgumentNullException.ThrowIfNull(eventArgs);

        if (_pendingChange is not null)
        {
            _coalesced = true;
        }

        _pendingChange = eventArgs;
        _invalidated();
    }
}
