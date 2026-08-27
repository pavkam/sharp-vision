// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Navigation;

/// <summary>Verifies detached navigation-group state, validation, and typed ownership.</summary>
public sealed class NavigationViewGroupTests
{
    /// <summary>Verifies a group starts expanded and delegates keyboard focus to its owning view.</summary>
    [Fact]
    public void Constructor_WhenCreated_UsesDocumentedInteractionDefaults()
    {
        var group = new NavigationViewGroup();

        group.Header.ShouldBe(string.Empty);
        group.IsExpanded.ShouldBeTrue();
        group.IsFocusable.ShouldBeFalse();
        group.IsTabStop.ShouldBeFalse();
        group.Items.ShouldBeEmpty();
    }

    /// <summary>Verifies header validation and child ownership commit atomically at the public boundary.</summary>
    [Fact]
    public void HeaderAndItems_WhenMutated_ValidateTextAndTransferOwnership()
    {
        var group = new NavigationViewGroup { Header = "Tools" };
        var item = new NavigationViewItem { Text = "Build" };

        group.Items.Add(item);

        group.Header.ShouldBe("Tools");
        var presentationParent = item.Parent.ShouldNotBeNull();
        presentationParent.ShouldNotBeSameAs(group);
        group.Items.ShouldContain(item);

        _ = Should.Throw<ArgumentException>(() => group.Header = "Bad\nHeader");

        group.Header.ShouldBe("Tools");
        group.Items.Remove(item).ShouldBeTrue();
        item.Parent.ShouldBeNull();
    }
}
