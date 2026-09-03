// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

/// <summary>Exposes one command bar's constrained semantic item and separator collection.</summary>
[PublicAPI]
public sealed class CommandBarEntryCollection: IReadOnlyList<ControlBase>
{
    private readonly CommandBar _owner;

    /// <summary>Initializes a typed view over one non-null command bar owner.</summary>
    /// <param name="owner">The owning command bar.</param>
    /// <exception cref="ArgumentNullException"><paramref name="owner"/> is null.</exception>
    internal CommandBarEntryCollection(CommandBar owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _owner = owner;
    }

    /// <summary>Gets or replaces one owned entry while preserving its collection position.</summary>
    /// <exception cref="ArgumentNullException">The assigned value is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the collection.</exception>
    /// <exception cref="ArgumentException">The assigned entry already belongs to a control tree.</exception>
    /// <exception cref="InvalidOperationException">
    /// The assigned entry is not a command-bar item or separator, or the attached owner is mutated off-dispatcher.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The owner or assigned entry is disposed.</exception>
    public ControlBase this[int index]
    {
        get => _owner.EntryAt(index);
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _owner.ReplaceEntry(index, value);
        }
    }

    /// <summary>Gets the number of retained semantic entries.</summary>
    public int Count => _owner.EntryCount;

    /// <summary>Adds one detached command item at the end.</summary>
    /// <param name="item">The item to retain.</param>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    /// <exception cref="ArgumentException">The item already belongs to a control tree.</exception>
    /// <exception cref="InvalidOperationException">The attached owner is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The owner or item is disposed.</exception>
    public void Add(CommandBarItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _owner.InsertEntry(Count, item);
    }

    /// <summary>Adds one detached separator at the end.</summary>
    /// <param name="separator">The separator to retain.</param>
    /// <exception cref="ArgumentNullException"><paramref name="separator"/> is null.</exception>
    /// <exception cref="ArgumentException">The separator already belongs to a control tree.</exception>
    /// <exception cref="InvalidOperationException">The attached owner is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The owner or separator is disposed.</exception>
    public void Add(CommandBarSeparator separator)
    {
        ArgumentNullException.ThrowIfNull(separator);
        _owner.InsertEntry(Count, separator);
    }

    /// <summary>Inserts one detached command item at a validated position.</summary>
    /// <param name="index">The insertion position from zero through <see cref="Count"/>.</param>
    /// <param name="item">The item to retain.</param>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the insertion range.</exception>
    /// <exception cref="ArgumentException">The item already belongs to a control tree.</exception>
    /// <exception cref="InvalidOperationException">The attached owner is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The owner or item is disposed.</exception>
    public void Insert(int index, CommandBarItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _owner.InsertEntry(index, item);
    }

    /// <summary>Inserts one detached separator at a validated position.</summary>
    /// <param name="index">The insertion position from zero through <see cref="Count"/>.</param>
    /// <param name="separator">The separator to retain.</param>
    /// <exception cref="ArgumentNullException"><paramref name="separator"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the insertion range.</exception>
    /// <exception cref="ArgumentException">The separator already belongs to a control tree.</exception>
    /// <exception cref="InvalidOperationException">The attached owner is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The owner or separator is disposed.</exception>
    public void Insert(int index, CommandBarSeparator separator)
    {
        ArgumentNullException.ThrowIfNull(separator);
        _owner.InsertEntry(index, separator);
    }

    /// <summary>Removes one identical retained command item without disposing it.</summary>
    /// <param name="item">The item to remove.</param>
    /// <returns>True when ownership was removed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The attached owner is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The owner is disposed.</exception>
    public bool Remove(CommandBarItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return _owner.RemoveEntry(item);
    }

    /// <summary>Removes one identical retained separator without disposing it.</summary>
    /// <param name="separator">The separator to remove.</param>
    /// <returns>True when ownership was removed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="separator"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The attached owner is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The owner is disposed.</exception>
    public bool Remove(CommandBarSeparator separator)
    {
        ArgumentNullException.ThrowIfNull(separator);
        return _owner.RemoveEntry(separator);
    }

    /// <summary>Removes the retained entry at a validated position without disposing it.</summary>
    /// <param name="index">The entry position.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the collection.</exception>
    /// <exception cref="InvalidOperationException">The attached owner is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The owner is disposed.</exception>
    public void RemoveAt(int index) => _owner.RemoveEntryAt(index);

    /// <summary>Moves one retained entry while preserving its identity and ownership generation.</summary>
    /// <param name="oldIndex">The current position.</param>
    /// <param name="newIndex">The destination position.</param>
    /// <exception cref="ArgumentOutOfRangeException">Either position is outside the collection.</exception>
    /// <exception cref="InvalidOperationException">The attached owner is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The owner is disposed.</exception>
    public void Move(int oldIndex, int newIndex) => _owner.MoveEntry(oldIndex, newIndex);

    /// <summary>Gets the identity position of an entry, or -1 when it is not retained here.</summary>
    /// <param name="entry">The candidate entry.</param>
    /// <returns>The zero-based identity position, or -1.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entry"/> is null.</exception>
    public int IndexOf(ControlBase entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return _owner.IndexOfEntry(entry);
    }

    /// <summary>Detaches every retained entry without disposing caller-owned instances.</summary>
    /// <exception cref="InvalidOperationException">The attached owner is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The owner is disposed.</exception>
    public void Clear() => _owner.ClearEntries();

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
