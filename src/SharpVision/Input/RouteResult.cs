// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

/// <summary>Captures the observable outcome of one completed routed event.</summary>
public readonly struct RouteResult
{
    /// <summary>Initializes a route result.</summary>
    internal RouteResult(bool handled, PostRouteCommand command, Control? anchor)
    {
        Handled = handled;
        Command = command;
        Anchor = anchor;
    }

    /// <summary>Gets whether routing handled the event.</summary>
    public bool Handled { get; }

    /// <summary>Gets the single deferred command requested by the route.</summary>
    public PostRouteCommand Command { get; }

    /// <summary>Gets the stable route anchor for a deferred command.</summary>
    public Control? Anchor { get; }
}
