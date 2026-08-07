// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies modality transitions between different control modal presentations.</summary>
public sealed class ModalityTransitionTests
{
    /// <summary>Verifies a menu item invocation that opens a popup transitions scopes cleanly.</summary>
    [Fact]
    public async Task OpenModal_WhenMenuItemOpensPopup_TransitionsFromMenuScopeToPopupScopeAsync()
    {
        // Arrange
        var background = new ProbeControl { Focusable = true };
        var dialogContent = new ProbeControl { Focusable = true };
        var dialog = new Popup { Content = dialogContent };
        var actionItem = new MenuItem { Text = "Action" };
        ModalScope? dialogScope = null;
        actionItem.Invoked += (_, _) =>
        {
            dialogScope = dialog.OpenModal(OutsideInteraction.Dismiss, dialogContent);
        };
        var menu = new Menu { Items = { actionItem } };
        var root = new Overlay { Children = { background, menu, dialog } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(30, 10),
            TestContext.Current.CancellationToken);

        // Pre-condition: focus the background
        await surface.UpdateAsync(
            () => surface.Application.Focus.Focus(background).ShouldBeTrue(),
            "focus background");

        // Act — click the menu item to invoke it and open the popup
        await surface.Pointer.ClickAsync(actionItem);

        // Assert — popup modal is active with focus on dialog content
        _ = dialogScope.ShouldNotBeNull();
        dialogScope.Active.ShouldBeTrue();
        surface.Application.Modality.Active.ShouldBeSameAs(dialogScope);
        surface.Application.Focus.Focused.ShouldBeSameAs(dialogContent);
        dialog.IsOpen.ShouldBeTrue();

        // Act — close the popup
        await surface.UpdateAsync(() => dialog.IsOpen = false, "close popup");

        // Assert — modal scope ended, focus returned to menu (which had focus when dialog opened)
        dialogScope.Active.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();
        surface.Application.Focus.Focused.ShouldBeSameAs(menu);
    }
}
