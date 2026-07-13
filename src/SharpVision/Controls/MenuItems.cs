namespace SharpVision.Controls;

using System.Collections;

/// <summary>Exposes one menu's typed managed item collection.</summary>
public sealed class MenuItems: IReadOnlyList<MenuItem>
{
    private readonly Menu _owner;

    /// <summary>Initializes a typed view over one non-null menu owner.</summary>
    /// <param name="owner">The owning menu.</param>
    /// <exception cref="ArgumentNullException"><paramref name="owner"/> is null.</exception>
    internal MenuItems(Menu owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _owner = owner;
    }

    /// <inheritdoc/>
    public MenuItem this[int index] => _owner.ItemAt(index);

    /// <inheritdoc/>
    public int Count => _owner.Children.Count;

    /// <summary>Adds one detached non-null menu item.</summary>
    /// <param name="item">The item to own.</param>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    /// <exception cref="ArgumentException">The item already belongs to a control tree.</exception>
    /// <exception cref="InvalidOperationException">The attached menu is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The menu or item is disposed.</exception>
    public void Add(MenuItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _owner.Add(item);
    }

    /// <summary>Removes one owned menu item.</summary>
    /// <param name="item">The non-null owned item.</param>
    /// <returns>True when ownership was removed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The attached menu is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The menu or item is disposed.</exception>
    public bool Remove(MenuItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return _owner.Remove(item);
    }

    /// <summary>Removes every owned item.</summary>
    /// <exception cref="InvalidOperationException">The attached menu is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The menu is disposed.</exception>
    public void Clear() => _owner.ClearItems();

    /// <inheritdoc/>
    public IEnumerator<MenuItem> GetEnumerator()
    {
        for (var index = 0; index < Count; index++)
        {
            yield return this[index];
        }
    }

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
