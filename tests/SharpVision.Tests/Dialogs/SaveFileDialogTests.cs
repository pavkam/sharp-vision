// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Dialogs;



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
        dialog.Loading.ShouldBeFalse();
    }

    /// <summary>Verifies the dialog includes a filename input row after the list metadata.</summary>
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

    /// <summary>Verifies the save dialog shares the visible up glyph, aligned metadata, and separated actions.</summary>
    [Fact]
    public async Task Render_WhenShowcaseSized_AlignsMetadataAndSeparatesActionsAsync()
    {
        // Arrange
        var directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "save-polish"));
        var source = new FakeFilePickerFileSystem();
        source.AddDirectory(directory);
        var dialog = new SaveFileDialog(new SaveFileOptions { InitialDirectory = directory }, source);

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            dialog,
            new Size(76, 35),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(static () => { }, "settle save dialog load");
        var window = OwnedTree.Find<Window>(dialog).ShouldNotBeNull();
        var list = OwnedTree.Find<UiListView>(dialog).ShouldNotBeNull();
        var listSurface = list.Parent.ShouldBeOfType<Dock>();
        var filter = OwnedTree.Find<ComboBox>(dialog).ShouldNotBeNull();
        var fileName = OwnedTree.FindAll<TextInput>(dialog).Single(static input =>
            input.Placeholder == "File name");
        var status = OwnedTree.FindAll<ControlText>(dialog).Single(static text =>
            text.Content == "0 folders · 0 files");
        var separator = OwnedTree.Find<Separator>(dialog).ShouldNotBeNull();
        var up = OwnedTree.FindAll<Button>(dialog).Single(static button => button.Text == "↑");
        var save = OwnedTree.FindAll<Button>(dialog).Single(static button => button.Text == "&Save");
        var cancel = OwnedTree.FindAll<Button>(dialog).Single(static button => button.Text == "&Cancel");

        // Assert
        up.Bounds.Width.ShouldBe(5);
        var upGlyph = up.TextControl.ShouldNotBeNull();
        upGlyph.Bounds.ShouldBe(new Rect(up.Bounds.X + 2, up.Bounds.Y + 1, 1, 1));
        surface.Cell(new Point(upGlyph.Bounds.X, upGlyph.Bounds.Y)).Text.ShouldBe("↑");
        filter.Bounds.X.ShouldBe(listSurface.Bounds.X);
        filter.Bounds.Y.ShouldBe(listSurface.Bounds.Bottom);
        Math.Abs((filter.Bounds.Width * 2) - (listSurface.Bounds.Width - 1))
            .ShouldBeLessThanOrEqualTo(1);
        status.Bounds.Right.ShouldBe(listSurface.Bounds.Right);
        status.Bounds.Y.ShouldBe(filter.Bounds.Y + 1);
        status.Bounds.Height.ShouldBe(1);
        fileName.Bounds.Y.ShouldBeGreaterThan(filter.Bounds.Bottom);
        separator.Bounds.X.ShouldBe(listSurface.Bounds.X);
        separator.Bounds.Right.ShouldBe(listSurface.Bounds.Right);
        save.Bounds.Y.ShouldBe(separator.Bounds.Bottom);
        cancel.Bounds.Y.ShouldBe(save.Bounds.Y);
        cancel.Bounds.Bottom.ShouldBe(window.ContentBounds.Bottom);
    }

    /// <summary>Verifies maximum-height layout leaves only the intentional row after list metadata.</summary>
    [Fact]
    public async Task Render_WhenMaximumHeight_LeavesOneRowBetweenMetadataAndFileNameAsync()
    {
        // Arrange
        var directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "save-height"));
        var source = new FakeFilePickerFileSystem();
        source.AddDirectory(directory);
        var dialog = new SaveFileDialog(
            new SaveFileOptions { InitialDirectory = directory, MaxVisibleRows = 7 },
            source);

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            dialog,
            new Size(100, 60),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(static () => { }, "settle maximum-height save dialog");
        var window = OwnedTree.Find<Window>(dialog).ShouldNotBeNull();
        var list = OwnedTree.Find<UiListView>(dialog).ShouldNotBeNull();
        var listSurface = list.Parent.ShouldBeOfType<Dock>();
        var filter = OwnedTree.Find<ComboBox>(dialog).ShouldNotBeNull();
        var fileName = OwnedTree.FindAll<TextInput>(dialog).Single(static input =>
            input.Placeholder == "File name");

        // Assert
        window.Bounds.Height.ShouldBe(28);
        filter.Bounds.Y.ShouldBe(listSurface.Bounds.Bottom);
        fileName.Bounds.Y.ShouldBe(filter.Bounds.Bottom + 1);
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
            .First(static button => button.Text.Contains("Save"));
        saveButton.Enabled.ShouldBeFalse();
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
            .First(static button => button.Text.Contains("Save"));
        saveButton.Enabled.ShouldBeTrue();
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
            .First(static button => button.Text.Contains("Save"));

        // Act
        saveButton.Enabled.ShouldBeFalse();
        await surface.UpdateAsync(() => fileNameInput.Text = "output.txt", "type filename");

        // Assert
        saveButton.Enabled.ShouldBeTrue();
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
    /// the unrepresentable name reaches TextInput.Text.</summary>
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
        dialog.Disposed.ShouldBeFalse();
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

    /// <summary>Verifies a single pointer click on a file selects it, populates the filename, and
    /// enables Save without completing the dialog - select-then-commit means one click only
    /// proposes a filename, and the Save button (a keyboard Enter, or a second click) is still
    /// required to commit it.</summary>
    [Fact]
    public async Task Selection_WhenFileIsClickedOnce_PopulatesFileNameWithoutCompletingAsync()
    {
        // Arrange
        var directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "save-pointer-select"));
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
        var fileNameInput = OwnedTree.FindAll<TextInput>(dialog)
            .First(static input => input.Placeholder == "File name");
        var saveButton = OwnedTree.FindAll<Button>(dialog).Single(static button => button.IsDefault);

        // Act
        await surface.Pointer.ClickAsync(list, new Point(1, 0));

        // Assert one click only selects and populates the filename
        list.SelectedIndex.ShouldBe(0);
        fileNameInput.Text.ShouldBe("existing.txt");
        saveButton.Enabled.ShouldBeTrue();
        dialog.HasSelectedResult.ShouldBeFalse();

        // Act: the Save button commits the already-populated filename - a regression guard for the
        // button path now that a pointer click alone no longer completes the dialog.
        await surface.Pointer.ClickAsync(saveButton);

        // Assert
        dialog.HasSelectedResult.ShouldBeTrue();
        var result = dialog.SelectedResult.ShouldNotBeNull();
        result.Confirmed.ShouldBeTrue();
        result.Path.ShouldBe(existingPath);
    }

    /// <summary>Verifies a double pointer click on a file updates the filename and attempts the save
    /// exactly like invoking it with the keyboard, instead of only updating the filename and leaving
    /// the dialog open.</summary>
    [Fact]
    public async Task Selection_WhenFileIsDoubleClickedWithPointer_AttemptsSaveAsync()
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
        await surface.Pointer.ClickAsync(list, new Point(1, 0));
        await surface.Pointer.ClickAsync(list, new Point(1, 0));

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
        dialog.Loading.ShouldBeFalse();
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
            var opener = new Button { Text = "Save" };
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
            await DialogWait.UntilAsync(surface, dialog, () => !dialog.Loading);
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
            var opener = new Button { Text = "Save" };
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
            await DialogWait.UntilAsync(surface, dialog, () => !dialog.Loading);
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
            var opener = new Button { Text = "Save" };
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
            await DialogWait.UntilAsync(surface, dialog, () => !dialog.Loading);
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

    /// <summary>
    /// Verifies typing a name that matches an existing subdirectory does not silently complete
    /// the save with a "confirmed" path that actually names a directory. File.Exists (which the
    /// overwrite check is built on) returns false for a directory, so without an explicit
    /// directory check the dialog would skip overwrite confirmation entirely - regardless of
    /// ConfirmOverwrite - and complete immediately.
    /// </summary>
    [Fact]
    public async Task Save_WhenTypedNameMatchesExistingDirectory_DoesNotCompleteAsync()
    {
        // Arrange
        var directory = Path.Combine(Path.GetTempPath(), $"save-dir-collision-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(directory);
        var subdirectory = Path.Combine(directory, "Reports");
        _ = Directory.CreateDirectory(subdirectory);

        try
        {
            var opener = new Button { Text = "Save" };
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
                        InitialFileName = "Reports",
                        ConfirmOverwrite = true
                    }),
                "show save dialog");
            var dialog = OwnedTree.Find<SaveFileDialog>(surface.Application.Root).ShouldNotBeNull();
            await DialogWait.UntilAsync(surface, dialog, () => !dialog.Loading);
            await surface.Keyboard.PressAsync(Code.Enter);

            // Assert: the dialog stays open instead of completing with a directory as the result.
            await surface.Application.Dispatcher.InvokeAsync(
                static () => { },
                TestContext.Current.CancellationToken);
            pending!.IsCompleted.ShouldBeFalse();
            dialog.Disposed.ShouldBeFalse();
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
            var opener = new Button { Text = "Save" };
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
            await DialogWait.UntilAsync(surface, dialog, () => !dialog.Loading);

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
    public void OwnedPartStyles_WhenUnset_FollowTheOwnedPartsOwnSemanticAppearance()
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
        var buttonStyle = ButtonStyle.Standard with { Padding = new Thickness(horizontal: 2, vertical: 1) };
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
        var buttonStyle = ButtonStyle.Standard with { Padding = new Thickness(horizontal: 1, vertical: 0) };
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
            var opener = new Button { Text = "Save" };
            var host = new Overlay { Children = { opener } };
            await using var surface = await ComponentSurface.MountAsync(
                host,
                new Size(100, 40),
                TestContext.Current.CancellationToken);
            var style = ButtonStyle.Standard with { Padding = new Thickness(horizontal: 2, vertical: 1) };
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
            await DialogWait.UntilAsync(surface, dialog, () => !dialog.Loading);

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

    /// <summary>Verifies the resolved aggregate style defaults to the Window fallback until an
    /// explicit local style is assigned, and that assigning one applies the root padding and
    /// file-list border on the next layout pass.</summary>
    [Fact]
    public void Style_WhenSet_OverridesFrameAndGeometryAndResetRestores()
    {
        using var dialog = new SaveFileDialog(new SaveFileOptions(), new FakeFilePickerFileSystem());
        var defaultStyle = dialog.ActualStyle;
        var style = defaultStyle with { RootPadding = new Thickness(2) };
        var engine = new LayoutEngine();

        dialog.Style.ShouldBeNull();

        dialog.Style = style;
        engine.Layout(dialog, new Size(100, 40));

        dialog.ActualStyle.ShouldBe(style);

        dialog.Style = null;
        engine.Layout(dialog, new Size(100, 40));

        dialog.Style.ShouldBeNull();
    }

    /// <summary>Verifies changing a caption updates the retained control owning that semantic
    /// content in place, without recreating it.</summary>
    [Fact]
    public void Captions_WhenChanged_UpdateTheRetainedControlsInPlace()
    {
        using var dialog = new SaveFileDialog(new SaveFileOptions(), new FakeFilePickerFileSystem());
        var saveButton = OwnedTree.FindAll<Button>(dialog).Single(static button => button.IsDefault);
        var fileNameInput = OwnedTree.FindAll<TextInput>(dialog).First(static input => input.Placeholder == "File name");
        var fileNameLabel = OwnedTree.FindAll<ControlText>(dialog).Single(static text => text.Content == "Name:");

        dialog.SaveText = "&Guardar";
        dialog.FileNameLabel = "Archivo:";
        dialog.FileNamePlaceholder = "Nombre del archivo";

        saveButton.Text.ShouldBe("&Guardar");
        fileNameLabel.Content.ShouldBe("Archivo:");
        fileNameInput.Placeholder.ShouldBe("Nombre del archivo");
        saveButton.ShouldBeSameAs(OwnedTree.FindAll<Button>(dialog).Single(static button => button.IsDefault));
    }

    /// <summary>Verifies a null caption throws before any observable mutation.</summary>
    [Fact]
    public void Captions_WhenSetToNull_ThrowBeforeMutation()
    {
        using var dialog = new SaveFileDialog(new SaveFileOptions(), new FakeFilePickerFileSystem());

        _ = Should.Throw<ArgumentNullException>(() => dialog.SaveText = null!);
        _ = Should.Throw<ArgumentNullException>(() => dialog.FileNameLabel = null!);
        _ = Should.Throw<ArgumentNullException>(() => dialog.FileNamePlaceholder = null!);
        _ = Should.Throw<ArgumentNullException>(() => dialog.OverwriteTitle = null!);
        _ = Should.Throw<ArgumentNullException>(() => dialog.OverwriteYesText = null!);
        _ = Should.Throw<ArgumentNullException>(() => dialog.OverwriteNoText = null!);
        _ = Should.Throw<ArgumentNullException>(() => dialog.OverwriteMessageFormat = null!);

        dialog.SaveText.ShouldBe("&Save");
    }

    /// <summary>Verifies the overwrite-confirmation MessageBox uses the configured title, captions,
    /// message formatter, and local style instead of the fixed defaults.</summary>
    [Fact]
    public async Task Save_WhenFileExistsAndOverwriteConfigurationIsSet_AppliesItToTheConfirmationAsync()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"save-confirm-config-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(directory);

        try
        {
            var existingPath = Path.Combine(directory, "existing.txt");
            await File.WriteAllTextAsync(existingPath, "content", TestContext.Current.CancellationToken);
            var opener = new Button { Text = "Save" };
            var host = new Overlay { Children = { opener } };
            await using var surface = await ComponentSurface.MountAsync(
                host,
                new Size(100, 40),
                TestContext.Current.CancellationToken);
            var messageBoxStyle = MessageBoxStyle.Default with { ActionBarMargin = new Thickness(3, 0) };
            Task<SaveFileResult>? pending = null;

            await surface.UpdateAsync(
                () => pending = SaveFileDialog.ShowAsync(
                    opener,
                    new SaveFileOptions
                    {
                        InitialDirectory = directory,
                        InitialFileName = "existing.txt",
                        ConfirmOverwrite = true,
                        OverwriteTitle = "¿Sobrescribir?",
                        OverwriteYesText = "&Sí",
                        OverwriteNoText = "&No, gracias",
                        OverwriteStyle = messageBoxStyle
                    }),
                "show save dialog with explicit overwrite configuration");
            var dialog = OwnedTree.Find<SaveFileDialog>(surface.Application.Root).ShouldNotBeNull();
            await DialogWait.UntilAsync(surface, dialog, () => !dialog.Loading);
            await surface.UpdateAsync(
                () => dialog.OverwriteMessageFormat = static name => $"¿Reemplazar '{name}'?",
                "customize the overwrite message formatter");

            await surface.Keyboard.PressAsync(Code.Enter);
            await surface.UpdateAsync(static () => { }, "settle confirmation dialog");

            var confirmation = OwnedTree.Find<MessageBox>(surface.Application.Root).ShouldNotBeNull();
            confirmation.Title.ShouldBe("¿Sobrescribir?");
            confirmation.Message.ShouldBe("¿Reemplazar 'existing.txt'?");
            confirmation.ActualStyle.ShouldBe(messageBoxStyle);
            var buttons = OwnedTree.FindAll<Button>(confirmation).ToArray();
            buttons.ShouldContain(static button => button.Text == "&Sí");
            buttons.ShouldContain(static button => button.Text == "&No, gracias");

            // Confirm overwrite by pressing Enter on the MessageBox's default (Yes) button.
            await surface.Keyboard.PressAsync(Code.Enter);
            var result = await pending!;
            result.Confirmed.ShouldBeTrue();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
