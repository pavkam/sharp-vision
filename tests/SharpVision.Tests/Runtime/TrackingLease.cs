// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Runtime;

/// <summary>Records asynchronous host-lease disposal for application tests.</summary>
internal sealed class TrackingLease: IAsyncDisposable
{
    /// <summary>Gets the number of completed disposal calls.</summary>
    internal int Disposals { get; private set; }

    /// <summary>Records one asynchronous disposal call.</summary>
    /// <returns>A completed operation.</returns>
    public ValueTask DisposeAsync()
    {
        Disposals++;
        return ValueTask.CompletedTask;
    }
}
