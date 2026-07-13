// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

using SharpVision.Controls;

/// <summary>Reports a committed RadioButton group selection transition.</summary>
public sealed class SelectionChangedEventArgs: EventArgs
{
    /// <summary>Initializes immutable old/new members and cause.</summary>
    /// <param name="previous">The previously selected member, or null.</param>
    /// <param name="current">The newly selected member, or null.</param>
    /// <param name="cause">The defined transition cause.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="cause"/> is unknown.</exception>
    public SelectionChangedEventArgs(
        RadioButton? previous,
        RadioButton? current,
        ActivationCause cause)
    {
        if (!Enum.IsDefined(cause))
        {
            throw new ArgumentOutOfRangeException(nameof(cause), cause, "The activation cause is unknown.");
        }

        Previous = previous;
        Current = current;
        Cause = cause;
    }

    /// <summary>Gets the previously selected member.</summary>
    public RadioButton? Previous { get; }

    /// <summary>Gets the newly selected member.</summary>
    public RadioButton? Current { get; }

    /// <summary>Gets the transition input path.</summary>
    public ActivationCause Cause { get; }
}
