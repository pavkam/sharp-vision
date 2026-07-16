// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Exposes one NavigationView section's constrained retained item collection.</summary>
public sealed class NavigationViewItems: IReadOnlyList<Control>
{
    private readonly bool _isFooter;
    private readonly NavigationView _owner;

    /// <summary>Initializes a typed view over one NavigationView section.</summary>
    /// <param name="owner">The non-null owning view.</param>
    /// <param name="isFooter">Whether this collection addresses the pinned footer.</param>
    /// <exception cref="ArgumentNullException"><paramref name="owner"/> is null.</exception>
    internal NavigationViewItems(NavigationView owner, bool isFooter)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _owner = owner;
        _isFooter = isFooter;
    }

    /// <inheritdoc/>
    public Control this[int index] => _owner.GetItem(index, _isFooter);

    /// <inheritdoc/>
    public int Count => _owner.GetItemCount(_isFooter);

    /// <summary>Adds one detached navigation item.</summary>
    /// <param name="item">The non-null detached item.</param>
    public void Add(NavigationViewItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _owner.AddEntry(item, _isFooter);
    }

    /// <summary>Adds one detached navigation group.</summary>
    /// <param name="group">The non-null detached group.</param>
    public void Add(NavigationViewGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);
        _owner.AddEntry(group, _isFooter);
    }

    /// <summary>Adds one detached separator.</summary>
    /// <param name="separator">The non-null detached separator.</param>
    public void Add(NavigationViewSeparator separator)
    {
        ArgumentNullException.ThrowIfNull(separator);
        _owner.AddEntry(separator, _isFooter);
    }

    /// <summary>Removes one owned item without disposing it.</summary>
    public bool Remove(NavigationViewItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return _owner.RemoveEntry(item, _isFooter);
    }

    /// <summary>Removes one owned group without disposing it.</summary>
    public bool Remove(NavigationViewGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);
        return _owner.RemoveEntry(group, _isFooter);
    }

    /// <summary>Removes one owned separator without disposing it.</summary>
    public bool Remove(NavigationViewSeparator separator)
    {
        ArgumentNullException.ThrowIfNull(separator);
        return _owner.RemoveEntry(separator, _isFooter);
    }

    /// <summary>Removes every owned entry without disposing it.</summary>
    public void Clear() => _owner.ClearEntries(_isFooter);

    /// <inheritdoc/>
    public IEnumerator<Control> GetEnumerator()
    {
        for (var index = 0; index < Count; index++)
        {
            yield return this[index];
        }
    }

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
