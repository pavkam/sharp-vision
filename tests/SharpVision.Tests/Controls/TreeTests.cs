using SharpVision.Tests.Support;
using SharpVision.Threading;

using Shouldly;

namespace SharpVision.Tests.Controls;

/// <summary>Verifies one-parent ownership, attachment, mutation, and disposal.</summary>
public sealed class TreeTests
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
