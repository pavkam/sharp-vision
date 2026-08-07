// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.


namespace SharpVision.Tests.Dialogs;



/// <summary>Defines retained composition and asynchronous state behavior for FilePickerDialog.</summary>
public sealed class FilePickerDialogTests
{
    /// <summary>Verifies construction copies configuration and composes one responsive dialog Window.</summary>
    [ComponentUnitEvidence(typeof(FilePickerDialog))]
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
        dialog.Loading.ShouldBeFalse();
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
        dialog.Loading.ShouldBeFalse();
        dialog.CurrentDirectory.ShouldBe(directory);
        dialog.Status.ShouldBe("1 folder · 1 file");
        dialog.SelectedPaths.ShouldBeEmpty();
        OwnedTree.Find<UiListView>(dialog).ShouldNotBeNull().Items.Count.ShouldBe(2);
        OwnedTree.Find<TextInput>(dialog).ShouldNotBeNull().Text.ShouldBe(directory);
    }

    /// <summary>Verifies invoking a file with the pointer accepts the dialog exactly like invoking it
    /// with the keyboard, instead of only updating the selection and leaving the dialog open.</summary>
    [Fact]
    public async Task SelectionAndInvocation_WhenFileIsInvokedWithPointer_AcceptsAsync()
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
        result.Accepted.ShouldBeTrue();
        result.Paths.ShouldHaveSingleItem().ShouldBe(file);
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
        dialog.Disposed.ShouldBeFalse();
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
        await surface.UpdateAsync(
            () =>
            {
                list.SelectedIndex = 0;
                list.ActivateCurrent(ActivationCause.Keyboard, Code.Enter, Modifiers.None).ShouldBeTrue();
            },
            "invoke directory");
        await surface.UpdateAsync(static () => { }, "settle child directory");
        dialog.CurrentDirectory.ShouldBe(child);
        dialog.SelectedPaths.ShouldBeEmpty();
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
        await WaitUntilAsync(surface, () => !dialog.Loading);

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
        dialog.Loading.ShouldBeTrue();
        await surface.UpdateAsync(() => filter.SelectedIndex = 1, "replace deferred reload");
        await WaitUntilAsync(surface, () => !dialog.Loading);
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
            dialog.Loading.ShouldBeTrue();
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

    /// <summary>Verifies FilePickerOptions carries every owned-part style through construction and
    /// through Copy(), matching how ShowAsync's copied snapshot reaches the constructed dialog.</summary>
    [Fact]
    public void Constructor_WhenOptionsCarryStyles_AppliesThemToTheConstructedDialog()
    {
        var buttonStyle = ButtonStyle.Standard with { Padding = new Thickness(horizontal: 1, vertical: 0) };
        var checkBoxStyle = CheckBoxStyle.Default;
        var scrollBarStyle = ScrollBarStyle.Default;
        var options = new FilePickerOptions
        {
            CancelButtonStyle = buttonStyle,
            ShowHiddenCheckBoxStyle = checkBoxStyle,
            FileListScrollBarStyle = scrollBarStyle,
            FilterScrollBarStyle = scrollBarStyle,
            OpenButtonStyle = buttonStyle
        };
        var copy = options.Copy();

        using var dialog = new FilePickerDialog(options, new FakeFilePickerFileSystem());

        copy.CancelButtonStyle.ShouldBe(buttonStyle);
        copy.ShowHiddenCheckBoxStyle.ShouldBe(checkBoxStyle);
        copy.FileListScrollBarStyle.ShouldBe(scrollBarStyle);
        copy.FilterScrollBarStyle.ShouldBe(scrollBarStyle);
        copy.OpenButtonStyle.ShouldBe(buttonStyle);
        dialog.ActualCancelButtonStyle.ShouldBe(buttonStyle);
        dialog.ActualShowHiddenCheckBoxStyle.ShouldBe(checkBoxStyle);
        dialog.ActualFileListScrollBarStyle.ShouldBe(scrollBarStyle);
        dialog.ActualFilterScrollBarStyle.ShouldBe(scrollBarStyle);
        dialog.ActualOpenButtonStyle.ShouldBe(buttonStyle);
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
        var dialog = OwnedTree.Find<FilePickerDialog>(surface.Application.Root).ShouldNotBeNull();

        // Assert
        dialog.ActualOpenButtonStyle.ShouldBe(style);

        // Cancel to complete the pending task and let the surface tear down cleanly.
        await surface.Keyboard.PressAsync(Code.Escape);
        _ = await pending!;
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
        var hidden = OwnedTree.Find<CheckBox>(dialog).ShouldNotBeNull();
        var pathInput = OwnedTree.Find<TextInput>(dialog).ShouldNotBeNull();

        dialog.ParentDirectoryText = "«";
        dialog.OpenText = "&Choose";
        dialog.ShowHiddenText = "Mostrar &ocultos";
        dialog.DirectoryPlaceholder = "Ruta";

        upButton.Text.ShouldBe("«");
        openButton.Text.ShouldBe("&Choose");
        hidden.Text.ShouldBe("Mostrar &ocultos");
        pathInput.Placeholder.ShouldBe("Ruta");
        upButton.ShouldBeSameAs(OwnedTree.FindAll<Button>(dialog).First(static button => button.Width == Length.Cells(5)));
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

        dialog.OpenText.ShouldBe("&Open");
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
            .ShouldBeTrue("the deterministic file-picker operation should settle within 500ms");
    }
}
