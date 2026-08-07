// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Collections;

/// <summary>Verifies TabItem header validation and content ownership.</summary>
public sealed class TabItemTests
{
    /// <summary>Verifies a new page rejects invalid header text before mutation.</summary>
    [ComponentUnitEvidence(typeof(TabItem))]
    [Fact]
    public void Properties_WhenCreatedOrAssignedInvalidHeader_PreserveDefaults()
    {
        var item = new TabItem();

        item.Header.ShouldBeNull();
        item.Content.ShouldBeNull();

        _ = Should.Throw<ArgumentNullException>(() => item.HeaderText = null!);

        item.Header.ShouldBeNull();
    }

    /// <summary>Verifies caller content replacement transfers ownership.</summary>
    [Fact]
    public void Content_WhenReplaced_TransfersOnlyCallerContentOwnership()
    {
        var first = new ControlText("First");
        var second = new ControlText("Second");
        var item = new TabItem { HeaderText = "Page", Content = first };

        item.Content = second;

        first.Parent.ShouldBeNull();
        second.Parent.ShouldBeSameAs(item);
    }
}
