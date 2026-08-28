// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Owns authority and cancellation for one latest-wins asynchronous control operation.</summary>
/// <remarks>
/// Callers retain the returned reference identity and combine <see cref="IsCurrent"/> with their
/// own domain predicates. This type deliberately owns no dispatcher, attachment, or fault policy.
/// </remarks>
internal sealed class LatestControlOperation
{
    private LatestControlOperationLease? _current;

    /// <summary>Gets whether authority is currently retained. This test seam proves a throwing
    /// replacement cancellation cannot strand the unreturned lease created by <see cref="Begin"/>.</summary>
    internal bool HasCurrent => _current is not null;

    /// <summary>Starts a new current operation and revokes the previous lease first.</summary>
    /// <returns>The new opaque current lease.</returns>
    internal LatestControlOperationLease Begin()
    {
        var lease = new LatestControlOperationLease();
        var previous = _current;
        _current = lease;

        try
        {
            previous?.CancelAndDispose();
        }
        catch
        {
            _ = TryAbort(lease);
            throw;
        }

        return lease;
    }

    /// <summary>Checks whether <paramref name="lease"/> still owns current authority.</summary>
    /// <param name="lease">The non-null candidate lease.</param>
    /// <returns>True only for the current reference identity.</returns>
    internal bool IsCurrent(LatestControlOperationLease lease)
    {
        ArgumentNullException.ThrowIfNull(lease);
        return ReferenceEquals(_current, lease);
    }

    /// <summary>Retires and disposes only a matching current lease.</summary>
    /// <param name="lease">The non-null candidate lease.</param>
    /// <returns>True when the lease was current and retired; otherwise, false.</returns>
    internal bool TryComplete(LatestControlOperationLease lease)
    {
        ArgumentNullException.ThrowIfNull(lease);

        if (!ReferenceEquals(_current, lease))
        {
            return false;
        }

        _current = null;
        lease.Dispose();
        return true;
    }

    /// <summary>Aborts and disposes only the lease whose startup failed.</summary>
    /// <param name="lease">The non-null failed-start candidate.</param>
    /// <returns>True when the lease was still current and aborted; otherwise, false.</returns>
    internal bool TryAbort(LatestControlOperationLease lease) => TryComplete(lease);

    /// <summary>Revokes current authority before cancellation callbacks run, then disposes it.</summary>
    internal void Cancel()
    {
        var current = _current;
        _current = null;
        current?.CancelAndDispose();
    }
}
