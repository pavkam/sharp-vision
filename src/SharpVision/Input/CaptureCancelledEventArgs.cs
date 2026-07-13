// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

using SharpVision.Controls;

/// <summary>Describes one implicit capture or press cancellation.</summary>
public sealed class CaptureCancelledEventArgs: EventArgs
{
    /// <summary>Initializes a validated interaction cancellation.</summary>
    /// <param name="control">The captured or pressed control.</param>
    /// <param name="reason">The defined cancellation reason.</param>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="reason"/> is unknown.</exception>
    public CaptureCancelledEventArgs(Control control, ReleaseReason reason)
    {
        ArgumentNullException.ThrowIfNull(control);

        if (!Enum.IsDefined(reason))
        {
            throw new ArgumentOutOfRangeException(nameof(reason), reason, "The release reason is unknown.");
        }

        Control = control;
        Reason = reason;
    }

    /// <summary>Gets the captured or pressed control.</summary>
    public Control Control { get; }

    /// <summary>Gets why interaction was cancelled.</summary>
    public ReleaseReason Reason { get; }
}
