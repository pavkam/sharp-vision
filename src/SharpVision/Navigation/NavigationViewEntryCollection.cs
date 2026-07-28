// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Navigation;

/// <summary>Exposes one navigation view's constrained item collection.</summary>
[PublicAPI]
public sealed class NavigationViewEntryCollection: IReadOnlyList<Control>
{
    private readonly NavigationView _owner;
    private readonly bool _isFooter;

    /// <summary>Initializes a typed view over one navigation view section.</summary>
    internal NavigationViewEntryCollection(NavigationView owner, bool isFooter)
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
    public void Add(NavigationViewItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _owner.AddEntry(item, _isFooter);
    }

    /// <summary>Adds one detached navigation group.</summary>
    public void Add(NavigationViewGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);
        _owner.AddEntry(group, _isFooter);
    }

    /// <summary>Adds one detached separator.</summary>
    public void Add(NavigationViewSeparator separator)
    {
        ArgumentNullException.ThrowIfNull(separator);
        _owner.AddEntry(separator, _isFooter);
    }

    /// <summary>Removes one owned item.</summary>
    public bool Remove(NavigationViewItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return _owner.RemoveEntry(item, _isFooter);
    }

    /// <summary>Removes one owned group.</summary>
    public bool Remove(NavigationViewGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);
        return _owner.RemoveEntry(group, _isFooter);
    }

    /// <summary>Removes one owned separator.</summary>
    public bool Remove(NavigationViewSeparator separator)
    {
        ArgumentNullException.ThrowIfNull(separator);
        return _owner.RemoveEntry(separator, _isFooter);
    }

    /// <summary>Removes every owned item.</summary>
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
