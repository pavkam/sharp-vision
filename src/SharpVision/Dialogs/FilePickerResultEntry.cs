// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Dialogs;

/// <summary>Pairs one <see cref="FilePickerResult"/> path with whether it names a directory, so a
/// caller of a picker running in <see cref="FileSelectionMode.Directories"/> or
/// <see cref="FileSelectionMode.FilesAndDirectories"/> can tell a returned directory apart from a
/// file without probing the filesystem again.</summary>
[PublicAPI]
public readonly record struct FilePickerResultEntry
{
    /// <summary>Initializes one accepted path paired with its directory flag.</summary>
    /// <param name="path">The non-null, fully qualified accepted path.</param>
    /// <param name="isDirectory">Whether the path names a directory rather than a file.</param>
    public FilePickerResultEntry(string path, bool isDirectory)
    {
        Path = path;
        IsDirectory = isDirectory;
    }

    /// <summary>Gets the non-null, fully qualified accepted path.</summary>
    public string Path { get; }

    /// <summary>Gets whether the path names a directory rather than a file.</summary>
    public bool IsDirectory { get; }
}
