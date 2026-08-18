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
    public TreeViewChildStateChangedEventArgs(TreeViewChildState previous, TreeViewChildState current)
    {
        Previous = previous;
        Current = current;
    }

    /// <summary>Gets the previous state.</summary>
    public TreeViewChildState Previous { get; }

    /// <summary>Gets the committed state.</summary>
    public TreeViewChildState Current { get; }
}
