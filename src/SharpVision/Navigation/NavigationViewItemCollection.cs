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
    public void Add(NavigationViewItem item) => _owner.AddItemCore(item);

    /// <summary>Removes one owned sub-item.</summary>
    public bool Remove(NavigationViewItem item) => _owner.RemoveItemCore(item);

    /// <summary>Removes every owned sub-item.</summary>
    public void Clear() => _owner.ClearItemsCore();

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
