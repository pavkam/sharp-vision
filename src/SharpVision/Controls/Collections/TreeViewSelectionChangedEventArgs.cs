// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Collections;

/// <summary>Reports one committed TreeView selection transition.</summary>
[PublicAPI]
public sealed class TreeViewSelectionChangedEventArgs: EventArgs
{
    private static readonly IReadOnlyList<TreeViewItem> _none = Array.AsReadOnly<TreeViewItem>([]);

    /// <summary>Initializes one immutable selection transition with owned delta snapshots.</summary>
    /// <param name="previousItem">The first selected item before the transition, or null.</param>
    /// <param name="currentItem">The first selected item after the transition, or null.</param>
    /// <param name="addedItems">The items this transition selected, in stable tree order.</param>
    /// <param name="removedItems">The items this transition deselected, in stable tree order.</param>
    /// <exception cref="ArgumentNullException">A delta sequence or one of its items is null.</exception>
    public TreeViewSelectionChangedEventArgs(
        TreeViewItem? previousItem,
        TreeViewItem? currentItem,
        IReadOnlyList<TreeViewItem> addedItems,
        IReadOnlyList<TreeViewItem> removedItems)
    {
        PreviousItem = previousItem;
        CurrentItem = currentItem;
        AddedItems = Copy(addedItems, nameof(addedItems));
        RemovedItems = Copy(removedItems, nameof(removedItems));
    }

    /// <summary>Initializes one immutable single-item selection transition.</summary>
    /// <remarks>
    /// Retained so a caller constructing the arguments directly, such as a test double, is not
    /// forced to supply deltas. Both delta snapshots are empty.
    /// </remarks>
    /// <param name="previousItem">The item before the transition, or null.</param>
    /// <param name="currentItem">The committed item, or null.</param>
    public TreeViewSelectionChangedEventArgs(
        TreeViewItem? previousItem,
        TreeViewItem? currentItem) : this(previousItem, currentItem, _none, _none)
    {
    }

    /// <summary>Gets the first selected item before the transition, or null.</summary>
    public TreeViewItem? PreviousItem { get; }

    /// <summary>Gets the first selected item after the transition, or null.</summary>
    public TreeViewItem? CurrentItem { get; }

    /// <summary>Gets the owned items this transition selected, in stable tree order.</summary>
    /// <remarks>
    /// <see cref="PreviousItem"/> and <see cref="CurrentItem"/> describe only the first selected
    /// identity, so a range, select-all, removal repair, or mode change is only fully described by
    /// these deltas. The snapshot is owned and stays valid after later selection changes.
    /// </remarks>
    public IReadOnlyList<TreeViewItem> AddedItems { get; }

    /// <summary>Gets the owned items this transition deselected, in stable tree order.</summary>
    public IReadOnlyList<TreeViewItem> RemovedItems { get; }

    private static IReadOnlyList<TreeViewItem> Copy(IReadOnlyList<TreeViewItem> items, string name)
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

        return Array.AsReadOnly(copy);
    }
}
