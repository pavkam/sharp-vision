// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Stores one independently ordered set of controls owned through a shared registry.</summary>
internal sealed class OwnedControlSlot
{
    /// <summary>Initializes one registered slot.</summary>
    /// <param name="registry">The non-null owning registry.</param>
    /// <param name="options">The validated role and traversal metadata.</param>
    /// <param name="capacity">The non-negative maximum control count.</param>
    public OwnedControlSlot(
        OwnedControlRegistry registry,
        OwnedControlOptions options,
        int capacity)
    {
        Debug.Assert(registry is not null, "A registered slot requires its owner registry.");
        Debug.Assert(capacity >= 0, "Slot capacity is validated before construction.");
        Registry = registry;
        Options = options;
        Capacity = capacity;
    }

    /// <summary>Raised after one complete structural change, including child-initiated disposal.</summary>
    public event Action? Changed;

    /// <summary>Gets one control by its zero-based position or atomically replaces it.</summary>
    /// <param name="index">The valid zero-based position.</param>
    /// <returns>The control at the requested position.</returns>
    /// <exception cref="ArgumentNullException">The assigned control is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the slot.</exception>
    /// <exception cref="ArgumentException">The assigned control cannot belong to this owner.</exception>
    /// <exception cref="InvalidOperationException">The owner is off-dispatcher or an ownership transaction is active.</exception>
    /// <exception cref="ObjectDisposedException">The owner or assigned control is disposed.</exception>
    public Control this[int index]
    {
        get => Items[index];
        set => Registry.Replace(this, index, value);
    }

    /// <summary>Gets the number of controls currently committed to this slot.</summary>
    public int Count => Items.Count;

    /// <summary>Gets the registry that owns this slot.</summary>
    public OwnedControlRegistry Registry { get; }

    /// <summary>Gets the validated role and traversal metadata.</summary>
    public OwnedControlOptions Options { get; }

    /// <summary>Gets the finite or effectively unbounded slot capacity.</summary>
    public int Capacity { get; }

    /// <summary>Adds one detached control at the end of this slot.</summary>
    /// <param name="control">The non-null detached control.</param>
    public void Add(Control control) => Insert(Count, control);

    /// <summary>Atomically clears this slot without disposing removed controls.</summary>
    public void Clear() => Registry.Clear(this);

    /// <summary>Determines whether this exact control belongs to the slot.</summary>
    /// <param name="control">The non-null candidate.</param>
    /// <returns>True when the identical control is present.</returns>
    public bool Contains(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);
        return IndexOfIdentity(control) >= 0;
    }

    /// <summary>Copies committed controls to an existing array.</summary>
    /// <param name="array">The non-null destination.</param>
    /// <param name="arrayIndex">The valid destination start index.</param>
    public void CopyTo(Control[] array, int arrayIndex)
    {
        ArgumentNullException.ThrowIfNull(array);
        Items.CopyTo(array, arrayIndex);
    }

    /// <summary>Gets an allocation-free enumerator over the current committed order.</summary>
    /// <returns>The underlying list value enumerator.</returns>
    public List<Control>.Enumerator GetEnumerator() => Items.GetEnumerator();

    /// <summary>Gets the identity position of one control.</summary>
    /// <param name="control">The non-null candidate.</param>
    /// <returns>The zero-based position, or -1.</returns>
    public int IndexOf(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);
        return IndexOfIdentity(control);
    }

    /// <summary>Inserts one detached control at a validated position.</summary>
    /// <param name="index">The insertion position from zero through <see cref="Count"/>.</param>
    /// <param name="control">The non-null detached control.</param>
    public void Insert(int index, Control control) => Registry.Insert(this, index, control);

    /// <summary>Removes one identical control without disposing it.</summary>
    /// <param name="control">The non-null candidate.</param>
    /// <returns>True when the control was removed.</returns>
    public bool Remove(Control control) => Registry.Remove(this, control);

    /// <summary>Removes the control at one valid position without disposing it.</summary>
    /// <param name="index">The valid zero-based position.</param>
    public void RemoveAt(int index) => Registry.RemoveAt(this, index);

    /// <summary>Atomically replaces the complete slot with a validated borrowed sequence.</summary>
    /// <param name="controls">The non-null candidate sequence.</param>
    public void ReplaceAll(IEnumerable<Control> controls) => Registry.ReplaceAll(this, controls);

    /// <summary>Gets the mutable storage reserved for registry commit operations.</summary>
    public List<Control> Items { get; } = [];

    /// <summary>Publishes one committed slot change outside structural state mutation.</summary>
    public void PublishChanged() => Changed?.Invoke();

    /// <summary>Removes a disposing child through its exact slot while disposal holds publication.</summary>
    /// <param name="control">The identical disposing child.</param>
    public void RemoveForDisposalWithinPublication(Control control) =>
        Registry.RemoveForDisposalWithinPublication(this, control);

    private int IndexOfIdentity(Control control)
    {
        for (var index = 0; index < Items.Count; index++)
        {
            if (ReferenceEquals(Items[index], control))
            {
                return index;
            }
        }

        return -1;
    }
}
