// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Dialogs;

/// <summary>Provides deterministic directory-first, case-insensitive file-entry ordering.</summary>
internal sealed class FilePickerEntryComparer: IComparer<FilePickerEntry>
{
    /// <summary>Gets the shared comparer used by every file-dialog snapshot.</summary>
    internal static FilePickerEntryComparer Instance { get; } = new();

    /// <inheritdoc/>
    public int Compare(FilePickerEntry? left, FilePickerEntry? right)
    {
        if (ReferenceEquals(left, right))
        {
            return 0;
        }

        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        if (left.Directory != right.Directory)
        {
            return left.Directory ? -1 : 1;
        }

        var insensitive = StringComparer.OrdinalIgnoreCase.Compare(left.Name, right.Name);
        return insensitive != 0
            ? insensitive
            : StringComparer.Ordinal.Compare(left.Name, right.Name);
    }
}
