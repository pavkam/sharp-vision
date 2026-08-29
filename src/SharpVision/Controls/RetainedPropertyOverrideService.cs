// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Coordinates property-override lease generations for one owner and owned-control slot.</summary>
internal sealed class RetainedPropertyOverrideService: IDisposable
{
    private readonly Dictionary<ControlBase, RetainedPropertyOverrideLease> _leases = [];
    private readonly ControlBase _owner;
    private readonly OwnedControlSlot _slot;
    private readonly Action<ControlBase, RetainedControlProperty>? _authoredValueChanged;
    private bool _isDisposed;

    /// <summary>Initializes one service bound to an exact owner and slot.</summary>
    /// <param name="owner">The non-null slot owner.</param>
    /// <param name="slot">The exact slot whose child generations are leased.</param>
    /// <param name="authoredValueChanged">Optional callback after a caller request is captured.</param>
    internal RetainedPropertyOverrideService(
        ControlBase owner,
        OwnedControlSlot slot,
        Action<ControlBase, RetainedControlProperty>? authoredValueChanged = null)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(slot);

        _owner = owner;
        _slot = slot;
        _authoredValueChanged = authoredValueChanged;
        slot.Changed += OnSlotChanged;
    }

    /// <summary>Gets the number of current child generations retained by this service.</summary>
    internal int Count => _leases.Count;

    /// <summary>Captures and installs one new child generation.</summary>
    /// <param name="child">The child currently committed to this service's slot.</param>
    /// <param name="descriptors">The non-empty distinct property descriptors.</param>
    /// <returns>The new current lease.</returns>
    internal RetainedPropertyOverrideLease Acquire(
        ControlBase child,
        params RetainedPropertyOverrideDescriptor[] descriptors)
    {
        ArgumentNullException.ThrowIfNull(child);
        ArgumentNullException.ThrowIfNull(descriptors);
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        if (!ReferenceEquals(child.OwningSlot, _slot))
        {
            throw new ArgumentException("The retained override child must belong to the service slot.", nameof(child));
        }

        if (descriptors.Length == 0)
        {
            throw new ArgumentException("A retained override requires at least one property.", nameof(descriptors));
        }

        if (_leases.Remove(child, out var previous))
        {
            previous.Retire();
        }

        var lease = new RetainedPropertyOverrideLease(
            child,
            descriptors,
            _authoredValueChanged,
            OnLeaseRetired);
        _leases.Add(child, lease);
        return lease;
    }

    /// <summary>Gets the current lease for one child.</summary>
    /// <param name="child">The non-null child.</param>
    /// <returns>The current lease.</returns>
    internal RetainedPropertyOverrideLease Get(ControlBase child) => _leases[child];

    /// <summary>Restores one detached child's latest authored values if its generation is current.</summary>
    /// <param name="child">The detached child.</param>
    internal void Restore(ControlBase child)
    {
        ArgumentNullException.ThrowIfNull(child);

        if (_leases.Remove(child, out var lease))
        {
            lease.Restore();
        }
    }

    /// <summary>Restores one captured generation only if it was not superseded by reownership.</summary>
    /// <param name="lease">The exact detached generation.</param>
    internal void Restore(RetainedPropertyOverrideLease lease)
    {
        ArgumentNullException.ThrowIfNull(lease);

        if (_leases.TryGetValue(lease.Child, out var current) && ReferenceEquals(current, lease))
        {
            _ = _leases.Remove(lease.Child);
            lease.Restore();
        }
    }

    /// <summary>Retires one captured generation without restoring authored values.</summary>
    /// <param name="lease">The exact generation ending through disposal.</param>
    internal void Retire(RetainedPropertyOverrideLease lease)
    {
        ArgumentNullException.ThrowIfNull(lease);

        if (_leases.TryGetValue(lease.Child, out var current) && ReferenceEquals(current, lease))
        {
            _ = _leases.Remove(lease.Child);
            lease.Retire();
        }
    }

    /// <summary>Retires every generation without restoring owner-disposal state.</summary>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _slot.Changed -= OnSlotChanged;

        foreach (var lease in _leases.Values.ToArray())
        {
            lease.Retire();
        }

        _leases.Clear();
    }

    private void OnSlotChanged(OwnedControlChange change)
    {
        foreach (var child in change.Removed.Span)
        {
            if (!_leases.TryGetValue(child, out var lease))
            {
                continue;
            }

            if (change.Reason == ReleaseReason.Disposed || child.IsDisposing || _owner.IsDisposing)
            {
                _ = _leases.Remove(child);
                lease.Retire();
            }
        }
    }

    private void OnLeaseRetired(RetainedPropertyOverrideLease lease)
    {
        if (_leases.TryGetValue(lease.Child, out var current) && ReferenceEquals(current, lease))
        {
            _ = _leases.Remove(lease.Child);
        }
    }
}
