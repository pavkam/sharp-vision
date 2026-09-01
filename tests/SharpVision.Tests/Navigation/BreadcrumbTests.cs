// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Navigation;

using Moq;

/// <summary>Verifies breadcrumb ownership, semantic current state, repair, and activation ordering.</summary>
public sealed class BreadcrumbTests
{
    /// <summary>Verifies an empty path starts with an explicit no-current state.</summary>
    [Fact]
    public void Constructor_WhenCreated_HasNoCurrentItem()
    {
        var breadcrumb = new Breadcrumb();

        breadcrumb.CurrentIndex.ShouldBe(-1);
        breadcrumb.CurrentItem.ShouldBeNull();
    }

    /// <summary>Verifies path mutations establish the final available location.</summary>
    [Fact]
    public void Items_WhenAdded_AutoSelectsFinalAvailableItem()
    {
        var breadcrumb = new Breadcrumb();
        var root = new BreadcrumbItem { Text = "Root" };
        var leaf = new BreadcrumbItem { Text = "Leaf" };

        breadcrumb.Items.Add(root);
        breadcrumb.Items.Add(leaf);

        breadcrumb.CurrentIndex.ShouldBe(1);
        breadcrumb.CurrentItem.ShouldBeSameAs(leaf);
        leaf.IsCurrent.ShouldBeTrue();
        root.IsCurrent.ShouldBeFalse();
    }

    /// <summary>Verifies a deliberate clear survives reads and ordinary layout until a path mutation.</summary>
    [Fact]
    public void CurrentIndex_WhenExplicitlyCleared_RemainsClearUntilCollectionMutation()
    {
        var breadcrumb = new Breadcrumb();
        breadcrumb.Items.Add(new BreadcrumbItem { Text = "Root" });
        breadcrumb.Items.Add(new BreadcrumbItem { Text = "Leaf" });

        breadcrumb.CurrentIndex = -1;

        breadcrumb.CurrentItem.ShouldBeNull();
        breadcrumb.CurrentIndex.ShouldBe(-1);

        var final = new BreadcrumbItem { Text = "Final" };
        breadcrumb.Items.Add(final);
        breadcrumb.CurrentItem.ShouldBeSameAs(final);
    }

    /// <summary>Verifies unavailable targets are rejected before semantic state changes.</summary>
    [Theory]
    [InlineData(Visibility.Hidden)]
    [InlineData(Visibility.Collapsed)]
    public void CurrentItem_WhenOwnedItemIsInvisible_ThrowsWithoutMutation(Visibility visibility)
    {
        var breadcrumb = new Breadcrumb();
        var root = new BreadcrumbItem { Text = "Root" };
        var leaf = new BreadcrumbItem { Text = "Leaf" };
        breadcrumb.Items.Add(root);
        breadcrumb.Items.Add(leaf);
        root.Visibility = visibility;

        _ = Should.Throw<InvalidOperationException>(() => breadcrumb.CurrentItem = root);

        breadcrumb.CurrentItem.ShouldBeSameAs(leaf);
    }

    /// <summary>Verifies disabling the current location repairs to the final surviving location.</summary>
    [Fact]
    public void CurrentItem_WhenCurrentBecomesUnavailable_RepairsToFinalAvailableItem()
    {
        var breadcrumb = new Breadcrumb();
        var root = new BreadcrumbItem { Text = "Root" };
        var middle = new BreadcrumbItem { Text = "Middle" };
        var leaf = new BreadcrumbItem { Text = "Leaf" };
        breadcrumb.Items.Add(root);
        breadcrumb.Items.Add(middle);
        breadcrumb.Items.Add(leaf);
        middle.IsEnabled = false;

        leaf.IsEnabled = false;

        breadcrumb.CurrentItem.ShouldBeSameAs(root);
        root.IsCurrent.ShouldBeTrue();
    }

