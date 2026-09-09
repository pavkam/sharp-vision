// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Menus;

/// <summary>Exposes one menu's constrained item and separator collection.</summary>
[PublicAPI]
public sealed class MenuEntryCollection: ItemCollection<ControlBase>
{
    private readonly Menu _owner;

    /// <summary>Initializes a typed view over one non-null menu owner.</summary>
    /// <param name="owner">The owning menu.</param>
    /// <exception cref="ArgumentNullException"><paramref name="owner"/> is null.</exception>
    internal MenuEntryCollection(Menu owner)
        : base(owner) =>
        _owner = owner;

    /// <summary>Gets or replaces one owned entry, preserving its position.</summary>
    /// <exception cref="ArgumentNullException">The assigned value is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the current entries.</exception>
    /// <exception cref="ArgumentException">The assigned entry already belongs to a control tree.</exception>
    /// <exception cref="InvalidOperationException">
    /// The attached menu is mutated off-dispatcher, or the assigned entry is not a <see cref="MenuItem"/> or
    /// <see cref="MenuSeparator"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The menu or the assigned entry is disposed.</exception>
    public override ControlBase this[int index]
    {
        get => base[index];
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _owner.ReplaceEntry(index, value);
        }
    }

    /// <summary>Adds one detached non-null menu item.</summary>
    /// <param name="item">The menu item to own.</param>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    /// <exception cref="ArgumentException">The item already belongs to a control tree.</exception>
    /// <exception cref="InvalidOperationException">The attached menu is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The menu or item is disposed.</exception>
    public void Add(MenuItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        Add((ControlBase) item);
    }

    /// <summary>Adds one detached non-null menu separator.</summary>
    /// <param name="separator">The menu separator to own.</param>
    /// <exception cref="ArgumentNullException"><paramref name="separator"/> is null.</exception>
    /// <exception cref="ArgumentException">The separator already belongs to a control tree.</exception>
    /// <exception cref="InvalidOperationException">The attached menu is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The menu or separator is disposed.</exception>
    public void Add(MenuSeparator separator)
    {
        ArgumentNullException.ThrowIfNull(separator);
        Add((ControlBase) separator);
    }

    /// <summary>Inserts one detached entry at a position, dispatching to the owner's typed overload
    /// for its exact runtime type.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the insertion range.</exception>
    /// <exception cref="ArgumentException">The item already belongs to a control tree.</exception>
    /// <exception cref="InvalidOperationException">
    /// The attached menu is mutated off-dispatcher, or <paramref name="item"/> is not a
    /// <see cref="MenuItem"/> or <see cref="MenuSeparator"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The menu or item is disposed.</exception>
    public override void Insert(int index, ControlBase item)
    {
        ArgumentNullException.ThrowIfNull(item);

        switch (item)
        {
            case MenuItem menuItem:
                _owner.Insert(index, menuItem);
                break;
            case MenuSeparator separator:
                _owner.Insert(index, separator);
                break;
            default:
                throw new InvalidOperationException(
                    "Menus may own only MenuItem and MenuSeparator controls through Items.");
        }
    }

    /// <summary>Inserts one detached non-null menu item at a position.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the insertion range.</exception>
    /// <exception cref="ArgumentException">The item already belongs to a control tree.</exception>
    /// <exception cref="InvalidOperationException">The attached menu is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The menu or item is disposed.</exception>
    public void Insert(int index, MenuItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        Insert(index, (ControlBase) item);
    }

    /// <summary>Inserts one detached non-null menu separator at a position.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="separator"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the insertion range.</exception>
    /// <exception cref="ArgumentException">The separator already belongs to a control tree.</exception>
    /// <exception cref="InvalidOperationException">The attached menu is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The menu or separator is disposed.</exception>
    public void Insert(int index, MenuSeparator separator)
    {
        ArgumentNullException.ThrowIfNull(separator);
        Insert(index, (ControlBase) separator);
    }

    /// <summary>Removes one owned entry, dispatching to the owner's typed overload for its exact
    /// runtime type. An entry of a foreign type was never owned and is reported as not removed.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The attached menu is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The menu is disposed.</exception>
    public override bool Remove(ControlBase item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return item switch
        {
            MenuItem menuItem => _owner.Remove(menuItem),
            MenuSeparator separator => _owner.Remove(separator),
            _ => false,
        };
    }

    /// <summary>Removes one owned menu item.</summary>
    /// <param name="item">The menu item to remove.</param>
    /// <returns>True when ownership was removed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The attached menu is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The menu or item is disposed.</exception>
    public bool Remove(MenuItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Remove((ControlBase) item);
    }

    /// <summary>Removes one owned menu separator.</summary>
    /// <param name="separator">The menu separator to remove.</param>
    /// <returns>True when ownership was removed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="separator"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The attached menu is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The menu or separator is disposed.</exception>
    public bool Remove(MenuSeparator separator)
    {
        ArgumentNullException.ThrowIfNull(separator);
        return Remove((ControlBase) separator);
    }

    /// <summary>Removes the owned entry at a position.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the current entries.</exception>
    /// <exception cref="InvalidOperationException">The attached menu is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The menu is disposed.</exception>
    public override void RemoveAt(int index) => _owner.RemoveAt(index);

    /// <summary>Moves one owned entry to a different position, preserving its identity.</summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="oldIndex"/> or <paramref name="newIndex"/> is outside the current entries.
    /// </exception>
    /// <exception cref="InvalidOperationException">The attached menu is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The menu is disposed.</exception>
    public override void Move(int oldIndex, int newIndex) => _owner.MoveEntry(oldIndex, newIndex);

    /// <summary>Removes every owned item and separator.</summary>
    /// <exception cref="InvalidOperationException">The attached menu is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The menu is disposed.</exception>
    public override void Clear() => _owner.ClearItems();
}
