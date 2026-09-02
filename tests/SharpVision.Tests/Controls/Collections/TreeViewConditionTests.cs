// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Collections;

/// <summary>Verifies TreeViewItemCollection rejects every invalid structural mutation before any
/// state changes: self-containment, duplicates, out-of-range replacement and move indexes, and
/// caller mutation of a loader-governed collection.</summary>
public sealed class TreeViewConditionTests
{
    /// <summary>Verifies an item cannot be added to its own children.</summary>
    [Fact]
    public void Add_WhenCandidateIsTheParentItself_ThrowsWithoutMutating()
    {
        var item = new TreeViewItem("Self");

        var exception = Should.Throw<InvalidOperationException>(() => item.Children.Add(item));

        exception.Message.ShouldContain("cannot contain itself");
        item.Children.Count.ShouldBe(0);
        item.ChildState.ShouldBe(TreeViewChildState.Leaf);
    }

    /// <summary>Verifies adding an item that the same collection already owns is rejected.</summary>
    [Fact]
    public void Add_WhenItemIsAlreadyInTheCollection_ThrowsArgumentException()
    {
        var tree = new TreeView();
        var item = new TreeViewItem("Once");
        tree.Items.Add(item);

        var exception = Should.Throw<ArgumentException>(() => tree.Items.Add(item));

        exception.ParamName.ShouldBe("item");
        tree.Items.Count.ShouldBe(1);
    }

    /// <summary>Verifies the indexer rejects an out-of-range replacement position without
    /// touching the candidate.</summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    public void Indexer_WhenReplacementIndexIsOutOfRange_ThrowsBeforeMutation(int index)
    {
        var tree = new TreeView();
        tree.Items.Add(new TreeViewItem("Only"));
        var candidate = new TreeViewItem("Candidate");

        _ = Should.Throw<ArgumentOutOfRangeException>(() => tree.Items[index] = candidate);

        tree.Items.Count.ShouldBe(1);
        tree.Items[0].Header.ShouldBe("Only");
        candidate.FindTreeView().ShouldBeNull();
    }

    /// <summary>Verifies Move rejects an out-of-range source index before any reorder.</summary>
    [Theory]
    [InlineData(-1, 0)]
    [InlineData(2, 0)]
    public void Move_WhenSourceIndexIsOutOfRange_ThrowsBeforeMutation(int oldIndex, int newIndex)
    {
        var tree = new TreeView();
        var first = new TreeViewItem("First");
        var second = new TreeViewItem("Second");
        tree.Items.Add(first);
        tree.Items.Add(second);

        var exception = Should.Throw<ArgumentOutOfRangeException>(() => tree.Items.Move(oldIndex, newIndex));

        exception.ParamName.ShouldBe("oldIndex");
        tree.Items[0].ShouldBeSameAs(first);
        tree.Items[1].ShouldBeSameAs(second);
    }

    /// <summary>Verifies every caller mutation path on a collection governed by an async child
    /// source is rejected, leaving the loader as the only writer.</summary>
    [Fact]
    public void Children_WhenGovernedByAChildSource_RejectEveryCallerMutation()
    {
        var item = new TreeViewItem("Remote") { ChildSource = new FakeTreeViewChildSource() };
        var candidate = new TreeViewItem("Candidate");
        item.ChildState.ShouldBe(TreeViewChildState.Unloaded);

        foreach (var mutation in new Action[]
                 {
                     () => item.Children.Add(candidate),
                     () => item.Children.Insert(0, candidate),
                     () => item.Children.Remove(candidate),
                     () => item.Children.RemoveAt(0),
                     () => item.Children.Move(0, 0),
                     item.Children.Clear,
                     () => item.Children[0] = candidate
                 })
        {
            var exception = Should.Throw<InvalidOperationException>(mutation);
            exception.Message.ShouldContain("governed by an async child source");
        }

        item.Children.Count.ShouldBe(0);
        candidate.FindTreeView().ShouldBeNull();
        item.ChildState.ShouldBe(TreeViewChildState.Unloaded);
    }

    /// <summary>Verifies assigning a child source over directly authored children is rejected and
    /// leaves those children and the Loaded state in place.</summary>
    [Fact]
    public void ChildSource_WhenChildrenWereAuthoredDirectly_ThrowsWithoutMutating()
    {
        var item = new TreeViewItem("Authored");
        item.Children.Add(new TreeViewItem("Child"));

        var exception = Should.Throw<InvalidOperationException>(() => item.ChildSource = new FakeTreeViewChildSource());

        exception.Message.ShouldContain("Clear Children first");
        item.ChildSource.ShouldBeNull();
        item.Children.Count.ShouldBe(1);
        item.ChildState.ShouldBe(TreeViewChildState.Loaded);
    }
}
