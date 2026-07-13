// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

using SharpVision.Controls;

/// <summary>Provides cancellable state before one focus transaction commits.</summary>
public sealed class FocusChangingEventArgs: EventArgs
{
    /// <summary>Initializes one pending focus transition.</summary>
    /// <param name="previous">The previously focused control.</param>
    /// <param name="next">The requested next control, or null for release.</param>
    public FocusChangingEventArgs(Control? previous, Control? next)
    {
        Previous = previous;
        Next = next;
    }

    /// <summary>Gets the control focused before the request.</summary>
    public Control? Previous { get; }

    /// <summary>Gets the requested next control, or null for release.</summary>
    public Control? Next { get; }

    /// <summary>Gets or sets whether an explicit request should be cancelled.</summary>
    public bool Cancel { get; set; }
}
