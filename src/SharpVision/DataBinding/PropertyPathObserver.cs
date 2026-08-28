// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.DataBinding;

using System.ComponentModel;
using System.Runtime.ExceptionServices;

/// <summary>Tracks notifying owners along one nested property path.</summary>
internal sealed class PropertyPathObserver: IDisposable
{
    private readonly Lock _gate = new();
    private readonly PropertyChangedEventHandler?[] _handlers;
    private readonly object?[] _owners;
    private readonly PropertyPath _path;
    private readonly Action _invalidated;
    private readonly object _root;
    private bool _disposed;

    /// <summary>Initializes and subscribes one complete reachable path.</summary>
    public PropertyPathObserver(object root, PropertyPath path, Action invalidated)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(invalidated);
        _root = root;
        _path = path;
        _invalidated = invalidated;
        _owners = new object?[path.Count];
        _handlers = new PropertyChangedEventHandler?[path.Count];

        lock (_gate)
        {
            Rebuild();
        }
    }

    /// <summary>Releases every reachable-owner subscription.</summary>
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            UnsubscribeAll();
        }
    }

    private void Rebuild()
    {
        UnsubscribeAll();
        var current = _root;

        for (var index = 0; index < _path.Count && current is not null; index++)
        {
            var segmentIndex = index;
            _owners[index] = current;

            if (current is INotifyPropertyChanged notifying)
            {
                void HandlePropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
                {
                    _ = sender;
                    OnPropertyChanged(segmentIndex, eventArgs);
                }

                var handler = (PropertyChangedEventHandler) HandlePropertyChanged;
                _handlers[index] = handler;
                notifying.PropertyChanged += handler;
            }

            current = _path.SegmentAt(index).Getter(current);
        }
    }

    private void OnPropertyChanged(int segmentIndex, PropertyChangedEventArgs eventArgs)
    {
        lock (_gate)
        {
            if (_disposed ||
                (!string.IsNullOrEmpty(eventArgs.PropertyName) &&
                 !string.Equals(
                     eventArgs.PropertyName,
                     _path.SegmentAt(segmentIndex).Property.Name,
                     StringComparison.Ordinal)))
            {
                return;
            }

            Rebuild();
        }

        _invalidated();
    }

    private void UnsubscribeAll()
    {
        ExceptionDispatchInfo? failure = null;

        for (var index = 0; index < _owners.Length; index++)
        {
            if (_owners[index] is INotifyPropertyChanged notifying &&
                _handlers[index] is { } handler)
            {
                ExceptionAggregation.Capture(
                    () => notifying.PropertyChanged -= handler,
                    ref failure);
            }

            _owners[index] = null;
            _handlers[index] = null;
        }

        failure?.Throw();
    }
}
