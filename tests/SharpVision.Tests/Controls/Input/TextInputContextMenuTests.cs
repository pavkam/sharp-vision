// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

using SharpVision.Menus;

/// <summary>Verifies TextInputContextMenu item composition, opening state synchronization, and
/// command forwarding to its owning TextInput.</summary>
public sealed class TextInputContextMenuTests
{
    /// <summary>Verifies the constructor rejects a null owning text input.</summary>
    [Fact]
    public void Constructor_WhenTextInputIsNull_Throws() =>
        _ = Should.Throw<ArgumentNullException>(() => new TextInputContextMenu(null!));

    /// <summary>Verifies every TextInput owns exactly one TextInputContextMenu with the documented
    /// item order, labels, and shortcut text.</summary>
    [Fact]
    public void Constructor_WhenTextInputIsCreated_BuildsDocumentedItemOrder()
    {
        // Arrange
        using var control = new TextInput();

        // Act
        var menu = control.ContextMenu.ShouldBeOfType<TextInputContextMenu>();

        // Assert
        menu.Items.Count.ShouldBe(8);
        var undo = menu.Items[0].ShouldBeOfType<MenuItem>();
        var redo = menu.Items[1].ShouldBeOfType<MenuItem>();
        _ = menu.Items[2].ShouldBeOfType<MenuSeparator>();
        var cut = menu.Items[3].ShouldBeOfType<MenuItem>();
        var copy = menu.Items[4].ShouldBeOfType<MenuItem>();
        var paste = menu.Items[5].ShouldBeOfType<MenuItem>();
        _ = menu.Items[6].ShouldBeOfType<MenuSeparator>();
        var selectAll = menu.Items[7].ShouldBeOfType<MenuItem>();

        undo.Text.ShouldBe("Undo");
        undo.ShortcutText.ShouldBe("Ctrl+Z");
        redo.Text.ShouldBe("Redo");
        redo.ShortcutText.ShouldBe("Ctrl+Y");
        cut.Text.ShouldBe("Cut");
        cut.ShortcutText.ShouldBe("Ctrl+X");
        copy.Text.ShouldBe("Copy");
        copy.ShortcutText.ShouldBe("Ctrl+C");
        paste.Text.ShouldBe("Paste");
        paste.ShortcutText.ShouldBe("Ctrl+V");
        selectAll.Text.ShouldBe("Select All");
        selectAll.ShortcutText.ShouldBe("Ctrl+A");
    }

    /// <summary>Verifies Opening enables Cut, Copy, and Select All exactly when a selection and
    /// content exist, and leaves Paste disabled without a clipboard reader.</summary>
    [Fact]
    public void Opening_WhenSelectionExistsWithoutClipboardReader_EnablesSelectionCommandsOnly()
    {
        // Arrange
        var control = new TextInput { Text = "abcdef" };
        control.Select(1, 2);
        var menu = control.ContextMenu.ShouldBeOfType<TextInputContextMenu>();

        // Act
        menu.Show(0, 0);

        // Assert
        Item(menu, 3).IsEnabled.ShouldBeTrue(); // Cut
        Item(menu, 4).IsEnabled.ShouldBeTrue(); // Copy
        Item(menu, 5).IsEnabled.ShouldBeFalse(); // Paste - no reader
        Item(menu, 7).IsEnabled.ShouldBeTrue(); // Select All
    }

    /// <summary>Verifies Opening disables Cut, Copy, and Select All when there is no selection and
    /// no text content.</summary>
    [Fact]
    public void Opening_WhenNoSelectionOrContent_DisablesSelectionCommands()
    {
        // Arrange
        var control = new TextInput();
        var menu = control.ContextMenu.ShouldBeOfType<TextInputContextMenu>();

        // Act
        menu.Show(0, 0);

        // Assert
        Item(menu, 3).IsEnabled.ShouldBeFalse(); // Cut
        Item(menu, 4).IsEnabled.ShouldBeFalse(); // Copy
        Item(menu, 7).IsEnabled.ShouldBeFalse(); // Select All
    }

