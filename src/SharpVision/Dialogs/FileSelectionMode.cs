// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Dialogs;

/// <summary>Selects which entry kinds <see cref="FilePickerDialog"/> accepts into its final
/// selection. Filters remove only files regardless of this mode, so a filter and a directory-aware
/// mode compose without conflict; navigation-on-invoke (double-click or Enter on a directory)
/// always still navigates into it, in every mode.</summary>
[PublicAPI]
public enum FileSelectionMode
{
    /// <summary>Accepts only files; a selected directory is excluded from the final selection.
    /// Matches the picker's behavior before this mode existed.</summary>
    Files,

    /// <summary>Accepts only directories; a selected file is excluded from the final selection.</summary>
    Directories,

    /// <summary>Accepts both files and directories into the final selection.</summary>
    FilesAndDirectories
}
