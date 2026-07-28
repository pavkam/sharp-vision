// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Dialogs;

/// <summary>Stores one immutable immediate child returned by the file-picker filesystem source.</summary>
internal sealed class FilePickerEntry
{
    /// <summary>Initializes one canonical directory or file entry.</summary>
    /// <param name="name">The non-null, non-blank basename.</param>
    /// <param name="fullPath">The non-null, fully qualified path.</param>
    /// <param name="isDirectory">Whether the entry navigates to a directory.</param>
    /// <param name="isHidden">Whether the entry follows hidden-name or hidden-attribute policy.</param>
    /// <exception cref="ArgumentNullException">A string argument is null.</exception>
    /// <exception cref="ArgumentException">The name is blank or the path is blank or not fully qualified.</exception>
    public FilePickerEntry(string name, string fullPath, bool isDirectory, bool isHidden)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(fullPath);

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A file-picker entry name cannot be blank.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(fullPath) || !Path.IsPathFullyQualified(fullPath))
        {
            throw new ArgumentException("A file-picker entry path must be fully qualified.", nameof(fullPath));
        }

        Name = name;
        FullPath = fullPath;
        IsDirectory = isDirectory;
        IsHidden = isHidden;
    }

    /// <summary>Gets the non-blank basename.</summary>
    public string Name { get; }

    /// <summary>Gets the fully qualified entry path.</summary>
    public string FullPath { get; }

    /// <summary>Gets whether this entry is a directory navigation target.</summary>
    public bool IsDirectory { get; }

    /// <summary>Gets whether this entry follows the hidden-entry policy.</summary>
    public bool IsHidden { get; }

    /// <summary>Compares entries by canonical path so refreshed directory snapshots retain selection.</summary>
    public override bool Equals(object? obj) =>
        obj is FilePickerEntry other &&
        string.Equals(FullPath, other.FullPath, StringComparison.OrdinalIgnoreCase);

    /// <summary>Returns the canonical-path hash used by snapshot selection remapping.</summary>
    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(FullPath);
}
