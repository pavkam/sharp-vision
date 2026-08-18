// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Collections;

/// <summary>Verifies committed TreeView selection deltas remain stable event snapshots.</summary>
public sealed class TreeViewSelectionChangedEventArgsTests
{
    /// <summary>Verifies a consumer cannot replace an item through the published read-only view.</summary>
    [Fact]
    public void AddedItems_WhenConsumerAttemptsArrayMutation_PreservesSnapshot()
    {
        var original = new TreeViewItem("original");
        var replacement = new TreeViewItem("replacement");
        var eventArgs = new TreeViewSelectionChangedEventArgs(null, original, [original], []);

        if (eventArgs.AddedItems is TreeViewItem[] mutableItems)
        {
            mutableItems[0] = replacement;
        }

        eventArgs.AddedItems.ShouldHaveSingleItem().ShouldBeSameAs(original);
    }
}
