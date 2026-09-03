// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Dialogs;

using SharpVision.Tests.Support;

/// <summary>Verifies the published IsLoading lifecycle of a mounted file picker when the file
/// system rejects a directory request synchronously, so a binding or busy indicator observing the
/// property is never left reporting a load that already failed.</summary>
public sealed class FilePickerDialogLoadStateTests
{
    /// <summary>Verifies a reload whose enumeration throws synchronously publishes IsLoading back
    /// to false before the status line reports the failure, instead of resetting the field silently
    /// and leaving observers stuck on the earlier true notification.</summary>
    [Fact]
    public async Task Reload_WhenEnumerationThrowsSynchronously_PublishesIsLoadingResetAsync()
    {
        // Arrange
        var directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "picker-sync-throw-loading"));
        var file = Path.Combine(directory, "Program.cs");
        var inner = new FakeFilePickerFileSystem();
        inner.AddDirectory(directory, new FilePickerEntry("Program.cs", file, false, false));
        var source = new SynchronouslyRejectingFileSystem(inner);
        var dialog = new FilePickerDialog(new FilePickerOptions { InitialDirectory = directory }, source);
        await using var surface = await ComponentSurface.MountAsync(
            dialog,
            new Size(80, 24),
            TestContext.Current.CancellationToken);
        await DialogWait.UntilAsync(surface, dialog, () => !dialog.IsLoading);
        var hidden = OwnedTree.Find<CheckBox>(dialog).ShouldNotBeNull();
        var observed = new List<(bool IsLoading, string Status)>();
        dialog.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(FilePickerDialog.IsLoading))
            {
                observed.Add((dialog.IsLoading, dialog.Status));
            }
        };
        source.RejectNext = new UnauthorizedAccessException("blocked");

        // Act
        await surface.UpdateAsync(() => hidden.IsChecked = true, "request a reload the file system rejects synchronously");

        // Assert - the reset is published, and it precedes the failure status
        dialog.IsLoading.ShouldBeFalse();
        observed.Select(static entry => entry.IsLoading).ShouldBe([true, false]);
        observed[1].Status.ShouldNotContain("blocked");
        dialog.Status.ShouldContain("blocked");
        dialog.CurrentDirectory.ShouldBe(directory);
    }

    /// <summary>Delegates to a fake file system and, when armed, throws the armed exception
    /// synchronously from the next entry request instead of returning a faulted task.</summary>
    private sealed class SynchronouslyRejectingFileSystem(IFilePickerFileSystem inner): IFilePickerFileSystem
    {
        /// <summary>Gets or sets the exception the next entry request throws synchronously.</summary>
        public Exception? RejectNext { get; set; }

        /// <inheritdoc/>
        public string GetFullPath(string path) => inner.GetFullPath(path);

        /// <inheritdoc/>
        public string? GetParent(string path) => inner.GetParent(path);

        /// <inheritdoc/>
        public bool FileExists(string path) => inner.FileExists(path);

        /// <inheritdoc/>
        public bool DirectoryExists(string path) => inner.DirectoryExists(path);

        /// <inheritdoc/>
        public Task<IReadOnlyList<FilePickerEntry>> GetEntriesAsync(
            string directory,
            FilePickerFilter filter,
            bool showHidden,
            CancellationToken cancellationToken)
        {
            if (RejectNext is { } exception)
            {
                RejectNext = null;
                throw exception;
            }

            return inner.GetEntriesAsync(directory, filter, showHidden, cancellationToken);
        }
    }
}
