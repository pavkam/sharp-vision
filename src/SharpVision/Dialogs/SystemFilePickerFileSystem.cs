// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Dialogs;

/// <summary>Enumerates local filesystem entries for the standard file-picker dialog.</summary>
internal sealed class SystemFilePickerFileSystem: IFilePickerFileSystem
{
    /// <inheritdoc/>
    public string GetFullPath(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return string.IsNullOrWhiteSpace(path)
            ? throw new ArgumentException("A filesystem path cannot be blank.", nameof(path))
            : Path.GetFullPath(path);
    }

    /// <inheritdoc/>
    public string? GetParent(string path)
    {
        var canonical = GetFullPath(path);
        return Directory.GetParent(canonical)?.FullName;
    }

    /// <inheritdoc/>
    public bool FileExists(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return File.Exists(GetFullPath(path));
    }

    /// <inheritdoc/>
    public bool DirectoryExists(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return Directory.Exists(GetFullPath(path));
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<FilePickerEntry>> GetEntriesAsync(
        string directory,
        FilePickerFilter filter,
        bool showHidden,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(filter);

        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new ArgumentException("A filesystem directory cannot be blank.", nameof(directory));
        }

        var canonical = GetFullPath(directory);
        return Task.Run<IReadOnlyList<FilePickerEntry>>(
            () => Enumerate(canonical, filter, showHidden, cancellationToken),
            cancellationToken);
    }

    private static FilePickerEntry[] Enumerate(
        string directory,
        FilePickerFilter filter,
        bool showHidden,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var source = new DirectoryInfo(directory);

        if (!source.Exists)
        {
            throw new DirectoryNotFoundException($"Directory '{directory}' does not exist.");
        }

        var entries = new List<FilePickerEntry>();

        foreach (var info in source.EnumerateFileSystemInfos())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var attributes = info.Attributes;
            var isDirectory = (attributes & FileAttributes.Directory) != 0;
            var isHidden = info.Name.StartsWith('.') ||
                           (attributes & FileAttributes.Hidden) != 0;

            if ((!showHidden && isHidden) || (!isDirectory && !filter.Matches(info.Name)))
            {
                continue;
            }

            entries.Add(new FilePickerEntry(info.Name, info.FullName, isDirectory, isHidden));
        }

        entries.Sort(Compare);
        return [.. entries];
    }

    private static int Compare(FilePickerEntry left, FilePickerEntry right)
    {
        if (left.Directory != right.Directory)
        {
            return left.Directory ? -1 : 1;
        }

        var insensitive = StringComparer.OrdinalIgnoreCase.Compare(left.Name, right.Name);
        return insensitive != 0 ? insensitive : StringComparer.Ordinal.Compare(left.Name, right.Name);
    }
}
