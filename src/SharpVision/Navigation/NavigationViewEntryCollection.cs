// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Navigation;

/// <summary>Exposes one navigation view's constrained item collection.</summary>
[PublicAPI]
public sealed class NavigationViewEntryCollection: IReadOnlyList<ControlBase>
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

    /// <summary>Gets or replaces one owned entry, preserving its position.</summary>
    /// <exception cref="ArgumentNullException">The assigned value is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the current entries.</exception>
    /// <exception cref="ArgumentException">
    /// The assigned value is not a navigation item, group, or separator, or it already belongs to a tree.
    /// </exception>
    /// <exception cref="InvalidOperationException">The owner is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The owner or assigned value is disposed.</exception>
    public ControlBase this[int index]
    {
        get => _owner.GetItem(index, _isFooter);
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _owner.ReplaceEntryAt(index, value, _isFooter);
        }
    }

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

    /// <summary>Inserts one detached navigation item at a position.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the insertion range.</exception>
    public void Insert(int index, NavigationViewItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _owner.InsertEntry(index, item, _isFooter);
    }

    /// <summary>Inserts one detached navigation group at a position.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the insertion range.</exception>
    public void Insert(int index, NavigationViewGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);
        _owner.InsertEntry(index, group, _isFooter);
    }

    /// <summary>Inserts one detached separator at a position.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the insertion range.</exception>
    public void Insert(int index, NavigationViewSeparator separator)
    {
        ArgumentNullException.ThrowIfNull(separator);
        _owner.InsertEntry(index, separator, _isFooter);
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

    /// <summary>Removes the owned entry at a position.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the current entries.</exception>
    public void RemoveAt(int index) => _owner.RemoveEntryAt(index, _isFooter);

    /// <summary>Moves one owned entry to a different position, preserving its identity.</summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="oldIndex"/> or <paramref name="newIndex"/> is outside the current entries.
    /// </exception>
    public void Move(int oldIndex, int newIndex) => _owner.MoveEntry(oldIndex, newIndex, _isFooter);

    /// <summary>Gets the position of one entry, or -1 when it is not owned by this section.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    public int IndexOf(ControlBase item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return _owner.IndexOfEntry(item, _isFooter);
    }

    /// <summary>Removes every owned item.</summary>
    public void Clear() => _owner.ClearEntries(_isFooter);

    /// <inheritdoc/>
    public IEnumerator<ControlBase> GetEnumerator()
    {
        for (var index = 0; index < Count; index++)
        {
            yield return this[index];
        }
    }

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
