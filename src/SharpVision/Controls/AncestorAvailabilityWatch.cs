// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using System.ComponentModel;

/// <summary>Owns the single ancestor subscription backing one
/// <see cref="ControlBase.WatchAncestorAvailability"/> call.</summary>
/// <remarks>
/// Re-resolves the owner's nearest unavailable ancestor with
/// <see cref="ControlBase.FindUnavailableAncestor"/> whenever the currently subscribed ancestor's
/// own <see cref="ControlBase.Visibility"/> or <see cref="ControlBase.IsEnabled"/> commits, moving
/// the subscription to whatever the re-walk finds. The owner's callback runs only when that re-walk
/// finds nothing - every ancestor is available again - never merely because the subscribed ancestor
/// changed to a different, still-unavailable one.
/// </remarks>
internal sealed class AncestorAvailabilityWatch: IDisposable
{
    private readonly ControlBase _owner;
    private readonly Action _onAvailable;
    private ControlBase? _ancestor;
    private bool _isDisposed;

    /// <summary>Starts watching one owner's ancestry, subscribing to its current nearest
    /// unavailable ancestor if it has one.</summary>
    /// <param name="owner">The control whose ancestry is walked.</param>
    /// <param name="onAvailable">Invoked when a re-walk finds every ancestor available.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    internal AncestorAvailabilityWatch(ControlBase owner, Action onAvailable)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(onAvailable);

        _owner = owner;
        _onAvailable = onAvailable;
        Resolve();
    }

    /// <summary>Re-walks the owner's ancestry and moves the subscription to whatever it finds,
    /// notifying the owner only when nothing unavailable remains.</summary>
    private void Resolve()
    {
        var unavailable = _owner.FindUnavailableAncestor();

        if (ReferenceEquals(_ancestor, unavailable))
        {
            return;
        }

        Unsubscribe();

        if (unavailable is not null)
        {
            _ancestor = unavailable;
            unavailable.PropertyChanged += OnAncestorPropertyChanged;
            return;
        }

        _onAvailable();
    }

    private void OnAncestorPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        _ = sender;

        if (eventArgs.PropertyName is nameof(ControlBase.Visibility) or nameof(ControlBase.IsEnabled))
        {
            Resolve();
        }
    }

    private void Unsubscribe()
    {
        if (_ancestor is not { } ancestor)
        {
            return;
        }

        _ancestor = null;
        ancestor.PropertyChanged -= OnAncestorPropertyChanged;
    }

    /// <summary>Stops watching and releases the current ancestor subscription, if any. Safe to call
    /// more than once.</summary>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        Unsubscribe();
    }
}
