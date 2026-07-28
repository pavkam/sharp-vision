// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Dialogs;


/// <summary>Defines real local-filesystem enumeration behavior required by the file picker.</summary>
public sealed class FilePickerFileSystemTests
{
    /// <summary>Verifies enumeration is immediate, filtered, hidden-aware, and deterministically sorted.</summary>
    [Fact]
    public async Task GetEntriesAsync_WhenDirectoryContainsMixedEntries_ReturnsDirectoriesThenMatchingFilesAsync()
    {
        // Arrange
        var directory = CreateTemporaryDirectory();

        try
        {
            _ = Directory.CreateDirectory(Path.Combine(directory, "zeta"));
            _ = Directory.CreateDirectory(Path.Combine(directory, "Alpha"));
            await File.WriteAllTextAsync(Path.Combine(directory, "b.cs"), "b", TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(Path.Combine(directory, "A.cs"), "a", TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(Path.Combine(directory, "notes.txt"), "notes", TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(Path.Combine(directory, ".secret.cs"), "secret", TestContext.Current.CancellationToken);
            var source = new SystemFilePickerFileSystem();

            // Act
            var entries = await source.GetEntriesAsync(
                directory,
                new FilePickerFilter("Sources", "*.cs"),
                showHidden: false,
                TestContext.Current.CancellationToken);

            // Assert
            entries.Select(static entry => entry.Name).ShouldBe(["Alpha", "zeta", "A.cs", "b.cs"]);
            entries.Select(static entry => entry.IsDirectory).ShouldBe([true, true, false, false]);
            entries.All(static entry => Path.IsPathFullyQualified(entry.FullPath)).ShouldBeTrue();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Verifies hidden entries appear only when requested while directories ignore file filters.</summary>
    [Fact]
    public async Task GetEntriesAsync_WhenHiddenEntriesAreRequested_IncludesDotNamesAsync()
    {
        // Arrange
        var directory = CreateTemporaryDirectory();

        try
        {
            _ = Directory.CreateDirectory(Path.Combine(directory, ".settings"));
            await File.WriteAllTextAsync(Path.Combine(directory, ".secret.cs"), "secret", TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(Path.Combine(directory, "visible.txt"), "visible", TestContext.Current.CancellationToken);
            var source = new SystemFilePickerFileSystem();

            // Act
            var entries = await source.GetEntriesAsync(
                directory,
                new FilePickerFilter("Sources", "*.cs"),
                showHidden: true,
                TestContext.Current.CancellationToken);

            // Assert
            entries.Select(static entry => entry.Name).ShouldBe([".settings", ".secret.cs"]);
            entries.All(static entry => entry.IsHidden).ShouldBeTrue();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Verifies path helpers canonicalize values and stop parent navigation at the root.</summary>
    [Fact]
    public void PathHelpers_WhenPathsAreValid_ReturnCanonicalDirectoryAndParent()
    {
        var source = new SystemFilePickerFileSystem();
        var directory = source.GetFullPath(Path.Combine(Environment.CurrentDirectory, "."));
        var root = Path.GetPathRoot(directory).ShouldNotBeNull();

        directory.ShouldBe(Path.GetFullPath(Environment.CurrentDirectory));
        source.GetParent(directory).ShouldBe(Directory.GetParent(directory)?.FullName);
        source.GetParent(root).ShouldBeNull();
    }

    /// <summary>Verifies cancellation and missing-directory failures remain observable to the dialog layer.</summary>
    [Fact]
    public async Task GetEntriesAsync_WhenRequestCannotRun_PropagatesCancellationOrDirectoryFailureAsync()
    {
        var source = new SystemFilePickerFileSystem();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        _ = await Should.ThrowAsync<OperationCanceledException>(() =>
            source.GetEntriesAsync(
                Environment.CurrentDirectory,
                FilePickerFilter.AllFiles,
                showHidden: false,
                cancellation.Token));

        var missing = Path.Combine(Path.GetTempPath(), $"sharp-vision-missing-{Guid.NewGuid():N}");
        _ = await Should.ThrowAsync<DirectoryNotFoundException>(() =>
            source.GetEntriesAsync(
                missing,
                FilePickerFilter.AllFiles,
                showHidden: false,
                TestContext.Current.CancellationToken));
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"sharp-vision-picker-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(path);
        return path;
    }
}
