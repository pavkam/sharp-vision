// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Collections;

/// <summary>Proves tab selection transitions reject indexes outside the documented sentinel range.</summary>
public sealed class TabSelectionChangedEventArgsTests
{
    /// <summary>Verifies each index sentinel agrees with whether its corresponding item exists.</summary>
    [Fact]
    public void Constructor_WhenIndexAndItemPresenceDisagree_Throws()
    {
        var item = new TabItem();

        var previousMissing = () => new TabSelectionChangedEventArgs(0, -1, null, null, ActivationCause.Programmatic);
        var previousUnexpected = () => new TabSelectionChangedEventArgs(-1, -1, item, null, ActivationCause.Programmatic);
        var currentMissing = () => new TabSelectionChangedEventArgs(-1, 0, null, null, ActivationCause.Programmatic);
        var currentUnexpected = () => new TabSelectionChangedEventArgs(-1, -1, null, item, ActivationCause.Programmatic);

        previousMissing.ShouldThrow<ArgumentException>().ParamName.ShouldBe("previousItem");
        previousUnexpected.ShouldThrow<ArgumentException>().ParamName.ShouldBe("previousItem");
        currentMissing.ShouldThrow<ArgumentException>().ParamName.ShouldBe("currentItem");
        currentUnexpected.ShouldThrow<ArgumentException>().ParamName.ShouldBe("currentItem");
    }

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
            null,
            ActivationCause.Programmatic);

        action.ShouldThrow<ArgumentOutOfRangeException>().ParamName.ShouldBe(parameterName);
    }

    /// <summary>Verifies an undefined cause is rejected the same way every sibling transition type rejects it.</summary>
    [Fact]
    public void Constructor_WhenCauseIsUndefined_Throws()
    {
        var action = () => new TabSelectionChangedEventArgs(-1, -1, null, null, (ActivationCause) (-1));

        action.ShouldThrow<ArgumentOutOfRangeException>().ParamName.ShouldBe("cause");
    }

    /// <summary>Verifies the constructed transition exposes the supplied cause.</summary>
    [Fact]
    public void Constructor_ExposesSuppliedCause()
    {
        var item = new TabItem();

        var eventArgs = new TabSelectionChangedEventArgs(-1, 0, null, item, ActivationCause.Keyboard);

        eventArgs.Cause.ShouldBe(ActivationCause.Keyboard);
    }
}
