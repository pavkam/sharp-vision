// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

/// <summary>Describes one committed focus transition.</summary>
[PublicAPI]
public sealed class FocusChangedEventArgs: EventArgs
{
    /// <summary>Initializes one committed focus transition.</summary>
    /// <param name="previous">The previously focused control.</param>
    /// <param name="current">The newly focused control.</param>
    public FocusChangedEventArgs(ControlBase? previous, ControlBase? current)
        : this(previous, current, FocusReason.Programmatic)
    {
    }

    /// <summary>Initializes one committed focus transition with its initiating reason.</summary>
    /// <param name="previous">The previously focused control.</param>
    /// <param name="current">The newly focused control.</param>
    /// <param name="reason">The defined reason for the committed transition.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="reason"/> is undefined.</exception>
    public FocusChangedEventArgs(ControlBase? previous, ControlBase? current, FocusReason reason)
    {
        if (!Enum.IsDefined(reason))
        {
            throw new ArgumentOutOfRangeException(nameof(reason), reason, "The focus reason is unknown.");
        }

        Previous = previous;
        Current = current;
        Reason = reason;
    }

    /// <summary>Gets the control focused before the commit.</summary>
    public ControlBase? Previous { get; }

    /// <summary>Gets the control focused after the commit.</summary>
    public ControlBase? Current { get; }

    /// <summary>Gets the reason for the committed transition.</summary>
    public FocusReason Reason { get; }
}
