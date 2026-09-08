// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Coordinates property-override lease generations for one owner and owned-control slot.</summary>
/// <remarks>
/// <para>
/// A service tracks at most one current <see cref="RetainedPropertyOverrideLease"/> generation per
/// realized child. <see cref="Acquire"/> installs a new generation for a child, retiring without
/// restoring whatever generation that child previously held; this is the generation rule the rest
/// of this type enforces: a lease obtained from an earlier <see cref="Acquire"/> call for the same
/// child stops being <see cref="RetainedPropertyOverrideLease.IsCurrent"/> the instant a later call
/// supersedes it, so any of its writes silently stop taking effect rather than racing the new
/// generation for the same storage. This rule holds across every service, not only within one: a
/// leased control has exactly one active generation of its own regardless of which service issued
/// it, so acquiring a second, independent lease for a child that already has a current one retires
/// the first without restoring it. A property an owner wants leased alongside properties another
/// service already controls for the same child must be part of that one <see cref="Acquire"/> call,
/// never a separate one.
/// </para>
/// <para>
/// The service also retires - without ever attempting to write a captured value back - any
/// generation whose child leaves through its own disposal, independently of whether the caller
/// remembers to call <see cref="Retire(RetainedPropertyOverrideLease)"/> itself: it watches the same
/// owned-control slot its leases are scoped to for exactly that condition.
/// </para>
/// <para>
/// The constructor is not public. <see cref="ItemsControl"/> exposes one instance per item owner
/// through <see cref="ItemsControl.ItemPropertyOverrides"/>; a third-party owner reaches this type
/// only that way, and an owner whose items already carry a generation from the shared owner focus
/// model - see <see cref="ItemsControl.EnableOwnerFocusModel"/> - extends that same generation
/// through <see cref="ItemsControl.GetOwnerFocusModelExtraDescriptors"/> rather than acquiring one
/// of its own for the same child.
/// </para>
/// </remarks>
[PublicAPI]
public sealed class RetainedPropertyOverrideService: IDisposable
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
    /// <remarks>
    /// A prior generation this service already tracked for <paramref name="child"/> is retired -
    /// without restoring its captured authored values - before the new one installs, because that
    /// prior lease's own <see cref="RetainedPropertyOverrideLease.IsCurrent"/> becomes false the
    /// instant this call installs its replacement.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="child"/> or <paramref name="descriptors"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="child"/> does not belong to this service's slot, <paramref name="descriptors"/>
    /// is empty, or it repeats a property.
    /// </exception>
    /// <exception cref="ObjectDisposedException">This service is disposed.</exception>
    public RetainedPropertyOverrideLease Acquire(
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
    /// <exception cref="KeyNotFoundException">
    /// This service holds no current generation for <paramref name="child"/>.
    /// </exception>
    public RetainedPropertyOverrideLease Get(ControlBase child) => _leases[child];

    /// <summary>Restores one detached child's latest authored values if its generation is current.</summary>
    /// <param name="child">The detached child.</param>
    /// <remarks>
    /// A no-op when this service holds no current generation for <paramref name="child"/> - already
    /// restored, already retired through its own disposal, or never leased in the first place.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="child"/> is null.</exception>
    public void Restore(ControlBase child)
    {
        ArgumentNullException.ThrowIfNull(child);

        if (_leases.Remove(child, out var lease))
        {
            lease.Restore();
        }
    }

    /// <summary>Restores one captured generation only if it was not superseded by reownership.</summary>
    /// <param name="lease">The exact detached generation.</param>
    /// <remarks>
    /// A no-op when <paramref name="lease"/> is no longer the generation this service tracks for its
    /// <see cref="RetainedPropertyOverrideLease.Child"/> - a later <see cref="Acquire"/> call for the
    /// same child already superseded it, or it was already restored or retired.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="lease"/> is null.</exception>
    public void Restore(RetainedPropertyOverrideLease lease)
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
    /// <remarks>
    /// A no-op under the same supersession rule as <see cref="Restore(RetainedPropertyOverrideLease)"/>.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="lease"/> is null.</exception>
    public void Retire(RetainedPropertyOverrideLease lease)
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
