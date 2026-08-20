// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies one-parent ownership, attachment, mutation, and disposal.</summary>
public sealed class ControlCollectionTests
{
    /// <summary>Verifies detached children attach in collection order.</summary>
    [Fact]
    public void Add_WhenTreeIsDetached_AssignsParentAndOrder()
    {
        var parent = new ProbeContainer();
        var first = new ProbeControl();
        var second = new ProbeControl();

        parent.Children.Add(first);
        parent.Children.Insert(0, second);

        parent.Children.ShouldBe([second, first]);
        first.Parent.ShouldBeSameAs(parent);
        second.Parent.ShouldBeSameAs(parent);
        first.Dispatcher.ShouldBeNull();
    }

    /// <summary>Verifies invalid ownership changes fail before altering either tree.</summary>
    [Fact]
    public void Add_WhenOwnershipIsInvalid_ThrowsBeforeMutation()
    {
        var firstParent = new ProbeContainer();
        var secondParent = new ProbeContainer();
        var child = new ProbeContainer();
        firstParent.Children.Add(child);

        _ = Should.Throw<ArgumentNullException>(() => firstParent.Children.Add(null!));
        _ = Should.Throw<ArgumentException>(() => firstParent.Children.Add(child));
        _ = Should.Throw<ArgumentException>(() => secondParent.Children.Add(child));
        _ = Should.Throw<ArgumentException>(() => child.Children.Add(firstParent));

        firstParent.Children.ShouldBe([child]);
        secondParent.Children.ShouldBeEmpty();
        child.Parent.ShouldBeSameAs(firstParent);
    }

    /// <summary>Verifies attachment and removal propagate one dispatcher recursively.</summary>
    [Fact]
    public async Task Attach_WhenTreeIsNested_PropagatesAndDetachesDispatcherAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var root = new ProbeContainer();
        var middle = new ProbeContainer();
        var leaf = new ProbeControl();
        middle.Children.Add(leaf);
        root.Children.Add(middle);

        await dispatcher.InvokeAsync(
            () => root.Attach(dispatcher),
            TestContext.Current.CancellationToken);

        root.Dispatcher.ShouldBeSameAs(dispatcher);
        middle.Dispatcher.ShouldBeSameAs(dispatcher);
        leaf.Dispatcher.ShouldBeSameAs(dispatcher);

        await dispatcher.InvokeAsync(
            () => root.Children.Remove(middle).ShouldBeTrue(),
            TestContext.Current.CancellationToken);

