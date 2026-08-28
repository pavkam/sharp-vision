// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Provides cancellation for one opaque latest-wins control operation.</summary>
internal sealed class LatestControlOperationLease: IDisposable
{
    private readonly CancellationTokenSource _cancellation = new();

    /// <summary>Initializes one independently cancellable operation lease.</summary>
    internal LatestControlOperationLease() => CancellationToken = _cancellation.Token;

    /// <summary>Gets the token cancelled when this lease loses current authority.</summary>
    internal CancellationToken CancellationToken { get; }

    /// <summary>Cancels and disposes this lease, preserving disposal when callbacks throw.</summary>
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
