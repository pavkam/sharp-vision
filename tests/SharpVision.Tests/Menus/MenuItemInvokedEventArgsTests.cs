// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Menus;

/// <summary>Proves menu invocation events contain a real semantic input path.</summary>
public sealed class MenuItemInvokedEventArgsTests
{
    /// <summary>Verifies the constructor exposes exactly the item and cause it was given.</summary>
    [Fact]
    public void Constructor_WhenGivenItemAndCause_ExposesBothAsGiven()
    {
        var item = new MenuItem();

        var eventArgs = new MenuItemInvokedEventArgs(item, ActivationCause.Pointer);

        eventArgs.Item.ShouldBeSameAs(item);
        eventArgs.Cause.ShouldBe(ActivationCause.Pointer);
    }

    /// <summary>Verifies a null item is rejected.</summary>
    [Fact]
    public void Constructor_WhenItemIsNull_ThrowsArgumentNullException()
    {
        var action = () => _ = new MenuItemInvokedEventArgs(null!, ActivationCause.Keyboard);

        action.ShouldThrow<ArgumentNullException>().ParamName.ShouldBe("item");
    }

    /// <summary>Verifies an undefined activation cause cannot enter a completed invocation event.</summary>
    [Fact]
    public void Constructor_WhenCauseIsUndefined_Throws()
    {
        var item = new MenuItem();
        var cause = (ActivationCause) 999;

        var action = () =>
        {
            _ = new MenuItemInvokedEventArgs(item, cause);
        };

        action.ShouldThrow<ArgumentOutOfRangeException>().ParamName.ShouldBe("cause");
    }
}
