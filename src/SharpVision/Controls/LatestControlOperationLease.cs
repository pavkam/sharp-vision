// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Provides cancellation for one opaque latest-wins control operation.</summary>
/// <remarks>
/// Only <see cref="LatestControlOperation.Begin"/> creates a lease; a control cannot construct one
/// directly. The identity is intentionally opaque - a control retains the reference it received from
/// <see cref="LatestControlOperation.Begin"/> and passes it back to
/// <see cref="LatestControlOperation.IsCurrent"/>, <see cref="LatestControlOperation.TryComplete"/>,
/// or <see cref="LatestControlOperation.TryAbort"/>, but cannot inspect or perform generation
/// arithmetic on it.
/// </remarks>
[PublicAPI]
public sealed class LatestControlOperationLease: IDisposable
{
    private readonly CancellationTokenSource _cancellation = new();

    /// <summary>Initializes one independently cancellable operation lease.</summary>
    internal LatestControlOperationLease() => CancellationToken = _cancellation.Token;

    /// <summary>Gets the token cancelled when this lease loses current authority.</summary>
    public CancellationToken CancellationToken { get; }

    /// <summary>Cancels and disposes this lease, preserving disposal when callbacks throw.</summary>
    /// <exception cref="Exception">A registered cancellation callback threw. Disposal still runs
    /// in a <c>finally</c> block before the exception propagates.</exception>
    internal void CancelAndDispose()
    {
        try
        {
            _cancellation.Cancel();
        }
        finally
        {
            _cancellation.Dispose();
        }
    }

    /// <summary>Disposes a successfully completed or aborted lease without cancellation.</summary>
    public void Dispose() => _cancellation.Dispose();
}
