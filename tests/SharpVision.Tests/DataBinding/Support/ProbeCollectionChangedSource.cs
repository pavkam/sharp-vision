// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.DataBinding.Support;

using System.Collections.Specialized;

/// <summary>Exposes deterministic collection-event accessor failures and reentry.</summary>
internal sealed class ProbeCollectionChangedSource: INotifyCollectionChanged
{
    private NotifyCollectionChangedEventHandler? _handlers;

    /// <inheritdoc/>
    public event NotifyCollectionChangedEventHandler? CollectionChanged
    {
        add
        {
            Adding?.Invoke();

            if (ThrowOnNextAdd)
            {
                ThrowOnNextAdd = false;
                throw new InvalidOperationException("Synthetic collection subscription failure.");
            }

            _handlers += value;

            if (ThrowAfterNextAdd)
            {
                ThrowAfterNextAdd = false;
                throw new InvalidOperationException("Synthetic post-registration subscription failure.");
            }

            Added?.Invoke();
        }
        remove
        {
            if (ThrowOnNextRemove)
            {
                ThrowOnNextRemove = false;
                throw new InvalidOperationException("Synthetic collection unsubscription failure.");
            }

            _handlers -= value;
        }
    }

    /// <summary>Gets optional work invoked from the event add accessor.</summary>
    internal Action? Adding { get; set; }

    /// <summary>Gets optional work invoked after handler registration but before accessor return.</summary>
    internal Action? Added { get; set; }

    /// <summary>Gets the number of currently registered handlers.</summary>
    internal int SubscriberCount => _handlers?.GetInvocationList().Length ?? 0;

    /// <summary>Gets or sets whether the next add accessor throws before registration.</summary>
    internal bool ThrowOnNextAdd { get; set; }

    /// <summary>Gets or sets whether the next add accessor throws after registering its handler.</summary>
    internal bool ThrowAfterNextAdd { get; set; }

    /// <summary>Gets or sets whether the next remove accessor throws before removal.</summary>
    internal bool ThrowOnNextRemove { get; set; }

    /// <summary>Raises one deterministic add notification.</summary>
    internal void RaiseAdd() => _handlers?.Invoke(
        this,
        new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, "item", 0));
}
