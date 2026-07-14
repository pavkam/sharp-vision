// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using System.Collections;

/// <summary>Owns a mutable ordered single-document inline collection.</summary>
public sealed class Inlines: IList<Inline>
{
    private readonly List<Inline> _items = [];
    private readonly RichText _owner;

    /// <summary>Initializes an empty collection for one non-null document.</summary>
    /// <param name="owner">The non-null owning document.</param>
    internal Inlines(RichText owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _owner = owner;
    }

    /// <inheritdoc/>
    public int Count => _items.Count;

    /// <inheritdoc/>
    public bool IsReadOnly => false;

    /// <inheritdoc/>
    public Inline this[int index]
    {
        get => _items[index];
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _owner.VerifyMutable();
            Inline previous = _items[index];

            if (ReferenceEquals(previous, value))
            {
                return;
            }

            ValidateDetached(value);
            previous.Detach();
            _items[index] = value;
            value.Attach(_owner);
            _owner.InlineChanged();
        }
    }

    /// <inheritdoc/>
    public void Add(Inline item) => Insert(_items.Count, item);

    /// <inheritdoc/>
    public void Clear()
    {
        _owner.VerifyMutable();

        if (_items.Count == 0)
        {
            return;
        }

        foreach (Inline item in _items)
        {
            item.Detach();
        }

        _items.Clear();
        _owner.InlineChanged();
    }

    /// <inheritdoc/>
    public bool Contains(Inline item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return _items.Contains(item);
    }

    /// <inheritdoc/>
    public void CopyTo(Inline[] array, int arrayIndex)
    {
        ArgumentNullException.ThrowIfNull(array);
        _items.CopyTo(array, arrayIndex);
    }

    /// <inheritdoc/>
    public List<Inline>.Enumerator GetEnumerator() => _items.GetEnumerator();

    /// <inheritdoc/>
    public int IndexOf(Inline item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return _items.IndexOf(item);
    }

    /// <inheritdoc/>
    public void Insert(int index, Inline item)
    {
        Validate(item);
        _items.Insert(index, item);
        item.Attach(_owner);
        _owner.InlineChanged();
    }

    /// <inheritdoc/>
    public bool Remove(Inline item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _owner.VerifyMutable();
        int index = _items.IndexOf(item);

        if (index < 0)
        {
            return false;
        }

        RemoveAt(index);
        return true;
    }

    /// <inheritdoc/>
    public void RemoveAt(int index)
    {
        _owner.VerifyMutable();
        Inline item = _items[index];
        _items.RemoveAt(index);
        item.Detach();
        _owner.InlineChanged();
    }

    IEnumerator<Inline> IEnumerable<Inline>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private void Validate(Inline item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _owner.VerifyMutable();
        ValidateDetached(item);
    }

    private static void ValidateDetached(Inline item)
    {
        Debug.Assert(item is not null, "Inline validation requires a non-null item.");

        if (item.Owner is not null)
        {
            throw new ArgumentException("The inline already belongs to a document.", nameof(item));
        }
    }
}
