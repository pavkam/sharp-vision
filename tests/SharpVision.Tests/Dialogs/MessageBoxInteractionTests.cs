// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Dialogs;

/// <summary>Proves every MessageBox button layout's captions, default and cancel roles, result
/// mapping per activated button, Escape mapping, Tab order, and option validation.</summary>
public sealed class MessageBoxInteractionTests
{
    /// <summary>Verifies each layout renders its buttons in the documented order with the
    /// documented default and cancel roles, and that activating the first button by Enter
    /// completes with that button's result and restores focus to the opener.</summary>
    [Theory]
    [InlineData(MessageBoxButtons.Ok, "&OK", MessageBoxResult.Ok)]
    [InlineData(MessageBoxButtons.OkCancel, "&OK|&Cancel", MessageBoxResult.Ok)]
    [InlineData(MessageBoxButtons.YesNo, "&Yes|&No", MessageBoxResult.Yes)]
    [InlineData(MessageBoxButtons.YesNoCancel, "&Yes|&No|&Cancel", MessageBoxResult.Yes)]
    public async Task ShowAsync_WhenLayoutVaries_RendersDocumentedButtonsAndDefaultResultAsync(
        MessageBoxButtons layout,
        string expectedCaptions,
        MessageBoxResult expectedDefault)
    {
        // Arrange
        var opener = new Button { Text = "Open" };
        var host = new Overlay { Children = { opener } };
        await using var surface = await ComponentSurface.MountAsync(
            host,
            new Size(60, 20),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => surface.Application.Focus.Focus(opener).ShouldBeTrue(), "focus opener");
        Task<MessageBoxResult>? pending = null;

        // Act
        await surface.UpdateAsync(
            () => pending = MessageBox.ShowAsync(opener, "Message", layout),
            $"show {layout} MessageBox");
        var messageBox = OwnedTree.Find<MessageBox>(surface.Application.Root).ShouldNotBeNull();
        var buttons = OwnedTree.FindAll<Button>(messageBox);

        // Assert
        string.Join('|', buttons.Select(static button => button.Text)).ShouldBe(expectedCaptions);
        buttons[0].IsDefault.ShouldBeTrue();
        buttons.Skip(1).ShouldAllBe(static button => !button.IsDefault);
        var expectsCancel = layout is MessageBoxButtons.OkCancel or MessageBoxButtons.YesNoCancel;
        buttons[^1].IsCancel.ShouldBe(expectsCancel);
        buttons.Take(buttons.Count - 1).ShouldAllBe(static button => !button.IsCancel);
        surface.Application.Focus.Focused.ShouldBeSameAs(buttons[0]);

        // Act
        await surface.Keyboard.PressAsync(Code.Enter);

        // Assert
        (await pending!).ShouldBe(expectedDefault);
        OwnedTree.Find<MessageBox>(surface.Application.Root).ShouldBeNull();
        opener.IsFocused.ShouldBeTrue();
    }

    /// <summary>Verifies clicking each button of each layout completes with exactly that button's
    /// result.</summary>
    [Theory]
    [InlineData(MessageBoxButtons.OkCancel, 1, MessageBoxResult.Cancel)]
    [InlineData(MessageBoxButtons.YesNo, 1, MessageBoxResult.No)]
    [InlineData(MessageBoxButtons.YesNoCancel, 1, MessageBoxResult.No)]
    [InlineData(MessageBoxButtons.YesNoCancel, 2, MessageBoxResult.Cancel)]
    [InlineData(MessageBoxButtons.Ok, 0, MessageBoxResult.Ok)]
    public async Task ShowAsync_WhenButtonIsClicked_CompletesWithThatButtonsResultAsync(
        MessageBoxButtons layout,
        int buttonIndex,
        MessageBoxResult expected)
    {
        // Arrange
        var opener = new Button { Text = "Open" };
        var host = new Overlay { Children = { opener } };
        await using var surface = await ComponentSurface.MountAsync(
            host,
            new Size(60, 20),
            TestContext.Current.CancellationToken);
        Task<MessageBoxResult>? pending = null;
        await surface.UpdateAsync(
            () => pending = MessageBox.ShowAsync(opener, "Message", layout),
            $"show {layout} MessageBox");
        var messageBox = OwnedTree.Find<MessageBox>(surface.Application.Root).ShouldNotBeNull();
        var button = OwnedTree.FindAll<Button>(messageBox)[buttonIndex];

        // Act - the dialog lives in the outermost presentation plane, so click by absolute cell
        await surface.Pointer.MoveToAsync(new Point(
            button.Bounds.X + (button.Bounds.Width / 2),
            button.Bounds.Y + (button.Bounds.Height / 2)));
        await surface.Pointer.PressAsync();
        await surface.Pointer.ReleaseAsync();

        // Assert
        (await pending!).ShouldBe(expected);
        messageBox.IsDisposed.ShouldBeTrue();
        OwnedTree.Find<MessageBox>(surface.Application.Root).ShouldBeNull();
    }

