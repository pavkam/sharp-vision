// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Layout;


/// <summary>Owns validated Grid track definitions and reports actual mutations.</summary>
public sealed class TrackCollection: IList<Track>, IReadOnlyList<Track>
{
    private readonly Action<int> _changing;
    private readonly Action _changed;
    private readonly List<Track> _items = [];

    /// <summary>Initializes a collection with a non-null owner callback.</summary>
    /// <param name="changing">
    /// The callback invoked before observable mutation with the candidate count.
    /// </param>
    /// <param name="changed">The callback invoked after actual mutation.</param>
    /// <exception cref="ArgumentNullException">A callback is null.</exception>
    internal TrackCollection(Action<int> changing, Action changed)
    {
        ArgumentNullException.ThrowIfNull(changing);
        ArgumentNullException.ThrowIfNull(changed);
        _changing = changing;
        _changed = changed;
    }

    /// <inheritdoc/>
    public Track this[int index]
    {
        get => _items[index];
        set
        {
            if (_items[index] == value)
            {
                return;
            }

            _changing(_items.Count);
            _items[index] = value;
            _changed();
        }
    }

    /// <inheritdoc/>
    public int Count => _items.Count;

    /// <inheritdoc/>
    public bool IsReadOnly => false;

    /// <inheritdoc/>
    public void Add(Track item)
    {
        _changing(_items.Count + 1);
        _items.Add(item);
        _changed();
    }

    /// <inheritdoc/>
    public void Clear()
    {
        if (_items.Count == 0)
        {
            return;
        }

        _changing(0);
        _items.Clear();
        _changed();
    }

    /// <inheritdoc/>
    public bool Contains(Track item) => _items.Contains(item);

    /// <inheritdoc/>
    public void CopyTo(Track[] array, int arrayIndex)
    {
        ArgumentNullException.ThrowIfNull(array);
        _items.CopyTo(array, arrayIndex);
    }

    /// <inheritdoc/>
    public IEnumerator<Track> GetEnumerator() => _items.GetEnumerator();

    /// <inheritdoc/>
    public int IndexOf(Track item) => _items.IndexOf(item);

    /// <inheritdoc/>
    public void Insert(int index, Track item)
    {
        _changing(_items.Count + 1);
        _items.Insert(index, item);
        _changed();
    }

    /// <inheritdoc/>
    public bool Remove(Track item)
    {
        var index = _items.IndexOf(item);

        if (index < 0)
        {
            return false;
        }

        _changing(_items.Count - 1);
        _items.RemoveAt(index);
        _changed();
        return true;
    }

    /// <inheritdoc/>
    public void RemoveAt(int index)
    {
        _ = _items[index];
        _changing(_items.Count - 1);
        _items.RemoveAt(index);
        _changed();
    }

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
