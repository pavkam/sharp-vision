// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

using SharpVision.Controls;

/// <summary>Describes one committed focus transition.</summary>
public sealed class FocusChangedEventArgs: EventArgs
{
    /// <summary>Initializes one committed focus transition.</summary>
    /// <param name="previous">The previously focused control.</param>
    /// <param name="current">The newly focused control.</param>
    public FocusChangedEventArgs(Control? previous, Control? current)
    {
        Previous = previous;
        Current = current;
    }

    /// <summary>Gets the control focused before the commit.</summary>
    public Control? Previous { get; }

    /// <summary>Gets the control focused after the commit.</summary>
    public Control? Current { get; }
}
