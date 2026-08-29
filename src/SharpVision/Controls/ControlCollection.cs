// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Adapts one registered container-child slot as a mutable ordered collection.</summary>
[PublicAPI]
public sealed class ControlCollection: IList<ControlBase>, IReadOnlyList<ControlBase>
{
    private OwnedControlSlot Slot { get; }

    /// <summary>Raised after one complete collection change, including child-initiated disposal.</summary>
    /// <remarks>
    /// This internal seam lets framework ownership roles observe the same committed slot used by
    /// public containers without exposing either the slot or its notifications to callers.
    /// </remarks>
    internal event Action<OwnedControlChange>? Changed
    {
        add => Slot.Changed += value;
        remove => Slot.Changed -= value;
    }

    /// <summary>Gets the registered slot used by framework-owned compound transactions.</summary>
    internal OwnedControlSlot OwnedSlot => Slot;

    /// <summary>Initializes an empty public container-child collection.</summary>
    /// <param name="owner">The owning container.</param>
    /// <param name="capacity">The non-negative maximum child count.</param>
    internal ControlCollection(Container owner, int capacity)
        : this(
            owner,
            capacity,
            new OwnedControlOptions(
                OwnedControlRole.ContainerChild,
                OwnedControlLayer.Normal,
                participatesInHitTesting: true,
                participatesInNavigation: true,
                partKey: null,
                InvalidationImpact.Measure))
    {
    }

    /// <summary>Initializes an adapter over one distinct private ownership slot.</summary>
    /// <param name="owner">The non-null owning control.</param>
    /// <param name="capacity">The non-negative maximum child count.</param>
    /// <param name="options">The validated ownership metadata.</param>
    internal ControlCollection(ControlBase owner, int capacity, OwnedControlOptions options)
    {
        ArgumentNullException.ThrowIfNull(owner);
        Slot = owner.RegisterOwnedSlot(options, capacity);
    }

    /// <inheritdoc cref="IList{T}.this" />
    public ControlBase this[int index]
    {
        get => Slot[index];
        set => Slot[index] = value;
    }

    /// <inheritdoc cref="ICollection{T}.Count" />
    public int Count => Slot.Count;

    /// <inheritdoc/>
    public bool IsReadOnly => false;

    /// <inheritdoc/>
    public void Add(ControlBase item) => Slot.Add(item);

    /// <inheritdoc/>
    public void Clear() => Slot.Clear();

    /// <inheritdoc/>
    [Pure]
    public bool Contains(ControlBase item) => Slot.Contains(item);

    /// <inheritdoc/>
    public void CopyTo(ControlBase[] array, int arrayIndex) => Slot.CopyTo(array, arrayIndex);

    /// <summary>Gets the allocation-free value enumerator used by direct iteration.</summary>
    /// <returns>The underlying slot enumerator.</returns>
    public List<ControlBase>.Enumerator GetEnumerator() => Slot.GetEnumerator();

    /// <inheritdoc/>
    [Pure]
    public int IndexOf(ControlBase item) => Slot.IndexOf(item);

    /// <inheritdoc/>
    public void Insert(int index, ControlBase item) => Slot.Insert(index, item);

    /// <summary>Atomically assigns or clears the only child of a capacity-one collection.</summary>
    /// <param name="item">The new detached child, or null to clear the collection.</param>
    /// <exception cref="ArgumentException"><paramref name="item"/> cannot be owned by this control.</exception>
    /// <exception cref="InvalidOperationException">The owner is off-dispatcher or capacity is not one.</exception>
    /// <exception cref="ObjectDisposedException">The owner or new child is disposed.</exception>
    internal void SetOnly(ControlBase? item)
    {
        if (Slot.Capacity != 1)
        {
            throw new InvalidOperationException("Only a capacity-one collection supports SetOnly.");
        }

        Slot.ReplaceAll(item is null ? [] : [item]);
    }

    /// <summary>Atomically replaces the complete child collection.</summary>
    /// <param name="items">The non-null candidate sequence.</param>
    internal void ReplaceAll(IEnumerable<ControlBase> items) => Slot.ReplaceAll(items);

    /// <summary>Atomically reorders one retained child without changing ownership or availability.</summary>
    /// <param name="oldIndex">The current zero-based position.</param>
    /// <param name="newIndex">The destination zero-based position.</param>
    internal void Move(int oldIndex, int newIndex)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint) oldIndex, (uint) Count);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint) newIndex, (uint) Count);

        if (oldIndex == newIndex)
        {
            return;
        }

        var next = new List<ControlBase>(Slot.Items);
        var item = next[oldIndex];
        next.RemoveAt(oldIndex);
        next.Insert(newIndex, item);
        Slot.ReplaceAll(next);
    }

    /// <inheritdoc/>
    public bool Remove(ControlBase item) => Slot.Remove(item);

    /// <inheritdoc/>
    public void RemoveAt(int index) => Slot.RemoveAt(index);

    /// <inheritdoc/>
    IEnumerator<ControlBase> IEnumerable<ControlBase>.GetEnumerator() => Slot.Items.GetEnumerator();

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => Slot.Items.GetEnumerator();
}
