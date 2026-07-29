// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Collections;

using System.ComponentModel;

/// <summary>Provides a cancellable proposed TreeView selection delta.</summary>
/// <remarks>
/// Raised only for caller- and input-driven changes. Normalization the control performs on its own
/// behalf — narrowing <see cref="TreeView.SelectionMode"/>, or repairing selection after items are
/// detached or disabled — is not cancellable, because refusing it would leave the control in a
/// state its own configuration forbids.
/// </remarks>
[PublicAPI]
public sealed class TreeViewSelectionChangingEventArgs: CancelEventArgs
{
    private static readonly TreeViewItem[] _none = [];

    /// <summary>Initializes owned proposed addition and removal snapshots.</summary>
    /// <param name="addedItems">The items proposed for selection, in stable tree order.</param>
    /// <param name="removedItems">The items proposed for deselection, in stable tree order.</param>
    /// <exception cref="ArgumentNullException">A delta sequence or one of its items is null.</exception>
    public TreeViewSelectionChangingEventArgs(
        IReadOnlyList<TreeViewItem> addedItems,
        IReadOnlyList<TreeViewItem> removedItems)
    {
        AddedItems = Copy(addedItems, nameof(addedItems));
        RemovedItems = Copy(removedItems, nameof(removedItems));
    }

    /// <summary>Gets the owned items proposed for selection, in stable tree order.</summary>
    public IReadOnlyList<TreeViewItem> AddedItems { get; }

    /// <summary>Gets the owned items proposed for deselection, in stable tree order.</summary>
    public IReadOnlyList<TreeViewItem> RemovedItems { get; }

    private static TreeViewItem[] Copy(IReadOnlyList<TreeViewItem> items, string name)
    {
        ArgumentNullException.ThrowIfNull(items, name);

        if (items.Count == 0)
        {
            return _none;
        }

        var copy = new TreeViewItem[items.Count];

        for (var index = 0; index < items.Count; index++)
        {
            copy[index] = items[index] ?? throw new ArgumentNullException(
                name,
                "A selection delta cannot contain null.");
        }

        return copy;
    }
}
