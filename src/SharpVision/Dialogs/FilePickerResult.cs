// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Dialogs;

/// <summary>Reports whether a file picker was accepted and the owned canonical file paths selected.</summary>
[PublicAPI]
public sealed class FilePickerResult
{
    private FilePickerResult(bool accepted, IReadOnlyList<string> paths)
    {
        Accepted = accepted;
        var copy = new string[paths.Count];

        for (var index = 0; index < paths.Count; index++)
        {
            copy[index] = paths[index];
        }

        Paths = Array.AsReadOnly(copy);
    }

    /// <summary>Gets the shared cancelled result with no paths.</summary>
    public static FilePickerResult Cancelled { get; } = new(false, []);

    /// <summary>Gets whether the user accepted the picker.</summary>
    public bool Accepted { get; }

    /// <summary>Gets the owned canonical file paths in stable display order.</summary>
    public IReadOnlyList<string> Paths { get; }

    /// <summary>Gets the first selected path, or null when no path was accepted.</summary>
    public string? SelectedPath => Paths.Count == 0 ? null : Paths[0];

    /// <summary>Creates an accepted result from non-null, absolute file paths.</summary>
    /// <param name="paths">The non-null path snapshot to own.</param>
    /// <returns>A new accepted immutable result.</returns>
    /// <exception cref="ArgumentNullException">The collection or one path is null.</exception>
    /// <exception cref="ArgumentException">A path is blank or is not fully qualified.</exception>
    internal static FilePickerResult Accept(IReadOnlyList<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        if (paths.Count == 0)
        {
            throw new ArgumentException("An accepted file-picker result requires at least one path.", nameof(paths));
        }

        var copy = new string[paths.Count];

        for (var index = 0; index < paths.Count; index++)
        {
            var path = paths[index];
            ArgumentNullException.ThrowIfNull(path);

            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
            {
                throw new ArgumentException("Accepted file paths must be non-blank and fully qualified.", nameof(paths));
            }

            copy[index] = path;
        }

        return new FilePickerResult(true, copy);
    }
}
