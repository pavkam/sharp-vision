// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Dialogs;

/// <summary>Proves file dialog navigation, recoverable load failures, ready-text authoring,
/// whitespace and directory-collision file names, superseded overwrite confirmations, initial
/// focus, and external cancellation over the fake file system.</summary>
public sealed class FileDialogInteractionTests
{
    #region Status and load failures

    /// <summary>Verifies an authored ReadyText is the status a dialog shows before it loads, for
    /// both dialog kinds. The base seeded the status from the default text before an authored
    /// value could be applied, so a custom ready text could never appear.</summary>
    [Fact]
    public void ReadyText_WhenAuthoredThroughOptions_IsTheInitialStatus()
    {
        // Arrange
        var directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "ready-text"));
        var source = new FakeFilePickerFileSystem();
        source.AddDirectory(directory);

        // Act
        var picker = new FilePickerDialog(
            new FilePickerOptions { InitialDirectory = directory, ReadyText = "Listo" },
            source);
        var save = new SaveFileDialog(
            new SaveFileOptions { InitialDirectory = directory, ReadyText = "Bereit" },
            source);

        // Assert
        picker.Status.ShouldBe("Listo");
        save.Status.ShouldBe("Bereit");

        // Act - a later change while still unloaded re-seeds again
        picker.ReadyText = "Prêt";

        // Assert
        picker.Status.ShouldBe("Prêt");
    }

    /// <summary>Verifies ReadyText changed after a successful load leaves the count status alone,
    /// and changed while a load is outstanding leaves the loading status alone, so an authored
    /// ready text can only ever replace the ready status it is meant for.</summary>
    [Fact]
    public async Task ReadyText_WhenChangedAfterOrDuringALoad_DoesNotClobberTheLiveStatusAsync()
    {
        // Arrange
        var directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "ready-text-live"));
        var source = new FakeFilePickerFileSystem();
        source.AddDirectory(
            directory,
            new FilePickerEntry("a.txt", Path.Combine(directory, "a.txt"), isDirectory: false, isHidden: false));
        var dialog = new FilePickerDialog(new FilePickerOptions { InitialDirectory = directory }, source);
        await using var surface = await ComponentSurface.MountAsync(
            dialog,
            new Size(76, 33),
            TestContext.Current.CancellationToken);
        await DialogWait.UntilAsync(surface, dialog, () => !dialog.IsLoading);
        dialog.Status.ShouldBe("0 folders · 1 file");

        // Act
        await surface.UpdateAsync(() => dialog.ReadyText = "Listo", "change the ready text after a load");

        // Assert
        dialog.Status.ShouldBe("0 folders · 1 file");
        StatusRow(surface, dialog, dialog.Status).ShouldBe("0 folders · 1 file");

        // Arrange - a deferred reload keeps the loading status on screen
        var deferred = source.DeferNext(directory);
        var hidden = OwnedTree.Find<CheckBox>(dialog).ShouldNotBeNull();
        await surface.Pointer.ClickAsync(hidden);
        dialog.IsLoading.ShouldBeTrue();
        dialog.Status.ShouldBe("Loading…");

        // Act
        await surface.UpdateAsync(() => dialog.ReadyText = "Prêt", "change the ready text while loading");

        // Assert
        dialog.Status.ShouldBe("Loading…");
        StatusRow(surface, dialog, dialog.Status).ShouldBe("Loading…");

        // Act
        deferred.SetResult([new FilePickerEntry("a.txt", Path.Combine(directory, "a.txt"), isDirectory: false, isHidden: false)]);
        await DialogWait.UntilAsync(surface, dialog, () => !dialog.IsLoading);

        // Assert
        dialog.Status.ShouldBe("0 folders · 1 file");
    }

    /// <summary>Verifies a whitespace-only location is rejected with a recoverable status and
    /// leaves the current directory and listing untouched.</summary>
    [Fact]
    public async Task PathSubmission_WhenWhitespaceOnly_ReportsRecoverableStatusAsync()
    {
        // Arrange
        var directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "whitespace-path"));
        var source = new FakeFilePickerFileSystem();
        source.AddDirectory(
            directory,
            new FilePickerEntry("a.txt", Path.Combine(directory, "a.txt"), isDirectory: false, isHidden: false));
        var dialog = new FilePickerDialog(new FilePickerOptions { InitialDirectory = directory }, source);
        await using var surface = await ComponentSurface.MountAsync(
            dialog,
            new Size(76, 33),
            TestContext.Current.CancellationToken);
        await DialogWait.UntilAsync(surface, dialog, () => !dialog.IsLoading);
        var path = OwnedTree.FindAll<TextInput>(dialog).Single(static input => input.Placeholder == "Directory path");
        await surface.UpdateAsync(
            () =>
            {
                surface.Application.Focus.Focus(path).ShouldBeTrue();
                path.Text = "   ";
            },
            "type a whitespace path");

        // Act
        await surface.Keyboard.PressAsync(Code.Enter);

        // Assert
        dialog.Status.ShouldStartWith("Cannot open directory:");
        dialog.CurrentDirectory.ShouldBe(directory);
        OwnedTree.Find<UiListView>(dialog).ShouldNotBeNull().Items.Count.ShouldBe(1);
        dialog.IsDisposed.ShouldBeFalse();
    }

    #endregion

    #region Parent navigation

    /// <summary>Verifies the parent button navigates up one level and is disabled at a root.</summary>
    [Fact]
    public async Task ParentButton_WhenClicked_NavigatesUpAndDisablesAtRootAsync()
    {
        // Arrange
        var root = Path.GetPathRoot(Path.GetTempPath())!;
        var parent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "up-parent"));
        var child = Path.Combine(parent, "child");
        var source = new FakeFilePickerFileSystem();
        source.AddDirectory(root);
        source.AddDirectory(parent, new FilePickerEntry("child", child, isDirectory: true, isHidden: false));
        source.AddDirectory(child);
        var dialog = new FilePickerDialog(new FilePickerOptions { InitialDirectory = child }, source);
        await using var surface = await ComponentSurface.MountAsync(
            dialog,
            new Size(76, 33),
            TestContext.Current.CancellationToken);
        await DialogWait.UntilAsync(surface, dialog, () => !dialog.IsLoading);
        var up = OwnedTree.FindAll<Button>(dialog).Single(static button => button.Text == "↑");
        up.IsEnabled.ShouldBeTrue();

        // Act
        await surface.Pointer.ClickAsync(up);
        await DialogWait.UntilAsync(surface, dialog, () => !dialog.IsLoading && dialog.CurrentDirectory == parent);

        // Assert
        dialog.CurrentDirectory.ShouldBe(parent);
        OwnedTree.Find<UiListView>(dialog).ShouldNotBeNull().Items.Count.ShouldBe(1);

        // Act - jump to the root through the location input
        var path = OwnedTree.FindAll<TextInput>(dialog).Single(static input => input.Placeholder == "Directory path");
        await surface.UpdateAsync(
            () =>
            {
                surface.Application.Focus.Focus(path).ShouldBeTrue();
                path.Text = root;
            },
            "type the root path");
        await surface.Keyboard.PressAsync(Code.Enter);
        await DialogWait.UntilAsync(surface, dialog, () => !dialog.IsLoading && dialog.CurrentDirectory == root);

        // Assert
        up.IsEnabled.ShouldBeFalse();
    }

    #endregion

    #region Save file names

    /// <summary>Verifies submitting a whitespace-only file name completes nothing and leaves the
    /// Save action disabled and the status untouched.</summary>
    [Fact]
    public async Task Save_WhenFileNameIsWhitespace_DoesNotCompleteAsync()
    {
        // Arrange
        var directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "save-whitespace"));
        var source = new FakeFilePickerFileSystem();
        source.AddDirectory(directory);
        var dialog = new SaveFileDialog(new SaveFileOptions { InitialDirectory = directory }, source);
        await using var surface = await ComponentSurface.MountAsync(
            dialog,
            new Size(76, 33),
            TestContext.Current.CancellationToken);
        await DialogWait.UntilAsync(surface, dialog, () => !dialog.IsLoading);
        var name = OwnedTree.FindAll<TextInput>(dialog).Single(static input => input.Placeholder == "File name");
        var save = OwnedTree.FindAll<Button>(dialog).Single(static button => button.Text == "&Save");
        var status = dialog.Status;
        await surface.UpdateAsync(
            () =>
            {
                surface.Application.Focus.Focus(name).ShouldBeTrue();
                name.Text = "   ";
            },
            "type a whitespace file name");

        // Act
        await surface.Keyboard.PressAsync(Code.Enter);

        // Assert
        dialog.HasSelectedResult.ShouldBeFalse();
        save.IsEnabled.ShouldBeFalse();
        dialog.Status.ShouldBe(status);
        dialog.IsDisposed.ShouldBeFalse();
    }

    /// <summary>Verifies a file name naming an existing subdirectory is refused with a status that
    /// says so.</summary>
    [Fact]
    public async Task Save_WhenFileNameNamesADirectory_ReportsDirectoryStatusAsync()
    {
        // Arrange
        var directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "save-directory-name"));
        var nested = Path.Combine(directory, "docs");
        var source = new FakeFilePickerFileSystem();
        source.AddDirectory(directory, new FilePickerEntry("docs", nested, isDirectory: true, isHidden: false));
        source.AddDirectory(nested);
        var dialog = new SaveFileDialog(new SaveFileOptions { InitialDirectory = directory }, source);
        await using var surface = await ComponentSurface.MountAsync(
            dialog,
            new Size(76, 33),
            TestContext.Current.CancellationToken);
        await DialogWait.UntilAsync(surface, dialog, () => !dialog.IsLoading);
        var name = OwnedTree.FindAll<TextInput>(dialog).Single(static input => input.Placeholder == "File name");
        await surface.UpdateAsync(
            () =>
            {
                surface.Application.Focus.Focus(name).ShouldBeTrue();
                name.Text = "docs";
            },
            "type the directory's name");

        // Act
        await surface.Keyboard.PressAsync(Code.Enter);

        // Assert
        dialog.HasSelectedResult.ShouldBeFalse();
        dialog.Status.ShouldBe("'docs' is a directory.");
        dialog.CurrentDirectory.ShouldBe(directory);
    }

    /// <summary>Verifies editing the file name while an overwrite confirmation is pending
    /// supersedes that request: a later Yes completes nothing, and a fresh save works.</summary>
    [Fact]
    public async Task Save_WhenFileNameChangesDuringOverwriteConfirmation_IgnoresTheStaleConfirmationAsync()
    {
        // Arrange
        var directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "save-superseded"));
        var existing = Path.Combine(directory, "a.txt");
        var source = new FakeFilePickerFileSystem();
        source.AddDirectory(directory, new FilePickerEntry("a.txt", existing, isDirectory: false, isHidden: false));
        var dialog = new SaveFileDialog(new SaveFileOptions { InitialDirectory = directory }, source);
        var confirmation = new TaskCompletionSource<MessageBoxResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var postReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var confirmations = 0;
        dialog.ConfirmOverwriteForLifecycleTest = () =>
        {
            confirmations++;
            return confirmation.Task;
        };
        dialog.PostAcceptanceHookForLifecycleTest = postReady.SetResult;
        await using var surface = await ComponentSurface.MountAsync(
            dialog,
            new Size(76, 33),
            TestContext.Current.CancellationToken);
        await DialogWait.UntilAsync(surface, dialog, () => !dialog.IsLoading);
        var name = OwnedTree.FindAll<TextInput>(dialog).Single(static input => input.Placeholder == "File name");
        var save = OwnedTree.FindAll<Button>(dialog).Single(static button => button.Text == "&Save");
        await surface.UpdateAsync(() => name.Text = "a.txt", "name the existing file");
        await surface.UpdateAsync(save.PerformClick, "request the save");
        confirmations.ShouldBe(1);

        // Act - supersede the pending confirmation, then answer it and wait until the acceptance
        // continuation has posted its completion before draining the dispatcher once
        await surface.UpdateAsync(() => name.Text = "b.txt", "edit the name while confirming");
        confirmation.SetResult(MessageBoxResult.Yes);
        await postReady.Task.WaitAsync(TestContext.Current.CancellationToken);
        await surface.UpdateAsync(static () => { }, "drain the stale completion");

        // Assert
        dialog.HasSelectedResult.ShouldBeFalse();
        dialog.IsDisposed.ShouldBeFalse();

        // Act - a fresh save of the new, non-existing name completes without confirmation
        await surface.UpdateAsync(save.PerformClick, "save the new name");

        // Assert
        confirmations.ShouldBe(1);
        dialog.HasSelectedResult.ShouldBeTrue();
        dialog.SelectedResult.ShouldNotBeNull().Path.ShouldBe(Path.Combine(directory, "b.txt"));
    }

    #endregion

    #region Presentation focus and cancellation

    /// <summary>Verifies a presented SaveFileDialog starts with focus in the file name input and
    /// the first successful load leaves it there.</summary>
    [Fact]
    public async Task ShowAsync_WhenSaveDialogIsPresented_FocusesFileNameInputAsync()
    {
        // Arrange
        var directory = Path.Combine(Path.GetTempPath(), $"save-focus-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(directory);
        var opener = new Button { Text = "Open" };
        var host = new Overlay { Children = { opener } };
        await using var surface = await ComponentSurface.MountAsync(
            host,
            new Size(90, 30),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => surface.Application.Focus.Focus(opener).ShouldBeTrue(), "focus opener");
        Task<SaveFileResult>? pending = null;

        // Act
        await surface.UpdateAsync(
            () => pending = SaveFileDialog.ShowAsync(opener, new SaveFileOptions { InitialDirectory = directory }),
            "show the save dialog");
        var dialog = OwnedTree.Find<SaveFileDialog>(surface.Application.Root).ShouldNotBeNull();
        await DialogWait.UntilAsync(surface, dialog, () => !dialog.IsLoading);

        // Assert
        var name = OwnedTree.FindAll<TextInput>(dialog).Single(static input => input.Placeholder == "File name");
        surface.Application.Focus.Focused.ShouldBeSameAs(name);

        // Act
        await surface.Keyboard.PressAsync(Code.Escape);

        // Assert
        (await pending!).IsConfirmed.ShouldBeFalse();
        opener.IsFocused.ShouldBeTrue();
    }

    /// <summary>Verifies a presented FilePickerDialog moves focus from the location input to the
    /// file list once the first load commits.</summary>
    [Fact]
    public async Task ShowAsync_WhenPickerIsPresented_MovesFocusToFileListAfterFirstLoadAsync()
    {
        // Arrange
        var directory = Path.Combine(Path.GetTempPath(), $"picker-focus-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "a.txt"), "a", TestContext.Current.CancellationToken);
        var opener = new Button { Text = "Open" };
        var host = new Overlay { Children = { opener } };
        await using var surface = await ComponentSurface.MountAsync(
            host,
            new Size(90, 30),
            TestContext.Current.CancellationToken);
        Task<FilePickerResult>? pending = null;

        // Act
        await surface.UpdateAsync(
            () => pending = FilePickerDialog.ShowAsync(opener, new FilePickerOptions { InitialDirectory = directory }),
            "show the picker");
        var dialog = OwnedTree.Find<FilePickerDialog>(surface.Application.Root).ShouldNotBeNull();
        await DialogWait.UntilAsync(surface, dialog, () => !dialog.IsLoading);

        // Assert
        surface.Application.Focus.Focused.ShouldBeSameAs(OwnedTree.Find<UiListView>(dialog).ShouldNotBeNull());

        // Act
        await surface.Keyboard.PressAsync(Code.Escape);

        // Assert
        (await pending!).IsAccepted.ShouldBeFalse();
    }

    /// <summary>Verifies cancelling the external token while a SaveFileDialog is presented cancels
    /// the task, removes the dialog, and restores focus to the opener.</summary>
    [Fact]
    public async Task ShowAsync_WhenSaveDialogIsCancelledExternally_CancelsTaskAndRestoresHostAsync()
    {
        // Arrange
        var directory = Path.Combine(Path.GetTempPath(), $"save-cancel-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(directory);
        var opener = new Button { Text = "Open" };
        var host = new Overlay { Children = { opener } };
        await using var surface = await ComponentSurface.MountAsync(
            host,
            new Size(90, 30),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => surface.Application.Focus.Focus(opener).ShouldBeTrue(), "focus opener");
        using var cancellation = new CancellationTokenSource();
        Task<SaveFileResult>? pending = null;
        await surface.UpdateAsync(
            () => pending = SaveFileDialog.ShowAsync(
                opener,
                new SaveFileOptions { InitialDirectory = directory },
                cancellation.Token),
            "show the save dialog");
        var dialog = OwnedTree.Find<SaveFileDialog>(surface.Application.Root).ShouldNotBeNull();
        await DialogWait.UntilAsync(surface, dialog, () => !dialog.IsLoading);

        // Act
        await cancellation.CancelAsync();
        _ = await Should.ThrowAsync<OperationCanceledException>(() => pending!);
        await surface.UpdateAsync(static () => { }, "settle the cancellation");

        // Assert
        OwnedTree.Find<SaveFileDialog>(surface.Application.Root).ShouldBeNull();
        dialog.IsDisposed.ShouldBeTrue();
        surface.Application.Modality.Active.ShouldBeNull();
        opener.IsFocused.ShouldBeTrue();
    }

    #endregion

    /// <summary>Reads the rendered status row - the cells under the dialog's status Text - as
    /// one trimmed string, so a status that was set but never repainted is caught.</summary>
    private static string StatusRow(ComponentSurface surface, ControlBase dialog, string expected)
    {
        var status = OwnedTree.FindAll<ControlText>(dialog).Single(text => text.Content == expected);
        var builder = new StringBuilder();

        for (var x = status.Bounds.X; x < status.Bounds.Right; x++)
        {
            var cell = surface.Cell(new Point(x, status.Bounds.Y));

            if (!cell.Continuation)
            {
                _ = builder.Append(cell.Text);
            }
        }

        return builder.ToString().Trim();
    }
}
