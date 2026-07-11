using System.Collections;

using SharpVision.Input;

namespace SharpVision.Controls;

/// <summary>Owns one container's validated ordered child controls.</summary>
public sealed class Children: IList<Control>, IReadOnlyList<Control>
{
    private readonly List<Control> _items = [];
    private readonly Container _owner;

    /// <summary>Initializes an empty collection for one non-null owner.</summary>
    /// <param name="owner">The owning container.</param>
    /// <exception cref="ArgumentNullException"><paramref name="owner"/> is null.</exception>
    internal Children(Container owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _owner = owner;
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
        Validate(item);
        Attach(item);
        _items.Insert(index, item);
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
        item.FocusOwner?.Unavailable(item);
        item.CaptureOwner?.Unavailable(item, ReleaseReason.Detached);
        item.SetFocusOwner(null);
        item.SetCaptureOwner(null);
        item.Detach();
        item.SetParent(null);
    }
}
