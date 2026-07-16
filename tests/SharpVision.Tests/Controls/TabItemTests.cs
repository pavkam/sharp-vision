// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies TabItem header validation, retained composition, content ownership, and selection state.</summary>
public sealed class TabItemTests
{
    /// <summary>Verifies a new page owns one retained header and rejects invalid header text before mutation.</summary>
    [Fact]
    public void Properties_WhenCreatedOrAssignedInvalidHeader_PreserveDefaults()
    {
        var item = new TabItem();

        item.Header.ShouldBeEmpty();
        item.Content.ShouldBeNull();
        item.IsSelected.ShouldBeFalse();
        item.HeaderPart.Content.ShouldBeOfType<ControlText>().Content.ShouldBeEmpty();

        _ = Should.Throw<ArgumentNullException>(() => item.Header = null!);
        _ = Should.Throw<ArgumentException>(() => item.Header = "bad\nheader");

        item.Header.ShouldBeEmpty();
    }

    /// <summary>Verifies caller content replacement transfers ownership without replacing the retained header.</summary>
    [Fact]
    public void Content_WhenReplaced_TransfersOnlyCallerContentOwnership()
    {
        var first = new ControlText("First");
        var second = new ControlText("Second");
        var item = new TabItem { Header = "Page", Content = first };
        var header = item.HeaderPart;

        item.Content = second;

        first.Parent.ShouldBeNull();
        second.Parent.ShouldBeSameAs(item);
        item.HeaderPart.ShouldBeSameAs(header);
        item.HeaderPart.Parent.ShouldBeSameAs(item);
    }
}
