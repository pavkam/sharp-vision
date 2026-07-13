// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Threading;

/// <summary>Suppresses dispatcher idle while asynchronous work remains pending.</summary>
internal sealed class PendingLease: IDisposable
{
    private Dispatcher? _owner;

    /// <summary>Initializes a lease for one non-null dispatcher.</summary>
    /// <param name="owner">The dispatcher whose pending count is held.</param>
    /// <exception cref="ArgumentNullException"><paramref name="owner"/> is null.</exception>
    internal PendingLease(Dispatcher owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _owner = owner;
    }

    /// <summary>Releases the pending-work count exactly once.</summary>
    public void Dispose() => Interlocked.Exchange(ref _owner, null)?.ReleasePending();
}
