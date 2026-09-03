// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Runtime;

/// <summary>Records synchronous restore-lease disposal for console connection tests.</summary>
internal sealed class TrackingRestore: IDisposable
{
    private readonly bool _throwOnDispose;

    internal TrackingRestore(bool throwOnDispose = false) => _throwOnDispose = throwOnDispose;

    /// <summary>Gets the number of completed disposal calls.</summary>
    internal int Disposals { get; private set; }

    /// <summary>Records one disposal call, throwing first when configured to simulate a failed
    /// terminal-mode restore.</summary>
    public void Dispose()
    {
        Disposals++;

        if (_throwOnDispose)
        {
            throw new IOException("Simulated terminal mode restore failure.");
        }
    }
}
