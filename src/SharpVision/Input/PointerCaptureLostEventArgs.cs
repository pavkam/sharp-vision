// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

/// <summary>Provides one already-committed direct pointer-capture loss notification.</summary>
[PublicAPI]
public sealed class PointerCaptureLostEventArgs: EventArgs
{
    /// <summary>Initializes one capture-loss notification.</summary>
    /// <param name="reason">The defined reason capture ended.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="reason"/> is unknown.</exception>
    public PointerCaptureLostEventArgs(PointerCaptureLossReason reason)
    {
        ArgumentOutOfRangeException.ThrowIfNotDefined(reason, nameof(reason), "The capture-loss reason is unknown.");

        Reason = reason;
    }

    /// <summary>Gets why exclusive capture ended.</summary>
    public PointerCaptureLossReason Reason { get; }
}
