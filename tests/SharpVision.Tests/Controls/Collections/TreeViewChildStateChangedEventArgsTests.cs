// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Collections;

/// <summary>Proves child-state transition events contain only defined lifecycle states, matching
/// every other EventArgs type carrying an enum parameter (for example
/// <see cref="TreeViewItemInvokedEventArgs"/>, <see cref="CheckChangedEventArgs"/>, and
/// <see cref="RadioButtonSelectionChangedEventArgs"/>).</summary>
public sealed class TreeViewChildStateChangedEventArgsTests
{
    /// <summary>Verifies an undefined previous state cannot enter a completed transition event.</summary>
    [Fact]
    public void Constructor_WhenPreviousIsUndefined_Throws()
    {
        var previous = (TreeViewChildState) 999;

        var action = () => _ = new TreeViewChildStateChangedEventArgs(previous, TreeViewChildState.Loaded);

        action.ShouldThrow<ArgumentOutOfRangeException>().ParamName.ShouldBe("previous");
    }

    /// <summary>Verifies an undefined current state cannot enter a completed transition event.</summary>
    [Fact]
    public void Constructor_WhenCurrentIsUndefined_Throws()
    {
        var current = (TreeViewChildState) 999;

        var action = () => _ = new TreeViewChildStateChangedEventArgs(TreeViewChildState.Unloaded, current);

        action.ShouldThrow<ArgumentOutOfRangeException>().ParamName.ShouldBe("current");
    }
}
