// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Collections;

/// <summary>Verifies proposed TreeView selection deltas remain stable event snapshots.</summary>
public sealed class TreeViewSelectionChangingEventArgsTests
{
    /// <summary>Verifies a consumer cannot replace an item through the published read-only view.</summary>
    [Fact]
    public void AddedItems_WhenConsumerAttemptsArrayMutation_PreservesSnapshot()
    {
        var original = new TreeViewItem("original");
        var replacement = new TreeViewItem("replacement");
        var eventArgs = new TreeViewSelectionChangingEventArgs([original], []);

        if (eventArgs.AddedItems is TreeViewItem[] mutableItems)
        {
            mutableItems[0] = replacement;
        }

        eventArgs.AddedItems.ShouldHaveSingleItem().ShouldBeSameAs(original);
    }
}