    /// <summary>Verifies Escape completes every layout with Cancel, whether or not a Cancel button
    /// exists.</summary>
    [Theory]
    [InlineData(MessageBoxButtons.Ok)]
    [InlineData(MessageBoxButtons.OkCancel)]
    [InlineData(MessageBoxButtons.YesNo)]
    [InlineData(MessageBoxButtons.YesNoCancel)]
    public async Task Escape_WhenLayoutVaries_CompletesWithCancelAsync(MessageBoxButtons layout)
    {
        // Arrange
        var opener = new Button { Text = "Open" };
        var host = new Overlay { Children = { opener } };
        await using var surface = await ComponentSurface.MountAsync(
            host,
            new Size(60, 20),
            TestContext.Current.CancellationToken);
        Task<MessageBoxResult>? pending = null;
        await surface.UpdateAsync(
            () => pending = MessageBox.ShowAsync(opener, "Message", layout),
            $"show {layout} MessageBox");

        // Act
        await surface.Keyboard.PressAsync(Code.Escape);

        // Assert
        (await pending!).ShouldBe(MessageBoxResult.Cancel);
        OwnedTree.Find<MessageBox>(surface.Application.Root).ShouldBeNull();
    }

    /// <summary>Verifies Tab cycles forward through all three buttons and wraps, and Shift+Tab
    /// cycles backward and wraps, never leaving the dialog.</summary>
    [Fact]
    public async Task Tab_WhenThreeButtonsExist_CyclesAndWrapsInBothDirectionsAsync()
    {
        // Arrange
        var opener = new Button { Text = "Open" };
        var host = new Overlay { Children = { opener } };
        await using var surface = await ComponentSurface.MountAsync(
            host,
            new Size(60, 20),
            TestContext.Current.CancellationToken);
        Task<MessageBoxResult>? pending = null;
        await surface.UpdateAsync(
            () => pending = MessageBox.ShowAsync(opener, "Message", MessageBoxButtons.YesNoCancel),
            "show YesNoCancel MessageBox");
        var messageBox = OwnedTree.Find<MessageBox>(surface.Application.Root).ShouldNotBeNull();
        var buttons = OwnedTree.FindAll<Button>(messageBox);
        surface.Application.Focus.Focused.ShouldBeSameAs(buttons[0]);

        // Act / Assert
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.Application.Focus.Focused.ShouldBeSameAs(buttons[1]);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.Application.Focus.Focused.ShouldBeSameAs(buttons[2]);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.Application.Focus.Focused.ShouldBeSameAs(buttons[0]);
        await surface.Keyboard.PressAsync(Code.Tab, Modifiers.Shift);
        surface.Application.Focus.Focused.ShouldBeSameAs(buttons[2]);
        await surface.Keyboard.PressAsync(Code.Tab, Modifiers.Shift);
        surface.Application.Focus.Focused.ShouldBeSameAs(buttons[1]);
        opener.IsFocused.ShouldBeFalse();

        // Act
        await surface.Keyboard.PressAsync(Code.Enter);

        // Assert
        (await pending!).ShouldBe(MessageBoxResult.No);
    }

    /// <summary>Verifies an undefined button layout supplied through options is rejected before
    /// any dialog is added to the host.</summary>
    [Fact]
    public async Task ShowAsync_WhenOptionsCarryUndefinedButtons_ThrowsWithoutPresentingAsync()
    {
        // Arrange
        var opener = new Button { Text = "Open" };
        var host = new Overlay { Children = { opener } };
        await using var surface = await ComponentSurface.MountAsync(
            host,
            new Size(60, 20),
            TestContext.Current.CancellationToken);

        // Act
        var failure = await surface.Application.Dispatcher.InvokeAsync(
            () => Record.Exception(() =>
            {
                _ = MessageBox.ShowAsync(
                    opener,
                    "Message",
                    new MessageBoxOptions { Buttons = (MessageBoxButtons) 99 });
            }),
            TestContext.Current.CancellationToken);

        // Assert
        _ = failure.ShouldBeOfType<ArgumentOutOfRangeException>();
        OwnedTree.Find<MessageBox>(surface.Application.Root).ShouldBeNull();
        OwnedTree.Find<MessageBox>(surface.Application.Root).ShouldBeNull();
        surface.Application.Modality.Active.ShouldBeNull();
    }

    /// <summary>Verifies changing a caption for a button the layout does not have leaves the
    /// existing buttons' widths untouched.</summary>
    [Fact]
    public void YesText_WhenLayoutHasNoYesButton_LeavesExistingButtonWidthsUntouched()
    {
        // Arrange
        var messageBox = new MessageBox("Message", buttons: MessageBoxButtons.Ok);
        var ok = OwnedTree.FindAll<Button>(messageBox).Single();
        var width = ok.Width;

        // Act
        messageBox.YesText = "A very long affirmative caption";

        // Assert
        ok.Width.ShouldBe(width);
        ok.Text.ShouldBe("&OK");
    }
}
