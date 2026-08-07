// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Input;

/// <summary>Proves list selection event data publishes deterministic sorted deltas.</summary>
public sealed class ListSelectionEventArgsTests
{
    /// <summary>Verifies proposed selection indexes are normalized before handlers observe them.</summary>
    [Fact]
    public void ChangingConstructor_WhenIndexesAreUnsorted_PublishesSortedSnapshots()
    {
        var eventArgs = new ListSelectionChangingEventArgs([3, 1, 2], [5, 4]);

        eventArgs.AddedIndexes.ToArray().ShouldBe([1, 2, 3]);
        eventArgs.RemovedIndexes.ToArray().ShouldBe([4, 5]);
    }

    /// <summary>Verifies committed selection indexes are normalized before handlers observe them.</summary>
    [Fact]
    public void ChangedConstructor_WhenIndexesAreUnsorted_PublishesSortedSnapshots()
    {
        var eventArgs = new ListSelectionChangedEventArgs([3, 1, 2], [5, 4]);

        eventArgs.AddedIndexes.ToArray().ShouldBe([1, 2, 3]);
        eventArgs.RemovedIndexes.ToArray().ShouldBe([4, 5]);
    }
}
