// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Dialogs;


using UiListView = ListView;

/// <summary>Defines retained composition and asynchronous state behavior for SaveFileDialog.</summary>
public sealed class SaveFileDialogTests
{
    /// <summary>Verifies construction copies configuration and composes one responsive dialog Window.</summary>
    [ComponentUnitEvidence(typeof(SaveFileDialog))]
    [Fact]
    public void Constructor_WhenConfigured_UsesCopiedOptionsAndSemanticControls()
    {
        // Arrange
        var initialDirectory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "save-construction"));
        var options = new SaveFileOptions
        {
            Title = "Export data",
            InitialDirectory = initialDirectory,
            InitialFileName = "report.csv",
            ConfirmOverwrite = false,
            ShowHidden = true,
            MaxVisibleRows = 7,
            Filters = [new FilePickerFilter("CSV", "*.csv"), FilePickerFilter.AllFiles],
            FilterIndex = 1
        };
        var source = new FakeFilePickerFileSystem();

        // Act
        var dialog = new SaveFileDialog(options, source);
        options.Title = "Changed";
        options.ShowHidden = false;

        // Assert
        var window = OwnedTree.Find<Window>(dialog).ShouldNotBeNull();
        var list = OwnedTree.Find<UiListView>(dialog).ShouldNotBeNull();
        var filter = OwnedTree.Find<ComboBox>(dialog).ShouldNotBeNull();
        var hidden = OwnedTree.Find<CheckBox>(dialog).ShouldNotBeNull();
        window.HeaderPlacement.ShouldBe(WindowTitlePlacement.Center);
        window.CanClose.ShouldBeTrue();
        window.CanMove.ShouldBeTrue();
        window.ShouldBeSameAs(dialog);
        window.Parent.ShouldBeNull();
        window.Header.ShouldBe("Export data");
        window.Width.ShouldBe(Length.Percent(80));
        window.Height.ShouldBe(Length.Percent(80));
        window.MaxWidth.ShouldBe(96);
        list.MaxHeight.ShouldBe(7);
        list.SelectionMode.ShouldBe(ListSelectionMode.Single);
        filter.SelectedIndex.ShouldBe(1);
        hidden.IsChecked.ShouldBe(true);
        dialog.CurrentDirectory.ShouldBe(initialDirectory);
        dialog.ShowHidden.ShouldBeTrue();
        dialog.FilterIndex.ShouldBe(1);
        dialog.FileName.ShouldBe("report.csv");
        dialog.IsLoading.ShouldBeFalse();
    }

    /// <summary>Verifies the dialog includes a filename input row between the list and status bar.</summary>
    [Fact]
    public void Constructor_WhenCreated_ContainsFileNameInput()
    {
        // Arrange and act
        var dialog = new SaveFileDialog(null, new FakeFilePickerFileSystem());
        var textInputs = OwnedTree.FindAll<TextInput>(dialog);

        // Assert
        textInputs.Count.ShouldBeGreaterThanOrEqualTo(2);
        textInputs.ShouldContain(static input => input.Placeholder == "File name");
    }

    /// <summary>Verifies the Save button is disabled when the filename input is empty.</summary>
    [Fact]
    public async Task SaveButton_WhenFileNameIsEmpty_IsDisabledAsync()
    {
        // Arrange
        var directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "save-empty"));
        var source = new FakeFilePickerFileSystem();
        source.AddDirectory(directory);
        var dialog = new SaveFileDialog(
            new SaveFileOptions { InitialDirectory = directory },
            source);

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            dialog,
            new Size(100, 40),
            TestContext.Current.CancellationToken);

        // Assert
        var saveButton = OwnedTree.FindAll<Button>(dialog)
            .First(static button => button.Content is ControlText text && text.Content.Contains("Save"));
        saveButton.IsEnabled.ShouldBeFalse();
    }

    /// <summary>Verifies the Save button is enabled when the filename input is non-empty.</summary>
    [Fact]
    public async Task SaveButton_WhenFileNameIsProvided_IsEnabledAsync()
    {
        // Arrange
        var directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "save-enabled"));
        var source = new FakeFilePickerFileSystem();
        source.AddDirectory(directory);
        var dialog = new SaveFileDialog(
            new SaveFileOptions { InitialDirectory = directory, InitialFileName = "test.txt" },
            source);

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            dialog,
            new Size(100, 40),
            TestContext.Current.CancellationToken);

        // Assert
        var saveButton = OwnedTree.FindAll<Button>(dialog)
            .First(static button => button.Content is ControlText text && text.Content.Contains("Save"));
        saveButton.IsEnabled.ShouldBeTrue();
    }

    /// <summary>Verifies the Save button becomes enabled after typing a filename.</summary>
    [Fact]
    public async Task SaveButton_WhenFileNameIsTyped_BecomesEnabledAsync()
    {
        // Arrange
        var directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "save-typed"));
        var source = new FakeFilePickerFileSystem();
        source.AddDirectory(directory);
        var dialog = new SaveFileDialog(
            new SaveFileOptions { InitialDirectory = directory },
            source);
        await using var surface = await ComponentSurface.MountAsync(
            dialog,
            new Size(100, 40),
            TestContext.Current.CancellationToken);
        var fileNameInput = OwnedTree.FindAll<TextInput>(dialog)
            .First(static input => input.Placeholder == "File name");
        var saveButton = OwnedTree.FindAll<Button>(dialog)
            .First(static button => button.Content is ControlText text && text.Content.Contains("Save"));

        // Act
        saveButton.IsEnabled.ShouldBeFalse();
        await surface.UpdateAsync(() => fileNameInput.Text = "output.txt", "type filename");

        // Assert
        saveButton.IsEnabled.ShouldBeTrue();
        dialog.FileName.ShouldBe("output.txt");
    }

    /// <summary>Verifies selecting a file in the list populates the filename input.</summary>
    [Fact]
    public async Task Selection_WhenFileIsSelected_PopulatesFileNameAsync()
    {
        // Arrange
        var directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "save-select"));
        var source = new FakeFilePickerFileSystem();
        source.AddDirectory(
            directory,
            new FilePickerEntry("docs", Path.Combine(directory, "docs"), true, false),
            new FilePickerEntry("readme.md", Path.Combine(directory, "readme.md"), false, false));
        var dialog = new SaveFileDialog(
            new SaveFileOptions { InitialDirectory = directory },
            source);
        await using var surface = await ComponentSurface.MountAsync(
            dialog,
            new Size(100, 40),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(static () => { }, "settle load");
        var list = OwnedTree.Find<UiListView>(dialog).ShouldNotBeNull();
        var fileNameInput = OwnedTree.FindAll<TextInput>(dialog)
            .First(static input => input.Placeholder == "File name");

        // Act
        await surface.UpdateAsync(() => list.SelectedIndex = 1, "select file");

        // Assert
        fileNameInput.Text.ShouldBe("readme.md");
    }

    /// <summary>Verifies selecting a file whose name contains a control character - legal filesystem
    /// data on POSIX - degrades to a status message instead of force-stopping the application when
    /// the unrepresentable name reaches TextInput.Text (see #219).</summary>
    [Fact]
    public async Task Selection_WhenFileNameHasControlCharacter_DoesNotForceStopAsync()
    {
        // Arrange
        var directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "save-select-control"));
        var fileName = "re" + '\u001b' + "port.txt";
        var source = new FakeFilePickerFileSystem();
        source.AddDirectory(
            directory,
            new FilePickerEntry(fileName, Path.Combine(directory, fileName), false, false));
        var dialog = new SaveFileDialog(
            new SaveFileOptions { InitialDirectory = directory },
            source);
        await using var surface = await ComponentSurface.MountAsync(
            dialog,
            new Size(100, 40),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(static () => { }, "settle load");
        var list = OwnedTree.Find<UiListView>(dialog).ShouldNotBeNull();
        var fileNameInput = OwnedTree.FindAll<TextInput>(dialog)
            .First(static input => input.Placeholder == "File name");

        // Act
        await surface.UpdateAsync(() => list.SelectedIndex = 0, "select control-character file");

        // Assert
        dialog.IsDisposed.ShouldBeFalse();
        fileNameInput.Text.ShouldBeEmpty();
        dialog.Status.ShouldBe("Cannot display this file's name.");
    }

    /// <summary>Verifies selecting a directory does not populate the filename input.</summary>
    [Fact]
    public async Task Selection_WhenDirectoryIsSelected_DoesNotPopulateFileNameAsync()
    {
        // Arrange
        var directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "save-dir-select"));
        var source = new FakeFilePickerFileSystem();
        source.AddDirectory(
            directory,
            new FilePickerEntry("docs", Path.Combine(directory, "docs"), true, false),
            new FilePickerEntry("readme.md", Path.Combine(directory, "readme.md"), false, false));
        var dialog = new SaveFileDialog(
            new SaveFileOptions { InitialDirectory = directory },
            source);
        await using var surface = await ComponentSurface.MountAsync(
            dialog,
            new Size(100, 40),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(static () => { }, "settle load");
        var list = OwnedTree.Find<UiListView>(dialog).ShouldNotBeNull();
        var fileNameInput = OwnedTree.FindAll<TextInput>(dialog)
            .First(static input => input.Placeholder == "File name");

        // Act
        await surface.UpdateAsync(() => list.SelectedIndex = 0, "select directory");

        // Assert
        fileNameInput.Text.ShouldBe(string.Empty);
    }

    /// <summary>Verifies invoking a file with the pointer updates the filename and attempts the save
    /// exactly like invoking it with the keyboard, instead of only updating the filename and leaving
    /// the dialog open (see #227).</summary>
    [Fact]
    public async Task Selection_WhenFileIsInvokedWithPointer_AttemptsSaveAsync()
    {
        // Arrange
        var directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "save-pointer-accept"));
        var existingPath = Path.Combine(directory, "existing.txt");
        var source = new FakeFilePickerFileSystem();
        source.AddDirectory(directory, new FilePickerEntry("existing.txt", existingPath, false, false));
        var dialog = new SaveFileDialog(
            new SaveFileOptions { InitialDirectory = directory, ConfirmOverwrite = false },
            source);
        await using var surface = await ComponentSurface.MountAsync(
            dialog,
            new Size(100, 40),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(static () => { }, "settle load");
        var list = OwnedTree.Find<UiListView>(dialog).ShouldNotBeNull();

        // Act
        await surface.UpdateAsync(
            () =>
            {
                list.SelectedIndex = 0;
                list.ActivateCurrent(ActivationCause.Pointer, null, Modifiers.None).ShouldBeTrue();
            },
            "invoke file with pointer");

        // Assert
        dialog.HasSelectedResult.ShouldBeTrue();
        var result = dialog.SelectedResult.ShouldNotBeNull();
        result.Confirmed.ShouldBeTrue();
        result.Path.ShouldBe(existingPath);
    }

    /// <summary>Verifies the dialog loads a directory and publishes file count on attachment.</summary>
    [Fact]
    public async Task OnAttached_WhenDirectoryLoads_PublishesCommittedSnapshotAsync()
    {
        // Arrange
        var directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "save-load"));
        var source = new FakeFilePickerFileSystem();
        source.AddDirectory(
            directory,
            new FilePickerEntry("src", Path.Combine(directory, "src"), true, false),
            new FilePickerEntry("Program.cs", Path.Combine(directory, "Program.cs"), false, false));
        var dialog = new SaveFileDialog(
            new SaveFileOptions { InitialDirectory = directory },
            source);

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            dialog,
            new Size(100, 40),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(static () => { }, "settle save dialog load");

        // Assert
        dialog.IsLoading.ShouldBeFalse();
        dialog.CurrentDirectory.ShouldBe(directory);
        dialog.Status.ShouldBe("1 folder · 1 file");
        OwnedTree.Find<UiListView>(dialog).ShouldNotBeNull().Items.Count.ShouldBe(2);
    }

    /// <summary>Verifies the save completes without confirmation when the file does not exist.</summary>
    [ComponentBehaviorEvidence(
        typeof(SaveFileDialog),
        ComponentBehavior.Mounted |
        ComponentBehavior.Hover |
        ComponentBehavior.Focus |
        ComponentBehavior.Tab |
        ComponentBehavior.DirectionalExcluded |
        ComponentBehavior.PressReleaseExcluded |
        ComponentBehavior.Activation |
        ComponentBehavior.KeyboardActivation |
        ComponentBehavior.UnavailableCleanup |
        ComponentBehavior.Transient |
        ComponentBehavior.Composition)]
    [Fact]
    public async Task Save_WhenFileDoesNotExist_CompletesWithPathAsync()
    {
        // Arrange
        var directory = Path.Combine(Path.GetTempPath(), $"save-new-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(directory);

        try
        {
            var opener = new Button { Content = new ControlText("Save") };
            var host = new Overlay { Children = { opener } };
            await using var surface = await ComponentSurface.MountAsync(
                host,
                new Size(100, 40),
                TestContext.Current.CancellationToken);
            Task<SaveFileResult>? pending = null;

            // Act
            await surface.UpdateAsync(
                () => pending = SaveFileDialog.ShowAsync(
                    opener,
                    new SaveFileOptions
                    {
                        InitialDirectory = directory,
                        InitialFileName = "newfile.txt",
                        ConfirmOverwrite = true
                    }),
                "show save dialog");
            var dialog = OwnedTree.Find<SaveFileDialog>(surface.Application.Root).ShouldNotBeNull();
            await WaitUntilAsync(surface, () => !dialog.IsLoading);
            await surface.Keyboard.PressAsync(Code.Enter);

            // Assert
            var result = await pending!;
            result.Confirmed.ShouldBeTrue();
            result.Path.ShouldBe(Path.Combine(directory, "newfile.txt"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Verifies the cancel action returns a null path.</summary>
    [Fact]
    public async Task Cancel_WhenPressed_ReturnsCancelledResultAsync()
    {
        // Arrange
        var directory = Path.Combine(Path.GetTempPath(), $"save-cancel-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(directory);

        try
        {
            var opener = new Button { Content = new ControlText("Save") };
            var host = new Overlay { Children = { opener } };
            await using var surface = await ComponentSurface.MountAsync(
                host,
                new Size(100, 40),
                TestContext.Current.CancellationToken);
            Task<SaveFileResult>? pending = null;

            // Act
            await surface.UpdateAsync(
                () => pending = SaveFileDialog.ShowAsync(
                    opener,
                    new SaveFileOptions
                    {
                        InitialDirectory = directory,
                        InitialFileName = "file.txt"
                    }),
                "show save dialog");
            var dialog = OwnedTree.Find<SaveFileDialog>(surface.Application.Root).ShouldNotBeNull();
            await WaitUntilAsync(surface, () => !dialog.IsLoading);
            await surface.Keyboard.PressAsync(Code.Escape);

            // Assert
            var result = await pending!;
            result.Confirmed.ShouldBeFalse();
            result.Path.ShouldBeNull();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Verifies the overwrite confirmation is skipped when ConfirmOverwrite is false.</summary>
    [Fact]
    public async Task Save_WhenConfirmOverwriteIsFalse_SkipsConfirmationAsync()
    {
        // Arrange
        var directory = Path.Combine(Path.GetTempPath(), $"save-no-confirm-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(directory);

        try
        {
            var existingPath = Path.Combine(directory, "existing.txt");
            await File.WriteAllTextAsync(existingPath, "content", TestContext.Current.CancellationToken);
            var opener = new Button { Content = new ControlText("Save") };
            var host = new Overlay { Children = { opener } };
            await using var surface = await ComponentSurface.MountAsync(
                host,
                new Size(100, 40),
                TestContext.Current.CancellationToken);
            Task<SaveFileResult>? pending = null;

            // Act
            await surface.UpdateAsync(
                () => pending = SaveFileDialog.ShowAsync(
                    opener,
                    new SaveFileOptions
                    {
                        InitialDirectory = directory,
                        InitialFileName = "existing.txt",
                        ConfirmOverwrite = false
                    }),
                "show save dialog");
            var dialog = OwnedTree.Find<SaveFileDialog>(surface.Application.Root).ShouldNotBeNull();
            await WaitUntilAsync(surface, () => !dialog.IsLoading);
            await surface.Keyboard.PressAsync(Code.Enter);

            // Assert
            var result = await pending!;
            result.Confirmed.ShouldBeTrue();
            result.Path.ShouldBe(existingPath);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Verifies the overwrite confirmation appears when the file exists and ConfirmOverwrite is true.</summary>
    [Fact]
    public async Task Save_WhenFileExistsAndConfirmOverwrite_ShowsConfirmationAsync()
    {
        // Arrange — use a real temporary directory so the dialog can load and FileExists works.
        var directory = Path.Combine(Path.GetTempPath(), $"save-confirm-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(directory);

        try
        {
            var existingPath = Path.Combine(directory, "existing.txt");
            await File.WriteAllTextAsync(existingPath, "content", TestContext.Current.CancellationToken);
            var opener = new Button { Content = new ControlText("Save") };
            var host = new Overlay { Children = { opener } };
            await using var surface = await ComponentSurface.MountAsync(
                host,
                new Size(100, 40),
                TestContext.Current.CancellationToken);
            Task<SaveFileResult>? pending = null;

            // Act
            await surface.UpdateAsync(
                () => pending = SaveFileDialog.ShowAsync(
                    opener,
                    new SaveFileOptions
                    {
                        InitialDirectory = directory,
                        InitialFileName = "existing.txt",
                        ConfirmOverwrite = true
                    }),
                "show save dialog");
            var dialog = OwnedTree.Find<SaveFileDialog>(surface.Application.Root).ShouldNotBeNull();
            await WaitUntilAsync(surface, () => !dialog.IsLoading);

            // Trigger save — this should show a MessageBox confirmation since the file exists.
            await surface.Keyboard.PressAsync(Code.Enter);
            await surface.UpdateAsync(static () => { }, "settle confirmation dialog");

            // Confirm overwrite by pressing Enter on the MessageBox's Yes button.
            await surface.Keyboard.PressAsync(Code.Enter);

            // Assert
            var result = await pending!;
            result.Confirmed.ShouldBeTrue();
            result.Path.ShouldBe(existingPath);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Verifies every owned-part style defaults to null and its resolved value follows the
    /// owned part's own semantic profile until an explicit local style is assigned.</summary>
    [Fact]
    public void OwnedPartStyles_WhenUnset_FollowTheOwnedPartsOwnSemanticProfile()
    {
        using var dialog = new SaveFileDialog(null, new FakeFilePickerFileSystem());
        using var expectedButton = new Button();
        using var expectedCheckBox = new CheckBox();

        dialog.CancelButtonStyle.ShouldBeNull();
        dialog.ShowHiddenCheckBoxStyle.ShouldBeNull();
        dialog.FileListScrollBarStyle.ShouldBeNull();
        dialog.FilterScrollBarStyle.ShouldBeNull();
        dialog.SaveButtonStyle.ShouldBeNull();
        dialog.ActualCancelButtonStyle.ShouldBe(expectedButton.ActualStyle);
        dialog.ActualShowHiddenCheckBoxStyle.ShouldBe(expectedCheckBox.ActualStyle);
        dialog.ActualSaveButtonStyle.ShouldBe(expectedButton.ActualStyle);
    }

    /// <summary>Verifies each owned-part style propagates to its owned control and resolves back
    /// through the dialog's own Actual* property.</summary>
    [Fact]
    public void OwnedPartStyles_WhenSet_PropagateToTheOwnedPart()
    {
        using var dialog = new SaveFileDialog(null, new FakeFilePickerFileSystem());
        var buttonStyle = new ButtonStyle(
            new Thickness(horizontal: 2, vertical: 1),
            ButtonStyle.Standard.Appearance);
        var checkBoxStyle = CheckBoxStyle.Default;
        var scrollBarStyle = ScrollBarStyle.Default;

        dialog.CancelButtonStyle = buttonStyle;
        dialog.ShowHiddenCheckBoxStyle = checkBoxStyle;
        dialog.FileListScrollBarStyle = scrollBarStyle;
        dialog.FilterScrollBarStyle = scrollBarStyle;
        dialog.SaveButtonStyle = buttonStyle;

        dialog.ActualCancelButtonStyle.ShouldBe(buttonStyle);
        dialog.ActualShowHiddenCheckBoxStyle.ShouldBe(checkBoxStyle);
        dialog.ActualFileListScrollBarStyle.ShouldBe(scrollBarStyle);
        dialog.ActualFilterScrollBarStyle.ShouldBe(scrollBarStyle);
        dialog.ActualSaveButtonStyle.ShouldBe(buttonStyle);

        var list = OwnedTree.Find<UiListView>(dialog).ShouldNotBeNull();
        var filter = OwnedTree.Find<ComboBox>(dialog).ShouldNotBeNull();
        var hidden = OwnedTree.Find<CheckBox>(dialog).ShouldNotBeNull();
        list.ScrollBarStyle.ShouldBe(scrollBarStyle);
        filter.ScrollBarStyle.ShouldBe(scrollBarStyle);
        hidden.Style.ShouldBe(checkBoxStyle);
    }

    /// <summary>Verifies SaveFileOptions carries every owned-part style through construction and
    /// through Copy(), matching how ShowAsync's copied snapshot reaches the constructed dialog.</summary>
    [Fact]
    public void Constructor_WhenOptionsCarryStyles_AppliesThemToTheConstructedDialog()
    {
        var buttonStyle = new ButtonStyle(
            new Thickness(horizontal: 1, vertical: 0),
            ButtonStyle.Standard.Appearance);
        var checkBoxStyle = CheckBoxStyle.Default;
        var scrollBarStyle = ScrollBarStyle.Default;
        var options = new SaveFileOptions
        {
            CancelButtonStyle = buttonStyle,
            ShowHiddenCheckBoxStyle = checkBoxStyle,
            FileListScrollBarStyle = scrollBarStyle,
            FilterScrollBarStyle = scrollBarStyle,
            SaveButtonStyle = buttonStyle
        };
        var copy = options.Copy();

        using var dialog = new SaveFileDialog(options, new FakeFilePickerFileSystem());

        copy.CancelButtonStyle.ShouldBe(buttonStyle);
        copy.ShowHiddenCheckBoxStyle.ShouldBe(checkBoxStyle);
        copy.FileListScrollBarStyle.ShouldBe(scrollBarStyle);
        copy.FilterScrollBarStyle.ShouldBe(scrollBarStyle);
        copy.SaveButtonStyle.ShouldBe(buttonStyle);
        dialog.ActualCancelButtonStyle.ShouldBe(buttonStyle);
        dialog.ActualShowHiddenCheckBoxStyle.ShouldBe(checkBoxStyle);
        dialog.ActualFileListScrollBarStyle.ShouldBe(scrollBarStyle);
        dialog.ActualFilterScrollBarStyle.ShouldBe(scrollBarStyle);
        dialog.ActualSaveButtonStyle.ShouldBe(buttonStyle);
    }

    /// <summary>Verifies the overwrite-confirmation MessageBox's Yes/No actions inherit the dialog's
    /// SaveButtonStyle, since this dialog-generated child has no persistent instance of its own to
    /// expose a separate style surface through.</summary>
    [Fact]
    public async Task Save_WhenFileExistsAndSaveButtonStyleIsSet_AppliesItToTheConfirmationActionsAsync()
    {
        // Arrange — use a real temporary directory so the dialog can load and FileExists works.
        var directory = Path.Combine(Path.GetTempPath(), $"save-confirm-style-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(directory);

        try
        {
            var existingPath = Path.Combine(directory, "existing.txt");
            await File.WriteAllTextAsync(existingPath, "content", TestContext.Current.CancellationToken);
            var opener = new Button { Content = new ControlText("Save") };
            var host = new Overlay { Children = { opener } };
            await using var surface = await ComponentSurface.MountAsync(
                host,
                new Size(100, 40),
                TestContext.Current.CancellationToken);
            var style = new ButtonStyle(
                new Thickness(horizontal: 2, vertical: 1),
                ButtonStyle.Standard.Appearance);
            Task<SaveFileResult>? pending = null;

            // Act
            await surface.UpdateAsync(
                () => pending = SaveFileDialog.ShowAsync(
                    opener,
                    new SaveFileOptions
                    {
                        InitialDirectory = directory,
                        InitialFileName = "existing.txt",
                        ConfirmOverwrite = true,
                        SaveButtonStyle = style
                    }),
                "show save dialog with an explicit Save Button style");
            var dialog = OwnedTree.Find<SaveFileDialog>(surface.Application.Root).ShouldNotBeNull();
            await WaitUntilAsync(surface, () => !dialog.IsLoading);

            // Trigger save — this should show a MessageBox confirmation since the file exists.
            await surface.Keyboard.PressAsync(Code.Enter);
            await surface.UpdateAsync(static () => { }, "settle confirmation dialog");

            // Assert
            var confirmation = OwnedTree.Find<MessageBox>(surface.Application.Root).ShouldNotBeNull();
            confirmation.ActualButtonStyle.ShouldBe(style);

            // Confirm overwrite by pressing Enter on the MessageBox's Yes button.
            await surface.Keyboard.PressAsync(Code.Enter);
            var result = await pending!;
            result.Confirmed.ShouldBeTrue();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static async Task WaitUntilAsync(ComponentSurface surface, Func<bool> predicate)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            if (await surface.Application.Dispatcher.InvokeAsync(predicate, TestContext.Current.CancellationToken))
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(5), TestContext.Current.CancellationToken);
        }

        (await surface.Application.Dispatcher.InvokeAsync(predicate, TestContext.Current.CancellationToken))
            .ShouldBeTrue("the save-file dialog operation should settle within 500ms");
    }
}
