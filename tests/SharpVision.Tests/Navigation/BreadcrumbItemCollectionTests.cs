// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Navigation;

/// <summary>Verifies the typed breadcrumb collection's ownership and mutation contract.</summary>
public sealed class BreadcrumbItemCollectionTests
{
    /// <summary>Verifies insertion order and identity.</summary>
    [Fact]
    public void Add_WhenItemsAreDetached_RetainsOrder()
    {
        var breadcrumb = new Breadcrumb();
        var root = new BreadcrumbItem();
        var leaf = new BreadcrumbItem();

        breadcrumb.Items.Add(root);
        breadcrumb.Items.Add(leaf);

        breadcrumb.Items.ShouldBe([root, leaf]);
    }

    /// <summary>Verifies duplicate ownership is rejected without mutating the path.</summary>
    [Fact]
    public void Add_WhenItemIsAlreadyOwned_ThrowsWithoutMutation()
    {
        var breadcrumb = new Breadcrumb();
        var item = new BreadcrumbItem();
        breadcrumb.Items.Add(item);

        _ = Should.Throw<ArgumentException>(() => breadcrumb.Items.Add(item));

        breadcrumb.Items.ShouldBe([item]);
    }

    /// <summary>Verifies retained focus requests remain authored while ownership imposes owner-only focus.</summary>
    [Fact]
    public void Remove_WhenOwned_RestoresLatestAuthoredFocusValues()
    {
        var breadcrumb = new Breadcrumb();
        var item = new BreadcrumbItem { IsFocusable = true, IsTabStop = false };
        breadcrumb.Items.Add(item);

        item.IsFocusable = true;
        item.IsTabStop = true;
        item.IsFocusable.ShouldBeFalse();
        item.IsTabStop.ShouldBeFalse();

        breadcrumb.Items.Remove(item).ShouldBeTrue();

        item.IsFocusable.ShouldBeTrue();
        item.IsTabStop.ShouldBeTrue();
    }

    /// <summary>Verifies replacement preserves position and detaches the old item.</summary>
    [Fact]
    public void Indexer_WhenReplaced_PreservesPosition()
    {
        var breadcrumb = new Breadcrumb();
        var old = new BreadcrumbItem();
        var replacement = new BreadcrumbItem();
        breadcrumb.Items.Add(old);

        breadcrumb.Items[0] = replacement;

        breadcrumb.Items[0].ShouldBeSameAs(replacement);
        old.Parent.ShouldBeNull();
    }

    /// <summary>Verifies move preserves current identity while changing its public index.</summary>
    [Fact]
    public void Move_WhenCurrentItemMoves_PreservesIdentityAndUpdatesIndex()
    {
        var breadcrumb = new Breadcrumb();
        var root = new BreadcrumbItem();
        var leaf = new BreadcrumbItem();
        breadcrumb.Items.Add(root);
        breadcrumb.Items.Add(leaf);

        breadcrumb.Items.Move(1, 0);

        breadcrumb.CurrentItem.ShouldBeSameAs(leaf);
        breadcrumb.CurrentIndex.ShouldBe(0);
    }

    /// <summary>Verifies caller disposal removes only that semantic item.</summary>
    [Fact]
    public void Dispose_WhenItemIsOwned_RemovesOnlyThatItem()
    {
        var breadcrumb = new Breadcrumb();
        var root = new BreadcrumbItem();
        var leaf = new BreadcrumbItem();
        breadcrumb.Items.Add(root);
        breadcrumb.Items.Add(leaf);

        leaf.Dispose();

        breadcrumb.Items.ShouldBe([root]);
        root.IsDisposed.ShouldBeFalse();
    }
}
