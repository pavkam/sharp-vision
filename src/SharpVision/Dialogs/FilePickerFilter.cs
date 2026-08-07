// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Dialogs;

using System.IO.Enumeration;

/// <summary>Defines one named set of basename wildcard patterns offered by a file picker.</summary>
[PublicAPI]
public sealed class FilePickerFilter
{
    /// <summary>Initializes a filter with a display name and one or more basename patterns.</summary>
    /// <param name="name">The non-null, non-blank display name.</param>
    /// <param name="patterns">One or more non-null, non-blank basename patterns using <c>*</c> and <c>?</c>.</param>
    /// <exception cref="ArgumentNullException">The name, pattern collection, or one pattern is null.</exception>
    /// <exception cref="ArgumentException">
    /// The name is blank, the collection is empty, or a pattern is blank, rooted, or contains a path separator.
    /// </exception>
    public FilePickerFilter(string name, params string[] patterns)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(patterns);

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("The filter name cannot be blank.", nameof(name));
        }

        if (patterns.Length == 0)
        {
            throw new ArgumentException("A file filter requires at least one pattern.", nameof(patterns));
        }

        var copy = new string[patterns.Length];

        for (var index = 0; index < patterns.Length; index++)
        {
            var pattern = patterns[index];
            ArgumentNullException.ThrowIfNull(patterns[index], nameof(patterns));

            if (string.IsNullOrWhiteSpace(pattern))
            {
                throw new ArgumentException("File filter patterns cannot be blank.", nameof(patterns));
            }

            if (Path.IsPathRooted(pattern) ||
                pattern.Contains(Path.DirectorySeparatorChar) ||
                pattern.Contains(Path.AltDirectorySeparatorChar))
            {
                throw new ArgumentException("File filter patterns must match a basename, not a path.", nameof(patterns));
            }

            copy[index] = pattern;
        }

        Name = name;
        Patterns = Array.AsReadOnly(copy);
    }

    /// <summary>Gets the canonical filter that matches every basename.</summary>
    public static FilePickerFilter AllFiles { get; } = new("All files", "*");

    /// <summary>Gets the non-blank display name.</summary>
    public string Name { get; }

    /// <summary>Gets the owned read-only basename-pattern snapshot.</summary>
    public IReadOnlyList<string> Patterns { get; }

    /// <summary>Determines whether one non-null basename matches any configured pattern.</summary>
    /// <param name="name">The non-null basename to compare.</param>
    /// <returns><see langword="true"/> when a pattern matches ordinally without case; otherwise, false.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
    internal bool Matches(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        foreach (var pattern in Patterns)
        {
            if (FileSystemName.MatchesSimpleExpression(pattern, name, ignoreCase: true))
            {
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc/>
    public override string ToString() => Name;
}
