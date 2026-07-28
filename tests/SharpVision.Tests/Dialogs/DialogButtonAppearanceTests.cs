// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Dialogs;

/// <summary>Verifies dialogs retain the library-default Button appearance.</summary>
public sealed class DialogButtonAppearanceTests
{
    /// <summary>Verifies dialog composition does not select a Button kind or override its appearance.</summary>
    [Fact]
    public void Construction_WhenDialogButtonsAreCreated_LeavesDefaultAppearance()
    {
        // Arrange
        var fileSystem = new FakeFilePickerFileSystem();
        using var filePicker = new FilePickerDialog(null, fileSystem);
        using var saveFile = new SaveFileDialog(null, fileSystem);
        using var messageBox = new MessageBox("Continue?", "Confirm", MessageBoxButtons.YesNoCancel);
        using var expected = new Button();

        // Act and assert
        foreach (var dialog in new Control[] { filePicker, saveFile, messageBox })
        {
            var buttons = OwnedTree.FindAll<Button>(dialog);
            buttons.ShouldNotBeEmpty();

            foreach (var button in buttons)
            {
                button.Style.ShouldBeNull();
                button.ActualStyle.ShouldBe(expected.ActualStyle);
                button.ActualFace.ShouldBe(expected.ActualFace);
                button.ActualBorder.ShouldBe(expected.ActualBorder);
                button.ActualShadow.ShouldBe(expected.ActualShadow);
            }
        }
    }
}