    /// <summary>Verifies Opening disables Cut but keeps Copy enabled for a read-only control with
    /// an active selection - cutting would mutate text, copying does not.</summary>
    [Fact]
    public void Opening_WhenReadOnlyWithSelection_DisablesCutButKeepsCopyEnabled()
    {
        // Arrange
        var control = new TextInput { Text = "abcdef", IsReadOnly = true };
        control.Select(0, 3);
        var menu = control.ContextMenu.ShouldBeOfType<TextInputContextMenu>();

        // Act
        menu.Show(0, 0);

        // Assert
        Item(menu, 3).IsEnabled.ShouldBeFalse(); // Cut
        Item(menu, 4).IsEnabled.ShouldBeTrue(); // Copy
    }

    /// <summary>Verifies Opening disables both Cut and Copy while password masking is active, even
    /// with a live selection, matching TextInput's own source-disclosure policy.</summary>
    [Fact]
    public void Opening_WhenPasswordMasked_DisablesCutAndCopy()
    {
        // Arrange
        var control = new TextInput { Text = "secret", PasswordCharacter = new Rune('*') };
        control.Select(0, control.Text.Length);
        var menu = control.ContextMenu.ShouldBeOfType<TextInputContextMenu>();

        // Act
        menu.Show(0, 0);

        // Assert
        Item(menu, 3).IsEnabled.ShouldBeFalse();
        Item(menu, 4).IsEnabled.ShouldBeFalse();
    }

    /// <summary>Verifies Opening enables Paste only when a non-null clipboard reader returns
    /// non-empty content and the control is not read-only.</summary>
    [Theory]
    [InlineData(false, "clip", true)]
    [InlineData(false, "", false)]
    [InlineData(true, "clip", false)]
    public void Opening_WhenClipboardReaderConfigured_TogglesPasteAsDocumented(
        bool isReadOnly,
        string clipboardContent,
        bool expectedEnabled)
    {
        // Arrange
        var control = new TextInput { IsReadOnly = isReadOnly };
        var menu = control.ContextMenu.ShouldBeOfType<TextInputContextMenu>();
        menu.ClipboardReader = () => clipboardContent;

        // Act
        menu.Show(0, 0);

        // Assert
        Item(menu, 5).IsEnabled.ShouldBe(expectedEnabled);
    }

    /// <summary>Verifies Opening enables Undo and Redo exactly when history is available.</summary>
    [Fact]
    public void Opening_WhenHistoryExists_EnablesUndoAndRedo()
    {
        // Arrange
        var control = new TextInput();
        var menu = control.ContextMenu.ShouldBeOfType<TextInputContextMenu>();
        menu.Show(0, 0);
        Item(menu, 0).IsEnabled.ShouldBeFalse();
        Item(menu, 1).IsEnabled.ShouldBeFalse();

        // Act
        control.Text = "typed";

        // Assert
        menu.Show(0, 0);
        Item(menu, 0).IsEnabled.ShouldBeTrue();
        control.Undo().ShouldBeTrue();
        menu.Show(0, 0);
        Item(menu, 1).IsEnabled.ShouldBeTrue();
    }

    /// <summary>Verifies invoking Cut removes the selection from the owning control and forwards
    /// the cut text to the clipboard writer.</summary>
    [Fact]
    public void Invoked_WhenCutIsActivated_RemovesSelectionAndWritesClipboard()
    {
        // Arrange
        var control = new TextInput { Text = "Hello World" };
        control.Select(0, 5);
        var menu = control.ContextMenu.ShouldBeOfType<TextInputContextMenu>();
        string? written = null;
        menu.ClipboardWriter = text => written = text;
        menu.Show(0, 0);

        // Act
        Item(menu, 3).PerformInvoke();

        // Assert
        control.Text.ShouldBe(" World");
        written.ShouldBe("Hello");
    }

