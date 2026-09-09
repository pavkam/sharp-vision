// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Owns authority and cancellation for one latest-wins asynchronous control operation.</summary>
/// <remarks>
/// <para>
/// This is the shared "resolve asynchronously, discard a stale result, deliver only the latest one"
/// primitive behind any control that starts a new asynchronous operation whenever its input changes
/// while an earlier one may still be in flight - a suggestion resolver re-querying on every
/// keystroke, a command palette re-searching, a tree node reloading its children, or a file dialog
/// re-listing a directory. Callers retain the returned <see cref="LatestControlOperationLease"/>
/// reference identity and combine <see cref="IsCurrent"/> with their own domain predicates - such as
/// "the control is still attached to the same dispatcher" or "the loaded source has not changed
/// underneath the operation" - before publishing a result. This type deliberately owns no
/// dispatcher, attachment, or fault policy of its own; combine it with
/// <see cref="ControlBase.PostForCurrentAttachment"/>,
/// <see cref="ControlBase.PostBackgroundCompletionForCurrentAttachment"/>, or
/// <see cref="ControlBase.DispatchToCurrentAttachment"/> for the delivery half of the contract.
/// </para>
/// <para>
/// <see cref="Begin"/> implements one reentrancy rule every caller depends on: the new lease is
/// installed as current before the previous lease is cancelled, and a cancellation callback that
/// throws is captured and reported to the caller as this call's own failure, not silently swallowed
/// - it still leaves the newly begun lease correctly retired first, so a throwing replacement
/// cancellation cannot strand the unreturned lease.
/// </para>
/// </remarks>
[PublicAPI]
public sealed class LatestControlOperation
{
    private LatestControlOperationLease? _current;

    /// <summary>Gets whether authority is currently retained.</summary>
    public bool HasCurrent => _current is not null;

    /// <summary>Starts a new current operation and revokes the previous lease first.</summary>
    /// <returns>The new opaque current lease.</returns>
    /// <remarks>
    /// The new lease becomes current before the previous lease is cancelled, so any cancellation
    /// callback the previous lease's <see cref="LatestControlOperationLease.CancellationToken"/> runs
    /// synchronously already observes the new lease as current rather than racing to see this one
    /// mid-transition. If that cancellation callback throws, this method aborts and disposes the new
    /// lease before the exception propagates, so no lease is ever left both current and unreturned.
    /// </remarks>
    /// <exception cref="Exception">A callback registered against the previous lease's
    /// <see cref="LatestControlOperationLease.CancellationToken"/> threw. The new lease is aborted
    /// before this exception propagates.</exception>
    public LatestControlOperationLease Begin()
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
    /// <exception cref="ArgumentNullException"><paramref name="lease"/> is null.</exception>
    public bool IsCurrent(LatestControlOperationLease lease)
    {
        ArgumentNullException.ThrowIfNull(lease);
        return ReferenceEquals(_current, lease);
    }

    /// <summary>Retires and disposes only a matching current lease.</summary>
    /// <param name="lease">The non-null candidate lease.</param>
    /// <returns>True when the lease was current and retired; otherwise, false.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="lease"/> is null.</exception>
    public bool TryComplete(LatestControlOperationLease lease)
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
    /// <exception cref="ArgumentNullException"><paramref name="lease"/> is null.</exception>
    public bool TryAbort(LatestControlOperationLease lease) => TryComplete(lease);

    /// <summary>Revokes current authority before cancellation callbacks run, then disposes it.</summary>
    /// <exception cref="Exception">A callback registered against the current lease's
    /// <see cref="LatestControlOperationLease.CancellationToken"/> threw.</exception>
    public void Cancel()
    {
        var current = _current;
        _current = null;
        current?.CancelAndDispose();
    }
}
