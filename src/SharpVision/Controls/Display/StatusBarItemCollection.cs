// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Display;

/// <summary>Exposes one status bar's constrained item collection.</summary>
[PublicAPI]
public sealed class StatusBarItemCollection: IReadOnlyList<StatusBarItem>
{
    private readonly StatusBar _owner;

    /// <summary>Initializes a typed view over one non-null status bar.</summary>
    /// <param name="owner">The owning status bar.</param>
    /// <exception cref="ArgumentNullException"><paramref name="owner"/> is null.</exception>
    internal StatusBarItemCollection(StatusBar owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _owner = owner;
    }

    /// <summary>Gets or replaces one owned item, preserving its position.</summary>
    /// <exception cref="ArgumentNullException">The assigned value is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the current items.</exception>
    /// <exception cref="ArgumentException">The assigned item already belongs to a control tree.</exception>
    /// <exception cref="InvalidOperationException">The attached bar is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The bar or the assigned item is disposed.</exception>
    public StatusBarItem this[int index]
    {
        get => _owner.ItemAt(index);
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _owner.ReplaceItem(index, value);
        }
    }

    /// <inheritdoc/>
    public int Count => _owner.ItemCount;

    /// <summary>Adds one detached non-null item at the end of the semantic collection.</summary>
    /// <param name="item">The status item to own.</param>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    /// <exception cref="ArgumentException">The item already belongs to a control tree.</exception>
    /// <exception cref="InvalidOperationException">The attached bar is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The bar or item is disposed.</exception>
    public void Add(StatusBarItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _owner.Add(item);
    }

    /// <summary>Inserts one detached non-null item at a position.</summary>
    /// <param name="index">The insertion position from zero through <see cref="Count"/>.</param>
    /// <param name="item">The status item to own.</param>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the insertion range.</exception>
    /// <exception cref="ArgumentException">The item already belongs to a control tree.</exception>
    /// <exception cref="InvalidOperationException">The attached bar is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The bar or item is disposed.</exception>
    public void Insert(int index, StatusBarItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _owner.Insert(index, item);
    }

    /// <summary>Removes one identical owned item without disposing it.</summary>
    /// <param name="item">The non-null item to remove.</param>
    /// <returns>True when the item was removed; otherwise, false.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The attached bar is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The bar is disposed.</exception>
    public bool Remove(StatusBarItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return _owner.Remove(item);
    }

    /// <summary>Removes the owned item at a position without disposing it.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the current items.</exception>
    /// <exception cref="InvalidOperationException">The attached bar is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The bar is disposed.</exception>
    public void RemoveAt(int index) => _owner.RemoveAt(index);

    /// <summary>Moves one owned item to a different position, preserving its identity.</summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="oldIndex"/> or <paramref name="newIndex"/> is outside the current items.
    /// </exception>
    /// <exception cref="InvalidOperationException">The attached bar is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The bar is disposed.</exception>
    public void Move(int oldIndex, int newIndex) => _owner.MoveItem(oldIndex, newIndex);

    /// <summary>Gets the position of one item, or -1 when it is not owned by this bar.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    public int IndexOf(StatusBarItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return _owner.IndexOfItem(item);
    }

    /// <summary>Removes every owned item without disposing the detached items.</summary>
    /// <exception cref="InvalidOperationException">The attached bar is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The bar is disposed.</exception>
    public void Clear() => _owner.ClearItems();

    /// <inheritdoc/>
    public IEnumerator<StatusBarItem> GetEnumerator()
    {
        for (var index = 0; index < Count; index++)
        {
            yield return this[index];
        }
    }

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
