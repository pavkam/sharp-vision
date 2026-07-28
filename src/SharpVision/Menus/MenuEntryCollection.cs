// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Menus;

/// <summary>Exposes one menu's constrained item and separator collection.</summary>
[PublicAPI]
public sealed class MenuEntryCollection: IReadOnlyList<Control>
{
    private readonly Menu _owner;

    /// <summary>Initializes a typed view over one non-null menu owner.</summary>
    /// <param name="owner">The owning menu.</param>
    /// <exception cref="ArgumentNullException"><paramref name="owner"/> is null.</exception>
    internal MenuEntryCollection(Menu owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _owner = owner;
    }

    /// <inheritdoc/>
    public Control this[int index] => _owner.ItemAt(index);

    /// <inheritdoc/>
    public int Count => _owner.ItemCount;

    /// <summary>Adds one detached non-null menu item.</summary>
    /// <param name="item">The menu item to own.</param>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    /// <exception cref="ArgumentException">The item already belongs to a control tree.</exception>
    /// <exception cref="InvalidOperationException">The attached menu is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The menu or item is disposed.</exception>
    public void Add(MenuItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _owner.Add(item);
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
        _owner.Add(separator);
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
        return _owner.Remove(item);
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
        return _owner.Remove(separator);
    }

    /// <summary>Removes every owned item and separator.</summary>
    /// <exception cref="InvalidOperationException">The attached menu is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The menu is disposed.</exception>
    public void Clear() => _owner.ClearItems();

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
