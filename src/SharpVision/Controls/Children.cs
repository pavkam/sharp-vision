using System.Collections;

using SharpVision.Input;

namespace SharpVision.Controls;

/// <summary>Owns one container's validated ordered child controls.</summary>
public sealed class Children: IList<Control>, IReadOnlyList<Control>
{
    private readonly List<Control> _items = [];
    private readonly Container _owner;
    private readonly int _capacity;

    /// <summary>Initializes an empty collection for one non-null owner and finite capacity.</summary>
    /// <param name="owner">The owning container.</param>
    /// <param name="capacity">The non-negative maximum child count.</param>
    /// <exception cref="ArgumentNullException"><paramref name="owner"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is negative.</exception>
    internal Children(Container owner, int capacity)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);
        _owner = owner;
        _capacity = capacity;
    }

    /// <inheritdoc/>
    public Control this[int index]
    {
        get => _items[index];
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _owner.VerifyMutable();
            var previous = _items[index];

            if (ReferenceEquals(previous, value))
            {
                return;
            }

            Validate(value);
            Detach(previous);
            Attach(value);
            _items[index] = value;
            _owner.Invalidate(Invalidation.Measure);
        }
    }

    /// <inheritdoc/>
    public int Count => _items.Count;

    /// <inheritdoc/>
    public bool IsReadOnly => false;

    /// <inheritdoc/>
    public void Add(Control item) => Insert(Count, item);

    /// <inheritdoc/>
    public void Clear()
    {
        _owner.VerifyMutable();

        if (_items.Count == 0)
        {
            return;
        }

        for (var index = _items.Count - 1; index >= 0; index--)
        {
            Detach(_items[index]);
        }

        _items.Clear();
        _owner.Invalidate(Invalidation.Measure);
    }

    /// <inheritdoc/>
    public bool Contains(Control item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return _items.Contains(item);
    }

    /// <inheritdoc/>
    public void CopyTo(Control[] array, int arrayIndex)
    {
        ArgumentNullException.ThrowIfNull(array);
        _items.CopyTo(array, arrayIndex);
    }

    /// <inheritdoc/>
    public IEnumerator<Control> GetEnumerator() => _items.GetEnumerator();

    /// <inheritdoc/>
    public int IndexOf(Control item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return _items.IndexOf(item);
    }

    /// <inheritdoc/>
    public void Insert(int index, Control item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _owner.VerifyMutable();
        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint) index, (uint) _items.Count);

        if (_items.Count >= _capacity)
        {
            throw new InvalidOperationException("The child collection is at capacity.");
        }

        Validate(item);
        Attach(item);
        _items.Insert(index, item);
        _owner.Invalidate(Invalidation.Measure);
    }

    /// <summary>Atomically assigns or clears the only child of a capacity-one collection.</summary>
    /// <param name="item">The new detached child, or null to clear the collection.</param>
    /// <exception cref="ArgumentException"><paramref name="item"/> cannot be owned by this container.</exception>
    /// <exception cref="InvalidOperationException">
    /// The owner is mutated off-dispatcher or the collection capacity is not one.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The owner or new child is disposed.</exception>
    internal void SetOnly(Control? item)
    {
        _owner.VerifyMutable();

        if (_capacity != 1)
        {
            throw new InvalidOperationException("Only a capacity-one collection supports SetOnly.");
        }

        var previous = _items.Count == 0 ? null : _items[0];

        if (ReferenceEquals(previous, item))
        {
            return;
        }

        if (item is not null)
        {
            Validate(item);
        }

        if (previous is not null)
        {
            Detach(previous);
        }

        if (item is null)
        {
            _items.Clear();
        }
        else
        {
            Attach(item);

            if (_items.Count == 0)
            {
                _items.Add(item);
            }
            else
            {
                _items[0] = item;
            }
        }

        _owner.Invalidate(Invalidation.Measure);
    }

    /// <inheritdoc/>
    public bool Remove(Control item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _owner.VerifyMutable();
        var index = _items.IndexOf(item);

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
        var item = _items[index];
        _items.RemoveAt(index);
        Detach(item);
        _owner.Invalidate(Invalidation.Measure);
    }

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private void Validate(Control item)
    {
        if (item.Parent is not null || item.Dispatcher is not null)
        {
            throw new ArgumentException("The child already belongs to a tree.", nameof(item));
        }

        for (Control? ancestor = _owner; ancestor is not null; ancestor = ancestor.Parent)
        {
            if (ReferenceEquals(ancestor, item))
            {
                throw new ArgumentException("Adding the child would create a cycle.", nameof(item));
            }
        }

        item.ValidateAttachment();
    }

    private void Attach(Control item)
    {
        item.SetParent(_owner);
        item.SetFocusOwner(_owner.FocusOwner);
        item.SetCaptureOwner(_owner.CaptureOwner);

        if (_owner.Dispatcher is { } dispatcher)
        {
            item.Attach(dispatcher);
        }
    }

    private static void Detach(Control item)
    {
        item.NotifyUnavailable(ReleaseReason.Detached);
        item.SetFocusOwner(null);
        item.SetCaptureOwner(null);
        item.Detach();
        item.SetParent(null);
    }
}
