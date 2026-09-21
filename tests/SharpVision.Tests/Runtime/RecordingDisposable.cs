// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Runtime;

/// <summary>Records synchronous disposal calls for terminal-bound resource ordering tests.</summary>
internal sealed class RecordingDisposable: IDisposable
{
    private readonly Action _onDispose;

    /// <summary>Initializes a recorder that invokes <paramref name="onDispose"/> once disposed.</summary>
    /// <param name="onDispose">The non-null callback invoked from <see cref="Dispose"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="onDispose"/> is null.</exception>
    internal RecordingDisposable(Action onDispose)
    {
        ArgumentNullException.ThrowIfNull(onDispose);
        _onDispose = onDispose;
    }

    /// <summary>Gets the number of completed disposal calls.</summary>
    internal int Disposals { get; private set; }

    /// <summary>Records one disposal call and invokes the configured callback.</summary>
    public void Dispose()
    {
        Disposals++;
        _onDispose();
    }
}
