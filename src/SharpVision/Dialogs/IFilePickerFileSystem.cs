// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Dialogs;

/// <summary>Abstracts canonical path handling and bounded immediate-child enumeration for a file picker.</summary>
internal interface IFilePickerFileSystem
{
    /// <summary>Canonicalizes one non-null, non-blank path.</summary>
    /// <param name="path">The path to canonicalize.</param>
    /// <returns>The fully qualified canonical path.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="path"/> is blank or invalid.</exception>
    public string GetFullPath(string path);

    /// <summary>Gets the canonical parent of a path.</summary>
    /// <param name="path">The non-null, non-blank path.</param>
    /// <returns>The fully qualified parent path, or null at a root.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="path"/> is blank or invalid.</exception>
    public string? GetParent(string path);

    /// <summary>Determines whether one canonical path refers to an existing file.</summary>
    /// <param name="path">The non-null path to test.</param>
    /// <returns><see langword="true"/> when the path identifies an existing file; otherwise, false.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is null.</exception>
    public bool FileExists(string path);

    /// <summary>Enumerates one directory's immediate children away from the UI dispatcher.</summary>
    /// <param name="directory">The canonical directory to enumerate.</param>
    /// <param name="filter">The non-null file filter; directories are never filtered.</param>
    /// <param name="showHidden">Whether hidden entries are included.</param>
    /// <param name="cancellationToken">Cancels enumeration.</param>
    /// <returns>A directories-first immutable entry snapshot.</returns>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="directory"/> is blank or invalid.</exception>
    /// <exception cref="DirectoryNotFoundException">The directory does not exist.</exception>
    /// <exception cref="UnauthorizedAccessException">The directory cannot be enumerated.</exception>
    /// <exception cref="IOException">The directory cannot be read.</exception>
    public Task<IReadOnlyList<FilePickerEntry>> GetEntriesAsync(
        string directory,
        FilePickerFilter filter,
        bool showHidden,
        CancellationToken cancellationToken);
}
