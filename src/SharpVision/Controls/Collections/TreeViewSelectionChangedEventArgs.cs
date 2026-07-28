// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Collections;

/// <summary>Reports one committed TreeView selection transition.</summary>
[PublicAPI]
public sealed class TreeViewSelectionChangedEventArgs: EventArgs
{
    /// <summary>Initializes one immutable selection transition.</summary>
    /// <param name="previousItem">The item before the transition, or null.</param>
    /// <param name="currentItem">The committed item, or null.</param>
    public TreeViewSelectionChangedEventArgs(
        TreeViewItem? previousItem,
        TreeViewItem? currentItem)
    {
        PreviousItem = previousItem;
        CurrentItem = currentItem;
    }

    /// <summary>Gets the item before the transition, or null.</summary>
    public TreeViewItem? PreviousItem { get; }

    /// <summary>Gets the committed item, or null.</summary>
    public TreeViewItem? CurrentItem { get; }
}
