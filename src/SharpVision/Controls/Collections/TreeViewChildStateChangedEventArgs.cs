// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Collections;

/// <summary>Reports one committed <see cref="TreeViewItem.ChildState"/> transition.</summary>
[PublicAPI]
public sealed class TreeViewChildStateChangedEventArgs: EventArgs
{
    /// <summary>Initializes immutable transition values.</summary>
    /// <param name="previous">The previous state.</param>
    /// <param name="current">The committed state.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="previous"/> or <paramref name="current"/> is undefined.
    /// </exception>
    public TreeViewChildStateChangedEventArgs(TreeViewChildState previous, TreeViewChildState current)
    {
        ArgumentOutOfRangeException.ThrowIfNotDefined(previous, nameof(previous), "The previous child state is unknown.");
        ArgumentOutOfRangeException.ThrowIfNotDefined(current, nameof(current), "The current child state is unknown.");

        Previous = previous;
        Current = current;
    }

    /// <summary>Gets the previous state.</summary>
    public TreeViewChildState Previous { get; }

    /// <summary>Gets the committed state.</summary>
    public TreeViewChildState Current { get; }
}
