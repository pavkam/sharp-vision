// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Navigation;

/// <summary>Exposes one navigation group's constrained sub-item collection.</summary>
[PublicAPI]
public sealed class NavigationViewItemCollection: IReadOnlyList<NavigationViewItem>
{
    private readonly NavigationViewGroup _owner;

    /// <summary>Initializes a typed view over one navigation group's sub-items.</summary>
    internal NavigationViewItemCollection(NavigationViewGroup owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _owner = owner;
    }

    /// <inheritdoc/>
    public NavigationViewItem this[int index] => _owner.ItemAt(index);

    /// <inheritdoc/>
    public int Count => _owner.ItemCount;

    /// <summary>Adds one detached sub-item.</summary>
    /// <param name="item">The non-null detached item.</param>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    /// <exception cref="ArgumentException">The item already belongs to a control tree.</exception>
    /// <exception cref="InvalidOperationException">An attached owner is mutated off its dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">An attached owner or the item is disposed.</exception>
    public void Add(NavigationViewItem item)
    {
        _owner.VerifyMutation();
        _owner.AddItemCore(item);
    }

    /// <summary>Removes one owned sub-item.</summary>
    /// <param name="item">The non-null candidate item.</param>
    /// <returns>True when the item was owned and removed; otherwise, false.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    /// <exception cref="InvalidOperationException">An attached owner is mutated off its dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">An attached owner is disposed.</exception>
    public bool Remove(NavigationViewItem item)
    {
        _owner.VerifyMutation();
        return _owner.RemoveItemCore(item);
    }

    /// <summary>Removes every owned sub-item.</summary>
    /// <exception cref="InvalidOperationException">An attached owner is mutated off its dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">An attached owner is disposed.</exception>
    public void Clear()
    {
        _owner.VerifyMutation();
        _owner.ClearItemsCore();
    }

    /// <inheritdoc/>
    public IEnumerator<NavigationViewItem> GetEnumerator()
    {
        for (var index = 0; index < Count; index++)
        {
            yield return this[index];
        }
    }

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