        middle.Parent.ShouldBeNull();
        middle.Dispatcher.ShouldBeNull();
        leaf.Dispatcher.ShouldBeNull();
    }

    /// <summary>Verifies attached tree mutation fails off-thread before state changes.</summary>
    [Fact]
    public async Task Add_WhenParentIsAttachedOffThread_ThrowsBeforeMutationAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var root = new ProbeContainer();
        var child = new ProbeControl();
        await dispatcher.InvokeAsync(
            () => root.Attach(dispatcher),
            TestContext.Current.CancellationToken);

        _ = Should.Throw<InvalidOperationException>(() => root.Children.Add(child));

        root.Children.ShouldBeEmpty();
        child.Parent.ShouldBeNull();
        child.Dispatcher.ShouldBeNull();
    }

    /// <summary>Verifies the collection always reports mutable - a caller pattern-matching against
    /// the inherited <see cref="IList{T}"/> surface must not see
    /// IsReadOnly true on a type whose entire purpose is exposing Add/Remove/Insert.</summary>
    [Fact]
    public void IsReadOnly_WhenAccessed_IsFalse()
    {
        var parent = new ProbeContainer();

        parent.Children.IsReadOnly.ShouldBeFalse();
    }

    /// <summary>Verifies indexed replacement and clear clean every ownership edge.</summary>
    [Fact]
    public void Indexer_WhenReplacingAndClearing_DetachesOldChildren()
    {
        var parent = new ProbeContainer();
        var first = new ProbeControl();
        var second = new ProbeControl();
        var replacement = new ProbeControl();
        parent.Children.Add(first);
        parent.Children.Add(second);

        parent.Children[0] = replacement;
        parent.Children.Clear();

        parent.Children.ShouldBeEmpty();
        first.Parent.ShouldBeNull();
        second.Parent.ShouldBeNull();
        replacement.Parent.ShouldBeNull();
    }

    /// <summary>Verifies capacity-one ownership rejects overflow before mutation.</summary>
    [Fact]
    public void Add_WhenCollectionCapacityIsOne_ThrowsBeforeMutation()
    {
        var parent = new ProbeContainer(capacity: 1);
        var first = new ProbeControl();
        var second = new ProbeControl();
        parent.Children.Add(first);

        _ = Should.Throw<InvalidOperationException>(() => parent.Children.Add(second));

        parent.Children.ShouldBe([first]);
        first.Parent.ShouldBeSameAs(parent);
        second.Parent.ShouldBeNull();
    }

    /// <summary>Verifies single-child replacement validates the new owner before detaching the old.</summary>
    [Fact]
    public void SetOnly_WhenReplacementIsInvalid_PreservesExistingOwnership()
    {
        var parent = new ProbeContainer(capacity: 1);
        var other = new ProbeContainer();
        var first = new ProbeControl();
        var invalid = new ProbeControl();
        parent.Children.SetOnly(first);
        other.Children.Add(invalid);

        _ = Should.Throw<ArgumentException>(() => parent.Children.SetOnly(invalid));

        parent.Children.ShouldBe([first]);
        first.Parent.ShouldBeSameAs(parent);
        invalid.Parent.ShouldBeSameAs(other);
    }

    /// <summary>Verifies single-child replacement commits both ownership edges atomically.</summary>
    [Fact]
    public void SetOnly_WhenReplacementIsValid_TransfersOnlyAfterValidation()
    {
        var parent = new ProbeContainer(capacity: 1);
        var first = new ProbeControl();
        var replacement = new ProbeControl();
        parent.Children.SetOnly(first);

        parent.Children.SetOnly(replacement);

        parent.Children.ShouldBe([replacement]);
        first.Parent.ShouldBeNull();
        replacement.Parent.ShouldBeSameAs(parent);
    }

    /// <summary>Verifies failed replacement retains active focus and pointer capture.</summary>
    [Fact]
    public async Task SetOnly_WhenReplacementIsInvalid_PreservesManagerOwnershipAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var parent = new ProbeContainer(capacity: 1);
        var other = new ProbeContainer();
        var first = new ProbeControl { IsFocusable = true };
        var invalid = new ProbeControl();
        parent.Children.SetOnly(first);
        other.Children.Add(invalid);

        await dispatcher.InvokeAsync(() =>
        {
            parent.Attach(dispatcher);
            using FocusManager focus = new(parent);
            using PointerManager capture = new(parent);
            focus.Focus(first).ShouldBeTrue();
            capture.Capture(first).ShouldBeTrue();

            _ = Should.Throw<ArgumentException>(() => parent.Children.SetOnly(invalid));

            focus.Focused.ShouldBeSameAs(first);
            capture.Captured.ShouldBeSameAs(first);
            first.Parent.ShouldBeSameAs(parent);
            first.Dispatcher.ShouldBeSameAs(dispatcher);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies Contains reports identity membership and rejects a null candidate.</summary>
    [Fact]
    public void Contains_WhenCalled_ReportsExactIdentityMembership()
    {
        var parent = new ProbeContainer();
        var member = new ProbeControl();
        var stranger = new ProbeControl();
        parent.Children.Add(member);

        parent.Children.Contains(member).ShouldBeTrue();
        parent.Children.Contains(stranger).ShouldBeFalse();
        _ = Should.Throw<ArgumentNullException>(() => parent.Children.Contains(null!));
    }

    /// <summary>Verifies IndexOf reports the exact zero-based position or -1, and rejects null.</summary>
    [Fact]
    public void IndexOf_WhenCalled_ReportsExactPositionOrNegativeOne()
    {
        var parent = new ProbeContainer();
        var first = new ProbeControl();
        var second = new ProbeControl();
        var stranger = new ProbeControl();
        parent.Children.Add(first);
        parent.Children.Add(second);

        parent.Children.IndexOf(first).ShouldBe(0);
        parent.Children.IndexOf(second).ShouldBe(1);
        parent.Children.IndexOf(stranger).ShouldBe(-1);
        _ = Should.Throw<ArgumentNullException>(() => parent.Children.IndexOf(null!));
    }

    /// <summary>Verifies CopyTo copies committed controls in order into an existing array.</summary>
    [Fact]
    public void CopyTo_WhenDestinationFits_CopiesInCommittedOrder()
    {
        var parent = new ProbeContainer();
        var first = new ProbeControl();
        var second = new ProbeControl();
        parent.Children.Add(first);
        parent.Children.Add(second);
        var destination = new ControlBase[3];

        parent.Children.CopyTo(destination, 1);

        ControlBase?[] expected = [null, first, second];
        destination.ShouldBe(expected);
    }

    /// <summary>Verifies CopyTo rejects a null destination without altering the collection.</summary>
    [Fact]
    public void CopyTo_WhenDestinationIsNull_ThrowsArgumentNullException()
    {
        var parent = new ProbeContainer();
        parent.Children.Add(new ProbeControl());

        _ = Should.Throw<ArgumentNullException>(() => parent.Children.CopyTo(null!, 0));

        parent.Children.Count.ShouldBe(1);
    }

    /// <summary>Verifies the allocation-free enumerator yields committed controls in order.</summary>
    [Fact]
    public void GetEnumerator_WhenIterated_YieldsControlsInCommittedOrder()
    {
        var parent = new ProbeContainer();
        var first = new ProbeControl();
        var second = new ProbeControl();
        parent.Children.Add(first);
        parent.Children.Add(second);
        List<ControlBase> observed = [];

        foreach (var child in parent.Children)
        {
            observed.Add(child);
        }

        observed.ShouldBe([first, second]);
    }

    /// <summary>Verifies the explicit non-generic and generic <see cref="IEnumerable{T}"/>
    /// implementations also yield committed controls in order, matching direct iteration.</summary>
    [Fact]
    public void GetEnumerator_WhenAccessedThroughIEnumerableInterface_YieldsSameOrder()
    {
        var parent = new ProbeContainer();
        var first = new ProbeControl();
        var second = new ProbeControl();
        parent.Children.Add(first);
        parent.Children.Add(second);

        IEnumerable<ControlBase> generic = parent.Children;
        System.Collections.IEnumerable nonGeneric = parent.Children;

        generic.ShouldBe([first, second]);
        nonGeneric.Cast<ControlBase>().ShouldBe([first, second]);
    }

    /// <summary>Verifies RemoveAt detaches the exact positioned child without disposing it.</summary>
    [Fact]
    public void RemoveAt_WhenPositionIsValid_DetachesExactChild()
    {
        var parent = new ProbeContainer();
        var first = new ProbeControl();
        var second = new ProbeControl();
        parent.Children.Add(first);
        parent.Children.Add(second);

        parent.Children.RemoveAt(0);

        parent.Children.ShouldBe([second]);
        first.Parent.ShouldBeNull();
        first.IsDisposed.ShouldBeFalse();
        second.Parent.ShouldBeSameAs(parent);
    }

    /// <summary>Verifies RemoveAt rejects an out-of-range position before mutating the collection.</summary>
    [Fact]
    public void RemoveAt_WhenPositionIsOutOfRange_ThrowsBeforeMutation()
    {
        var parent = new ProbeContainer();
        var only = new ProbeControl();
        parent.Children.Add(only);

        _ = Should.Throw<ArgumentOutOfRangeException>(() => parent.Children.RemoveAt(1));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => parent.Children.RemoveAt(-1));

        parent.Children.ShouldBe([only]);
        only.Parent.ShouldBeSameAs(parent);
    }

    /// <summary>Verifies Remove returns false for a control this collection does not own, without
    /// disturbing existing membership, and rejects a null candidate.</summary>
    [Fact]
    public void Remove_WhenControlIsNotOwned_ReturnsFalseWithoutMutation()
    {
        var parent = new ProbeContainer();
        var owned = new ProbeControl();
        var stranger = new ProbeControl();
        parent.Children.Add(owned);

        parent.Children.Remove(stranger).ShouldBeFalse();
        _ = Should.Throw<ArgumentNullException>(() => parent.Children.Remove(null!));

        parent.Children.ShouldBe([owned]);
    }

    /// <summary>Verifies disposing a container recursively disposes owned descendants.</summary>
    [Fact]
    public void Dispose_WhenTreeIsDetached_DisposesOwnedChildrenOnce()
    {
        var root = new ProbeContainer();
        var child = new ProbeContainer();
        var leaf = new ProbeControl();
        child.Children.Add(leaf);
        root.Children.Add(child);

        root.Dispose();
        root.Dispose();

        root.IsDisposed.ShouldBeTrue();
        child.IsDisposed.ShouldBeTrue();
        leaf.IsDisposed.ShouldBeTrue();
        root.Children.ShouldBeEmpty();
        child.Parent.ShouldBeNull();
    }
}
