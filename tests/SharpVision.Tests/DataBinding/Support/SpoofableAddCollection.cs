// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.DataBinding.Support;

using System.Collections.ObjectModel;
using System.Collections.Specialized;

/// <summary>
/// An observable collection that can raise an Add notification whose reported index does not
/// match its actual item count, simulating a hand-rolled <see cref="INotifyCollectionChanged"/>
/// source or a coalesced/batched event rather than an ordinary <see cref="ObservableCollection{T}"/>
/// mutation, which always keeps the two in sync.
/// </summary>
internal sealed class SpoofableAddCollection<T>: ObservableCollection<T>
{
    /// <summary>Raises an Add notification for <paramref name="item"/> at <paramref name="index"/> without inserting it.</summary>
    public void RaiseSpoofedAdd(T item, int index) =>
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, index));
}
