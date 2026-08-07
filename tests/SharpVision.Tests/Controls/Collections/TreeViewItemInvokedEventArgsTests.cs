// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Collections;

/// <summary>Proves tree item invocation events contain a real semantic input path.</summary>
public sealed class TreeViewItemInvokedEventArgsTests
{
    /// <summary>Verifies an undefined activation cause cannot enter a completed invocation event.</summary>
    [Fact]
    public void Constructor_WhenCauseIsUndefined_Throws()
    {
        var item = new TreeViewItem();
        var cause = (ActivationCause) 999;

        var action = () =>
        {
            _ = new TreeViewItemInvokedEventArgs(item, cause);
        };

        action.ShouldThrow<ArgumentOutOfRangeException>().ParamName.ShouldBe("cause");
    }
}
