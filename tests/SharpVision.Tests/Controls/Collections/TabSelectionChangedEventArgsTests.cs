// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Collections;

/// <summary>Proves tab selection transitions reject indexes outside the documented sentinel range.</summary>
public sealed class TabSelectionChangedEventArgsTests
{
    /// <summary>Verifies neither transition endpoint can publish an index below the -1 sentinel.</summary>
    [Theory]
    [InlineData(-2, 0, "previousIndex")]
    [InlineData(0, -2, "currentIndex")]
    public void Constructor_WhenIndexIsBelowSentinel_Throws(
        int previousIndex,
        int currentIndex,
        string parameterName)
    {
        var action = () => new TabSelectionChangedEventArgs(
            previousIndex,
            currentIndex,
            null,
            null);

        action.ShouldThrow<ArgumentOutOfRangeException>().ParamName.ShouldBe(parameterName);
    }
}
