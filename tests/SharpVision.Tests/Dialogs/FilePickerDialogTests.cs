// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.


namespace SharpVision.Tests.Dialogs;



/// <summary>Defines retained composition and asynchronous state behavior for FilePickerDialog.</summary>
public sealed class FilePickerDialogTests
{
    /// <summary>Verifies a localized caption committed from PropertyChanged owns the retained
    /// navigation button instead of being overwritten by the outer captured text.</summary>
    [Fact]
    public void ParentDirectoryText_WhenPropertyObserverCommitsNewerText_UpdatesRetainedButton()
    {
        using var dialog = new FilePickerDialog();
        dialog.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(FilePickerDialog.ParentDirectoryText) &&
                dialog.ParentDirectoryText == "Old")
            {
                dialog.ParentDirectoryText = "New";
            }
        };

        dialog.ParentDirectoryText = "Old";

        dialog.ParentDirectoryText.ShouldBe("New");
        OwnedTree.FindAll<Button>(dialog).ShouldContain(button => button.Text == "New");
        OwnedTree.FindAll<Button>(dialog).ShouldNotContain(button => button.Text == "Old");
    }

    /// <summary>Verifies every other immediately forwarded picker caption and placeholder follows
    /// its newest owner value after synchronous reentry.</summary>
    [Fact]
    public void ForwardedText_WhenPropertyObserversCommitNewerValues_UpdatesRetainedControls()
    {
        using var dialog = new FilePickerDialog();
        dialog.PropertyChanged += (_, eventArgs) =>
        {
            switch (eventArgs.PropertyName)
            {
                case nameof(FilePickerDialog.DirectoryPlaceholder) when dialog.DirectoryPlaceholder == "Outer path":
                    dialog.DirectoryPlaceholder = "Nested path";
                    break;
                case nameof(FilePickerDialog.ShowHiddenText) when dialog.ShowHiddenText == "Outer hidden":
                    dialog.ShowHiddenText = "Nested hidden";
                    break;
                case nameof(FilePickerDialog.CancelText) when dialog.CancelText == "Outer cancel":
                    dialog.CancelText = "Nested cancel";
                    break;
                case nameof(FilePickerDialog.OpenText) when dialog.OpenText == "Outer open":
                    dialog.OpenText = "Nested open";
                    break;
                default:
                    break;
            }
        };

        dialog.DirectoryPlaceholder = "Outer path";
        dialog.ShowHiddenText = "Outer hidden";
        dialog.CancelText = "Outer cancel";
        dialog.OpenText = "Outer open";

        OwnedTree.FindAll<TextInput>(dialog).ShouldContain(input => input.Placeholder == "Nested path");
        OwnedTree.FindAll<CheckBox>(dialog).ShouldContain(toggle => toggle.Text == "Nested hidden");
        OwnedTree.FindAll<Button>(dialog).ShouldContain(button => button.Text == "Nested cancel");
        OwnedTree.FindAll<Button>(dialog).ShouldContain(button => button.Text == "Nested open");
    }
    /// <summary>Verifies construction copies configuration and composes one responsive dialog Window.</summary>
    [Fact]
    public void Constructor_WhenConfigured_UsesCopiedOptionsAndSemanticControls()
    {
        // Arrange
        var initialDirectory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "picker-construction"));
        var options = new FilePickerOptions
        {
            Title = "Open source",
            InitialDirectory = initialDirectory,
            AllowMultiple = true,
            ShowHidden = true,
            MaxVisibleRows = 7,
            Filters = [new FilePickerFilter("Sources", "*.cs"), FilePickerFilter.AllFiles],
            FilterIndex = 1
        };
        var source = new FakeFilePickerFileSystem();

        // Act
        var dialog = new FilePickerDialog(options, source);
        options.Title = "Changed";
        options.ShowHidden = false;
        options.MaxVisibleRows = 11;
        options.FilterIndex = 0;

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
        window.Header.ShouldBe("Open source");
        window.Width.ShouldBe(Length.Percent(80));
        window.Height.ShouldBe(Length.Percent(80));
        window.MinWidth.ShouldBe(0);
        window.MaxWidth.ShouldBe(96);
        window.MinHeight.ShouldBe(0);
        window.MaxHeight.ShouldBe(26);
        list.MaxHeight.ShouldBe(7);
        list.SelectionMode.ShouldBe(ListSelectionMode.Multiple);
        filter.SelectedIndex.ShouldBe(1);
        filter.ActualBorder.Sides.ShouldBe(BorderSide.All);
        hidden.IsChecked.ShouldBe(true);
        dialog.CurrentDirectory.ShouldBe(initialDirectory);
        dialog.ShowHidden.ShouldBeTrue();
        dialog.FilterIndex.ShouldBe(1);
        dialog.SelectedPaths.ShouldBeEmpty();
        dialog.IsLoading.ShouldBeFalse();
    }

    /// <summary>Verifies direct and ancestor-inherited disablement both resolve through
    /// EffectiveIsEnabled without requiring a mounted surface, and re-enabling restores it - the
    /// detached counterpart to the mounted disabled-appearance evidence in
    /// FilePickerDialogSurfaceTests.</summary>
    [Fact]
    public void Enabled_WhenSetDirectlyOrByAncestor_UpdatesEffectiveIsEnabled()
    {
        using var dialog = new FilePickerDialog(null, new FakeFilePickerFileSystem());
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

    /// <summary>Verifies attachment loads one snapshot and publishes file-only count and ListView rows.</summary>
    [Fact]
    public async Task OnAttached_WhenInitialDirectoryLoads_PublishesCommittedSnapshotAsync()
    {
        // Arrange
        var directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "picker-load"));
        var source = new FakeFilePickerFileSystem();
        source.AddDirectory(
            directory,
            new FilePickerEntry("src", Path.Combine(directory, "src"), isDirectory: true, isHidden: false),
            new FilePickerEntry("Program.cs", Path.Combine(directory, "Program.cs"), isDirectory: false, isHidden: false));
        var dialog = new FilePickerDialog(
            new FilePickerOptions { InitialDirectory = directory },
            source);

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            dialog,
            new Size(100, 40),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(static () => { }, "settle file picker load");

        // Assert
        dialog.IsLoading.ShouldBeFalse();
        dialog.CurrentDirectory.ShouldBe(directory);
        dialog.Status.ShouldBe("1 folder · 1 file");
        dialog.SelectedPaths.ShouldBeEmpty();
        OwnedTree.Find<UiListView>(dialog).ShouldNotBeNull().Items.Count.ShouldBe(2);
        OwnedTree.Find<TextInput>(dialog).ShouldNotBeNull().Text.ShouldBe(directory);
    }

    /// <summary>Verifies parent navigation is an exact lock-normalized Backspace command, leaving
    /// application-command chords available to the mounted route.</summary>
    [Theory]
    [InlineData(Modifiers.None, true)]
    [InlineData(Modifiers.CapsLock, true)]
    [InlineData(Modifiers.NumLock, true)]
    [InlineData(Modifiers.CapsLock | Modifiers.NumLock, true)]
    [InlineData(Modifiers.Control, false)]
    [InlineData(Modifiers.Alt, false)]
    [InlineData(Modifiers.Super, false)]
    [InlineData(Modifiers.Hyper, false)]
    [InlineData(Modifiers.Meta, false)]
    [InlineData(Modifiers.Control | Modifiers.Shift | Modifiers.CapsLock, false)]
    public async Task OnListKey_WhenBackspaceCarriesModifiers_NavigatesOnlyForPlainCommandAsync(
        Modifiers modifiers,
        bool expectedNavigation)
    {
        var parent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"picker-parent-{modifiers}"));
        var child = Path.Combine(parent, "child");
        var source = new FakeFilePickerFileSystem();
        source.AddDirectory(parent);
        source.AddDirectory(child);
        var dialog = new FilePickerDialog(new FilePickerOptions { InitialDirectory = child }, source);
        await using var surface = await ComponentSurface.MountAsync(
            dialog,
            new Size(100, 40),
            TestContext.Current.CancellationToken);
        await DialogWait.UntilAsync(surface, dialog, () => !dialog.IsLoading);
        var list = OwnedTree.Find<UiListView>(dialog).ShouldNotBeNull();
        await surface.UpdateAsync(
            () => surface.Application.Focus.Focus(list).ShouldBeTrue(),
            "focus file list");

        await surface.Keyboard.PressAsync(Code.Backspace, modifiers);

        if (expectedNavigation)
        {
            await DialogWait.UntilAsync(surface, dialog, () => !dialog.IsLoading && dialog.CurrentDirectory == parent);
        }

        dialog.CurrentDirectory.ShouldBe(expectedNavigation ? parent : child);
    }

    /// <summary>Verifies a single pointer click on a file selects it and enables Open without
    /// completing the dialog - select-then-commit means one click only proposes a choice, and the
    /// Open button (a keyboard Enter, or a second click) is still required to commit it.</summary>
    [Fact]
    public async Task SelectionAndInvocation_WhenFileIsClickedOnce_SelectsAndEnablesOpenWithoutCompletingAsync()
    {
        // Arrange
        var directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "picker-pointer-select"));
        var file = Path.Combine(directory, "notes.txt");
        var source = new FakeFilePickerFileSystem();
        source.AddDirectory(directory, new FilePickerEntry("notes.txt", file, isDirectory: false, isHidden: false));
        var dialog = new FilePickerDialog(new FilePickerOptions { InitialDirectory = directory }, source);
        await using var surface = await ComponentSurface.MountAsync(
            dialog,
            new Size(100, 40),
            TestContext.Current.CancellationToken);
        var list = OwnedTree.Find<UiListView>(dialog).ShouldNotBeNull();
        var openButton = OwnedTree.FindAll<Button>(dialog).Single(static button => button.IsDefault);

        // Act
        await surface.Pointer.ClickAsync(list, new Point(1, 0));

        // Assert one click only selects
        list.SelectedIndex.ShouldBe(0);
        dialog.SelectedPaths.ShouldHaveSingleItem().ShouldBe(file);
        openButton.IsEnabled.ShouldBeTrue();
        dialog.HasSelectedResult.ShouldBeFalse();

        // Act: the Open button commits the already-selected file - a regression guard for the
        // button path now that a pointer click alone no longer completes the dialog.
        await surface.Pointer.ClickAsync(openButton);

        // Assert
        dialog.HasSelectedResult.ShouldBeTrue();
        var result = dialog.SelectedResult.ShouldNotBeNull();
        result.IsAccepted.ShouldBeTrue();
        result.Paths.ShouldHaveSingleItem().ShouldBe(file);
    }

    /// <summary>Verifies a double pointer click on a file accepts the dialog exactly like invoking it
    /// with the keyboard, instead of only updating the selection and leaving the dialog open.</summary>
    [Fact]
    public async Task SelectionAndInvocation_WhenFileIsDoubleClickedWithPointer_AcceptsAsync()
    {
        // Arrange
        var directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "picker-pointer-accept"));
        var file = Path.Combine(directory, "notes.txt");
        var source = new FakeFilePickerFileSystem();
        source.AddDirectory(directory, new FilePickerEntry("notes.txt", file, isDirectory: false, isHidden: false));
        var dialog = new FilePickerDialog(new FilePickerOptions { InitialDirectory = directory }, source);
        await using var surface = await ComponentSurface.MountAsync(
            dialog,
            new Size(80, 24),
            TestContext.Current.CancellationToken);
        var list = OwnedTree.Find<UiListView>(dialog).ShouldNotBeNull();

        // Act
        await surface.Pointer.ClickAsync(list, new Point(1, 0));
        await surface.Pointer.ClickAsync(list, new Point(1, 0));

        // Assert
        dialog.HasSelectedResult.ShouldBeTrue();
        var result = dialog.SelectedResult.ShouldNotBeNull();
        result.IsAccepted.ShouldBeTrue();
        result.Paths.ShouldHaveSingleItem().ShouldBe(file);
    }

    /// <summary>Verifies the default Files selection mode behaves byte-identically to the picker's
    /// pre-existing behavior: a selected directory row never enables Open and never appears in the
    /// accepted selection - a regression guard for FileSelectionMode's addition.</summary>
    [Fact]
    public async Task SelectionAndInvocation_WhenModeIsFilesAndDirectoryIsClicked_NeverEnablesOpenAsync()
    {
        // Arrange
        var directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "picker-mode-files-default"));
        var subdirectory = Path.Combine(directory, "sub");
        var source = new FakeFilePickerFileSystem();
        source.AddDirectory(directory, new FilePickerEntry("sub", subdirectory, isDirectory: true, isHidden: false));
        source.AddDirectory(subdirectory);
        var dialog = new FilePickerDialog(
            new FilePickerOptions { InitialDirectory = directory, SelectionMode = FileSelectionMode.Files },
            source);
        await using var surface = await ComponentSurface.MountAsync(
            dialog,
            new Size(100, 40),
            TestContext.Current.CancellationToken);
        var list = OwnedTree.Find<UiListView>(dialog).ShouldNotBeNull();
        var openButton = OwnedTree.FindAll<Button>(dialog).Single(static button => button.IsDefault);

        // Act
        await surface.Pointer.ClickAsync(list, new Point(1, 0));

        // Assert
        list.SelectedIndex.ShouldBe(0);
        dialog.SelectedPaths.ShouldBeEmpty();
        openButton.IsEnabled.ShouldBeFalse();
    }

    /// <summary>Verifies a picker in Directories mode lets a single click on a directory row enable
    /// Open, and the Open Button then accepts that directory - the exact interaction FileSelectionMode
    /// exists to add, while navigation-on-invoke (double-click or Enter) is untouched by the mode.</summary>
    [Fact]
    public async Task SelectionAndInvocation_WhenModeIsDirectoriesAndDirectoryIsSelected_EnablesOpenAndAcceptsAsync()
    {
        // Arrange
        var directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "picker-mode-directories"));
        var subdirectory = Path.Combine(directory, "sub");
        var source = new FakeFilePickerFileSystem();
        source.AddDirectory(directory, new FilePickerEntry("sub", subdirectory, isDirectory: true, isHidden: false));
        source.AddDirectory(subdirectory);
        var dialog = new FilePickerDialog(
            new FilePickerOptions { InitialDirectory = directory, SelectionMode = FileSelectionMode.Directories },
            source);
        await using var surface = await ComponentSurface.MountAsync(
            dialog,
            new Size(100, 40),
            TestContext.Current.CancellationToken);
        var list = OwnedTree.Find<UiListView>(dialog).ShouldNotBeNull();
        var openButton = OwnedTree.FindAll<Button>(dialog).Single(static button => button.IsDefault);

        // Act: one click only selects
        await surface.Pointer.ClickAsync(list, new Point(1, 0));

        // Assert
        list.SelectedIndex.ShouldBe(0);
        dialog.SelectedPaths.ShouldHaveSingleItem().ShouldBe(subdirectory);
        openButton.IsEnabled.ShouldBeTrue();
        dialog.HasSelectedResult.ShouldBeFalse();

        // Act: the Open button commits the already-selected directory
        await surface.Pointer.ClickAsync(openButton);

        // Assert
        dialog.HasSelectedResult.ShouldBeTrue();
        var result = dialog.SelectedResult.ShouldNotBeNull();
        result.IsAccepted.ShouldBeTrue();
        result.Paths.ShouldHaveSingleItem().ShouldBe(subdirectory);
        result.Entries.ShouldHaveSingleItem().IsDirectory.ShouldBeTrue();
    }

    /// <summary>Verifies double-clicking a directory row in Directories mode still navigates into it
    /// instead of accepting it - only the Open Button commits a directory selection, matching the
    /// picker's existing select-then-commit model unchanged by FileSelectionMode.</summary>
    [Fact]
    public async Task SelectionAndInvocation_WhenModeIsDirectoriesAndDirectoryIsDoubleClicked_StillNavigatesAsync()
    {
        // Arrange
        var directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "picker-mode-directories-navigate"));
        var subdirectory = Path.Combine(directory, "sub");
        var source = new FakeFilePickerFileSystem();
        source.AddDirectory(directory, new FilePickerEntry("sub", subdirectory, isDirectory: true, isHidden: false));
        source.AddDirectory(subdirectory);
        var dialog = new FilePickerDialog(
            new FilePickerOptions { InitialDirectory = directory, SelectionMode = FileSelectionMode.Directories },
            source);
        await using var surface = await ComponentSurface.MountAsync(
            dialog,
            new Size(100, 40),
            TestContext.Current.CancellationToken);
        var list = OwnedTree.Find<UiListView>(dialog).ShouldNotBeNull();

        // Act: a double-click still navigates into the directory
        await surface.Pointer.ClickAsync(list, new Point(1, 0));
        await surface.Pointer.ClickAsync(list, new Point(1, 0));
        await DialogWait.UntilAsync(surface, dialog, () => !dialog.IsLoading && dialog.CurrentDirectory == subdirectory);

        // Assert
        dialog.CurrentDirectory.ShouldBe(subdirectory);
        dialog.HasSelectedResult.ShouldBeFalse();
    }

    /// <summary>Verifies a picker in FilesAndDirectories mode accepts both a selected file and a
    /// selected directory together into the same result.</summary>
    [Fact]
    public async Task SelectionAndInvocation_WhenModeIsFilesAndDirectories_AcceptsBothKindsAsync()
    {
        // Arrange
        var directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "picker-mode-both"));
        var subdirectory = Path.Combine(directory, "sub");
        var file = Path.Combine(directory, "notes.txt");
        var source = new FakeFilePickerFileSystem();
        source.AddDirectory(
            directory,
            new FilePickerEntry("sub", subdirectory, isDirectory: true, isHidden: false),
            new FilePickerEntry("notes.txt", file, isDirectory: false, isHidden: false));
        source.AddDirectory(subdirectory);
        var dialog = new FilePickerDialog(
            new FilePickerOptions
            {
                InitialDirectory = directory,
                AllowMultiple = true,
                SelectionMode = FileSelectionMode.FilesAndDirectories
            },
            source);
        await using var surface = await ComponentSurface.MountAsync(
            dialog,
            new Size(100, 40),
            TestContext.Current.CancellationToken);
        var openButton = OwnedTree.FindAll<Button>(dialog).Single(static button => button.IsDefault);
        var directoryRow = OwnedTree.FindAll<ControlText>(dialog)
            .Single(text => text.Content.StartsWith("▸ sub", StringComparison.Ordinal));
        var fileRow = OwnedTree.FindAll<ControlText>(dialog)
            .Single(text => text.Content.StartsWith("· notes.txt", StringComparison.Ordinal));

        // Act: select both rows with a Control-held second click, exactly like any other
        // multi-selection in a Multiple-mode ListView
        await surface.Pointer.ClickAsync(directoryRow.Parent.ShouldNotBeNull());
        await surface.Pointer.ClickAsync(fileRow.Parent.ShouldNotBeNull(), Modifiers.Control);
        await surface.Pointer.ClickAsync(openButton);

        // Assert
        dialog.HasSelectedResult.ShouldBeTrue();
        var result = dialog.SelectedResult.ShouldNotBeNull();
        result.IsAccepted.ShouldBeTrue();
        result.Paths.Count.ShouldBe(2);
        result.Paths.ShouldContain(subdirectory);
        result.Paths.ShouldContain(file);
        result.Entries.Count.ShouldBe(2);
        result.Entries.Count(static entry => entry.IsDirectory).ShouldBe(1);
        result.Entries.Count(static entry => !entry.IsDirectory).ShouldBe(1);
    }

    /// <summary>Verifies navigating into a directory whose name contains a control character - legal
    /// filesystem data on POSIX - degrades to a status message instead of force-stopping the
    /// application when the unrepresentable name reaches TextInput.Text.</summary>
    [Fact]
    public async Task SelectionAndInvocation_WhenDirectoryNameHasControlCharacter_DoesNotForceStopAsync()
    {
        // Arrange
        var directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "picker-control-nav"));
        var childName = "sub\u001bdir";
        var child = Path.Combine(directory, childName);
        var source = new FakeFilePickerFileSystem();
        source.AddDirectory(
            directory,
            new FilePickerEntry(childName, child, isDirectory: true, isHidden: false));
        source.AddDirectory(child);
        var dialog = new FilePickerDialog(new FilePickerOptions { InitialDirectory = directory }, source);
        await using var surface = await ComponentSurface.MountAsync(
            dialog,
            new Size(80, 24),
            TestContext.Current.CancellationToken);
        var list = OwnedTree.Find<UiListView>(dialog).ShouldNotBeNull();

        // Act
        await surface.UpdateAsync(
            () =>
            {
                list.SelectedIndex = 0;
                list.ActivateCurrent(ActivationCause.Keyboard, Code.Enter, Modifiers.None).ShouldBeTrue();
            },
            "invoke control-character directory");
        await surface.UpdateAsync(static () => { }, "settle control-character directory load");

        // Assert
        dialog.IsDisposed.ShouldBeFalse();
        dialog.CurrentDirectory.ShouldBe(child);
        dialog.Status.ShouldStartWith("Cannot display this directory's name.");
        OwnedTree.Find<TextInput>(dialog).ShouldNotBeNull().Text.ShouldBe(directory);
    }

    /// <summary>Verifies every dialog source receives deterministic directory-first ordering.</summary>
    [Fact]
    public async Task OnAttached_WhenSourceOrderIsUnsorted_OrdersDirectoriesAndNamesAsync()
    {
        var directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "picker-order"));
        var source = new FakeFilePickerFileSystem();
        source.AddDirectory(
            directory,
            new FilePickerEntry("z.cs", Path.Combine(directory, "z.cs"), false, false),
            new FilePickerEntry("Alpha", Path.Combine(directory, "Alpha"), true, false),
            new FilePickerEntry("a.cs", Path.Combine(directory, "a.cs"), false, false));
        var dialog = new FilePickerDialog(new FilePickerOptions { InitialDirectory = directory }, source);

        await using var surface = await ComponentSurface.MountAsync(
            dialog,
            new Size(80, 24),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(static () => { }, "settle ordered file picker load");

        var list = OwnedTree.Find<UiListView>(dialog).ShouldNotBeNull();
        list.Items.Cast<FilePickerEntry>().Select(static entry => entry.Name)
            .ShouldBe(["Alpha", "a.cs", "z.cs"]);
    }

    /// <summary>Verifies a refresh remaps selection by canonical path instead of replacement object identity.</summary>
    [Fact]
    public async Task Reload_WhenSelectedEntryStillExists_PreservesSelectionAsync()
    {
        var directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "picker-selection-refresh"));
        var selectedPath = Path.Combine(directory, "Keep.cs");
        var source = new FakeFilePickerFileSystem();
        source.AddDirectory(
            directory,
            new FilePickerEntry("Keep.cs", selectedPath, false, false),
            new FilePickerEntry("Other.cs", Path.Combine(directory, "Other.cs"), false, false));
        var dialog = new FilePickerDialog(
            new FilePickerOptions { InitialDirectory = directory },
            source);

        await using var surface = await ComponentSurface.MountAsync(
            dialog,
            new Size(80, 24),
            TestContext.Current.CancellationToken);
        var list = OwnedTree.Find<UiListView>(dialog).ShouldNotBeNull();
        var hidden = OwnedTree.Find<CheckBox>(dialog).ShouldNotBeNull();
        await surface.UpdateAsync(() => list.SelectedIndex = 0, "select refresh candidate");

        source.AddDirectory(
            directory,
            new FilePickerEntry("Keep.cs", selectedPath, false, false),
            new FilePickerEntry("Other.cs", Path.Combine(directory, "Other.cs"), false, false),
            new FilePickerEntry(".hidden.cs", Path.Combine(directory, ".hidden.cs"), false, true));

        await surface.UpdateAsync(() => hidden.IsChecked = true, "refresh selected directory");
        await surface.UpdateAsync(static () => { }, "settle refreshed selection");

        dialog.SelectedPaths.ShouldHaveSingleItem().ShouldBe(selectedPath);
        list.SelectedItem.ShouldBeOfType<FilePickerEntry>().FullPath.ShouldBe(selectedPath);
    }

    /// <summary>Verifies a refresh that reveals a newly-added case-variant sibling (e.g. "Readme.txt"
    /// alongside a selected "readme.txt") still remaps the preserved selection to the original file
    /// rather than colliding the two case-variant rows into the same BuildIndexMap bucket - the
    /// FilePickerEntry identity regression this fix guards against.</summary>
    [Fact]
    public async Task Reload_WhenCaseVariantSiblingAppears_PreservesSelectionOnOriginalCaseAsync()
    {
        var directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "picker-case-variant-refresh"));
        var lowerPath = Path.Combine(directory, "readme.txt");
        var upperPath = Path.Combine(directory, "Readme.txt");
        var source = new FakeFilePickerFileSystem();
        source.AddDirectory(
            directory,
            new FilePickerEntry("readme.txt", lowerPath, false, false));
        var dialog = new FilePickerDialog(
            new FilePickerOptions { InitialDirectory = directory },
            source);

        await using var surface = await ComponentSurface.MountAsync(
            dialog,
            new Size(80, 24),
            TestContext.Current.CancellationToken);
        var list = OwnedTree.Find<UiListView>(dialog).ShouldNotBeNull();
        var hidden = OwnedTree.Find<CheckBox>(dialog).ShouldNotBeNull();
        await surface.UpdateAsync(() => list.SelectedIndex = 0, "select the original case-variant file");

        source.AddDirectory(
            directory,
            new FilePickerEntry("readme.txt", lowerPath, false, false),
            new FilePickerEntry("Readme.txt", upperPath, false, false));

        await surface.UpdateAsync(() => hidden.IsChecked = true, "reveal the newly-added case-variant sibling");
        await surface.UpdateAsync(static () => { }, "settle case-variant reload");

        dialog.SelectedPaths.ShouldHaveSingleItem().ShouldBe(lowerPath);
        list.SelectedItem.ShouldBeOfType<FilePickerEntry>().FullPath.ShouldBe(lowerPath);
    }

    /// <summary>Verifies file selection publishes paths while directory invocation replaces the snapshot.</summary>
    [Fact]
    public async Task SelectionAndInvocation_WhenRowsDiffer_SelectsFilesAndNavigatesDirectoriesAsync()
    {
        // Arrange
        var directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "picker-navigation"));
        var child = Path.Combine(directory, "src");
        var file = Path.Combine(directory, "Program.cs");
        var source = new FakeFilePickerFileSystem();
        source.AddDirectory(
            directory,
            new FilePickerEntry("src", child, isDirectory: true, isHidden: false),
            new FilePickerEntry("Program.cs", file, isDirectory: false, isHidden: false));
        source.AddDirectory(
            child,
            new FilePickerEntry("Child.cs", Path.Combine(child, "Child.cs"), isDirectory: false, isHidden: false));
        var dialog = new FilePickerDialog(new FilePickerOptions { InitialDirectory = directory }, source);
        await using var surface = await ComponentSurface.MountAsync(
            dialog,
            new Size(80, 24),
            TestContext.Current.CancellationToken);
        var list = OwnedTree.Find<UiListView>(dialog).ShouldNotBeNull();

        // Act
        await surface.UpdateAsync(() => list.SelectedIndex = 1, "select file");

        // Assert
        dialog.SelectedPaths.ShouldHaveSingleItem().ShouldBe(file);

        // Act: a single pointer click on the directory row selects it without navigating - the
        // select-then-commit contract extended from files to directories.
        await surface.Pointer.ClickAsync(list, new Point(1, 0));

        // Assert: still in the same directory, only the row selected.
        list.SelectedIndex.ShouldBe(0);
        dialog.CurrentDirectory.ShouldBe(directory);
        dialog.SelectedPaths.ShouldBeEmpty();

        // Act: Enter still navigates the selected directory.
        await surface.UpdateAsync(
            () => list.ActivateCurrent(ActivationCause.Keyboard, Code.Enter, Modifiers.None).ShouldBeTrue(),
            "invoke directory with Enter");
        await surface.UpdateAsync(static () => { }, "settle child directory");
        dialog.CurrentDirectory.ShouldBe(child);
        dialog.SelectedPaths.ShouldBeEmpty();
        list.Items.Count.ShouldBe(1);
    }

    /// <summary>Verifies a double pointer click on a directory navigates into it, mirroring Enter's
    /// commit behavior now that a single click only selects.</summary>
    [Fact]
    public async Task SelectionAndInvocation_WhenDirectoryIsDoubleClickedWithPointer_NavigatesAsync()
    {
        // Arrange
        var directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "picker-navigation-double-click"));
        var child = Path.Combine(directory, "src");
        var source = new FakeFilePickerFileSystem();
        source.AddDirectory(
            directory,
            new FilePickerEntry("src", child, isDirectory: true, isHidden: false));
        source.AddDirectory(
            child,
            new FilePickerEntry("Child.cs", Path.Combine(child, "Child.cs"), isDirectory: false, isHidden: false));
        var dialog = new FilePickerDialog(new FilePickerOptions { InitialDirectory = directory }, source);
        await using var surface = await ComponentSurface.MountAsync(
            dialog,
            new Size(80, 24),
            TestContext.Current.CancellationToken);
        var list = OwnedTree.Find<UiListView>(dialog).ShouldNotBeNull();

        // Act
        await surface.Pointer.ClickAsync(list, new Point(1, 0));
        await surface.Pointer.ClickAsync(list, new Point(1, 0));
        await surface.UpdateAsync(static () => { }, "settle child directory");

        // Assert
        dialog.CurrentDirectory.ShouldBe(child);
        list.Items.Count.ShouldBe(1);
    }

    /// <summary>Verifies hidden and filter controls reload the current directory from semantic state.</summary>
    [Fact]
    public async Task Options_WhenChanged_ReloadCurrentDirectoryAndClearSelectionAsync()
    {
        // Arrange
        var directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "picker-options"));
        var source = new FakeFilePickerFileSystem();
        source.AddDirectory(
            directory,
            new FilePickerEntry("Visible.cs", Path.Combine(directory, "Visible.cs"), false, false),
            new FilePickerEntry("notes.txt", Path.Combine(directory, "notes.txt"), false, false),
            new FilePickerEntry(".hidden.cs", Path.Combine(directory, ".hidden.cs"), false, true));
        var dialog = new FilePickerDialog(
            new FilePickerOptions
            {
                InitialDirectory = directory,
                Filters = [new FilePickerFilter("Sources", "*.cs"), FilePickerFilter.AllFiles]
            },
            source);
        await using var surface = await ComponentSurface.MountAsync(
            dialog,
            new Size(80, 24),
            TestContext.Current.CancellationToken);
        var list = OwnedTree.Find<UiListView>(dialog).ShouldNotBeNull();
        var hidden = OwnedTree.Find<CheckBox>(dialog).ShouldNotBeNull();
        var filter = OwnedTree.Find<ComboBox>(dialog).ShouldNotBeNull();

        // Act and assert
        list.Items.Count.ShouldBe(1);
        await surface.UpdateAsync(() => hidden.IsChecked = true, "show hidden entries");
        await surface.UpdateAsync(static () => { }, "settle hidden reload");
        dialog.ShowHidden.ShouldBeTrue();
        list.Items.Count.ShouldBe(2);

        await surface.UpdateAsync(() => filter.SelectedIndex = 1, "select all-files filter");
        await surface.UpdateAsync(static () => { }, "settle filter reload");
        dialog.FilterIndex.ShouldBe(1);
        list.Items.Count.ShouldBe(3);
        dialog.SelectedPaths.ShouldBeEmpty();
    }

    /// <summary>Verifies multiple mode publishes every selected file in stable row order.</summary>
    [Fact]
    public async Task Selection_WhenMultipleFilesAreChosen_PublishesAllFilePathsInDisplayOrderAsync()
    {
        // Arrange
        var directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "picker-multiple"));
        var first = Path.Combine(directory, "A.cs");
        var second = Path.Combine(directory, "B.cs");
        var source = new FakeFilePickerFileSystem();
        source.AddDirectory(
            directory,
            new FilePickerEntry("A.cs", first, false, false),
            new FilePickerEntry("B.cs", second, false, false));
        var dialog = new FilePickerDialog(
            new FilePickerOptions { InitialDirectory = directory, AllowMultiple = true },
            source);
        await using var surface = await ComponentSurface.MountAsync(
            dialog,
            new Size(80, 24),
            TestContext.Current.CancellationToken);
        var list = OwnedTree.Find<UiListView>(dialog).ShouldNotBeNull();

        // Act
        await surface.UpdateAsync(
            () =>
            {
                list.SetSelected(0, true).ShouldBeTrue();
                list.SetSelected(1, true).ShouldBeTrue();
            },
            "select multiple files");

        // Assert
        dialog.SelectedPaths.Count.ShouldBe(2);
        dialog.SelectedPaths[0].ShouldBe(first);
        dialog.SelectedPaths[1].ShouldBe(second);
        dialog.Status.ShouldBe("2 files selected");
    }

    /// <summary>Verifies a failed replacement retains the committed directory, rows, and selection.</summary>
    [Fact]
    public async Task Reload_WhenEnumerationFails_RetainsLastSuccessfulSnapshotAsync()
    {
        // Arrange
        var directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "picker-failure"));
        var file = Path.Combine(directory, "Program.cs");
        var source = new FakeFilePickerFileSystem();
        source.AddDirectory(directory, new FilePickerEntry("Program.cs", file, false, false));
        var dialog = new FilePickerDialog(new FilePickerOptions { InitialDirectory = directory }, source);
        await using var surface = await ComponentSurface.MountAsync(
            dialog,
            new Size(80, 24),
            TestContext.Current.CancellationToken);
        var list = OwnedTree.Find<UiListView>(dialog).ShouldNotBeNull();
        var hidden = OwnedTree.Find<CheckBox>(dialog).ShouldNotBeNull();
        await surface.UpdateAsync(() => list.SelectedIndex = 0, "select retained file");
        source.FailNext(directory, new UnauthorizedAccessException("blocked"));

        // Act
        await surface.UpdateAsync(() => hidden.IsChecked = true, "request failing reload");
        await DialogWait.UntilAsync(surface, dialog, () => !dialog.IsLoading);

        // Assert
        dialog.CurrentDirectory.ShouldBe(directory);
        list.Items.Count.ShouldBe(1);
        dialog.SelectedPaths.ShouldHaveSingleItem().ShouldBe(file);
        dialog.Status.ShouldContain("blocked");
    }

    /// <summary>Verifies a submitted missing directory becomes inline status instead of escaping routed input.</summary>
    [Fact]
    public async Task PathSubmission_WhenEnumerationThrowsSynchronously_RetainsSnapshotAndReportsErrorAsync()
    {
        // Arrange
        var directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "picker-path-valid"));
        var missing = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "picker-path-missing"));
        var file = Path.Combine(directory, "Program.cs");
        var source = new FakeFilePickerFileSystem();
        source.AddDirectory(directory, new FilePickerEntry("Program.cs", file, false, false));
        var dialog = new FilePickerDialog(new FilePickerOptions { InitialDirectory = directory }, source);
        await using var surface = await ComponentSurface.MountAsync(
            dialog,
            new Size(80, 24),
            TestContext.Current.CancellationToken);
        var path = OwnedTree.Find<TextInput>(dialog).ShouldNotBeNull();
        var list = OwnedTree.Find<UiListView>(dialog).ShouldNotBeNull();

        // Act
        await surface.UpdateAsync(
            () =>
            {
                path.Text = missing;
                surface.Application.Focus.Focus(path).ShouldBeTrue();
            },
            "enter missing directory path");
        await surface.Keyboard.PressAsync(Code.Enter);

        // Assert
        dialog.CurrentDirectory.ShouldBe(directory);
        list.Items.Count.ShouldBe(1);
        dialog.Status.ShouldContain("does not exist");
    }

    /// <summary>Verifies a late completion from a cancelled generation cannot overwrite newer rows.</summary>
    [Fact]
    public async Task Reload_WhenOlderRequestCompletesLast_IgnoresStaleSnapshotAsync()
    {
        // Arrange
        var directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "picker-stale"));
        var oldPath = Path.Combine(directory, "Old.cs");
        var freshPath = Path.Combine(directory, "Fresh.cs");
        var stalePath = Path.Combine(directory, "Stale.cs");
        var source = new FakeFilePickerFileSystem();
        source.AddDirectory(directory, new FilePickerEntry("Old.cs", oldPath, false, false));
        var dialog = new FilePickerDialog(
            new FilePickerOptions
            {
                InitialDirectory = directory,
                Filters = [new FilePickerFilter("Sources", "*.cs"), FilePickerFilter.AllFiles]
            },
            source);
        await using var surface = await ComponentSurface.MountAsync(
            dialog,
            new Size(80, 24),
            TestContext.Current.CancellationToken);
        var list = OwnedTree.Find<UiListView>(dialog).ShouldNotBeNull();
        var hidden = OwnedTree.Find<CheckBox>(dialog).ShouldNotBeNull();
        var filter = OwnedTree.Find<ComboBox>(dialog).ShouldNotBeNull();
        var stale = source.DeferNext(directory);
        source.AddDirectory(directory, new FilePickerEntry("Fresh.cs", freshPath, false, false));

        // Act
        await surface.UpdateAsync(() => hidden.IsChecked = true, "start deferred reload");
        dialog.IsLoading.ShouldBeTrue();
        await surface.UpdateAsync(() => filter.SelectedIndex = 1, "replace deferred reload");
        await DialogWait.UntilAsync(surface, dialog, () => !dialog.IsLoading);
        _ = stale.TrySetResult([new FilePickerEntry("Stale.cs", stalePath, false, false)]);
        await surface.UpdateAsync(static () => { }, "deliver stale completion");

        // Assert
        dialog.CurrentDirectory.ShouldBe(directory);
        _ = list.Items.ShouldHaveSingleItem();
        ((FilePickerEntry) list.Items[0]!).Name.ShouldBe("Fresh.cs");
    }

    /// <summary>Verifies a directory load that completes after the owning dispatcher has already
    /// been disposed does not fault the fire-and-forget load-observation loop, matching the
    /// failure path's own dispatcher-disposed guard.</summary>
    [Fact]
    public async Task Reload_WhenDispatcherDisposesBeforeCompletion_DoesNotFaultAsync()
    {
        // Arrange
        var directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "picker-disposed-dispatcher"));
        var file = Path.Combine(directory, "Program.cs");
        var source = new FakeFilePickerFileSystem();
        source.AddDirectory(directory, new FilePickerEntry("Program.cs", file, false, false));
        var dialog = new FilePickerDialog(new FilePickerOptions { InitialDirectory = directory }, source);
        var surface = await ComponentSurface.MountAsync(
            dialog,
            new Size(80, 24),
            TestContext.Current.CancellationToken);
        var hidden = OwnedTree.Find<CheckBox>(dialog).ShouldNotBeNull();
        var deferred = source.DeferNext(directory);

        try
        {
            // Act
            await surface.UpdateAsync(() => hidden.IsChecked = true, "start deferred reload");
            dialog.IsLoading.ShouldBeTrue();
            var observation = dialog.LastLoadObservation.ShouldNotBeNull();

            await surface.Application.Dispatcher.DisposeAsync();
            _ = deferred.TrySetResult([new FilePickerEntry("Program.cs", file, false, false)]);

            await observation.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            // Assert
            observation.IsFaulted.ShouldBeFalse();
        }
        finally
        {
            try
            {
                await surface.DisposeAsync();
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }

    /// <summary>Verifies a directory load that completes while the owning dispatcher's bounded
    /// queue stays saturated through both the success-path CommitLoad post and its own bridging
    /// retry drops the fault instead of letting it escape and fault the unobserved
    /// <see cref="FileDialogBase{TResult}.LastLoadObservation"/> - the documented, accepted edge
    /// for an already-saturated queue with nothing draining it, distinct from the disposed-dispatcher
    /// path above.</summary>
    [Fact]
    public async Task Reload_WhenDispatcherQueueIsFullAtCompletionOnBothAttempts_DropsTheFaultAsync()
    {
        // Arrange
        var directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "picker-saturated-queue"));
        var file = Path.Combine(directory, "Program.cs");
        var source = new FakeFilePickerFileSystem();
        source.AddDirectory(directory, new FilePickerEntry("Program.cs", file, false, false));
        var dialog = new FilePickerDialog(new FilePickerOptions { InitialDirectory = directory }, source);
        var surface = await ComponentSurface.MountAsync(
            dialog,
            new Size(80, 24),
            TestContext.Current.CancellationToken);
        var hidden = OwnedTree.Find<CheckBox>(dialog).ShouldNotBeNull();
        var deferred = source.DeferNext(directory);

        try
        {
            // Act
            await surface.UpdateAsync(() => hidden.IsChecked = true, "start deferred reload");
            dialog.IsLoading.ShouldBeTrue();
            var observation = dialog.LastLoadObservation.ShouldNotBeNull();
            var dispatcher = surface.Application.Dispatcher;

            // Saturate the queue while the load is still deferred, then resolve it - unlike the
            // dispatcher-disposed test above, the mounted surface's own background activity means
            // the queue's remaining capacity is not known in advance, so fill until it provably
            // rejects a post rather than assuming a fixed number of free slots.
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

                Should.NotThrow(
                    () => { _ = deferred.TrySetResult([new FilePickerEntry("Program.cs", file, false, false)]); });

                // The continuation resumes off the dispatcher (ConfigureAwait(false)), so give it
                // a moment to reach its own post while the queue is still saturated.
                await Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken);
            }
            finally
            {
                release.Set();
            }

            // Assert
            //
            // The saturated queue never drains during this window (the hostage stays blocked until
            // release.Set() above), so both the success-path post and its own bridging retry find
            // it full - the deliberately accepted double-failure edge, dropped rather than retried
            // indefinitely, rather than escaping and faulting this unobserved task.
            await observation.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            observation.IsCompletedSuccessfully.ShouldBeTrue();

            // The posted commit never got a chance to run, so the dialog stays loading instead of
            // advancing - a sane, non-corrupted outcome rather than a silent, permanent stall.
            dialog.IsLoading.ShouldBeTrue();

            // The backlog of filler posts ahead of the retry in the FIFO queue may still be large
            // (possibly under parallel test-suite load) even though the observation has already
            // completed - and disposal below needs a free slot of its own. Retry briefly rather
            // than assuming the backlog is gone by now.
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
            try
            {
                await surface.DisposeAsync();
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }

    /// <summary>Verifies a directory load that fails while the owning dispatcher's bounded queue
    /// stays saturated through both the failure-path CommitLoadFailure post and its own bridging
    /// retry drops the fault instead of letting it escape and fault the unobserved
    /// <see cref="FileDialogBase{TResult}.LastLoadObservation"/> - the sibling edge case to the
    /// success-commit post covered immediately above.</summary>
    [Fact]
    public async Task Reload_WhenDispatcherQueueIsFullAtFailedCompletionOnBothAttempts_DropsTheFaultAsync()
    {
        // Arrange
        var directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "picker-saturated-queue-failure"));
        var file = Path.Combine(directory, "Program.cs");
        var source = new FakeFilePickerFileSystem();
        source.AddDirectory(directory, new FilePickerEntry("Program.cs", file, false, false));
        var dialog = new FilePickerDialog(new FilePickerOptions { InitialDirectory = directory }, source);
        var surface = await ComponentSurface.MountAsync(
            dialog,
            new Size(80, 24),
            TestContext.Current.CancellationToken);
        var hidden = OwnedTree.Find<CheckBox>(dialog).ShouldNotBeNull();
        var deferred = source.DeferNext(directory);

        try
        {
            // Act
            await surface.UpdateAsync(() => hidden.IsChecked = true, "start deferred reload");
            dialog.IsLoading.ShouldBeTrue();
            var observation = dialog.LastLoadObservation.ShouldNotBeNull();
            var dispatcher = surface.Application.Dispatcher;

            // Saturate the queue while the load is still deferred, then fail it - unlike the
            // dispatcher-disposed test above, the mounted surface's own background activity means
            // the queue's remaining capacity is not known in advance, so fill until it provably
            // rejects a post rather than assuming a fixed number of free slots.
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

                Should.NotThrow(
                    () => { _ = deferred.TrySetException(new IOException("simulated enumeration failure")); });

                // The continuation resumes off the dispatcher (ConfigureAwait(false)), so give it
                // a moment to reach its own post while the queue is still saturated.
                await Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken);
            }
            finally
            {
                release.Set();
            }

            // Assert
            //
            // The saturated queue never drains during this window (the hostage stays blocked until
            // release.Set() above), so both the failure-path post and its own bridging retry find
            // it full - the deliberately accepted double-failure edge, dropped rather than retried
            // indefinitely, rather than escaping and faulting this unobserved task.
            await observation.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            observation.IsCompletedSuccessfully.ShouldBeTrue();

            // The posted failure commit never got a chance to run, so the dialog stays loading
            // instead of advancing - a sane, non-corrupted outcome rather than a silent, permanent
            // stall.
            dialog.IsLoading.ShouldBeTrue();

            // The backlog of filler posts ahead of the retry in the FIFO queue may still be large
            // (possibly under parallel test-suite load) even though the observation has already
            // completed - and disposal below needs a free slot of its own. Retry briefly rather
            // than assuming the backlog is gone by now.
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
            try
            {
                await surface.DisposeAsync();
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }

    /// <summary>Verifies the success-commit post's bridging retry - given a genuine chance to
    /// succeed once the saturated slot frees, exactly as a live dispatcher queue drains in
    /// practice - reaches <see cref="Dispatcher.UnhandledException"/> with the original "queue is
    /// full" failure, the same outcome a synchronous dispatcher-callback failure already produces.
    /// The commit itself never runs (only the rethrow was ever queued), so the dialog stays stuck
    /// Loading - loading state is deliberately not reset as a substitute for this contract.</summary>
    [Fact]
    public async Task Reload_WhenDispatcherQueueIsFullAtCompletionThenFrees_BridgesToUnhandledExceptionAsync()
    {
        // Arrange
        var directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "picker-saturated-queue-retry"));
        var file = Path.Combine(directory, "Program.cs");
        var source = new FakeFilePickerFileSystem();
        source.AddDirectory(directory, new FilePickerEntry("Program.cs", file, false, false));
        var dialog = new FilePickerDialog(new FilePickerOptions { InitialDirectory = directory }, source);
        var surface = await ComponentSurface.MountAsync(
            dialog,
            new Size(80, 24),
            TestContext.Current.CancellationToken);
        var hidden = OwnedTree.Find<CheckBox>(dialog).ShouldNotBeNull();
        var deferred = source.DeferNext(directory);

        try
        {
            // Act
            await surface.UpdateAsync(() => hidden.IsChecked = true, "start deferred reload");
            dialog.IsLoading.ShouldBeTrue();
            var observation = dialog.LastLoadObservation.ShouldNotBeNull();
            var dispatcher = surface.Application.Dispatcher;

            // Saturate the queue while the load is still deferred - same shape as the sibling
            // "drops the fault" test above - except this time the hostage is released
            // deterministically via PostRetryHookForTests, right when the first commit-post
            // attempt fails, so the bridging retry gets a genuine chance to succeed instead of
            // finding the queue full a second time.
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var hostageRelease = new ManualResetEventSlim();
            dispatcher.Post(() =>
            {
                entered.SetResult();
                hostageRelease.Wait();
            });
            await entered.Task.WaitAsync(TestContext.Current.CancellationToken);

            // The queue's remaining capacity beyond the hostage isn't known in advance (the
            // mounted surface has its own background activity), so fill until it provably rejects
            // a post. Whichever filler actually lands in the one free slot signals fillerDrained
            // once it runs - the rest never get the chance to.
            var fillerDrained = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            try
            {
                while (true)
                {
                    dispatcher.Post(() => fillerDrained.TrySetResult());
                }
            }
            catch (InvalidOperationException)
            {
            }

            dialog.PostRetryHookForTests = () =>
            {
                hostageRelease.Set();
                _ = fillerDrained.Task.Wait(TimeSpan.FromSeconds(5));
            };

            var unhandled = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
            dispatcher.UnhandledException += (_, eventArgs) => unhandled.TrySetResult(eventArgs.Exception);

            Should.NotThrow(
                () => { _ = deferred.TrySetResult([new FilePickerEntry("Program.cs", file, false, false)]); });

            // Assert
            var reported = await unhandled.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            reported.ShouldBeOfType<InvalidOperationException>().Message.ShouldBe("The dispatcher queue is full.");
            await observation.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            observation.IsCompletedSuccessfully.ShouldBeTrue();
            dialog.IsLoading.ShouldBeTrue();
        }
        finally
        {
            try
            {
                await surface.DisposeAsync();
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }

    /// <summary>Verifies the failure-commit post's bridging retry, the sibling edge case to the
    /// success-commit post covered immediately above, also reaches
    /// <see cref="Dispatcher.UnhandledException"/> once the saturated slot frees.</summary>
    [Fact]
    public async Task Reload_WhenDispatcherQueueIsFullAtFailedCompletionThenFrees_BridgesToUnhandledExceptionAsync()
    {
        // Arrange
        var directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "picker-saturated-queue-retry-failure"));
        var file = Path.Combine(directory, "Program.cs");
        var source = new FakeFilePickerFileSystem();
        source.AddDirectory(directory, new FilePickerEntry("Program.cs", file, false, false));
        var dialog = new FilePickerDialog(new FilePickerOptions { InitialDirectory = directory }, source);
        var surface = await ComponentSurface.MountAsync(
            dialog,
            new Size(80, 24),
            TestContext.Current.CancellationToken);
        var hidden = OwnedTree.Find<CheckBox>(dialog).ShouldNotBeNull();
        var deferred = source.DeferNext(directory);

        try
        {
            // Act
            await surface.UpdateAsync(() => hidden.IsChecked = true, "start deferred reload");
            dialog.IsLoading.ShouldBeTrue();
            var observation = dialog.LastLoadObservation.ShouldNotBeNull();
            var dispatcher = surface.Application.Dispatcher;

            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var hostageRelease = new ManualResetEventSlim();
            dispatcher.Post(() =>
            {
                entered.SetResult();
                hostageRelease.Wait();
            });
            await entered.Task.WaitAsync(TestContext.Current.CancellationToken);

            // The queue's remaining capacity beyond the hostage isn't known in advance (the
            // mounted surface has its own background activity), so fill until it provably rejects
            // a post. Whichever filler actually lands in the one free slot signals fillerDrained
            // once it runs - the rest never get the chance to.
            var fillerDrained = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            try
            {
                while (true)
                {
                    dispatcher.Post(() => fillerDrained.TrySetResult());
                }
            }
            catch (InvalidOperationException)
            {
            }

            dialog.PostRetryHookForTests = () =>
            {
                hostageRelease.Set();
                _ = fillerDrained.Task.Wait(TimeSpan.FromSeconds(5));
            };

            var unhandled = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
            dispatcher.UnhandledException += (_, eventArgs) => unhandled.TrySetResult(eventArgs.Exception);

            Should.NotThrow(
                () => { _ = deferred.TrySetException(new IOException("simulated enumeration failure")); });

            // Assert
            var reported = await unhandled.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            reported.ShouldBeOfType<InvalidOperationException>().Message.ShouldBe("The dispatcher queue is full.");
            await observation.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            observation.IsCompletedSuccessfully.ShouldBeTrue();
            dialog.IsLoading.ShouldBeTrue();
        }
        finally
        {
            try
            {
                await surface.DisposeAsync();
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }

    /// <summary>Verifies every owned-part style defaults to null and its resolved value follows the
    /// owned part's own semantic profile until an explicit local style is assigned.</summary>
    [Fact]
    public void OwnedPartStyles_WhenUnset_FollowTheOwnedPartsOwnSemanticAppearance()
    {
        using var dialog = new FilePickerDialog(null, new FakeFilePickerFileSystem());
        using var expectedButton = new Button();
        using var expectedCheckBox = new CheckBox();

        dialog.CancelButtonStyle.ShouldBeNull();
        dialog.ShowHiddenCheckBoxStyle.ShouldBeNull();
        dialog.FileListScrollBarStyle.ShouldBeNull();
        dialog.FilterScrollBarStyle.ShouldBeNull();
        dialog.OpenButtonStyle.ShouldBeNull();
        dialog.ActualCancelButtonStyle.ShouldBe(expectedButton.ActualStyle);
        dialog.ActualShowHiddenCheckBoxStyle.ShouldBe(expectedCheckBox.ActualStyle);
        dialog.ActualOpenButtonStyle.ShouldBe(expectedButton.ActualStyle);
    }

    /// <summary>Verifies each owned-part style propagates to its owned control and resolves back
    /// through the dialog's own Actual* property.</summary>
    [Fact]
    public void OwnedPartStyles_WhenSet_PropagateToTheOwnedPart()
    {
        using var dialog = new FilePickerDialog(null, new FakeFilePickerFileSystem());
        var buttonStyle = ButtonStyle.Standard with { Padding = new Thickness(horizontal: 2, vertical: 1) };
        var checkBoxStyle = CheckBoxStyle.Default;
        var scrollBarStyle = ScrollBarStyle.Default;

        dialog.CancelButtonStyle = buttonStyle;
        dialog.ShowHiddenCheckBoxStyle = checkBoxStyle;
        dialog.FileListScrollBarStyle = scrollBarStyle;
        dialog.FilterScrollBarStyle = scrollBarStyle;
        dialog.OpenButtonStyle = buttonStyle;

        dialog.ActualCancelButtonStyle.ShouldBe(buttonStyle);
        dialog.ActualShowHiddenCheckBoxStyle.ShouldBe(checkBoxStyle);
        dialog.ActualFileListScrollBarStyle.ShouldBe(scrollBarStyle);
        dialog.ActualFilterScrollBarStyle.ShouldBe(scrollBarStyle);
        dialog.ActualOpenButtonStyle.ShouldBe(buttonStyle);

        var list = OwnedTree.Find<UiListView>(dialog).ShouldNotBeNull();
        var filter = OwnedTree.Find<ComboBox>(dialog).ShouldNotBeNull();
        var hidden = OwnedTree.Find<CheckBox>(dialog).ShouldNotBeNull();
        list.ScrollBarStyle.ShouldBe(scrollBarStyle);
        filter.ScrollBarStyle.ShouldBe(scrollBarStyle);
        hidden.Style.ShouldBe(checkBoxStyle);
    }

    /// <summary>Verifies FilePickerOptions carries every owned-part style, plus the aggregate
    /// <see cref="FilePickerOptions.Style"/>, through construction and through Copy(), matching how
    /// ShowAsync's copied snapshot reaches the constructed dialog.</summary>
    [Fact]
    public void Constructor_WhenOptionsCarryStyles_AppliesThemToTheConstructedDialog()
    {
        var buttonStyle = ButtonStyle.Standard with { Padding = new Thickness(horizontal: 1, vertical: 0) };
        var checkBoxStyle = CheckBoxStyle.Default;
        var scrollBarStyle = ScrollBarStyle.Default;
        var aggregateStyle = FilePickerDialogStyle.Default with { RootPadding = new Thickness(2) };
        var options = new FilePickerOptions
        {
            CancelButtonStyle = buttonStyle,
            ShowHiddenCheckBoxStyle = checkBoxStyle,
            FileListScrollBarStyle = scrollBarStyle,
            FilterScrollBarStyle = scrollBarStyle,
            OpenButtonStyle = buttonStyle,
            Style = aggregateStyle
        };
        var copy = options.Copy();

        using var dialog = new FilePickerDialog(options, new FakeFilePickerFileSystem());

        copy.CancelButtonStyle.ShouldBe(buttonStyle);
        copy.ShowHiddenCheckBoxStyle.ShouldBe(checkBoxStyle);
        copy.FileListScrollBarStyle.ShouldBe(scrollBarStyle);
        copy.FilterScrollBarStyle.ShouldBe(scrollBarStyle);
        copy.OpenButtonStyle.ShouldBe(buttonStyle);
        copy.Style.ShouldBe(aggregateStyle);
        dialog.ActualCancelButtonStyle.ShouldBe(buttonStyle);
        dialog.ActualShowHiddenCheckBoxStyle.ShouldBe(checkBoxStyle);
        dialog.ActualFileListScrollBarStyle.ShouldBe(scrollBarStyle);
        dialog.ActualFilterScrollBarStyle.ShouldBe(scrollBarStyle);
        dialog.ActualOpenButtonStyle.ShouldBe(buttonStyle);
        dialog.ActualStyle.ShouldBe(aggregateStyle);
    }

    /// <summary>Verifies FilePickerOptions carries every caption and directory-placeholder text
    /// through construction and through Copy(), matching how ShowAsync's copied snapshot reaches
    /// the constructed dialog - the sibling coverage to the owned-part style forwarding above.</summary>
    [Fact]
    public void Constructor_WhenOptionsCarryCaptions_AppliesThemToTheConstructedDialog()
    {
        var options = new FilePickerOptions
        {
            ParentDirectoryText = "«",
            DirectoryPlaceholder = "Ruta",
            ShowHiddenText = "Mostrar &ocultos",
            CancelText = "&Salir",
            OpenText = "&Elegir"
        };
        var copy = options.Copy();

        using var dialog = new FilePickerDialog(options, new FakeFilePickerFileSystem());

        copy.ParentDirectoryText.ShouldBe("«");
        copy.DirectoryPlaceholder.ShouldBe("Ruta");
        copy.ShowHiddenText.ShouldBe("Mostrar &ocultos");
        copy.CancelText.ShouldBe("&Salir");
        copy.OpenText.ShouldBe("&Elegir");
        dialog.ParentDirectoryText.ShouldBe("«");
        dialog.DirectoryPlaceholder.ShouldBe("Ruta");
        dialog.ShowHiddenText.ShouldBe("Mostrar &ocultos");
        dialog.CancelText.ShouldBe("&Salir");
        dialog.OpenText.ShouldBe("&Elegir");
    }

    /// <summary>Verifies FilePickerOptions carries the status texts and formatters through
    /// construction and through Copy(), matching how ShowAsync's copied snapshot reaches the
    /// constructed dialog - previously these members were silently dropped by the ctor apply
    /// path even though FileDialogBase and FilePickerDialog both exposed writable properties for
    /// them.</summary>
    [Fact]
    public void Constructor_WhenOptionsCarryStatusTextsAndFormatters_AppliesThemToTheConstructedDialog()
    {
        string CountFormat(int folders, int files) => $"{folders}f/{files}d";
        string SelectionFormat(int count) => $"chosen: {count}";

        var options = new FilePickerOptions
        {
            ReadyText = "Listo",
            LoadingText = "Cargando…",
            CountFormat = CountFormat,
            SelectionFormat = SelectionFormat
        };
        var copy = options.Copy();

        using var dialog = new FilePickerDialog(options, new FakeFilePickerFileSystem());

        copy.ReadyText.ShouldBe("Listo");
        copy.LoadingText.ShouldBe("Cargando…");
        copy.CountFormat.ShouldBe(CountFormat);
        copy.SelectionFormat.ShouldBe(SelectionFormat);
        dialog.ReadyText.ShouldBe("Listo");
        dialog.LoadingText.ShouldBe("Cargando…");
        dialog.CountFormat(3, 5).ShouldBe("3f/5d");
        dialog.SelectionFormat(2).ShouldBe("chosen: 2");
    }

    /// <summary>Verifies a null FilePickerOptions status text or formatter leaves the constructed
    /// dialog's own defaults intact instead of forwarding a null value.</summary>
    [Fact]
    public void Constructor_WhenOptionsOmitStatusTextsAndFormatters_KeepsDialogDefaults()
    {
        using var dialog = new FilePickerDialog(new FilePickerOptions(), new FakeFilePickerFileSystem());

        dialog.ReadyText.ShouldBe("Ready");
        dialog.LoadingText.ShouldBe("Loading…");
        dialog.CountFormat(1, 1).ShouldBe("1 folder · 1 file");
        dialog.SelectionFormat(1).ShouldBe("1 file selected");
    }

    /// <summary>Verifies ShowAsync forwards an explicit Open Button style to the presented dialog
    /// without exposing the underlying Button instance.</summary>
    [Fact]
    public async Task ShowAsync_WhenOpenButtonStyleIsSupplied_AppliesItToThePresentedDialogAsync()
    {
        // Arrange
        var opener = new Button { Text = "Browse" };
        var host = new Overlay { Children = { opener } };
        await using var surface = await ComponentSurface.MountAsync(
            host,
            new Size(100, 40),
            TestContext.Current.CancellationToken);
        var style = ButtonStyle.Standard with { Padding = new Thickness(horizontal: 2, vertical: 1) };
        Task<FilePickerResult>? pending = null;

        // Act
        await surface.UpdateAsync(
            () => pending = FilePickerDialog.ShowAsync(opener, new FilePickerOptions { OpenButtonStyle = style }),
            "show file picker with an explicit Open Button style");
        var presentedDialogs = OwnedTree.FindAll<FilePickerDialog>(surface.Application.Root);
        var dialog = presentedDialogs.ShouldHaveSingleItem(
            $"ShowAsync should have presented exactly one FilePickerDialog under the mounted root; " +
            $"found {presentedDialogs.Count}. Full owned tree under the root: " +
            $"[{string.Join(", ", OwnedTree.FindAll<ControlBase>(surface.Application.Root).Select(static control => control.GetType().Name))}]");

        // Assert: OpenButtonStyle is a Local-only forwarding slot (StyleDefinitions.Part resolves
        // local ?? fallback(theme)), so a non-null local value like this test's style always wins
        // over the ambient Theme - this assertion should be unaffected by any other test's theme
        // or ambient state, and the diagnostic below records what actually resolved if that ever
        // stops being true.
        dialog.ActualOpenButtonStyle.ShouldBe(
            style,
            $"expected the presented dialog to resolve OpenButtonStyle from ShowAsync's options rather than " +
            $"an ambient default. Local OpenButtonStyle was " +
            $"{(dialog.OpenButtonStyle is null ? "null" : dialog.OpenButtonStyle.ToString())}; " +
            $"resolved ActualOpenButtonStyle was {dialog.ActualOpenButtonStyle}; " +
            $"expected style was {style}.");

        // Cancel to complete the pending task and let the surface tear down cleanly.
        await surface.Keyboard.PressAsync(Code.Escape);
        _ = await pending!;
    }

    /// <summary>Verifies ShowAsync rejects a null owner before constructing or attaching any dialog.</summary>
    [Fact]
    public void ShowAsync_WhenOwnerIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => FilePickerDialog.ShowAsync(null!));

    /// <summary>Verifies ShowAsync rejects a disposed owner before constructing any dialog.</summary>
    [Fact]
    public void ShowAsync_WhenOwnerIsDisposed_ThrowsObjectDisposedException()
    {
        var owner = new Button();
        owner.Dispose();

        _ = Should.Throw<ObjectDisposedException>(() => FilePickerDialog.ShowAsync(owner));
    }

    /// <summary>Verifies ShowAsync rejects an already cancelled token before constructing any dialog.</summary>
    [Fact]
    public void ShowAsync_WhenCancellationTokenIsAlreadyCancelled_ThrowsOperationCanceledException()
    {
        var owner = new Button();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        _ = Should.Throw<OperationCanceledException>(
            () => FilePickerDialog.ShowAsync(owner, cancellationToken: cancellation.Token));
    }

    /// <summary>Verifies ShowAsync rejects a detached (never attached) owner with the documented
    /// ArgumentException instead of a null-reference fault reading its Dispatcher.</summary>
    [Fact]
    public void ShowAsync_WhenOwnerIsDetached_ThrowsArgumentException()
    {
        var owner = new Button();

        var exception = Should.Throw<ArgumentException>(() => FilePickerDialog.ShowAsync(owner));

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

            var exception = Should.Throw<ArgumentException>(() => FilePickerDialog.ShowAsync(owner));

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

        _ = Should.Throw<InvalidOperationException>(() => FilePickerDialog.ShowAsync(opener));

        OwnedTree.Find<FilePickerDialog>(surface.Application.Root).ShouldBeNull();
    }

    /// <summary>Verifies the resolved aggregate style defaults to the Window fallback until an
    /// explicit local style is assigned, and that assigning one applies the root padding and
    /// file-list border on the next layout pass.</summary>
    [Fact]
    public void Style_WhenSet_OverridesFrameAndGeometryAndResetRestores()
    {
        using var dialog = new FilePickerDialog(new FilePickerOptions(), new FakeFilePickerFileSystem());
        var defaultStyle = dialog.ActualStyle;
        var style = defaultStyle with
        {
            RootPadding = new Thickness(2),
            FileListBorder = defaultStyle.FileListBorder with { GlyphStyle = BorderGlyphStyle.Heavy }
        };
        var fileListSurface = OwnedTree.Find<Dock>(dialog).ShouldNotBeNull();
        var engine = new LayoutEngine();

        dialog.Style.ShouldBeNull();

        dialog.Style = style;
        engine.Layout(dialog, new Size(100, 40));

        dialog.ActualStyle.ShouldBe(style);
        fileListSurface.Border.GlyphStyle.ShouldBe(BorderGlyphStyle.Heavy);

        dialog.Style = null;
        engine.Layout(dialog, new Size(100, 40));

        dialog.Style.ShouldBeNull();
        fileListSurface.Border.GlyphStyle.ShouldBe(defaultStyle.FileListBorder.GlyphStyle);
    }

    /// <summary>Verifies changing a caption updates the retained control owning that semantic
    /// content in place, without recreating it.</summary>
    [Fact]
    public void Captions_WhenChanged_UpdateTheRetainedControlsInPlace()
    {
        using var dialog = new FilePickerDialog(new FilePickerOptions(), new FakeFilePickerFileSystem());
        var upButton = OwnedTree.FindAll<Button>(dialog).First(static button => button.Width == Length.Cells(5));
        var openButton = OwnedTree.FindAll<Button>(dialog).Single(static button => button.IsDefault);
        var cancelButton = OwnedTree.FindAll<Button>(dialog).Single(static button => button.IsCancel);
        var hidden = OwnedTree.Find<CheckBox>(dialog).ShouldNotBeNull();
        var pathInput = OwnedTree.Find<TextInput>(dialog).ShouldNotBeNull();

        dialog.ParentDirectoryText = "«";
        dialog.OpenText = "&Choose";
        dialog.ShowHiddenText = "Mostrar &ocultos";
        dialog.DirectoryPlaceholder = "Ruta";
        dialog.CancelText = "&Salir";

        upButton.Text.ShouldBe("«");
        openButton.Text.ShouldBe("&Choose");
        hidden.Text.ShouldBe("Mostrar &ocultos");
        pathInput.Placeholder.ShouldBe("Ruta");
        cancelButton.Text.ShouldBe("&Salir");
        upButton.ShouldBeSameAs(OwnedTree.FindAll<Button>(dialog).First(static button => button.Width == Length.Cells(5)));
        cancelButton.ShouldBeSameAs(OwnedTree.FindAll<Button>(dialog).Single(static button => button.IsCancel));
    }

    /// <summary>Verifies a null caption throws before any observable mutation.</summary>
    [Fact]
    public void Captions_WhenSetToNull_ThrowBeforeMutation()
    {
        using var dialog = new FilePickerDialog(new FilePickerOptions(), new FakeFilePickerFileSystem());

        _ = Should.Throw<ArgumentNullException>(() => dialog.OpenText = null!);
        _ = Should.Throw<ArgumentNullException>(() => dialog.ParentDirectoryText = null!);
        _ = Should.Throw<ArgumentNullException>(() => dialog.ShowHiddenText = null!);
        _ = Should.Throw<ArgumentNullException>(() => dialog.CancelText = null!);
        _ = Should.Throw<ArgumentNullException>(() => dialog.DirectoryPlaceholder = null!);

        dialog.OpenText.ShouldBe("&Open");
        dialog.DirectoryPlaceholder.ShouldBe("Directory path");
    }

    /// <summary>Verifies a custom selection formatter builds the status text shown while at least
    /// one file is selected.</summary>
    [Fact]
    public async Task SelectionFormat_WhenCustomized_BuildsTheSelectionStatusTextAsync()
    {
        var directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "picker-selection-format"));
        var source = new FakeFilePickerFileSystem();
        source.AddDirectory(
            directory,
            new FilePickerEntry("a.txt", Path.Combine(directory, "a.txt"), isDirectory: false, isHidden: false));
        var dialog = new FilePickerDialog(new FilePickerOptions { InitialDirectory = directory }, source)
        {
            SelectionFormat = static count => $"{count} elegido(s)"
        };
        await using var surface = await ComponentSurface.MountAsync(
            dialog,
            new Size(100, 40),
            TestContext.Current.CancellationToken);
        var list = OwnedTree.Find<UiListView>(dialog).ShouldNotBeNull();

        await surface.UpdateAsync(() => list.SelectedIndex = 0, "select one entry");

        dialog.Status.ShouldBe("1 elegido(s)");
    }

    /// <summary>Verifies ReadyText and LoadingText default to the documented status captions and
    /// round-trip through their own getters. ReadyText only ever seeds the shared Status field
    /// once, at construction, so no further status transition observes a value assigned after
    /// construction; LoadingText's own dynamic effect on Status is covered separately below.
    /// ShowHiddenText's own default is asserted directly here too, since every other coverage of
    /// it only ever observes a customized value.</summary>
    [Fact]
    public void ReadyAndLoadingText_WhenAccessedOrChanged_DefaultAndRoundTrip()
    {
        using var dialog = new FilePickerDialog(null, new FakeFilePickerFileSystem());

        dialog.ReadyText.ShouldBe("Ready");
        dialog.LoadingText.ShouldBe("Loading…");
        dialog.ShowHiddenText.ShouldBe("Show &hidden");

        dialog.ReadyText = "Idle";
        dialog.LoadingText = "Cargando…";

        dialog.ReadyText.ShouldBe("Idle");
        dialog.LoadingText.ShouldBe("Cargando…");
    }

    /// <summary>Verifies a customized LoadingText appears as the Status shown while a directory
    /// request is outstanding, the documented dynamic effect LoadingText has beyond construction.</summary>
    [Fact]
    public async Task LoadingText_WhenCustomized_AppearsAsStatusWhileRequestIsOutstandingAsync()
    {
        // Arrange
        var directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "picker-loading-text"));
        var file = Path.Combine(directory, "a.txt");
        var source = new FakeFilePickerFileSystem();
        source.AddDirectory(directory, new FilePickerEntry("a.txt", file, isDirectory: false, isHidden: false));
        var dialog = new FilePickerDialog(new FilePickerOptions { InitialDirectory = directory }, source)
        {
            LoadingText = "Cargando…"
        };
        await using var surface = await ComponentSurface.MountAsync(
            dialog,
            new Size(80, 24),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(static () => { }, "settle initial load");
        var hidden = OwnedTree.Find<CheckBox>(dialog).ShouldNotBeNull();
        var deferred = source.DeferNext(directory);

        // Act
        await surface.UpdateAsync(() => hidden.IsChecked = true, "start deferred reload");

        // Assert
        dialog.IsLoading.ShouldBeTrue();
        dialog.Status.ShouldBe("Cargando…");

        // Cleanup: resolve the deferred load so the surface can settle before disposal.
        _ = deferred.TrySetResult([new FilePickerEntry("a.txt", file, false, false)]);
        await surface.UpdateAsync(static () => { }, "settle deferred reload");
    }

    /// <summary>Verifies start observers may detach or dispose the dialog before filesystem work
    /// exists without letting the abandoned start transaction continue into retained children.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LoadStart_WhenLoadingObserverInvalidatesDialog_DoesNotStartRequestAsync(bool dispose)
    {
        await using var dispatcher = Dispatcher.Start();
        var directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "picker-start-invalidated"));
        var source = new FakeFilePickerFileSystem();
        source.AddDirectory(directory);
        var dialog = new FilePickerDialog(new FilePickerOptions { InitialDirectory = directory }, source);
        await dispatcher.InvokeAsync(() => dialog.Attach(dispatcher), TestContext.Current.CancellationToken);

        for (var attempt = 0; attempt < 10 && dialog.IsLoading; attempt++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
            await dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);
        }

        dialog.IsLoading.ShouldBeFalse();
        var previousObservation = dialog.LastLoadObservation;
        _ = source.DeferNext(directory);
        var hidden = OwnedTree.Find<CheckBox>(dialog).ShouldNotBeNull();
        dialog.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(FilePickerDialog.IsLoading) && dialog.IsLoading)
            {
                if (dispose)
                {
                    dialog.Dispose();
                }
                else
                {
                    dialog.Detach();
                }
            }
        };

        await dispatcher.InvokeAsync(
            () => { hidden.IsChecked = !hidden.IsChecked; },
            TestContext.Current.CancellationToken);

        dialog.LastLoadObservation.ShouldBeSameAs(previousObservation);

        if (!dialog.IsDisposed)
        {
            await dispatcher.InvokeAsync(dialog.Dispose, TestContext.Current.CancellationToken);
        }
    }

    /// <summary>Verifies a throwing start observer rewinds loading state and releases the request
    /// transaction before preserving the subscriber's original exception.</summary>
    [Fact]
    public async Task LoadStart_WhenLoadingObserverThrows_DoesNotStrandLoadingStateAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "picker-start-throws"));
        var source = new FakeFilePickerFileSystem();
        source.AddDirectory(directory);
        var dialog = new FilePickerDialog(new FilePickerOptions { InitialDirectory = directory }, source);
        dialog.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(FilePickerDialog.IsLoading) && dialog.IsLoading)
            {
                throw new InvalidOperationException("loading observer failed");
            }
        };

        var failure = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await dispatcher.InvokeAsync(() => dialog.Attach(dispatcher), TestContext.Current.CancellationToken));

        failure.Message.ShouldBe("loading observer failed");
        dialog.IsLoading.ShouldBeFalse();
        dialog.LastLoadObservation.ShouldBeNull();
        await dispatcher.InvokeAsync(dialog.Dispose, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies both successful and recoverable failed completions stop immediately when
    /// the IsLoading observer detaches or disposes their dialog.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task LoadCompletion_WhenLoadingObserverInvalidatesDialog_StopsSafelyAsync(
        bool fail,
        bool dispose)
    {
        await using var dispatcher = Dispatcher.Start();
        var directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "picker-completion-invalidated"));
        var source = new FakeFilePickerFileSystem();
        source.AddDirectory(directory);
        var completion = source.DeferNext(directory);
        var dialog = new FilePickerDialog(new FilePickerOptions { InitialDirectory = directory }, source);
        var invalidated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        dialog.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName != nameof(FilePickerDialog.IsLoading) || dialog.IsLoading)
            {
                return;
            }

            if (dispose)
            {
                dialog.Dispose();
            }
            else
            {
                dialog.Detach();
            }

            _ = invalidated.TrySetResult();
        };
        await dispatcher.InvokeAsync(() => dialog.Attach(dispatcher), TestContext.Current.CancellationToken);

        _ = fail
            ? completion.TrySetException(new UnauthorizedAccessException("blocked"))
            : completion.TrySetResult([]);

        await invalidated.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        dispatcher.FatalException.ShouldBeNull();

        if (!dialog.IsDisposed)
        {
            await dispatcher.InvokeAsync(dialog.Dispose, TestContext.Current.CancellationToken);
        }
    }

    /// <summary>Verifies success and failure completions release their request and commit
    /// IsLoading=false before preserving a completion observer's thrown exception.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LoadCompletion_WhenLoadingObserverThrows_DoesNotStrandLoadingStateAsync(bool fail)
    {
        await using var dispatcher = Dispatcher.Start();
        var directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "picker-completion-throws"));
        var source = new FakeFilePickerFileSystem();
        source.AddDirectory(directory);
        var completion = source.DeferNext(directory);
        var dialog = new FilePickerDialog(new FilePickerOptions { InitialDirectory = directory }, source);
        var unhandled = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        dispatcher.UnhandledException += (_, eventArgs) =>
        {
            eventArgs.IsHandled = true;
            _ = unhandled.TrySetResult(eventArgs.Exception);
        };
        dialog.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(FilePickerDialog.IsLoading) && !dialog.IsLoading)
            {
                throw new InvalidOperationException("completion observer failed");
            }
        };
        await dispatcher.InvokeAsync(() => dialog.Attach(dispatcher), TestContext.Current.CancellationToken);

        _ = fail
            ? completion.TrySetException(new UnauthorizedAccessException("blocked"))
            : completion.TrySetResult([]);
        var failure = await unhandled.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        failure.Message.ShouldBe("completion observer failed");
        dialog.IsLoading.ShouldBeFalse();
        await dispatcher.InvokeAsync(dialog.Dispose, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a custom folder/file count formatter builds the Status text committed after
    /// a successful directory load, in place of the default "N folders · M files" wording.</summary>
    [Fact]
    public async Task CountFormat_WhenCustomized_BuildsTheReadyStatusTextAsync()
    {
        // Arrange
        var directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "picker-count-format"));
        var source = new FakeFilePickerFileSystem();
        source.AddDirectory(
            directory,
            new FilePickerEntry("docs", Path.Combine(directory, "docs"), isDirectory: true, isHidden: false),
            new FilePickerEntry("a.txt", Path.Combine(directory, "a.txt"), isDirectory: false, isHidden: false));
        var dialog = new FilePickerDialog(new FilePickerOptions { InitialDirectory = directory }, source)
        {
            CountFormat = static (folders, files) => $"{folders}d/{files}f"
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            dialog,
            new Size(80, 24),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(static () => { }, "settle customized-count load");

        // Assert
        dialog.Status.ShouldBe("1d/1f");
    }
}
