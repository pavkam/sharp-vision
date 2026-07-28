// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Dialogs;

/// <summary>Reports whether a save-file dialog was confirmed and the owned canonical file path selected.</summary>
[PublicAPI]
public sealed class SaveFileResult
{
    private SaveFileResult(string? path) => Path = path;

    /// <summary>Gets the shared cancelled result with no path.</summary>
    public static SaveFileResult Cancelled { get; } = new(null);

    /// <summary>Gets the canonical file path, or null when cancelled.</summary>
    public string? Path { get; }

    /// <summary>Gets whether the user confirmed a save target.</summary>
    public bool Confirmed => Path is not null;

    /// <summary>Creates a confirmed result from one non-null, absolute file path.</summary>
    /// <param name="path">The non-null, fully qualified path to own.</param>
    /// <returns>A new confirmed immutable result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="path"/> is blank or is not fully qualified.</exception>
    internal static SaveFileResult FromPath(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        return string.IsNullOrWhiteSpace(path) || !System.IO.Path.IsPathFullyQualified(path)
            ? throw new ArgumentException(
                "A confirmed save-file path must be non-blank and fully qualified.",
                nameof(path))
            : new SaveFileResult(path);
    }
}
