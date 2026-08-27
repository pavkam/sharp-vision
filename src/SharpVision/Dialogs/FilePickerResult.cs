// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Dialogs;

/// <summary>Reports whether a file picker was accepted and the owned canonical paths selected -
/// files by default, or also directories when the picker's <see cref="FileSelectionMode"/>
/// allows it.</summary>
[PublicAPI]
public sealed class FilePickerResult
{
    private FilePickerResult(bool accepted, IReadOnlyList<FilePickerResultEntry> entries)
    {
        IsAccepted = accepted;
        var copy = new FilePickerResultEntry[entries.Count];

        for (var index = 0; index < entries.Count; index++)
        {
            copy[index] = entries[index];
        }

        Entries = Array.AsReadOnly(copy);
        Paths = Array.AsReadOnly(copy.Select(static entry => entry.Path).ToArray());
    }

    /// <summary>Gets the shared cancelled result with no paths.</summary>
    public static FilePickerResult Cancelled { get; } = new(false, []);

    /// <summary>Gets whether the user accepted the picker.</summary>
    public bool IsAccepted { get; }

    /// <summary>Gets the owned canonical file paths in stable display order.</summary>
    public IReadOnlyList<string> Paths { get; }

    /// <summary>Gets the owned accepted entries, pairing each canonical path with whether it names
    /// a directory, in the same stable display order as <see cref="Paths"/>. Every entry is a file
    /// unless the picker ran in <see cref="FileSelectionMode.Directories"/> or
    /// <see cref="FileSelectionMode.FilesAndDirectories"/>.</summary>
    public IReadOnlyList<FilePickerResultEntry> Entries { get; }

    /// <summary>Gets the first selected path, or null when no path was accepted.</summary>
    public string? SelectedPath => Paths.Count == 0 ? null : Paths[0];

    /// <summary>Creates an accepted result from non-null, absolute file or directory paths.</summary>
    /// <param name="entries">The non-null entry snapshot to own.</param>
    /// <returns>A new accepted immutable result.</returns>
    /// <exception cref="ArgumentNullException">The collection or one path is null.</exception>
    /// <exception cref="ArgumentException">The collection is empty, or a path is blank or is not fully qualified.</exception>
    internal static FilePickerResult Accept(IReadOnlyList<FilePickerResultEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        if (entries.Count == 0)
        {
            throw new ArgumentException("An accepted file-picker result requires at least one path.", nameof(entries));
        }

        var copy = new FilePickerResultEntry[entries.Count];

        for (var index = 0; index < entries.Count; index++)
        {
            var entry = entries[index];
            var path = entry.Path;
            ArgumentNullException.ThrowIfNull(path);

            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
            {
                throw new ArgumentException(
                    "Accepted file-picker paths must be non-blank and fully qualified.",
                    nameof(entries));
            }

            copy[index] = entry;
        }

        return new FilePickerResult(true, copy);
    }
}