    /// <summary>Verifies invoking Copy leaves text untouched and forwards the copied text to the
    /// clipboard writer.</summary>
    [Fact]
    public void Invoked_WhenCopyIsActivated_PreservesTextAndWritesClipboard()
    {
        // Arrange
        var control = new TextInput { Text = "Hello World" };
        control.Select(6, 5);
        var menu = control.ContextMenu.ShouldBeOfType<TextInputContextMenu>();
        string? written = null;
        menu.ClipboardWriter = text => written = text;
        menu.Show(0, 0);

        // Act
        Item(menu, 4).PerformInvoke();

        // Assert
        control.Text.ShouldBe("Hello World");
        written.ShouldBe("World");
    }

    /// <summary>Verifies invoking Cut with no selection does not invoke the clipboard writer,
    /// matching the documented empty-return no-write behavior.</summary>
    [Fact]
    public void Invoked_WhenCutIsActivatedWithoutSelection_DoesNotWriteClipboard()
    {
        // Arrange
        var control = new TextInput { Text = "Hello" };
        var menu = control.ContextMenu.ShouldBeOfType<TextInputContextMenu>();
        var writes = 0;
        menu.ClipboardWriter = _ => writes++;
        menu.Show(0, 0);

        // Act
        Item(menu, 3).PerformInvoke();

        // Assert
        writes.ShouldBe(0);
        control.Text.ShouldBe("Hello");
    }

    /// <summary>Verifies invoking Paste inserts the clipboard reader's content at the caret through
    /// the owning control's normal edit transaction.</summary>
    [Fact]
    public void Invoked_WhenPasteIsActivated_InsertsClipboardContentAtCaret()
    {
        // Arrange
        var control = new TextInput { Text = "AZ", CaretIndex = 1 };
        var menu = control.ContextMenu.ShouldBeOfType<TextInputContextMenu>();
        menu.ClipboardReader = () => "B";
        menu.Show(0, 0);

        // Act
        Item(menu, 5).PerformInvoke();

        // Assert
        control.Text.ShouldBe("ABZ");
    }

    /// <summary>Verifies invoking Select All selects the complete current text.</summary>
    [Fact]
    public void Invoked_WhenSelectAllIsActivated_SelectsEntireText()
    {
        // Arrange
        var control = new TextInput { Text = "abcdef" };
        var menu = control.ContextMenu.ShouldBeOfType<TextInputContextMenu>();
        menu.Show(0, 0);

        // Act
        Item(menu, 7).PerformInvoke();

        // Assert
        control.SelectionStart.ShouldBe(0);
        control.SelectionLength.ShouldBe(6);
    }

    /// <summary>Verifies invoking Undo and Redo forward to the owning control's own history.</summary>
    [Fact]
    public void Invoked_WhenUndoThenRedoAreActivated_RestoresOwningControlHistory()
    {
        // Arrange
        var control = new TextInput { Text = "A" };
        var menu = control.ContextMenu.ShouldBeOfType<TextInputContextMenu>();
        menu.Show(0, 0);

        // Act
        Item(menu, 0).PerformInvoke();

        // Assert
        control.Text.ShouldBeEmpty();

        // Act
        menu.Show(0, 0);
        Item(menu, 1).PerformInvoke();

        // Assert
        control.Text.ShouldBe("A");
    }

    /// <summary>Verifies disposing the context menu unsubscribes its item handlers so a later
    /// activation no longer forwards to the owning control.</summary>
    [Fact]
    public void Dispose_WhenCalled_StopsForwardingItemActivationToOwningControl()
    {
        // Arrange
        var control = new TextInput { Text = "abcdef" };
        control.Select(0, 3);
        var menu = control.ContextMenu.ShouldBeOfType<TextInputContextMenu>();
        var cut = Item(menu, 3);
        var undo = Item(menu, 0);

        // Act
        menu.Dispose();
        cut.PerformInvoke();
        undo.PerformInvoke();

        // Assert
        control.Text.ShouldBe("abcdef");
    }

    private static MenuItem Item(TextInputContextMenu menu, int index) =>
        menu.Items[index].ShouldBeOfType<MenuItem>();
}
