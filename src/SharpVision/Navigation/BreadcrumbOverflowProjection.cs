// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Navigation;

using SharpVision.Menus;

/// <summary>Owns one ephemeral menu adapter for an overflowed semantic breadcrumb item.</summary>
internal sealed class BreadcrumbOverflowProjection: IDisposable
{
    private readonly Breadcrumb _owner;
    private readonly BreadcrumbItem _source;
    private readonly long _collectionGeneration;
    private readonly long _overflowGeneration;
    private bool _isDisposed;

    /// <summary>Initializes a projection guarded by exact owner generations.</summary>
    internal BreadcrumbOverflowProjection(
        Breadcrumb owner,
        BreadcrumbItem source,
        long collectionGeneration,
        long overflowGeneration)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(source);
        _owner = owner;
        _source = source;
        _collectionGeneration = collectionGeneration;
        _overflowGeneration = overflowGeneration;
        Item = new MenuItem { Text = source.Text, UseMnemonic = source.UseMnemonic };
        Item.Invoked += OnInvoked;
    }

    /// <summary>Gets the retained private menu item.</summary>
    internal MenuItem Item { get; }

    /// <inheritdoc/>
    public void Dispose() => Retire(disposeItem: true);

    /// <summary>Releases the adapter, optionally disposing an item not already entering owner disposal.</summary>
    internal void Retire(bool disposeItem)
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        Item.Invoked -= OnInvoked;

        if (disposeItem)
        {
            Item.Dispose();
        }
    }

    private void OnInvoked(object? sender, MenuItemInvokedEventArgs eventArgs)
    {
        _ = sender;
        _ = _owner.TryActivateProjection(
            _source,
            eventArgs.Cause,
            _collectionGeneration,
            _overflowGeneration);
    }
}
