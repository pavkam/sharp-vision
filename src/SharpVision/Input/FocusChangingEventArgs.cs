// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

/// <summary>Provides cancellable state before one focus transaction commits.</summary>
[PublicAPI]
public sealed class FocusChangingEventArgs: EventArgs
{
    /// <summary>Initializes one pending focus transition.</summary>
    /// <param name="previous">The previously focused control.</param>
    /// <param name="next">The requested next control, or null for release.</param>
    public FocusChangingEventArgs(ControlBase? previous, ControlBase? next)
        : this(previous, next, FocusReason.Programmatic)
    {
    }

    /// <summary>Initializes one pending focus transition with its initiating reason.</summary>
    /// <param name="previous">The previously focused control.</param>
    /// <param name="next">The requested next control, or null for release.</param>
    /// <param name="reason">The defined reason for the requested transition.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="reason"/> is undefined.</exception>
    public FocusChangingEventArgs(ControlBase? previous, ControlBase? next, FocusReason reason)
    {
        if (!Enum.IsDefined(reason))
        {
            throw new ArgumentOutOfRangeException(nameof(reason), reason, "The focus reason is unknown.");
        }

        Previous = previous;
        Next = next;
        Reason = reason;
    }

    /// <summary>Gets the control focused before the request.</summary>
    public ControlBase? Previous { get; }

    /// <summary>Gets the requested next control, or null for release.</summary>
    public ControlBase? Next { get; }

    /// <summary>Gets the reason for the requested transition.</summary>
    public FocusReason Reason { get; }

    /// <summary>
    /// Gets or sets whether a cancellable request should be rejected; non-cancellable transitions ignore this value.
    /// </summary>
    public bool Cancel { get; set; }
}
