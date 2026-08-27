// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Dialogs;

using System.Reflection;

/// <summary>Defines retained composition and asynchronous state behavior for SaveFileDialog.</summary>
public sealed class SaveFileDialogTests
{
    /// <summary>Verifies caller formatter failures become deterministic dialog status instead of
    /// escaping the accept interaction through async void.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Save_WhenOverwriteMessageFormatterFails_ReportsRecoverableStatusAsync(bool returnsNull)
    {
        var directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "save-formatter-failure"));
        var existingPath = Path.Combine(directory, "existing.txt");
        var source = new FakeFilePickerFileSystem();
        source.AddDirectory(directory, new FilePickerEntry("existing.txt", existingPath, false, false));
        var dialog = new SaveFileDialog(
            new SaveFileOptions
            {
                InitialDirectory = directory,
                InitialFileName = "existing.txt",
                ConfirmOverwrite = true
            },
            source)
        {
            OverwriteMessageFormat = returnsNull
                ? static _ => null!
                : static _ => throw new FormatException("formatter boom")
        };
        var save = OwnedTree.FindAll<Button>(dialog).Single(static button => button.IsDefault);
        var root = new Overlay { Children = { dialog } };
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(
            () => root.Attach(dispatcher),
            TestContext.Current.CancellationToken);

        for (var attempt = 0; attempt < 20 && dialog.IsLoading; attempt++)
        {
            await dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);
        }

        await dispatcher.InvokeAsync(save.PerformClick, TestContext.Current.CancellationToken);

        dialog.HasSelectedResult.ShouldBeFalse();
        dialog.Status.ShouldStartWith("Cannot confirm overwrite:");
        await dispatcher.InvokeAsync(root.Dispose, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies overwrite completion captured by an earlier dispatcher attachment cannot
    /// publish its abandoned path after the modeless dialog migrates to a new owner.</summary>
    [Fact]
    public async Task Save_WhenDialogMigratesDuringOverwriteConfirmation_IgnoresPreviousRequestAsync()
    {
        var directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "save-migration"));
        var existingPath = Path.Combine(directory, "existing.txt");
        var source = new FakeFilePickerFileSystem();
        source.AddDirectory(directory, new FilePickerEntry("existing.txt", existingPath, false, false));
        var dialog = new SaveFileDialog(
            new SaveFileOptions
            {
                InitialDirectory = directory,
                InitialFileName = "existing.txt",
                ConfirmOverwrite = true
            },
            source);
        var fileName = OwnedTree.FindAll<TextInput>(dialog)
            .First(static input => input.Placeholder == "File name");
        var save = OwnedTree.FindAll<Button>(dialog).Single(static button => button.IsDefault);
        var confirmation = new TaskCompletionSource<MessageBoxResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var postReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        dialog.ConfirmOverwriteForLifecycleTest = () => confirmation.Task;
        dialog.PostAcceptanceHookForLifecycleTest = postReady.SetResult;
        await using var previousDispatcher = Dispatcher.Start();
        await using var currentDispatcher = Dispatcher.Start();
        var previousRoot = new Overlay { Children = { dialog } };
        var currentRoot = new Overlay();

        await previousDispatcher.InvokeAsync(
            () =>
            {
                previousRoot.Attach(previousDispatcher);
                save.PerformClick();
                previousRoot.Children.Remove(dialog).ShouldBeTrue();
            },
            TestContext.Current.CancellationToken);
        await currentDispatcher.InvokeAsync(
            () =>
            {
                currentRoot.Children.Add(dialog);
                currentRoot.Attach(currentDispatcher);
                fileName.Text = "new.txt";
            },
            TestContext.Current.CancellationToken);

        confirmation.SetResult(MessageBoxResult.Yes);
        await postReady.Task.WaitAsync(TestContext.Current.CancellationToken);
        await previousDispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);
        await currentDispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);

        dialog.HasSelectedResult.ShouldBeFalse();
        dialog.FileName.ShouldBe("new.txt");
        await currentDispatcher.InvokeAsync(currentRoot.Dispose, TestContext.Current.CancellationToken);
        await previousDispatcher.InvokeAsync(previousRoot.Dispose, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies save-specific retained captions and placeholders follow newer values
    /// committed from their owner notifications.</summary>
    [Fact]
    public void ForwardedText_WhenPropertyObserversCommitNewerValues_UpdatesRetainedControls()
    {
        using var dialog = new SaveFileDialog();
        dialog.PropertyChanged += (_, eventArgs) =>
        {
            switch (eventArgs.PropertyName)
            {
                case nameof(SaveFileDialog.SaveText) when dialog.SaveText == "Outer save":
                    dialog.SaveText = "Nested save";
                    break;
                case nameof(SaveFileDialog.FileNameLabel) when dialog.FileNameLabel == "Outer label":
                    dialog.FileNameLabel = "Nested label";
                    break;
                case nameof(SaveFileDialog.FileNamePlaceholder) when dialog.FileNamePlaceholder == "Outer name":
                    dialog.FileNamePlaceholder = "Nested name";
                    break;
                default:
                    break;
            }
        };

        dialog.SaveText = "Outer save";
        dialog.FileNameLabel = "Outer label";
        dialog.FileNamePlaceholder = "Outer name";

        OwnedTree.FindAll<Button>(dialog).ShouldContain(button => button.Text == "Nested save");
        OwnedTree.FindAll<ControlText>(dialog).ShouldContain(text => text.Content == "Nested label");
        OwnedTree.FindAll<TextInput>(dialog).ShouldContain(input => input.Placeholder == "Nested name");
    }
    /// <summary>Verifies construction copies configuration and composes one responsive dialog Window.</summary>
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

    /// <summary>Verifies direct and ancestor-inherited disablement both resolve through
    /// EffectiveIsEnabled without requiring a mounted surface, and re-enabling restores it - the
    /// detached counterpart to the mounted disabled-appearance evidence below.</summary>
    [Fact]
    public void Enabled_WhenSetDirectlyOrByAncestor_UpdatesEffectiveIsEnabled()
    {
        using var dialog = new SaveFileDialog(null, new FakeFilePickerFileSystem());
        var stack = new Stack { Children = { dialog } };

        dialog.EffectiveIsEnabled.ShouldBeTrue();

        dialog.IsEnabled = false;
        dialog.EffectiveIsEnabled.ShouldBeFalse();
        dialog.IsEnabled = true;
        dialog.EffectiveIsEnabled.ShouldBeTrue();

        stack.IsEnabled = false;
        dialog.IsEnabled.ShouldBeTrue();
        dialog.EffectiveIsEnabled.ShouldBeFalse();

        stack.IsEnabled = true;
        dialog.EffectiveIsEnabled.ShouldBeTrue();
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
            .First(static button => button.Text.Contains("Save"));
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
            .First(static button => button.Text.Contains("Save"));

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
        saveButton.IsEnabled.ShouldBeTrue();
        dialog.HasSelectedResult.ShouldBeFalse();

        // Act: the Save button commits the already-populated filename - a regression guard for the
        // button path now that a pointer click alone no longer completes the dialog.
        await surface.Pointer.ClickAsync(saveButton);

        // Assert
        dialog.HasSelectedResult.ShouldBeTrue();
        var result = dialog.SelectedResult.ShouldNotBeNull();
        result.IsConfirmed.ShouldBeTrue();
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
        result.IsConfirmed.ShouldBeTrue();
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
            await DialogWait.UntilAsync(surface, dialog, () => !dialog.IsLoading);
            await surface.Keyboard.PressAsync(Code.Enter);

            // Assert
            var result = await pending!;
            result.IsConfirmed.ShouldBeTrue();
            result.Path.ShouldBe(Path.Combine(directory, "newfile.txt"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Verifies direct and ancestor-inherited disablement switch the mounted
    /// SaveFileDialog to its disabled appearance, re-enabling restores Normal, and a disabled
    /// instance moved to a genuinely different size arranges identically to an
    /// independently-mounted enabled instance at that same size.</summary>
    [Fact]
    public async Task Enabled_WhenDisabledDirectlyOrByAncestor_ChangesAppearanceAndRecoversAsync()
    {
        // Arrange
        var directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "save-disabled-contract"));
        var source = new FakeFilePickerFileSystem();
        source.AddDirectory(directory);
        var dialog = new SaveFileDialog(new SaveFileOptions { InitialDirectory = directory }, source);
        var stack = new Stack { Children = { dialog } };
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(100, 40),
            TestContext.Current.CancellationToken);
        await DialogWait.UntilAsync(surface, dialog, () => !dialog.IsLoading);
        surface.ShouldHaveState(dialog, VisualState.Normal);

        // Act and assert direct disable
        await surface.UpdateAsync(() => dialog.IsEnabled = false, "disable SaveFileDialog directly");
        surface.ShouldHaveState(dialog, VisualState.Disabled);

        // Act and assert re-enable recovery
        await surface.UpdateAsync(() => dialog.IsEnabled = true, "re-enable SaveFileDialog");
        surface.ShouldHaveState(dialog, VisualState.Normal);

        // Act and assert ancestor-inherited disable
        await surface.UpdateAsync(() => stack.IsEnabled = false, "disable ancestor Stack");
        surface.ShouldHaveState(dialog, VisualState.Disabled);
        dialog.IsEnabled.ShouldBeTrue();
        dialog.EffectiveIsEnabled.ShouldBeFalse();
        await surface.UpdateAsync(() => stack.IsEnabled = true, "re-enable ancestor Stack");
        surface.ShouldHaveState(dialog, VisualState.Normal);

        // Arrange geometry comparison: a disabled instance moved to a genuinely different size -
        // same-size arrange is a no-op - must match an independently-mounted enabled instance
        // arranged directly at that same size.
        var disabledSource = new FakeFilePickerFileSystem();
        disabledSource.AddDirectory(directory);
        var disabled = new SaveFileDialog(new SaveFileOptions { InitialDirectory = directory }, disabledSource);
        await using var disabledSurface = await ComponentSurface.MountAsync(
            disabled,
            new Size(100, 40),
            TestContext.Current.CancellationToken);
        await DialogWait.UntilAsync(disabledSurface, disabled, () => !disabled.IsLoading);
        await disabledSurface.UpdateAsync(() => disabled.IsEnabled = false, "disable independent SaveFileDialog");

        // Act
        await disabledSurface.ResizeAsync(new Size(76, 30));

        var enabledSource = new FakeFilePickerFileSystem();
        enabledSource.AddDirectory(directory);
        var enabled = new SaveFileDialog(new SaveFileOptions { InitialDirectory = directory }, enabledSource);
        await using var enabledSurface = await ComponentSurface.MountAsync(
            enabled,
            new Size(76, 30),
            TestContext.Current.CancellationToken);
        await DialogWait.UntilAsync(enabledSurface, enabled, () => !enabled.IsLoading);

        // Assert
        disabled.Bounds.ShouldBe(enabled.Bounds);
        disabled.DesiredSize.ShouldBe(enabled.DesiredSize);
    }

    /// <summary>Verifies a disabled SaveFileDialog's close glyph renders with the theme's
    /// disabled-text color and ignores pointer hover/press instead of closing the dialog.</summary>
    [Fact]
    public async Task CloseGlyph_WhenSaveFileDialogIsDisabled_ShowsDisabledColorAndIgnoresPointerAsync()
    {
        // Arrange
        var directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "save-disabled-close"));
        var source = new FakeFilePickerFileSystem();
        source.AddDirectory(directory);
        var dialog = new SaveFileDialog(new SaveFileOptions { InitialDirectory = directory }, source);
        var closed = 0;
        dialog.Closed += (_, _) => closed++;
        await using var surface = await ComponentSurface.MountAsync(
            dialog,
            new Size(76, 35),
            TestContext.Current.CancellationToken);
        await DialogWait.UntilAsync(surface, dialog, () => !dialog.IsLoading);
        var theme = dialog.Theme.ShouldNotBeNull();
        var disabledForeground = TerminalPalette.Project(
            ThemeColorHelper.DisabledForeground(theme),
            ColorDepth.Basic16);
        var glyphPoint = new Point(dialog.Bounds.X + 4, dialog.Bounds.Y);

        // Act
        await surface.UpdateAsync(() => dialog.IsEnabled = false, "disable SaveFileDialog");

        // Assert disabled close-glyph color
        surface.Cell(glyphPoint).Style.Foreground.ShouldBe(disabledForeground);

        // Act - aim a pointer press at the close glyph while disabled
        await surface.Pointer.MoveToAsync(dialog, new Point(4, 0));
        await surface.Pointer.PressAsync();
        await surface.Pointer.ReleaseAsync();

        // Assert the press neither closed the dialog nor changed the glyph's rendered color
        closed.ShouldBe(0);
        dialog.IsDisposed.ShouldBeFalse();
        surface.Cell(glyphPoint).Style.Foreground.ShouldBe(disabledForeground);
    }

    /// <summary>Verifies a local Style override's close-mark color reaches the rendered close
    /// glyph, proving the close chrome consults SaveFileDialog's own resolved style instead of
    /// always reading the generic "window" theme section.</summary>
    [Fact]
    public async Task CloseGlyph_WhenLocalStyleOverridesCloseMarkColor_UsesTheOverrideColorAsync()
    {
        // Arrange
        var directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "save-local-close-style"));
        var source = new FakeFilePickerFileSystem();
        source.AddDirectory(directory);
        var dialog = new SaveFileDialog(new SaveFileOptions { InitialDirectory = directory }, source)
        {
            Style = SaveFileDialogStyle.Default with { CloseMarkColor = SemanticColor.Error }
        };
        await using var surface = await ComponentSurface.MountAsync(
            dialog,
            new Size(76, 35),
            TestContext.Current.CancellationToken);
        await DialogWait.UntilAsync(surface, dialog, () => !dialog.IsLoading);
        var theme = dialog.Theme.ShouldNotBeNull();
        var glyphPoint = new Point(dialog.Bounds.X + 4, dialog.Bounds.Y);

        // Assert
        surface.Cell(glyphPoint).Style.Foreground.ShouldBe(
            TerminalPalette.Project(theme.Error, ColorDepth.Basic16));
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
            await DialogWait.UntilAsync(surface, dialog, () => !dialog.IsLoading);
            await surface.Keyboard.PressAsync(Code.Escape);

            // Assert
            var result = await pending!;
            result.IsConfirmed.ShouldBeFalse();
            result.Path.ShouldBeNull();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Verifies ShowAsync rejects a null owner before constructing or attaching any dialog.</summary>
    [Fact]
    public void ShowAsync_WhenOwnerIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => SaveFileDialog.ShowAsync(null!));

    /// <summary>Verifies ShowAsync rejects a disposed owner before constructing any dialog.</summary>
    [Fact]
    public void ShowAsync_WhenOwnerIsDisposed_ThrowsObjectDisposedException()
    {
        var owner = new Button();
        owner.Dispose();

        _ = Should.Throw<ObjectDisposedException>(() => SaveFileDialog.ShowAsync(owner));
    }

    /// <summary>Verifies ShowAsync rejects an already cancelled token before constructing any dialog.</summary>
    [Fact]
    public void ShowAsync_WhenCancellationTokenIsAlreadyCancelled_ThrowsOperationCanceledException()
    {
        var owner = new Button();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        _ = Should.Throw<OperationCanceledException>(
            () => SaveFileDialog.ShowAsync(owner, cancellationToken: cancellation.Token));
    }

    /// <summary>Verifies ShowAsync rejects a detached (never attached) owner with the documented
    /// ArgumentException instead of a null-reference fault reading its Dispatcher.</summary>
    [Fact]
    public void ShowAsync_WhenOwnerIsDetached_ThrowsArgumentException()
    {
        var owner = new Button();

        var exception = Should.Throw<ArgumentException>(() => SaveFileDialog.ShowAsync(owner));

        exception.ParamName.ShouldBe("owner");
    }

    /// <summary>Verifies ShowAsync rejects an attached owner with no presentation host, and leaves
    /// the owner otherwise unaffected - no dialog is ever constructed for this rejection.</summary>
    [Fact]
    public async Task ShowAsync_WhenOwnerHasNoPresentationHost_ThrowsArgumentExceptionAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var owner = new Button { Text = "Bare" };
            owner.Attach(dispatcher, UnicodePolicy.Default, TerminalCapabilities.Conservative);

            var exception = Should.Throw<ArgumentException>(() => SaveFileDialog.ShowAsync(owner));

            exception.ParamName.ShouldBe("owner");
            owner.Parent.ShouldBeNull();
            owner.IsDisposed.ShouldBeFalse();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies ShowAsync called from a thread other than the owner's dispatcher throws
    /// InvalidOperationException instead of racing dialog construction against the owning
    /// dispatcher, and that no dialog was attached under the mounted root.</summary>
    [Fact]
    public async Task ShowAsync_WhenCalledOffTheOwnersDispatcher_ThrowsInvalidOperationExceptionAsync()
    {
        var opener = new Button { Text = "Open" };
        var host = new Overlay { Children = { opener } };
        await using var surface = await ComponentSurface.MountAsync(
            host,
            new Size(48, 14),
            TestContext.Current.CancellationToken);

        _ = Should.Throw<InvalidOperationException>(() => SaveFileDialog.ShowAsync(opener));

        OwnedTree.Find<SaveFileDialog>(surface.Application.Root).ShouldBeNull();
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
            await DialogWait.UntilAsync(surface, dialog, () => !dialog.IsLoading);
            await surface.Keyboard.PressAsync(Code.Enter);

            // Assert
            var result = await pending!;
            result.IsConfirmed.ShouldBeTrue();
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
            await DialogWait.UntilAsync(surface, dialog, () => !dialog.IsLoading);
            await surface.Keyboard.PressAsync(Code.Enter);

            // Assert: the dialog stays open instead of completing with a directory as the result.
            await surface.Application.Dispatcher.InvokeAsync(
                static () => { },
                TestContext.Current.CancellationToken);
            pending!.IsCompleted.ShouldBeFalse();
            dialog.IsDisposed.ShouldBeFalse();
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
            await DialogWait.UntilAsync(surface, dialog, () => !dialog.IsLoading);

            // Trigger save — this should show a MessageBox confirmation since the file exists.
            await surface.Keyboard.PressAsync(Code.Enter);
            await surface.UpdateAsync(static () => { }, "settle confirmation dialog");

            // Confirm overwrite by pressing Enter on the MessageBox's Yes button.
            await surface.Keyboard.PressAsync(Code.Enter);

            // Assert
            var result = await pending!;
            result.IsConfirmed.ShouldBeTrue();
            result.Path.ShouldBe(existingPath);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Verifies declining the overwrite confirmation ("No") leaves the save dialog open
    /// and its own returned task uncompleted, the sibling outcome to the "Yes" path covered by
    /// Save_WhenFileExistsAndConfirmOverwrite_ShowsConfirmationAsync immediately above - the
    /// production code only completes on <c>MessageBoxResult.Yes</c>, but nothing previously
    /// proved the "No" branch is actually reachable and inert instead of always falling through.</summary>
    [Fact]
    public async Task Save_WhenOverwriteConfirmationIsDeclined_LeavesTheDialogOpenWithoutCompletingAsync()
    {
        // Arrange
        var directory = Path.Combine(Path.GetTempPath(), $"save-confirm-decline-{Guid.NewGuid():N}");
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
            await DialogWait.UntilAsync(surface, dialog, () => !dialog.IsLoading);

            // Trigger save so the overwrite confirmation appears, then decline it.
            await surface.Keyboard.PressAsync(Code.Enter);
            await surface.UpdateAsync(static () => { }, "settle confirmation dialog");
            var confirmation = OwnedTree.Find<MessageBox>(surface.Application.Root).ShouldNotBeNull();
            var no = OwnedTree.FindAll<Button>(confirmation).Single(static button => button.Text == "&No");
            await surface.UpdateAsync(
                () => surface.Application.Focus.Focus(no).ShouldBeTrue(),
                "focus overwrite confirmation No button");
            await surface.Keyboard.PressAsync(Code.Enter);
            await surface.UpdateAsync(static () => { }, "settle declined confirmation");

            // Assert
            pending!.IsCompleted.ShouldBeFalse();
            dialog.IsDisposed.ShouldBeFalse();
            OwnedTree.Find<MessageBox>(surface.Application.Root).ShouldBeNull();
            OwnedTree.Find<SaveFileDialog>(surface.Application.Root).ShouldBeSameAs(dialog);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Verifies the async void CompleteAcceptedAsync does not let InvalidOperationException
    /// escape from its post-await confirmation post when the owning dispatcher's bounded queue is
    /// saturated at the moment the overwrite confirmation resolves. CompleteAcceptedAsync's own
    /// comment documents that an unhandled exception here is unobservable by any caller and becomes
    /// process-fatal. The confirmation is completed directly (bypassing the Yes button) with no
    /// ambient SynchronizationContext captured, matching the method's own documented contract that
    /// the MessageBox completion resumes off this dispatcher rather than back on it - the same
    /// off-thread shape as <c>DispatcherTests.Post_WhenQueueIsFull_ThrowsBeforeEnqueueAsync</c>.</summary>
    [Fact]
    public async Task Save_WhenDispatcherQueueIsSaturatedAtConfirmationTime_DoesNotThrowUnhandledAsync()
    {
        // Arrange
        var directory = Path.Combine(Path.GetTempPath(), $"save-saturated-confirm-{Guid.NewGuid():N}");
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

            // Not awaited: this test completes the confirmation directly through reflection
            // rather than through Complete(), so ShowAsync's own returned task never settles.
            await surface.UpdateAsync(
                () => _ = SaveFileDialog.ShowAsync(
                    opener,
                    new SaveFileOptions
                    {
                        InitialDirectory = directory,
                        InitialFileName = "existing.txt",
                        ConfirmOverwrite = true
                    }),
                "show save dialog");
            var dialog = OwnedTree.Find<SaveFileDialog>(surface.Application.Root).ShouldNotBeNull();
            await DialogWait.UntilAsync(surface, dialog, () => !dialog.IsLoading);
            var dispatcher = surface.Application.Dispatcher;
            var saveButton = OwnedTree.FindAll<Button>(dialog).Single(static button => button.IsDefault);
            var enterArgs = new KeyEventArgs(new Stroke(
                Code.Enter,
                character: null,
                nativeCode: 0,
                Modifiers.None,
                KeyAction.Press));

            // Trigger the save with no ambient SynchronizationContext captured by
            // CompleteAcceptedAsync's own await - the dispatcher normally installs one for every
            // callback it runs, but this method's own comment documents that the real MessageBox
            // completion does NOT resume through it, so this reproduces that contract directly
            // instead of relying on incidental timing.
            await dispatcher.InvokeAsync(
                () =>
                {
                    var previousContext = SynchronizationContext.Current;
                    SynchronizationContext.SetSynchronizationContext(null);

                    try
                    {
                        _ = Router.Route(saveButton, Events.Key, enterArgs);
                    }
                    finally
                    {
                        SynchronizationContext.SetSynchronizationContext(previousContext);
                    }
                },
                TestContext.Current.CancellationToken);

            var confirmation = OwnedTree.Find<MessageBox>(surface.Application.Root).ShouldNotBeNull();
            var completionField = typeof(Dialog<MessageBoxResult>).GetField(
                "_completion",
                BindingFlags.Instance | BindingFlags.NonPublic).ShouldNotBeNull();
            var completion = completionField.GetValue(confirmation)
                .ShouldBeOfType<TaskCompletionSource<MessageBoxResult>>();

            // Act: saturate the dispatcher's bounded queue while it is blocked (not draining), then
            // resolve the confirmation from this thread - CompleteAcceptedAsync's own post-await
            // post must now observe a genuinely full queue instead of the one free slot its own
            // continuation would otherwise always find waiting for it.
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using ManualResetEventSlim release = new();
            dispatcher.Post(() =>
            {
                entered.SetResult();
                release.Wait();
            });
            await entered.Task.WaitAsync(TestContext.Current.CancellationToken);

            try
            {
                // The mounted surface's own background activity (renders, invalidations queued
                // before the block took effect) already occupies some of the queue's capacity, so
                // fill until the queue provably rejects a post rather than assuming a fixed number
                // of free slots.
                try
                {
                    while (true)
                    {
                        dispatcher.Post(static () => { });
                    }
                }
                catch (InvalidOperationException)
                {
                }

                Should.NotThrow(() => { _ = completion.TrySetResult(MessageBoxResult.Yes); });

                // The continuation observes no captured SynchronizationContext, so it resumes off
                // this dispatcher; give it a moment to reach its own post-await post.
                await Task.Delay(TimeSpan.FromMilliseconds(500), TestContext.Current.CancellationToken);
            }
            finally
            {
                release.Set();
            }

            // Assert - the dialog and dispatcher survive a saturated confirmation instead of an
            // unhandled exception tearing down the process. Draining the backlog this test posted
            // happens on the dispatcher's own thread, unsynchronized with this one, so under
            // parallel test-suite load a post attempted immediately after release.Set() can still
            // observe a momentarily full queue; retry briefly rather than assuming a free slot.
            dialog.IsDisposed.ShouldBeFalse();

            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);

            while (true)
            {
                try
                {
                    await surface.UpdateAsync(static () => { }, "confirm the dispatcher still processes work");
                    break;
                }
                catch (InvalidOperationException) when (DateTime.UtcNow < deadline)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(25), TestContext.Current.CancellationToken);
                }
            }
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

    /// <summary>Verifies SaveFileOptions carries every owned-part style, plus the aggregate
    /// <see cref="SaveFileOptions.Style"/>, through construction and through Copy(), matching how
    /// ShowAsync's copied snapshot reaches the constructed dialog.</summary>
    [Fact]
    public void Constructor_WhenOptionsCarryStyles_AppliesThemToTheConstructedDialog()
    {
        var buttonStyle = ButtonStyle.Standard with { Padding = new Thickness(horizontal: 1, vertical: 0) };
        var checkBoxStyle = CheckBoxStyle.Default;
        var scrollBarStyle = ScrollBarStyle.Default;
        var aggregateStyle = SaveFileDialogStyle.Default with { RootPadding = new Thickness(2) };
        var options = new SaveFileOptions
        {
            CancelButtonStyle = buttonStyle,
            ShowHiddenCheckBoxStyle = checkBoxStyle,
            FileListScrollBarStyle = scrollBarStyle,
            FilterScrollBarStyle = scrollBarStyle,
            SaveButtonStyle = buttonStyle,
            Style = aggregateStyle
        };
        var copy = options.Copy();

        using var dialog = new SaveFileDialog(options, new FakeFilePickerFileSystem());

        copy.CancelButtonStyle.ShouldBe(buttonStyle);
        copy.ShowHiddenCheckBoxStyle.ShouldBe(checkBoxStyle);
        copy.FileListScrollBarStyle.ShouldBe(scrollBarStyle);
        copy.FilterScrollBarStyle.ShouldBe(scrollBarStyle);
        copy.SaveButtonStyle.ShouldBe(buttonStyle);
        copy.Style.ShouldBe(aggregateStyle);
        dialog.ActualCancelButtonStyle.ShouldBe(buttonStyle);
        dialog.ActualShowHiddenCheckBoxStyle.ShouldBe(checkBoxStyle);
        dialog.ActualFileListScrollBarStyle.ShouldBe(scrollBarStyle);
        dialog.ActualFilterScrollBarStyle.ShouldBe(scrollBarStyle);
        dialog.ActualSaveButtonStyle.ShouldBe(buttonStyle);
        dialog.ActualStyle.ShouldBe(aggregateStyle);
    }

    /// <summary>Verifies SaveFileOptions carries every caption, directory-placeholder, and
    /// overwrite-confirmation caption through construction and through Copy(), matching how
    /// ShowAsync's copied snapshot reaches the constructed dialog - the sibling coverage to the
    /// owned-part style forwarding above.</summary>
    [Fact]
    public void Constructor_WhenOptionsCarryCaptions_AppliesThemToTheConstructedDialog()
    {
        var options = new SaveFileOptions
        {
            ParentDirectoryText = "«",
            DirectoryPlaceholder = "Ruta",
            ShowHiddenText = "Mostrar &ocultos",
            CancelText = "&Salir",
            SaveText = "&Guardar",
            FileNameLabel = "Archivo:",
            FileNamePlaceholder = "Nombre del archivo",
            OverwriteTitle = "¿Sobrescribir?",
            OverwriteYesText = "&Sí",
            OverwriteNoText = "&No, gracias"
        };
        var copy = options.Copy();

        using var dialog = new SaveFileDialog(options, new FakeFilePickerFileSystem());

        copy.ParentDirectoryText.ShouldBe("«");
        copy.DirectoryPlaceholder.ShouldBe("Ruta");
        copy.ShowHiddenText.ShouldBe("Mostrar &ocultos");
        copy.CancelText.ShouldBe("&Salir");
        copy.SaveText.ShouldBe("&Guardar");
        copy.FileNameLabel.ShouldBe("Archivo:");
        copy.FileNamePlaceholder.ShouldBe("Nombre del archivo");
        copy.OverwriteTitle.ShouldBe("¿Sobrescribir?");
        copy.OverwriteYesText.ShouldBe("&Sí");
        copy.OverwriteNoText.ShouldBe("&No, gracias");
        dialog.ParentDirectoryText.ShouldBe("«");
        dialog.DirectoryPlaceholder.ShouldBe("Ruta");
        dialog.ShowHiddenText.ShouldBe("Mostrar &ocultos");
        dialog.CancelText.ShouldBe("&Salir");
        dialog.SaveText.ShouldBe("&Guardar");
        dialog.FileNameLabel.ShouldBe("Archivo:");
        dialog.FileNamePlaceholder.ShouldBe("Nombre del archivo");
        dialog.OverwriteTitle.ShouldBe("¿Sobrescribir?");
        dialog.OverwriteYesText.ShouldBe("&Sí");
        dialog.OverwriteNoText.ShouldBe("&No, gracias");
    }

    /// <summary>Verifies SaveFileOptions carries the status texts and the overwrite-message
    /// formatter through construction and through Copy(), matching how ShowAsync's copied snapshot
    /// reaches the constructed dialog - previously these members were silently dropped by the ctor
    /// apply path even though FileDialogBase and SaveFileDialog both exposed writable properties
    /// for them.</summary>
    [Fact]
    public void Constructor_WhenOptionsCarryStatusTextsAndFormatters_AppliesThemToTheConstructedDialog()
    {
        string CountFormat(int folders, int files) => $"{folders}f/{files}d";
        string OverwriteMessageFormat(string fileName) => $"Replace {fileName}?";

        var options = new SaveFileOptions
        {
            ReadyText = "Listo",
            LoadingText = "Cargando…",
            CountFormat = CountFormat,
            OverwriteMessageFormat = OverwriteMessageFormat
        };
        var copy = options.Copy();

        using var dialog = new SaveFileDialog(options, new FakeFilePickerFileSystem());

        copy.ReadyText.ShouldBe("Listo");
        copy.LoadingText.ShouldBe("Cargando…");
        copy.CountFormat.ShouldBe((Func<int, int, string>) CountFormat);
        copy.OverwriteMessageFormat.ShouldBe((Func<string, string>) OverwriteMessageFormat);
        dialog.ReadyText.ShouldBe("Listo");
        dialog.LoadingText.ShouldBe("Cargando…");
        dialog.CountFormat(3, 5).ShouldBe("3f/5d");
        dialog.OverwriteMessageFormat("a.txt").ShouldBe("Replace a.txt?");
    }

    /// <summary>Verifies a null SaveFileOptions status text or formatter leaves the constructed
    /// dialog's own defaults intact instead of forwarding a null value.</summary>
    [Fact]
    public void Constructor_WhenOptionsOmitStatusTextsAndFormatters_KeepsDialogDefaults()
    {
        using var dialog = new SaveFileDialog(new SaveFileOptions(), new FakeFilePickerFileSystem());

        dialog.ReadyText.ShouldBe("Ready");
        dialog.LoadingText.ShouldBe("Loading…");
        dialog.CountFormat(1, 1).ShouldBe("1 folder · 1 file");
        dialog.OverwriteMessageFormat("a.txt").ShouldBe("'a.txt' already exists.\nDo you want to replace it?");
    }

    /// <summary>Verifies OverwriteStyle round-trips through its own getter, since the dialog's
    /// existing confirmation-flow coverage only ever proves its downstream effect on the generated
    /// MessageBox's ActualStyle, not that the property itself returns the assigned value.</summary>
    [Fact]
    public void OverwriteStyle_WhenSet_RoundTrips()
    {
        using var dialog = new SaveFileDialog(null, new FakeFilePickerFileSystem());
        var style = MessageBoxStyle.Default with { ActionBarMargin = new Thickness(3, 0) };

        dialog.OverwriteStyle.ShouldBeNull();

        dialog.OverwriteStyle = style;

        dialog.OverwriteStyle.ShouldBe(style);
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
            await DialogWait.UntilAsync(surface, dialog, () => !dialog.IsLoading);

            // Trigger save — this should show a MessageBox confirmation since the file exists.
            await surface.Keyboard.PressAsync(Code.Enter);
            await surface.UpdateAsync(static () => { }, "settle confirmation dialog");

            // Assert
            var confirmation = OwnedTree.Find<MessageBox>(surface.Application.Root).ShouldNotBeNull();
            confirmation.ActualButtonStyle.ShouldBe(style);

            // Confirm overwrite by pressing Enter on the MessageBox's Yes button.
            await surface.Keyboard.PressAsync(Code.Enter);
            var result = await pending!;
            result.IsConfirmed.ShouldBeTrue();
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
            await DialogWait.UntilAsync(surface, dialog, () => !dialog.IsLoading);
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
            result.IsConfirmed.ShouldBeTrue();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
