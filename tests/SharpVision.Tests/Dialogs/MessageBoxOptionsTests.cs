// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Dialogs;

/// <summary>Verifies defaults and ShowAsync forwarding for the MessageBox options carrier. Options
/// themselves are plain init-only properties with no local validation - the required-member checks
/// live in <see cref="MessageBox.ShowAsync(ControlBase,string,MessageBoxOptions)"/>, so this suite
/// exercises those effects rather than a property setter.</summary>
public sealed class MessageBoxOptionsTests
{
    /// <summary>Verifies a freshly constructed options record exposes the documented defaults.</summary>
    [Fact]
    public void Constructor_WhenCreated_UsesDocumentedDefaults()
    {
        var options = new MessageBoxOptions();

        options.Title.ShouldBe("Message");
        options.Buttons.ShouldBe(MessageBoxButtons.Ok);
        options.OkText.ShouldBe("&OK");
        options.CancelText.ShouldBe("&Cancel");
        options.YesText.ShouldBe("&Yes");
        options.NoText.ShouldBe("&No");
        options.Style.ShouldBeNull();
        options.ButtonStyle.ShouldBeNull();
    }

    /// <summary>Verifies ShowAsync forwards OkText, CancelText, and ButtonStyle from options onto
    /// the presented MessageBox - the sibling captions and style already covered by
    /// MessageBoxTests.ShowAsync_WhenConfiguredThroughOptions_AppliesEveryConfiguredValueAsync only
    /// exercises Title, Buttons, YesText, NoText, and Style, leaving these three unobserved through
    /// the options-carrier path.</summary>
    [Fact]
    public async Task ShowAsync_WhenOkTextCancelTextAndButtonStyleAreSet_AppliesThemToThePresentedDialogAsync()
    {
        // Arrange
        var opener = new Button { Text = "Open" };
        var host = new Overlay { Children = { opener } };
        await using var surface = await ComponentSurface.MountAsync(
            host,
            new Size(40, 12),
            TestContext.Current.CancellationToken);
        var buttonStyle = ButtonStyle.Standard with { Padding = new Thickness(horizontal: 2, vertical: 1) };
        var options = new MessageBoxOptions
        {
            Buttons = MessageBoxButtons.OkCancel,
            OkText = "&Guardar",
            CancelText = "&Descartar",
            ButtonStyle = buttonStyle
        };
        Task<MessageBoxResult>? pending = null;

        // Act
        await surface.UpdateAsync(
            () => pending = MessageBox.ShowAsync(opener, "¿Guardar cambios?", options),
            "show MessageBox configured through options");
        var messageBox = OwnedTree.Find<MessageBox>(surface.Application.Root).ShouldNotBeNull();

        // Assert
        messageBox.OkText.ShouldBe("&Guardar");
        messageBox.CancelText.ShouldBe("&Descartar");
        messageBox.ActualButtonStyle.ShouldBe(buttonStyle);
        OwnedTree.FindAll<Button>(messageBox).Select(static button => button.Text)
            .ShouldBe(["&Guardar", "&Descartar"]);

        await surface.Keyboard.PressAsync(Code.Enter);
        (await pending!).ShouldBe(MessageBoxResult.Ok);
    }

    /// <summary>Verifies each required options caption is validated before any dialog is
    /// constructed or attached - MessageBoxTests.ShowAsync_WhenOptionsAreInvalid_ThrowsValidationExceptions
    /// only covers a null options/owner/message argument, leaving a null caption inside an
    /// otherwise valid options instance unobserved.</summary>
    [Fact]
    public void ShowAsync_WhenARequiredCaptionIsNull_ThrowsArgumentNullException()
    {
        var opener = new Button { Text = "Open" };
        var host = new Overlay { Children = { opener } };

        _ = Should.Throw<ArgumentNullException>(
            () => MessageBox.ShowAsync(opener, "Message", new MessageBoxOptions { OkText = null! }));
        _ = Should.Throw<ArgumentNullException>(
            () => MessageBox.ShowAsync(opener, "Message", new MessageBoxOptions { CancelText = null! }));
        _ = Should.Throw<ArgumentNullException>(
            () => MessageBox.ShowAsync(opener, "Message", new MessageBoxOptions { YesText = null! }));
        _ = Should.Throw<ArgumentNullException>(
            () => MessageBox.ShowAsync(opener, "Message", new MessageBoxOptions { NoText = null! }));
        _ = Should.Throw<ArgumentNullException>(
            () => MessageBox.ShowAsync(opener, "Message", new MessageBoxOptions { Title = null! }));
    }
}
