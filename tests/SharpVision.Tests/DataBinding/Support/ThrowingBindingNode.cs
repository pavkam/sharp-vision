// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.DataBinding.Support;

using System.ComponentModel;

/// <summary>Provides a nested binding node with observable, optionally throwing unsubscription.</summary>
internal sealed class ThrowingBindingNode: INotifyPropertyChanged
{
    private PropertyChangedEventHandler? _handlers;

    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged
    {
        add => _handlers += value;
        remove
        {
            RemovalAttempts++;
            _handlers -= value;

            if (ThrowOnRemove)
            {
                throw new InvalidOperationException("Synthetic property unsubscription failure.");
            }
        }
    }

    /// <summary>Gets or sets the next nested node.</summary>
    public ThrowingBindingNode? Child { get; set; }

    /// <summary>Gets or sets the leaf text.</summary>
    public string? Value { get; set; }

    /// <summary>Gets the number of retained property-change handlers.</summary>
    internal int SubscriberCount => _handlers?.GetInvocationList().Length ?? 0;

    /// <summary>Gets the number of remove-accessor attempts.</summary>
    internal int RemovalAttempts { get; private set; }

    /// <summary>Gets or sets whether removal throws after releasing the handler.</summary>
    internal bool ThrowOnRemove { get; set; }
}