    /// <summary>Verifies a disabled owned target is rejected before current state changes.</summary>
    [Fact]
    public void CurrentIndex_WhenTargetIsDisabled_ThrowsWithoutMutation()
    {
        var breadcrumb = new Breadcrumb();
        var root = new BreadcrumbItem { Text = "Root", IsEnabled = false };
        var leaf = new BreadcrumbItem { Text = "Leaf" };
        breadcrumb.Items.Add(root);
        breadcrumb.Items.Add(leaf);

        _ = Should.Throw<InvalidOperationException>(() => breadcrumb.CurrentIndex = 0);

        breadcrumb.CurrentItem.ShouldBeSameAs(leaf);
    }

    /// <summary>Verifies a foreign item follows the established selected-item convention and clears current.</summary>
    [Fact]
    public void CurrentItem_WhenAssignedForeignItem_ClearsCurrent()
    {
        var breadcrumb = new Breadcrumb();
        breadcrumb.Items.Add(new BreadcrumbItem { Text = "Root" });

        breadcrumb.CurrentItem = new BreadcrumbItem { Text = "Foreign" };

        breadcrumb.CurrentIndex.ShouldBe(-1);
        breadcrumb.CurrentItem.ShouldBeNull();
    }

    /// <summary>Verifies item subscribers observe the owner commit made by canonical activation.</summary>
    [Fact]
    public void PerformInvoke_WhenSubscriberPredatesOwnership_ObservesCommittedCurrent()
    {
        var item = new BreadcrumbItem { Text = "Root" };
        var breadcrumb = new Breadcrumb();
        BreadcrumbItem? observed = null;
        item.Invoked += (_, _) => observed = breadcrumb.CurrentItem;
        breadcrumb.Items.Add(new BreadcrumbItem { Text = "Other" });
        breadcrumb.Items.Add(item);
        breadcrumb.CurrentIndex = 0;

        item.PerformInvoke();

        observed.ShouldBeSameAs(item);
        breadcrumb.CurrentItem.ShouldBeSameAs(item);
    }

    /// <summary>Verifies reentrant removal after the item event denies the captured command.</summary>
    [Fact]
    public void PerformInvoke_WhenInvokedHandlerRemovesItem_DoesNotExecuteCapturedCommand()
    {
        var command = new Mock<System.Windows.Input.ICommand>();
        _ = command.Setup(candidate => candidate.CanExecute(null)).Returns(true);
        var item = new BreadcrumbItem { Text = "Root", Command = command.Object };
        var breadcrumb = new Breadcrumb();
        breadcrumb.Items.Add(item);
        item.Invoked += (_, _) => breadcrumb.Items.Remove(item);

        item.PerformInvoke();

        command.Verify(candidate => candidate.Execute(null), Times.Never);
        breadcrumb.Items.ShouldBeEmpty();
    }

    /// <summary>Verifies event publication follows index, item, then transition-event order.</summary>
    [Fact]
    public void CurrentItem_WhenChanged_PublishesCommittedPropertyOrder()
    {
        var breadcrumb = new Breadcrumb();
        var root = new BreadcrumbItem { Text = "Root" };
        var leaf = new BreadcrumbItem { Text = "Leaf" };
        breadcrumb.Items.Add(root);
        breadcrumb.Items.Add(leaf);
        List<string> observed = [];
        breadcrumb.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(Breadcrumb.CurrentIndex) or nameof(Breadcrumb.CurrentItem))
            {
                observed.Add(args.PropertyName);
            }
        };
        breadcrumb.CurrentChanged += (_, args) =>
        {
            args.CurrentItem.ShouldBeSameAs(root);
            observed.Add(nameof(Breadcrumb.CurrentChanged));
        };

        breadcrumb.CurrentItem = root;

        observed.ShouldBe([
            nameof(Breadcrumb.CurrentIndex),
            nameof(Breadcrumb.CurrentItem),
            nameof(Breadcrumb.CurrentChanged)]);
    }
}
